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
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using LCU.State.API.NapkinIDE.User.Management.Utils;
using Fathym;
using LCU.Presentation.State.ReqRes;

namespace LCU.State.API.NapkinIDE.User.Management
{
    [Serializable]
    public class UserManagementState
    {
        #region Constants
        public const string HUB_NAME = "usermanagement";
        #endregion

        #region Properties 
        public virtual string Country { get; set; }

        public virtual string FullName { get; set; }

        public virtual string Handle { get; set; }

        public virtual StateDetails StateDetails { get; set; }
        #endregion

        #region Constructors
        public UserManagementState(StateDetails stateDetails)
        {
            this.StateDetails = stateDetails;
        }
        #endregion

        #region API Methods
        public virtual void SetUserDetails(string fullName, string country, string handle)
        {
            Country = country;

            FullName = fullName;

            Handle = handle;
        }
        #endregion
    }

    public static class UserManagementStateEntity
    {
        [FunctionName("UserManagementStateEntity")]
        public static void Run([EntityTrigger] IDurableEntityContext ctx, ILogger log)
        {
            var action = ctx.OperationName.ToLowerInvariant();

            var state = ctx.GetState<UserManagementState>();

            if (action == "$init" && state == null)
                state = new UserManagementState(ctx.GetInput<StateDetails>());

            switch (action)
            {
                case "setuserdetails":
                    var actionReq = ctx.GetInput<ExecuteActionRequest>();

                    state.SetUserDetails(actionReq.Arguments.Metadata["FullName"].ToString(),
                        actionReq.Arguments.Metadata["Country"].ToString(), actionReq.Arguments.Metadata["Handle"].ToString());
                    break;
            }

            ctx.SetState(state);

            ctx.StartNewOrchestration("SendState", state);

            // ctx.Return(state);
        }
    }
}
