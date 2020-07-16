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
    public class RefreshBillingRequest : BaseRequest
    {
        [DataMember(Name = "id")]
        public virtual string ID { get; set; }
        
        [DataMember(Name = "licenseType")]
        public virtual string LicenseType { get; set; }
    }

    [Serializable]
    [DataContract]
    public class RefreshUserRequest : BaseRequest
    {

    }

    public class Refresh
    {
        protected readonly string billingEntApiKey;

        protected readonly EnterpriseArchitectClient entArch;

        protected readonly EnterpriseManagerClient entMgr;

        protected readonly SecurityManagerClient secMgr;

        public Refresh(EnterpriseArchitectClient entArch, EnterpriseManagerClient entMgr, SecurityManagerClient secMgr)
        {
            billingEntApiKey = Environment.GetEnvironmentVariable("LCU-BILLING-ENTERPRISE-API-KEY");

            this.entArch = entArch;

            this.entMgr = entMgr;

            this.secMgr = secMgr;
        }

        #region API Methods
        [FunctionName("Refresh")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)] IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var stateDetails = StateUtils.LoadStateDetails(req);

            if (stateDetails.StateKey == "billing")
                return await stateBlob.WithStateHarness<UserBillingState, RefreshBillingRequest, UserBillingStateHarness>(req, signalRMessages, log,
                    async (harness, refreshReq, actReq) =>
                {
                    log.LogInformation($"Refreshing user billing state");

                    return await refreshUserBilling(harness, log, stateDetails, refreshReq);
                });
            else
                return await stateBlob.WithStateHarness<UserManagementState, RefreshUserRequest, UserManagementStateHarness>(req, signalRMessages, log,
                    async (harness, refreshReq, actReq) =>
                {
                    log.LogInformation($"Refreshing user management state");

                    return await refreshUserManagement(harness, log, stateDetails, refreshReq);
                });
        }
        #endregion

        #region Helpers
        protected virtual async Task<Status> refreshUserBilling(UserBillingStateHarness harness, ILogger log, StateDetails stateDetails, RefreshBillingRequest request)
        {
            await harness.Refresh(entMgr, secMgr, billingEntApiKey, stateDetails.Username, request.LicenseType);

            return Status.Success;
        }

        protected virtual async Task<Status> refreshUserManagement(UserManagementStateHarness harness, ILogger log, StateDetails stateDetails, RefreshUserRequest request)
        {
            harness.ConfigureInfrastructureOptions();

            harness.ConfigureJourneys();

            harness.ConfigurePersonas();

            harness.SetUserType(harness.State.Personas.FirstOrDefault().Lookup.As<UserTypes>());

            harness.DetermineSetupStep();

            //await harness.LoadSubscriptionDetails(entMgr, secMgr, stateDetails.EnterpriseAPIKey, stateDetails.Username);

            await Task.WhenAll(new[]{
                harness.LoadRegistrationHosts(entMgr, stateDetails.EnterpriseAPIKey),
                harness.HasDevOpsOAuth(entMgr, stateDetails.EnterpriseAPIKey, stateDetails.Username)
            });

            return Status.Success;
        }
        #endregion
    }
}
