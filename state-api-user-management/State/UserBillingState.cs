using System;
using Fathym;
using System.Runtime.Serialization;
using System.Collections.Generic;
using LCU.Personas.Enterprises;
using LCU.Graphs.Registry.Enterprises.Identity;

namespace LCU.State.API.NapkinIDE.UserManagement.State
{
    [Serializable]
    [DataContract]
    public class UserBillingState
    {
        [DataMember]
        public virtual string CustomerName { get; set; }

        [DataMember]
        public virtual List<LicenseAccessToken> ExistingLicenseTypes { get; set; }

        [DataMember]
        public virtual string FeaturedPlanGroup { get; set; }

        [DataMember]
        public virtual LicenseTypeDetails LicenseType { get; set; }

        [DataMember]
        public virtual bool Loading { get; set; }

        [DataMember]
        public virtual DateTime NextBillingDate { get; set; }

        [DataMember]
        public virtual string PaymentMethodID { get; set; }

        [DataMember]
        public virtual Status PaymentStatus { get; set; }

        [DataMember]
        public virtual List<BillingPlanOption> Plans { get; set; }

        [DataMember]
        public virtual string PopularPlanGroup { get; set; }

        [DataMember]
        public virtual string PurchasedPlanLookup { get; set; }

        [DataMember]
        public virtual List<string> RequiredOptIns { get; set; }

        [DataMember]
        public virtual Status Status { get; set; }

        [DataMember]
        public virtual string SubscriptionID { get; set; }
  

        [DataMember]
        public virtual string SuccessRedirect { get; set; }

        [DataMember]
        public virtual DateTime SuspendAccountOn { get; set; }

        [DataMember]
        public virtual string Username { get; set; }
        
    }

    [DataContract]
    public class LicenseTypeDetails
    {
        [DataMember]
        public virtual string Lookup { get; set; }

        [DataMember]
        public virtual string Name { get; set; }
    }

    [DataContract]
    public class TemplateEmailModel
    {
        [DataMember]
        public virtual string EmailFrom { get; set; }

        [DataMember]
        public virtual string EmailTo { get; set; }

        [DataMember]
        public virtual TemplateDataModel TemplateData { get; set; }
                            
        [DataMember]
        public virtual string TemplateId { get; set; }

    }

    [DataContract]
    public class TemplateDataModel
    {
        [DataMember]
        public virtual string suspendOn { get; set; }

        [DataMember]
        public virtual string userName { get; set; }

    }
}
