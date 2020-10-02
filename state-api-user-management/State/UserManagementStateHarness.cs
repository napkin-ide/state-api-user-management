using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
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
using LCU.Personas.Client.Enterprises;
using LCU.Personas.Client.DevOps;
using LCU.Personas.Enterprises;
using LCU.Personas.Identity;
using LCU.Personas.Client.Applications;
using LCU.Personas.Client.Identity;
using Fathym.API;
using LCU.Personas.DevOps;
using LCU.Graphs.Registry.Enterprises.Identity;
using Newtonsoft.Json.Linq;
using LCU.Personas.Client.Security;
using LCU.Personas.Security;
using LCU.Personas;
using LCU.State.API.NapkinIDE.UserManagement.Management;

namespace LCU.State.API.NapkinIDE.UserManagement.State
{
    public class UserManagementStateHarness : LCUStateHarness<UserManagementState>
    {
        #region Fields 
        #endregion

        #region Properties 
        #endregion

        #region Constructors
        public UserManagementStateHarness(UserManagementState state)
            : base(state ?? new UserManagementState())
        { }
        #endregion

        #region API Methods

        public virtual async Task AreAzureEnvSettingsValid(EnterpriseManagerClient entMgr)
        {
            State.AzureInfrastructureInvalidComponent = String.Empty;
            State.AzureInfrastructureInvalidComponentError = String.Empty;

            var config = State.EnvSettings.JSONConvert<AzureInfrastructureConfig>();

            var valid = await entMgr.AreEnvironmentSettingsValid(config, "check-app");

            State.AzureInfrastructureValid = valid.Status;

            if (!State.AzureInfrastructureValid)
            {
                State.AzureInfrastructureInvalidComponent = valid.Status.Metadata["ErrorFrom"].ToString();
                State.AzureInfrastructureInvalidComponentError = valid.Status.Message;
            }
        }

        public virtual async Task<Status> BootOrganizationEnterprise(EnterpriseArchitectClient entArch, string parentEntLookup, string username)
        {
            var status = Status.Success;

            if (State.NewEnterpriseLookup.IsNullOrEmpty())
            {
                var entRes = await entArch.CreateEnterprise(new CreateEnterpriseRequest()
                {
                    Description = State.OrganizationDescription ?? State.OrganizationName,
                    Host = State.Host,
                    Name = State.OrganizationName
                }, parentEntLookup, username);

                State.NewEnterpriseLookup = entRes.Model?.EnterpriseLookup;

                status = entRes.Status;
            }

            UpdateStatus(status);

            return status;
        }

        public virtual async Task<Status> BootOrganizationEnvironment(EnterpriseManagerClient entMgr, DevOpsArchitectClient devOpsArch)
        {
            var status = Status.Success;

            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && State.EnvironmentLookup.IsNullOrEmpty())
            {
                var envResp = await devOpsArch.EnsureEnvironment(new Personas.DevOps.EnsureEnvironmentRequest()
                {
                    EnvSettings = State.EnvSettings,
                    OrganizationLookup = State.OrganizationLookup,
                }, State.NewEnterpriseLookup);

                // var envResp = await devOpsArch.With(async client => {
                //     var res = await client.PostAsJsonAsync($"infrastructure/{State.NewEnterpriseLookup}/ensure/env", new Personas.DevOps.EnsureEnvironmentRequest()
                //     {
                //         EnvSettings = State.EnvSettings,
                //         OrganizationLookup = State.OrganizationLookup,
                //     });

                //     return res;
                // });


                // var resp = await envResp.Content.ReadAsStringAsync();

                State.EnvironmentLookup = envResp.Model?.Lookup;

                status = envResp.Status;
            }
            else if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
                await entMgr.SaveEnvironmentSettings(State.EnvSettings, State.NewEnterpriseLookup, State.EnvironmentLookup);

            UpdateStatus(status);

            return status;
        }

