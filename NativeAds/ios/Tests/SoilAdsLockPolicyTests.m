#import <XCTest/XCTest.h>
#import "SoilAdsLockPolicy.h"

// "Fullscreen lock policy" in PROTOCOL.md, pinned to the timing the Unity-drawn ads always had.
// Same cases as FullscreenLockPolicyTests.cs and LockPolicyTest.java.

static SoilAdsLockSettings S(double image, double fraction, double minVideo)
{
    SoilAdsLockSettings s = {image, fraction, minVideo};
    return s;
}

static SoilAdsLockSettings Interstitial(void)
{
    SoilAdsLockSettings s = S(5, 0.8, 5);
    s.maxLockSeconds = 15;
    return s;
}
static SoilAdsLockSettings Rewarded(void) { return S(20, 1.0, 0); }

static BOOL Unlocked(SoilAdsLockSettings s, BOOL video, double duration, double visible, double position,
                     BOOL ended, BOOL failed)
{
    return SoilAdsIsUnlocked(s, video, duration, visible, position, ended, failed);
}

static NSInteger Remaining(SoilAdsLockSettings s, BOOL video, double duration, double visible, double position,
                           BOOL ended, BOOL failed)
{
    return SoilAdsSecondsRemaining(s, video, duration, visible, position, ended, failed);
}

@interface SoilAdsLockPolicyTests : XCTestCase
@end

@implementation SoilAdsLockPolicyTests

- (void)testInterstitialImageUnlocksAfterFiveSeconds
{
    XCTAssertFalse(Unlocked(Interstitial(), NO, 0, 4.9, 0, NO, NO));
    XCTAssertTrue(Unlocked(Interstitial(), NO, 0, 5, 0, NO, NO));
}

- (void)testRewardedImageUnlocksAfterTwentySeconds
{
    XCTAssertFalse(Unlocked(Rewarded(), NO, 0, 19.9, 0, NO, NO));
    XCTAssertTrue(Unlocked(Rewarded(), NO, 0, 20, 0, NO, NO));
}

- (void)testInterstitialVideoUnlocksAtEightyPercent
{
    XCTAssertEqualWithAccuracy(SoilAdsVideoLockSeconds(Interstitial(), 15), 12, 1e-9);
    XCTAssertFalse(Unlocked(Interstitial(), YES, 15, 11.9, 11.9, NO, NO));
    XCTAssertTrue(Unlocked(Interstitial(), YES, 15, 12, 12, NO, NO));
}

- (void)testInterstitialIsAlwaysClosableBy15Seconds
{
    // Google Play: interstitials must be closable after 15 s. 80% of a 30 s video would be 24 s.
    XCTAssertEqualWithAccuracy(SoilAdsVideoLockSeconds(Interstitial(), 30), 15, 1e-9);
    XCTAssertEqualWithAccuracy(SoilAdsScreenLockSeconds(Interstitial(), YES, 30), 15, 1e-9);
    XCTAssertFalse(Unlocked(Interstitial(), YES, 30, 14.9, 14.9, NO, NO));
    XCTAssertTrue(Unlocked(Interstitial(), YES, 30, 15, 15, NO, NO));
    XCTAssertTrue(Unlocked(Interstitial(), YES, 30, 15, 2, NO, NO), @"a stalled video too");
    XCTAssertEqual(Remaining(Interstitial(), YES, 30, 0, 0, NO, NO), 15);
    SoilAdsLockSettings longImage = Interstitial();
    longImage.imageLockSeconds = 20;
    XCTAssertTrue(Unlocked(longImage, NO, 0, 15, 0, NO, NO), @"the cap holds for any interstitial");
}

- (void)testRewardedIsNotCappedAndNoCapMeansTheFullLock
{
    XCTAssertEqualWithAccuracy(SoilAdsVideoLockSeconds(Rewarded(), 30), 30, 1e-9);
    XCTAssertFalse(Unlocked(Rewarded(), YES, 30, 29, 29, NO, NO));
    SoilAdsLockSettings uncapped = Interstitial();
    uncapped.maxLockSeconds = 0;
    XCTAssertEqualWithAccuracy(SoilAdsVideoLockSeconds(uncapped, 30), 24, 1e-9);
}

- (void)testInterstitialVideoShorterThanFiveSecondsStillLocksFiveSeconds
{
    XCTAssertEqualWithAccuracy(SoilAdsVideoLockSeconds(Interstitial(), 3), 5, 1e-9);
    XCTAssertFalse(Unlocked(Interstitial(), YES, 3, 3.1, 3, YES, NO));
    XCTAssertEqual(Remaining(Interstitial(), YES, 3, 3.1, 3, YES, NO), 2);
    XCTAssertTrue(Unlocked(Interstitial(), YES, 3, 5, 3, YES, NO));
}

