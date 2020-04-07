using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using LCU.StateAPI;
using Microsoft.WindowsAzure.Storage.Blob;
using LCU.StateAPI.Utilities;

namespace LCU.State.API.NapkinIDE.UserManagement
{
    public static class ConnectToState
    {
        [FunctionName("ConnectToState")]
        public static async Task<ConnectToStateResponse> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")]HttpRequest req, ILogger log,
            ClaimsPrincipal claimsPrincipal, //[LCUStateDetails]StateDetails stateDetails,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRGroupAction> signalRGroupActions,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            var stateDetails = StateUtils.LoadStateDetails(req);

            if (stateDetails.StateKey == "billing")
                return await signalRMessages.ConnectToState<UserBillingState>(req, log, claimsPrincipal, stateBlob, signalRGroupActions);
            else
                return await signalRMessages.ConnectToState<UserManagementState>(req, log, claimsPrincipal, stateBlob, signalRGroupActions);
        }
    }
}
