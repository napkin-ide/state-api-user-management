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
using Fathym;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Runtime.Serialization;
using Fathym.API;
using System.Collections.Generic;
using System.Linq;
using LCU.Personas.Client.Applications;
using LCU.StateAPI.Utilities;
using System.Security.Claims;
using LCU.Personas.Client.Enterprises;
using LCU.Personas.Client.Security;
using Castle.Core.Configuration;
using LCU.State.API.NapkinIDE.UserManagement.State;

namespace LCU.State.API.NapkinIDE.UserManagement.Host
{
    [Serializable]
    [DataContract]
    public class RefreshRequest : BaseRequest
    { }

    public class Refresh
    {
        protected readonly string billingEntApiKey;

        protected readonly EnterpriseManagerClient entMgr;

        protected readonly SecurityManagerClient secMgr;

        public Refresh(EnterpriseManagerClient entMgr, SecurityManagerClient secMgr)
        {
            billingEntApiKey = Environment.GetEnvironmentVariable("LCU-BILLING-ENTERPRISE-API-KEY");

            this.entMgr = entMgr;
            
            this.secMgr = secMgr;
        }

        #region API Methods
        [FunctionName("Refresh")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var stateDetails = StateUtils.LoadStateDetails(req);

            if (stateDetails.StateKey == "billing")
                return await stateBlob.WithStateHarness<UserBillingState, RefreshRequest, UserBillingStateHarness>(req, signalRMessages, log,
                    async (harness, refreshReq, actReq) =>
                {
                    log.LogInformation($"Refreshing user billing state");

                    return await refreshUserBilling(harness, log, stateDetails);
                });
            else
                return await stateBlob.WithStateHarness<UserManagementState, RefreshRequest, UserManagementStateHarness>(req, signalRMessages, log,
                    async (harness, refreshReq, actReq) =>
                {
                    log.LogInformation($"Refreshing user management state");

                    return await refreshUserManagement(harness, log, stateDetails);
                });
        }
        #endregion

        #region Helpers
        protected virtual async Task<Status> refreshUserBilling(UserBillingStateHarness harness, ILogger log, StateDetails stateDetails)
        {
            await harness.Refresh(entMgr, secMgr, billingEntApiKey, stateDetails.Username);

            return Status.Success;
        }

        protected virtual async Task<Status> refreshUserManagement(UserManagementStateHarness harness, ILogger log, StateDetails stateDetails)
        {
            harness.ConfigureInfrastructureOptions();

            harness.ConfigureJourneys();

            harness.ConfigurePersonas();

            harness.SetUserType(harness.State.Personas.FirstOrDefault().Lookup.As<UserTypes>());

            harness.DetermineSetupStep();

            await Task.WhenAll(new[]{
                harness.LoadRegistrationHosts(entMgr, stateDetails.EnterpriseAPIKey),
                harness.HasDevOpsOAuth(entMgr, stateDetails.EnterpriseAPIKey, stateDetails.Username)
            });

            return Status.Success;
        }
        #endregion
    }
}