- (void)testRewardedVideoUnlocksAtTheEndEvenWhenShorterThanTwentySeconds
{
    XCTAssertEqualWithAccuracy(SoilAdsVideoLockSeconds(Rewarded(), 15), 15, 1e-9);
    XCTAssertFalse(Unlocked(Rewarded(), YES, 15, 14.9, 14.9, NO, NO));
    XCTAssertTrue(Unlocked(Rewarded(), YES, 15, 15, 15, NO, NO));
    XCTAssertTrue(Unlocked(Rewarded(), YES, 15, 14, 14.5, YES, NO), @"position lags the end");
    XCTAssertTrue(Unlocked(Rewarded(), YES, 3, 3, 3, YES, NO));
}

- (void)testRewardedVideoLongerThanAMinuteIsWatchedToTheEnd
{
    XCTAssertFalse(Unlocked(Rewarded(), YES, 90, 70, 70, NO, NO));
    XCTAssertTrue(Unlocked(Rewarded(), YES, 90, 90, 90, NO, NO));
}

- (void)testStalledVideoUnlocksWhenTheCountdownEnds
{
    XCTAssertFalse(Unlocked(Rewarded(), YES, 15, 19.9, 1, NO, NO));
    XCTAssertTrue(Unlocked(Rewarded(), YES, 15, 20, 1, NO, NO));
    XCTAssertTrue(Unlocked(Rewarded(), YES, 30, 30, 1, NO, NO));
    XCTAssertTrue(Unlocked(Interstitial(), YES, 20, 16, 1, NO, NO));
}

- (void)testFailedVideoUnlocksWhenTheCountdownEnds
{
    XCTAssertFalse(Unlocked(Rewarded(), YES, 15, 19, 2, NO, YES));
    XCTAssertTrue(Unlocked(Rewarded(), YES, 15, 20, 2, NO, YES));
    XCTAssertTrue(Unlocked(Interstitial(), YES, 3, 5, 1, NO, YES));
}

- (void)testVideoOfUnknownLengthIsTimedLikeAnImage
{
    XCTAssertFalse(Unlocked(Rewarded(), YES, 0, 19, 5, NO, NO));
    XCTAssertTrue(Unlocked(Rewarded(), YES, 0, 20, 5, NO, NO));
    XCTAssertFalse(Unlocked(Rewarded(), YES, NAN, 19, 5, NO, NO));
}

- (void)testCountdownFollowsWhicheverRuleIsCloser
{
    XCTAssertEqual(Remaining(Interstitial(), YES, 15, 0, 0, NO, NO), 12);
    XCTAssertEqual(Remaining(Interstitial(), YES, 15, 11.5, 11.5, NO, NO), 1);
    XCTAssertEqual(Remaining(Interstitial(), YES, 15, 13, 13, NO, NO), 0);
    XCTAssertEqual(Remaining(Rewarded(), YES, 15, 10, 10, NO, NO), 5);
    XCTAssertEqual(Remaining(Rewarded(), YES, 15, 15, 1, NO, NO), 5);
    XCTAssertEqual(Remaining(Rewarded(), YES, 30, 10, 0, NO, YES), 20);
}

- (void)testCountdownForImagesFollowsVisibleTime
{
    XCTAssertEqual(Remaining(Interstitial(), NO, 0, 0, 0, NO, NO), 5);
    XCTAssertEqual(Remaining(Interstitial(), NO, 0, 2.2, 0, NO, NO), 3);
    XCTAssertEqual(Remaining(Rewarded(), NO, 0, 0, 0, NO, NO), 20);
}

- (void)testCountdownIsNeverNegative
{
    XCTAssertEqual(Remaining(Interstitial(), NO, 0, 100, 0, NO, NO), 0);
    XCTAssertEqual(Remaining(Rewarded(), YES, 15, 40, 15, YES, NO), 0);
    XCTAssertEqual(Remaining(Interstitial(), NO, 0, NAN, NAN, NO, NO), 5);
}

#pragma mark - Stateful tracker

- (void)testUnlockIsStickyAndReportedOnce
{
    SoilAdsLockPolicy *p = [[SoilAdsLockPolicy alloc] initWithSettings:Interstitial() isVideo:NO videoDuration:0 rewarded:NO];
    XCTAssertFalse(p.unlocked);
    XCTAssertEqual(p.secondsRemaining, 5);
    XCTAssertFalse([p updateWithVisibleSeconds:2 positionSeconds:0 videoEnded:NO videoFailed:NO]);
    XCTAssertEqual(p.secondsRemaining, 3);
    XCTAssertTrue([p updateWithVisibleSeconds:5 positionSeconds:0 videoEnded:NO videoFailed:NO]);
    XCTAssertEqual(p.secondsRemaining, 0);
    XCTAssertFalse([p updateWithVisibleSeconds:6 positionSeconds:0 videoEnded:NO videoFailed:NO]);
    XCTAssertFalse([p updateWithVisibleSeconds:0 positionSeconds:0 videoEnded:NO videoFailed:NO]);
    XCTAssertTrue(p.unlocked, @"once unlocked it stays unlocked");
}

