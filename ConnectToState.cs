using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Security.Claims;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using LCU.Presentation.State;
using System.Runtime.Serialization;
using LCU.State.API.NapkinIDE.User.Management.Utils;
using Fathym.API;
using Fathym;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace LCU.State.API.NapkinIDE.User.Management
{
    [DataContract]
    public class ConnectToStateResponse : BaseResponse
    {
        [DataMember]
        public virtual string GroupName { get; set; }
    }

    public static class ConnectToState
    {
        [FunctionName("ConnectToState")]
        public static async Task<ConnectToStateResponse> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")]HttpRequest req, ILogger logger, 
            ClaimsPrincipal claimsPrincipal, [DurableClient] IDurableEntityClient entity,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRGroupAction> signalRGroupActions)
            // [Blob("state/{headers.lcu-ent-api-key}/usermanagement/__config.lcu", FileAccess.Read)] string stateCfgStr
        {
            try
            {
                var stateDetails = StateUtils.LoadStateDetails(req, claimsPrincipal);

                logger.LogInformation($"Connecting to state {stateDetails.HubName}.");

                var context = await StateUtils.LoadHubContext(stateDetails.HubName);

                var groupName = await groupClient(signalRGroupActions, stateDetails);

                var entityId = new EntityId(nameof(UserManagementStateEntity), stateDetails.Username);

                await entity.SignalEntityAsync(entityId, "$Init", stateDetails);

                return new ConnectToStateResponse()
                {
                    Status = Status.Success,
                    GroupName = groupName
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"There was an error durring call ConnectToState");

                throw;
            }
        }

        private static async Task<string> groupClient(IAsyncCollector<SignalRGroupAction> signalRGroupActions,
            StateDetails stateDetails)
        {
            var groupName = StateUtils.BuildGroupName(stateDetails);

            await signalRGroupActions.AddAsync(
                new SignalRGroupAction
                {
                    UserId = stateDetails.Username,
                    GroupName = "Test",//groupName,
                    Action = GroupAction.Add
                });

            return groupName;
        }
    }
}
