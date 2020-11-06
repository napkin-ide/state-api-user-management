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
using LCU.State.API.NapkinIDE.UserManagement.State;

namespace LCU.State.API.NapkinIDE.UserManagement.Management
{
    [Serializable]
    [DataContract]
    public class SetUserTypeRequest : BaseRequest
    {
        [DataMember]
        public virtual UserTypes UserType { get; set; }
    }

    public static class SetUserType
    {
        [FunctionName("SetUserType")]
        public static async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, SetUserTypeRequest, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, userDetsReq) =>
            {
                log.LogInformation($"Executing SetUserType Action.");

                //harness.SetUserType(userDetsReq.UserType);

                return Status.Success;
            });
        }
    }
}
