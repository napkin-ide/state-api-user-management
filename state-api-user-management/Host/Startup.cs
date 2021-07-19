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
        // public override void Configure(IFunctionsHostBuilder builder)
        // {
        //     base.Configure(builder);

        //     var adB2cTenant = Environment.GetEnvironmentVariable("LCU-AZURE-AD-B2C-TENANT");

        //     var adB2cPolicy = Environment.GetEnvironmentVariable("LCU-AZURE-AD-B2C-POLICY");

        //     var adB2cApiName = Environment.GetEnvironmentVariable("LCU-AZURE-AD-B2C-APINAME");

        //     var adB2cClientId = Environment.GetEnvironmentVariable("LCU-AZURE-AD-B2C-CLIENTID");

        //     var storageAccountConn = Environment.GetEnvironmentVariable("LCU-STORAGE-CONNECTION");

        //     // var storageAccount = CloudStorageAccount.Parse(storageAccountConn);

        //     // builder.Services.AddLCUDataProtection(storageAccount);

        //     builder.Services.AddAuthentication(options =>
        //     {
        //         options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        //     })
        //     .AddJwtBearer(jwtOptions =>
        //     {
        //         jwtOptions.Authority = $"https://{adB2cTenant}.b2clogin.com/{adB2cTenant}.onmicrosoft.com/{adB2cPolicy}/v2.0/";

        //         jwtOptions.Audience = adB2cClientId;

        //         jwtOptions.Events = new JwtBearerEvents
        //         {
        //             OnAuthenticationFailed = authenticationFailed
        //         };
        //     });

        //     // builder.Services.AddAuthorization(options =>
        //     // {
        //     //     options.AddPolicy("OnlyAdmins", policyBuilder =>
        //     //     {
        //     //         // configure my policy requirements
        //     //     });
        //     // });
        // }
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