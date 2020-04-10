using System;
using Fathym;
using System.Runtime.Serialization;
using System.Collections.Generic;
using LCU.Personas.Enterprises;

namespace LCU.State.API.NapkinIDE.UserManagement
{
    [Serializable]
    [DataContract]
    public class UserBillingState
    {
        [DataMember]
        public virtual string CustomerName { get; set; }

        [DataMember]
        public virtual bool Loading { get; set; }

        [DataMember]
        public virtual string PaymentMethodID { get; set; }

        [DataMember]
        public virtual Status PaymentStatus { get; set; }

        [DataMember]
        public virtual List<BillingPlanOption> Plans { get; set; }

        [DataMember]
        public virtual Status Status { get; set; }

        [DataMember]
        public virtual string Username { get; set; }
    }
}