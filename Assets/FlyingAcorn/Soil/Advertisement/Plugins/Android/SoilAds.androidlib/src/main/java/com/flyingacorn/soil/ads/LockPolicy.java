package com.flyingacorn.soil.ads;

/**
 * When the close button of a fullscreen ad unlocks ("Fullscreen lock policy" in PROTOCOL.md): the
 * same timing the Unity-drawn ads always had (interstitial 5 s, or 80% of a video but never under
 * 5 s; rewarded 20 s, or the whole video), counting only time the ad is on screen. Mirrors
 * Logic/FullscreenLockPolicy.cs. Pure Java so it runs in plain JVM unit tests.
 */
final class LockPolicy {
    private final ShowOptions options;
    private final boolean video;
    private final double durationSeconds;

    private boolean unlocked;
    private double visibleSeconds;
    private double positionSeconds;
    private boolean videoEnded;
    private boolean videoFailed;

    LockPolicy(ShowOptions options, boolean video, double durationSeconds) {
        this.options = options;
        this.video = video;
        this.durationSeconds = durationSeconds;
    }

    /** Feeds the latest playback state. Returns true only on the call that first unlocks. */
    boolean update(double visibleSeconds, double positionSeconds, boolean videoEnded, boolean videoFailed) {
        this.visibleSeconds = visibleSeconds;
        this.positionSeconds = positionSeconds;
        this.videoEnded = this.videoEnded || videoEnded;
        this.videoFailed = this.videoFailed || videoFailed;
        if (unlocked) return false;
        unlocked = isUnlocked(options, video, durationSeconds, visibleSeconds, positionSeconds, this.videoEnded,
                this.videoFailed);
        return unlocked;
    }

    boolean isUnlocked() {
        return unlocked;
    }

    /** Countdown shown on the close button; 0 once unlocked. */
    int secondsRemaining() {
        if (unlocked) return 0;
        return secondsRemaining(options, video, durationSeconds, visibleSeconds, positionSeconds, videoEnded,
                videoFailed);
    }

    /** Playback needed to unlock a video: max(min, fraction x duration); may outlast a short video. */
    static double videoLockSeconds(ShowOptions options, double durationSeconds) {
        double duration = Math.max(0, durationSeconds);
        return Math.max(0, Math.max(options.minVideoLockSeconds, options.videoLockFraction * duration));
    }

    /** On-screen time that unlocks regardless of playback; also the net under a stalled or failed video. */
    static double screenLockSeconds(ShowOptions options, boolean video, double durationSeconds) {
        return video
                ? Math.max(videoLockSeconds(options, durationSeconds), options.imageLockSeconds)
                : options.imageLockSeconds;
    }

    static boolean isUnlocked(ShowOptions options, boolean video, double durationSeconds, double visibleSeconds,
                              double positionSeconds, boolean videoEnded, boolean videoFailed) {
        if (visibleSeconds >= screenLockSeconds(options, video, durationSeconds)) return true;
        return playsVideo(video, durationSeconds, videoFailed)
                && progress(durationSeconds, visibleSeconds, positionSeconds, videoEnded)
                >= videoLockSeconds(options, durationSeconds);
    }

    static int secondsRemaining(ShowOptions options, boolean video, double durationSeconds, double visibleSeconds,
                                double positionSeconds, boolean videoEnded, boolean videoFailed) {
        double remaining = screenLockSeconds(options, video, durationSeconds) - visibleSeconds;
        if (playsVideo(video, durationSeconds, videoFailed)) {
            remaining = Math.min(remaining, videoLockSeconds(options, durationSeconds)
                    - progress(durationSeconds, visibleSeconds, positionSeconds, videoEnded));
        }
        return (int) Math.max(0, Math.ceil(remaining));
    }

    /** A video of unknown length cannot be followed; it is timed like a failed one. */
    private static boolean playsVideo(boolean video, double durationSeconds, boolean videoFailed) {
        return video && !videoFailed && durationSeconds > 0;
    }

    /** After its end a video keeps counting on-screen time, so a short one still unlocks at its lock. */
    private static double progress(double durationSeconds, double visibleSeconds, double positionSeconds,
                                   boolean videoEnded) {
        return videoEnded ? Math.max(durationSeconds, visibleSeconds) : positionSeconds;
    }
}
