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

namespace LCU.State.API.NapkinIDE.User.Management
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
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var stateDetails = StateUtils.LoadStateDetails(req);

            var status = await initializeBoot(req, log, signalRMessages, stateBlob);

            if (status)
                status = await bootEnvironment(req, log, signalRMessages, stateBlob, stateDetails);

            if (status)
                status = await bootDevOps(req, log, signalRMessages, stateBlob, stateDetails);

            if (status)
                status = await bootInfrastructure(req, log, signalRMessages, stateBlob, stateDetails);

            return status;
        }
        #endregion

        #region Helpers
        protected virtual async Task<Status> bootDevOps(HttpRequest req, ILogger log, IAsyncCollector<SignalRMessage> signalRMessages,
            CloudBlockBlob stateBlob, StateDetails stateDetails)
        {
            var status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData) =>
            {
                log.LogInformation($"Configuring Project DevOps...");

                await harness.BootIaC(devOpsArch, stateDetails.EnterpriseAPIKey, stateDetails.Username);

                harness.UpdateBootOption("DevOps", 
                    status: Status.Initialized.Clone("Configuring DevOps Environment Package Feeds..."));
            });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(req, signalRMessages, log,
                    async (harness, reqData) =>
                {
                    log.LogInformation($"Configuring Project DevOps...");

                    await harness.BootLCUFeeds(devOpsArch, stateDetails.EnterpriseAPIKey, stateDetails.Username);

                    harness.UpdateBootOption("DevOps", 
                        status: Status.Initialized.Clone("Configuring DevOps Environment Task Library..."));
                });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(req, signalRMessages, log,
                    async (harness, reqData) =>
                {
                    log.LogInformation($"Configuring Project DevOps...");

                    await harness.BootTaskLibrary(devOpsArch, stateDetails.EnterpriseAPIKey, stateDetails.Username);

                    harness.UpdateBootOption("DevOps", 
                        status: Status.Initialized.Clone("Configuring DevOps Environment with Infrastructure as Code builds and releases..."));
                });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(req, signalRMessages, log,
                    async (harness, reqData) =>
                {
                    log.LogInformation($"Configuring Project DevOps...");

                    await harness.BootIaCBuildsAndReleases(devOpsArch, stateDetails.EnterpriseAPIKey, stateDetails.Username);

                    harness.UpdateBootOption("DevOps", 
                        status: Status.Success.Clone("DevOps Environment Configured"), 
                        loading: false);
                });

            return status;
        }

        protected virtual async Task<Status> bootEnvironment(HttpRequest req, ILogger log, IAsyncCollector<SignalRMessage> signalRMessages,
            CloudBlockBlob stateBlob, StateDetails stateDetails)
        {
            return await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData) =>
            {
                log.LogInformation($"Configuring Project Environment...");

                await harness.BootOrganizationEnvironment(entArch, entMgr, devOpsArch, stateDetails.EnterpriseAPIKey, stateDetails.Username);

                harness.UpdateBootOption("Project", status: Status.Success.Clone("Project Environment Configured"), loading: false);

                harness.UpdateBootOption("DevOps", status: Status.Initialized.Clone("Configuring DevOps Environment..."));
            });
        }

        protected virtual async Task<Status> bootInfrastructure(HttpRequest req, ILogger log, IAsyncCollector<SignalRMessage> signalRMessages,
            CloudBlockBlob stateBlob, StateDetails stateDetails)
        {
            return await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData) =>
            {
                log.LogInformation($"Configuring Project Environment...");

                await harness.BootDAFInfrastructure(devOpsArch, stateDetails.EnterpriseAPIKey, stateDetails.Username);

                harness.UpdateBootOption("Infrastructure", status: Status.Initialized.Clone("Deploying Environment Infrastructure..."));
            });
        }

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
