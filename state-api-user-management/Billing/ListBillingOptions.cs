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
using Fathym.API;
using Fathym;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using LCU.Personas.Client.Enterprises;
using LCU.StateAPI.Utilities;
using LCU.Personas.Client.Security;
using Microsoft.Extensions.Configuration;
using LCU.State.API.NapkinIDE.UserManagement.State;
using LCU.Personas.Client.Identity;
using System.Collections.Generic;
using LCU.Personas.Enterprises;

namespace LCU.State.API.NapkinIDE.UserManagement.Billing
{
    public class ListBillingOptions
    {
        protected readonly EnterpriseManagerClient entMgr;

        public ListBillingOptions(EnterpriseManagerClient entMgr)
        {
            this.entMgr = entMgr;
        }

        [FunctionName("ListBillingOptions")]
        public virtual async Task<BaseResponse<List<BillingPlanOption>>> Run([HttpTrigger] HttpRequest req, ILogger log)
        {
            var entLookup = req.Headers["lcu-ent-lookup"];
            
            var licenseType = req.Query["licenseType"];

            log.LogInformation($"ListBillingPlanOptions with {entLookup} for {licenseType}.");
            
            var plansResp = await entMgr.ListBillingPlanOptions(entLookup, licenseType);

            log.LogInformation($"Plans response: {plansResp.Status.ToJSON()}");

            return plansResp;
        }
    }
}
