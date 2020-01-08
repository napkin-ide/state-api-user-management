
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using LCU.Presentation.State;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;

namespace LCU.State.API.NapkinIDE.User.Management.Utils
{
    public static class StateUtils
    {
        public static string BuildGroupName(StateDetails stateDetails, LCUStateConfiguration stateCfg)
        {
            var username = stateCfg.UseUsername ? $"{stateDetails.Username}|" : null;

            return $"{stateDetails.EnterpriseAPIKey}|{stateDetails.HubName}|{username}{stateDetails.stateKey}".ToMD5Hash();
        }

        public static string LoadEntApiKey(string statePath)
        {
            var splits = statePath.Split('/');

            return splits[0];
        }

        public static string LoadEntApiKey(HttpRequest req)
        {
            var entApiKey = req.Headers["lcu-ent-api-key"];

            return entApiKey;
        }

        public static async Task<IServiceHubContext> LoadHubContext(string hubName)
        {
            var context = await StaticServiceHubContextStore.Get().GetAsync(hubName);

            return context;
        }

        public static string LoadHubName(string statePath)
        {
            var splits = statePath.Split('/');

            return splits[1];
        }

        public static string LoadHubName(HttpRequest req)
        {
            var entApiKey = req.Headers["lcu-hub-name"];

            return entApiKey;
        }

        public static StateDetails LoadStateDetails(string statePath)
        {
            var entApiKey = StateUtils.LoadEntApiKey(statePath);

            var hubName = StateUtils.LoadHubName(statePath);

            var stateKey = StateUtils.LoadStateKey(statePath);

            var username = StateUtils.LoadUsername(statePath);

            return new StateDetails()
            {
                EnterpriseAPIKey = entApiKey,
                HubName = hubName,
                stateKey = stateKey,
                Username = username
            };
        }

        public static StateDetails LoadStateDetails(HttpRequest req, ClaimsPrincipal user)
        {
            var entApiKey = StateUtils.LoadEntApiKey(req);

            var hubName = StateUtils.LoadHubName(req);

            var stateKey = StateUtils.LoadStateKey(req);

            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);

            return new StateDetails()
            {
                EnterpriseAPIKey = entApiKey,
                HubName = hubName,
                stateKey = stateKey,
                Username = userIdClaim.Value
            };
        }

        public static string LoadStateKey(string statePath)
        {
            var splits = statePath.Split('/');

            return splits.Length == 3 ? splits[2] : splits[4];
        }

        public static string LoadStateKey(HttpRequest req)
        {
            var entApiKey = req.Headers["lcu-state-key"];

            return entApiKey;
        }

        public static string LoadUsername(string statePath)
        {
            var splits = statePath.Split('/');

            return splits.Length > 3 ? splits[2] : null;
        }

        public static LCUStateConfiguration ParseStateConfig(string stateCfgStr)
        {
            return stateCfgStr?.FromJSON<LCUStateConfiguration>();
        }
    }
}