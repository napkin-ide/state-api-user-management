using Fathym.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace state_api_user_management_tests
{
    [TestClass]
    public class ListLicensesTests : AzFunctionTestBase
    {
        
        public ListLicensesTests() : base()
        {
            APIRoute = "api/ListLicenses";                
        }

        [TestMethod]
        public async Task TestListLicenses()
        {
            LcuEntApiKey = "3ebd1c0d-22d0-489e-a46f-3260103c8cd7";            
            PrincipalId = "george.hatch@fathym.com";

            addRequestHeaders();

            var url = $"{HostURL}/{APIRoute}";            

            var response = await httpGet(url); 

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            var model = getContent<dynamic>(response);

            //throw new NotImplementedException("Implement me!");
        }
    }
}
