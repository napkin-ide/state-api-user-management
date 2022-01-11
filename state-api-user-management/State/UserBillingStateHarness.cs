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
using LCU.Personas.Client.DevOps;
using LCU.Personas.Client.Applications;
using LCU.Personas.Client.Identity;
using Fathym.API;
using LCU.Personas.Client.Security;
using LCU.Graphs.Registry.Enterprises.Identity;
using LCU.State.API.NapkinIDE.UserManagement.Management;
using Newtonsoft.Json.Linq;
using Fathym.Design;
using LCU.State.API.UserManagement.Host.TempRefit;

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

        public virtual async Task ChangeSubscription(IEnterprisesBillingManagerService entBillingMgr, ISecurityDataTokenService secMgr, IIdentityAccessService idMgr, string entLookup,
            string username, string customerName, string plan)
        {
            //cancel existing subscription 
            var cancelResp = await entBillingMgr.CancelSubscriptionByUser(username, entLookup);

            var planOption = this.State.Plans.First(p => p.Lookup == plan);

            var licenseType = planOption.Metadata["LicenseType"].ToString();

            //Remove license access
            await idMgr.RevokeLicense(entLookup, username, licenseType);

            // create new subscription
            var completeResp = await entBillingMgr.CompleteStripeSubscription(
                    new CompleteStripeSubscriptionRequest()
                    {
                        CustomerName = State.CustomerName,
                        Plan = plan,
                        Username = username,
                        TrialPeriodDays = 0
                    }, entLookup, licenseType);

            State.PaymentStatus = completeResp.Status;

            if (State.PaymentStatus)
            {
                State.PurchasedPlanLookup = plan;

                var tosResp = await secMgr.SetDataToken(new DataToken(){
                    Lookup = "LCU-USER-BILLING.TermsOfService",
                    Name = "LCU-USER-BILLING.TermsOfService",
                    Description = "Billing Terms of Service",
                    Value = DateTimeOffset.UtcNow.ToString(),                  
                });

                var eaResp = await secMgr.SetDataToken(new DataToken(){
                    Lookup = "LCU-USER-BILLING.EnterpriseAgreement",
                    Name = "LCU-USER-BILLING.EnterpriseAgreement",
                    Description = "Billing Enterprise Agreement",
                    Value = DateTimeOffset.UtcNow.ToString(),                  
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

        public virtual async Task CompletePayment(IEnterprisesBillingManagerService entMgr, ISecurityDataTokenService secMgr, IIdentityAccessService idMgr, string entLookup,
            string username, string methodId, string customerName, string plan, int trialPeriodDays)
        {
            State.CustomerName = customerName;

            State.PaymentMethodID = methodId;

            var planOption = this.State.Plans.First(p => p.Lookup == plan);

            var licenseTypeCore = planOption.Metadata["LicenseType"].ToString();

            var licenseTypes = planOption.Metadata.ContainsKey("LicenseTypeOverrides") ?
                planOption.Metadata["LicenseTypeOverrides"].ToString().Split('|', StringSplitOptions.RemoveEmptyEntries) : 
                new[] { licenseTypeCore };

            var completeResp = await entMgr.CompleteStripeSubscription(
                new CompleteStripeSubscriptionRequest()
                {
                    CustomerName = State.CustomerName,
                    PaymentMethodID = methodId,
                    Plan = plan,
                    TrialPeriodDays = trialPeriodDays,
                    Username = username
                }, entLookup, licenseTypeCore);

            State.PaymentStatus = completeResp.Status;

            if (State.PaymentStatus.Code == 0 )
            {
                State.PurchasedPlanLookup = plan;

                var tosResp = await secMgr.SetDataToken(new DataToken(){
                    Lookup = "LCU-USER-BILLING.TermsOfService",
                    Name = "LCU-USER-BILLING.TermsOfService",
                    Description = "Billing Terms of Service",
                    Value = DateTimeOffset.UtcNow.ToString(),                  
                });

                var eaResp = await secMgr.SetDataToken(new DataToken(){
                    Lookup = "LCU-USER-BILLING.EnterpriseAgreement",
                    Name = "LCU-USER-BILLING.EnterpriseAgreement",
                    Description = "Billing Enterprise Agreement",
                    Value = DateTimeOffset.UtcNow.ToString(),                  
                });

                var setLicenseAccessResp = await DesignOutline.Instance.Chain<BaseResponse>()
                    .AddResponsibilities(licenseTypes.Select<string, Func<BaseResponse>>(licenseType =>
                    {
                        return () =>
                        {
                            var token = new LicenseAccessToken()
                            {
                                Details = planOption.JSONConvert<MetadataModel>(),
                                EnterpriseLookup = entLookup,
                                Lookup = licenseType,
                                AccessStartDate = DateTime.Now,
                                TrialPeriodDays = trialPeriodDays,
                                Username = username
                            };

                            var latResp = idMgr.IssueLicenseAccess(token, entLookup).Result;

                            return latResp;
                        };
                    }).ToArray())
                    .SetShouldContinue(latResp => latResp.Status)
                    .Run();

                State.PaymentStatus = setLicenseAccessResp.Status;

                State.SubscriptionID = completeResp.SubscriptionID;

                State.SuccessRedirect = planOption.Metadata["SuccessRedirect"].ToString();
            }

            else{
                //TODO handle when payment fails but subscription is assigned
            }
        }

        public virtual async Task DetermineRequiredOptIns(ISecurityDataTokenService secMgr, string entLookup, string username)
        {
            var tosDataToken = await secMgr.GetDataToken("LCU-USER-BILLING.TermsOfService", entLookup, username);

            var eaDataToken = await secMgr.GetDataToken("LCU-USER-BILLING.EnterpriseAgreement", entLookup, username);

            State.RequiredOptIns = new List<string>();

            if (!tosDataToken.Status || tosDataToken.Model.Lookup != "LCU-USER-BILLING.TermsOfService")
                State.RequiredOptIns.Add("ToS");

            if (!eaDataToken.Status || eaDataToken.Model.Lookup != "LCU-USER-BILLING.EnterpriseAgreement")
                State.RequiredOptIns.Add("EA");
        }

        public virtual async Task<Status> HandleChargeFailed(IEnterprisesBillingManagerService entBillingMgr, IIdentityAccessService idMgr, string entLookup, string userEmail, Stripe.Event stripeEvent)
        {

            string fromEmail = "alerts@fathym.com";

            string supportEmail = "support@fathym.com";

            State.SuspendAccountOn = DateTime.Now.AddDays(15);

            string suspendOnStr = State.SuspendAccountOn.ToString();

            State.PaymentStatus = Status.Conflict;

            log.LogInformation($"Users State {State.ToJSON()}");

            var usersLics = await entBillingMgr.GetCustomersIncompleteLicenseTypes(userEmail, entLookup);

            log.LogInformation($"Users licenses {usersLics}");

            if(usersLics.Model.IsNullOrEmpty()){
                //existing user with license              

                //email the user that their cc needs to be updated and the charge failed with link to update cc
            
            var suspensionNotice = new SendNotificationRequest()
                {
                    
                        EmailFrom = fromEmail,
                        EmailTo = userEmail,
                        dynamic_template_data = new TemplateDataModel 
                            {
                                suspendOn = suspendOnStr
                            },
                        template_id = "d-b7fb6618e8d3466b94bffd27e5a43f16"
                    
                };
            await SendTemplateEmail(entBillingMgr, entLookup, suspensionNotice);

            //email fathym support about the card failure
            var cardFailedNotice = new SendNotificationRequest()
                {
                    EmailFrom = fromEmail,
                    EmailTo = supportEmail,
                    dynamic_template_data = new TemplateDataModel 
                            {
                                userName = userEmail,
                                suspendOn = suspendOnStr
                            },
                        template_id = "d-8048d19cfc264ca6a364a964d1deec76"
                };
            await SendTemplateEmail(entBillingMgr, entLookup, cardFailedNotice);
            }

            if(!usersLics.Model.IsNullOrEmpty()){
                //new user signup that failed

                var ccFailedNotice = new SendNotificationRequest()
                {
                        EmailFrom = fromEmail,
                        EmailTo = userEmail,
                        dynamic_template_data = new TemplateDataModel 
                            { },
                        template_id = "d-ecd308931cc54e4f91f5d795f323cd95"
                };
            await SendTemplateEmail(entBillingMgr, entLookup, ccFailedNotice);
            }   


            //TODO automate pause the users account with fathym after 15 day grace period once event is recieved

            //TODO automate once 15 day grace period has passed suspend the users account and notify the user.

            return Status.Success;

            // throw new NotImplementedException();
        }
        
        public virtual async Task<Status> ListLicenses(IIdentityAccessService idAccessSvc, string entLookup, string username, string licenseType)
        {
            var licenseAccess = await idAccessSvc.ListLicensesByUsername(entLookup, username, new List<string>() { licenseType });

            State.ExistingLicenseTypes = licenseAccess.Model;

            return (licenseAccess != null) ? Status.Success : Status.Unauthorized.Clone($"No licenses found for user {username}");
        }

        public virtual async Task LoadBillingPlans(IEnterprisesBillingManagerService entBillingMgr, string entLookup, string licenseType)
        {
            var plansResp = await entBillingMgr.ListBillingPlanOptions(entLookup, licenseType);

            State.Plans = plansResp.Model ?? new List<API.UserManagement.Host.TempRefit.BillingPlanOption>();

            State.FeaturedPlanGroup = State.Plans.FirstOrDefault(plan =>
            {
                return plan.Metadata.ContainsKey("Featured") && plan.Metadata["Featured"].ToObject<bool>();
            })?.PlanGroup;

            State.PopularPlanGroup = State.Plans.FirstOrDefault(plan =>
            {
                return plan.Metadata.ContainsKey("Popular") && plan.Metadata["Popular"].ToObject<bool>();
            })?.PlanGroup;
        }

        public virtual async Task Refresh(IEnterprisesBillingManagerService entBillingMgr, IIdentityAccessService idAccessSvc, ISecurityDataTokenService secMgr, string entLookup, string username, string licenseType)
        {
            ResetStateCheck();

            await LoadBillingPlans(entBillingMgr, entLookup, licenseType);

            SetUsername(username);

            await DetermineRequiredOptIns(secMgr, entLookup, username);

            await ListLicenses(idAccessSvc, entLookup, username, licenseType);
        }

        public virtual void ResetStateCheck(bool force = false)
        {
            if (force || State.PaymentStatus)
                State = new UserBillingState();

            if (force)
                State = new UserBillingState();
        }

        public virtual void SetUsername(string username)
        {
            State.Username = username;
        }

        // public virtual async Task<Status> SendNotification(IEnterprisesBillingManagerService entMgr, string entLookup, string username, SendNotificationRequest notification)
        // {
        //     // Send email from app manager client 
        //     var model = new MetadataModel();

        //     model.Metadata.Add(new KeyValuePair<string, JToken>("SendNotificationRequest", JToken.Parse(JsonConvert.SerializeObject(notification))));

        //     await entMgr.SendNotificationEmail(model, entLookup);

        //     return Status.Success;
        // }

        public virtual async Task<Status> SendTemplateEmail(IEnterprisesBillingManagerService entBillingMgr, string entLookup, SendNotificationRequest notification)
        {
            // Send email from app manager client 
            var model = new MetadataModel();

            model.Metadata.Add(new KeyValuePair<string, JToken>("TemplateEmail", JToken.Parse(JsonConvert.SerializeObject(notification))));

            // await entBillingMgr.SendTemplateEmail(model, entLookup);

            return Status.Success;
        }

        public virtual async Task UpdatePaymentInfo(IEnterprisesBillingManagerService entBillingMgr, ISecurityDataTokenService secMgr, string entLookup,
            string username, string methodId, string customerName)
        {
            State.CustomerName = customerName;

            State.PaymentMethodID = methodId;

            var updateResp = await entBillingMgr.UpdateStripeSubscription(entLookup,
                    new UpdateStripeSubscriptionRequest()
                    {
                        CustomerName = State.CustomerName,
                        PaymentMethodID = methodId,
                        Username = username
                    });

            State.PaymentStatus = updateResp.Status;

            if (State.PaymentStatus)
            {

                var tosResp = await secMgr.SetDataToken(new DataToken(){
                    Lookup = "LCU-USER-BILLING.TermsOfService",
                    Name = "LCU-USER-BILLING.TermsOfService",
                    Description = "Billing Terms of Service",
                    Value = DateTimeOffset.UtcNow.ToString(),                  
                });

                var eaResp = await secMgr.SetDataToken(new DataToken(){
                    Lookup = "LCU-USER-BILLING.EnterpriseAgreement",
                    Name = "LCU-USER-BILLING.EnterpriseAgreement",
                    Description = "Billing Enterprise Agreement",
                    Value = DateTimeOffset.UtcNow.ToString(),                  
                });
            }

        }
        #endregion
    }
}