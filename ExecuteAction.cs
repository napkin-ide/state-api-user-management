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
using LCU.State.API.NapkinIDE.User.Management.Utils;
using System.Security.Claims;

namespace LCU.State.API.NapkinIDE.User.Management
{
    public class ExecuteActionArguments
    {
        public virtual ExecuteActionRequest ActionRequest { get; set; }

        public virtual StateDetails StateDetails { get; set; }
    }
    public static class ExecuteAction
    {
        [FunctionName("ExecuteAction")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Admin, "post", Route = null)] HttpRequest req,
            ClaimsPrincipal claimsPrincipal, ILogger log, [DurableClient] IDurableOrchestrationClient actions)
        {
            log.LogInformation("Executing action");

                var stateDetails = StateUtils.LoadStateDetails(req, claimsPrincipal);

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var actionReq = requestBody?.FromJSON<ExecuteActionRequest>();

            log.LogInformation($"{actionReq.ToJSON()}");

            try
            {
                string instanceId = await actions.StartNewAsync(actionReq.Type, new ExecuteActionArguments()
                {
                    ActionRequest = actionReq,
                    StateDetails = stateDetails
                });

                return actions.CreateCheckStatusResponse(req, instanceId);
            }
            catch
            {
                log.LogError($"Issue invoking action: {actionReq.ToJSON()}");

                return new OkResult();
            }
        }
    }
}
