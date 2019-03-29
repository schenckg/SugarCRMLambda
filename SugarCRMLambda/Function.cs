using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Newtonsoft.Json;
using SugarCRMLibrary;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace SugarCRMLambda
{
    public class Function
    {
        string SugarURL { get; set; }
        string SugarUser { get; set; }
        string SugarPassword { get; set; }

        /// <summary>
        /// AWS Lambda Function intended to be called by Amazon Connect.
        /// Searches for Sugar CRM Contacts with matching phone number in Sugar CRM.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<object> FunctionHandler(dynamic obj, ILambdaContext context)
        {
            Log.Info("--------- STARTING ------------");
            Log.Debug(GetObjectString(obj));


            // Get the phone number of the customer
            Log.Debug("-------------------------------");
            string phoneNumber = obj?["Details"]?["ContactData"]?["CustomerEndpoint"]?.Address;
            Log.Info($"FunctionHandler: phoneNumber: {phoneNumber}");

            if (string.IsNullOrEmpty(phoneNumber))
                return ErrorReturn("phonennumber is empty or not set");

            bool bLoggedIn = Sugar.UserLoggedIn;
            Log.Debug($"User Logged In: {bLoggedIn}");

            if (!bLoggedIn)
            {
                GetSettingsFromEnvironment();

                // Authenticate the user with Sugar CRM REST interface
                try
                {
                    await Sugar.Authenticate(SugarURL, SugarUser, SugarPassword);
                }
                catch (Exception ex)
                {
                    return ErrorReturn(ex.Message);
                }
            }

            Debug.Assert(Sugar.UserLoggedIn);

            try
            {
                var contacts = await Sugar.SearchContacts(phoneNumber);
                Log.Debug($"Found {contacts.Count} contacts");
                foreach (Contact contact in contacts)
                    Log.Debug($"Contact Name: {contact.Name}, ID: {contact.ID}");

                if (contacts.Count > 0)
                {
                    Log.Info($">>>>> {contacts[0].Name} <<<<<");
                    return new
                    {
                        Contact = $"\"{contacts[0].Name}\""
                    };
                }
                else
                {
                    return "{}";
                }
            }
            catch (Exception ex)
            {
                return ErrorReturn(ex.Message);
            }

            // THIS WORKS WITH WHEN RETURNED TO CONNECT
            //var result = new
            //{
            //    statusCode = 200,
            //    body = "\"Hello from Lambda!\""
            //};
        }

        private void GetSettingsFromEnvironment()
        {
            SugarURL = Environment.GetEnvironmentVariable("SugarURL");
            SugarUser = Environment.GetEnvironmentVariable("SugarUser");
            SugarPassword = Environment.GetEnvironmentVariable("SugarPassword");
            Log.Info($"Settings from Environment: Sugar URL: {SugarURL}, Sugar User: {SugarUser}, Sugar Password: {(string.IsNullOrEmpty(SugarPassword) ? "<empty>" : "********")}");
        }

        private static string GetObjectString(object o)
        {
            string json = JsonConvert.SerializeObject(o, Formatting.Indented);
            return json;
        }

        private object ErrorReturn(string strError)
        {
            Log.Error(strError);
            var result = new
            {
                error = $"\"{strError}\""
            };
            return result;
        }
    }
}
