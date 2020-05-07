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
using LCU.Personas.Enterprises;
using LCU.Graphs.Registry.Enterprises.Identity;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace LCU.State.API.NapkinIDE.UserManagement
{
    [Serializable]
    [DataContract]
    public class UserManagementState
    {
        #region Constants
        public const string HUB_NAME = "usermanagement";
        #endregion

        [DataMember]
        public virtual bool Booted { get; set; }

        [DataMember]
        public virtual List<BootOption> BootOptions { get; set; }

        [DataMember]
        public virtual List<JourneyDetail> Details { get; set; }

        [DataMember]
        public virtual string DevOpsAppID { get; set; }

        [DataMember]
        public virtual string DevOpsClientSecret { get; set; }

        [DataMember]
        public virtual string DevOpsScopes { get; set; }

        [DataMember]
        public virtual string EnvironmentLookup { get; set; }

        [DataMember]
        public virtual MetadataModel EnvSettings { get; set; }

        [DataMember]
        public virtual bool HasDevOpsOAuth { get; set; }

        [DataMember]
        public virtual string Host { get; set; }

        [DataMember]
        public virtual List<string> HostOptions { get; set; }

        [DataMember]
        public virtual Dictionary<string, string> InfrastructureOptions { get; set; }

        [DataMember]
        public virtual LicenseAccessToken FreeTrialToken { get; set; }

        [DataMember]
        public virtual bool Loading { get; set; }

        [DataMember]
        public virtual string NewEnterpriseAPIKey { get; set; }

        [DataMember]
        public virtual string OrganizationDescription { get; set; }

        [DataMember]
        public virtual string OrganizationLookup { get; set; }

        [DataMember]
        public virtual string OrganizationName { get; set; }

        [DataMember]
        public virtual string PaymentMethodID { get; set; }

        [DataMember]
        public virtual List<JourneyPersona> Personas { get; set; }

        [DataMember]
        [JsonConverter(typeof(StringEnumConverter))]
        public virtual NapkinIDESetupStepTypes SetupStep { get; set; }

        [DataMember]
        public virtual Status Status { get; set; }

        [DataMember]
        public virtual List<LicenseAccessToken> Subscribers { get; set; }

        [DataMember]
        public virtual string Template { get; set; }

        [DataMember]
        public virtual string Terms { get; set; }

        [DataMember]
        public virtual bool TermsAccepted { get; set; }

        [DataMember]
        [JsonConverter(typeof(StringEnumConverter))]
        public virtual UserTypes UserType { get; set; }

        public virtual string Username { get; set; }

        public List<LicenseAccessToken> UserLicenses { get; set; }
    }

    [DataContract]
    public class JourneyPersona
    {
        [DataMember]
        public virtual List<string> Descriptions { get; set; }

        [DataMember]
        public virtual IDictionary<string, List<string>> DetailLookupCategories { get; set; }

        [DataMember]
        public virtual string Lookup { get; set; }

        [DataMember]
        public virtual string Name { get; set; }
    }

    [DataContract]
    public class JourneyDetail
    {
        [DataMember]
        public virtual string Description { get; set; }

        [DataMember]
        public virtual string Lookup { get; set; }

        [DataMember]
        public virtual string Name { get; set; }
    }

    [DataContract]
    public class BootOption
    {
        [DataMember]
        public virtual string Description { get; set; }

        [DataMember]
        public virtual bool Loading { get; set; }

        [DataMember]
        public virtual string Lookup { get; set; }

        [DataMember]
        public virtual string Name { get; set; }

        [DataMember]
        [JsonConverter(typeof(StringEnumConverter))]
        public virtual NapkinIDESetupStepTypes? SetupStep { get; set; }

        [DataMember]
        public virtual Status Status { get; set; }
    }

    [DataContract]
    public enum NapkinIDESetupStepTypes
    {
        [EnumMember]
        OrgDetails,

        [EnumMember]
        AzureSetup,

        [EnumMember]
        Review,

        [EnumMember]
        Complete
    }

    [DataContract]
    public class AzureInfaSettings
    {
        [DataMember]
        public virtual string AzureTenantID { get; set; }

        [DataMember]
        public virtual string AzureSubID { get; set; }

        [DataMember]
        public virtual string AzureAppID { get; set; }

        [DataMember]
        public virtual string AzureAppAuthKey { get; set; }
    }

    [DataContract]
    public class AccessRequest
    {

        [DataMember]
        public virtual string User { get; set; }


        [DataMember]
        public virtual string EnterpriseID { get; set; }


    }

    [DataContract]
    public class AccessRequestEmail
    {

        [DataMember]
        public virtual string User { get; set; }

        [DataMember]
        public virtual string EnterpriseID { get; set; }

        [DataMember]
        public virtual string Subject { get; set; }

        [DataMember]
        public virtual string Content { get; set; }

        [DataMember]
        public virtual string EmailTo { get; set; }

        [DataMember]
        public virtual string EmailFrom { get; set; }


    }

    [DataContract]
    public enum UserTypes
    {
        [EnumMember]
        Develop,

        [EnumMember]
        Design,

        [EnumMember]
        Manage
    }

    // public static class UserManagementStateEntity
    // {
    //     [FunctionName("UserManagementState")]
    //     public static async Task Run([EntityTrigger] IDurableEntityContext ctx, ILogger log,
    //         [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages)
    //     {
    //         log.LogInformation($"Executing UserManagementState entity.");

    //         await ctx.WithEntityState<UserManagementState>(signalRMessages, log, async (state, actionReq) =>
    //         {
    //             switch (actionReq.Type)
    //             {
    //                 case "SetUserDetails":
    //                     state.SetUserDetails(actionReq.Arguments.Metadata["FullName"].ToString(),
    //                         actionReq.Arguments.Metadata["Country"].ToString(), actionReq.Arguments.Metadata["Handle"].ToString());
    //                     break;
    //             }
    //         });

    //         // var action = ctx.OperationName.ToLowerInvariant();

    //         // var state = ctx.GetState<UserManagementState>();

    //         // if (action == "$init" && state == null)
    //         //     state = new UserManagementState(ctx.GetInput<StateDetails>());

    //         // log.LogInformation($"UserManagementState state loaded.");

    //         // switch (action)
    //         // {
    //         // }

    //         // ctx.SetState(state);

    //         // log.LogInformation($"UserManagementState state set.");

    //         // ctx.StartNewOrchestration("SendState", state);

    //         // log.LogInformation($"UserManagementState state sent.");

    //         // ctx.Return(state);
    //     }
    // }
}
