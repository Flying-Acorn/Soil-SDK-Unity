using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlyingAcorn.Soil.Advertisement.Data;
using FlyingAcorn.Soil.Advertisement.Logic;
using UnityEngine;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.E2E
{
    /// <summary>
    /// End-to-end test of the ad players through the real Unity bridge: the native players on a
    /// device, or the Editor's simulated player in Play mode. Copied into the project only while
    /// run.sh uses it; never shipped.
    ///
    /// Every step logs one line "SOILADS-E2E ..." that the driver script reads:
    ///   STEP name              a scenario started
    ///   EXPECT_TAP format      a fullscreen ad is about to show; the driver taps its close button
    ///                          once unlocked (Android) - on iOS the runner closes it itself
    ///   PASS name / FAIL name  the scenario's verdict
    ///   DONE passed=N failed=M
    /// Fullscreen ads pause Unity, so events raised while one is up arrive, in order, right after
    /// it closes; the scenarios therefore check the order of events, not their timing.
    /// </summary>
    public class SoilAdsE2ERunner : MonoBehaviour
    {
        // The driver's screenshot can take seconds on a busy machine; keep the banner up meanwhile.
        private const float ScreenshotSeconds = 6;

        private readonly List<string> _events = new();
        private int _passed;
        private int _failed;
        private string _dir;

        private static void Log(string line)
        {
            Debug.Log($"SOILADS-E2E {line}");
#if UNITY_EDITOR
            // No driver in the Editor: the runner captures the Game view itself.
            if (line.StartsWith("SCREENSHOT ")) Screenshot(line.Substring("SCREENSHOT ".Length));
#endif
        }

        private void Awake()
        {
            Subscribe();
        }

        private IEnumerator Start()
        {
            yield return null;
            _dir = Path.Combine(Application.persistentDataPath, "SoilAdsE2E");
            Directory.CreateDirectory(_dir);
            foreach (var name in new[] { "video", "image", "logo", "broken" })
            {
                var bytes = Resources.Load<TextAsset>($"SoilAdsE2E/{name}").bytes;
                File.WriteAllBytes(Path.Combine(_dir, name + Extension(name)), bytes);
            }

            // Short locks so the run stays quick; the lock formula itself is unit-tested natively.
            Advertisement.FullscreenOptionsOverride = new FullscreenShowOptions
            {
                ImageLockSeconds = 2, VideoLockFraction = 1, MinVideoLockSeconds = 0
            };

            yield return Scenario("banner_image", BannerImage());
            yield return Scenario("banner_text", BannerText());
            yield return Scenario("load_broken_media", LoadBroken());
            yield return Scenario("load_video_falls_back_to_image", VideoFallsBackToImage());
            yield return Scenario("show_before_load", ShowBeforeLoad());
            yield return Scenario("interstitial_image", Fullscreen(AdFormat.interstitial, video: false));
            yield return Scenario("rewarded_video", Fullscreen(AdFormat.rewarded, video: true));
            yield return Scenario("rewarded_again_after_close", RewardedAgain());
#if UNITY_ANDROID && !UNITY_EDITOR
            // Real taps need the driver's UI automation, which only Android has here; the iOS
            // player's clicks are covered by its XCTests.
            yield return Scenario("banner_click", BannerClick());
            yield return Scenario("interstitial_click_then_close", InterstitialClick());
#endif
#if UNITY_EDITOR
            yield return Scenario("editor_clicks_are_not_counted", EditorClicksAreNotCounted());
            yield return Scenario("editor_blocks_game_input", EditorBlocksGameInput());
            yield return Scenario("editor_default_lock_times", EditorDefaultLockTimes());
#endif

            Log($"DONE passed={_passed} failed={_failed}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(_failed == 0 && _passed > 0 ? 0 : 1);
#endif
        }

        private static string Extension(string name) => name switch
        {
            "video" => ".mp4",
            "broken" => ".mp4",
            _ => ".png"
        };

        private string PathOf(string name) => Path.Combine(_dir, name + Extension(name));

        #region Scenarios

        private IEnumerator BannerImage()
        {
            Prepare(AdFormat.banner, new AdCreative
            {
                AdId = "banner-image", ImagePath = PathOf("image"), LogoPath = PathOf("logo"),
                Title = "Soil E2E", ClickUrl = "https://example.com/"
            });
            yield return Expect("banner:Loaded");
            Check(Advertisement.IsFormatReady(AdFormat.banner), "banner ready");

            Advertisement.ShowBanner(AdPosition.BottomCenter);
            yield return Expect("banner:Shown");
            Log("SCREENSHOT banner_bottom");
            yield return new WaitForSecondsRealtime(ScreenshotSeconds);

            Advertisement.ShowBanner(AdPosition.TopCenter);
            yield return new WaitForSecondsRealtime(2);
            Log("SCREENSHOT banner_top");
            yield return new WaitForSecondsRealtime(ScreenshotSeconds);
            Check(!_events.Contains("banner:Shown#2"), "moving a visible banner sends no second Shown");

            Advertisement.HideAd(AdFormat.banner);
            yield return Expect("banner:Closed");
            yield return Expect("banner:Loaded#2");
            Check(Advertisement.IsFormatReady(AdFormat.banner), "a closed banner is prepared again");
            Advertisement.BannerPosition = AdPosition.BottomCenter;
        }

        private IEnumerator BannerText()
        {
            Prepare(AdFormat.banner, new AdCreative
            {
                AdId = "banner-text", LogoPath = PathOf("logo"), Title = "استاد کلمات",
                Description = "هر روز ذهنت را تمرین بده", CallToAction = "نصب", ClickUrl = "https://example.com/"
            });
            yield return Expect("banner:Loaded");
            Advertisement.ShowAd(AdFormat.banner);
            yield return Expect("banner:Shown");
            yield return new WaitForSecondsRealtime(2);
            Log("SCREENSHOT banner_text_rtl");
            yield return new WaitForSecondsRealtime(ScreenshotSeconds);
            Advertisement.HideAd(AdFormat.banner);
            yield return Expect("banner:Closed");
        }

        private IEnumerator LoadBroken()
        {
            Prepare(AdFormat.interstitial, new AdCreative { AdId = "broken", ImagePath = PathOf("broken") });
            yield return Expect("interstitial:Error");
            Check(!Advertisement.IsFormatReady(AdFormat.interstitial), "a broken ad is not ready");
        }

        private IEnumerator VideoFallsBackToImage()
        {
            Prepare(AdFormat.interstitial, new AdCreative
            {
                AdId = "fallback", VideoPath = PathOf("broken"), ImagePath = PathOf("image")
            });
            yield return Expect("interstitial:Loaded");
        }

        private IEnumerator ShowBeforeLoad()
        {
            Advertisement.PrepareForTesting(AdFormat.rewarded, null);
            yield return null;
            _events.Clear();
            Advertisement.ShowAd(AdFormat.rewarded);
            yield return Expect("rewarded:Error");
        }

        private IEnumerator Fullscreen(AdFormat format, bool video)
        {
            Prepare(format, new AdCreative
            {
                AdId = $"{format}-{(video ? "video" : "image")}",
                VideoPath = video ? PathOf("video") : null,
                ImagePath = PathOf("image"),
                LogoPath = PathOf("logo"),
                Title = "Soil E2E " + format,
                Description = "Native player end-to-end test",
                CallToAction = "Open",
                ClickUrl = "https://example.com/"
            });
            yield return Expect($"{format}:Loaded");

            yield return ShowAndWaitForClose(format);

            var expected = format == AdFormat.rewarded
                ? new[] { "rewarded:Shown", "rewarded:Rewarded", "rewarded:Closed" }
                : new[] { $"{format}:Shown", $"{format}:Closed" };
            var got = _events.Where(e => expected.Contains(e)).ToList();
            Check(got.SequenceEqual(expected), $"events in order: {string.Join(",", got)}");
            Check(!Advertisement.IsFullscreenAdShowing, "nothing left on screen");
        }

        private IEnumerator RewardedAgain()
        {
            // The same creative is prepared again after closing; cooldown holds Loaded back.
            Advertisement.ResetRewardedAdCooldown();
            Advertisement.LoadAd(AdFormat.rewarded);
            yield return Expect("rewarded:Loaded");
            yield return ShowAndWaitForClose(AdFormat.rewarded);
            Check(_events.Contains("rewarded:Rewarded"), "rewarded again");
        }

        private IEnumerator BannerClick()
        {
            Prepare(AdFormat.banner, new AdCreative
            {
                AdId = "banner-click", ImagePath = PathOf("image"), ClickUrl = "https://example.com/"
            });
            yield return Expect("banner:Loaded");
            Advertisement.ShowAd(AdFormat.banner);
            yield return Expect("banner:Shown");
            Log("EXPECT_CLICK banner");
            yield return Expect("banner:Clicked", 60);
            Advertisement.HideAd(AdFormat.banner);
            yield return Expect("banner:Closed");
        }

        private IEnumerator InterstitialClick()
        {
            Prepare(AdFormat.interstitial, new AdCreative
            {
                AdId = "interstitial-click", ImagePath = PathOf("image"), Title = "Tap me",
                CallToAction = "Open", ClickUrl = "https://example.com/"
            });
            yield return Expect("interstitial:Loaded");
            Log("EXPECT_CLICK_THEN_TAP interstitial");
            Advertisement.ShowAd(AdFormat.interstitial);
            yield return Expect("interstitial:Closed", 120);
            var expected = new[] { "interstitial:Shown", "interstitial:Clicked", "interstitial:Closed" };
            var got = _events.Where(e => expected.Contains(e)).ToList();
            Check(got.SequenceEqual(expected), $"events in order: {string.Join(",", got)}");
        }

        #endregion

#if UNITY_EDITOR
        // ---- Editor: the simulated player's own buttons stand in for a finger.

        private static Player.EditorAdPlayer EditorPlayer => (Player.EditorAdPlayer)Advertisement.PlayerForTesting;

        private static string ScreenshotDir => Path.Combine(Path.GetDirectoryName(Application.dataPath),
            "NativeAds", "unity-e2e", "out", "editor");

        private static void Screenshot(string name)
        {
            Directory.CreateDirectory(ScreenshotDir);
            ScreenCapture.CaptureScreenshot(Path.Combine(ScreenshotDir, name + ".png"));
        }

        private IEnumerator WaitForUnlockThenClose(AdFormat format)
        {
            var until = Time.realtimeSinceStartup + 60;
            yield return new WaitForSecondsRealtime(1);
            Screenshot($"fullscreen_{format}_locked");
            Check(!EditorPlayer.PressClose() || EditorPlayer.IsFullscreenUnlocked, "close does nothing while locked");
            while (!EditorPlayer.IsFullscreenUnlocked && Time.realtimeSinceStartup < until) yield return null;
            yield return null;
            Screenshot($"fullscreen_{format}_unlocked");
            yield return null;
            Check(EditorPlayer.PressClose(), "close works once unlocked");
        }

        private IEnumerator EditorClicksAreNotCounted()
        {
            var notices = new List<string>();
            void Watch(string message, string stack, LogType type)
            {
                if (message.StartsWith(Player.AdLinks.EditorNotice)) notices.Add(message);
            }
            Application.logMessageReceived += Watch;
            try
            {
                Prepare(AdFormat.banner, new AdCreative
                {
                    AdId = "banner-click", ImagePath = PathOf("image"), ClickUrl = "https://example.com/banner"
                });
                yield return Expect("banner:Loaded");
                Advertisement.ShowAd(AdFormat.banner);
                yield return Expect("banner:Shown");
                Check(EditorPlayer.PressBanner(), "banner clickable");
                yield return Expect("banner:Clicked");
                Advertisement.HideAd(AdFormat.banner);
                yield return Expect("banner:Closed");

                Prepare(AdFormat.interstitial, new AdCreative
                {
                    AdId = "cta-click", ImagePath = PathOf("image"), Title = "Tap me", CallToAction = "Open",
                    ClickUrl = "https://example.com/cta"
                });
                yield return Expect("interstitial:Loaded");
                Advertisement.ShowAd(AdFormat.interstitial);
                yield return Expect("interstitial:Shown");
                Check(EditorPlayer.PressCallToAction(), "call to action clickable");
                yield return Expect("interstitial:Clicked");
                yield return WaitForUnlockThenClose(AdFormat.interstitial);
                yield return Expect("interstitial:Closed");
            }
            finally
            {
                Application.logMessageReceived -= Watch;
            }

            Check(notices.Count == 2 && notices.Any(n => n.EndsWith("/banner")) && notices.Any(n => n.EndsWith("/cta")),
                $"both clicks were logged, not sent ({notices.Count} notices)");
        }

        private IEnumerator EditorBlocksGameInput()
        {
            var system = new GameObject("GameEventSystem").AddComponent<UnityEngine.EventSystems.EventSystem>();
            Prepare(AdFormat.interstitial, new AdCreative { AdId = "input", ImagePath = PathOf("image") });
            yield return Expect("interstitial:Loaded");
            Advertisement.ShowAd(AdFormat.interstitial);
            yield return Expect("interstitial:Shown");
            Check(!system.enabled, "the game's UI gets no clicks under a fullscreen ad");
            yield return WaitForUnlockThenClose(AdFormat.interstitial);
            yield return Expect("interstitial:Closed");
            Check(system.enabled, "the game's UI is back after the ad");
            Destroy(system.gameObject);
        }

        private IEnumerator EditorDefaultLockTimes()
        {
            // The real defaults: interstitial image 5 s; rewarded video (simulated 6 s) at its end.
            var saved = Advertisement.FullscreenOptionsOverride;
            Advertisement.FullscreenOptionsOverride = null;
            try
            {
                foreach (var (format, video, expected) in new[]
                         { (AdFormat.interstitial, false, 5f), (AdFormat.rewarded, true, 6f) })
                {
                    Advertisement.ResetRewardedAdCooldown();
                    Prepare(format, new AdCreative
                    {
                        AdId = $"default-{format}", ImagePath = PathOf("image"), VideoPath = video ? PathOf("video") : null
                    });
                    yield return Expect($"{format}:Loaded");
                    var shownAt = Time.realtimeSinceStartup;
                    Advertisement.ShowAd(format);
                    while (!EditorPlayer.IsFullscreenUnlocked && Time.realtimeSinceStartup - shownAt < 30) yield return null;
                    var took = Time.realtimeSinceStartup - shownAt;
                    Log($"LOCK {format} unlocked after {took:0.00} s (expected {expected} s)");
                    Check(Mathf.Abs(took - expected) < 0.75f, $"{format} unlocked after {took:0.00} s, expected {expected} s");
                    if (format == AdFormat.rewarded)
                    {
                        yield return Expect("rewarded:Rewarded");
                        Check(!_events.Contains("rewarded:Closed"), "reward comes before close");
                    }
                    Check(EditorPlayer.PressClose(), "close works once unlocked");
                    yield return Expect($"{format}:Closed");
                }
            }
            finally
            {
                Advertisement.FullscreenOptionsOverride = saved;
            }
        }
#endif

        private IEnumerator ShowAndWaitForClose(AdFormat format)
        {
            Log($"EXPECT_TAP {format}");
#if UNITY_EDITOR
            Advertisement.ShowAd(format);
            yield return WaitForUnlockThenClose(format);
            yield return Expect($"{format}:Closed", 30);
#elif UNITY_IOS
            // Unity is paused while the ad is up, so the close comes from a background thread
            // straight to the player once the (short) lock has passed.
            var player = Advertisement.PlayerForTesting;
            var timer = new System.Threading.Timer(_ => player.Hide(format.ToString()), null, 12000, -1);
            Advertisement.ShowAd(format);
            yield return Expect($"{format}:Closed", 90);
            timer.Dispose();
#else
            Advertisement.ShowAd(format);
            yield return Expect($"{format}:Closed", 90);
#endif
        }

        private void Prepare(AdFormat format, AdCreative creative)
        {
            _events.Clear();
            Advertisement.PrepareForTesting(format, creative);
        }

        private IEnumerator Scenario(string name, IEnumerator body)
        {
            Log($"STEP {name}");
            _current = name;
            _currentFailed = false;
            _events.Clear();
            yield return body;
            if (_currentFailed) _failed++;
            else
            {
                _passed++;
                Log($"PASS {name}");
            }
        }

        private string _current;
        private bool _currentFailed;

        private void Check(bool ok, string what)
        {
            if (ok) return;
            _currentFailed = true;
            Log($"FAIL {_current}: {what} (events: {string.Join(",", _events)})");
        }

        private IEnumerator Expect(string evt, float timeoutSeconds = 15)
        {
            // Only time the game runs counts: a click can leave the app for a browser (and its
            // first-run screens) for longer than the timeout, and the answer arrives on return.
            var waited = 0f;
            while (!_events.Contains(evt) && waited < timeoutSeconds)
            {
                yield return null;
                waited += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            }
            Check(_events.Contains(evt), $"expected {evt}");
        }

        private void Record(string evt, AdEventData data = null)
        {
            // Numbered repeats let a scenario assert something happened only once.
            var key = _events.Contains(evt) ? $"{evt}#{_events.Count(e => e.StartsWith(evt))+1}" : evt;
            _events.Add(key);
            Log($"EVENT {key}{(data != null && data.AdError != AdError.None ? " " + data.AdError : "")}");
        }

        private void Subscribe()
        {
            Events.OnBannerAdLoaded += d => Record("banner:Loaded", d);
            Events.OnBannerAdError += d => Record("banner:Error", d);
            Events.OnBannerAdShown += d => Record("banner:Shown", d);
            Events.OnBannerAdClosed += d => Record("banner:Closed", d);
            Events.OnBannerAdClicked += d => Record("banner:Clicked", d);
            Events.OnInterstitialAdLoaded += d => Record("interstitial:Loaded", d);
            Events.OnInterstitialAdError += d => Record("interstitial:Error", d);
            Events.OnInterstitialAdShown += d => Record("interstitial:Shown", d);
            Events.OnInterstitialAdClosed += d => Record("interstitial:Closed", d);
            Events.OnInterstitialAdClicked += d => Record("interstitial:Clicked", d);
            Events.OnRewardedAdLoaded += d => Record("rewarded:Loaded", d);
            Events.OnRewardedAdError += d => Record("rewarded:Error", d);
            Events.OnRewardedAdShown += d => Record("rewarded:Shown", d);
            Events.OnRewardedAdClosed += d => Record("rewarded:Closed", d);
            Events.OnRewardedAdClicked += d => Record("rewarded:Clicked", d);
            Events.OnRewardedAdRewarded += d => Record("rewarded:Rewarded", d);
        }
    }
}
