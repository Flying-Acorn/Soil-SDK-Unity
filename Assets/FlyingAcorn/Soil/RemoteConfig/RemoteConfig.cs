using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
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

        [UsedImplicitly]
        internal static JObject UserInfo =>
            RemoteConfigPlayerPrefs.CachedRemoteConfigData?[Constants.UserInfoKey] as JObject;

        [UsedImplicitly]
        internal static JObject PurchasingSettings =>
            RemoteConfigPlayerPrefs.CachedRemoteConfigData?[Constants.PurchasingSettingsKey] as JObject;

        internal static UserInfo RemoteConfigUserInfo;
        internal static Purchasing.Models.PurchasingSettings RemoteConfigPurchasingSettings;

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
            foreach (var property in Soil.Core.User.UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties())
                properties[property.Key] = property.Value;
            return properties;
        }

        private static async Task FetchRemoteConfig()
        {
            await Initialize();

            var stringBody = JsonConvert.SerializeObject(new Dictionary<string, object>
                { { "properties", GetPlayerProperties() } });

            using var fetchClient = new HttpClient();
            fetchClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            fetchClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, FetchUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            HttpResponseMessage response;
            string responseString;

            try
            {
                response = await fetchClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new SoilException("Request timed out while fetching remote config",
                    SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while fetching remote config: {ex.Message}",
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while fetching remote config: {ex.Message}",
                    SoilExceptionErrorCode.TransportError);
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            try
            {
                RemoteConfigPlayerPrefs.CachedRemoteConfigData = JObject.Parse(responseString);
                ABTestHandler.InitializeAbTesting(UserDefinedConfigs);
            }
            catch (Exception)
            {
                throw new SoilException($"Failed to parse remote config data. Response: {responseString}",
                    SoilExceptionErrorCode.InvalidResponse);
            }

            try
            {
                RemoteConfigUserInfo = null;
                if (RemoteConfigPlayerPrefs.CachedRemoteConfigData.ContainsKey(Constants.UserInfoKey))
                    RemoteConfigUserInfo = JsonConvert.DeserializeObject<UserInfo>(RemoteConfigPlayerPrefs.CachedRemoteConfigData[Constants.UserInfoKey].ToString());
                RemoteConfigUserInfo ??= SoilServices.UserInfo;
                UserApiHandler.ReplaceRegionInfo(RemoteConfigUserInfo);
            }
            catch (Exception e)
            {
                Analytics.MyDebug.LogException(e, $"Failed to parse remote config user info. Response: {responseString}");
            }

            try
            {
                RemoteConfigPurchasingSettings = null;
                if (RemoteConfigPlayerPrefs.CachedRemoteConfigData.ContainsKey(Constants.PurchasingSettingsKey))
                    RemoteConfigPurchasingSettings = JsonConvert.DeserializeObject<Purchasing.Models.PurchasingSettings>(RemoteConfigPlayerPrefs.CachedRemoteConfigData[Constants.PurchasingSettingsKey].ToString());

                _ = Purchasing.PurchasingPlayerPrefs.SetAlternateSettings(RemoteConfigPurchasingSettings);
            }
            catch (Exception)
            {
                _ = Purchasing.PurchasingPlayerPrefs.SetAlternateSettings(null);
                Analytics.MyDebug.Info($"Failed to parse remote config purchasing settings. Response: {responseString}");
            }
            _fetchSuccessState = true;
        }
    }
}