using System;
using System.Collections.Generic;
using System.Linq;
using FlyingAcorn.Soil.Advertisement.Logic;
using NUnit.Framework;

namespace FlyingAcorn.Soil.Advertisement.Tests
{
    /// <summary>
    /// What keeps a slot working when things go wrong around it: a game handler that throws, a
    /// player that never answers a load or a show, and answers that arrive too late.
    /// </summary>
    public class AdSlotRobustnessTests
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
        private List<Exception> _handlerErrors;
        private double _now;
        private AdSlots _slots;

        private static readonly AdCreative Creative = new() { AdId = "ad", ImagePath = "/i.png" };

        [SetUp]
        public void SetUp()
        {
            _player = new FakePlayer();
            _notices = new List<string>();
            _handlerErrors = new List<Exception>();
            _now = 0;
            _slots = new AdSlots(_player, null, () => _now, _handlerErrors.Add);
            foreach (var slot in _slots.All)
            {
                var format = slot.Format;
                slot.Notice += (notice, error) =>
                    _notices.Add(error == null ? $"{format}:{notice}" : $"{format}:{notice}:{error}");
            }
        }

        private void Native(string format, NativeAdEventType type, string error = null)
        {
            _slots.HandleEvent(new NativeAdEvent { Format = format, Type = type, Media = "image", Error = error });
        }

        private AdSlot Prepared(string format)
        {
            var slot = _slots[format];
            slot.SetCreative(Creative);
            Native(format, NativeAdEventType.Loaded);
            _notices.Clear();
            return slot;
        }

        private void Elapse(double seconds)
        {
            _now += seconds;
            _slots.Tick();
        }

        // --- Load requests for an ad that is gone ---

        [Test]
        public void LoadAd_WhileABannerWhoseAdWasRemovedIsOnScreen_IsAnsweredAtOnce()
        {
            var slot = Prepared(AdFormats.Banner);
            _slots.Show(AdFormats.Banner, "{}");
            Native(AdFormats.Banner, NativeAdEventType.Shown);
            slot.SetCreative(null); // the cache lost the banner's files
            _notices.Clear();

            slot.RequestLoad();

            CollectionAssert.AreEqual(new[] { "banner:LoadFailed:no_fill" }, _notices);
        }

        [Test]
        public void LoadAd_WaitingForAFullscreenShow_IsAnsweredWhenItsAdIsRemoved()
        {
            var slot = Prepared(AdFormats.Interstitial);
            _slots.Show(AdFormats.Interstitial, "{}");
            Native(AdFormats.Interstitial, NativeAdEventType.Shown);
            slot.RequestLoad(); // waits for the show to end
            _notices.Clear();

            slot.SetCreative(null);
            Native(AdFormats.Interstitial, NativeAdEventType.Closed);
            slot.RequestLoad();

            CollectionAssert.AreEqual(new[]
            {
                "interstitial:LoadFailed:no_fill", "interstitial:Closed", "interstitial:LoadFailed:no_fill"
            }, _notices);
        }

        [Test]
        public void LoadAd_WaitingForAFullscreenShow_StillLoadsTheNextAdWhenThereIsOne()
        {
            var slot = Prepared(AdFormats.Interstitial);
            _slots.Show(AdFormats.Interstitial, "{}");
            Native(AdFormats.Interstitial, NativeAdEventType.Shown);
            slot.RequestLoad();
            _notices.Clear();

            Native(AdFormats.Interstitial, NativeAdEventType.Closed);
            Native(AdFormats.Interstitial, NativeAdEventType.Loaded);

            CollectionAssert.AreEqual(new[] { "interstitial:Closed", "interstitial:Loaded" }, _notices);
        }

        // --- Throwing handlers ---

