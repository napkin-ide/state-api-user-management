using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using LCU.State.API.NapkinIDE.UserManagement;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Fathym;
using LCU.Personas.Client.Enterprises;
using LCU.Personas.Client.Identity;
using LCU.Personas.Client.Security;
using LCU.StateAPI.Utilities;
using LCU.State.API.NapkinIDE.UserManagement.State;
using LCU.State.API.UserManagement.Host.TempRefit;

namespace LCU.State.API.NapkinIDE.UserManagement.Management
{
    [Serializable]
    [DataContract]
    public class CancelSubscriptionRequest
    {

        [DataMember]
        public virtual string CancellationReason { get; set; }

        [DataMember]
        public virtual string LicenseType { get; set; }

    }

    public class CancelSubscription
    {
        protected IEnterprisesBillingManagerService engMgr;

        protected IIdentityAccessService idMgr;

        protected ISecurityDataTokenService secMgr;

        public CancelSubscription(IEnterprisesBillingManagerService engMgr, IIdentityAccessService idMgr, ISecurityDataTokenService secMgr)
        {
            this.engMgr = engMgr;

            this.idMgr = idMgr;

            this.secMgr = secMgr;
        }

        [FunctionName("CancelSubscription")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, CancelSubscriptionRequest, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData) =>
            {
                log.LogInformation($"Executing CancelSubscription Action.");

                var stateDetails = StateUtils.LoadStateDetails(req);

                var status =  await harness.CancelSubscription(engMgr, idMgr, secMgr, stateDetails.EnterpriseLookup, stateDetails.Username, reqData.CancellationReason, reqData.LicenseType);

                return status;
            });
        }
    }
}
