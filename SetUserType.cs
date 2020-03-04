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

namespace LCU.State.API.NapkinIDE.User.Management
{
    public static class SetUserType
    {
        [Serializable]
        [DataContract]
        public class SetUserTypeRequest : BaseRequest
        {
            [DataMember]
            public virtual UserTypes UserType { get; set; }
        }

        [FunctionName("SetUserType")]
        public static async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateAction<UserManagementState, SetUserTypeRequest>(req, signalRMessages, log, async (state, userDetsReq) =>
            {
                log.LogInformation($"Executing SetUserDetails Action.");

                state.SetUserType(userDetsReq.UserType);

                return state;
            });
        }
    }
}
