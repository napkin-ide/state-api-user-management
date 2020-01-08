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
        public static async Task<ConnectToStateResponse> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")]HttpRequest req, ILogger logger, ClaimsPrincipal claimsPrincipal,
            [SignalR(HubName = "{headers.lcu-hub-name}")]IAsyncCollector<SignalRGroupAction> signalRGroupActions,
            [Blob("state/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/__config.lcu", FileAccess.Read)] string stateCfgStr)
        {
            try
            {
                var stateDetails = StateUtils.LoadStateDetails(req, claimsPrincipal);

                logger.LogInformation($"Connecting to state {stateDetails.HubName}.");

                var stateCfg = stateCfgStr.FromJSON<LCUStateConfiguration>();

                var context = await StaticServiceHubContextStore.Get().GetAsync("HubName");

                var userIdClaim = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier);

                var groupName = await groupClient(signalRGroupActions, stateCfg, stateDetails);

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

        private static async Task<string> groupClient(IAsyncCollector<SignalRGroupAction> signalRGroupActions, LCUStateConfiguration stateCfg,
            StateDetails stateDetails)
        {
            var groupName = StateUtils.BuildGroupName(stateDetails, stateCfg);

            await signalRGroupActions.AddAsync(
                new SignalRGroupAction
                {
                    UserId = stateDetails.Username,
                    GroupName = groupName,
                    Action = GroupAction.Add
                });

            return groupName;
        }
    }
}
