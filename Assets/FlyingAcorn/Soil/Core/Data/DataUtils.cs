using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FlyingAcorn.Analytics;
using Cysharp.Threading.Tasks;
using static FlyingAcorn.Soil.Core.Data.Constants;

namespace FlyingAcorn.Soil.Core.Data
{
    public static class DataUtils
    {
        private static string _cachedDomain;

        internal static string GetRegionalApiDomain()
        {
            if (_cachedDomain != null) return _cachedDomain;

            var x = new[] { 104, 116, 116, 112, 115, 58, 47, 47 };
            var y = new[] { 115, 111, 105, 108, 46, 102, 108, 121, 105, 110, 103, 97, 99, 111, 114, 110, 46, 105, 114 };
            var z = new char[x.Length + y.Length];

            for (int i = 0; i < x.Length; i++) z[i] = (char)x[i];
            for (int i = 0; i < y.Length; i++) z[x.Length + i] = (char)y[i];

            _cachedDomain = new string(z);
            return _cachedDomain;
        }

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

            // BaseUtcOffset is the zone's standard offset; +3:30 is the only zone the IR region uses.
            var utcOffset = tz.BaseUtcOffset;

            var region = utcOffset == TimeSpan.FromHours(3.5) ? Region.IR : Region.WW;

            MyDebug.Verbose($"TimeZone - Offset: {utcOffset}, Mapped Region: {region}");

            switch (region)
            {
                case Region.IR:
                    return new RegionSettings { Region = Region.IR, ApiUrl = IRApiUrl() };
                case Region.WW:
                default:
                    return new RegionSettings { Region = Region.WW, ApiUrl = FallBackApiUrl };
            }
        }

        internal static string FindApiUrl()
        {
            var store = Analytics.Utils.GetStore();

            switch (store)
            {
#if !UNITY_IOS || UNITY_EDITOR
                case Analytics.BuildData.Constants.Store.CafeBazaar:
                case Analytics.BuildData.Constants.Store.Myket:
                    return IRApiUrl();
#endif
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
            var settingForCountry = regionEnum switch
            {
                Region.IR => new RegionSettings { Region = Region.IR, ApiUrl = IRApiUrl() },
                Region.WW => new RegionSettings { Region = Region.WW, ApiUrl = FallBackApiUrl },
                _ => null
            };

            if (settingForCountry == null || settingForCountry.Region == Region.WW)
            {
                MyDebug.Verbose($"User region is WW or not found ({regionEnum}), preferring timezone-based URL: {timezoneSettings.ApiUrl}");
                return timezoneSettings.ApiUrl ?? FallBackApiUrl;
            }

            MyDebug.Verbose($"Using country-specific API URL for region {regionEnum}: {settingForCountry.ApiUrl}");
            return settingForCountry.ApiUrl ?? FallBackApiUrl;
        }

        public static UniTask ExecuteUnityWebRequestWithTimeout(UnityEngine.Networking.UnityWebRequest request, int timeoutSeconds)
        {
            return ExecuteUnityWebRequestWithTimeout(request, timeoutSeconds, false);
        }

        /// <param name="ignoreTimeScale">
        /// Count the timeout in unscaled time, so it still runs out while the game is paused with
        /// Time.timeScale = 0.
        /// </param>
        public static async UniTask ExecuteUnityWebRequestWithTimeout(UnityEngine.Networking.UnityWebRequest request, int timeoutSeconds, bool ignoreTimeScale)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (timeoutSeconds <= 0) timeoutSeconds = 1; // sanity clamp

            // Fast-path: already finished (rare but possible if using a cached result)
            if (request.isDone) return;

            // Capture the url now, while the native request is guaranteed alive. By the time the timeout
            // branch runs the request may already be disposed, and then `request.url` throws too - which
            // is how timeouts end up reported with no endpoint to identify them by.
            var requestUrl = "<unavailable>";
            try { requestUrl = request.url ?? "<null>"; } catch { /* native request may already be gone */ }

            var tcs = new UniTaskCompletionSource();
            var operation = request.SendWebRequest();

            // Unity guarantees AsyncOperation.completed on main thread; no need to marshal unless future changes.
            operation.completed += _ => tcs.TrySetResult();

            // Separate CTS to allow cancellation of the scheduled timeout when request wins.
            using var timeoutCts = new System.Threading.CancellationTokenSource();
            var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(timeoutSeconds), ignoreTimeScale, cancellationToken: timeoutCts.Token);

            // UniTask.WhenAny with two tasks returns index (0 => request finished, 1 => timeout)
            int winner;
            try
            {
                winner = await UniTask.WhenAny(tcs.Task, timeoutTask);
            }
            catch (Exception ex)
            {
                // If something unexpected happened before completion (very rare), abort to free resources.
                var isDoneSafely = false;
                try { isDoneSafely = request.isDone; } catch { /* native request may already be disposed */ }

                if (!isDoneSafely)
                {
                    try { request.Abort(); } catch { /* ignore */ }
                }
                throw new SoilException($"Unexpected error waiting for request: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (winner == 1) // timeout branch
            {
                // Abort the underlying request; some platforms may still invoke completed later, but tcs already resolved or will be ignored.
                try { request.Abort(); } catch { /* ignore */ }

                throw new SoilException($"Request timed out (url: {requestUrl})", SoilExceptionErrorCode.Timeout);
            }

            // Cancel timeout so Delay task stops (avoids needless continuation work)
            timeoutCts.Cancel();

            // If the server explicitly rejects the access token with BAD_TOKEN,
            // wipe it locally so the next API call triggers a silent refresh.
            // NOTE: responseCode (and downloadHandler) access the native UnityWebRequest binding,
            // which can be null if the request was disposed concurrently or aborted by the platform
            // before this continuation resumed on the next player-loop tick. Guard the entire block.
            try
            {
                if (request.responseCode == 401)
                {
                    var body = request.downloadHandler?.text;
                    if (!string.IsNullOrEmpty(body) && body.Contains("BAD_TOKEN"))
                    {
                        var tokenData = User.UserPlayerPrefs.TokenData;
                        if (tokenData != null)
                        {
                            tokenData.Access = "";
                            User.UserPlayerPrefs.TokenData = tokenData;
                            MyDebug.Info("Server rejected access token (BAD_TOKEN). Invalidated locally; next call will refresh.");
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Never let token-cleanup logic break the caller's error flow.
            }

            // Ensure we're back on main thread if caller will touch Unity objects right after.
            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();
        }
    }
}