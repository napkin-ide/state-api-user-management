using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Fathym;
using Microsoft.Azure.Storage.Blob;
using System.IO;
using LCU.StateAPI.Utilities;
using LCU.Personas.Client.Identity;
using LCU.State.API.NapkinIDE.UserManagement.State;
using LCU.State.API.UserManagement.Host.TempRefit;

namespace LCU.State.API.NapkinIDE.UserManagement.Management
{
    [Serializable]
	[DataContract]
	public class ListSubcribersRequest
	{
		

    }


    public class ListSubcribers
    {
        protected IIdentityAccessService idMgr;

        public ListSubcribers(IIdentityAccessService idMgr)
        {
            this.idMgr = idMgr;
        }

        [FunctionName("ListSubscribers")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, ListSubcribersRequest, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData, actReq) =>
            {
                log.LogInformation($"List subscribers");
                
                var stateDetails = StateUtils.LoadStateDetails(req);

				await harness.ListSubscribers(idMgr, stateDetails.EnterpriseLookup);

                return Status.Success;
            });
        }
    }
}