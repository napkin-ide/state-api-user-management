using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LCU.Personas.Client.Applications;
using LCU.Personas.Client.Enterprises;
using LCU.Personas.Client.Identity;
using LCU.Personas.Client.Security;
using System.Linq;
using System;
using LCU.StateAPI;
using LCU.StateAPI.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using System.Threading.Tasks;
using LCU.State.API.UserManagement.Host.TempRefit;

[assembly: FunctionsStartup(typeof(LCU.State.API.UserManagement.Host.Startup))]

namespace LCU.State.API.UserManagement.Host
{
    public class Startup : StateAPIStartup
    {
        #region Fields
        #endregion

        #region Constructors
        public Startup() : base()
        { }
        #endregion

        #region API Methods
        public override void Configure(IFunctionsHostBuilder builder)
        {
            base.Configure(builder);

            //  TODO: Refit client registration
            // builder.Services.AddLCUPersonas(null, null, null);
            var httpOpts = new LCUStartupHTTPClientOptions()
            {
                CircuitBreakDurationSeconds = 5,
                CircuitFailuresAllowed = 5,
                LongTimeoutSeconds = 60,
                RetryCycles = 3,
                RetrySleepDurationMilliseconds = 500,
                TimeoutSeconds = 30,
                Options = new System.Collections.Generic.Dictionary<string, LCUClientOptions>()
                {
                    {
                        nameof(IApplicationsIoTService),
                        new LCUClientOptions()
                        {
                            BaseAddress = Environment.GetEnvironmentVariable($"{typeof(IApplicationsIoTService).FullName}.BaseAddress")
                        }
                    },

                    {
                        nameof(IEnterprisesAPIManagementService),
                        new LCUClientOptions()
                        {
                            BaseAddress = Environment.GetEnvironmentVariable($"{typeof(IEnterprisesAPIManagementService).FullName}.BaseAddress")
                        }
                    },

                    {
                        nameof(IEnterprisesHostingManagerService),
                        new LCUClientOptions()
                        {
                            BaseAddress = Environment.GetEnvironmentVariable($"{typeof(IEnterprisesHostingManagerService).FullName}.BaseAddress")
                        }
                    },

                    {
                        nameof(IEnterprisesManagementService),
                        new LCUClientOptions()
                        {
                            BaseAddress = Environment.GetEnvironmentVariable($"{typeof(IEnterprisesManagementService).FullName}.BaseAddress")
                        }                 
                    },

                    {
                        nameof(IIdentityAccessService),
                        new LCUClientOptions()
                        {
                            BaseAddress = Environment.GetEnvironmentVariable($"{typeof(IIdentityAccessService).FullName}.BaseAddress")
                        }
                    },

                    {
                        nameof(ISecurityDataTokenService),
                        new LCUClientOptions()
                        {
                            BaseAddress = Environment.GetEnvironmentVariable($"{typeof(ISecurityDataTokenService).FullName}.BaseAddress")
                        }
                    }
                }
            };

            var registry = builder.Services.AddLCUPollyRegistry(httpOpts);

            builder.Services.AddLCUHTTPClient<IApplicationsIoTService>(registry, httpOpts);

            builder.Services.AddLCUHTTPClient<IEnterprisesAPIManagementService>(registry, httpOpts);

            builder.Services.AddLCUHTTPClient<IEnterprisesHostingManagerService>(registry, httpOpts);

            builder.Services.AddLCUHTTPClient<IEnterprisesManagementService>(registry, httpOpts);

            builder.Services.AddLCUHTTPClient<IIdentityAccessService>(registry, httpOpts);

            builder.Services.AddLCUHTTPClient<ISecurityDataTokenService>(registry, httpOpts);
        }
        #endregion

        #region Helpers
        // protected virtual Task authenticationFailed(AuthenticationFailedContext arg)
        // {
        //     // For debugging purposes only!
        //     var s = $"AuthenticationFailed: {arg.Exception.Message}";

        //     arg.Response.ContentLength = s.Length;

        //     arg.Response.Body.Write(Encoding.UTF8.GetBytes(s), 0, s.Length);

        //     return Task.FromResult(0);
        // }
        #endregion
    }
}