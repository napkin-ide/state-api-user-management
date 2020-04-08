using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core.Exceptions;
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
        public virtual async Task<Status> RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var stateCtxt = context.GetInput<StateActionContext>();

            if (!context.IsReplaying)
                log.LogInformation($"Booting organization environment for: {stateCtxt.ToJSON()}");

            var genericRetryOptions = new RetryOptions(TimeSpan.FromSeconds(1), 10)
            {
                BackoffCoefficient = 1.5,
                Handle = handleRetryException
            };

            var status = Status.Initialized;

            try
            {
                status = await context.CallActivityWithRetryAsync<Status>("BootOrganizationOrchestration_Environment", genericRetryOptions, stateCtxt);

                if (status)
                {
                    if (!context.IsReplaying)
                        log.LogInformation($"Booting organization DevOps for: {stateCtxt.ToJSON()}");

                    status = await context.CallActivityWithRetryAsync<Status>("BootOrganizationOrchestration_DevOps", genericRetryOptions, stateCtxt);
                }
                else if (!context.IsReplaying)
                    log.LogError($"Booting organization environment failed for: {stateCtxt.ToJSON()}");

                if (status)
                {
                    if (!context.IsReplaying)
                        log.LogInformation($"Booting organization infrastructure for: {stateCtxt.ToJSON()}");

                    status = await context.CallActivityWithRetryAsync<Status>("BootOrganizationOrchestration_Infrastructure", genericRetryOptions, stateCtxt);
                }
                else if (!context.IsReplaying)
                    log.LogError($"Booting organization DevOps failed for: {stateCtxt.ToJSON()}");

                if (status)
                {
                    status = await handleCanFinalize(context, log, stateCtxt);
                }
                else if (!context.IsReplaying)
                    log.LogError($"Booting organization infrastructure failed for: {stateCtxt.ToJSON()}");

                if (status)
                {
                    if (!context.IsReplaying)
                        log.LogInformation($"Booting organization domain for: {stateCtxt.ToJSON()}");

                    status = await context.CallActivityWithRetryAsync<Status>("BootOrganizationOrchestration_Domain", genericRetryOptions, stateCtxt);
                }
                else if (!context.IsReplaying)
                    log.LogError($"Booting organization infrastructure never completed for: {stateCtxt.ToJSON()}");

                if (status)
                {
                    if (!context.IsReplaying)
                        log.LogInformation($"Booting organization micro-application orchestration for: {stateCtxt.ToJSON()}");

                    status = await context.CallActivityWithRetryAsync<Status>("BootOrganizationOrchestration_MicroApps", genericRetryOptions, stateCtxt);
                }
                else if (!context.IsReplaying)
                    log.LogError($"Booting organization domain failed for: {stateCtxt.ToJSON()}");

                if (status && !context.IsReplaying)
                    log.LogInformation($"Booting organization completed for: {stateCtxt.ToJSON()}");
                else if (!context.IsReplaying)
                    log.LogError($"Booting organization micro-application orchestration failed for: {stateCtxt.ToJSON()}");
            }
            catch (FunctionFailedException fex)
            {
                if (fex.InnerException is StatusException sex)
                {
                    status = Status.GeneralError.Clone(sex.Message, new { Exception = fex.InnerException.ToString() });

                    if (!context.IsReplaying)
                        log.LogInformation($"Booting organization failed: {fex.ToString()}");

                    if (stateCtxt.ActionRequest == null)
                        stateCtxt.ActionRequest = new Presentation.State.ReqRes.ExecuteActionRequest();

                    stateCtxt.ActionRequest.Arguments = status.JSONConvert<MetadataModel>();

                    status = await context.CallActivityAsync<Status>("BootOrganizationOrchestration_UpdateStatus", stateCtxt);
                }
            }

            return status;
        }

        [FunctionName("BootOrganizationOrchestration_CanFinalize")]
        public virtual async Task<Status> CanFinalize([ActivityTrigger] StateActionContext stateCtxt, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{stateCtxt.StateDetails.EnterpriseAPIKey}/{stateCtxt.StateDetails.HubName}/{stateCtxt.StateDetails.Username}/{stateCtxt.StateDetails.StateKey}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                {
                    log.LogInformation($"Checking if build and release are complete...");

                    var canFinalize = await harness.CanFinalize(entMgr, stateCtxt.StateDetails.EnterpriseAPIKey, stateCtxt.StateDetails.Username);

                    if (!canFinalize)
                        harness.UpdateBootOption("Infrastructure", status: Status.Initialized.Clone("Waiting for infrastructure deployment to finish..."));
                    else
                    {
                        harness.UpdateBootOption("Infrastructure", status: Status.Success.Clone("Infrastructure Configured and Deployed"), loading: false);

                        harness.UpdateBootOption("Domain", status: Status.Initialized.Clone("Configuring Domain Security..."));
                    }

                    return canFinalize;
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

                    var status = await harness.BootIaC(devOpsArch, stateCtxt.StateDetails.EnterpriseAPIKey, stateCtxt.StateDetails.Username);

                    if (status)
                        harness.UpdateBootOption("DevOps", status: Status.Initialized.Clone("Configuring DevOps Environment Package Feeds..."));
                    else
                        harness.UpdateBootOption("DevOps", status: Status.GeneralError.Clone("Error Configuring DevOps Environment"));

                    harness.UpdateStatus(status);

                    return status;
                });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Configuring Project DevOps...");

                        var status = await harness.BootLCUFeeds(devOpsArch, stateCtxt.StateDetails.EnterpriseAPIKey, stateCtxt.StateDetails.Username);

                        if (status)
                            harness.UpdateBootOption("DevOps", status: Status.Initialized.Clone("Configuring DevOps Environment Task Library..."));
                        else
                            harness.UpdateBootOption("DevOps", status: Status.GeneralError.Clone("Error Configuring DevOps Environment Package Feed"));

                        harness.UpdateStatus(status);

                        return status;
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Configuring Project DevOps...");

                        var status = await harness.BootTaskLibrary(devOpsArch, stateCtxt.StateDetails.EnterpriseAPIKey, stateCtxt.StateDetails.Username);

                        if (status)
                            harness.UpdateBootOption("DevOps", status: Status.Initialized.Clone("Configuring DevOps Environment with Infrastructure as Code builds and releases..."));
                        else
                            harness.UpdateBootOption("DevOps", status: Status.GeneralError.Clone("Error Configuring DevOps Environment Task Library"));

                        harness.UpdateStatus(status);

                        return status;
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Configuring Project DevOps...");

                        var status = await harness.BootIaCBuildsAndReleases(devOpsArch, stateCtxt.StateDetails.EnterpriseAPIKey, stateCtxt.StateDetails.Username);

                        if (status)
                        {
                            harness.UpdateBootOption("DevOps",
                                status: Status.Success.Clone("DevOps Environment Configured"),
                                loading: false);

                            harness.UpdateBootOption("Infrastructure", status: Status.Initialized.Clone("Deploying Environment Infrastructure..."));
                        }
                        else
                            harness.UpdateBootOption("DevOps", status: Status.GeneralError.Clone("Error Configuring DevOps Environment with Infrastructure as Code builds and releases"));

                        harness.UpdateStatus(status);

                        return status;
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

                    var status = await harness.BootHostAuthApp(entArch);

                    if (status)
                        harness.UpdateBootOption("Domain", status: Status.Initialized.Clone("Configuring Host..."));
                    else
                        harness.UpdateBootOption("Domain", status: Status.GeneralError.Clone("Error Configuring Domain Security"));

                    harness.UpdateStatus(status);

                    return status;
                });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Booting host...");

                        var status = await harness.BootHost(entArch, stateCtxt.StateDetails.EnterpriseAPIKey);

                        if (status)
                            harness.UpdateBootOption("Domain", status: Status.Initialized.Clone("Configuring Host SSL with Let's Encrypt..."));
                        else
                            harness.UpdateBootOption("Domain", status: Status.GeneralError.Clone("Error Configuring Host"));

                        harness.UpdateStatus(status);

                        return status;
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Booting host SSL with Let's Encrypt...");

                        var status = await harness.BootHostSSL(entArch, stateCtxt.StateDetails.EnterpriseAPIKey);

                        if (status)
                        {
                            harness.UpdateBootOption("Domain", status: Status.Success.Clone("Host Configured"), loading: false);

                            harness.UpdateBootOption("MicroApps", status: Status.Initialized.Clone("Configuring micro-applications orechestration runtime..."));
                        }
                        else
                            harness.UpdateBootOption("Domain", status: Status.GeneralError.Clone("Error Configuring Host SSL with Let's Encrypt"));

                        harness.UpdateStatus(status);

                        return status;
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

                    var status = await harness.BootOrganizationEnvironment(entArch, entMgr, devOpsArch, stateCtxt.StateDetails.EnterpriseAPIKey, stateCtxt.StateDetails.Username);

                    if (status)
                    {
                        harness.UpdateBootOption("Project", status: Status.Success.Clone("Project Environment Configured"), loading: false);

                        harness.UpdateBootOption("DevOps", status: Status.Initialized.Clone("Configuring DevOps Environment..."));
                    }
                    else
                        harness.UpdateBootOption("Project", status: Status.GeneralError.Clone("Error Configuring Project Environment"));

                    harness.UpdateStatus(status);

                    return status;
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
                    log.LogInformation($"Configuring Project Infrastructure...");

                    var status = await harness.BootDAFInfrastructure(devOpsArch, stateCtxt.StateDetails.EnterpriseAPIKey, stateCtxt.StateDetails.Username);

                    if (status)
                        harness.UpdateBootOption("Infrastructure", status: Status.Initialized.Clone("Committing Environment Infrastructure as Code..."));
                    else
                        harness.UpdateBootOption("Infrastructure", status: Status.GeneralError.Clone("Error Configuring Project Infrastructure"));

                    harness.UpdateStatus(status);

                    return status;
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

                    var status = await harness.BootMicroAppsRuntime(entArch);

                    if (status)
                        harness.UpdateBootOption("MicroApps", status: Status.Initialized.Clone("Configuring Data Apps Low-Code Unit™..."));
                    else
                        harness.UpdateBootOption("MicroApps", status: Status.GeneralError.Clone("Error Configuring Micro-Applicatinos Runtime"));

                    harness.UpdateStatus(status);

                    return status;
                });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Booting Data Applications Low-Code Unit™...");

                        var status = await harness.BootDataApps(appDev);

                        if (status)
                            harness.UpdateBootOption("MicroApps", status: Status.Initialized.Clone("Configuring Data Applications Low-Code Unit™..."));
                        else
                            harness.UpdateBootOption("MicroApps", status: Status.GeneralError.Clone("Error Configuring Data Applications Low-Code Unit™"));

                        harness.UpdateStatus(status);

                        return status;
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Booting Data Flow Low-Code Unit™...");

                        var status = await harness.BootDataFlow(appDev);

                        if (status)
                        {
                            harness.UpdateBootOption("MicroApps", status: Status.Success.Clone("Micro-Applications Orechestration Configured"), loading: false);

                            harness.CompleteBoot();
                        }
                        else
                            harness.UpdateBootOption("MicroApps", status: Status.GeneralError.Clone("Error Configuring Data Flow Low-Code Unit™"));

                        harness.UpdateStatus(status);

                        return status;
                    });

            return status;
        }

        [FunctionName("BootOrganizationOrchestration_UpdateStatus")]
        public virtual async Task<Status> UpdateStatus([ActivityTrigger] StateActionContext stateCtxt, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{stateCtxt.StateDetails.EnterpriseAPIKey}/{stateCtxt.StateDetails.HubName}/{stateCtxt.StateDetails.Username}/{stateCtxt.StateDetails.StateKey}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, Status, UserManagementStateHarness>(stateCtxt.StateDetails,
                stateCtxt.ActionRequest, signalRMessages, log, async (harness, status) =>
                {
                    log.LogInformation($"Booting micro-apps runtime...");

                    harness.UpdateStatus(status);

                    return Status.Success;
                });
        }
        #endregion

        #region Helpers
        protected virtual bool handleRetryException(Exception ex)
        {
            if (ex is TaskFailedException tex)
            {
                if (tex.InnerException is StatusException sex)
                    return true;
                else
                    return false;
            }
            else
                return false;
        }

        protected virtual async Task<Status> handleCanFinalize(IDurableOrchestrationContext context, ILogger log, StateActionContext stateCtxt)
        {
            var canFinalize = Status.GeneralError;

            var operationTimeoutTime = context.CurrentUtcDateTime.AddMinutes(30);

            // using (var timeoutCts = new CancellationTokenSource())
            // {
            //     while (true)
            //     {
            if (!context.IsReplaying)
                log.LogInformation($"Waiting for organization infrastructure to boot for: {stateCtxt.ToJSON()}");

            // var operationHasTimedOut = context.CurrentUtcDateTime > operationTimeoutTime;

            // if (operationHasTimedOut)
            // {
            //     if (!context.IsReplaying)
            //         log.LogError($"Booting organization infrastructure timedout for: {stateCtxt.ToJSON()}");

            //     context.SetCustomStatus("Organization Booting has timed out, please try again.");

            //     break;
            // }

            // // if (!context.IsReplaying)
            // {
            //     var deadline = context.CurrentUtcDateTime.AddSeconds(10);

            //     if (!context.IsReplaying)
            //         log.LogInformation($"Establishing delay timer until {deadline} for: {stateCtxt.ToJSON()}");

            //     await context.CreateTimer(deadline, 0, timeoutCts.Token);
            // }

            var retryOptions = new RetryOptions(TimeSpan.FromSeconds(10), 360)
            {
                Handle = handleRetryException,
                RetryTimeout = TimeSpan.FromMinutes(30)
            };

            canFinalize = await context.CallActivityWithRetryAsync<Status>("BootOrganizationOrchestration_CanFinalize", retryOptions, stateCtxt);

            // if (canFinalize)
            // {
            //     timeoutCts.Cancel();

            //     break;
            // }
            //     }
            // }

            return canFinalize;
        }
        #endregion
    }
}