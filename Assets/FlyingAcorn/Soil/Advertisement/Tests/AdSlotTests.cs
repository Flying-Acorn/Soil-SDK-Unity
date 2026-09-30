using System.Collections.Generic;
using System.Linq;
using FlyingAcorn.Soil.Advertisement.Logic;
using NUnit.Framework;

namespace FlyingAcorn.Soil.Advertisement.Tests
{
    /// <summary>
    /// The Unity-side life cycle of banner, interstitial and rewarded ads against a scripted
    /// native player: every LoadAd is answered, fullscreen ads are consumed and prepared again,
    /// the rewarded cooldown holds Loaded back, and one fullscreen ad shows at a time.
    /// </summary>
    public class AdSlotTests
    {
        private sealed class FakePlayer : IAdPlayer
        {
            public readonly List<string> Calls = new();

            public void Load(string format, string creativeJson) => Calls.Add($"load:{format}");
            public void Show(string format, string optionsJson) => Calls.Add($"show:{format}");
            public void Hide(string format) => Calls.Add($"hide:{format}");
            public void Destroy(string format) => Calls.Add($"destroy:{format}");

            public int Count(string call) => Calls.Count(c => c == call);
        }

        private FakePlayer _player;
        private List<string> _notices;
        private bool _gated;
        private AdSlots _slots;

        private static readonly AdCreative Creative = new() { AdId = "ad", ImagePath = "/i.png" };

        [SetUp]
        public void SetUp()
        {
            _player = new FakePlayer();
            _notices = new List<string>();
            _gated = false;
            _slots = new AdSlots(_player, () => _gated);
            foreach (var slot in _slots.All)
            {
                var format = slot.Format;
                slot.Notice += (notice, error) =>
                    _notices.Add(error == null ? $"{format}:{notice}" : $"{format}:{notice}:{error}");
            }
        }

        private void Native(string format, NativeAdEventType type, string media = "image", string error = null)
        {
            _slots.HandleEvent(new NativeAdEvent { Format = format, Type = type, Media = media, Error = error });
        }

        private AdSlot Prepared(string format)
        {
            var slot = _slots[format];
            slot.SetCreative(Creative);
            Native(format, NativeAdEventType.Loaded);
            _notices.Clear();
            return slot;
        }

        [Test]
        public void Creative_IsSentToThePlayer_AndAnnouncedOnceLoaded()
        {
            var slot = _slots[AdFormats.Interstitial];

            slot.SetCreative(Creative);

            Assert.AreEqual(1, _player.Count("load:interstitial"));
            Assert.IsFalse(slot.IsReady);
            CollectionAssert.IsEmpty(_notices);

            Native(AdFormats.Interstitial, NativeAdEventType.Loaded, "video");

            Assert.IsTrue(slot.IsReady);
            Assert.AreEqual("video", slot.Media);
            CollectionAssert.AreEqual(new[] { "interstitial:Loaded" }, _notices);

            _slots.Tick();
            Assert.AreEqual(1, _notices.Count, "an unrequested preparation is announced once");
        }

        [Test]
        public void LoadAd_OnAPreparedSlot_IsAnsweredAtOnce()
        {
            var slot = Prepared(AdFormats.Banner);

            slot.RequestLoad();

            CollectionAssert.AreEqual(new[] { "banner:Loaded" }, _notices);
            Assert.AreEqual(1, _player.Count("load:banner"), "no second decode");
        }

        [Test]
        public void LoadAd_WhileAssetsAreCaching_IsAnsweredWhenThePlayerIsReady()
        {
            var slot = _slots[AdFormats.Rewarded];
            slot.BeginCaching();

            slot.RequestLoad();
            CollectionAssert.IsEmpty(_notices);

            slot.SetCreative(Creative);
            CollectionAssert.IsEmpty(_notices);

            Native(AdFormats.Rewarded, NativeAdEventType.Loaded);
            CollectionAssert.AreEqual(new[] { "rewarded:Loaded" }, _notices);
        }

        [Test]
        public void LoadAd_WhenTheServerHadNothing_FailsWithNoFill()
        {
            var slot = _slots[AdFormats.Interstitial];
            slot.BeginCaching();
            slot.RequestLoad();

            slot.SetCreative(null);

            CollectionAssert.AreEqual(new[] { "interstitial:LoadFailed:no_fill" }, _notices);
            Assert.AreEqual(1, _player.Count("destroy:interstitial"));

            _notices.Clear();
            slot.RequestLoad();
            CollectionAssert.AreEqual(new[] { "interstitial:LoadFailed:no_fill" }, _notices);
        }

        [Test]
        public void LoadAd_BeforeInitialization_FailsAsNotLoaded()
        {
            _slots[AdFormats.Banner].RequestLoad();

            CollectionAssert.AreEqual(new[] { "banner:LoadFailed:not_loaded" }, _notices);
        }

