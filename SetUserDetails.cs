using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using LCU.Presentation.State.ReqRes;
using Fathym;
using LCU.StateAPI;
using Microsoft.AspNetCore.Http;
using System;
using Fathym.API;
using System.IO;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using System.Runtime.Serialization;

namespace LCU.State.API.NapkinIDE.User.Management
{
    [Serializable]
    [DataContract]
    public class SetUserDetailsRequest : BaseRequest
    {
        [DataMember]
        public virtual string Country { get; set; }

        [DataMember]
        public virtual string FullName { get; set; }

        [DataMember]
        public virtual string Handle { get; set; }
    }

    public static class SetUserDetails
    {
        [FunctionName("SetUserDetails")]
        public static async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateAction<UserManagementState, SetUserDetailsRequest>(req, signalRMessages, log, async (state, userDetsReq) =>
            {
                log.LogInformation($"Executing SetUserDetails Action.");

                state.SetUserDetails(userDetsReq.FullName, userDetsReq.Country, userDetsReq.Handle);

                return state;
            });
        }
    }
}