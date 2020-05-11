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
using Fathym;
using LCU.State.API.NapkinIDE.UserManagement;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.WindowsAzure.Storage.Blob;
using LCU.Personas.Client.Enterprises;
using LCU.State.API.NapkinIDE.UserManagement.State;

namespace LCU.State.API.NapkinIDE.Setup.Management
{
    [Serializable]
    [DataContract]
    public class ConfigureInfrastructureRequest
    {
        [DataMember]
        public virtual string InfrastructureType { get; set; }

        [DataMember]
        public virtual MetadataModel Settings { get; set; }

        [DataMember]
        public virtual string Template { get; set; }

        [DataMember]
        public virtual bool UseDefaultSettings { get; set; }
    }

    public class ConfigureInfrastructure
    {
        protected EnterpriseArchitectClient entArch;

        protected EnterpriseManagerClient entMgr;

        public ConfigureInfrastructure(EnterpriseArchitectClient entArch, EnterpriseManagerClient entMgr)
        {
            this.entArch = entArch;

            this.entMgr = entMgr;
        }

        [FunctionName("ConfigureInfrastructure")]
        public async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<UserManagementState, ConfigureInfrastructureRequest, UserManagementStateHarness>(req, signalRMessages, log,
                async (harness, reqData) =>
            {
                log.LogInformation($"Executing SetUserDetails Action.");

                await harness.ConfigureInfrastructure(entArch, entMgr, reqData.InfrastructureType, reqData.UseDefaultSettings, reqData.Settings, reqData.Template);

                return Status.Success;
            });
        }
    }
}
