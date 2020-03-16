using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Fathym;
using LCU.Personas.Client.DevOps;
using LCU.Personas.Client.Enterprises;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;

namespace LCU.State.API.NapkinIDE.User.Management
{
    public class BootOrganizationOrchestration
    {
        #region Fields
        protected DevOpsArchitectClient devOpsArch;

        protected EnterpriseArchitectClient entArch;

        protected EnterpriseManagerClient entMgr;
        #endregion

        #region Constructors
        public BootOrganizationOrchestration(EnterpriseArchitectClient entArch, EnterpriseManagerClient entMgr, DevOpsArchitectClient devOpsArch)
        {
            this.devOpsArch = devOpsArch;

            this.entArch = entArch;

            this.entMgr = entMgr;
        }
        #endregion

        #region API Methods
        [FunctionName("BootOrganizationOrchestration")]
        public virtual async Task<Status> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var stateCtxt = context.GetInput<StateActionContext>();

            var status = await context.CallActivityAsync<Status>("BootOrganizationOrchestration_Environment", stateCtxt);

            if (status)
                status = await context.CallActivityAsync<Status>("BootOrganizationOrchestration_DevOps", stateCtxt);

            if (status)
                status = await context.CallActivityAsync<Status>("BootOrganizationOrchestration_Infrastructure", stateCtxt);

            return status;
        }

        [FunctionName("BootOrganizationOrchestration_Environment")]
        public virtual async Task<Status> BootEnvironment([ActivityTrigger] StateActionContext stateCtxt, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{stateCtxt.StateDetails.EnterpriseAPIKey}/{stateCtxt.StateDetails.HubName}/{stateCtxt.StateDetails.Username}/{stateCtxt.StateDetails.StateKey}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                {
                    log.LogInformation($"Configuring Project Environment...");

                    await harness.BootOrganizationEnvironment(entArch, entMgr, devOpsArch, stateCtxt.StateDetails.EnterpriseAPIKey, stateCtxt.StateDetails.Username);

                    harness.UpdateBootOption("Project", status: Status.Success.Clone("Project Environment Configured"), loading: false);

                    harness.UpdateBootOption("DevOps", status: Status.Initialized.Clone("Configuring DevOps Environment..."));
                });
        }

        [FunctionName("BootOrganizationOrchestration_DevOps")]
        public virtual async Task<Status> BootDevOps([ActivityTrigger] StateActionContext stateCtxt, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{stateCtxt.StateDetails.EnterpriseAPIKey}/{stateCtxt.StateDetails.HubName}/{stateCtxt.StateDetails.Username}/{stateCtxt.StateDetails.StateKey}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                {
                    log.LogInformation($"Configuring Project DevOps...");

                    await harness.BootIaC(devOpsArch, stateCtxt.StateDetails.EnterpriseAPIKey, stateCtxt.StateDetails.Username);

                    harness.UpdateBootOption("DevOps",
                        status: Status.Initialized.Clone("Configuring DevOps Environment Package Feeds..."));
                });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Configuring Project DevOps...");

                        await harness.BootLCUFeeds(devOpsArch, stateCtxt.StateDetails.EnterpriseAPIKey, stateCtxt.StateDetails.Username);

                        harness.UpdateBootOption("DevOps",
                            status: Status.Initialized.Clone("Configuring DevOps Environment Task Library..."));
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Configuring Project DevOps...");

                        await harness.BootTaskLibrary(devOpsArch, stateCtxt.StateDetails.EnterpriseAPIKey, stateCtxt.StateDetails.Username);

                        harness.UpdateBootOption("DevOps",
                            status: Status.Initialized.Clone("Configuring DevOps Environment with Infrastructure as Code builds and releases..."));
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Configuring Project DevOps...");

                        await harness.BootIaCBuildsAndReleases(devOpsArch, stateCtxt.StateDetails.EnterpriseAPIKey, stateCtxt.StateDetails.Username);

                        harness.UpdateBootOption("DevOps",
                            status: Status.Success.Clone("DevOps Environment Configured"),
                            loading: false);
                    });

            return status;
        }
       
        [FunctionName("BootOrganizationOrchestration_Infrastructure")]
        public virtual async Task<Status> BootInfrastructure([ActivityTrigger] StateActionContext stateCtxt, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{stateCtxt.StateDetails.EnterpriseAPIKey}/{stateCtxt.StateDetails.HubName}/{stateCtxt.StateDetails.Username}/{stateCtxt.StateDetails.StateKey}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                {
                    log.LogInformation($"Configuring Project Environment...");

                    await harness.BootDAFInfrastructure(devOpsArch, stateCtxt.StateDetails.EnterpriseAPIKey, stateCtxt.StateDetails.Username);

                    harness.UpdateBootOption("Infrastructure", status: Status.Initialized.Clone("Deploying Environment Infrastructure..."));
                });
        }
        #endregion

        #region Helpers
        #endregion
    }
}