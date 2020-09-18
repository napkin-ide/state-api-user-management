using System;
using Fathym;
using System.Runtime.Serialization;
using System.Collections.Generic;
using LCU.Personas.Enterprises;

namespace LCU.State.API.NapkinIDE.UserManagement
{
    public enum UserManagementErrorCodes : int
    {
        HostAlreadyExists = 101,
        AzureEnvSettingsInvalid = 102
    }
}
