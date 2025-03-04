using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.CloudSave.Data;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Constants = FlyingAcorn.Soil.Core.Data.Constants;

namespace FlyingAcorn.Soil.CloudSave
{
    public static class CloudSave
    {
        private static HttpClient _loadClient;
        private static HttpClient _saveClient;
        private static readonly string CloudSaveUrl = $"{Constants.ApiUrl}/cloudsave/";

        [UsedImplicitly]
        public static async Task Initialize()
        {
            await SoilServices.Initialize();
        }

        public static async Task SaveAsync(string key, object value, bool isPublic = false)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new Exception("Key cannot be null or empty");
            }

            if (string.IsNullOrEmpty(value?.ToString()))
            {
                throw new Exception("Value cannot be null or empty");
            }


            var payload = new Dictionary<string, object>()
            {
                { "key", key },
                { "value", value },
                { "is_public", isPublic }
            };

            await Initialize();

            var stringBody = JsonConvert.SerializeObject(payload, Formatting.None);
            
            // _saveClient?.Dispose(); // support async
            _saveClient = new HttpClient();
            _saveClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            _saveClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            _saveClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Post, CloudSaveUrl);
            request.Content = new StringContent(stringBody, System.Text.Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            string responseString;

            try
            {
                response = await _saveClient.SendAsync(request);
                responseString = response.Content.ReadAsStringAsync().Result;

            }
            catch (Exception e)
            {
                throw new SoilException($"Network error while updating player info. Error: {e.Message}",
                    SoilExceptionErrorCode.TransportError);
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Network error while updating player info. Response: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            var saveResponse = JsonConvert.DeserializeObject<SaveModel>(responseString);
            if (saveResponse == null)
            {
                throw new SoilException("Failed to save data", SoilExceptionErrorCode.InvalidResponse);
            }

            CloudSavePlayerPrefs.Save(saveResponse);
            MyDebug.Info($"{key} saved in cloud");
        }

        public static async Task<SaveModel> LoadAsync(string key, string otherUserID = null,
            List<Constants.DataScopes> extraScopes = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new Exception("Key cannot be null or empty");
            }

            await Initialize();

            // _loadClient?.Dispose(); // support async
            _loadClient = new HttpClient();
            _loadClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            _loadClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            _loadClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var query = $"?key={key}";
            if (!string.IsNullOrEmpty(otherUserID))
                query += $"&user={otherUserID}";
            if (extraScopes is { Count: > 0 })
                query += $"&extra_scopes={string.Join(",", extraScopes.Distinct())}";
            var request = new HttpRequestMessage(HttpMethod.Get, $"{CloudSaveUrl}{query}");
            var response = await _loadClient.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException($"Key {key} not found");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Network error while updating player info. Response: {responseString}");
            }

            var saveResponse = JsonConvert.DeserializeObject<SaveModel>(responseString);
            if (saveResponse == null)
            {
                throw new Exception("Failed to load data");
            }

            if (string.IsNullOrEmpty(otherUserID) || otherUserID == SoilServices.UserInfo.uuid)
                CloudSavePlayerPrefs.Save(saveResponse);
            MyDebug.Verbose($"{key} loaded from cloud");
            return saveResponse;
        }
    }
}