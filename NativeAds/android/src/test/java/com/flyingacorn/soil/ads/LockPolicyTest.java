package com.flyingacorn.soil.ads;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

/**
 * "Fullscreen lock policy" in PROTOCOL.md, pinned to the timing the Unity-drawn ads always had.
 * Same cases as FullscreenLockPolicyTests.cs and SoilAdsLockPolicyTests.m.
 */
public class LockPolicyTest {
    private static final double EPS = 1e-9;
    private static final ShowOptions INTERSTITIAL = ShowOptions.defaults("interstitial"); // 5 / 0.8 / 5
    private static final ShowOptions REWARDED = ShowOptions.defaults("rewarded");         // 20 / 1.0 / 0

    private static boolean unlocked(ShowOptions o, boolean video, double duration, double visible, double position,
                                    boolean ended, boolean failed) {
        return LockPolicy.isUnlocked(o, video, duration, visible, position, ended, failed);
    }

    private static int remaining(ShowOptions o, boolean video, double duration, double visible, double position,
                                 boolean ended, boolean failed) {
        return LockPolicy.secondsRemaining(o, video, duration, visible, position, ended, failed);
    }

    @Test
    public void interstitialImageUnlocksAfterFiveSeconds() {
        assertFalse(unlocked(INTERSTITIAL, false, 0, 4.9, 0, false, false));
        assertTrue(unlocked(INTERSTITIAL, false, 0, 5, 0, false, false));
    }

    @Test
    public void rewardedImageUnlocksAfterTwentySeconds() {
        assertFalse(unlocked(REWARDED, false, 0, 19.9, 0, false, false));
        assertTrue(unlocked(REWARDED, false, 0, 20, 0, false, false));
    }

    @Test
    public void interstitialVideoUnlocksAtEightyPercent() {
        assertEquals(16, LockPolicy.videoLockSeconds(INTERSTITIAL, 20), EPS);
        assertFalse(unlocked(INTERSTITIAL, true, 20, 15.9, 15.9, false, false));
        assertTrue(unlocked(INTERSTITIAL, true, 20, 16, 16, false, false));
    }

    @Test
    public void interstitialVideoShorterThanFiveSecondsStillLocksFiveSeconds() {
        assertEquals(5, LockPolicy.videoLockSeconds(INTERSTITIAL, 3), EPS);
        assertFalse(unlocked(INTERSTITIAL, true, 3, 3.1, 3, true, false));
        assertEquals(2, remaining(INTERSTITIAL, true, 3, 3.1, 3, true, false));
        assertTrue(unlocked(INTERSTITIAL, true, 3, 5, 3, true, false));
    }

    @Test
    public void rewardedVideoUnlocksAtTheEndEvenWhenShorterThanTwentySeconds() {
        assertEquals(15, LockPolicy.videoLockSeconds(REWARDED, 15), EPS);
        assertFalse(unlocked(REWARDED, true, 15, 14.9, 14.9, false, false));
        assertTrue(unlocked(REWARDED, true, 15, 15, 15, false, false));
        assertTrue(unlocked(REWARDED, true, 15, 14, 14.5, true, false)); // position lags the end
        assertTrue(unlocked(REWARDED, true, 3, 3, 3, true, false));
    }

    @Test
    public void rewardedVideoLongerThanAMinuteIsWatchedToTheEnd() {
        assertFalse(unlocked(REWARDED, true, 90, 70, 70, false, false));
        assertTrue(unlocked(REWARDED, true, 90, 90, 90, false, false));
    }

    @Test
    public void stalledVideoUnlocksWhenTheCountdownEnds() {
        assertFalse(unlocked(REWARDED, true, 15, 19.9, 1, false, false));
        assertTrue(unlocked(REWARDED, true, 15, 20, 1, false, false));
        assertTrue(unlocked(REWARDED, true, 30, 30, 1, false, false));
        assertTrue(unlocked(INTERSTITIAL, true, 20, 16, 1, false, false));
    }

    @Test
    public void failedVideoUnlocksWhenTheCountdownEnds() {
        assertFalse(unlocked(REWARDED, true, 15, 19, 2, false, true));
        assertTrue(unlocked(REWARDED, true, 15, 20, 2, false, true));
        assertTrue(unlocked(INTERSTITIAL, true, 3, 5, 1, false, true));
    }

    @Test
    public void videoOfUnknownLengthIsTimedLikeAnImage() {
        assertFalse(unlocked(REWARDED, true, 0, 19, 5, false, false));
        assertTrue(unlocked(REWARDED, true, 0, 20, 5, false, false));
    }

    @Test
    public void countdownFollowsWhicheverRuleIsCloser() {
        assertEquals(16, remaining(INTERSTITIAL, true, 20, 0, 0, false, false));
        assertEquals(1, remaining(INTERSTITIAL, true, 20, 15.5, 15.5, false, false));
        assertEquals(0, remaining(INTERSTITIAL, true, 20, 17, 17, false, false));
        assertEquals(5, remaining(REWARDED, true, 15, 10, 10, false, false));
        assertEquals(5, remaining(REWARDED, true, 15, 15, 1, false, false));
        assertEquals(20, remaining(REWARDED, true, 30, 10, 0, false, true));
    }

    @Test
    public void countdownForImagesFollowsVisibleTime() {
        assertEquals(5, remaining(INTERSTITIAL, false, 0, 0, 0, false, false));
        assertEquals(3, remaining(INTERSTITIAL, false, 0, 2.2, 0, false, false));
        assertEquals(20, remaining(REWARDED, false, 0, 0, 0, false, false));
    }

    @Test
    public void countdownIsNeverNegative() {
        assertEquals(0, remaining(INTERSTITIAL, false, 0, 100, 0, false, false));
        assertEquals(0, remaining(REWARDED, true, 15, 40, 15, true, false));
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
    public void trackerFallsBackToTheCountdownWhenVideoFails() {
        LockPolicy lock = new LockPolicy(REWARDED, true, 15);
        assertFalse(lock.update(3, 3, false, false));
        assertEquals(12, lock.secondsRemaining());
        assertFalse(lock.update(4, 3, false, true));
        assertEquals(16, lock.secondsRemaining()); // 20 - 4 on screen
        assertFalse(lock.update(5, 3, false, false)); // failure is sticky
        assertTrue(lock.update(20, 3, false, false));
    }

    @Test
    public void trackerUnlocksWhenRewardedVideoEnds() {
        LockPolicy lock = new LockPolicy(REWARDED, true, 15);
        assertFalse(lock.update(14, 14.8, false, false));
        assertTrue(lock.update(14.9, 14.8, true, false));
    }

    @Test
    public void trackerKeepsCountingAfterAShortInterstitialVideoEnds() {
        LockPolicy lock = new LockPolicy(INTERSTITIAL, true, 3);
        assertFalse(lock.update(3, 3, true, false));
        assertEquals(2, lock.secondsRemaining());
        assertFalse(lock.update(4, 3, false, false)); // ended is sticky
        assertTrue(lock.update(5, 3, false, false));
    }
}
