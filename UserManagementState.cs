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

namespace LCU.State.API.NapkinIDE.User.Management
{
    [Serializable]
    [DataContract]
    public class UserManagementState
    {
        #region Constants
        public const string HUB_NAME = "usermanagement";
        #endregion

        #region Properties 
        [DataMember]
        public virtual List<JourneyDetail> Details { get; set; }

        [DataMember]
        public virtual List<JourneyPersona> Personas { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [DataMember]
        public virtual UserTypes UserType { get; set; }
        #endregion

        #region Constructors
        public UserManagementState()
        { }
        #endregion

        #region API Methods
        public virtual void SetUserType(UserTypes userType)
        {
            UserType = userType;
        }
        #endregion
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
