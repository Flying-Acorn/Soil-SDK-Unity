using FlyingAcorn.Soil.Advertisement.Logic;
using NUnit.Framework;

namespace FlyingAcorn.Soil.Advertisement.Tests
{
    /// <summary>
    /// The close-button lock of fullscreen ads, pinned to what the Unity-drawn ads did before the
    /// native players: interstitial 5 s, or 80% of a video but never under 5 s; rewarded 20 s, or
    /// the whole video; the countdown doubles as the safety net for a stalled or failed video.
    /// The same cases are tested against the Java and Objective-C players.
    /// </summary>
    public class FullscreenLockPolicyTests
    {
        private static readonly FullscreenShowOptions Interstitial = FullscreenShowOptions.InterstitialDefaults();
        private static readonly FullscreenShowOptions Rewarded = FullscreenShowOptions.RewardedDefaults();

        private static bool Unlocked(FullscreenShowOptions o, bool video, float duration, float visible,
            float position = 0, bool ended = false, bool failed = false)
            => FullscreenLockPolicy.IsUnlocked(o, video, duration, visible, position, ended, failed);

        private static int Remaining(FullscreenShowOptions o, bool video, float duration, float visible,
            float position = 0, bool ended = false, bool failed = false)
            => FullscreenLockPolicy.SecondsRemaining(o, video, duration, visible, position, ended, failed);

        [Test]
        public void InterstitialImage_UnlocksAfterFiveSeconds()
        {
            Assert.IsFalse(Unlocked(Interstitial, false, 0, 4.9f));
            Assert.IsTrue(Unlocked(Interstitial, false, 0, 5f));
        }

        [Test]
        public void RewardedImage_UnlocksAfterTwentySeconds()
        {
            Assert.IsFalse(Unlocked(Rewarded, false, 0, 19.9f));
            Assert.IsTrue(Unlocked(Rewarded, false, 0, 20f));
        }

        [Test]
        public void InterstitialVideo_UnlocksAtEightyPercent()
        {
            Assert.AreEqual(12f, FullscreenLockPolicy.VideoLockSeconds(Interstitial, 15f), 1e-4);
            Assert.IsFalse(Unlocked(Interstitial, true, 15f, 11.9f, 11.9f));
            Assert.IsTrue(Unlocked(Interstitial, true, 15f, 12f, 12f));
        }

        [Test]
        public void Interstitial_IsAlwaysClosableBy15Seconds()
        {
            // Google Play: interstitials must be closable after 15 s. 80% of a 30 s video would be 24 s.
            Assert.AreEqual(15f, FullscreenLockPolicy.VideoLockSeconds(Interstitial, 30f), 1e-4);
            Assert.AreEqual(15f, FullscreenLockPolicy.ScreenLockSeconds(Interstitial, true, 30f), 1e-4);
            Assert.IsFalse(Unlocked(Interstitial, true, 30f, 14.9f, 14.9f));
            Assert.IsTrue(Unlocked(Interstitial, true, 30f, 15f, 15f));
            Assert.IsTrue(Unlocked(Interstitial, true, 30f, 15f, 2f), "a stalled video too");
            Assert.AreEqual(15, Remaining(Interstitial, true, 30f, 0f, 0f));
            var longImage = FullscreenShowOptions.InterstitialDefaults();
            longImage.ImageLockSeconds = 20f;
            Assert.IsTrue(Unlocked(longImage, false, 0f, 15f, 0f), "the cap holds for any interstitial");
        }

        [Test]
        public void Rewarded_IsNotCapped_AndNoCapMeansTheFullLock()
        {
            Assert.AreEqual(30f, FullscreenLockPolicy.VideoLockSeconds(Rewarded, 30f), 1e-4);
            Assert.IsFalse(Unlocked(Rewarded, true, 30f, 29f, 29f));
            var uncapped = FullscreenShowOptions.InterstitialDefaults();
            uncapped.MaxLockSeconds = 0f;
            Assert.AreEqual(24f, FullscreenLockPolicy.VideoLockSeconds(uncapped, 30f), 1e-4);
        }

