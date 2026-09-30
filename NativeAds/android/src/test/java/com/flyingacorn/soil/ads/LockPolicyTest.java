package com.flyingacorn.soil.ads;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

/** Every rule of "Fullscreen lock policy" in PROTOCOL.md. */
public class LockPolicyTest {
    private static final double EPS = 1e-9;
    private static final ShowOptions INTERSTITIAL = ShowOptions.defaults("interstitial"); // 5 / 0.8 / 5 / 60
    private static final ShowOptions REWARDED = ShowOptions.defaults("rewarded");         // 20 / 1.0 / 0 / 60

    private static ShowOptions options(double image, double fraction, double minVideo, double max) {
        return new ShowOptions(image, fraction, minVideo, max, false);
    }

    private static boolean unlocked(ShowOptions o, boolean video, double duration, double visible, double position,
                                    boolean ended, boolean failed) {
        return LockPolicy.isUnlocked(o, video, duration, visible, position, ended, failed);
    }

    // ---- videoLock = clamp(max(minVideoLockSeconds, videoLockFraction * D), 0, D)

    @Test
    public void videoLockUsesFractionWhenLarger() {
        assertEquals(24, LockPolicy.videoLockSeconds(INTERSTITIAL, 30), EPS);
    }

    @Test
    public void videoLockUsesMinimumWhenLarger() {
        assertEquals(5, LockPolicy.videoLockSeconds(INTERSTITIAL, 6), EPS); // 0.8 * 6 = 4.8 < 5
    }

    @Test
    public void videoLockIsClampedToDuration() {
        assertEquals(3, LockPolicy.videoLockSeconds(INTERSTITIAL, 3), EPS); // min 5 > D 3
    }

    @Test
    public void videoLockNeverNegative() {
        assertEquals(0, LockPolicy.videoLockSeconds(options(5, -1, -3, 60), 10), EPS);
    }

    @Test
    public void videoLockOfWholeVideoForRewarded() {
        assertEquals(15, LockPolicy.videoLockSeconds(REWARDED, 15), EPS);
    }

    @Test
    public void videoLockOfZeroDurationIsZero() {
        assertEquals(0, LockPolicy.videoLockSeconds(INTERSTITIAL, 0), EPS);
    }

    @Test
    public void videoLockFractionAboveOneStillClampedToDuration() {
        assertEquals(10, LockPolicy.videoLockSeconds(options(5, 2.0, 0, 60), 10), EPS);
    }

    // ---- image rule

    @Test
    public void imageLockedUntilImageLockSeconds() {
        assertFalse(unlocked(INTERSTITIAL, false, 0, 0, 0, false, false));
        assertFalse(unlocked(INTERSTITIAL, false, 0, 4.999, 0, false, false));
        assertTrue(unlocked(INTERSTITIAL, false, 0, 5, 0, false, false));
        assertTrue(unlocked(INTERSTITIAL, false, 0, 7, 0, false, false));
    }

    @Test
    public void imageIgnoresVideoPositionAndEnded() {
        assertFalse(unlocked(INTERSTITIAL, false, 30, 1, 100, true, false));
    }

    @Test
    public void zeroImageLockUnlocksImmediately() {
        assertTrue(unlocked(options(0, 0.8, 5, 60), false, 0, 0, 0, false, false));
    }

    // ---- video rule

    @Test
    public void videoLockedUntilPositionReachesLock() {
        assertFalse(unlocked(INTERSTITIAL, true, 30, 30, 23.9, false, false));
        assertTrue(unlocked(INTERSTITIAL, true, 30, 30, 24, false, false));
    }

    @Test
    public void videoIgnoresVisibleTimeBelowSafetyNet() {
        // Visible for longer than imageLockSeconds, but the video has not reached its lock.
        assertFalse(unlocked(INTERSTITIAL, true, 30, 20, 2, false, false));
    }

    @Test
    public void videoEndedUnlocksEvenIfPositionIsShort() {
        assertTrue(unlocked(REWARDED, true, 15, 14, 14.9, true, false));
    }

    @Test
    public void rewardedVideoNeedsWholeVideo() {
        assertFalse(unlocked(REWARDED, true, 15, 15, 14.9, false, false));
        assertTrue(unlocked(REWARDED, true, 15, 15, 15, false, false));
    }

    @Test
    public void shortVideoUnlocksAtItsEnd() {
        assertFalse(unlocked(INTERSTITIAL, true, 3, 2.9, 2.9, false, false));
        assertTrue(unlocked(INTERSTITIAL, true, 3, 3, 3, false, false));
    }

    // ---- safety net

