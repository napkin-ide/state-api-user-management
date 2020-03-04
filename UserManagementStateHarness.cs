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
    public class UserManagementStateHarness
    {
        #region Properties 
        public virtual UserManagementState State { get; protected set; }
        #endregion

        #region Constructors
        public UserManagementStateHarness(UserManagementState state)
        { 
            State = state ?? new UserManagementState();
        }
        #endregion

        #region API Methods
        public virtual void SetUserType(UserTypes userType)
        {
            State.UserType = userType;
        }
        #endregion
    }
}