        [Test]
        public void InterstitialVideo_ShorterThanFiveSeconds_StillLocksFiveSeconds()
        {
            // As before: the end of an interstitial video does not unlock it early.
            Assert.AreEqual(5f, FullscreenLockPolicy.VideoLockSeconds(Interstitial, 3f), 1e-4);
            Assert.IsFalse(Unlocked(Interstitial, true, 3f, 3.1f, 3f, ended: true));
            Assert.AreEqual(2, Remaining(Interstitial, true, 3f, 3.1f, 3f, ended: true));
            Assert.IsTrue(Unlocked(Interstitial, true, 3f, 5f, 3f, ended: true));
        }

        [Test]
        public void RewardedVideo_UnlocksAtTheEnd_EvenWhenShorterThanTwentySeconds()
        {
            Assert.AreEqual(15f, FullscreenLockPolicy.VideoLockSeconds(Rewarded, 15f), 1e-4);
            Assert.IsFalse(Unlocked(Rewarded, true, 15f, 14.9f, 14.9f));
            Assert.IsTrue(Unlocked(Rewarded, true, 15f, 15f, 15f));
            Assert.IsTrue(Unlocked(Rewarded, true, 15f, 14f, 14.5f, ended: true), "an ended video unlocks even if the position lags");
            Assert.IsTrue(Unlocked(Rewarded, true, 3f, 3f, 3f, ended: true), "a 3 s rewarded video rewards at 3 s");
        }

        [Test]
        public void RewardedVideo_LongerThanAMinute_IsWatchedToTheEnd()
        {
            Assert.IsFalse(Unlocked(Rewarded, true, 90f, 70f, 70f), "no cap below the video's length");
            Assert.IsTrue(Unlocked(Rewarded, true, 90f, 90f, 90f));
        }

        [Test]
        public void StalledVideo_UnlocksWhenTheCountdownEnds()
        {
            // Playback stuck at 1 s: the on-screen countdown still ends, as it always did.
            Assert.IsFalse(Unlocked(Rewarded, true, 15f, 19.9f, 1f));
            Assert.IsTrue(Unlocked(Rewarded, true, 15f, 20f, 1f), "rewarded: 20 s, or the video's length if longer");
            Assert.IsTrue(Unlocked(Rewarded, true, 30f, 30f, 1f));
            Assert.IsTrue(Unlocked(Interstitial, true, 20f, 16f, 1f), "interstitial: 80% of the video");
        }

        [Test]
        public void FailedVideo_UnlocksWhenTheCountdownEnds()
        {
            Assert.IsFalse(Unlocked(Rewarded, true, 15f, 19f, 2f, failed: true));
            Assert.IsTrue(Unlocked(Rewarded, true, 15f, 20f, 2f, failed: true));
            Assert.IsTrue(Unlocked(Interstitial, true, 3f, 5f, 1f, failed: true));
        }

        [Test]
        public void VideoOfUnknownLength_IsTimedLikeAnImage()
        {
            Assert.IsFalse(Unlocked(Rewarded, true, 0f, 19f, 5f));
            Assert.IsTrue(Unlocked(Rewarded, true, 0f, 20f, 5f));
        }

        [Test]
        public void Countdown_FollowsWhicheverRuleIsCloser()
        {
            Assert.AreEqual(12, Remaining(Interstitial, true, 15f, 0f, 0f));
            Assert.AreEqual(1, Remaining(Interstitial, true, 15f, 11.5f, 11.5f));
            Assert.AreEqual(0, Remaining(Interstitial, true, 15f, 13f, 13f));
            Assert.AreEqual(5, Remaining(Rewarded, true, 15f, 10f, 10f), "playback: 5 s to the end");
            Assert.AreEqual(5, Remaining(Rewarded, true, 15f, 15f, 1f), "stalled: 5 s to the 20 s countdown");
            Assert.AreEqual(20, Remaining(Rewarded, true, 30f, 10f, 0f, failed: true), "failed: the 30 s countdown");
        }

        [Test]
        public void Countdown_ForImagesFollowsVisibleTime()
        {
            Assert.AreEqual(5, Remaining(Interstitial, false, 0, 0f));
            Assert.AreEqual(3, Remaining(Interstitial, false, 0, 2.2f));
            Assert.AreEqual(20, Remaining(Rewarded, false, 0, 0f));
        }

        [Test]
        public void Countdown_IsNeverNegative()
        {
            Assert.AreEqual(0, Remaining(Interstitial, false, 0, 100f));
            Assert.AreEqual(0, Remaining(Rewarded, true, 15f, 40f, 15f, ended: true));
        }
    }
}