        [Test]
        public void ThrowingHandler_OnClosed_StillPreparesTheAdAgain()
        {
            var slot = Prepared(AdFormats.Interstitial);
            slot.Notice += (_, _) => throw new InvalidOperationException("game bug");
            _slots.Show(AdFormats.Interstitial, "{}");

            Native(AdFormats.Interstitial, NativeAdEventType.Shown);
            Native(AdFormats.Interstitial, NativeAdEventType.Closed);

            Assert.IsFalse(_slots.IsFullscreenShowing);
            Assert.AreEqual(2, _player.Count("load:interstitial"), "prepared again despite the throwing handler");
            Assert.AreEqual(2, _handlerErrors.Count, "each throw is reported");

            Native(AdFormats.Interstitial, NativeAdEventType.Loaded);
            Assert.IsTrue(slot.IsReady);
        }

        [Test]
        public void ThrowingHandler_DoesNotSilenceTheOtherHandlers()
        {
            var slot = _slots[AdFormats.Banner];
            var later = new List<AdSlotNotice>();
            // Subscribed before and after the throwing one.
            slot.Notice += (_, _) => throw new Exception("first");
            slot.Notice += (notice, _) => later.Add(notice);

            slot.SetCreative(Creative);
            Native(AdFormats.Banner, NativeAdEventType.Loaded);

            CollectionAssert.AreEqual(new[] { "banner:Loaded" }, _notices);
            CollectionAssert.AreEqual(new[] { AdSlotNotice.Loaded }, later);
            Assert.AreEqual(1, _handlerErrors.Count);
        }

        [Test]
        public void ThrowingHandler_OnShowFailed_StillPreparesTheAdAgain()
        {
            var slot = Prepared(AdFormats.Rewarded);
            slot.Notice += (_, _) => throw new Exception("boom");
            _slots.Show(AdFormats.Rewarded, "{}");

            Native(AdFormats.Rewarded, NativeAdEventType.ShowFailed, NativeAdErrors.NoHost);

            Assert.IsFalse(slot.IsShowing);
            Assert.IsTrue(slot.IsPreparing);
            Assert.AreEqual(2, _player.Count("load:rewarded"));
        }

        [Test]
        public void ThrowingErrorReporter_DoesNotBreakTheSlot()
        {
            var slots = new AdSlots(_player, null, () => _now, _ => throw new Exception("reporter"));
            var slot = slots[AdFormats.Banner];
            slot.Notice += (_, _) => throw new Exception("handler");

            slot.SetCreative(Creative);
            Assert.DoesNotThrow(() =>
                slots.HandleEvent(new NativeAdEvent { Format = AdFormats.Banner, Type = NativeAdEventType.Loaded }));
            Assert.IsTrue(slot.IsReady);
        }

        // --- Prepare watchdog ---

        [Test]
        public void UnansweredLoad_FailsWithTimeout()
        {
            var slot = _slots[AdFormats.Interstitial];
            slot.SetCreative(Creative);
            slot.RequestLoad();

            Elapse(AdSlot.PrepareTimeoutSeconds - 1);
            Assert.IsTrue(slot.IsPreparing);
            CollectionAssert.IsEmpty(_notices);

            Elapse(1);

            Assert.IsFalse(slot.IsPreparing);
            Assert.IsFalse(slot.IsReady);
            CollectionAssert.AreEqual(new[] { "interstitial:LoadFailed:timeout" }, _notices);
            Assert.AreEqual(1, _player.Count("destroy:interstitial"), "the hung decode is cancelled");

            Elapse(AdSlot.PrepareTimeoutSeconds * 2);
            Assert.AreEqual(1, _notices.Count, "reported once");
        }

        [Test]
        public void LateLoaded_AfterTheTimeout_IsIgnored()
        {
            var slot = _slots[AdFormats.Rewarded];
            slot.SetCreative(Creative);
            Elapse(AdSlot.PrepareTimeoutSeconds);
            _notices.Clear();

            Native(AdFormats.Rewarded, NativeAdEventType.Loaded);
            Native(AdFormats.Rewarded, NativeAdEventType.LoadFailed, NativeAdErrors.MediaUnreadable);

            Assert.IsFalse(slot.IsReady);
            CollectionAssert.IsEmpty(_notices);
        }

