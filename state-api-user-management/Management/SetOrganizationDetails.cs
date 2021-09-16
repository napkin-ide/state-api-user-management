using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using LCU.State.API.NapkinIDE.UserManagement;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Fathym;
using LCU.Personas.Client.Enterprises;
using LCU.State.API.NapkinIDE.UserManagement.State;
using LCU.State.API.UserManagement.Host.TempRefit;

namespace LCU.State.API.NapkinIDE.Setup.Management
{
	[Serializable]
	[DataContract]
	public class SetOrganizationDetailsRequest
	{
		[DataMember]
		public virtual string Description { get; set; }

		[DataMember]
		public virtual string Name { get; set; }

		[DataMember]
		public virtual string Lookup { get; set; }
	}

    public class SetOrganizationDetails
    {
        protected IEnterprisesBillingManagerService entMgr;

        public SetOrganizationDetails(IEnterprisesBillingManagerService entMgr)
        {
            this.entMgr = entMgr;
        }

        [FunctionName("SetOrganizationDetails")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, SetOrganizationDetailsRequest, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData) =>
            {
                log.LogInformation($"Executing SetOrganizationDetails Action.");

                await harness.SetOrganizationDetails(entMgr, reqData.Name, reqData.Description, reqData.Lookup, true);

                return Status.Success;
            });
        }
    }
}
