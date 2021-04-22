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
using Stripe;

namespace LCU.State.API.NapkinIDE.UserManagement.Billing
{
    [Serializable]
    [DataContract]
    [Route("api/[controller]")]
    public class StripeWebHookRequest : Controller
    {
          [HttpPost]
          public async Task<IActionResult> Index()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                var stripeEvent = EventUtility.ParseEvent(json);

                // Handle the event
                if (stripeEvent.Type == Events.PaymentIntentSucceeded)
                {
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                    // Then define and call a method to handle the successful payment intent.
                    // handlePaymentIntentSucceeded(paymentIntent);
                }
                else if (stripeEvent.Type == Events.PaymentMethodAttached)
                {
                    var paymentMethod = stripeEvent.Data.Object as PaymentMethod;
                    // Then define and call a method to handle the successful attachment of a PaymentMethod.
                    // handlePaymentMethodAttached(paymentMethod);
                }
                else if(stripeEvent.Type == Events.ChargeFailed)
                {
                    var chargeFailed = stripeEvent.Data.Object as Charge;
                    await StripeWebhook.HandleChargeFailed(chargeFailed);


                }
                // ... handle other event types
                else
                {
                    // Unexpected event type
                    Console.WriteLine("Unhandled event type: {0}", stripeEvent.Type);
                }
                return Ok();
            }
            catch (StripeException e)
            {
                return BadRequest();
            }
        }

    }

       


    public class StripeWebhook
    {
        protected readonly EnterpriseManagerClient entMgr;

        protected readonly IdentityManagerClient idMgr;

        protected readonly SecurityManagerClient secMgr;

        public StripeWebhook(EnterpriseManagerClient entMgr, SecurityManagerClient secMgr, IdentityManagerClient idMgr)
        {
            this.entMgr = entMgr;

            this.idMgr = idMgr;

            this.secMgr = secMgr;
        }

         public virtual async Task<Status> HandleChargeFailed(Charge failedCharge)
        {
            await harness.HandleFailedCharge(failedCharge);

            return Status.Success;
        }

        // [FunctionName("HandleChargeFailed")]
        // public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log, CloudBlockBlob stateBlob)
        // {
        //     var stateDetails = StateUtils.LoadStateDetails(req);

        //     return await stateBlob.WithStateHarness<UserBillingState, ChangeSubscriptionRequest, UserBillingStateHarness>(req log,
        //         async (harness, payReq) =>
        //     {
        //         log.LogInformation($"Executing ChangeSubscription Action.");

        //         await harness.HandleStripeCardWebHook(entMgr, secMgr, idMgr, stateDetails.EnterpriseLookup, stateDetails.Username, payReq.CustomerName, payReq.Plan);


        //         return Status.Success;
        //     });
        // }
    }
}
