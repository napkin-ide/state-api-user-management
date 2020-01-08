using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;

namespace LCU.State.API.NapkinIDE.User.Management
{
    public static class Negotiate
    {
        [FunctionName("Negotiate")]
        public static SignalRConnectionInfo Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [SignalRConnectionInfo(HubName = "user-management")] SignalRConnectionInfo connectionInfo)//, UserId = "{headers.x-ms-client-principal-id}"
        {
                // var context = await StaticServiceHubContextStore.Get().GetAsync(HubName);

            return connectionInfo;
        }
    }
}
