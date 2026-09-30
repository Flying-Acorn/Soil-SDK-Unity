using FlyingAcorn.Soil.Advertisement.Logic;
using NUnit.Framework;

namespace FlyingAcorn.Soil.Advertisement.Tests
{
    /// <summary>
    /// The close-button lock of fullscreen ads. The same cases are tested against the Java and
    /// Objective-C players, so all three agree.
    /// </summary>
    public class FullscreenLockPolicyTests
    {
        private static readonly FullscreenShowOptions Interstitial = FullscreenShowOptions.InterstitialDefaults();
        private static readonly FullscreenShowOptions Rewarded = FullscreenShowOptions.RewardedDefaults();

        private static bool Unlocked(FullscreenShowOptions o, bool video, float duration, float visible,
            float position = 0, bool ended = false, bool failed = false)
            => FullscreenLockPolicy.IsUnlocked(o, video, duration, visible, position, ended, failed);

        [Test]
        public void InterstitialImage_UnlocksAfterFiveVisibleSeconds()
        {
            Assert.IsFalse(Unlocked(Interstitial, false, 0, 4.9f));
            Assert.IsTrue(Unlocked(Interstitial, false, 0, 5f));
        }

        [Test]
        public void InterstitialVideo_UnlocksAtEightyPercent()
        {
            Assert.AreEqual(16f, FullscreenLockPolicy.VideoLockSeconds(Interstitial, 20f), 1e-4);
            Assert.IsFalse(Unlocked(Interstitial, true, 20f, 30f, 15.9f), "wall time does not count, playback does");
            Assert.IsTrue(Unlocked(Interstitial, true, 20f, 16f, 16f));
        }

        [Test]
        public void InterstitialVideo_NeverLocksLessThanFiveSeconds()
        {
            Assert.AreEqual(5f, FullscreenLockPolicy.VideoLockSeconds(Interstitial, 6f), 1e-4);
            Assert.IsFalse(Unlocked(Interstitial, true, 6f, 4.9f, 4.9f));
            Assert.IsTrue(Unlocked(Interstitial, true, 6f, 5f, 5f));
        }

        [Test]
        public void VideoLock_IsNeverLongerThanTheVideo()
        {
            Assert.AreEqual(3f, FullscreenLockPolicy.VideoLockSeconds(Interstitial, 3f), 1e-4);
            Assert.AreEqual(0f, FullscreenLockPolicy.VideoLockSeconds(Interstitial, 0f), 1e-4);
            Assert.AreEqual(0f, FullscreenLockPolicy.VideoLockSeconds(Interstitial, -1f), 1e-4);
        }

        [Test]
        public void RewardedVideo_UnlocksOnlyAtTheEnd()
        {
            Assert.AreEqual(15f, FullscreenLockPolicy.VideoLockSeconds(Rewarded, 15f), 1e-4);
            Assert.IsFalse(Unlocked(Rewarded, true, 15f, 14.9f, 14.9f));
            Assert.IsTrue(Unlocked(Rewarded, true, 15f, 15f, 15f));
            Assert.IsTrue(Unlocked(Rewarded, true, 15f, 14f, 14.5f, ended: true), "an ended video unlocks even if the position lags");
        }

        [Test]
        public void RewardedImage_UnlocksAfterTwentyVisibleSeconds()
        {
            Assert.IsFalse(Unlocked(Rewarded, false, 0, 19.9f));
            Assert.IsTrue(Unlocked(Rewarded, false, 0, 20f));
        }

        [Test]
        public void FailedVideo_FallsBackToTheImageRule_FromTheFirstSecondShown()
        {
            Assert.IsFalse(Unlocked(Rewarded, true, 30f, 19f, 2f, failed: true));
            Assert.IsTrue(Unlocked(Rewarded, true, 30f, 20f, 2f, failed: true));
        }

        [Test]
        public void SafetyNet_NeverTrapsTheUser()
        {
            Assert.IsTrue(Unlocked(Rewarded, true, 300f, 60f, 1f), "a stalled long video unlocks at the cap");
            Assert.IsFalse(Unlocked(Rewarded, true, 300f, 59.9f, 1f));

            var longImage = new FullscreenShowOptions { ImageLockSeconds = 90, MaxLockSeconds = 60 };
            Assert.IsTrue(Unlocked(longImage, false, 0, 60f));
        }

        [Test]
        public void Countdown_FollowsPlaybackForVideo()
        {
            Assert.AreEqual(16, FullscreenLockPolicy.SecondsRemaining(Interstitial, true, 20f, 0f, 0f, false));
            Assert.AreEqual(1, FullscreenLockPolicy.SecondsRemaining(Interstitial, true, 20f, 15.5f, 15.5f, false));
            Assert.AreEqual(0, FullscreenLockPolicy.SecondsRemaining(Interstitial, true, 20f, 17f, 17f, false));
        }

        [Test]
        public void Countdown_FollowsVisibleTimeForImages()
        {
            Assert.AreEqual(5, FullscreenLockPolicy.SecondsRemaining(Interstitial, false, 0, 0f, 0f, false));
            Assert.AreEqual(3, FullscreenLockPolicy.SecondsRemaining(Interstitial, false, 0, 2.2f, 0f, false));
            Assert.AreEqual(20, FullscreenLockPolicy.SecondsRemaining(Rewarded, true, 30f, 0f, 0f, true));
        }

        [Test]
        public void Countdown_IsNeverNegative_NorBeyondTheCap()
        {
            Assert.AreEqual(0, FullscreenLockPolicy.SecondsRemaining(Interstitial, false, 0, 100f, 0f, false));
            Assert.AreEqual(10, FullscreenLockPolicy.SecondsRemaining(Rewarded, true, 300f, 50f, 1f, false));
        }
    }
}
