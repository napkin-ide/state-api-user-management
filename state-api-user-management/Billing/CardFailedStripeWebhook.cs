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
using LCU.Personas.Client.Identity;
using LCU.StateAPI.Utilities;
using LCU.Personas.Client.Security;
using LCU.State.API.NapkinIDE.UserManagement.State;

namespace LCU.State.API.NapkinIDE.UserManagement.Billing
{
    [Serializable]
    [DataContract]
    public class CardFailedStripeWebhookRequest : BaseRequest
    {
        [DataMember]
        public virtual string LicenseType { get; set; }
    }

    public class CardFailedStripeWebhook
    {
        protected readonly EnterpriseManagerClient entMgr;

        protected readonly IdentityManagerClient idMgr;

        protected readonly SecurityManagerClient secMgr;

        public CardFailedStripeWebhook(EnterpriseManagerClient entMgr, SecurityManagerClient secMgr)
        {
            this.entMgr = entMgr;

            this.secMgr = secMgr;
        }

        [FunctionName("CardFailedStripeWebhook")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log)
        {
            var stateDetails = StateUtils.LoadStateDetails(req);

            return await stateBlob.WithStateHarness<UserBillingState, CardFailedStripeWebhookRequest, UserBillingStateHarness>(req, signalRMessages, log,
                async (harness, dataReq) =>
            {
                log.LogInformation($"Executing CompletePayment Action.");

                harness.CardFailedStripeWebhook(force: true);

                await harness.Refresh(entMgr, idMgr, secMgr, stateDetails.EnterpriseLookup, stateDetails.Username, dataReq.LicenseType);

                return Status.Success;
            });
        }
    }
}
