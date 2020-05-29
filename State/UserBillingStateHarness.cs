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

namespace LCU.State.API.NapkinIDE.UserManagement.State
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

        public virtual async Task LoadBillingPlans(EnterpriseManagerClient entMgr, string entApiKey, string licenseType)
        {
            var plansResp = await entMgr.ListBillingPlanOptions(entApiKey, licenseType);

            State.Plans = plansResp.Model ?? new List<BillingPlanOption>();

            State.FeaturedPlanGroup = State.Plans.FirstOrDefault(plan =>
            {
                return plan.Metadata.ContainsKey("Featured") && plan.Metadata["Featured"].ToObject<bool>();
            })?.PlanGroup;

            State.PopularPlanGroup = State.Plans.FirstOrDefault(plan =>
            {
                return plan.Metadata.ContainsKey("Popular") && plan.Metadata["Popular"].ToObject<bool>();
            })?.PlanGroup;
        }

        public virtual void SetUsername(string username)
        {
            State.Username = username;
        }

        public virtual async Task CompletePayment(EnterpriseManagerClient entMgr, SecurityManagerClient secMgr, IdentityManagerClient idMgr, string entApiKey,
            string username, string methodId, string customerName, string plan, int trialPeriodDays)
        {
            State.CustomerName = customerName;

            State.PaymentMethodID = methodId;

            // var completeResp = await entMgr.Post<CompleteStripeSubscriptionRequest, CompleteStripeSubscriptionResponse>($"billing/{entApiKey}/stripe/subscription",
            var completeResp = await entMgr.CompleteStripeSubscription(entApiKey,
                    new CompleteStripeSubscriptionRequest()
                    {
                        CustomerName = State.CustomerName,
                        PaymentMethodID = methodId,
                        Plan = plan,
                        TrialPeriodDays = trialPeriodDays,
                        Username = username
                    });

            State.PaymentStatus = completeResp.Status;

            if (State.PaymentStatus)
            {
                State.PurchasedPlanLookup = plan;

                var resp = await secMgr.SetIdentityThirdPartyData(entApiKey, username, new Dictionary<string, string>()
                {
                    { "LCU-USER-BILLING.TermsOfService", DateTimeOffset.UtcNow.ToString() },
                    { "LCU-USER-BILLING.EnterpriseAgreement", DateTimeOffset.UtcNow.ToString() }
                });

                var planOption = this.State.Plans.First(p => p.Lookup == plan);

                var licenseType = planOption.Metadata["LicenseType"].ToString();

                var setLicenseAccessResp = await idMgr.IssueLicenseAccess(new Graphs.Registry.Enterprises.Identity.LicenseAccessToken()
                {
                    EnterpriseAPIKey = entApiKey,
                    Lookup = licenseType,
                    AccessStartDate = DateTime.Now,
                    TrialPeriodDays = trialPeriodDays,
                    Username = username,
                    Metadata = planOption.JSONConvert<MetadataModel>().Metadata
                }, entApiKey);

                State.PaymentStatus = setLicenseAccessResp.Status;

                State.SuccessRedirect = licenseType == "lcu" ? "/workspace/new" : "https://forecast.fathym-it.com/";
            }
        }

        public virtual async Task Refresh(EnterpriseManagerClient entMgr, SecurityManagerClient secMgr, string entApiKey, string username, string licenseType)
        {
            ResetStateCheck();

            await LoadBillingPlans(entMgr, entApiKey, licenseType);

            var ltName = licenseType == "lcu" ? "Fathym Framework" : licenseType == "forecast" ? "Fathym Forecast" : "";

            State.LicenseType = new LicenseTypeDetails()
            {
                Lookup = licenseType,
                Name = ltName
            };

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
