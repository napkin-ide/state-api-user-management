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

namespace LCU.State.API.NapkinIDE.User.Management
{
    [Serializable]
    public class UserManagementState
    {
        #region Properties        
        public virtual string FirstName { get; set; }

        public virtual string LastName { get; set; }

        public virtual string Username { get; protected set; }
        #endregion

        #region Constructors
        public UserManagementState(string username)
        {
            this.Username = username;
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
        [FunctionName("UserManagementState")]
        public static void Run([EntityTrigger] IDurableEntityContext ctx, ILogger log)
        {
            var action = ctx.OperationName.ToLowerInvariant();

            var state = action == "$Init" ? new UserManagementState(ctx.GetInput<string>()) : ctx.GetState<UserManagementState>();

            switch (action)
            {
                case "SetUserDetails":
                    (string FirstName, string LastName) dets = ctx.GetInput<(string, string)>();

                    state.SetUserDetails(dets.FirstName, dets.LastName);
                    break;
            }

            ctx.SetState(state);

            // ctx.Return(state);
        }
    }
}
