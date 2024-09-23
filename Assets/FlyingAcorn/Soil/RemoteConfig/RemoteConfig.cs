using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FlyingAcorn.Soil.RemoteConfig
{
    public static class RemoteConfig
    {
        [UsedImplicitly] public static Action<JObject> OnSuccessfulFetch;
        [UsedImplicitly] public static Action<bool> OnServerAnswer;
        [UsedImplicitly] public static JObject Configs => RemoteConfigPlayerPrefs.CachedRemoteConfigData;

        private static Dictionary<string, object> _sessionExtraProperties = new();

        private const string FetchUrl = Constants.ApiUrl + "/remoteconfig/";


        public static async void FetchConfig(Dictionary<string, object> extraProperties = null)
        {
            _sessionExtraProperties = extraProperties ?? new Dictionary<string, object>();
            await FetchRemoteConfig();
        }

        private static Dictionary<string, object> GetPlayerProperties()
        {
            var properties = _sessionExtraProperties;
            if (SoilServices.UserInfo == null || SoilServices.UserInfo.properties == null) return properties;
            foreach (var property in SoilServices.UserInfo.properties.ToDictionary())
                properties[property.Key] = property.Value;
            return properties;
        }

        private static async Task FetchRemoteConfig()
        {
            await SoilServices.Initialize();

            var stringBody = JsonConvert.SerializeObject(new Dictionary<string, object>
                { { "properties", GetPlayerProperties() } });
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, FetchUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            var response = await client.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError(responseString);
                OnServerAnswer?.Invoke(false);
            }
            else
            {
                try
                {
                    RemoteConfigPlayerPrefs.CachedRemoteConfigData = JObject.Parse(responseString);
                }
                catch (Exception)
                {
                    Debug.LogError($"FlyingAcorn ====> Failed to parse fetched data. Data: {responseString}");
                    OnServerAnswer?.Invoke(false);
                    return;
                }

                OnServerAnswer?.Invoke(true);
                OnSuccessfulFetch?.Invoke(Configs);
            }
        }
    }
}