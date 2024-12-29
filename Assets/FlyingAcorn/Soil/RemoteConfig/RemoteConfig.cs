using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using FlyingAcorn.Soil.RemoteConfig.ABTesting;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        private static readonly string FetchUrl = $"{Core.Data.Constants.ApiUrl}/remoteconfig/";


        private static async Task Initialize()
        {
            await SoilServices.Initialize();
        }

        public static async void FetchConfig(Dictionary<string, object> extraProperties = null)
        {
            if (_fetching) return;
            _fetching = true;
            _sessionExtraProperties = extraProperties ?? new Dictionary<string, object>();
            try
            {
                await FetchRemoteConfig();
            }
            catch (Exception)
            {
                _fetching = false;
                _fetchSuccessState = false;
                OnServerAnswer?.Invoke(false);
            }

            _fetching = false;
            OnServerAnswer?.Invoke(_fetchSuccessState);
            if (_fetchSuccessState)
                OnSuccessfulFetch?.Invoke(RemoteConfigPlayerPrefs.CachedRemoteConfigData);
        }

        private static Dictionary<string, object> GetPlayerProperties()
        {
            var properties = _sessionExtraProperties;
            foreach (var property in UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties())
                properties[property.Key] = property.Value;
            return properties;
        }

        private static async Task FetchRemoteConfig()
        {
            await Initialize();

            var stringBody = JsonConvert.SerializeObject(new Dictionary<string, object>
                { { "properties", GetPlayerProperties() } });
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, FetchUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            HttpResponseMessage response;
            string responseString;
            try
            {
                response = await client.SendAsync(request);
                responseString = response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception e)
            {
                throw new Exception($"FlyingAcorn ====> Failed to fetch remote config. Error: {e.Message}");
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                throw new Exception(
                    $"FlyingAcorn ====> Failed to fetch remote config. Status code: {response.StatusCode}");
            }

            try
            {
                RemoteConfigPlayerPrefs.CachedRemoteConfigData = JObject.Parse(responseString);
                ABTestHandler.InitializeAbTesting(UserDefinedConfigs);
                _fetchSuccessState = true;
            }
            catch (Exception)
            {
                throw new Exception($"FlyingAcorn ====> Failed to parse remote config. Response: {responseString}");
            }
        }
    }
}