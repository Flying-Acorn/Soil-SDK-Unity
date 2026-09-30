using FlyingAcorn.Soil.Advertisement.Logic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FlyingAcorn.Soil.Advertisement.Tests
{
    /// <summary>The JSON exchanged with the native players (NativeAds/PROTOCOL.md).</summary>
    public class NativeAdProtocolTests
    {
        [TestCase("loaded", NativeAdEventType.Loaded)]
        [TestCase("loadFailed", NativeAdEventType.LoadFailed)]
        [TestCase("shown", NativeAdEventType.Shown)]
        [TestCase("showFailed", NativeAdEventType.ShowFailed)]
        [TestCase("clicked", NativeAdEventType.Clicked)]
        [TestCase("rewarded", NativeAdEventType.Rewarded)]
        [TestCase("closed", NativeAdEventType.Closed)]
        [TestCase("somethingNew", NativeAdEventType.Unknown)]
        public void EventNames(string name, NativeAdEventType expected)
        {
            var e = NativeAdEvent.Parse($"{{\"format\":\"rewarded\",\"event\":\"{name}\"}}");

            Assert.AreEqual(expected, e.Type);
            Assert.AreEqual("rewarded", e.Format);
        }

        [Test]
        public void LoadedEvent_CarriesMediaAndDuration()
        {
            var e = NativeAdEvent.Parse("{\"format\":\"interstitial\",\"event\":\"loaded\",\"media\":\"video\",\"durationMs\":15000}");

            Assert.AreEqual("video", e.Media);
            Assert.AreEqual(15000, e.DurationMs);
        }

        [Test]
        public void FailureEvent_CarriesErrorAndMessage()
        {
            var e = NativeAdEvent.Parse("{\"format\":\"banner\",\"event\":\"showFailed\",\"error\":\"no_host\",\"message\":\"no activity\"}");

            Assert.AreEqual(NativeAdErrors.NoHost, e.Error);
            Assert.AreEqual("no activity", e.Message);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[1,2]")]
        public void Garbage_IsIgnored(string json)
        {
            Assert.IsNull(NativeAdEvent.Parse(json));
        }

        [Test]
        public void MissingFields_AreTolerated()
        {
            var e = NativeAdEvent.Parse("{\"event\":\"loaded\",\"durationMs\":\"soon\"}");

            Assert.AreEqual(NativeAdEventType.Loaded, e.Type);
            Assert.IsNull(e.Format);
            Assert.AreEqual(0, e.DurationMs);
        }

        [Test]
        public void FullscreenDefaults_MatchTheProtocol()
        {
            var interstitial = JObject.Parse(FullscreenShowOptions.DefaultsFor(AdFormats.Interstitial).ToJson());
            Assert.AreEqual(5f, (float)interstitial["imageLockSeconds"]);
            Assert.AreEqual(0.8f, (float)interstitial["videoLockFraction"]);
            Assert.AreEqual(5f, (float)interstitial["minVideoLockSeconds"]);
            Assert.IsNull(interstitial["maxLockSeconds"], "no cap: the countdown is the safety net");
            Assert.AreEqual(false, (bool)interstitial["startMuted"]);

            var rewarded = JObject.Parse(FullscreenShowOptions.DefaultsFor(AdFormats.Rewarded).ToJson());
            Assert.AreEqual(20f, (float)rewarded["imageLockSeconds"]);
            Assert.AreEqual(1f, (float)rewarded["videoLockFraction"]);
            Assert.AreEqual(0f, (float)rewarded["minVideoLockSeconds"]);
        }

        [Test]
        public void BannerOptions_DefaultToBottom()
        {
            Assert.AreEqual("bottom", (string)JObject.Parse(BannerPositions.ToJson(null))["position"]);
            Assert.AreEqual("top", (string)JObject.Parse(BannerPositions.ToJson(BannerPositions.Top))["position"]);
        }

        [Test]
        public void Formats()
        {
            Assert.IsTrue(AdFormats.IsFullscreen("interstitial"));
            Assert.IsTrue(AdFormats.IsFullscreen("rewarded"));
            Assert.IsFalse(AdFormats.IsFullscreen("banner"));
            Assert.IsFalse(AdFormats.IsPlayerFormat("native"), "native ads are drawn by the game, not a player");
            Assert.IsTrue(AdFormats.IsPlayerFormat("banner"));
        }
    }
}
