using System;
using System.IO;
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

namespace LCU.State.API.NapkinIDE.UserManagement
{
    [Serializable]
    [DataContract]
    public class UserBillingState
    {
        [DataMember]
        public virtual bool Loading { get; set; }

        [DataMember]
        public virtual string PaymentMethodID { get; set; }

        [DataMember]
        public virtual List<BillingPlanOption> Plans { get; set; }

        [DataMember]
        public virtual Status Status { get; set; }
    }

    [Serializable]
    [DataContract]
    public class BillingPlanOption
    {
        [DataMember]
        public virtual string Description { get; set; }

        [DataMember]
        public virtual string Lookup { get; set; }

        [DataMember]
        public virtual string Name { get; set; }

        [DataMember]
        public virtual double Price { get; set; }
    }
}
