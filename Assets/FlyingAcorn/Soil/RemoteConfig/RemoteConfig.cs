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
using Unity.VisualScripting;
using UnityEngine;

namespace FlyingAcorn.Soil.RemoteConfig
{
    public static class RemoteConfig
    {
        [UsedImplicitly] public static Action<JObject> OnRemoteConfigSuccessfulFetch;
        [UsedImplicitly] public static Action<bool> OnRemoteConfigServerAnswer;
        [UsedImplicitly] public static JObject LatestRemoteConfigData => RemoteConfigPlayerPrefs.CachedRemoteConfigData;
        private static Action _onInitialize;


        private static Dictionary<string, object> _sessionExtraProperties = new();

        private const string FetchUrl = Constants.ApiUrl + "/remoteconfig/";


        public static void FetchConfig(Dictionary<string, object> extraProperties = null)
        {
            _sessionExtraProperties = extraProperties ?? new Dictionary<string, object>();
            _ = FetchRemoteConfig();
        }

        private static Dictionary<string, object> GetPlayerProperties()
        {
            var properties = _sessionExtraProperties;
            if (SoilServices.UserInfo != null && SoilServices.UserInfo.properties != null)
                properties.AddRange(SoilServices.UserInfo.properties.ToDictionary());
            return properties;
        }

        private static async Task FetchRemoteConfig()
        {
            await SoilServices.Initialize();

            var stringBody = JsonConvert.SerializeObject(new Dictionary<string, object> { { "properties", GetPlayerProperties() } });
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
                OnRemoteConfigServerAnswer?.Invoke(false);
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
                    OnRemoteConfigServerAnswer?.Invoke(false);
                    return;
                }

                OnRemoteConfigServerAnswer?.Invoke(true);
                OnRemoteConfigSuccessfulFetch?.Invoke(LatestRemoteConfigData);
            }
        }
    }
}