using FlyingAcorn.Soil.Advertisement.Logic;
using NUnit.Framework;

namespace FlyingAcorn.Soil.Advertisement.Tests
{
    /// <summary>
    /// Rotating fullscreen ads: one next ad group is fetched at a time, what was seen is reported
    /// so the server can weigh it down, a server that excludes seen ids still gives an ad, and an
    /// ad the game may be about to show is never replaced.
    /// </summary>
    public class AdRotationTests
    {
        [TestCase(AdFormats.Interstitial)]
        [TestCase(AdFormats.Rewarded)]
        public void FullscreenFormats_FetchTheNextAdGroup(string format)
        {
            Assert.IsTrue(AdRotation.ShouldFetchNext(format, fetching: false, waitingToSwitch: false));
        }

        [TestCase(AdFormats.Banner)]
        [TestCase("native")]
        [TestCase("unknown")]
        [TestCase(null)]
        public void OtherFormats_DoNotRotate(string format)
        {
            Assert.IsFalse(AdRotation.ShouldFetchNext(format, fetching: false, waitingToSwitch: false));
        }

        [Test]
        public void OneFetchAtATime_AndNoneWhileOneWaitsToSwitch()
        {
            Assert.IsFalse(AdRotation.ShouldFetchNext(AdFormats.Rewarded, fetching: true, waitingToSwitch: false));
            Assert.IsFalse(AdRotation.ShouldFetchNext(AdFormats.Rewarded, fetching: false, waitingToSwitch: true));
        }

        [Test]
        public void TheCloseRetries_OnlyAFetchThatFailed()
        {
            Assert.IsTrue(AdRotation.RetriesAfterClose(AdFormats.Interstitial, lastFetchFailed: true, fetching: false, waitingToSwitch: false));
            Assert.IsFalse(AdRotation.RetriesAfterClose(AdFormats.Interstitial, lastFetchFailed: false, fetching: false, waitingToSwitch: false));
            Assert.IsFalse(AdRotation.RetriesAfterClose(AdFormats.Rewarded, lastFetchFailed: true, fetching: true, waitingToSwitch: false));
            Assert.IsFalse(AdRotation.RetriesAfterClose(AdFormats.Rewarded, lastFetchFailed: true, fetching: false, waitingToSwitch: true));
            Assert.IsFalse(AdRotation.RetriesAfterClose(AdFormats.Banner, lastFetchFailed: true, fetching: false, waitingToSwitch: false));
        }

        [Test]
        public void AClosedInterstitialWaitsForItsNextAdGroup_ForAWhile()
        {
            Assert.IsTrue(AdRotation.HoldsAfterClose(AdFormats.Interstitial, nextAdPending: true, secondsSinceClose: 0));
            Assert.IsTrue(AdRotation.HoldsAfterClose(AdFormats.Interstitial, nextAdPending: true, secondsSinceClose: 5.9));
            Assert.IsFalse(AdRotation.HoldsAfterClose(AdFormats.Interstitial, nextAdPending: true, secondsSinceClose: AdRotation.HoldAfterCloseSeconds));
            Assert.IsFalse(AdRotation.HoldsAfterClose(AdFormats.Interstitial, nextAdPending: false, secondsSinceClose: 1));
        }

        [Test]
        public void ARewardedAdIsHeldToo_ItsCooldownCanEndWhileUnityIsPaused()
        {
            Assert.IsTrue(AdRotation.HoldsAfterClose(AdFormats.Rewarded, nextAdPending: true, secondsSinceClose: 1));
            Assert.IsFalse(AdRotation.HoldsAfterClose(AdFormats.Rewarded, nextAdPending: false, secondsSinceClose: 1));
        }

        [Test]
        public void BannersAreNeverHeld()
        {
            Assert.IsFalse(AdRotation.HoldsAfterClose(AdFormats.Banner, nextAdPending: true, secondsSinceClose: 1));
            Assert.IsFalse(AdRotation.HoldsAfterClose("native", nextAdPending: true, secondsSinceClose: 1));
        }

        [Test]
        public void NoHoldBeforeAnyClose()
        {
            Assert.IsFalse(AdRotation.HoldsAfterClose(AdFormats.Interstitial, nextAdPending: true, secondsSinceClose: -1));
        }

        [Test]
        public void SeenIds_AreTheHistoryThenTheExtras_WithoutBlanksOrDuplicates()
        {
            var seen = AdRotation.SeenIds(new[] { "a", "b", "", null }, new[] { "b", "c", null });

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, seen);
        }

