using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Fathym;
using LCU.StateAPI.Utilities;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Extensions.Logging;

namespace LCU.State.API.NapkinIDE.User.Management
{
    public static class SendState
    {
        [FunctionName("SendState")]
        public static async Task<Status> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var state = context.GetInput<UserManagementState>();

            return await context.CallActivityAsync<Status>("EmitState", state);
        }
    }
}