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
using System.Net;
using System.Net.Http;
using System.Text;
using LCU.StateAPI;
using LCU.State.API.UserManagement.Host.TempRefit;

namespace LCU.State.API.NapkinIDE.UserManagement.Billing
{
    public class ListBillingOptions
    {
        protected readonly IEnterprisesBillingManagerService entBillingMgr;

        public ListBillingOptions(IEnterprisesBillingManagerService entBillingMgr)
        {
            this.entBillingMgr = entBillingMgr;
        }

        [FunctionName("ListBillingOptions")]
        [FunctionResponseCache(600, ResponseCacheLocation.Any)]
        public virtual async Task<HttpResponseMessage> Run([HttpTrigger] HttpRequest req, ILogger log)
        {
            var stateDetails = StateUtils.LoadStateDetails(req);

            var entLookup = stateDetails.EnterpriseLookup;

            var licenseType = req.Query["licenseType"];

            log.LogInformation($"ListBillingPlanOptions with {entLookup} for {licenseType}.");

            var plansResp = await entBillingMgr.ListBillingPlanOptions(entLookup, licenseType);

            log.LogInformation($"Plans response: {plansResp.Status.ToJSON()}");

            var statusCode = plansResp.Status || plansResp.Status == Status.NotLocated ? HttpStatusCode.OK : HttpStatusCode.InternalServerError;

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(plansResp.ToJSON(), Encoding.UTF8, "application/json")
            };
        }
    }
}
