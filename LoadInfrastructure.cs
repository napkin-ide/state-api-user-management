using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Fathym;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;
using LCU.StateAPI.Utilities;
using LCU.Personas.Client.Enterprises;

namespace LCU.State.API.NapkinIDE.UserManagement
{
    [Serializable]
	[DataContract]
	public class LoadInfrastructureRequest
	{
		[DataMember]
		public virtual string EnvLookup { get; set; }

        
		[DataMember]
		public virtual string Type { get; set; }

    }


    public class LoadInfrastructure
    {
        protected EnterpriseManagerClient entMgr;

        public LoadInfrastructure(EnterpriseManagerClient entMgr)
        {
            this.entMgr = entMgr;
        }

        [FunctionName("LoadInfrastructure")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, LoadInfrastructureRequest, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData, actReq) =>
            {
                log.LogInformation($"Load infrastructure details");
                
                var stateDetails = StateUtils.LoadStateDetails(req);

				await harness.LoadInfrastructure(entMgr, stateDetails.EnterpriseAPIKey, reqData.EnvLookup, reqData.Type);

                return Status.Success;
            });
        }
    }
}