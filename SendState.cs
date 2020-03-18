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
using Microsoft.WindowsAzure.Storage.Blob;

namespace LCU.State.API.NapkinIDE.UserManagement
{
    public static class SendState
    {
        // public const string BLOB_TRIGGER_PATH= "state-api/{entApiKey}/usermanagement/{username}/{stateKey}";

        // [FunctionName("SendState")]
        // public static async Task Run(ILogger log,
        //     [BlobTrigger(BLOB_TRIGGER_PATH)] CloudBlockBlob stateBlob,
        //     string entApiKey, string username, string stateKey,
        //     [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages)
        // {
        //     await signalRMessages.EmitState<UserManagementState>(stateBlob, new StateDetails()
        //     {
        //         EnterpriseAPIKey = entApiKey,
        //         HubName = UserManagementState.HUB_NAME, 
        //         StateKey = stateKey, 
        //         Username = username
        //     }, log);
        // }
    }
}