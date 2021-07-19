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
using Microsoft.Azure.Storage.Blob;
using System.Runtime.Serialization;
using Fathym.API;
using System.Collections.Generic;
using System.Linq;
using LCU.Personas.Client.Applications;
using LCU.StateAPI.Utilities;
using System.Security.Claims;
using LCU.Personas.Client.Identity;
using LCU.Personas.Client.Enterprises;
using LCU.Personas.Client.Security;
using LCU.State.API.NapkinIDE.UserManagement.State;
using LCU.State.API.UserManagement.Host.TempRefit;

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
        protected readonly IEnterprisesBillingManagerService entBillingMgr;

        protected readonly IdentityManagerClient idMgr;

        protected readonly SecurityManagerClient secMgr;

        public Refresh(IEnterprisesBillingManagerService entBillingMgr, IdentityManagerClient idMgr, SecurityManagerClient secMgr)
        {
            this.entBillingMgr = entBillingMgr;

            this.idMgr = idMgr;

            this.secMgr = secMgr;
        }

        #region API Methods
        [FunctionName("Refresh")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)] IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var stateDetails = StateUtils.LoadStateDetails(req);

            if (stateDetails.StateKey.StartsWith("billing"))
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
            await harness.Refresh(entBillingMgr, idMgr, secMgr, stateDetails.EnterpriseLookup, stateDetails.Username, request.LicenseType);

            return Status.Success;
        }

        protected virtual async Task<Status> refreshUserManagement(UserManagementStateHarness harness, ILogger log, StateDetails stateDetails, RefreshUserRequest request)
        {
            harness.ConfigureInfrastructureOptions();

            //harness.ConfigureJourneys();

            //harness.ConfigurePersonas();

            //harness.SetUserType(harness.State.Personas.FirstOrDefault().Lookup.As<UserTypes>());

            harness.DetermineSetupStep();

            await harness.HasAzureOAuth(entMgr, stateDetails.EnterpriseLookup, stateDetails.Username);
            
            //TODO: Remove hardcoded LCU
            
            await harness.LoadSubscriptionDetails(entMgr, secMgr, stateDetails.EnterpriseLookup, stateDetails.Username, "LCU");

            await Task.WhenAll(new[]{
                harness.LoadRegistrationHosts(entMgr, stateDetails.EnterpriseLookup),
                // harness.HasDevOpsOAuth(entMgr, stateDetails.EnterpriseLookup, stateDetails.Username)
            });

            // TODO: may need to track auth requests in the future
            harness.State.RequestAuthorizationSent = String.Empty;
            
            return Status.Success;
        }
        #endregion
    }
}
