
using System;
using System.Threading.Tasks;
using LCU.Presentation.State;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;

namespace LCU.State.API.NapkinIDE.User.Management.Utils
{
    public class StateDetails
    {
        public virtual string EnterpriseAPIKey { get; set; }
        
        public virtual string HubName { get; set; }
        
        public virtual string stateKey { get; set; }
        
        public virtual string Username { get; set; }
    }
}