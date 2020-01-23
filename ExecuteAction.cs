using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using LCU.Presentation.State.ReqRes;
using Fathym;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace LCU.State.API.NapkinIDE.User.Management
{
    public static class ExecuteAction
    {
        [FunctionName("ExecuteAction")]
        public static async Task<Status> Run([HttpTrigger(AuthorizationLevel.Admin, "post", Route = null)] HttpRequest req,
             ILogger log)//[OrchestrationTrigger]IDurableOrchestrationContext context,
        {
            log.LogInformation("Executing action");

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var actionReq = requestBody?.FromJSON<ExecuteActionRequest>();

            log.LogInformation($"{actionReq.ToJSON()}");

            //  TODO

            return Status.Success;
        }
    }
}
