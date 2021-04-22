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
using LCU.Graphs.Registry.Enterprises.Identity;

namespace LCU.State.API.NapkinIDE.UserManagement.State
{
    public class UserBillingStateHarness : LCUStateHarness<UserBillingState>
    {
        #region Fields 
        #endregion

        #region Properties 
        #endregion

        #region Constructors
        public UserBillingStateHarness(UserBillingState state, ILogger log)
            : base(state ?? new UserBillingState(), log)
        { }
        #endregion

        #region API Methods

        public virtual async Task ChangeSubscription(EnterpriseManagerClient entMgr, SecurityManagerClient secMgr, IdentityManagerClient idMgr, string entLookup,
            string username,  string customerName, string plan)
        {
            //cancel existing subscription 
            var cancelResp = await entMgr.CancelSubscriptionByUser(username, entLookup);

            var planOption = this.State.Plans.First(p => p.Lookup == plan);

            var licenseType = planOption.Metadata["LicenseType"].ToString();

            //Remove license access
            await idMgr.RevokeLicenseAccess(entLookup, username, licenseType );

            

            // create new subscription
            var completeResp = await entMgr.CompleteStripeSubscription(entLookup, licenseType,
                    new CompleteStripeSubscriptionRequest()
                    {
                        CustomerName = State.CustomerName,
                        Plan = plan,
                        Username = username,
                        TrialPeriodDays = 0
                    });

            State.PaymentStatus = completeResp.Status;

            if (State.PaymentStatus)
            {
                State.PurchasedPlanLookup = plan;

                var resp = await secMgr.SetIdentityThirdPartyData(entLookup, username, new Dictionary<string, string>()
                {
                    { "LCU-USER-BILLING.TermsOfService", DateTimeOffset.UtcNow.ToString() },
                    { "LCU-USER-BILLING.EnterpriseAgreement", DateTimeOffset.UtcNow.ToString() },
                    { "LCU-STRIPE-SUBSCRIPTION-ID", completeResp.SubscriptionID}
                });

            //issue new license access
            var setLicenseAccessResp = await idMgr.IssueLicenseAccess(new LicenseAccessToken()
            {
                AccessStartDate = System.DateTime.Now,
                Details = planOption.JSONConvert<MetadataModel>(),
                EnterpriseLookup = entLookup,
                Lookup = licenseType,
                TrialPeriodDays = 0,
                Username = username
            }, entLookup);

            State.PaymentStatus = setLicenseAccessResp.Status;

            State.SubscriptionID = completeResp.SubscriptionID;

            State.SuccessRedirect = planOption.Metadata["SuccessRedirect"].ToString();
            }

            await ListLicenses(idMgr, entLookup, username, licenseType);
            
            State.Loading = false;
        }

        public virtual async Task CompletePayment(EnterpriseManagerClient entMgr, SecurityManagerClient secMgr, IdentityManagerClient idMgr, string entLookup,
            string username, string methodId, string customerName, string plan, int trialPeriodDays)
        {
            State.CustomerName = customerName;

            State.PaymentMethodID = methodId;

            var planOption = this.State.Plans.First(p => p.Lookup == plan);

            var licenseType = planOption.Metadata["LicenseType"].ToString();

            var completeResp = await entMgr.CompleteStripeSubscription(entLookup, licenseType,
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

                var resp = await secMgr.SetIdentityThirdPartyData(entLookup, username, new Dictionary<string, string>()
                {
                    { "LCU-USER-BILLING.TermsOfService", DateTimeOffset.UtcNow.ToString() },
                    { "LCU-USER-BILLING.EnterpriseAgreement", DateTimeOffset.UtcNow.ToString() },
                    { "LCU-STRIPE-SUBSCRIPTION-ID", completeResp.SubscriptionID}
                });

                var token = new LicenseAccessToken();

                token.Details = planOption.JSONConvert<MetadataModel>();

                token.EnterpriseLookup = entLookup;

                token.Lookup = licenseType;

                token.AccessStartDate = DateTime.Now;

                token.TrialPeriodDays = trialPeriodDays;

                token.Username = username;

                var setLicenseAccessResp = await idMgr.IssueLicenseAccess(token, entLookup);

                State.PaymentStatus = setLicenseAccessResp.Status;

                State.SubscriptionID = completeResp.SubscriptionID;

                State.SuccessRedirect = planOption.Metadata["SuccessRedirect"].ToString();
            }
        }

