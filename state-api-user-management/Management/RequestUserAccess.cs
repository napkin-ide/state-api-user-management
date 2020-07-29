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
using LCU.Personas.Client.Applications;
using LCU.Personas.Client.Security;
using LCU.State.API.NapkinIDE.UserManagement.State;

namespace LCU.State.API.NapkinIDE.UserManagement.Management
{
   
    public class RequestUserAccess
    {
        protected IdentityManagerClient idMgr;

        protected SecurityManagerClient secMgr;

        protected ApplicationManagerClient appMgr;

        public RequestUserAccess(ApplicationManagerClient appMgr, SecurityManagerClient secMgr, IdentityManagerClient idMgr)
        {
            this.idMgr = idMgr;

            this.secMgr = secMgr;

            this.appMgr = appMgr;
        }

        [FunctionName("RequestUserAccess")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, dynamic, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData) =>
            {
                log.LogInformation($"Executing RequestUserAccess Action.");

                var stateDetails = StateUtils.LoadStateDetails(req);

                var status = await harness.RequestAuthorization(secMgr, appMgr, idMgr, stateDetails.Username, stateDetails.EnterpriseAPIKey, stateDetails.Host);

                return status;
            });
        }
    }
}