    @Test
    public void safetyNetUnlocksEverything() {
        ShowOptions o = options(100, 1.0, 100, 10);
        assertFalse(unlocked(o, false, 0, 9.9, 0, false, false));
        assertTrue(unlocked(o, false, 0, 10, 0, false, false));
        assertTrue(unlocked(o, true, 200, 10, 0, false, false));
        assertTrue(unlocked(o, true, 200, 10, 0, false, true));
    }

    @Test
    public void zeroMaxLockUnlocksImmediately() {
        assertTrue(unlocked(options(5, 0.8, 5, 0), true, 30, 0, 0, false, false));
    }

    // ---- video failed switches to the image rule, measured from the first show

    @Test
    public void failedVideoUsesImageRuleOnVisibleTime() {
        assertFalse(unlocked(INTERSTITIAL, true, 30, 4, 30, true, true)); // ended/position no longer count
        assertTrue(unlocked(INTERSTITIAL, true, 30, 5, 0, false, true));
    }

    // ---- countdown

    @Test
    public void countdownForImage() {
        assertEquals(5, LockPolicy.secondsRemaining(INTERSTITIAL, false, 0, 0, 0, false));
        assertEquals(1, LockPolicy.secondsRemaining(INTERSTITIAL, false, 0, 4.2, 0, false));
        assertEquals(2, LockPolicy.secondsRemaining(INTERSTITIAL, false, 0, 3.0, 0, false));
        assertEquals(0, LockPolicy.secondsRemaining(INTERSTITIAL, false, 0, 5, 0, false));
    }

    @Test
    public void countdownForVideo() {
        assertEquals(24, LockPolicy.secondsRemaining(INTERSTITIAL, true, 30, 0, 0, false));
        assertEquals(14, LockPolicy.secondsRemaining(INTERSTITIAL, true, 30, 40, 10.5, false));
        assertEquals(10, LockPolicy.secondsRemaining(INTERSTITIAL, true, 30, 50, 10.5, false)); // safety net cap
        assertEquals(1, LockPolicy.secondsRemaining(REWARDED, true, 15, 15, 14.9, false));
    }

    @Test
    public void countdownForFailedVideoUsesVisibleTime() {
        assertEquals(3, LockPolicy.secondsRemaining(INTERSTITIAL, true, 30, 2, 29, true));
    }

    @Test
    public void countdownNeverBelowZero() {
        assertEquals(0, LockPolicy.secondsRemaining(INTERSTITIAL, false, 0, 100, 0, false));
        assertEquals(0, LockPolicy.secondsRemaining(INTERSTITIAL, true, 30, 1, 29, false));
        assertEquals(0, LockPolicy.secondsRemaining(INTERSTITIAL, true, 30, 100, 0, false));
    }

    @Test
    public void countdownNeverAboveSafetyNet() {
        ShowOptions o = options(20, 1.0, 0, 3);
        assertEquals(3, LockPolicy.secondsRemaining(o, false, 0, 0, 0, false));
        assertEquals(2, LockPolicy.secondsRemaining(o, false, 0, 1.5, 0, false));
        assertEquals(3, LockPolicy.secondsRemaining(o, true, 60, 0, 0, false));
    }

    // ---- stateful tracker

    @Test
    public void updateReportsFirstUnlockOnceAndStaysUnlocked() {
        LockPolicy lock = new LockPolicy(INTERSTITIAL, false, 0);
        assertFalse(lock.update(1, 0, false, false));
        assertEquals(4, lock.secondsRemaining());
        assertTrue(lock.update(5, 0, false, false));
        assertTrue(lock.isUnlocked());
        assertFalse(lock.update(6, 0, false, false));
        assertFalse(lock.update(0, 0, false, false)); // inputs going "back" never re-lock
        assertTrue(lock.isUnlocked());
        assertEquals(0, lock.secondsRemaining());
    }

    @Test
    public void trackerSwitchesToImageRuleWhenVideoFails() {
        LockPolicy lock = new LockPolicy(REWARDED, true, 15);
        assertFalse(lock.update(3, 3, false, false));
        assertEquals(12, lock.secondsRemaining());
        assertFalse(lock.update(4, 3, false, true));
        assertEquals(16, lock.secondsRemaining()); // 20 - 4 visible
        assertTrue(lock.update(20, 3, false, true));
    }

    @Test
    public void trackerUnlocksWhenVideoEnds() {
        LockPolicy lock = new LockPolicy(REWARDED, true, 15);
        assertFalse(lock.update(14, 14.8, false, false));
        assertTrue(lock.update(14.9, 14.8, true, false));
    }
}