        [Test]
        public void CachingFailure_FailsPendingLoads()
        {
            var slot = _slots[AdFormats.Interstitial];
            slot.BeginCaching();
            slot.RequestLoad();

            slot.CachingFailed("network");

            CollectionAssert.AreEqual(new[] { "interstitial:LoadFailed:network" }, _notices);
        }

        [Test]
        public void CachingFailure_KeepsAnEarlierCreative()
        {
            var slot = Prepared(AdFormats.Interstitial);
            slot.BeginCaching();

            slot.CachingFailed("network");

            Assert.IsTrue(slot.IsReady);
            CollectionAssert.IsEmpty(_notices);
        }

        [Test]
        public void PlayerLoadFailure_IsReported()
        {
            var slot = _slots[AdFormats.Interstitial];
            slot.SetCreative(Creative);

            Native(AdFormats.Interstitial, NativeAdEventType.LoadFailed, error: NativeAdErrors.MediaUnreadable);

            Assert.IsFalse(slot.IsReady);
            CollectionAssert.AreEqual(new[] { "interstitial:LoadFailed:media_unreadable" }, _notices);
        }

        [Test]
        public void LoadAd_AfterAPlayerFailure_TriesAgain()
        {
            var slot = _slots[AdFormats.Interstitial];
            slot.SetCreative(Creative);
            Native(AdFormats.Interstitial, NativeAdEventType.LoadFailed, error: NativeAdErrors.MediaUnreadable);

            slot.RequestLoad();

            Assert.AreEqual(2, _player.Count("load:interstitial"));
        }

        [Test]
        public void StaleLoadedEvents_AreIgnored()
        {
            var slot = _slots[AdFormats.Interstitial];

            Native(AdFormats.Interstitial, NativeAdEventType.Loaded);

            Assert.IsFalse(slot.IsReady);
            CollectionAssert.IsEmpty(_notices);
        }

        [Test]
        public void Fullscreen_FullLifeCycle_ConsumesAndPreparesAgain()
        {
            var slot = Prepared(AdFormats.Rewarded);

            _slots.Show(AdFormats.Rewarded, "{}");
            Assert.AreEqual(1, _player.Count("show:rewarded"));
            Assert.IsFalse(slot.IsReady, "a fullscreen show consumes the ad");
            Assert.IsTrue(_slots.IsFullscreenShowing);

            Native(AdFormats.Rewarded, NativeAdEventType.Shown);
            Native(AdFormats.Rewarded, NativeAdEventType.Clicked);
            Native(AdFormats.Rewarded, NativeAdEventType.Rewarded);
            Native(AdFormats.Rewarded, NativeAdEventType.Closed);

            CollectionAssert.AreEqual(
                new[] { "rewarded:Shown", "rewarded:Clicked", "rewarded:Rewarded", "rewarded:Closed" }, _notices);
            Assert.IsFalse(_slots.IsFullscreenShowing);
            Assert.AreEqual(2, _player.Count("load:rewarded"), "the same creative is prepared again after closing");

            _notices.Clear();
            slot.RequestLoad();
            CollectionAssert.IsEmpty(_notices, "not answered until the player has decoded it again");
            Native(AdFormats.Rewarded, NativeAdEventType.Loaded);
            CollectionAssert.AreEqual(new[] { "rewarded:Loaded" }, _notices);
        }

        [Test]
        public void LoadAd_DuringAFullscreenShow_IsAnsweredAfterItCloses()
        {
            var slot = Prepared(AdFormats.Interstitial);
            _slots.Show(AdFormats.Interstitial, "{}");
            Native(AdFormats.Interstitial, NativeAdEventType.Shown);
            _notices.Clear();

            slot.RequestLoad();
            CollectionAssert.IsEmpty(_notices);

            Native(AdFormats.Interstitial, NativeAdEventType.Closed);
            Native(AdFormats.Interstitial, NativeAdEventType.Loaded);

            CollectionAssert.AreEqual(new[] { "interstitial:Closed", "interstitial:Loaded" }, _notices);
        }

        [Test]
        public void Show_WithoutALoadedAd_Fails()
        {
            _slots.Show(AdFormats.Interstitial, "{}");

            CollectionAssert.AreEqual(new[] { "interstitial:ShowFailed:not_loaded" }, _notices);
            Assert.AreEqual(0, _player.Count("show:interstitial"));
        }

        [Test]
        public void OnlyOneFullscreenAd_AtATime()
        {
            Prepared(AdFormats.Interstitial);
            Prepared(AdFormats.Rewarded);

            _slots.Show(AdFormats.Interstitial, "{}");
            _slots.Show(AdFormats.Rewarded, "{}");
            _slots.Show(AdFormats.Interstitial, "{}");

            CollectionAssert.AreEqual(new[]
            {
                "rewarded:ShowFailed:already_showing",
                "interstitial:ShowFailed:already_showing"
            }, _notices);
            Assert.IsTrue(_slots[AdFormats.Rewarded].IsReady, "the refused ad is still loaded");
            Assert.AreEqual(1, _player.Count("show:interstitial"));
            Assert.AreEqual(0, _player.Count("show:rewarded"));
        }

