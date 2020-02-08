using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Fathym;
using LCU.State.API.NapkinIDE.User.Management.Utils;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;

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

        [FunctionName("EmitState")]
        public static async Task<Status> EmitState([ActivityTrigger] UserManagementState state, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages)
        {
            var stateDetails = state.StateDetails;

            var groupName = StateUtils.BuildGroupName(stateDetails);

            var sendMethod = $"ReceiveState";//=>{groupName}";

            await signalRMessages.AddAsync(new SignalRMessage()
            {
                Target = sendMethod,
                GroupName = "Test",//groupName,
                Arguments = new[] { state }
            });

            // var context = await StateUtils.LoadHubContext(stateDetails.HubName);

            // await context.Clients.Group(groupName).SendCoreAsync(sendMethod, new[] { state });

            return Status.Success;
        }
    }
}