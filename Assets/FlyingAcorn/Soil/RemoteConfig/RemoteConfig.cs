using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using Cysharp.Threading.Tasks;
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
        [UsedImplicitly] public static Action OnSuccessfulFetch;
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

        private static bool _fetchSuccessState;

        private static Dictionary<string, object> _sessionExtraProperties = new();
        private static bool _fetching;

        private static string FetchUrl => $"{Core.Data.Constants.ApiUrl}/remoteconfig/";

        [UsedImplicitly]
        public static bool IsFetchedAndReady => _fetchSuccessState && RemoteConfigPlayerPrefs.CachedRemoteConfigData != null;

        [UsedImplicitly]
        public static bool IsFetching => _fetching;

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
                _fetchSuccessState = false;

                // Initialize AB Testing with cached data on fetch failure
                // This ensures users remain in their experiments even when using fallback/cached configs
                if (RemoteConfigPlayerPrefs.CachedRemoteConfigData != null && UserDefinedConfigs != null)
                {
                    try
                    {
                        ABTestHandler.InitializeAbTesting(UserDefinedConfigs);
                    }
                    catch (Exception ex)
                    {
                        Analytics.MyDebug.LogWarning($"Failed to initialize AB Testing with cached data: {ex.Message}");
                    }
                }
            }

            _fetching = false;
            OnServerAnswer?.Invoke(_fetchSuccessState);
            if (_fetchSuccessState)
                OnSuccessfulFetch?.Invoke();
        }

        private static Dictionary<string, object> GetPlayerProperties()
        {
            // Create a new dictionary to avoid mutating _sessionExtraProperties
            var properties = new Dictionary<string, object>();

            // Add dynamic system properties first (lowest priority)
            try
            {
                var systemProperties = Soil.Core.User.UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties();
                if (systemProperties != null)
                {
                    foreach (var property in systemProperties)
                    {
                        if (!string.IsNullOrEmpty(property.Key) && property.Value != null)
                        {
                            properties[property.Key] = property.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Analytics.MyDebug.LogWarning($"Failed to add system properties: {ex.Message}");
            }

            // Add user's custom properties from UpdatePlayerInfo (medium priority, can override system properties)
            if (SoilServices.UserInfo?.custom_properties != null)
            {
                foreach (var kvp in SoilServices.UserInfo.custom_properties)
                {
                    if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value != null)
                    {
                        properties[kvp.Key] = kvp.Value;
                    }
                }
            }

            // Add session-specific extra properties last (highest priority, can override everything)
            // These are passed at FetchConfig call time and represent the most recent/contextual data
            if (_sessionExtraProperties != null)
            {
                foreach (var kvp in _sessionExtraProperties)
                {
                    if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value != null)
                    {
                        properties[kvp.Key] = kvp.Value;
                    }
                }
            }

            return properties;
        }

        private static async UniTask FetchRemoteConfig()
        {
            if (!SoilServices.Ready)
            {
                throw new SoilException("Soil services are not ready", SoilExceptionErrorCode.NotReady);
            }

            var stringBody = JsonConvert.SerializeObject(new Dictionary<string, object>
                { { "properties", GetPlayerProperties() } });

            string responseString = null;
            using var request = new UnityWebRequest(FetchUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(stringBody)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);

            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout * 2);
                responseString = request.downloadHandler?.text;
            }
            catch (SoilException) { throw; }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while fetching remote config: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                throw new SoilException($"Server returned error {(System.Net.HttpStatusCode)request.responseCode}: {responseString}", SoilExceptionErrorCode.TransportError);
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
            _fetchSuccessState = true;
        }
    }
}