        [Test]
        public void BannerAndFullscreen_CanShowTogether()
        {
            Prepared(AdFormats.Banner);
            Prepared(AdFormats.Interstitial);

            _slots.Show(AdFormats.Banner, "{}");
            _slots.Show(AdFormats.Interstitial, "{}");

            CollectionAssert.IsEmpty(_notices);
            Assert.AreEqual(1, _player.Count("show:banner"));
            Assert.AreEqual(1, _player.Count("show:interstitial"));
        }

        [Test]
        public void NativeShowFailure_PreparesTheFullscreenAdAgain()
        {
            var slot = Prepared(AdFormats.Interstitial);
            _slots.Show(AdFormats.Interstitial, "{}");

            Native(AdFormats.Interstitial, NativeAdEventType.ShowFailed, error: NativeAdErrors.NoHost);

            CollectionAssert.AreEqual(new[] { "interstitial:ShowFailed:no_host" }, _notices);
            Assert.IsFalse(_slots.IsFullscreenShowing);
            Assert.AreEqual(2, _player.Count("load:interstitial"));
            Assert.IsTrue(slot.IsPreparing);
        }

        [Test]
        public void Banner_StaysLoadedAcrossShowAndHide()
        {
            var slot = Prepared(AdFormats.Banner);

            _slots.Show(AdFormats.Banner, "{}");
            Native(AdFormats.Banner, NativeAdEventType.Shown);
            slot.Hide();
            Native(AdFormats.Banner, NativeAdEventType.Closed);

            Assert.IsTrue(slot.IsReady);
            Assert.AreEqual(1, _player.Count("hide:banner"));
            Assert.AreEqual(1, _player.Count("load:banner"), "a banner is not consumed");

            _slots.Show(AdFormats.Banner, "{}");
            Assert.AreEqual(2, _player.Count("show:banner"));
        }

        [Test]
        public void NewCreative_DuringAFullscreenShow_WaitsForTheNextShow()
        {
            var slot = Prepared(AdFormats.Interstitial);
            _slots.Show(AdFormats.Interstitial, "{}");
            Native(AdFormats.Interstitial, NativeAdEventType.Shown);

            slot.SetCreative(new AdCreative { AdId = "new", ImagePath = "/n.png" });
            Native(AdFormats.Interstitial, NativeAdEventType.Loaded);

            Assert.IsFalse(slot.IsReady, "nothing is ready while the old ad is still on screen");
            Assert.AreEqual(0, _player.Count("hide:interstitial"), "the ad on screen is not disturbed");

            _notices.Clear();
            Native(AdFormats.Interstitial, NativeAdEventType.Closed);

            Assert.IsTrue(slot.IsReady);
            Assert.AreEqual(2, _player.Count("load:interstitial"), "already prepared, no extra load after close");
            CollectionAssert.AreEqual(new[] { "interstitial:Closed", "interstitial:Loaded" }, _notices);
        }

        [Test]
        public void NoFill_WhileABannerIsOnScreen_LeavesItThere()
        {
            var slot = Prepared(AdFormats.Banner);
            _slots.Show(AdFormats.Banner, "{}");

            slot.SetCreative(null);

            Assert.AreEqual(0, _player.Count("destroy:banner"));
            Assert.IsFalse(slot.IsReady);
        }

        [Test]
        public void Gate_HoldsBackReadinessAndLoaded()
        {
            _gated = true;
            var slot = _slots[AdFormats.Rewarded];
            slot.SetCreative(Creative);
            Native(AdFormats.Rewarded, NativeAdEventType.Loaded);
            slot.RequestLoad();

            Assert.IsFalse(slot.IsReady);
            CollectionAssert.IsEmpty(_notices);

            _slots.Tick();
            CollectionAssert.IsEmpty(_notices);

            _gated = false;
            _slots.Tick();
            _slots.Tick();

            Assert.IsTrue(slot.IsReady);
            CollectionAssert.AreEqual(new[] { "rewarded:Loaded" }, _notices);
        }

        [Test]
        public void Gate_OnlyAppliesToRewarded()
        {
            _gated = true;
            Assert.IsTrue(Prepared(AdFormats.Interstitial).IsReady);
            Assert.IsTrue(Prepared(AdFormats.Banner).IsReady);
        }

        [Test]
        public void EventsForUnknownFormats_AreIgnored()
        {
            _slots.HandleEvent(new NativeAdEvent { Format = "native", Type = NativeAdEventType.Loaded });
            _slots.HandleEvent(new NativeAdEvent { Format = null, Type = NativeAdEventType.Closed });
            _slots.HandleEvent(null);
            _slots.Show("native", "{}");

            CollectionAssert.IsEmpty(_notices);
            CollectionAssert.IsEmpty(_player.Calls);
        }
    }
}
