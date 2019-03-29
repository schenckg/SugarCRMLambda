using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace SugarCRMLibrary
{
    public static class Sugar
    {
        private const bool LogSoap = false;

        static public string SugarURL { get; private set; }
        static public string SugarUser { get; private set; }
        static public string SugarPassword { get; private set; }

        static public bool UserLoggedIn { get; private set; } = false;
        static public string AccessToken { get; private set; }
        static public string RefreshToken { get; private set; }

        static Sugar()
        {
        }

        static public async Task Authenticate(string strURL, string strUser, string strPassword)
        {
            Log.Info($">>> Authenticate: URL: {strURL}, User: {strUser}, Password: {strPassword}");
            SugarURL = strURL;

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(SugarURL);
                client.DefaultRequestHeaders
                    .Accept
                    .Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", "AWS Lambda Function");

                string strBody = $@"{{
                    ""grant_type"": ""password"",
                    ""client_id"": ""sugar"",
                    ""platform"": ""LambdaFunction"",
                    ""username"": ""{strUser}"",
                    ""password"": ""{strPassword}""
                }}";

                try
                {
                    dynamic obj = await PostRequest("rest/v11/oauth2/token", strBody);
                    if (LogSoap)
                        Log.Debug($"Authenticate returned {obj}");

                    // Grab the error if any
                    string strError = obj["error"];
                    if (strError != null)
                    {
                        // Refresh failed, throw an error
                        string strErrorMessage = obj["error_message"];
                        Log.Warning($"<<< Authenticate: Failed: {strError}, error message: {strErrorMessage}");
                        throw new Exception(strErrorMessage);
                    }

                    UserLoggedIn = true;
                    SugarUser = strUser;
                    SugarPassword = strPassword;
                    AccessToken = obj["access_token"];
                    RefreshToken = obj["refresh_token"];
                }
                catch (Exception ex)
                {
                    string strError = $"Failed trying to authenticate user {strUser}: {ex.Message}";
                    Log.Error($"<<< Authenticate: {strError}");
                    throw new Exception(strError);
                }
            }
            Log.Info($"<<< Authenticate: Complete");
        }

        static private async Task Reauthenticate()
        {
            await Authenticate(SugarURL, SugarUser, SugarPassword);
        }

        static private async Task Refresh()
        {
            Log.Debug($">>> Refresh: Refresh: Token: {RefreshToken}");

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(SugarURL);
                client.DefaultRequestHeaders
                    .Accept
                    .Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", "AWS Lambda Function");

                string strBody = $@"{{
                    ""grant_type"": ""refresh_token"",
                    ""client_id"": ""sugar"",
                    ""refresh_token"": ""{RefreshToken}""
                }}";

                dynamic obj = await PostRequest("rest/v11/oauth2/token", strBody);
                Log.Debug($"Refresh returned {obj}");

                // Grab the error if any
                string strError = obj["error"];
                if (strError != null)
                {
                    // Refresh failed, throw an error
                    string strErrorMessage = obj["error_message"];
                    Log.Warning($"<<< Refresh: Failed: {strError}, error message: {strErrorMessage}");
                    throw new Exception(strErrorMessage);
                }

                AccessToken = obj["access_token"];
                RefreshToken = obj["refresh_token"];
                Log.Info($"<<< Refresh: Complete");
            }
        }

        static private async Task<dynamic> Post(string strRestAction, string strBody)
        {
            Log.Debug($">>> Post: Action: {strRestAction}");
            if (LogSoap)
                Log.Debug($"Post Body: {strBody}");

            // Note we don't capture exceptions overall so caller is responsible for handling failures
            if (!UserLoggedIn)
            {
                Log.Error($"<<< Post: User is not authenticated");
                throw new Exception("User is not authenticated");
            }

            dynamic obj = await PostRequest(strRestAction, strBody);

            // Grab the error if any
            string strError = obj["error"];

            // Did our session expire?
            if (strError == "invalid_grant")
            {
                Log.Warning($"Post: Ssession expired, attempt to refresh");

                try
                {
                    // Yes, we need to try to refresh our connection
                    await Refresh();
                    Log.Info($"Post: Session successfully refreshed");
                }
                catch (Exception)
                {
                    // Looks like, not only did our token expire, but we also failed to generate a new one
                    Log.Warning($"Post: Refresh failed, attempt to reauthenticate");

                    // Try Re-authenticating...
                    // If this throws an exception we'll let it bubble up
                    await Reauthenticate();
                    Log.Info($"Post: Session successfully reauthenticated");
                }

                // Now try our request again
                obj = await PostRequest(strRestAction, strBody);
                strError = obj["error"];
            }

            if (strError != null)
            {
                // Something we didn't expect, throw an error
                string strErrorMessage = obj["error_message"];
                Log.Warning($"<<< Post: Failed, error: {strError}, error message: {strErrorMessage}");
                throw new Exception(strErrorMessage);
            }

            Log.Debug($"<<< Post: Complete");
            return obj;
        }

        static private async Task<dynamic> PostRequest(string strRestAction, string strBody)
        {
            Log.Debug($">>> PostRequest: Action: {strRestAction}");
            //Log.Debug($"PostRequest Body: {strBody}");

            HttpContent content = new StringContent(strBody, Encoding.UTF8, "application/json");
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(SugarURL);
                client.DefaultRequestHeaders
                    .Accept
                    .Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("oAuth-Token", AccessToken);
                client.DefaultRequestHeaders.Add("User-Agent", "C# App");

                // Make the initial request
                var result = await client.PostAsync(strRestAction, content);
                string resultContent = await result.Content.ReadAsStringAsync();
                dynamic obj = JsonConvert.DeserializeObject<dynamic>(resultContent);
                if (LogSoap)
                    Log.Debug($"PostRequest: PostAsync returned: {Environment.NewLine}{obj}");
                Log.Debug($"<<< PostRequest: Complete");
                return obj;
            }
        }

        static private string CleanNumber(string strNumber)
        {
            string strCleanNumber = new string(strNumber.Where(c => char.IsDigit(c)).ToArray<char>());
            if (strCleanNumber.Length == 11 && strCleanNumber[0] == '1')
                strCleanNumber = strCleanNumber.Substring(1);
            return strCleanNumber;
        }

        static private void GetSearchNumberParts(string strNumber, out string strAreaCode, out string strPrefix, out string strLine)
        {
            strAreaCode = strNumber.Substring(0, 3);
            strPrefix = strNumber.Substring(3, 3);
            strLine = strNumber.Substring(6);
        }

        static public async Task<List<Contact>> SearchContacts(string strNumber)
        {
            strNumber = CleanNumber(strNumber);
            Log.Info($">>> SearchContacts: Number: {strNumber}");

            string strFilter;
            if (strNumber.Length == 10)
            {
                // 10 digit number, we need to search on the parts of the number
                GetSearchNumberParts(strNumber, out string strAreaCode, out string strPrefix, out string strLine);
                strFilter = $@"
                    [
                        {{""$or"": [
                            {{""$and"": [
                                {{""phone_work"":{{""$contains"":""{strAreaCode}""}}}},
                                {{""phone_work"":{{""$contains"":""{strPrefix}""}}}},
                                {{""phone_work"":{{""$contains"":""{strLine}""}}}}
                            ]}},
                            {{""$and"":[
                                {{""phone_mobile"":{{""$contains"":""{strAreaCode}""}}}},
                                {{""phone_mobile"":{{""$contains"":""{strPrefix}""}}}},
                                {{""phone_mobile"":{{""$contains"":""{strLine}""}}}}
                            ]}},
                            {{""$and"":[
                                {{""phone_home"":{{""$contains"":""{strAreaCode}""}}}},
                                {{""phone_home"":{{""$contains"":""{strPrefix}""}}}},
                                {{""phone_home"":{{""$contains"":""{strLine}""}}}}
                            ]}},
                            {{""$and"":[
                                {{""phone_other"":{{""$contains"":""{strAreaCode}""}}}},
                                {{""phone_other"":{{""$contains"":""{strPrefix}""}}}},
                                {{""phone_other"":{{""$contains"":""{strLine}""}}}}
                            ]}}
                        ]}}
                    ]";
            }
            else
            {
                strFilter = $@"
                    [
                        {{""$or"": [
                            {{""phone_work"":{{""$equals"":""{strNumber}""}}}},
                            {{""phone_mobile"":{{""$equals"":""{strNumber}""}}}},
                            {{""phone_home"":{{""$equals"":""{strNumber}""}}}},
                            {{""phone_other"":{{""$equals"":""{strNumber}""}}}}
                        ]}}
                    ]";
            }

            string strBody =
                $@"{{""filter"": {strFilter},
                ""fields"": ""id,name,account_id,account_name"",
                ""order_by"": ""date_modified:ASC"",
                ""max_num"": 20,
                ""offset"": 0
            }}";

            try
            {
                dynamic obj = await Post("rest/v11/Contacts/filter", strBody);
                JArray records = obj["records"];

                List<Contact> contacts = new List<Contact>();
                foreach (JToken record in records)
                {
                    string strID = record.Value<string>("id");
                    string strName = record.Value<string>("name");
                    string strAccountID = record.Value<string>("account_id");
                    string strAccountName = record.Value<string>("account_name");
                    if (string.IsNullOrEmpty(strName))
                        continue;

                    contacts.Add(new Contact(strID, strName, strAccountID, strAccountName));
                }

                Log.Info($"<<< SearchContacts: Complete, returning {contacts.Count} records");
                return contacts;
            }
            catch (Exception ex)
            {
                Log.Warning($"<<< SearchContacts failed {ex.Message}");
                throw new Exception($"SearchContacts failed: {ex.Message}");
            }
        }
    }
}
