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
        public virtual async Task LoadBillingPlans()
        {
            State.Plans = new List<BillingPlanOption>()
            {
                new BillingPlanOption() 
                {
                    Description = "Billing Plan 1 description",
                    Lookup = "plan1",
                    Name = "Billing Plan 1",
                    Price = 20
                },
                new BillingPlanOption() 
                {
                    Description = "Billing Plan 2 description",
                    Lookup = "plan1",
                    Name = "Billing Plan 2",
                    Price = 100
                },
                new BillingPlanOption() 
                {
                    Description = "Billing Plan 3 description",
                    Lookup = "plan3",
                    Name = "Billing Plan 3",
                    Price = 200
                }
            };
        }

        public virtual void SetUsername(string username)
        {
            State.Username = username;
        }

        public virtual void SetPaymentMethod(string methodId)
        {
            State.PaymentMethodID = methodId;
        }
        #endregion
    }
}
