using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Advertisement.Data;
using FlyingAcorn.Soil.Advertisement.Models;
using FlyingAcorn.Soil.Core;
using UnityEngine;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.RotationE2E
{
    /// <summary>
    /// End-to-end test of fullscreen ad rotation against a real Soil server (run.sh points the
    /// build at a local one). Copied into the project only while run.sh uses it; never shipped.
    ///
    /// Shows ads round after round, the way a game does (LoadAd, wait for Loaded, ShowAd), and
    /// logs one line "SOILROT ..." per step for drive.py. The launch intent's "scenario" extra
    /// picks the rounds: "rotation" (default) alternates rewarded and interstitial ads for 6
    /// rounds; "interstitial" shows 8 interstitials back to back.
    ///   SHOWING format round ad group match click   about to show; the driver closes it once unlocked
    ///   CLOSED format round events                  the events the show raised, in order
    ///   FILES format count                          cache files of the format a few seconds later
    ///   FAIL reason / DONE shown=N
    /// "match" says whether the ad being shown belongs to the ad group the cache holds for the
    /// format - an ad built from one ad group's image and another's click link would be false.
    /// </summary>
    public class SoilRotationE2ERunner : MonoBehaviour
    {
        private string _scenario = "rotation";
        private const float LoadTimeoutSeconds = 240;
        private const float ShowTimeoutSeconds = 240;
        private const float SettleSeconds = 4;

        private readonly List<string> _events = new();
        private bool _initialized;
        private string _initFailure;
        private AdEventData _loaded;
        private AdEventData _error;
        private bool _closed;
        private int _shown;
        // The format the current round waits on; the other format's events are only recorded.
        private AdFormat _current;

        private static void Log(string line) => Debug.Log($"SOILROT {line}");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            MyDebug.SetLogLevel(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.DebugSeverity);
            var go = new GameObject("SoilRotationE2E");
            DontDestroyOnLoad(go);
            go.AddComponent<SoilRotationE2ERunner>();
        }

        private void Subscribe(AdFormat format)
        {
            // Only events of the format the round waits on count; both formats' slots raise events
            // on their own (e.g. the other one's Loaded after it prepares again).
            bool Current() => _current == format;
            void Record(string name)
            {
                if (Current()) _events.Add(name);
            }

            if (format == AdFormat.interstitial)
            {
                Events.OnInterstitialAdLoaded += d => { if (Current()) _loaded = d; Record("Loaded"); };
                Events.OnInterstitialAdError += d => { if (Current()) _error = d; Record($"Error:{d.AdError}"); };
                Events.OnInterstitialAdShown += d => Record("Shown");
                Events.OnInterstitialAdClosed += d => { if (Current()) _closed = true; Record("Closed"); };
            }
            else
            {
                Events.OnRewardedAdLoaded += d => { if (Current()) _loaded = d; Record("Loaded"); };
                Events.OnRewardedAdError += d => { if (Current()) _error = d; Record($"Error:{d.AdError}"); };
                Events.OnRewardedAdShown += d => Record("Shown");
                Events.OnRewardedAdRewarded += d => Record("Rewarded");
                Events.OnRewardedAdClosed += d => { if (Current()) _closed = true; Record("Closed"); };
            }
        }

        private IEnumerator Start()
        {
            _scenario = LaunchScenario() ?? "rotation";
            Log($"START scenario={_scenario}");
            Application.logMessageReceived += (condition, stack, type) =>
            {
                if (type == LogType.Exception) Log($"EXCEPTION {condition.Replace('\n', ' ')}");
            };

            SoilServices.InitializeAsync();
            var deadline = Time.realtimeSinceStartup + 120;
            while (!SoilServices.Ready && Time.realtimeSinceStartup < deadline) yield return null;
            if (!SoilServices.Ready)
            {
                Log("FAIL soil-not-ready");
                yield break;
            }

            Events.OnInitialized += () => _initialized = true;
            Events.OnInitializeFailed += reason => _initFailure = reason;
            Subscribe(AdFormat.interstitial);
            Subscribe(AdFormat.rewarded);
            Advertisement.InitializeAsync(new List<AdFormat> { AdFormat.interstitial, AdFormat.rewarded });

            deadline = Time.realtimeSinceStartup + 120;
            while (!_initialized && _initFailure == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (!_initialized)
            {
                Log($"FAIL ads-not-initialized {_initFailure}");
                yield break;
            }

            if (_scenario == "interstitial")
            {
                for (var round = 1; round <= 8; round++)
                    yield return ShowOnce(AdFormat.interstitial, round);
            }
            else
            {
                for (var round = 1; round <= 6; round++)
                {
                    yield return ShowOnce(AdFormat.rewarded, round);
                    yield return ShowOnce(AdFormat.interstitial, round);
                }
            }

            Log($"DONE shown={_shown}");
        }

        private IEnumerator ShowOnce(AdFormat format, int round)
        {
            _current = format;
            _events.Clear();
            _loaded = null;
            _error = null;
            _closed = false;

            Advertisement.LoadAd(format);
            var deadline = Time.realtimeSinceStartup + LoadTimeoutSeconds;
            while (_loaded == null && _error == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (_loaded == null)
            {
                Log($"FAIL load {format} round={round} error={_error?.AdError} events={string.Join(",", _events)}");
                yield break;
            }

            var group = AdvertisementPlayerPrefs.CachedAdGroups.TryGetValue(format, out var g) ? g : null;
            var adId = _loaded.ad?.id;
            var mismatch = Mismatch(_loaded.ad, group);
            Log($"SHOWING {format} round={round} ad={adId} group={group?.id} name={group?.name} " +
                $"match={mismatch == null} why={mismatch ?? "-"} click={group?.click_url}");

            _events.Clear();
            Advertisement.ShowAd(format);
            deadline = Time.realtimeSinceStartup + ShowTimeoutSeconds;
            while (!_closed && _error == null && Time.realtimeSinceStartup < deadline) yield return null;
            Log($"CLOSED {format} round={round} events={string.Join(",", _events)}");
            if (_closed) _shown++;

            yield return new WaitForSecondsRealtime(SettleSeconds);
            Log($"FILES {format} {CountFiles(format)}");
        }

        /// <summary>
        /// Null when every visible part of the prepared ad belongs to the ad group the cache holds:
        /// the texts of the ad whose media is on screen (the video's ad, else the image's), the
        /// group's image and logo assets, and the group's click link. Otherwise what is off.
        /// </summary>
        private static string Mismatch(Ad shown, AdGroup group)
        {
            if (shown == null) return "no-ad";
            if (group == null) return "no-cached-group";
            var ads = (group.video_ads ?? new List<Ad>()).Concat(group.image_ads ?? new List<Ad>()).ToList();
            if (ads.All(a => a.id != shown.id)) return "ad-not-in-group";
            if ((shown.action_button?.url ?? "") != (group.click_url ?? "")) return "click-link";

            var owner = shown.main_video?.id != null
                ? ads.FirstOrDefault(a => a.main_video?.id == shown.main_video.id)
                : ads.FirstOrDefault(a => a.main_image?.id == shown.main_image?.id);
            if (owner == null) return "media-not-in-group";
            if (Text(shown.main_header) != Text(owner.main_header)) return "title";
            if (Text(shown.description) != Text(owner.description)) return "description";
            if (Text(shown.action_button) != Text(owner.action_button)) return "call-to-action";
            if (shown.main_image?.id != null && ads.All(a => a.main_image?.id != shown.main_image.id)) return "image";
            if (shown.logo?.id != null && ads.All(a => a.logo?.id != shown.logo.id)) return "logo";
            return null;
        }

        private static string Text(Asset asset) => string.IsNullOrWhiteSpace(asset?.text_content) ? "" : asset.text_content;

        private static string LaunchScenario()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity.Call<AndroidJavaObject>("getIntent");
                return intent.Call<string>("getStringExtra", "scenario");
            }
            catch (Exception)
            {
                return null;
            }
#else
            return null;
#endif
        }

        private static int CountFiles(AdFormat format)
        {
            var dir = Path.Combine(Application.persistentDataPath, "SoilAssets");
            if (!Directory.Exists(dir)) return 0;
            return Directory.GetFiles(dir).Count(f => Path.GetFileName(f).StartsWith(format + "_", StringComparison.Ordinal));
        }
    }
}
