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
        private static readonly string CloudSaveUrl = $"{Constants.ApiUrl}/cloudsave/";

        [UsedImplicitly]
        [System.Obsolete("Initialize() is deprecated. Use event-based approach with SoilServices.InitializeAsync() instead. Subscribe to SoilServices.OnServicesReady and SoilServices.OnInitializationFailed events.", true)]
        public static async Task Initialize()
        {
            await SoilServices.InitializeAndWait();
        }

        public static async Task SaveAsync(string key, object value, bool isPublic = false)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new SoilException("Key cannot be null or empty", SoilExceptionErrorCode.InvalidRequest);
            }

            if (string.IsNullOrEmpty(value?.ToString()))
            {
                throw new SoilException("Value cannot be null or empty", SoilExceptionErrorCode.InvalidRequest);
            }


            var payload = new Dictionary<string, object>()
            {
                { "key", key },
                { "value", value },
                { "is_public", isPublic }
            };

            await SoilServices.InitializeAndWait();

            var stringBody = JsonConvert.SerializeObject(payload, Formatting.None);

            using var saveClient = new HttpClient();
            saveClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            saveClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            saveClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Post, CloudSaveUrl);
            request.Content = new StringContent(stringBody, System.Text.Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            string responseString;
            try
            {
                response = await saveClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException)
            {
                throw new SoilException("Request timed out while saving data", SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while saving data: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while saving data: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}",
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
                throw new SoilException("Key cannot be null or empty", SoilExceptionErrorCode.InvalidRequest);
            }

            await SoilServices.InitializeAndWait();

            using var loadClient = new HttpClient();
            loadClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            loadClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            loadClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var query = $"?key={key}";
            if (!string.IsNullOrEmpty(otherUserID))
                query += $"&user={otherUserID}";
            if (extraScopes is { Count: > 0 })
                query += $"&extra_scopes={string.Join(",", extraScopes.Distinct())}";
            var request = new HttpRequestMessage(HttpMethod.Get, $"{CloudSaveUrl}{query}");

            HttpResponseMessage response;
            string responseString;
            try
            {
                response = await loadClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException)
            {
                throw new SoilException("Request timed out while loading data", SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while loading data: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while loading data: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new SoilNotFoundException($"Key {key} not found");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            var saveResponse = JsonConvert.DeserializeObject<SaveModel>(responseString);
            if (saveResponse == null)
            {
                throw new SoilException("Failed to load data", SoilExceptionErrorCode.InvalidResponse);
            }

            if (string.IsNullOrEmpty(otherUserID) || otherUserID == SoilServices.UserInfo.uuid)
                CloudSavePlayerPrefs.Save(saveResponse);
            MyDebug.Verbose($"{key} loaded from cloud");
            return saveResponse;
        }
    }
}