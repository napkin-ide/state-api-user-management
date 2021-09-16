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
using LCU.Personas.Client.Identity;
using LCU.StateAPI.Utilities;
using Fathym.API;
using LCU.State.API.NapkinIDE.UserManagement.State;
using LCU.State.API.UserManagement.Host.TempRefit;

namespace LCU.State.API.NapkinIDE.UserManagement.Management
{
    [Serializable]
    [DataContract]
    public class ListLicensesRequest : BaseRequest
    {
        [DataMember]
        public string LicenseType { get; set; }

    }

    public class ListLicenses
    {
        protected IIdentityAccessService idMgr;

        public ListLicenses(IIdentityAccessService idMgr)
        {
            this.idMgr = idMgr;
        }

        [FunctionName("ListLicenses")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-lookup}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, ListLicensesRequest, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData) =>
            {
                log.LogInformation($"Executing ListLicenses Action.");

                var stateDetails = StateUtils.LoadStateDetails(req);

                var status = await harness.ListLicenses(idMgr, stateDetails.EnterpriseLookup, stateDetails.Username, reqData.LicenseType);

                return status;
            });
        }
    }
}
