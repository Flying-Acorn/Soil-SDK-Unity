using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FlyingAcorn.Analytics;
using FlyingAcorn.Analytics.BuildData;
using UnityEngine;
using Cysharp.Threading.Tasks;
using static FlyingAcorn.Soil.Core.Data.Constants;

namespace FlyingAcorn.Soil.Core.Data
{
    public static class DataUtils
    {
        internal static string GetScriptingBackend()
        {
            return Analytics.BuildData.BuildDataUtils.GetScriptingBackend();
        }

        public static string GetUserBuildNumber()
        {
            return Analytics.BuildData.BuildDataUtils.GetUserBuildNumber();
        }
        
        public static DateTime GetBuildDate()
        {
            return Analytics.BuildData.BuildDataUtils.GetBuildDate();
        }

        public static Analytics.BuildData.Constants.Store GetStore()
        {
            var store = AnalyticsPlayerPrefs.Store;
            if (store != Analytics.BuildData.Constants.Store.Unknown)
                return store;
            return Analytics.BuildData.BuildDataUtils.GetStore();
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
                case Analytics.BuildData.Constants.Store.CafeBazaar:
                case Analytics.BuildData.Constants.Store.Myket:
                    return IRApiUrl;
                case Analytics.BuildData.Constants.Store.LandingPage:
                case Analytics.BuildData.Constants.Store.Unknown:
                case Analytics.BuildData.Constants.Store.BetaChannel:
                case Analytics.BuildData.Constants.Store.Postman:
                case Analytics.BuildData.Constants.Store.GooglePlay:
                case Analytics.BuildData.Constants.Store.AppStore:
                case Analytics.BuildData.Constants.Store.Github:
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