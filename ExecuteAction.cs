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
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Admin, "post", Route = null)] HttpRequest req,
             ILogger log, [DurableClient] IDurableOrchestrationClient actions)
        {
            log.LogInformation("Executing action");

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var actionReq = requestBody?.FromJSON<ExecuteActionRequest>();

            log.LogInformation($"{actionReq.ToJSON()}");

            try
            {
                string instanceId = await actions.StartNewAsync(actionReq.Type, actionReq);

                return actions.CreateCheckStatusResponse(req, instanceId);
            }
            catch
            {
                log.LogError("Issue invoking action", actionReq);
                
                return new OkResult();
            }
        }
    }
}