- (void)testRewardOnceWhenTheVideoEnds
{
    SoilAdsLockPolicy *p = [[SoilAdsLockPolicy alloc] initWithSettings:Rewarded() isVideo:YES videoDuration:10 rewarded:YES];
    XCTAssertFalse([p takeReward]);
    [p updateWithVisibleSeconds:5 positionSeconds:5 videoEnded:NO videoFailed:NO];
    XCTAssertFalse([p takeReward]);
    XCTAssertTrue([p updateWithVisibleSeconds:10 positionSeconds:9.98 videoEnded:YES videoFailed:NO]);
    XCTAssertTrue([p takeReward]);
    XCTAssertFalse([p takeReward]);
    [p updateWithVisibleSeconds:20 positionSeconds:10 videoEnded:YES videoFailed:NO];
    XCTAssertFalse([p takeReward]);
}

- (void)testInterstitialNeverRewards
{
    SoilAdsLockPolicy *p = [[SoilAdsLockPolicy alloc] initWithSettings:S(0, 0, 0) isVideo:NO videoDuration:0 rewarded:NO];
    XCTAssertTrue(p.unlocked);
    XCTAssertFalse([p takeReward]);
}

- (void)testUnlockedFromTheStartStillRewardsOnce
{
    SoilAdsLockPolicy *p = [[SoilAdsLockPolicy alloc] initWithSettings:S(0, 0, 0) isVideo:NO videoDuration:0 rewarded:YES];
    XCTAssertTrue(p.unlocked);
    XCTAssertTrue([p takeReward]);
    XCTAssertFalse([p takeReward]);
}

- (void)testMidPlayFailureFallsBackToTheCountdown
{
    SoilAdsLockPolicy *p = [[SoilAdsLockPolicy alloc] initWithSettings:Rewarded() isVideo:YES videoDuration:15 rewarded:YES];
    [p updateWithVisibleSeconds:3 positionSeconds:3 videoEnded:NO videoFailed:NO];
    XCTAssertEqual(p.secondsRemaining, 12);
    [p updateWithVisibleSeconds:4 positionSeconds:3 videoEnded:NO videoFailed:YES];
    XCTAssertEqual(p.secondsRemaining, 16, @"20 s countdown from when the ad was first shown");
    XCTAssertFalse([p updateWithVisibleSeconds:19.9 positionSeconds:14 videoEnded:NO videoFailed:NO], @"failure is sticky");
    XCTAssertTrue([p updateWithVisibleSeconds:20 positionSeconds:3 videoEnded:NO videoFailed:NO]);
    XCTAssertTrue([p takeReward]);
}

- (void)testShortInterstitialVideoKeepsCountingAfterItEnds
{
    SoilAdsLockPolicy *p = [[SoilAdsLockPolicy alloc] initWithSettings:Interstitial() isVideo:YES videoDuration:3 rewarded:NO];
    XCTAssertFalse([p updateWithVisibleSeconds:3 positionSeconds:3 videoEnded:YES videoFailed:NO]);
    XCTAssertEqual(p.secondsRemaining, 2);
    XCTAssertFalse([p updateWithVisibleSeconds:4 positionSeconds:0 videoEnded:NO videoFailed:NO], @"ended is sticky");
    XCTAssertTrue([p updateWithVisibleSeconds:5 positionSeconds:0 videoEnded:NO videoFailed:NO]);
}

- (void)testCountdownOnlyGoesDown
{
    SoilAdsLockPolicy *p = [[SoilAdsLockPolicy alloc] initWithSettings:Interstitial() isVideo:YES videoDuration:15 rewarded:NO];
    NSInteger previous = p.secondsRemaining;
    for (double t = 0; t <= 20; t += 0.25) {
        [p updateWithVisibleSeconds:t positionSeconds:t videoEnded:NO videoFailed:NO];
        XCTAssertGreaterThanOrEqual(p.secondsRemaining, 0);
        XCTAssertLessThanOrEqual(p.secondsRemaining, previous);
        previous = p.secondsRemaining;
    }
    XCTAssertTrue(p.unlocked);
}

- (void)testUnusableDurationIsTimedLikeAnImage
{
    SoilAdsLockPolicy *p = [[SoilAdsLockPolicy alloc] initWithSettings:Rewarded() isVideo:YES videoDuration:NAN rewarded:YES];
    XCTAssertEqual(p.videoDuration, 0);
    XCTAssertFalse(p.unlocked, @"no instant reward for a video of unknown length");
    XCTAssertEqual(p.secondsRemaining, 20);
    SoilAdsLockPolicy *q = [[SoilAdsLockPolicy alloc] initWithSettings:Interstitial() isVideo:NO videoDuration:0 rewarded:NO];
    [q updateWithVisibleSeconds:-3 positionSeconds:NAN videoEnded:NO videoFailed:NO];
    XCTAssertEqual(q.visibleSeconds, 0);
    XCTAssertEqual(q.positionSeconds, 0);
    XCTAssertEqual(q.secondsRemaining, 5);
}

@end