        [Test]
        public void LoadAd_AfterATimeout_PreparesAgain_WithAFreshWatchdog()
        {
            var slot = _slots[AdFormats.Interstitial];
            slot.SetCreative(Creative);
            Elapse(AdSlot.PrepareTimeoutSeconds);
            _notices.Clear();

            slot.RequestLoad();
            Assert.AreEqual(2, _player.Count("load:interstitial"));

            Elapse(AdSlot.PrepareTimeoutSeconds / 2);
            Native(AdFormats.Interstitial, NativeAdEventType.Loaded);

            Assert.IsTrue(slot.IsReady);
            CollectionAssert.AreEqual(new[] { "interstitial:Loaded" }, _notices);
        }

        [Test]
        public void TimedOutLoad_DuringAFullscreenShow_LeavesTheAdOnScreen()
        {
            var slot = Prepared(AdFormats.Interstitial);
            _slots.Show(AdFormats.Interstitial, "{}");
            Native(AdFormats.Interstitial, NativeAdEventType.Shown);
            slot.SetCreative(new AdCreative { AdId = "new", ImagePath = "/n.png" });

            Elapse(AdSlot.PrepareTimeoutSeconds);

            Assert.AreEqual(0, _player.Count("destroy:interstitial"));
            Assert.AreEqual(0, _player.Count("hide:interstitial"));
            Assert.IsTrue(slot.IsShowing);
        }

        [Test]
        public void AnsweredLoad_IsNeverTimedOut()
        {
            var slot = Prepared(AdFormats.Banner);

            Elapse(AdSlot.PrepareTimeoutSeconds * 3);

            Assert.IsTrue(slot.IsReady);
            CollectionAssert.IsEmpty(_notices);
        }

        // --- Show watchdog ---

        [Test]
        public void UnansweredFullscreenShow_FailsWithTimeout_AndFreesTheScreen()
        {
            var slot = Prepared(AdFormats.Rewarded);
            _slots.Show(AdFormats.Rewarded, "{}");

            Elapse(AdSlot.ShowAnswerTimeoutSeconds - 1);
            Assert.IsTrue(_slots.IsFullscreenShowing);

            Elapse(1);

            Assert.IsFalse(_slots.IsFullscreenShowing);
            Assert.IsFalse(slot.IsShowing);
            CollectionAssert.AreEqual(new[] { "rewarded:ShowFailed:timeout" }, _notices);
            Assert.AreEqual(1, _player.Count("hide:rewarded"));
            Assert.AreEqual(2, _player.Count("load:rewarded"), "the ad is prepared again");
        }

        [Test]
        public void FullscreenShow_ThatAppeared_IsNeverTimedOut()
        {
            var slot = Prepared(AdFormats.Interstitial);
            _slots.Show(AdFormats.Interstitial, "{}");
            Native(AdFormats.Interstitial, NativeAdEventType.Shown);

            Elapse(AdSlot.ShowAnswerTimeoutSeconds * 10);

            Assert.IsTrue(slot.IsShowing);
            Assert.AreEqual(0, _player.Count("hide:interstitial"));
            CollectionAssert.AreEqual(new[] { "interstitial:Shown" }, _notices);
        }

        [Test]
        public void Banner_IsNeverShowTimedOut()
        {
            var slot = Prepared(AdFormats.Banner);
            _slots.Show(AdFormats.Banner, "{}");

            Elapse(AdSlot.ShowAnswerTimeoutSeconds * 2);

            Assert.IsTrue(slot.IsShowing);
            CollectionAssert.IsEmpty(_notices);
        }

