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
using LCU.State.API.UserManagement.Host.TempRefit;

namespace LCU.State.API.NapkinIDE.UserManagement.Billing
{
    [Serializable]
    [DataContract]
    public class ResetStateCheckRequest : BaseRequest
    {
        [DataMember]
        public virtual string LicenseType { get; set; }
    }

    public class ResetStateCheck
    {
        protected readonly IEnterprisesBillingManagerService entBillingMgr;

        protected readonly IIdentityAccessService idMgr;

        protected readonly ISecurityDataTokenService secMgr;

        public ResetStateCheck(IEnterprisesBillingManagerService entBillingMgr, IIdentityAccessService idMgr, ISecurityDataTokenService secMgr)
        {
            this.entBillingMgr = entBillingMgr;

            this.idMgr = idMgr;

            this.secMgr = secMgr;            
        }

        [FunctionName("ResetStateCheck")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)] IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var stateDetails = StateUtils.LoadStateDetails(req);

            return await stateBlob.WithStateHarness<UserBillingState, ResetStateCheckRequest, UserBillingStateHarness>(req, signalRMessages, log,
                async (harness, dataReq) =>
            {
                log.LogInformation($"Executing CompletePayment Action.");

                harness.ResetStateCheck(force: true);

                await harness.Refresh(entBillingMgr, idMgr, secMgr, stateDetails.EnterpriseLookup, stateDetails.Username, dataReq.LicenseType);

                return Status.Success;
            });
        }
    }
}
