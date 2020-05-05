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
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Fathym;
using LCU.Personas.Client.Identity;
using LCU.StateAPI.Utilities;
using Fathym.API;

namespace LCU.State.API.NapkinIDE.Setup
{
    [Serializable]
    [DataContract]
    public class HasLicenseRequest : BaseRequest
    {
        [DataMember]
        public string Lookup { get; set; }

    }

    public class ListLicenses
    {
        protected IdentityManagerClient idMgr;

        public ListLicenses(IdentityManagerClient idMgr)
        {
            this.idMgr = idMgr;
        }

        [FunctionName("HasLicense")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, HasLicenseRequest, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData) =>
            {
                log.LogInformation($"Executing ListLicenses Action.");

                var stateDetails = StateUtils.LoadStateDetails(req);

                var status = await harness.HasLicenseAccess(idMgr, stateDetails.EnterpriseAPIKey, stateDetails.Username);

                return status;
            });
        }
    }
}
