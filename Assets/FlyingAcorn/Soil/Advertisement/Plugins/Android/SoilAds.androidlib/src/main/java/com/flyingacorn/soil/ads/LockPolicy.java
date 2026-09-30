package com.flyingacorn.soil.ads;

/**
 * When the close button of a fullscreen ad unlocks ("Fullscreen lock policy" in PROTOCOL.md).
 * Pure Java so it runs in plain JVM unit tests.
 */
final class LockPolicy {
    private final ShowOptions options;
    private final boolean video;
    private final double durationSeconds;

    private boolean unlocked;
    private double visibleSeconds;
    private double positionSeconds;
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
        this.videoFailed = videoFailed;
        if (unlocked) return false;
        unlocked = isUnlocked(options, video, durationSeconds, visibleSeconds, positionSeconds, videoEnded,
                videoFailed);
        return unlocked;
    }

    boolean isUnlocked() {
        return unlocked;
    }

    /** Countdown shown on the close button; 0 once unlocked. */
    int secondsRemaining() {
        if (unlocked) return 0;
        return secondsRemaining(options, video, durationSeconds, visibleSeconds, positionSeconds, videoFailed);
    }

    static double videoLockSeconds(ShowOptions options, double durationSeconds) {
        double lock = Math.max(options.minVideoLockSeconds, options.videoLockFraction * durationSeconds);
        return Math.min(Math.max(lock, 0), Math.max(durationSeconds, 0));
    }

    static boolean isUnlocked(ShowOptions options, boolean video, double durationSeconds, double visibleSeconds,
                              double positionSeconds, boolean videoEnded, boolean videoFailed) {
        if (visibleSeconds >= options.maxLockSeconds) return true;
        if (video && !videoFailed) {
            return videoEnded || positionSeconds >= videoLockSeconds(options, durationSeconds);
        }
        return visibleSeconds >= options.imageLockSeconds;
    }

    static int secondsRemaining(ShowOptions options, boolean video, double durationSeconds, double visibleSeconds,
                                double positionSeconds, boolean videoFailed) {
        double remaining = video && !videoFailed
                ? videoLockSeconds(options, durationSeconds) - positionSeconds
                : options.imageLockSeconds - visibleSeconds;
        double capped = Math.min(Math.ceil(remaining), Math.ceil(options.maxLockSeconds - visibleSeconds));
        return (int) Math.max(0, capped);
    }
}
