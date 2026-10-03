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
        public void OnlyInterstitials_FetchAheadAfterSwitching()
        {
            Assert.IsTrue(AdRotation.FetchesAheadAfterSwitch(AdFormats.Interstitial));
            Assert.IsFalse(AdRotation.FetchesAheadAfterSwitch(AdFormats.Rewarded));
            Assert.IsFalse(AdRotation.FetchesAheadAfterSwitch(AdFormats.Banner));
            Assert.IsFalse(AdRotation.FetchesAheadAfterSwitch(null));
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
            Assert.IsTrue(AdRotation.CanSwitchNow(slotReady: false, slotShowing: false, formatCaching: false));
            Assert.IsFalse(AdRotation.CanSwitchNow(slotReady: true, slotShowing: false, formatCaching: false));
            Assert.IsFalse(AdRotation.CanSwitchNow(slotReady: false, slotShowing: true, formatCaching: false));
            Assert.IsFalse(AdRotation.CanSwitchNow(slotReady: false, slotShowing: false, formatCaching: true));
        }
    }
}
