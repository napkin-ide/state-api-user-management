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
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Fathym;
using LCU.Personas.Client.Identity;
using LCU.StateAPI.Utilities;

namespace LCU.State.API.NapkinIDE.Setup
{
    [Serializable]
    [DataContract]
    public class SetFreeTrialRequest
    {

        [DataMember]
        public virtual bool IsNewSubscriber { get; set; }

        [DataMember]
        public virtual bool IsLocked { get; set; }

        [DataMember]
        public virtual bool IsReset { get; set; }

        [DataMember]
        public virtual string UserName { get; set; }

        [DataMember]
        public virtual int TrialLength { get; set; }
    }

    public class SetFreeTrial
    {
        protected IdentityManagerClient idMgr;

        public SetFreeTrial(IdentityManagerClient idMgr)
        {
            this.idMgr = idMgr;
        }

        [FunctionName("SetFreeTrial")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, SetFreeTrialRequest, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData) =>
            {
                log.LogInformation($"Executing SetFreeTrial Action.");

                var stateDetails = StateUtils.LoadStateDetails(req);

                var status = (reqData.IsNewSubscriber) ? 
                                await harness.SetLimitedAccessToken(idMgr, stateDetails.EnterpriseAPIKey, reqData.UserName, reqData.TrialLength) :
                                await harness.UpdateLimitedAccessToken(idMgr, stateDetails.EnterpriseAPIKey, reqData.UserName, reqData.TrialLength, reqData.IsLocked, reqData.IsReset);

                return status;
            });
        }
    }
}
