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

namespace LCU.State.API.NapkinIDE.User.Management
{
    public static class SetUserDetails
    {
        [FunctionName("SetUserDetails")]
        public static async Task<Status> RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var actArgs = context.GetInput<ExecuteActionArguments>();

            var entityId = new EntityId(nameof(UserManagementStateEntity), actArgs.StateDetails.Username);

            context.SignalEntity(entityId, "SetUserDetails", actArgs.ActionRequest);

            return Status.Success;
        }
    }
}