using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.CloudSave.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Constants = FlyingAcorn.Soil.Core.Data.Constants;

namespace FlyingAcorn.Soil.CloudSave
{
    public static class CloudSave
    {
        private static readonly string CloudSaveUrl = $"{Constants.ApiUrl}/cloudsave/";

        [UsedImplicitly]
        public static async Task Initialize()
        {
            await SocialAuthentication.Initialize();
        }

        public static async Task SaveAsync(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new Exception("Key cannot be null or empty");
            }

            if (string.IsNullOrEmpty(value?.ToString()))
            {
                throw new Exception("Value cannot be null or empty");
            }
            

            var payload = new SaveModel
            {
                key = key,
                value = value
            };
            
            await Initialize();

            var stringBody = JsonConvert.SerializeObject(payload, Formatting.None);
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Post, CloudSaveUrl);
            request.Content = new StringContent(stringBody, System.Text.Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Network error while updating player info. Response: {responseString}");
            }

            var saveResponse = JsonConvert.DeserializeObject<SaveModel>(responseString);
            if (saveResponse == null)
            {
                throw new Exception("Failed to save data");
            }

            CloudSavePlayerPrefs.Save(saveResponse);
            MyDebug.Info($"{key} saved in cloud");
        }

        public static async Task<object> LoadAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new Exception("Key cannot be null or empty");
            }

            await Initialize();

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Get, $"{CloudSaveUrl}?key={key}");
            var response = await client.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new Exception($"Key {key} not found");
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

            CloudSavePlayerPrefs.Save(saveResponse);
            MyDebug.Info($"{key} loaded from cloud");
            return saveResponse.value;
        }
    }
}