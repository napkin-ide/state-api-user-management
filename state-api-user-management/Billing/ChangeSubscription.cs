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
using Fathym.API;
using Fathym;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using LCU.Personas.Client.Enterprises;
using LCU.StateAPI.Utilities;
using LCU.Personas.Client.Security;
using Microsoft.Extensions.Configuration;
using LCU.State.API.NapkinIDE.UserManagement.State;
using LCU.Personas.Client.Identity;
using LCU.State.API.UserManagement.Host.TempRefit;

namespace LCU.State.API.NapkinIDE.UserManagement.Billing
{
    [Serializable]
    [DataContract]
    public class ChangeSubscriptionRequest : BaseRequest
    {
        [DataMember]
        public virtual string CustomerName { get; set; }

        [DataMember]
        public virtual string Plan { get; set; }

    }

    public class ChangeSubscription
    {
        protected readonly IEnterprisesBillingManagerService entBillingMgr;

        protected readonly IIdentityAccessService idMgr;

        protected readonly ISecurityDataTokenService secMgr;

        public ChangeSubscription(IEnterprisesBillingManagerService entBillingMgr, ISecurityDataTokenService secMgr, IIdentityAccessService idMgr)
        {
            this.entBillingMgr = entBillingMgr;

            this.idMgr = idMgr;

            this.secMgr = secMgr;
        }

        [FunctionName("ChangeSubscription")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var stateDetails = StateUtils.LoadStateDetails(req);

            return await stateBlob.WithStateHarness<UserBillingState, ChangeSubscriptionRequest, UserBillingStateHarness>(req, signalRMessages, log,
                async (harness, payReq) =>
            {
                log.LogInformation($"Executing ChangeSubscription Action.");

                await harness.ChangeSubscription(entBillingMgr, secMgr, idMgr, stateDetails.EnterpriseLookup, stateDetails.Username, payReq.CustomerName, payReq.Plan);

                //  TODO:  Set State Status and Loading

                return Status.Success;
            });
        }
    }
}
