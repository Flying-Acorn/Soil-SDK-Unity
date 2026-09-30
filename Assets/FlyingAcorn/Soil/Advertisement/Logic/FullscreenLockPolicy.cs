using System;

namespace FlyingAcorn.Soil.Advertisement.Logic
{
    /// <summary>
    /// When a fullscreen ad's close button unlocks ("Fullscreen lock policy" in
    /// NativeAds/PROTOCOL.md). The Android and iOS players implement the same formula natively;
    /// this copy drives the Editor simulation and is the reference the native tests mirror.
    /// </summary>
    public static class FullscreenLockPolicy
    {
        public static float VideoLockSeconds(FullscreenShowOptions options, float durationSeconds)
        {
            var duration = Math.Max(0f, durationSeconds);
            var lockSeconds = Math.Max(options.MinVideoLockSeconds, options.VideoLockFraction * duration);
            return Math.Min(Math.Max(lockSeconds, 0f), duration);
        }

        /// <param name="isVideo">The ad plays a video (not an image).</param>
        /// <param name="visibleSeconds">Time on screen while the app was in the foreground.</param>
        public static bool IsUnlocked(FullscreenShowOptions options, bool isVideo, float durationSeconds,
            float visibleSeconds, float positionSeconds, bool videoEnded, bool videoFailed)
        {
            if (visibleSeconds >= options.MaxLockSeconds) return true;
            if (isVideo && !videoFailed)
                return videoEnded || positionSeconds >= VideoLockSeconds(options, durationSeconds);
            return visibleSeconds >= options.ImageLockSeconds;
        }

        /// <summary>The number the close button shows while locked.</summary>
        public static int SecondsRemaining(FullscreenShowOptions options, bool isVideo, float durationSeconds,
            float visibleSeconds, float positionSeconds, bool videoFailed)
        {
            var remaining = isVideo && !videoFailed
                ? VideoLockSeconds(options, durationSeconds) - positionSeconds
                : options.ImageLockSeconds - visibleSeconds;
            remaining = Math.Min(remaining, options.MaxLockSeconds - visibleSeconds);
            return Math.Max(0, (int)Math.Ceiling(remaining));
        }
    }
}