        [Test]
        public void LateAnswers_ToAnAbandonedShow_AreSwallowed_AndALateAdIsTakenDown()
        {
            var slot = Prepared(AdFormats.Rewarded);
            _slots.Show(AdFormats.Rewarded, "{}");
            Elapse(AdSlot.ShowAnswerTimeoutSeconds);
            _notices.Clear();

            Native(AdFormats.Rewarded, NativeAdEventType.Shown);
            Assert.AreEqual(2, _player.Count("hide:rewarded"), "an ad that appeared after all is dismissed");
            Native(AdFormats.Rewarded, NativeAdEventType.Rewarded);
            Native(AdFormats.Rewarded, NativeAdEventType.Closed);

            CollectionAssert.IsEmpty(_notices);
            Assert.IsFalse(_slots.IsFullscreenShowing);

            // The next show is reported normally.
            Native(AdFormats.Rewarded, NativeAdEventType.Loaded);
            _notices.Clear();
            _slots.Show(AdFormats.Rewarded, "{}");
            Native(AdFormats.Rewarded, NativeAdEventType.Shown);
            Native(AdFormats.Rewarded, NativeAdEventType.Closed);
            CollectionAssert.AreEqual(new[] { "rewarded:Shown", "rewarded:Closed" }, _notices);
            Assert.IsFalse(slot.IsShowing);
        }

        [Test]
        public void NewShow_AfterAnAbandonedOne_IsNotSwallowed()
        {
            Prepared(AdFormats.Interstitial);
            _slots.Show(AdFormats.Interstitial, "{}");
            Elapse(AdSlot.ShowAnswerTimeoutSeconds);
            Native(AdFormats.Interstitial, NativeAdEventType.Loaded);
            _notices.Clear();

            // The abandoned show never reports anything; the next one must still be heard.
            _slots.Show(AdFormats.Interstitial, "{}");
            Native(AdFormats.Interstitial, NativeAdEventType.Shown);

            CollectionAssert.AreEqual(new[] { "interstitial:Shown" }, _notices);
        }

        [Test]
        public void RefusedShow_OfASlotWithAnAbandonedShow_IsStillReported()
        {
            Prepared(AdFormats.Rewarded);
            _slots.Show(AdFormats.Rewarded, "{}");
            Elapse(AdSlot.ShowAnswerTimeoutSeconds);
            Native(AdFormats.Rewarded, NativeAdEventType.Loaded);

            Prepared(AdFormats.Interstitial);
            _slots.Show(AdFormats.Interstitial, "{}");
            _notices.Clear();
            _slots.Show(AdFormats.Rewarded, "{}");

            CollectionAssert.AreEqual(new[] { "rewarded:ShowFailed:already_showing" }, _notices);
        }

        // --- IsFullscreenShowing ---

        [Test]
        public void IsFullscreenShowing_FollowsFullscreenSlotsOnly()
        {
            Prepared(AdFormats.Banner);
            Prepared(AdFormats.Interstitial);

            _slots.Show(AdFormats.Banner, "{}");
            Assert.IsFalse(_slots.IsFullscreenShowing, "a banner does not cover the game");

            _slots.Show(AdFormats.Interstitial, "{}");
            Assert.IsTrue(_slots.IsFullscreenShowing);

            Native(AdFormats.Interstitial, NativeAdEventType.Shown);
            Native(AdFormats.Interstitial, NativeAdEventType.Closed);
            Assert.IsFalse(_slots.IsFullscreenShowing);
        }

#if !UNITY_5_3_OR_NEWER // allocation counters are a .NET (Core) API; runs under NativeAds/logic-tests
        [Test]
        public void IsFullscreenShowing_DoesNotAllocate()
        {
            Prepared(AdFormats.Rewarded);
            _slots.Show(AdFormats.Rewarded, "{}");
            var warmUp = _slots.IsFullscreenShowing;

            var before = GC.GetAllocatedBytesForCurrentThread();
            var any = false;
            for (var i = 0; i < 1000; i++)
                any |= _slots.IsFullscreenShowing;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(any && warmUp);
            Assert.AreEqual(0, allocated);
        }
#endif
    }
}
