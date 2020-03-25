using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Fathym;
using LCU.Personas.Client.Applications;
using LCU.Personas.Client.DevOps;
using LCU.Personas.Client.Enterprises;
using LCU.StateAPI;
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
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var stateCtxt = context.GetInput<StateActionContext>();

            if (!context.IsReplaying)
                log.LogInformation($"Booting organization environment for: {stateCtxt.ToJSON()}");

            var status = await context.CallActivityAsync<Status>("BootOrganizationOrchestration_Environment", stateCtxt);

            if (status)
            {
                if (!context.IsReplaying)
                    log.LogInformation($"Booting organization DevOps for: {stateCtxt.ToJSON()}");

                status = await context.CallActivityAsync<Status>("BootOrganizationOrchestration_DevOps", stateCtxt);
            }
            else if (!context.IsReplaying)
                log.LogError($"Booting organization environment failed for: {stateCtxt.ToJSON()}");

            if (status)
            {
                if (!context.IsReplaying)
                    log.LogInformation($"Booting organization infrastructure for: {stateCtxt.ToJSON()}");

                status = await context.CallActivityAsync<Status>("BootOrganizationOrchestration_Infrastructure", stateCtxt);
            }
            else if (!context.IsReplaying)
                log.LogError($"Booting organization DevOps failed for: {stateCtxt.ToJSON()}");


            var canFinalize = Status.GeneralError;

            var operationTimeoutTime = context.CurrentUtcDateTime.AddMinutes(30);

            if (status)
            {
                using (var timeoutCts = new CancellationTokenSource())
                {
                    while (true)
                    {
                        if (!context.IsReplaying)
                            log.LogInformation($"Waiting for organization infrastructure to boot for: {stateCtxt.ToJSON()}");

                        var operationHasTimedOut = context.CurrentUtcDateTime > operationTimeoutTime;

                        if (operationHasTimedOut)
                        {
                            if (!context.IsReplaying)
                                log.LogError($"Booting organization infrastructure timedout for: {stateCtxt.ToJSON()}");

                            context.SetCustomStatus("Organization Booting has timed out, please try again.");

                            break;
                        }

                        // if (!context.IsReplaying)
                        {
                            var deadline = context.CurrentUtcDateTime.AddSeconds(10);

                            if (!context.IsReplaying)
                                log.LogInformation($"Establishing delay timer until {deadline} for: {stateCtxt.ToJSON()}");

                            await context.CreateTimer(deadline, 0, timeoutCts.Token);
                        }

                        var stuff = await context.CallActivityAsync<dynamic>("BootOrganizationOrchestration_CanFinalize", stateCtxt);

                        string stuffs = stuff.ToString();

                        canFinalize = stuffs.FromJSON<Status>();

                        if (canFinalize)
                        {
                            timeoutCts.Cancel();

                            break;
                        }
                    }
                }
            }
            else if (!context.IsReplaying)
                log.LogError($"Booting organization infrastructure failed for: {stateCtxt.ToJSON()}");

            if (status)
            {
                if (!context.IsReplaying)
                    log.LogInformation($"Booting organization domain for: {stateCtxt.ToJSON()}");

                status = await context.CallActivityAsync<Status>("BootOrganizationOrchestration_Domain", stateCtxt);
            }
            else if (!context.IsReplaying)
                log.LogError($"Booting organization infrastructure never completed for: {stateCtxt.ToJSON()}");

            if (status)
            {
                if (!context.IsReplaying)
                    log.LogInformation($"Booting organization micro-application orchestration for: {stateCtxt.ToJSON()}");

                status = await context.CallActivityAsync<Status>("BootOrganizationOrchestration_MicroApps", stateCtxt);
            }
            else if (!context.IsReplaying)
                log.LogError($"Booting organization domain failed for: {stateCtxt.ToJSON()}");

            if (status && !context.IsReplaying)
                log.LogInformation($"Booting organization completed for: {stateCtxt.ToJSON()}");
            else if (!context.IsReplaying)
                log.LogError($"Booting organization micro-application orchestration failed for: {stateCtxt.ToJSON()}");

            return status;
        }

        [FunctionName("BootOrganizationOrchestration_CanFinalize")]
        public virtual async Task<Status> CanFinalize([ActivityTrigger] StateActionContext stateCtxt, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{stateCtxt.StateDetails.EnterpriseAPIKey}/{stateCtxt.StateDetails.HubName}/{stateCtxt.StateDetails.Username}/{stateCtxt.StateDetails.StateKey}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var canFinalize = Status.GeneralError;

            await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                {
                    log.LogInformation($"Checking if build and release are complete...");

                    canFinalize = await harness.CanFinalize(entMgr, stateCtxt.StateDetails.EnterpriseAPIKey, stateCtxt.StateDetails.Username);

                    if (!canFinalize)
                        harness.UpdateBootOption("Infrastructure", status: Status.Initialized.Clone("Waiting for infrastructure deployment to finish..."));
                    else
                    {
                        harness.UpdateBootOption("Infrastructure", status: Status.Success.Clone("Infrastructure Configured and Deployed"), loading: false);

                        harness.UpdateBootOption("Domain", status: Status.Initialized.Clone("Configuring Domain Security..."));
                    }
                });

            return canFinalize;
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

                        harness.UpdateBootOption("Domain", status: Status.Success.Clone("Host Configured"), loading: false);

                        harness.UpdateBootOption("MicroApps", status: Status.Initialized.Clone("Configuring micro-applications orechestration runtime..."));
                    });

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
                });
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

                    harness.UpdateBootOption("MicroApps", status: Status.Initialized.Clone("Configuring Data Apps Low Code Unit..."));
                });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Booting data apps Low Code Unit...");

                        await harness.BootDataApps(appDev);

                        harness.UpdateBootOption("MicroApps", status: Status.Initialized.Clone("Configuring Data Flow Low Code Unit..."));
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Booting data flow Low Code Unit...");

                        await harness.BootDataFlow(appDev);

                        harness.UpdateBootOption("MicroApps", status: Status.Success.Clone("Micro-Applications Orechestration Configured"), loading: false);

                        harness.CompleteBoot();
                    });

            return status;
        }
        #endregion

        #region Helpers
        #endregion
    }
}