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
using Microsoft.Azure.Storage.Blob;
using Fathym;
using System.Runtime.Serialization;
using LCU.Personas.Client.Enterprises;
using LCU.Personas.Client.DevOps;
using LCU.StateAPI.Utilities;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Net.Http;
using LCU.Presentation.State.ReqRes;
using LCU.StateAPI;
using LCU.State.API.NapkinIDE.UserManagement.State;

namespace LCU.State.API.NapkinIDE.UserManagement.Management
{
    [Serializable]
    [DataContract]
    public class BootOrganizationRequest
    {
    }

    public class BootOrganization
    {
        #region Fields
        #endregion

        #region Constructors
        public BootOrganization()
        { }
        #endregion

        #region API Methods
        // [FunctionName("BootOrganization")]
        // public virtual async Task<IActionResult> Run([HttpTrigger] HttpRequest req, ILogger log,
        //     [DurableClient] IDurableOrchestrationClient starter,
        //     [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
        //     [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        // {
        //     // await initializeBoot(req, log, signalRMessages, stateBlob);

        //     // return await starter.StartAction("BootOrganizationOrchestration", req, log);
        // }
        #endregion

        #region Helpers
        protected virtual async Task<Status> initializeBoot(HttpRequest req, ILogger log, IAsyncCollector<SignalRMessage> signalRMessages,
            CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData) =>
            {
                // harness.SetBootOptionsLoading();

                // harness.UpdateBootOption("Environment", 1, status: Status.Initialized.Clone("Configuring Workspace Environment..."));

                // harness.UpdateStatus(null);

                return Status.Success;
            });
        }
        #endregion
    }
}
