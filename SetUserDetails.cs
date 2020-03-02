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

namespace LCU.State.API.NapkinIDE.User.Management
{
    [Serializable]
    public class SetUserDetailsRequest : BaseRequest
    {

    }

    public static class SetUserDetails
    {
        [FunctionName("SetUserDetails")]
        public static async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [Blob("state-api/usermanagement", FileAccess.Write)] CloudBlobDirectory directory)
        {
            log.LogInformation($"Executing SetUserDetails Action.");

            var actArgs = await req.LoadBody<ExecuteActionArguments>();

            var entityId = new EntityId(typeof(UserManagementState).Name, actArgs.StateDetails.Username);

            log.LogInformation($"Loading entity {entityId}");
            
            // context.SignalEntity(entityId, "SetUserDetails", actArgs.ActionRequest);

            return Status.Success;
        }
    }
}