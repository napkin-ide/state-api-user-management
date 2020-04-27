using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Fathym;
using LCU.Presentation.State.ReqRes;
using LCU.StateAPI.Utilities;
using LCU.StateAPI;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;
using System.Collections.Generic;
using LCU.Personas.Client.Enterprises;
using LCU.Personas.Client.DevOps;
using LCU.Personas.Enterprises;
using LCU.Personas.Client.Applications;
using LCU.Personas.Client.Identity;
using Fathym.API;
using LCU.Personas.Client.Security;

namespace LCU.State.API.NapkinIDE.UserManagement
{
    public class UserBillingStateHarness : LCUStateHarness<UserBillingState>
    {
        #region Fields 
        #endregion

        #region Properties 
        #endregion

        #region Constructors
        public UserBillingStateHarness(UserBillingState state)
            : base(state ?? new UserBillingState())
        { }
        #endregion

        #region API Methods
        public virtual async Task DetermineRequiredOptIns(SecurityManagerClient secMgr, string entApiKey, string username)
        {
            var thirdPartyData = await secMgr.RetrieveIdentityThirdPartyData(entApiKey, username, "LCU-USER-BILLING.TermsOfService", "LCU-USER-BILLING.EnterpriseAgreement");

            State.RequiredOptIns = new List<string>();

            if (!thirdPartyData.Status || !thirdPartyData.Model.ContainsKey("LCU-USER-BILLING.TermsOfService"))
                State.RequiredOptIns.Add("ToS");

            if (!thirdPartyData.Status || !thirdPartyData.Model.ContainsKey("LCU-USER-BILLING.EnterpriseAgreement"))
                State.RequiredOptIns.Add("EA");
        }

        public virtual async Task LoadBillingPlans(EnterpriseManagerClient entMgr, string entApiKey)
        {
            var plansResp = await entMgr.ListBillingPlanOptions(entApiKey, "all");

            State.Plans = plansResp.Model ?? new List<BillingPlanOption>();

            State.Plans = State.Plans.OrderBy(p => p.Interval).ToList();

            State.FeaturedPlanGroup = "pro";//State.Plans.LastOrDefault()?.PlanGroup;
        }

        public virtual void SetUsername(string username)
        {
            State.Username = username;
        }

        public virtual async Task CompletePayment(EnterpriseManagerClient entMgr, SecurityManagerClient secMgr, string entApiKey, string username, string methodId, string customerName, 
            string plan)
        {
            State.CustomerName = customerName;

            State.PaymentMethodID = methodId;

            // var completeResp = await entMgr.CompleteStripeSubscription(entApiKey, 
            var completeResp = await entMgr.Post<CompleteStripeSubscriptionRequest, CompleteStripeSubscriptionResponse>($"billing/{entApiKey}/stripe/subscription",
                    new CompleteStripeSubscriptionRequest()
                    {
                        CustomerName = State.CustomerName,
                        PaymentMethodID = methodId,
                        Plan = plan,
                        Username = username
                    });

            State.PaymentStatus = completeResp.Status;

            if (State.PaymentStatus)
            {
                var resp = await secMgr.SetIdentityThirdPartyData(entApiKey, username, new Dictionary<string, string>()
                {
                    { "LCU-USER-BILLING.TermsOfService", DateTimeOffset.UtcNow.ToString() },
                    { "LCU-USER-BILLING.EnterpriseAgreement", DateTimeOffset.UtcNow.ToString() }
                });
            }
        }

        public virtual async Task Refresh(EnterpriseManagerClient entMgr, SecurityManagerClient secMgr, string entApiKey, string username)
        {
            ResetStateCheck();

            await LoadBillingPlans(entMgr, entApiKey);

            SetUsername(username);

            await DetermineRequiredOptIns(secMgr, entApiKey, username);
        }

        public virtual void ResetStateCheck(bool force = false)
        {
            if (force || State.PaymentStatus)
                State = new UserBillingState();
        }
        #endregion
    }
}
