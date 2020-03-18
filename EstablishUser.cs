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

namespace LCU.State.API.NapkinIDE.UserManagement
{
        [Serializable]
        [DataContract]
        public class EstablishUserRequest : BaseRequest
        {
            [DataMember]
            public virtual string AzureAppID { get; set; }
            
            [DataMember]
            public virtual string AzureAuthKey { get; set; }
            
            [DataMember]
            public virtual string AzureSubID { get; set; }
            
            [DataMember]
            public virtual string AzureTenantID { get; set; }
            
            [DataMember]
            public virtual string OrgDescription { get; set; }
            
            [DataMember]
            public virtual string OrgLookup { get; set; }
            
            [DataMember]
            public virtual string OrgName { get; set; }
        }

    public static class EstablishUser
    {
        [FunctionName("EstablishUser")]
        public static async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, EstablishUserRequest, UserManagementStateHarness>(req, signalRMessages, log, 
                async (harness, payReq) =>
            {
                    log.LogInformation($"Executing SetUserSetupStep Action.");

                    // harness.SetPaymentMethod(payReq.MethodID);
            });
        }
    }
}
