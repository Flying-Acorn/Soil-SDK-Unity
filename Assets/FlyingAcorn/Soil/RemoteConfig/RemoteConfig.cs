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
            RemoteConfigPlayerPrefs.CachedRemoteConfigData?[Constants.ExchangeRateParentKey] as JObject;

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

        private static string FetchUrl => $"{Core.Data.Constants.ApiUrl}/remoteconfig/";


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
            if (!SoilServices.Ready)
            {
                throw new SoilException("Soil services are not ready", SoilExceptionErrorCode.NotReady);
            }

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
            catch (Exception e)
            {
                throw new SoilException($"Failed to parse remote config data. Response: {e} - {responseString}", SoilExceptionErrorCode.InvalidResponse);
            }

            try
            {
                RemoteConfigUserInfo = null;
                if (RemoteConfigPlayerPrefs.CachedRemoteConfigData.ContainsKey(Constants.UserInfoKey))
                {
                    object userInfoRaw = RemoteConfigPlayerPrefs.CachedRemoteConfigData[Constants.UserInfoKey];
                    if (userInfoRaw != null)
                        RemoteConfigUserInfo = JsonConvert.DeserializeObject<UserInfo>(userInfoRaw.ToString());
                    else
                        Analytics.MyDebug.LogWarning("RemoteConfig: UserInfo key found but value is null");
                }
                else
                {
                    Analytics.MyDebug.LogWarning("RemoteConfig: No user_info key found in remote config response");
                }

                RemoteConfigUserInfo ??= SoilServices.UserInfo;
                UserApiHandler.ReplaceRegionInfo(RemoteConfigUserInfo);
            }
            catch (Exception e)
            {
                try
                {
                    var userInfoPayload = "<Failed to get payload>";
                    try
                    {
                        userInfoPayload = RemoteConfigPlayerPrefs.CachedRemoteConfigData?.ContainsKey(Constants.UserInfoKey) == true
                            ? RemoteConfigPlayerPrefs.CachedRemoteConfigData[Constants.UserInfoKey]?.ToString()
                            : "<UserInfoKey not present>";
                    }
                    catch
                    {
                        userInfoPayload = "<Error accessing user info payload>";
                    }

                    var errorContext = "<Failed to get context>";
                    try
                    {
                        errorContext = $"SoilServices.UserInfo null: {SoilServices.UserInfo == null}, " +
                                     $"UserPlayerPrefs.UserInfo null: {UserPlayerPrefs.UserInfo == null}, " +
                                     $"RemoteConfigUserInfo null: {RemoteConfigUserInfo == null}";
                    }
                    catch
                    {
                        errorContext = "<Error accessing context objects>";
                    }

                    Analytics.MyDebug.LogException(e, $"Failed to parse remote config user info. " +
                        $"Exception: {e.GetType().Name}: {e.Message}\n" +
                        $"Context: {errorContext}\n" +
                        $"StackTrace: {e.StackTrace}\n" +
                        $"UserInfo Payload: {userInfoPayload}\n" +
                        $"Full Response: {responseString}");
                }
                catch
                {
                    Analytics.MyDebug.LogException(e, "Failed to parse remote config user info - logging failed");
                }
            }

            try
            {
                RemoteConfigPurchasingSettings = null;
                if (RemoteConfigPlayerPrefs.CachedRemoteConfigData.ContainsKey(Constants.PurchasingSettingsKey))
                    RemoteConfigPurchasingSettings = JsonConvert.DeserializeObject<Purchasing.Models.PurchasingSettings>(RemoteConfigPlayerPrefs.CachedRemoteConfigData[Constants.PurchasingSettingsKey].ToString());

                _ = Purchasing.PurchasingPlayerPrefs.SetAlternateSettings(RemoteConfigPurchasingSettings);
            }
            catch (Exception e)
            {
                try
                {
                    _ = Purchasing.PurchasingPlayerPrefs.SetAlternateSettings(null);

                    try
                    {
                        Analytics.MyDebug.LogError($"Failed to parse remote config purchasing settings. Exception: {e.GetType().Name}: {e.Message}");
                    }
                    catch
                    {
                        Analytics.MyDebug.LogError($"Failed to log error for remote config purchasing settings. Exception: {e}");
                    }
                }
                catch
                {
                    Analytics.MyDebug.LogError($"Failed to set alternate purchasing settings to null. Response: {e} - {responseString}");
                }
            }
            _fetchSuccessState = true;
        }
    }
}