        [Test]
        public void SeenIds_AcceptMissingLists()
        {
            CollectionAssert.IsEmpty(AdRotation.SeenIds(null, null));
            CollectionAssert.AreEqual(new[] { "x" }, AdRotation.SeenIds(null, new[] { "x" }));
        }

        [Test]
        public void RetriesWithoutSeenIds_OnlyWhenNothingCameBackForThem()
        {
            Assert.IsTrue(AdRotation.RetryWithoutSeenIds(gotAdGroup: false, seenAdGroupCount: 2, seenCampaignCount: 0));
            Assert.IsTrue(AdRotation.RetryWithoutSeenIds(gotAdGroup: false, seenAdGroupCount: 0, seenCampaignCount: 1));
            Assert.IsFalse(AdRotation.RetryWithoutSeenIds(gotAdGroup: true, seenAdGroupCount: 2, seenCampaignCount: 1));
            Assert.IsFalse(AdRotation.RetryWithoutSeenIds(gotAdGroup: false, seenAdGroupCount: 0, seenCampaignCount: 0));
        }

        [Test]
        public void OnlyADifferentAdGroupReplacesTheCurrentOne()
        {
            Assert.IsTrue(AdRotation.IsNewAdGroup("a", "b"));
            Assert.IsTrue(AdRotation.IsNewAdGroup(null, "b"));
            Assert.IsFalse(AdRotation.IsNewAdGroup("a", "a"));
            Assert.IsFalse(AdRotation.IsNewAdGroup("a", null));
            Assert.IsFalse(AdRotation.IsNewAdGroup("a", ""));
        }

        [Test]
        public void SwitchesOnlyWhenTheSlotHasNothingReadyOrOnScreen()
        {
            Assert.IsTrue(AdRotation.CanSwitchNow(slotReady: false, slotPreparing: false, slotShowing: false, formatCaching: false, fullscreenOnScreen: false));
            Assert.IsFalse(AdRotation.CanSwitchNow(slotReady: true, slotPreparing: false, slotShowing: false, formatCaching: false, fullscreenOnScreen: false));
            Assert.IsFalse(AdRotation.CanSwitchNow(slotReady: false, slotPreparing: false, slotShowing: true, formatCaching: false, fullscreenOnScreen: false));
            Assert.IsFalse(AdRotation.CanSwitchNow(slotReady: false, slotPreparing: false, slotShowing: false, formatCaching: true, fullscreenOnScreen: false));
        }

        [Test]
        public void NeverSwitchesWhileTheSlotIsStillPreparing()
        {
            // The answer to the old load could be taken for the answer to the new one.
            Assert.IsFalse(AdRotation.CanSwitchNow(slotReady: false, slotPreparing: true, slotShowing: false, formatCaching: false, fullscreenOnScreen: false));
        }

        [Test]
        public void NeverSwitchesWhileAnotherFullscreenAdIsOnScreen()
        {
            // A rewarded video playing would leave the interstitial's new video undecodable.
            Assert.IsFalse(AdRotation.CanSwitchNow(slotReady: false, slotPreparing: false, slotShowing: false, formatCaching: false, fullscreenOnScreen: true));
        }

        [Test]
        public void AFailedLaunchBacksOff_UpToFiveMinutes()
        {
            Assert.AreEqual(5, AdRotation.LaunchRetryDelaySeconds(1));
            Assert.AreEqual(15, AdRotation.LaunchRetryDelaySeconds(2));
            Assert.AreEqual(300, AdRotation.LaunchRetryDelaySeconds(6));
            Assert.AreEqual(300, AdRotation.LaunchRetryDelaySeconds(50));
            Assert.AreEqual(5, AdRotation.LaunchRetryDelaySeconds(0));
        }

        [Test]
        public void AFailedLaunchRetries_AfterTheBackoff_OrSoonerOnLoadAd()
        {
            Assert.IsFalse(AdRotation.ShouldRetryLaunch(busy: false, secondsSinceLastTry: 20, failures: 3, loadRequested: false));
            Assert.IsTrue(AdRotation.ShouldRetryLaunch(busy: false, secondsSinceLastTry: 30, failures: 3, loadRequested: false));
            Assert.IsTrue(AdRotation.ShouldRetryLaunch(busy: false, secondsSinceLastTry: 5, failures: 3, loadRequested: true));
            // Mediation retries a load at once; those do not each send a request.
            Assert.IsFalse(AdRotation.ShouldRetryLaunch(busy: false, secondsSinceLastTry: 1, failures: 3, loadRequested: true));
            Assert.IsFalse(AdRotation.ShouldRetryLaunch(busy: true, secondsSinceLastTry: 1000, failures: 1, loadRequested: true));
        }
    }
}
