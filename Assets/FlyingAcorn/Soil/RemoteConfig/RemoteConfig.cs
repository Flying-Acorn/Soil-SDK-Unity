using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
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

        [UsedImplicitly]
        public static JObject UserDefinedConfigs =>
            RemoteConfigPlayerPrefs.CachedRemoteConfigData?["remote_configs"] as JObject;

        [UsedImplicitly]
        public static JObject ExchangeRates =>
            RemoteConfigPlayerPrefs.CachedRemoteConfigData?["exchange_rates"] as JObject;

        [UsedImplicitly] public static bool FetchSuccessState;

        private static Dictionary<string, object> _sessionExtraProperties = new();
        private static bool _fetching;

        private const string FetchUrl = Constants.ApiUrl + "/remoteconfig/";


        public static async void FetchConfig(Dictionary<string, object> extraProperties = null)
        {
            _sessionExtraProperties = extraProperties ?? new Dictionary<string, object>();

            FetchRemoteConfig();
        }

        private static Dictionary<string, object> GetPlayerProperties()
        {
            var properties = _sessionExtraProperties;
            foreach (var property in UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties())
                properties[property.Key] = property.Value;
            return properties;
        }

        private static async void FetchRemoteConfig()
        {
            if (_fetching) return;
            _fetching = true;
            try
            {
                await SoilServices.Initialize();
            }
            catch (Exception e)
            {
                Debug.LogError($"FlyingAcorn ====> Failed to initialize SoilServices. Error: {e.Message}");
                _fetching = false;
                FetchSuccessState = false;
                OnServerAnswer?.Invoke(false);
                return;
            }

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
                FetchSuccessState = false;
            }
            else
            {
                try
                {
                    RemoteConfigPlayerPrefs.CachedRemoteConfigData = JObject.Parse(responseString);
                    FetchSuccessState = true;
                    Debug.Log("FlyingAcorn ====> Remote config fetched successfully."); 
                }
                catch (Exception)
                {
                    Debug.LogError($"FlyingAcorn ====> Failed to parse fetched data. Data: {responseString}");
                    FetchSuccessState = false;
                }
            }

            _fetching = false;
            OnServerAnswer?.Invoke(FetchSuccessState);
            if (FetchSuccessState)
                OnSuccessfulFetch?.Invoke(RemoteConfigPlayerPrefs.CachedRemoteConfigData);
        }
    }
}