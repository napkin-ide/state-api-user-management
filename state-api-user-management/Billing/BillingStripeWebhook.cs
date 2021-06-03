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
using LCU.Personas.Client.Applications;
using LCU.Personas.Client.Enterprises;
using LCU.Personas.Client.Identity;
using LCU.StateAPI.Utilities;
using LCU.Personas.Client.Security;
using LCU.State.API.NapkinIDE.UserManagement.State;
using Stripe;
using LCU.Presentation.State.ReqRes;

namespace LCU.State.API.NapkinIDE.UserManagement.Billing
{
    public class BillingStripeWebhook
    {
        protected readonly EnterpriseManagerClient entMgr;

        protected readonly IdentityManagerClient idMgr;

        protected readonly ApplicationManagerClient appMgr;

        protected readonly SecurityManagerClient secMgr;

        public BillingStripeWebhook(ApplicationManagerClient appMgr, EnterpriseManagerClient entMgr, SecurityManagerClient secMgr)
        {
            this.appMgr = appMgr;

            this.entMgr = entMgr;

            this.secMgr = secMgr;
        }

        [FunctionName("BillingStripeWebhook")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)] IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api", FileAccess.ReadWrite)] CloudBlobContainer blobContainer)
        {
            using var bodyRdr = new StreamReader(req.Body);

            var json = await bodyRdr.ReadToEndAsync();

            var stripeEvent = EventUtility.ParseEvent(json);

            var stateDetails = new StateDetails();

            if(stripeEvent.Type == Events.ChargeFailed)
            {

                var charge = (Charge)stripeEvent.Data.Object;

                var userEmail = charge.BillingDetails.Email;

                stateDetails = new StateDetails()
                {
                    EnterpriseLookup = req.Query["lcu-ent-lookup"],
                    HubName = UserManagementState.HUB_NAME,
                    StateKey = "billing",
                    Username = userEmail
                };
            }

            else{
                stateDetails = new StateDetails()
                {
                    EnterpriseLookup = req.Query["lcu-ent-lookup"],
                    HubName = UserManagementState.HUB_NAME,
                    StateKey = "billing",
                    Username = ""
                };

            }

            log.LogInformation($"State Details {stateDetails.ToJSON()}");

            var stateBlob = blobContainer.GetBlockBlobReference($"{stateDetails.EnterpriseLookup}/{stateDetails.HubName}/{stateDetails.Username}/{stateDetails.StateKey}");

            var exActReq = await req.LoadBody<ExecuteActionRequest>();



            //If Stripe.Event doesn't work... MetadataModel
            return await stateBlob.WithStateHarness<UserBillingState, Stripe.Event, UserBillingStateHarness>(stateDetails, exActReq,
                signalRMessages, log,
                async (harness, dataReq) =>
            {
                log.LogInformation($"Executing CompletePayment Action.");

                var status = Status.Initialized;

                switch (stripeEvent.Type)
                {
                    case Events.ChargeFailed:
                        status = await harness.HandleChargeFailed(entMgr, appMgr, stateDetails.EnterpriseLookup, stateDetails.Username, stripeEvent);
                        break;

                    default:
                        status = Status.Success.Clone("Stripe Web Hook not handled");
                        break;
                }

                log.LogInformation($"Completed execution of web hook with: {status.ToJSON()}");

                return status;
            });
            
        }
    }
}
