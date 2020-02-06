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

namespace LCU.State.API.NapkinIDE.User.Management
{
    [Serializable]
    public class UserManagementState
    {
        
        #region Properties        
        public virtual string FirstName { get; protected set; }

        public virtual string LastName { get; protected set; }

        public virtual StateDetails StateDetails { get; protected set; }
        #endregion

        #region Constructors
        public UserManagementState(StateDetails stateDetails)
        {
            this.StateDetails = stateDetails;
        }
        #endregion

        #region API Methods
        public virtual void SetUserDetails(string firstName, string lastName)
        {
            FirstName = firstName;
            
            LastName = lastName;
        }
        #endregion
    }

    public static class UserManagementStateEntity
    {
        [FunctionName("UserManagementStateEntity")]
        public static void Run([EntityTrigger] IDurableEntityContext ctx, ILogger log)
        {
            var action = ctx.OperationName.ToLowerInvariant();

            var state = action == "$init" ? new UserManagementState(ctx.GetInput<StateDetails>()) : ctx.GetState<UserManagementState>();

            switch (action)
            {
                case "setuserdetails":
                    (string FirstName, string LastName) dets = ctx.GetInput<(string, string)>();

                    state.SetUserDetails(dets.FirstName, dets.LastName);
                    break;
            }

            ctx.SetState(state);

            ctx.StartNewOrchestration("SendState", state);

            // ctx.Return(state);
        }
    }
}
