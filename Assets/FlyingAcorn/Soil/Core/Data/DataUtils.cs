using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FlyingAcorn.Analytics;
using UnityEngine;
using Cysharp.Threading.Tasks;
using static FlyingAcorn.Soil.Core.Data.Constants;

namespace FlyingAcorn.Soil.Core.Data
{
    public static class DataUtils
    {
        private static BuildData.BuildData _buildSettings;

        internal static string GetScriptingBackend()
        {
            if (!_buildSettings)
                _buildSettings = Resources.Load<BuildData.BuildData>(BuildSettingsName);
            var scriptingBackend = "Unknown";
            if (_buildSettings && !string.IsNullOrEmpty(_buildSettings.ScriptingBackend))
                scriptingBackend = _buildSettings.ScriptingBackend;

            return scriptingBackend;
        }

        public static string GetUserBuildNumber()
        {
            if (!_buildSettings)
                _buildSettings = Resources.Load<BuildData.BuildData>(BuildSettingsName);
            var build = "Unknown";
            if (_buildSettings && !string.IsNullOrEmpty(_buildSettings.BuildNumber))
                build = _buildSettings.BuildNumber;

            return build;
        }
        
        public static DateTime GetBuildDate()
        {
            _buildSettings = Resources.Load<BuildData.BuildData>(BuildSettingsName);
            var buildTime = _buildSettings ? _buildSettings.LastBuildTime : null; // Format is "yyyy/MM/dd-HH:mm:ss"
            var buildDate = DateTime.TryParseExact(buildTime, "yyyy/MM/dd-HH:mm:ss", null,
                System.Globalization.DateTimeStyles.None, out var parsedDate)
                ? parsedDate
                : DateTime.MinValue;
            return buildDate;
        }

        public static Store GetStore()
        {
            if (!_buildSettings)
                _buildSettings = Resources.Load<BuildData.BuildData>(BuildSettingsName);
            var storeName = Store.Unknown;
            if (_buildSettings && _buildSettings.StoreName != Store.Unknown)
                storeName = _buildSettings.StoreName;
            if (storeName != Store.Unknown) return storeName;
            if (!Application.isEditor)
                MyDebug.LogError("Store name is not set in BuildData");
            return Store.Unknown;
        }

        public static IEnumerable<FieldInfo> GetAllFields(this Type t)
        {
            if (t == null)
                return Enumerable.Empty<FieldInfo>();

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Static | BindingFlags.Instance |
                                       BindingFlags.DeclaredOnly;
            return t.GetFields(flags).Concat(GetAllFields(t.BaseType));
        }

        public static RegionSettings GetSettingsForTimeZone()
        {
            var tz = System.TimeZoneInfo.Local;

            var utcOffset = tz.BaseUtcOffset;
            var standardName = tz.StandardName ?? string.Empty;

            var region = Region.WW;

            if (utcOffset == TimeSpan.FromHours(3.5) || utcOffset == TimeSpan.FromHours(4.5) ||
                standardName.Contains("Iran", StringComparison.OrdinalIgnoreCase) ||
                standardName.Contains("Tehran", StringComparison.OrdinalIgnoreCase))
            {
                region = Region.IR;
            }

            MyDebug.Verbose($"TimeZone - Offset: {utcOffset}, StandardName: {standardName}, Mapped Region: {region}");

            return APIPerRegion.Find(x => x.Region == region) ?? new RegionSettings
            {
                Region = region,
                ApiUrl = FallBackApiUrl
            };
        }

        internal static string FindApiUrl()
        {
            var store = GetStore();
            
            switch (store)
            {
                case Store.CafeBazaar:
                case Store.Myket:
                    return IRApiUrl;
                case Store.LandingPage:
                case Store.Unknown:
                case Store.BetaChannel:
                case Store.Postman:
                case Store.GooglePlay:
                case Store.AppStore:
                case Store.Github:
                default:
                    break;
            }

            var timezoneSettings = GetSettingsForTimeZone();
            
            if (SoilServices.UserInfo?.country == null)
            {
                MyDebug.Verbose($"No user info available, using timezone-based URL: {timezoneSettings.ApiUrl}");
                return timezoneSettings.ApiUrl ?? FallBackApiUrl;
            }
            
            var region = SoilServices.UserInfo.country;
            var regionEnum = Enum.TryParse(region, true, out Region regionParsed) ? regionParsed : Region.WW;
            var settingForCountry = APIPerRegion.Find(x => x.Region == regionEnum);
            
            if (settingForCountry == null || settingForCountry.Region == Region.WW)
            {
                MyDebug.Verbose($"User region is WW or not found ({regionEnum}), preferring timezone-based URL: {timezoneSettings.ApiUrl}");
                return timezoneSettings.ApiUrl ?? FallBackApiUrl;
            }
            
            MyDebug.Verbose($"Using country-specific API URL for region {regionEnum}: {settingForCountry.ApiUrl}");
            return settingForCountry.ApiUrl ?? FallBackApiUrl;
        }
        
        public static async UniTask ExecuteUnityWebRequestWithTimeout(UnityEngine.Networking.UnityWebRequest request, int timeoutSeconds)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (timeoutSeconds <= 0) timeoutSeconds = 1; // sanity clamp

            // Fast-path: already finished (rare but possible if using a cached result)
            if (request.isDone) return;

            var tcs = new UniTaskCompletionSource();
            var operation = request.SendWebRequest();

            // Unity guarantees AsyncOperation.completed on main thread; no need to marshal unless future changes.
            operation.completed += _ => tcs.TrySetResult();

            // Separate CTS to allow cancellation of the scheduled timeout when request wins.
            using var timeoutCts = new System.Threading.CancellationTokenSource();
            var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken: timeoutCts.Token);

            // UniTask.WhenAny with two tasks returns index (0 => request finished, 1 => timeout)
            int winner;
            try
            {
                winner = await UniTask.WhenAny(tcs.Task, timeoutTask);
            }
            catch (Exception ex)
            {
                // If something unexpected happened before completion (very rare), abort to free resources.
                if (!request.isDone)
                {
                    try { request.Abort(); } catch { /* ignore */ }
                }
                throw new SoilException($"Unexpected error waiting for request: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (winner == 1) // timeout branch
            {
                // Abort the underlying request; some platforms may still invoke completed later, but tcs already resolved or will be ignored.
                try { request.Abort(); } catch { /* ignore */ }
                throw new SoilException($"Request timed out (url: {request.url})", SoilExceptionErrorCode.Timeout);
            }

            // Cancel timeout so Delay task stops (avoids needless continuation work)
            timeoutCts.Cancel();

            // Ensure we're back on main thread if caller will touch Unity objects right after.
            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();
        }
    }
}