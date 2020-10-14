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
using LCU.Personas.DevOps;
using LCU.State.API.NapkinIDE.UserManagement.State;
using LCU.StateAPI;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Storage.Blob;

namespace LCU.State.API.NapkinIDE.UserManagement.Management
{
    public class BootOrganizationOrchestration
    {
        #region Fields
        protected ApplicationDeveloperClient appDev;

        protected ApplicationManagerClient appMgr;

        protected DevOpsArchitectClient devOpsArch;

        protected EnterpriseArchitectClient entArch;

        protected EnterpriseManagerClient entMgr;
        #endregion

        #region Constructors
        public BootOrganizationOrchestration(ApplicationDeveloperClient appDev, ApplicationManagerClient appMgr, DevOpsArchitectClient devOpsArch,
            EnterpriseArchitectClient entArch, EnterpriseManagerClient entMgr)
        {
            this.appDev = appDev;

            this.appMgr = appMgr;

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
                    // status = Status.GeneralError.Clone("Unable to finish booting organization, please contact support.", new { Exception = fex.InnerException.ToString() });

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
            [SignalR(HubName = UserManagementState.HUB_NAME)] IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{stateCtxt.StateDetails.EnterpriseLookup}/{stateCtxt.StateDetails.HubName}/{stateCtxt.StateDetails.Username}/{stateCtxt.StateDetails.StateKey}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                {
                    log.LogInformation($"Checking if build and release are complete...");

                    var canFinalize = await harness.CanFinalize(devOpsArch, stateCtxt.StateDetails.Username);

                    if (canFinalize)
                    {
                        harness.UpdateBootOption("Infrastructure", 4, status: canFinalize.Clone("Infrastructure Configured and Deployed"), loading: false);

                        harness.UpdateBootOption("Domain", 1, status: Status.Initialized.Clone("Configuring Domain Security..."));
                    }
                    else
                    {
                        var infraStatus = canFinalize;//.Clone("Waiting for infrastructure deployment to finish...");

                        infraStatus.Code = Status.Initialized.Code;

                        harness.UpdateBootOption("Infrastructure", 3, status: Status.Initialized.Clone("Building and Releasing Environment Infrastructure..."));
                    }

                    return canFinalize;
                });//, preventStatusException: true);
        }