        public virtual async Task DetermineRequiredOptIns(SecurityManagerClient secMgr, string entLookup, string username)
        {
            var thirdPartyData = await secMgr.RetrieveIdentityThirdPartyData(entLookup, username, "LCU-USER-BILLING.TermsOfService", "LCU-USER-BILLING.EnterpriseAgreement");

            State.RequiredOptIns = new List<string>();

            if (!thirdPartyData.Status || !thirdPartyData.Model.ContainsKey("LCU-USER-BILLING.TermsOfService"))
                State.RequiredOptIns.Add("ToS");

            if (!thirdPartyData.Status || !thirdPartyData.Model.ContainsKey("LCU-USER-BILLING.EnterpriseAgreement"))
                State.RequiredOptIns.Add("EA");
        }

        public virtual async Task<Status> HandleChargeFailed(Stripe.Event stripeEvent)
        {
            throw new NotImplementedException();
        }
        
        public virtual async Task<Status> ListLicenses(IdentityManagerClient idMgr, string entLookup, string username, string licenseType)
        {
            var licenseAccess = await idMgr.ListLicenseAccessTokens(entLookup, username, new List<string>() { licenseType });

            State.ExistingLicenseTypes = licenseAccess.Model;

            return (licenseAccess != null) ? Status.Success : Status.Unauthorized.Clone($"No licenses found for user {username}");
        }

        public virtual async Task LoadBillingPlans(EnterpriseManagerClient entMgr, string entLookup, string licenseType)
        {
            var plansResp = await entMgr.ListBillingPlanOptions(entLookup, licenseType);

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

        public virtual async Task Refresh(EnterpriseManagerClient entMgr, IdentityManagerClient idMgr, SecurityManagerClient secMgr, string entLookup, string username, string licenseType)
        {
            ResetStateCheck();

            await LoadBillingPlans(entMgr, entLookup, licenseType);

            SetUsername(username);

            await DetermineRequiredOptIns(secMgr, entLookup, username);

            await ListLicenses(idMgr, entLookup, username, licenseType);
        }

        public virtual void ResetStateCheck(bool force = false)
        {
            // if (force || State.PaymentStatus)
            //     State = new UserBillingState();

            if (force)
                State = new UserBillingState();
        }

        public virtual void SetUsername(string username)
        {
            State.Username = username;
        }

        public virtual async Task UpdatePaymentInfo(EnterpriseManagerClient entMgr, SecurityManagerClient secMgr, string entLookup,
            string username, string methodId, string customerName)
        {
            State.CustomerName = customerName;

            State.PaymentMethodID = methodId;

            var updateResp = await entMgr.UpdateStripeSubscription(entLookup,
                    new UpdateStripeSubscriptionRequest()
                    {
                        CustomerName = State.CustomerName,
                        PaymentMethodID = methodId,
                        Username = username
                    });

            State.PaymentStatus = updateResp.Status;

            if (State.PaymentStatus)
            {

                var resp = await secMgr.SetIdentityThirdPartyData(entLookup, username, new Dictionary<string, string>()
                {
                    { "LCU-USER-BILLING.TermsOfService", DateTimeOffset.UtcNow.ToString() },
                    { "LCU-USER-BILLING.EnterpriseAgreement", DateTimeOffset.UtcNow.ToString() },
                });
            }

        }
        #endregion
    }
}