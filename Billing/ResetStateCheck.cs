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
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using LCU.Personas.Client.Enterprises;
using LCU.StateAPI.Utilities;
using LCU.Personas.Client.Security;
using LCU.State.API.NapkinIDE.UserManagement.State;

namespace LCU.State.API.NapkinIDE.UserManagement.Billing
{
    [Serializable]
    [DataContract]
    public class ResetStateCheckRequest : BaseRequest
    { }

    public class ResetStateCheck
    {
        protected readonly EnterpriseManagerClient entMgr;

        protected readonly SecurityManagerClient secMgr;

        public ResetStateCheck(EnterpriseManagerClient entMgr, SecurityManagerClient secMgr)
        {
            this.entMgr = entMgr;
            
            this.secMgr = secMgr;
        }

        [FunctionName("ResetStateCheck")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var stateDetails = StateUtils.LoadStateDetails(req);

            return await stateBlob.WithStateHarness<UserBillingState, CompletePaymentRequest, UserBillingStateHarness>(req, signalRMessages, log,
                async (harness, payReq) =>
            {
                log.LogInformation($"Executing CompletePayment Action.");

                harness.ResetStateCheck(force: true);

                await harness.Refresh(entMgr, secMgr, stateDetails.EnterpriseAPIKey, stateDetails.Username);

                return Status.Success;
            });
        }
    }
}