        [FunctionName("BootOrganizationOrchestration_DevOps")]
        public virtual async Task<Status> BootDevOps([ActivityTrigger] StateActionContext stateCtxt, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)] IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{stateCtxt.StateDetails.EnterpriseLookup}/{stateCtxt.StateDetails.HubName}/{stateCtxt.StateDetails.Username}/{stateCtxt.StateDetails.StateKey}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            //  Ensure DevOps Project
            var status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                {
                    log.LogInformation($"Configuring DevOps Project...");

                    var status = await harness.EnsureDevOpsProject(devOpsArch, stateCtxt.StateDetails.Username);

                    if (status)
                        harness.UpdateBootOption("DevOps", 2, status: Status.Initialized.Clone("Configuring DevOps Repositories..."));
                    else
                        harness.UpdateBootOption("DevOps", 1, status: Status.GeneralError.Clone("Error Configuring DevOps Project, retrying."));

                    harness.UpdateStatus(status);

                    return status;
                });

            //  Ensure Repositories
            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Configuring DevOps Repositories...");

                        var status = await harness.BootRepositories(devOpsArch, stateCtxt.StateDetails.Username);

                        if (status)
                            harness.UpdateBootOption("DevOps", 3, status: Status.Initialized.Clone("Configuring DevOps Feed..."));
                        else
                            harness.UpdateBootOption("DevOps", 2, status: Status.GeneralError.Clone("Error Configuring DevOps Repositories, retrying."));

                        harness.UpdateStatus(status);

                        return status;
                    });

            //  Ensure Feed
            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Configuring DevOps Feeds...");

                        var status = await harness.BootFeeds(devOpsArch, stateCtxt.StateDetails.Username);

                        if (status)
                            harness.UpdateBootOption("DevOps", 4, status: Status.Initialized.Clone("Configuring DevOps Service Endpoints..."));
                        else
                            harness.UpdateBootOption("DevOps", 3, status: Status.GeneralError.Clone("Error Configuring DevOps Feeds, retrying."));

                        harness.UpdateStatus(status);

                        return status;
                    });

            //  Ensure Service Endpoints
            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Configuring DevOps Service Endpoints...");

                        var status = await harness.BootServiceEndpoints(devOpsArch, stateCtxt.StateDetails.Username);

                        if (status)
                            harness.UpdateBootOption("DevOps", 5, status: Status.Initialized.Clone("Configuring DevOps Task Library..."));
                        else
                            harness.UpdateBootOption("DevOps", 4, status: Status.GeneralError.Clone("Error Configuring DevOps Service Endpoints, retrying."));

                        harness.UpdateStatus(status);

                        return status;
                    });

            //  Ensure Task Library
            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Configuring DevOps Task Library...");

                        var status = await harness.BootTaskLibrary(devOpsArch, stateCtxt.StateDetails.Username);

                        if (status)
                            harness.UpdateBootOption("DevOps", 6, status: Status.Initialized.Clone("Configuring DevOps Build Definitions..."));
                        else
                            harness.UpdateBootOption("DevOps", 5, status: Status.GeneralError.Clone("Error Configuring DevOps Task Library, retrying."));

                        harness.UpdateStatus(status);

                        return status;
                    });

            //  Ensure Build Definitions
            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Configuring DevOps Build Definitions...");

                        var status = await harness.BootBuildDefinitions(devOpsArch, stateCtxt.StateDetails.Username);

                        if (status)
                            harness.UpdateBootOption("DevOps", 7, status: Status.Initialized.Clone("Configuring DevOps Release Definitions..."));
                        else
                            harness.UpdateBootOption("DevOps", 6, status: Status.GeneralError.Clone("Error Configuring DevOps Build Definitions, retrying."));

                        harness.UpdateStatus(status);

                        return status;
                    });

            //  Ensure Release Definitions
            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Configuring DevOps Release Definitions...");

                        var status = await harness.BootReleaseDefinitions(devOpsArch, stateCtxt.StateDetails.Username);

                        if (status)
                            harness.UpdateBootOption("DevOps", 8, status: Status.Initialized.Clone("Setting Up DevOps Repositories..."));
                        else
                            harness.UpdateBootOption("DevOps", 7, status: Status.GeneralError.Clone("Error Configuring DevOps Release Definitions, retrying."));

                        harness.UpdateStatus(status);

                        return status;
                    });

            //  Setup Repositories
            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Setting Up DevOps Repositories...");

                        var status = await harness.SetupRepositories(devOpsArch, stateCtxt.StateDetails.Username);

                        if (status)
                            harness.UpdateBootOption("DevOps", 9, status: Status.Success.Clone("Verifying DevOps Setup"));
                        else
                            harness.UpdateBootOption("DevOps", 8, status: Status.GeneralError.Clone("Error Configuring DevOps Environment with Infrastructure as Code builds and releases, retrying."));

                        harness.UpdateStatus(status);

                        return status;
                    });

            //  Verify DevOps setup
            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Verifying DevOps Setup...");

                        var status = await harness.VerifyDevOpsSetup(entMgr, devOpsArch);

                        if (status)
                        {
                            harness.UpdateBootOption("DevOps", 10, status: Status.Success.Clone("DevOps Environment Configured"), loading: false);

                            harness.UpdateBootOption("Infrastructure", 1, status: Status.Initialized.Clone("Deploying Environment Infrastructure..."));
                        }
                        else
                            harness.UpdateBootOption("DevOps", 9, status: Status.GeneralError.Clone("Error Verifying DevOps Environment, retrying."));

                        harness.UpdateStatus(status);

                        return status;
                    });

            return status;
        }

        [FunctionName("BootOrganizationOrchestration_Domain")]
        public virtual async Task<Status> BootDomain([ActivityTrigger] StateActionContext stateCtxt, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)] IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{stateCtxt.StateDetails.EnterpriseLookup}/{stateCtxt.StateDetails.HubName}/{stateCtxt.StateDetails.Username}/{stateCtxt.StateDetails.StateKey}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                {
                    log.LogInformation($"Booting host auth app...");

                    var status = await harness.BootHostAuthApp(entArch);

                    if (status)
                        harness.UpdateBootOption("Domain", 2, status: Status.Initialized.Clone("Configuring Host..."));
                    else
                        harness.UpdateBootOption("Domain", 1, status: Status.GeneralError.Clone("Error Configuring Domain Security, retrying."));

                    harness.UpdateStatus(status);

                    return status;
                });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Booting host...");

                        var status = await harness.BootHost(entArch, stateCtxt.StateDetails.EnterpriseLookup);

                        if (status)
                            harness.UpdateBootOption("Domain", 3, status: Status.Initialized.Clone("Configuring Host SSL with Let's Encrypt..."));
                        else
                            harness.UpdateBootOption("Domain", 2, status: Status.GeneralError.Clone("Error Configuring Host, retrying."));

                        harness.UpdateStatus(status);

                        return status;
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Booting host SSL with Let's Encrypt...");

                        var status = await harness.BootHostSSL(entArch, stateCtxt.StateDetails.EnterpriseLookup);

                        if (status)
                            harness.UpdateBootOption("Domain", 4, status: Status.Success.Clone("Verifying Host Configuration..."));
                        else
                            harness.UpdateBootOption("Domain", 3, status: Status.GeneralError.Clone("Error Configuring Host SSL with Let's Encrypt, retrying."));

                        harness.UpdateStatus(status);

                        return status;
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Verifying Host Configuration...");

                        var status = await harness.VerifyHosting(entMgr);

                        if (status)
                        {
                            harness.UpdateBootOption("Domain", 5, status: Status.Success.Clone("Host Configured & Verified"), loading: false);

                            harness.UpdateBootOption("MicroApps", 1, status: Status.Initialized.Clone("Downloading and installing LCU Runtime..."));
                        }
                        else
                            harness.UpdateBootOption("Domain", 4, status: Status.GeneralError.Clone("Error Verifying Host, retrying."));

                        harness.UpdateStatus(status);

                        return status;
                    });

            return status;
        }

        [FunctionName("BootOrganizationOrchestration_Environment")]
        public virtual async Task<Status> BootEnvironment([ActivityTrigger] StateActionContext stateCtxt, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)] IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{stateCtxt.StateDetails.EnterpriseLookup}/{stateCtxt.StateDetails.HubName}/{stateCtxt.StateDetails.Username}/{stateCtxt.StateDetails.StateKey}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
               stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
               {
                   log.LogInformation($"Configuring Workspace Enterprise...");

                   var status = await harness.BootOrganizationEnterprise(entArch, stateCtxt.StateDetails.EnterpriseLookup, stateCtxt.StateDetails.Username);

                   if (status)
                       harness.UpdateBootOption("Environment", 2, status: Status.Initialized.Clone("Workspace Enterprise Configured, setting up environment"));
                   else
                       harness.UpdateBootOption("Environment", 1, status: Status.GeneralError.Clone("Error Configuring Workspace Enterprise, retrying."));

                   return status;
               });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Configuring Workspace Environment...");

                        var status = await harness.BootOrganizationEnvironment(entMgr, devOpsArch);

                        if (status)
                            harness.UpdateBootOption("Environment", 3, status: Status.Success.Clone("Verifying Environment Configuration"));
                        else
                            harness.UpdateBootOption("Environment", 2, status: Status.GeneralError.Clone("Error Configuring Workspace Environment, retrying."));

                        return status;
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Verifying Workspace Environment...");

                        var status = await harness.VerifyOrganizationEnvironment(entMgr);

                        if (status)
                        {
                            harness.UpdateBootOption("Environment", 4, status: Status.Success.Clone("Workspace Environment Configured"), loading: false);

                            harness.UpdateBootOption("DevOps", 1, status: Status.Initialized.Clone("Configuring DevOps Environment..."));
                        }
                        else
                            harness.UpdateBootOption("Environment", 3, status: Status.GeneralError.Clone("Error Verifying Workspace Environment, retrying."));

                        return status;
                    });

            return status;
        }

        [FunctionName("BootOrganizationOrchestration_Infrastructure")]
        public virtual async Task<Status> BootInfrastructure([ActivityTrigger] StateActionContext stateCtxt, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)] IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{stateCtxt.StateDetails.EnterpriseLookup}/{stateCtxt.StateDetails.HubName}/{stateCtxt.StateDetails.Username}/{stateCtxt.StateDetails.StateKey}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                {
                    log.LogInformation($"Configuring Project Infrastructure...");

                    var status = await harness.BootDAFInfrastructure(devOpsArch, stateCtxt.StateDetails.Username);

                    if (status)
                        harness.UpdateBootOption("Infrastructure", 2, status: Status.Initialized.Clone("Verifying Project Infrastructure Config..."));
                    else
                        harness.UpdateBootOption("Infrastructure", 1, status: Status.GeneralError.Clone("Error Configuring Project Infrastructure, retrying."));

                    harness.UpdateStatus(status);

                    return status;
                });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Verifying Project Infrastructure Config...");

                        var status = await harness.VerifyDAFInfrastructure(entMgr);

                        if (status)
                            harness.UpdateBootOption("Infrastructure", 3, status: Status.Initialized.Clone("Building and Releasing Environment Infrastructure..."));
                        else
                            harness.UpdateBootOption("Infrastructure", 2, status: Status.GeneralError.Clone("Error Verifying Project Infrastructure Config, retrying."));

                        harness.UpdateStatus(status);

                        return status;
                    });

            return status;
        }

        [FunctionName("BootOrganizationOrchestration_MicroApps")]
        public virtual async Task<Status> BootMicroApps([ActivityTrigger] StateActionContext stateCtxt, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)] IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{stateCtxt.StateDetails.EnterpriseLookup}/{stateCtxt.StateDetails.HubName}/{stateCtxt.StateDetails.Username}/{stateCtxt.StateDetails.StateKey}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                {
                    log.LogInformation($"Booting micro-apps runtime...");

                    var status = await harness.BootMicroAppsRuntime(entArch);

                    if (status)
                        if (harness.State.Template == "fathym\\daf-iot-starter")
                            harness.UpdateBootOption("MicroApps", 2, status: Status.Initialized.Clone("Configuring IoT Data Flow..."));
                        else
                            harness.UpdateBootOption("MicroApps", 4, status: Status.Initialized.Clone("Configuring Application Orchestration..."));
                    else
                        harness.UpdateBootOption("MicroApps", 1, status: Status.GeneralError.Clone("Error Configuring Micro-Applications Runtime, retrying."));

                    harness.UpdateStatus(status);

                    return status;
                });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        if (harness.State.Template == "fathym\\daf-iot-starter")
                        {
                            log.LogInformation($"Configuring IoT Data Flow...");

                            var status = await harness.BootIoTWelcome(appDev);

                            if (status)
                                harness.UpdateBootOption("MicroApps", 3, status: Status.Initialized.Clone("Deploying IoT Data Flow..."));
                            else
                                harness.UpdateBootOption("MicroApps", 2, status: Status.GeneralError.Clone("Error Configuring IoT Data Flow, retrying."));

                            harness.UpdateStatus(status);
                        }

                        return status;
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        if (harness.State.Template == "fathym\\daf-iot-starter")
                        {
                            log.LogInformation($"Deploying IoT Data Flow...");

                            var status = await harness.DeployIoTDataFlow(appDev, entMgr);

                            if (status)
                                harness.UpdateBootOption("MicroApps", 4, status: Status.Initialized.Clone("Setting up IoT Applications..."));
                            else
                                harness.UpdateBootOption("MicroApps", 3, status: Status.GeneralError.Clone("Error Deploying IoT Data Flow, retrying."));

                            harness.UpdateStatus(status);
                        }

                        return status;
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        if (harness.State.Template == "fathym\\daf-iot-starter")
                        {
                            log.LogInformation($"Setting up IoT Applications...");

                            var status = await harness.SetupIoTWelcomeApps(appDev, appMgr, entMgr);

                            if (status)
                                harness.UpdateBootOption("MicroApps", 5, status: Status.Initialized.Clone("Configuring Application Orchestration..."));
                            else
                                harness.UpdateBootOption("MicroApps", 4, status: Status.GeneralError.Clone("Error setting up IoT Applications, retrying."));

                            harness.UpdateStatus(status);
                        }

                        return status;
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Configuring Application Orchestration...");

                        var status = await harness.BootAppOrch(appDev);

                        if (status)
                            harness.UpdateBootOption("MicroApps", 6, status: Status.Initialized.Clone("Configuring Data Flow Low-Code Unit™...."));
                        else
                            harness.UpdateBootOption("MicroApps", 5, status: Status.GeneralError.Clone("Error Configuring Application Orchestration, retrying."));

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
                            harness.UpdateBootOption("MicroApps", 7, status: Status.Success.Clone("Verifying Micro-Applications Orchestration..."));
                        else
                            harness.UpdateBootOption("MicroApps", 6, status: Status.GeneralError.Clone("Error Configuring Data Flow Low-Code Unit™, retrying."));

                        harness.UpdateStatus(status);

                        return status;
                    });

            if (status)
                status = await stateBlob.WithStateHarness<UserManagementState, BootOrganizationRequest, UserManagementStateHarness>(stateCtxt.StateDetails,
                    stateCtxt.ActionRequest, signalRMessages, log, async (harness, reqData) =>
                    {
                        log.LogInformation($"Verifying Micro-Applications Orchestration...");

                        var status = await harness.VerifyMicroAppsOrchestration(appMgr);

                        if (status)
                        {
                            harness.UpdateBootOption("MicroApps", 8, status: Status.Success.Clone("Micro-Applications Orchestration Configured"), loading: false);

                            harness.CompleteBoot();
                        }
                        else
                            harness.UpdateBootOption("MicroApps", 7, status: Status.GeneralError.Clone("Error Verifying Micro-Applications Orchestration, retrying."));

                        harness.UpdateStatus(status);

                        return status;
                    });

            return status;
        }

        [FunctionName("BootOrganizationOrchestration_UpdateStatus")]
        public virtual async Task<Status> UpdateStatus([ActivityTrigger] StateActionContext stateCtxt, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)] IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{stateCtxt.StateDetails.EnterpriseLookup}/{stateCtxt.StateDetails.HubName}/{stateCtxt.StateDetails.Username}/{stateCtxt.StateDetails.StateKey}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
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
            // var canFinalize = Status.GeneralError;

            // var operationTimeoutTime = context.CurrentUtcDateTime.AddMinutes(60);

            // if (!context.IsReplaying)
            //     log.LogInformation($"Instantiating can finalize loop for: {stateCtxt.ToJSON()}");

            // while (context.CurrentUtcDateTime < operationTimeoutTime)
            // {
            //     if (!context.IsReplaying)
            //         log.LogInformation($"Waiting for organization infrastructure to boot for: {stateCtxt.ToJSON()}");

            //     canFinalize = await context.CallActivityAsync<Status>("BootOrganizationOrchestration_CanFinalize", stateCtxt);

            //     if (canFinalize)
            //     {
            //         if (!context.IsReplaying)
            //             log.LogInformation($"Organization infrastructure booted for: {stateCtxt.ToJSON()}");

            //         break;
            //     }
            //     else
            //     {
            //         // Wait for the next checkpoint
            //         var nextCheckpoint = context.CurrentUtcDateTime.AddSeconds(30);

            //         if (!context.IsReplaying)
            //             log.LogInformation($"Checking organization infrastructure to boot at {nextCheckpoint} for: {stateCtxt.ToJSON()}");

            //         await context.CreateTimer(nextCheckpoint, CancellationToken.None);
            //     }
            // }

            var retryOptions = new RetryOptions(TimeSpan.FromSeconds(10), 500)
            {
                Handle = handleRetryException,
                RetryTimeout = TimeSpan.FromMinutes(60)
            };

            var canFinalize = await context.CallActivityWithRetryAsync<Status>("BootOrganizationOrchestration_CanFinalize", retryOptions, stateCtxt);

            return canFinalize;
        }
        #endregion
    }
}