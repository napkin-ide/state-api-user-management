using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.WindowsAzure.Storage.Blob;
using Fathym;
using System.Runtime.Serialization;
using LCU.Personas.Client.Enterprises;
using LCU.Personas.Client.DevOps;
using LCU.StateAPI.Utilities;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Net.Http;
using LCU.Presentation.State.ReqRes;
using LCU.StateAPI;

namespace LCU.State.API.NapkinIDE.UserManagement
{
    [Serializable]
    [DataContract]
    public class BootOrganizationRequest
    {
    }

    public class BootOrganization
    {
        #region Fields
        protected DevOpsArchitectClient devOpsArch;

        protected EnterpriseArchitectClient entArch;

        protected EnterpriseManagerClient entMgr;
        #endregion

        #region Constructors
        public BootOrganization(EnterpriseArchitectClient entArch, EnterpriseManagerClient entMgr, DevOpsArchitectClient devOpsArch)
        {
            this.devOpsArch = devOpsArch;

            this.entArch = entArch;

            this.entMgr = entMgr;
        }
        #endregion

        #region API Methods
        [FunctionName("BootOrganization")]
        public virtual async Task<IActionResult> Run([HttpTrigger] HttpRequest req, ILogger log,
            [DurableClient] IDurableOrchestrationClient starter,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var stateDetails = StateUtils.LoadStateDetails(req);

            await initializeBoot(req, log, signalRMessages, stateBlob);

            var instanceId = $"{stateDetails.EnterpriseAPIKey}-{stateDetails.HubName}-{stateDetails.Username}-{stateDetails.StateKey}";

            var instanceStatus = await starter.GetStatusAsync(instanceId, false);

            if (instanceStatus != null && (instanceStatus.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew ||
                instanceStatus.RuntimeStatus == OrchestrationRuntimeStatus.Pending || instanceStatus.RuntimeStatus == OrchestrationRuntimeStatus.Running))
                await starter.TerminateAsync(instanceId, "Restarting orchestration");

            instanceId = await starter.StartNewAsync("BootOrganizationOrchestration", instanceId, new StateActionContext()
            {
                ActionRequest = await req.LoadBody<ExecuteActionRequest>(),
                StateDetails = stateDetails
            });

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
        #endregion

        #region Helpers
        protected virtual async Task<Status> initializeBoot(HttpRequest req, ILogger log, IAsyncCollector<SignalRMessage> signalRMessages,
            CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData) =>
            {
                harness.SetBootOptionsLoading();

                harness.UpdateBootOption("Project", status: Status.Initialized.Clone("Configuring Project Environment..."));
            });
        }
        #endregion
    }
}
