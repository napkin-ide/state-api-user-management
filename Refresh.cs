using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Fathym;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Runtime.Serialization;
using Fathym.API;
using System.Collections.Generic;
using System.Linq;
using LCU.Personas.Client.Applications;

namespace LCU.State.API.NapkinIDE.User.Management
{
    [Serializable]
    [DataContract]
    public class RefreshRequest : BaseRequest
    { }

    public class Refresh
    {
        protected ApplicationDeveloperClient appDev;

        public Refresh(ApplicationDeveloperClient appDev)
        {
            this.appDev = appDev;
        }

        [FunctionName("Refresh")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = UserManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateAction<UserManagementState, RefreshRequest>(req, signalRMessages, log, async (state, refreshReq) =>
            {
                log.LogInformation($"Refresh");

                // if (state.Personas.IsNullOrEmpty())
                state.Personas = new List<JourneyPersona>()
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

                state.Details = new List<JourneyDetail>()
                {
                    new JourneyDetail()
                    {
                        Name = "SPAs with Angular",
                        Lookup = "AngularSPA",
                        Description = "Create and host your next Angular application with Fathym's Low Code Unit."
                    },
                    new JourneyDetail()
                    {
                        Name = "Low Code Unit Blade",
                        Lookup = "LCUBlade",
                        Description = "Create a new Low Code Unit Blade for your Enterprise IDE."
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

                state.UserType = state.Personas.First().Lookup.As<UserTypes>();

                return state;
            });
        }
    }
}
