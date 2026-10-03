using System;

namespace FlyingAcorn.Soil.Advertisement.Logic
{
    /// <summary>
    /// When a fullscreen ad's close button unlocks ("Fullscreen lock policy" in
    /// NativeAds/PROTOCOL.md). The Android and iOS players implement the same formula natively;
    /// this copy drives the Editor simulation and is the reference the native tests mirror.
    ///
    /// It reproduces the Unity-drawn ads the SDK used to have - interstitial: 5 s, or 80% of a
    /// video but never under 5 s; rewarded: 20 s, or the whole video - with two changes: only time
    /// the ad is actually on screen counts, and no lock outlasts MaxLockSeconds (interstitial 15 s).
    /// </summary>
    public static class FullscreenLockPolicy
    {
        /// <summary>Playback needed to unlock a video: max(min, fraction × duration); may outlast a short video.</summary>
        public static float VideoLockSeconds(FullscreenShowOptions options, float durationSeconds)
        {
            var duration = Math.Max(0f, durationSeconds);
            return Capped(options, Math.Max(0f, Math.Max(options.MinVideoLockSeconds, options.VideoLockFraction * duration)));
        }

        private static float Capped(FullscreenShowOptions options, float seconds) =>
            options.MaxLockSeconds > 0 ? Math.Min(seconds, options.MaxLockSeconds) : seconds;

        /// <summary>
        /// On-screen time that unlocks regardless of playback: the countdown the SDK always ran. It
        /// is also what saves the user from a stalled or failed video.
        /// </summary>
        public static float ScreenLockSeconds(FullscreenShowOptions options, bool isVideo, float durationSeconds)
        {
            return Capped(options, isVideo
                ? Math.Max(VideoLockSeconds(options, durationSeconds), options.ImageLockSeconds)
                : options.ImageLockSeconds);
        }

        /// <param name="isVideo">The ad plays a video (not an image).</param>
        /// <param name="visibleSeconds">Time on screen while the app was in the foreground.</param>
        public static bool IsUnlocked(FullscreenShowOptions options, bool isVideo, float durationSeconds,
            float visibleSeconds, float positionSeconds, bool videoEnded, bool videoFailed)
        {
            if (visibleSeconds >= ScreenLockSeconds(options, isVideo, durationSeconds)) return true;
            return PlaysVideo(isVideo, durationSeconds, videoFailed)
                   && Progress(durationSeconds, visibleSeconds, positionSeconds, videoEnded)
                   >= VideoLockSeconds(options, durationSeconds);
        }

        /// <summary>The number the close button shows while locked.</summary>
        public static int SecondsRemaining(FullscreenShowOptions options, bool isVideo, float durationSeconds,
            float visibleSeconds, float positionSeconds, bool videoEnded, bool videoFailed)
        {
            var remaining = ScreenLockSeconds(options, isVideo, durationSeconds) - visibleSeconds;
            if (PlaysVideo(isVideo, durationSeconds, videoFailed))
                remaining = Math.Min(remaining, VideoLockSeconds(options, durationSeconds)
                                                - Progress(durationSeconds, visibleSeconds, positionSeconds, videoEnded));
            return Math.Max(0, (int)Math.Ceiling(remaining));
        }

        // A video of unknown length cannot be followed; it is timed like a failed one.
        private static bool PlaysVideo(bool isVideo, float durationSeconds, bool videoFailed) =>
            isVideo && !videoFailed && durationSeconds > 0;

        // How far the video has got. After its end the ad keeps counting on-screen time, so a
        // video shorter than its lock still unlocks at the lock (a 3 s interstitial video: 5 s).
        private static float Progress(float durationSeconds, float visibleSeconds, float positionSeconds, bool videoEnded) =>
            videoEnded ? Math.Max(durationSeconds, visibleSeconds) : positionSeconds;
    }
}