        public virtual async Task<Status> BootDAFInfrastructure(DevOpsArchitectClient devOpsArch, string username)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty() && !State.ProjectID.IsNullOrEmpty())
            {
                var resp = await devOpsArch.SetEnvironmentInfrastructure(new Personas.DevOps.SetEnvironmentInfrastructureRequest()
                {
                    EnvironmentLookup = State.EnvironmentLookup,
                    ProjectID = State.ProjectID,
                    Template = State.Template,
                    Username = username
                }, State.NewEnterpriseLookup);

                return resp.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> BootHost(EnterpriseArchitectClient entArch, string parentEntLookup)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var response = await entArch.EnsureHost(new EnsureHostRequest()
                {
                    EnviromentLookup = State.EnvironmentLookup
                }, State.NewEnterpriseLookup, State.Host, State.EnvironmentLookup, parentEntLookup);

                return response.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> BootHostAuthApp(EnterpriseArchitectClient entArch)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await entArch.EnsureHostAuthApp(State.NewEnterpriseLookup, State.Host, State.EnvironmentLookup);

                return resp.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> BootHostSSL(EnterpriseArchitectClient entArch, string parentEntLookup)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await entArch.EnsureHostsSSL(new EnsureHostsSSLRequest()
                {
                    Hosts = new List<string>() { State.Host }
                }, State.NewEnterpriseLookup, State.EnvironmentLookup, parentEntLookup);

                return resp.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> BootMicroAppsRuntime(EnterpriseArchitectClient entArch)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await entArch.EnsureLCURuntime(State.NewEnterpriseLookup, State.EnvironmentLookup);

                return resp.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> BootBuildDefinitions(DevOpsArchitectClient devOpsArch, string username)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty() && !State.ProjectID.IsNullOrEmpty())
            {
                var resp = await devOpsArch.EnsureBuilds(new Personas.DevOps.EnsureBuildsRequest()
                {
                    ProjectID = State.ProjectID,
                    Username = username
                }, State.NewEnterpriseLookup);

                return resp.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> BootDataApps(ApplicationDeveloperClient appDev)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await appDev.ConfigureNapkinIDEForDataApps(State.NewEnterpriseLookup);

                return resp.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> BootDataFlow(ApplicationDeveloperClient appDev)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await appDev.ConfigureNapkinIDEForDataFlows(State.NewEnterpriseLookup);

                return resp.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> BootFeeds(DevOpsArchitectClient devOpsArch, string username)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await devOpsArch.EnsureFeed(new Personas.DevOps.EnsureFeedRequest()
                {
                    EnvironmentLookup = State.EnvironmentLookup,
                    Username = username
                }, State.NewEnterpriseLookup);

                return resp.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> BootIoTWelcome(ApplicationDeveloperClient appDev)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await appDev.ConfigureNapkinIDEForIoTWelcome(State.NewEnterpriseLookup, State.EnvironmentLookup, State.Host);

                return resp.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> DeployIoTDataFlow(ApplicationDeveloperClient appDev, EnterpriseManagerClient entMgr)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var dfLookup = "iot"; //  Will need to be handled differently if default ever changes in ConfigureNapkinIDEForIoTWelcome

                //  Initializing warm-storage call
                var infraDetsResp = await entMgr.LoadInfrastructureDetails(State.NewEnterpriseLookup, State.EnvironmentLookup,
                    "warm-storage");

                var status = infraDetsResp.Status;

                if (status)
                {
                    var dfResp = await appDev.DeployDataFlow(new Personas.Applications.DeployDataFlowRequest()
                    {
                        DataFlowLookup = dfLookup
                    }, State.NewEnterpriseLookup, State.EnvironmentLookup);

                    status = dfResp.Status;
                }
                else
                    status = Status.GeneralError.Clone("Unable to save the data flow");

                return status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> SetupIoTWelcome(ApplicationDeveloperClient appDev, ApplicationManagerClient appMgr,
            EnterpriseManagerClient entMgr)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var dfLookup = "iot"; //  Will need to be handled differently if default ever changes in ConfigureNapkinIDEForIoTWelcome

                var apps = await appMgr.ListApplications(State.NewEnterpriseLookup);

                var status = apps.Status;

                if (!apps.Model.Any(app => app.PathRegex == "/freeboard*"))
                {
                    var saveResp = await appDev.SaveAppAndDAFApps(new Personas.Applications.SaveAppAndDAFAppsRequest()
                    {
                        Application = new Graphs.Registry.Enterprises.Apps.Application()
                        {
                            Name = "Freeboard",
                            Description = "Freeboard is an open source tool for visualizing data.",
                            PathRegex = "/freeboard*"
                        },
                        DAFApps = new List<Graphs.Registry.Enterprises.Apps.DAFApplication>()
                    {
                        new Graphs.Registry.Enterprises.Apps.DAFApplication()
                        {
                            Priority = 500,
                            Details = new Graphs.Registry.Enterprises.Apps.DAFViewApplicationDetails()
                            {
                                BaseHref = "/freeboard/",
                                NPMPackage = "@semanticjs/freeboard",
                                PackageVersion = "latest",
                                StateConfig = new
                                {
                                    ActionRoot = "/api/state",
                                    Root = "/api/state",
                                    FreeboardConfigURL = "templates/freeboard/DeviceDemoDashboard.json"
                                }.JSONConvert<MetadataModel>()
                            }
                        }
                    }
                    }, State.NewEnterpriseLookup, State.Host);

                    status = saveResp.Status;
                }

                if (status && !apps.Model.Any(app => app.PathRegex == "/lcu-charts*"))
                {
                    var saveResp = await appDev.SaveAppAndDAFApps(new Personas.Applications.SaveAppAndDAFAppsRequest()
                    {
                        Application = new Graphs.Registry.Enterprises.Apps.Application()
                        {
                            Name = "LCU Charts",
                            Description = "LCU Charts is an application based on Fathym's open source charting library that provides a great starting point for creating customized visualizations.",
                            PathRegex = "/lcu-charts*"
                        },
                        DAFApps = new List<Graphs.Registry.Enterprises.Apps.DAFApplication>()
                        {
                            new Graphs.Registry.Enterprises.Apps.DAFApplication()
                            {
                                Priority = 500,
                                Details = new Graphs.Registry.Enterprises.Apps.DAFViewApplicationDetails()
                                {
                                    BaseHref = "/lcu-charts/",
                                    NPMPackage = "@lowcodeunit/lcu-charts-demo",
                                    PackageVersion = "latest",
                                    StateConfig = new
                                    {
                                        ActionRoot = "/api/state",
                                        Root = "/api/state"
                                    }.JSONConvert<MetadataModel>()
                                }
                            }
                        }
                    }, State.NewEnterpriseLookup, State.Host);

                    status = saveResp.Status;
                }

                if (status && !apps.Model.Any(app => app.PathRegex == $"/api/data-flow/{dfLookup}/warm-query*"))
                {
                    var infraDets = await entMgr.LoadInfrastructureDetails(State.NewEnterpriseLookup, State.EnvironmentLookup,
                        "warm-query");

                    //  TODO:  Support multiple
                    await infraDets.Model.Take(1).Each(async infraDet =>
                    {
                        var saveResp = await appDev.SaveAppAndDAFApps(new Personas.Applications.SaveAppAndDAFAppsRequest()
                        {
                            Application = new Graphs.Registry.Enterprises.Apps.Application()
                            {
                                Name = $"Warm Query APIs - {dfLookup} - {infraDet.DisplayName}",
                                Description = "These API proxies make it easy to connect and work with your observational data.",
                                PathRegex = $"/api/data-flow/{dfLookup}/warm-query*"
                            },
                            DAFApps = new List<Graphs.Registry.Enterprises.Apps.DAFApplication>()
                            {
                                new Graphs.Registry.Enterprises.Apps.DAFApplication()
                                {
                                    Priority = 500,
                                    Details = new Graphs.Registry.Enterprises.Apps.DAFAPIApplicationDetails()
                                    {
                                        APIRoot = infraDet.Connections["$api"],
                                        InboundPath = $"data-flow/{dfLookup}/warm-query",
                                        Methods = "GET",
                                        Security = $"x-functions-key~{infraDet.Connections["default"]}"
                                    }
                                }
                            }
                        }, State.NewEnterpriseLookup, State.Host);

                        status = saveResp.Status;
                    });
                }

                if (status && !apps.Model.Any(app => app.PathRegex == $"/api/data-flow/{dfLookup}/data-stream*"))
                {
                    var infraDets = await entMgr.LoadInfrastructureDetails(State.NewEnterpriseLookup, State.EnvironmentLookup,
                        "data-stream");

                    //  TODO:  Support multiple
                    await infraDets.Model.Take(1).Each(async infraDet =>
                    {
                        var saveResp = await appDev.SaveAppAndDAFApps(new Personas.Applications.SaveAppAndDAFAppsRequest()
                        {
                            Application = new Graphs.Registry.Enterprises.Apps.Application()
                            {
                                Name = $"Data Stream APIs - {dfLookup} - {infraDet.DisplayName}",
                                Description = "This API proxies make it easy to connect and send your own device data.",
                                PathRegex = $"/api/data-flow/{dfLookup}/data-stream*"
                            },
                            DAFApps = new List<Graphs.Registry.Enterprises.Apps.DAFApplication>()
                            {
                                new Graphs.Registry.Enterprises.Apps.DAFApplication()
                                {
                                    Priority = 500,
                                    Details = new Graphs.Registry.Enterprises.Apps.DAFAPIApplicationDetails()
                                    {
                                        APIRoot = infraDet.DisplayName,
                                        InboundPath = $"data-flow/{dfLookup}/data-stream",
                                        Methods = "POST PUT",
                                        Security = $"Microsoft.Azure.EventHubs~{infraDet.Connections.First().Value}"
                                    }
                                }
                            }
                        }, State.NewEnterpriseLookup, State.Host);

                        status = saveResp.Status;
                    });
                }

                return status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> BootReleaseDefinitions(DevOpsArchitectClient devOpsArch, string username)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty() && !State.ProjectID.IsNullOrEmpty())
            {
                var resp = await devOpsArch.EnsureReleases(new Personas.DevOps.EnsureReleasesRequest()
                {
                    EnvironmentLookup = State.EnvironmentLookup,
                    ProjectID = State.ProjectID,
                    Username = username
                }, State.NewEnterpriseLookup);

                return resp.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> BootRepositories(DevOpsArchitectClient devOpsArch, string username)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty() && !State.ProjectID.IsNullOrEmpty())
            {
                var resp = await devOpsArch.EnsureRepositories(new Personas.DevOps.EnsureRepositoriesRequest()
                {
                    EnvironmentLookup = State.EnvironmentLookup,
                    ProjectID = State.ProjectID,
                    Username = username
                }, State.NewEnterpriseLookup);

                return resp.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> BootServiceEndpoints(DevOpsArchitectClient devOpsArch, string username)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty() && !State.ProjectID.IsNullOrEmpty())
            {
                var resp = await devOpsArch.EnsureServiceEndpoints(new Personas.DevOps.EnsureServiceEndpointsRequest()
                {
                    EnvironmentLookup = State.EnvironmentLookup,
                    ProjectID = State.ProjectID,
                    Username = username
                }, State.NewEnterpriseLookup);

                await Task.Delay(2500);

                return resp.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> BootTaskLibrary(DevOpsArchitectClient devOpsArch, string username)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty() && !State.ProjectID.IsNullOrEmpty())
            {
                var resp = await devOpsArch.EnsureTaskLibrary(new EnsureTaskLibraryRequest()
                {
                    ProjectID = State.ProjectID,
                    Username = username
                }, State.NewEnterpriseLookup);

                return resp.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> CancelSubscription(EnterpriseManagerClient entMgr, IdentityManagerClient idMgr, SecurityManagerClient secMgr, string entLookup, string username, string reason)
        {
            string mrktEmail = "marketing@fathym.com";

            // get subscription token by user name
            var subIdToken = await secMgr.RetrieveIdentityThirdPartyData(entLookup, username, "LCU-STRIPE-SUBSCRIPTION-ID");

            string subId = subIdToken.Model["LCU-STRIPE-SUBSCRIPTION-ID"].ToString();

            if (String.IsNullOrEmpty(subId)) return Status.GeneralError.Clone($"No subscripton ID was found for user {username}");

            // Issue cancellation
            var response = await entMgr.CancelSubscription(subId, entLookup);

            // If subscription is successfully cancelled
            if (response.Status)
            {

                // Get the user's LATs from the graph db
                var licenseAccess = await idMgr.ListLicenseAccessTokens(entLookup, username, new List<string>() { "LCU" });

                // Expire the LAT
                foreach (LicenseAccessToken token in licenseAccess.Model)
                {
                    token.IsLocked = true;
                    token.ExpirationDate = System.DateTime.Now;
                    await idMgr.IssueLicenseAccess(token, entLookup);
                }

                var cancelNotice = new SendNotificationRequest()
                {
                    EmailFrom = mrktEmail,
                    EmailTo = username,
                    Subject = "Subscription Cancelled",
                    Content = @"Hi there\n\nThanks for trying out Fathym! We are constantly upgrading and improving our framework and hope see you again someday soon. \n\n
                                We are always listening to our users. If you have any feedback or suggestions for features we should add, please feel free to reply to this email and let us know. \n\n
                                Thanks again,\n
                                Team Fathym",
                    ReplyTo = ""
                };

                // Send email to let  the cancellation took place 
                await SendFeedback(entMgr, entLookup, mrktEmail, reason);

                // Send email to user to let them know cancellation has taken place
                await SendNotification(entMgr, entLookup, username, cancelNotice);

                return Status.Success;
            }

            return response.Status;
        }

        public virtual async Task<Status> CanFinalize(DevOpsArchitectClient devOpsArch, string username)
        {
            var status = Status.GeneralError;

            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty() && !State.ProjectID.IsNullOrEmpty())
            {
                var bldsResp = await devOpsArch.AreBuildsComplete(new AreBuildsCompleteRequest()
                {
                    ProjectID = State.ProjectID,
                    Username = username
                }, State.NewEnterpriseLookup);

                var rlsResp = await devOpsArch.AreReleaseDeploysComplete(new AreReleaseDeploysCompleteRequest()
                {
                    EnvironmentLookup = State.EnvironmentLookup,
                    ProjectID = State.ProjectID,
                    Username = username
                }, State.NewEnterpriseLookup);

                status = bldsResp.Status && rlsResp.Status;
            }

            return status;
        }

        public virtual void CompleteBoot()
        {
            State.Booted = true;
        }

        public virtual async Task ConfigureInfrastructure(EnterpriseArchitectClient entArch, EnterpriseManagerClient entMgr, string infraType, bool useDefaultSettings,
            MetadataModel settings, string template, bool shouldStep, string entLookup, string username)
        {
            var envLookup = $"{State.OrganizationLookup}-prd";

            State.Booted = false;

            State.EnvSettings = settings;

            State.Template = template;

            await AreAzureEnvSettingsValid(entMgr);

            if (State.AzureInfrastructureValid)
            {
                if (shouldStep)
                {
                    // await HasAzureOAuth(entMgr, entLookup, username);

                    // if (State.HasAzureOAuth)
                    SetNapkinIDESetupStep(NapkinIDESetupStepTypes.Review);
                }
                else
                    await ConfigureAzureLocationOptions(entArch);

                State.Status = null;
            }
            else
                State.Status = new Status()
                {
                    Code = (int)UserManagementErrorCodes.AzureEnvSettingsInvalid,
                    Message = State.AzureInfrastructureValid.Message,
                    Metadata = State.AzureInfrastructureValid.Metadata
                };
        }

        public virtual void ConfigureBootOptions()
        {
            State.BootOptions = new List<BootOption>();

            State.BootOptions.Add(new BootOption()
            {
                Name = "Configure Workspace Details",
                Lookup = "Project",
                Description = "Data Configuration, Workspace Set Up, Default secure-hosting",
                SetupStep = NapkinIDESetupStepTypes.OrgDetails,
                TotalSteps = 2
            });

            State.BootOptions.Add(new BootOption()
            {
                Name = "Connect to DevOps",
                Lookup = "DevOps",
                Description = "Source Control, Builds, Deployment",
                TotalSteps = 8
            });

            State.BootOptions.Add(new BootOption()
            {
                Name = "Connect Infrastructure",
                Lookup = "Infrastructure",
                Description = "Scalable, Cost Effective Infrastructure Configuration",
                SetupStep = NapkinIDESetupStepTypes.AzureSetup,
                TotalSteps = 2
            });

            State.BootOptions.Add(new BootOption()
            {
                Name = "Configure Domain",
                Lookup = "Domain",
                Description = "User Security, Host Setup, Free Open Source SSL",
                TotalSteps = 3
            });

            var currentInfraOpt = State.InfrastructureOptions[State.Template];

            State.BootOptions.Add(new BootOption()
            {
                Name = "Orchestrate Micro-Application",
                Lookup = "MicroApps",
                Description = $"{currentInfraOpt}, Data Flow LCU, Application Flow LCU",
                TotalSteps = 6
            });
        }

        public virtual void ConfigureInfrastructureOptions()
        {
            State.InfrastructureOptions = new Dictionary<string, string>();

            State.InfrastructureOptions["fathym\\daf-state-setup"] = "Low-Code Unit™ Runtime";

            State.InfrastructureOptions["fathym\\daf-iot-starter"] = "Low-Code Unit™ Runtime w/ IoT";
        }

        public virtual async Task ConfigureAzureLocationOptions(EnterpriseArchitectClient entArch)
        {
            var azureRegions = await entArch.ListAzureRegions(State.EnvSettings.JSONConvert<AzureInfrastructureConfig>(), new List<string>() { "Microsoft.SignalRService/SignalR" });

            State.AzureLocationOptions = azureRegions.Model;
        }

        public virtual void ConfigurePersonas()
        {
            // if (state.Personas.IsNullOrEmpty())
            State.Personas = new List<JourneyPersona>()
            {
                new JourneyPersona()
                {
                    Name = "Developer Journeys",
                    Lookup = "Develop",
                    Descriptions = new List<string>() {
                        "Start from a number of developer journeys that will get you up and running in minutes."
                    },
                    DetailLookupCategories = new Dictionary<string, List<string>>()
                    {
                        {
                            "Featured", new List<string>()
                            {
                                "AngularSPA",
                                "LCUBlade",
                                "EdgeToApp",
                                "PowerBIDataApps"
                            }
                        }
                    }
                },
                // new JourneyPersona()
                // {
                //     Name = "Designer Journeys",
                //     Lookup = "Design",
                //     Descriptions = new List<string>() {
                //         "Start from a number of designer journeys that will get you up and running in minutes."
                //     }
                // },
                new JourneyPersona()
                {
                    Name = "Admin Journeys",
                    Lookup = "Manage",
                    Descriptions = new List<string>() {
                        "Start from a number of admin journeys that will get you up and running in minutes."
                    },
                    DetailLookupCategories = new Dictionary<string, List<string>>()
                    {
                        {
                            "Featured", new List<string>()
                            {
                                "UserSetup",
                                "PowerBIDataApps",
                                "ContainerDeployment"
                            }
                        }
                    }
                }
            };
        }

        public virtual void ConfigureJourneys()
        {
            State.Details = new List<JourneyDetail>()
            {
                new JourneyDetail()
                {
                    Name = "SPAs with Angular",
                    Lookup = "AngularSPA",
                    Description = "Create and host your next Angular application with Fathym's Low-Code Unit™."
                },
                new JourneyDetail()
                {
                    Name = "Low-Code Unit™ Blade",
                    Lookup = "LCUBlade",
                    Description = "Create a new Low-Code Unit™ Blade for your Enterprise IDE."
                },
                new JourneyDetail()
                {
                    Name = "Edge to App",
                    Lookup = "EdgeToApp",
                    Description = "Leverage a number of edge devices to explore the workflow for delivering edge data to customer applications."
                },
                new JourneyDetail()
                {
                    Name = "Power BI Data Applications",
                    Lookup = "PowerBIDataApps",
                    Description = "Securely host and deliver your PowerBI reports internally and with customers."
                },
                new JourneyDetail()
                {
                    Name = "Build a Dashboard",
                    Lookup = "DashboardBasic",
                    Description = "Build a dashboard rapidly."
                },
                new JourneyDetail()
                {
                    Name = "Deploy Freeboard",
                    Lookup = "DashboardFreeboard",
                    Description = "Build a freeobard deployment rapidly."
                },
                new JourneyDetail()
                {
                    Name = "User Setup",
                    Lookup = "UserSetup",
                    Description = "Complete your user profile."
                },
                new JourneyDetail()
                {
                    Name = "Container Deployment Strategy",
                    Lookup = "ContainerDeployment",
                    Description = "Setup and configure your enterprise container deployment strategy."
                },
                new JourneyDetail()
                {
                    Name = "Splunk for Enterprise",
                    Lookup = "SplunkEnterprise",
                    Description = "Splunk enterprise setup in a snap."
                },
                new JourneyDetail()
                {
                    Name = "Open Source your Legacy",
                    Lookup = "OpenSourceLegacy",
                    Description = "A pathway to moving your enterprise legacy applications to the open source."
                },
                new JourneyDetail()
                {
                    Name = "Onboard ABB Flow Device",
                    Lookup = "ABB G5 Flow Device",
                    Description = "A pathway to moving your enterprise legacy applications to the open source."
                },
                new JourneyDetail()
                {
                    Name = "Fathym Classic for Enterprise",
                    Lookup = "FathymClassicEnterprise",
                    Description = "Fathym Classic enterprise setup in a snap."
                }
            };
        }

        public virtual async Task<Status> DenyAccess(ApplicationManagerClient appMgr, string entLookup, string token)
        {
            var response = await appMgr.DenyAccess(token, entLookup);

            return Status.Success;
        }

        public virtual void DetermineSetupStep()
        {
            if (State.OrganizationName.IsNullOrEmpty())
                State.SetupStep = NapkinIDESetupStepTypes.OrgDetails;
        }

        public virtual async Task<Status> EnsureDevOpsProject(DevOpsArchitectClient devOpsArch, string username)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await devOpsArch.EnsureDevOpsProject(new EnsureDevOpsProjectRequest()
                {
                    EnvironmentLookup = State.EnvironmentLookup,
                    Username = username
                }, State.NewEnterpriseLookup);

                State.ProjectID = resp.Model.HasValue ? resp.Model.Value.ToString() : null;

                return resp.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual async Task<Status> GrantAccess(ApplicationManagerClient appMgr, string entLookup, string token)
        {
            var response = await appMgr.GrantAccess(token, entLookup);

            return Status.Success;
        }

        public virtual async Task HasAzureOAuth(EnterpriseManagerClient entMgr, string entLookup, string username)
        {
            // var hasDevOps = await entMgr.HasAzureOAuth(entLookup, username);

            // State.HasAzureOAuth = hasDevOps.Status;
        }

        // public virtual async Task HasDevOpsOAuth(EnterpriseManagerClient entMgr, string entLookup, string username)
        // {
        //     var hasDevOps = await entMgr.HasDevOpsOAuth(entLookup, username);

        //     State.HasDevOpsOAuth = hasDevOps.Status;
        // }

        public virtual async Task ListSubscribers(IdentityManagerClient idMgr, string entLookup)
        {
            // Get the list of subscribers based on subscriber status
            var subscriberResp = await idMgr.ListLicensedSubscribers(entLookup);

            State.Subscribers = subscriberResp.Model;
        }

        public virtual async Task<Status> ListLicenses(IdentityManagerClient idMgr, string entLookup, string username)
        {
            var licenseAccess = await idMgr.ListLicenseAccessTokens(entLookup, username, new List<string>() { "lcu" });

            State.UserLicenses = licenseAccess.Model;

            return (licenseAccess != null) ? Status.Success : Status.Unauthorized.Clone($"No licenses found for user {username}");
        }

        public virtual async Task LoadRegistrationHosts(EnterpriseManagerClient entMgr, string entLookup)
        {
            if (State.HostOptions.IsNullOrEmpty())
            {
                var regHosts = await entMgr.ListRegistrationHosts(entLookup);

                State.HostOptions = regHosts.Model;
            }
        }

        public virtual async Task LoadSubscriptionDetails(EnterpriseManagerClient entMgr, SecurityManagerClient secMgr, string entLookup, string username)
        {
            // get subscription token by user name
            var subIdToken = await secMgr.RetrieveIdentityThirdPartyData(entLookup, username, "LCU-STRIPE-SUBSCRIPTION-ID");

            string subId = subIdToken.Model["LCU-STRIPE-SUBSCRIPTION-ID"]?.ToString();

            if (!String.IsNullOrEmpty(subId))
            {

                // get subscription details 
                var subDetails = await entMgr.GetStripeSubscriptionDetails(subId, entLookup);

                State.SubscriptionDetails = subDetails.Model;
            }

        }

        public virtual async Task<Status> HasLicenseAccessWithLookup(IdentityManagerClient idMgr, string entLookup, string username, string lookup)
        {
            var licenseAccess = await idMgr.HasLicenseAccess(entLookup, username, AllAnyTypes.All, new List<string>() { "lcu" });

            return licenseAccess.Status ? Status.Success : Status.Unauthorized.Clone($"No license found for user {username}");
        }

        public virtual async Task<Status> RequestAuthorization(SecurityManagerClient secMgr, ApplicationManagerClient appMgr, IdentityManagerClient idMgr, string userID, string enterpriseID, string hostName)
        {
            // Create an access request
            var accessRequest = new AccessRequest()
            {
                User = userID,
                EnterpriseID = enterpriseID
            };

            // Create JToken to attached to metadata model
            var model = new MetadataModel();
            model.Metadata.Add(new KeyValuePair<string, JToken>("AccessRequest", JToken.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(accessRequest))));

            // Create token model - is including the access request payload redundant?? 
            var tokenModel = new CreateTokenModel()
            {
                Payload = model,
                UserEmail = userID,
                OrganizationID = enterpriseID,
                Encrypt = true
            };

            // Encrypt user email and enterpries ID, generate token
            var response = await secMgr.CreateToken("RequestAccessToken", tokenModel);

            // Build grant/deny links and text body
            if (response != null)
            {
                string grantLink = $"<a href=\"{hostName}/grant/token?={response.Model}\">Grant Access</a>";
                string denyLink = $"<a href=\"{hostName}/deny/token?={response.Model}\">Deny Access</a>";
                string emailHtml = $"A user has requested access to this Organization : {grantLink} {denyLink}";

                // Send email from app manager client 

                var email = new AccessRequestEmail()
                {
                    Content = emailHtml,
                    EmailFrom = "registration@fathym.com",
                    EmailTo = "registration@fathym.com",
                    User = userID,
                    Subject = "Access authorization requested",
                    EnterpriseID = enterpriseID
                };

                var emailModel = new MetadataModel();
                model.Metadata.Add(new KeyValuePair<string, JToken>("AccessRequestEmail", JToken.Parse(JsonConvert.SerializeObject(email))));

                var reqResult = await appMgr.SendAccessRequestEmail(model, enterpriseID);

                State.RequestAuthorizationSent = (reqResult.Status) ? "True" : "False";
            }

            // If successful, adjust state to reflect that a request was sent for this enterprise by this user
            return Status.Success;
        }

        public virtual async Task<Status> SecureHost(EnterpriseManagerClient entMgr)
        {
            var root = State.HostOptions.FirstOrDefault();

            var host = $"{State.OrganizationLookup}.{root}";

            try
            {
                var hostResp = await entMgr.ResolveHost(host, false);

                if (hostResp.Status == Status.NotLocated)
                    State.Host = host;

                return hostResp.Status;
            }
            catch (Exception ex)
            {
                return Status.NotLocated;
            }
        }

        public virtual void SetBootOptionsLoading()
        {
            State.BootOptions.ForEach(bo =>
            {
                bo.Loading = true;
            });
        }

        public virtual async Task<Status> SetupRepositories(DevOpsArchitectClient devOpsArch, string username)
        {
            if (!State.NewEnterpriseLookup.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty() && !State.ProjectID.IsNullOrEmpty())
            {
                var resp = await devOpsArch.SetupRepositories(new Personas.DevOps.SetupRepositoriesRequest()
                {
                    EnvironmentLookup = State.EnvironmentLookup,
                    ProjectID = State.ProjectID,
                    Username = username
                }, State.NewEnterpriseLookup);

                return resp.Status;
            }
            else
                return Status.GeneralError.Clone("Boot not properly configured.");
        }

        public virtual void UpdateStatus(Status status)
        {
            State.Status = status;
        }

        public virtual void UpdateBootOption(string bootOptionLookup, int bootStep, Status status = null, bool? loading = null)
        {
            var bootOption = State.BootOptions.FirstOrDefault(bo => bo.Lookup == bootOptionLookup);

            if (bootOption != null)
            {
                if (status != null)
                {
                    bootOption.Status = status;

                    if (status.Code == 0)
                    {
                        UpdateBootOptionText(bootOptionLookup);
                    }
                }

                bootOption.CompletedSteps = bootStep - 1;

                if (loading.HasValue)
                    bootOption.Loading = loading.Value;
            }
        }

        public virtual void UpdateBootOptionText(string bootOptionLookup)
        {
            var bootOption = State.BootOptions.FirstOrDefault(bo => bo.Lookup == bootOptionLookup);

            switch (bootOption.Lookup)
            {
                case "Project":
                    bootOption.Name = "Workspace Details Configured";
                    break;
                case "DevOps":
                    bootOption.Name = "DevOps Connected";
                    break;
                case "Infrastructure":
                    bootOption.Name = "Infrastructure Connected";
                    break;
                case "Domain":
                    bootOption.Name = "Domain Configured";
                    break;
                case "MicroApps":
                    bootOption.Name = "Micro-Application Orchestrated";
                    break;
            }

            State.BootOptions.FirstOrDefault(bo => bo.Lookup == bootOptionLookup).Name = bootOption.Name;
        }

        public virtual async Task<Status> SendFeedback(EnterpriseManagerClient entMgr, string entLookup, string username, string feedback)
        {

            // Send email from app manager client 
            var model = new MetadataModel();

            var email = new FeedbackEmail()
            {
                FeedbackReason = feedback,
                EmailFrom = "registration@fathym.com",
                EmailTo = "marketing@fathym.com",
                User = username,
                Subject = "Service feedback",
                EnterpriseID = entLookup
            };

            var emailModel = new MetadataModel();

            model.Metadata.Add(new KeyValuePair<string, JToken>("FeedbackEmail", JToken.Parse(JsonConvert.SerializeObject(email))));

            await entMgr.SendFeedbackEmail(model, entLookup);

            return Status.Success;
        }

        public virtual async Task<Status> SendNotification(EnterpriseManagerClient entMgr, string entLookup, string username, SendNotificationRequest notification)
        {
            // Send email from app manager client 
            var model = new MetadataModel();

            model.Metadata.Add(new KeyValuePair<string, JToken>("SendNotificationRequest", JToken.Parse(JsonConvert.SerializeObject(notification))));

            await entMgr.SendNotification(model, entLookup);

            return Status.Success;
        }

        public virtual void SetNapkinIDESetupStep(NapkinIDESetupStepTypes step)
        {
            State.SetupStep = step;

            if (State.SetupStep == NapkinIDESetupStepTypes.Review)
                ConfigureBootOptions();
        }

        public virtual async Task<Status> SetLicenseAccess(IdentityManagerClient idMgr, string entLookup, string username, int trialLength, bool isLocked, bool isReset)
        {
            var response = await idMgr.IssueLicenseAccess(new LicenseAccessToken()
            {
                IsLocked = isLocked,
                IsReset = isReset,
                TrialPeriodDays = trialLength,
                Username = username
            }, entLookup);

            return response.Status;
        }

        public virtual async Task SetOrganizationDetails(EnterpriseManagerClient entMgr, string name, string description, string lookup, bool accepted)
        {
            State.OrganizationName = name;

            State.OrganizationDescription = description;

            State.OrganizationLookup = lookup;

            State.AzureLocationOptions = null;

            State.AzureInfrastructureValid = null;

            var secured = await SecureHost(entMgr);

            if (secured == Status.NotLocated)
            {
                State.TermsAccepted = accepted;

                if (!name.IsNullOrEmpty())
                    SetNapkinIDESetupStep(NapkinIDESetupStepTypes.AzureSetup);
                else
                    SetNapkinIDESetupStep(NapkinIDESetupStepTypes.OrgDetails);

                State.Status = null;
            }
            else
                State.Status = new Status()
                {
                    Code = (int)UserManagementErrorCodes.HostAlreadyExists,
                    Message = "An enterprise with that lookup already exists."
                };
        }

        public virtual void SetUserType(UserTypes userType)
        {
            State.UserType = userType;
        }

        public virtual async Task<Status> ValidateSubscription(EnterpriseManagerClient entMgr, IdentityManagerClient idMgr, string entLookup, string username, string subscriberId)
        {
            // Get subscription status from Stripe for a user
            var response = await entMgr.ValidateSubscription(subscriberId, entLookup);

            // If subscription status is inactive
            if (!response.Status)
            {

                // Get the user's LATs from the graph db
                var licenseAccess = await idMgr.ListLicenseAccessTokens(entLookup, username, new List<string>() { "LCU" });

                // If user has a LAT that is not limited trial, expire the LAT
                foreach (LicenseAccessToken token in licenseAccess.Model)
                {

                    token.IsLocked = true;

                    if (token.Lookup != "LCU.NapkinIDE.LimitedTrial")
                    {
                        await idMgr.IssueLicenseAccess(token, entLookup);
                    }
                }

                return Status.Success;
            }

            return response.Status;
        }
        #endregion
    }
}
