using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.RemoteConfig.ABTesting;
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
            RemoteConfigPlayerPrefs.CachedRemoteConfigData?[Constants.UserDefinedParentKey] as JObject;

        [UsedImplicitly]
        public static JObject ExchangeRates =>
            RemoteConfigPlayerPrefs.CachedRemoteConfigData?["exchange_rates"] as JObject;

        private static bool _fetchSuccessState;

        private static Dictionary<string, object> _sessionExtraProperties = new();
        private static bool _fetching;

        private const string FetchUrl = Core.Data.Constants.ApiUrl + "/remoteconfig/";


        public static void FetchConfig(Dictionary<string, object> extraProperties = null)
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
                _fetchSuccessState = false;
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
            var response = new HttpResponseMessage();
            var responseString = string.Empty;
            try
            {
                response = await client.SendAsync(request);
                responseString = response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"FlyingAcorn ====> Failed to fetch remote config. Error: {e.Message}");
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                Debug.LogError(responseString);
                _fetchSuccessState = false;
            }
            else
            {
                try
                {
                    RemoteConfigPlayerPrefs.CachedRemoteConfigData = JObject.Parse(responseString);
                    ABTestHandler.InitializeAbTesting(UserDefinedConfigs);
                    _fetchSuccessState = true;
                    Debug.Log("FlyingAcorn ====> Remote config fetched successfully."); 
                }
                catch (Exception)
                {
                    Debug.LogError($"FlyingAcorn ====> Failed to parse fetched data. Data: {responseString}");
                    _fetchSuccessState = false;
                }
            }

            _fetching = false;
            OnServerAnswer?.Invoke(_fetchSuccessState);
            if (_fetchSuccessState)
                OnSuccessfulFetch?.Invoke(RemoteConfigPlayerPrefs.CachedRemoteConfigData);
        }
    }
}