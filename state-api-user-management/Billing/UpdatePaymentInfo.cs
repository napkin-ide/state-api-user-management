using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Runtime.Serialization;
using Fathym.API;
using Fathym;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.Storage.Blob;
using LCU.Personas.Client.Enterprises;
using LCU.StateAPI.Utilities;
using LCU.Personas.Client.Security;
using LCU.State.API.NapkinIDE.UserManagement.State;

namespace LCU.State.API.NapkinIDE.UserManagement.Billing
{
    [Serializable]
    [DataContract]
    public class UpdatePaymentInfoRequest : BaseRequest
    {
        [DataMember]
        public virtual string CustomerName { get; set; }

        [DataMember]
        public virtual string MethodID { get; set; }

        
    }

    public class UpdatePaymentInfo
    {
        protected readonly EnterpriseManagerClient entMgr;

        protected readonly SecurityManagerClient secMgr;

        public UpdatePaymentInfo(EnterpriseManagerClient entMgr, SecurityManagerClient secMgr)
        {
            this.entMgr = entMgr;

            this.secMgr = secMgr;
        }

        [FunctionName("UpdatePaymentInfo")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var stateDetails = StateUtils.LoadStateDetails(req);

            return await stateBlob.WithStateHarness<UserBillingState, UpdatePaymentInfoRequest, UserBillingStateHarness>(req, signalRMessages, log,
                async (harness, payReq) =>
            {
                log.LogInformation($"Executing UpdatePaymentInfo Action.");

                await harness.UpdatePaymentInfo(entMgr, secMgr, stateDetails.EnterpriseLookup, stateDetails.Username, payReq.MethodID, payReq.CustomerName);

                //  TODO:  Set State Status and Loading

                return Status.Success;
            });
        }
    }
}
