using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Fathym;
using LCU.Personas.Client.Applications;
using LCU.Personas.Client.DevOps;
using LCU.Personas.Client.Enterprises;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;

namespace LCU.State.API.NapkinIDE.UserManagement
{
    public class BootOrganizationOrchestration
    {
        #region Fields
        protected ApplicationDeveloperClient appDev;

        protected DevOpsArchitectClient devOpsArch;

        protected EnterpriseArchitectClient entArch;

        protected EnterpriseManagerClient entMgr;
        #endregion

        #region Constructors
        public BootOrganizationOrchestration(ApplicationDeveloperClient appDev, DevOpsArchitectClient devOpsArch, EnterpriseArchitectClient entArch,
            EnterpriseManagerClient entMgr)
        {
            this.appDev = appDev;

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

                        harness.UpdateBootOption("Infrastructure", status: Status.Initialized.Clone("Deploying Environment Infrastructure..."));
                    });

            return status;
        }

        [FunctionName("BootOrganizationOrchestration_Domain")]
        public virtual async Task<Status> BootDomain([ActivityTrigger] StateActionContext stateCtxt, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{stateCtxt.StateDetails.EnterpriseAPIKey}/{stateCtxt.StateDetails.HubName}/{stateCtxt.StateDetails.Username}/{stateCtxt.StateDetails.StateKey}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                {
                    log.LogInformation($"Booting host auth app...");

                    await harness.BootHostAuthApp(entArch);

                    harness.UpdateBootOption("Domain", status: Status.Initialized.Clone("Configuring Host..."));
                });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Booting host...");

                        await harness.BootHost(entArch, stateCtxt.StateDetails.EnterpriseAPIKey);

                        harness.UpdateBootOption("Domain", status: Status.Initialized.Clone("Configuring Host SSL with Let's Encrypt..."));
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Booting host SSL with Let's Encrypt...");

                        await harness.BootHostSSL(entArch, stateCtxt.StateDetails.EnterpriseAPIKey);

                        harness.UpdateBootOption("Domain", status: Status.Initialized.Clone("Host Configured"), loading: true);

                        harness.UpdateBootOption("MicroApps", status: Status.Initialized.Clone("Configuring micro-applications orechestration..."));
                    });

            return status;
        }

        [FunctionName("BootOrganizationOrchestration_MicroApps")]
        public virtual async Task<Status> BootMicroApps([ActivityTrigger] StateActionContext stateCtxt, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{stateCtxt.StateDetails.EnterpriseAPIKey}/{stateCtxt.StateDetails.HubName}/{stateCtxt.StateDetails.Username}/{stateCtxt.StateDetails.StateKey}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                {
                    log.LogInformation($"Booting micro-apps runtime...");

                    await harness.BootMicroAppsRuntime(entArch);

                    harness.UpdateBootOption("MicroApps", status: Status.Initialized.Clone("Configuring Low Code Unit Runtime ..."));
                });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Booting host...");

                        await harness.BootHost(entArch, stateCtxt.StateDetails.EnterpriseAPIKey);

                        harness.UpdateBootOption("Domain", status: Status.Initialized.Clone("Configuring Host SSL with Let's Encrypt..."));
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Booting host SSL with Let's Encrypt...");

                        await harness.BootHostSSL(entArch, stateCtxt.StateDetails.EnterpriseAPIKey);

                        harness.UpdateBootOption("Domain", status: Status.Initialized.Clone("Host Configured"), loading: true);

                        harness.UpdateBootOption("MicroApps", status: Status.Initialized.Clone("Configuring micro-applications orechestration..."));
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

                    harness.UpdateBootOption("Infrastructure", status: Status.Initialized.Clone("Committing Environment Infrastructure as Code..."));

                    harness.UpdateBootOption("Domain", status: Status.Initialized.Clone("Configuring Domain Security..."));
                });
        }
        #endregion

        #region Helpers
        #endregion
    }
}