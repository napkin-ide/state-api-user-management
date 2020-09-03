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
using LCU.Personas.Client.Identity;
using LCU.StateAPI.Utilities;
using Fathym.API;
using LCU.Personas.Client.Applications;
using LCU.State.API.NapkinIDE.UserManagement.State;
using LCU.Personas.Client.Enterprises;

namespace LCU.State.API.NapkinIDE.UserManagement.Management
{
    [DataContract]
    public class SendNotificationRequest 
    {

        [DataMember]
        public virtual string Content { get; set; }

        [DataMember]
        public virtual string Subject { get; set; }

        [DataMember]
        public virtual string ReplyTo { get; set; }
        
        [DataMember]
        public virtual string EmailTo { get; set; }
        
        [DataMember]
        public virtual string EmailFrom { get; set; }
    }

    public class SendNotification
    {
        protected EnterpriseManagerClient entMgr;

        public SendNotification(EnterpriseManagerClient entMgr)
        {
            this.entMgr = entMgr;
        }

        [FunctionName("SendNotification")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, SendNotificationRequest, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData) =>
            {
                log.LogInformation($"Executing SendNotification Action.");

                var stateDetails = StateUtils.LoadStateDetails(req);

                var status = await harness.SendNotification(entMgr, stateDetails.EnterpriseLookup, stateDetails.Username, reqData);

                return status;
            });
        }
    }
}
