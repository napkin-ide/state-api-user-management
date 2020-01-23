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
using System.Security.Claims;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using LCU.State.API.NapkinIDE.User.Management.Utils;
using Microsoft.WindowsAzure.Storage.Blob;

namespace LCU.State.API.NapkinIDE.User.Management
{
    public static class SendState
    {
        [FunctionName("SendState")]
        public static async Task Run([BlobTrigger("state/{statePath}")] string state, string statePath, ILogger logger,
            [Blob("state", FileAccess.Read)] CloudBlobContainer blobContainer)
        {
            var stateDetails = StateUtils.LoadStateDetails(statePath);

            var stateCfgPath = $"{stateDetails.EnterpriseAPIKey}/{stateDetails.HubName}/__config.lcu";

            var stateCfgBlob = blobContainer.GetBlockBlobReference(stateCfgPath);

            var stateCfg = StateUtils.ParseStateConfig(await stateCfgBlob.DownloadTextAsync());

            var groupName = StateUtils.BuildGroupName(stateDetails, stateCfg);

            var context = await StateUtils.LoadHubContext(stateDetails.HubName);

            var sendMethod = $"ReceiveState=>{groupName}";

            await context.Clients.Group(groupName).SendCoreAsync(sendMethod, new[] { state.FromJSON<object>() });
        }
    }
}
