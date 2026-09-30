#import <XCTest/XCTest.h>
#import "SoilAdsLockPolicy.h"

static SoilAdsLockSettings S(double image, double fraction, double minVideo, double max)
{
    SoilAdsLockSettings s = {image, fraction, minVideo, max};
    return s;
}

// C# defaults from PROTOCOL.md.
static SoilAdsLockSettings Interstitial(void) { return S(5, 0.8, 5, 60); }
static SoilAdsLockSettings Rewarded(void) { return S(20, 1.0, 0, 60); }

@interface SoilAdsLockPolicyTests : XCTestCase
@end

@implementation SoilAdsLockPolicyTests

#pragma mark - videoLock = clamp(max(minVideoLockSeconds, videoLockFraction * D), 0, D)

- (void)testVideoLockTable
{
    struct { SoilAdsLockSettings s; double duration; double expected; } rows[] = {
        {Interstitial(), 15, 12},      // fraction wins
        {Interstitial(), 30, 24},
        {Interstitial(), 6, 5},        // minimum wins (0.8 * 6 = 4.8)
        {Interstitial(), 3, 3},        // minimum clamped to the duration
        {Interstitial(), 0, 0},
        {Interstitial(), -4, 0},       // negative duration treated as 0
        {Interstitial(), NAN, 0},
        {Interstitial(), INFINITY, 0},
        {Rewarded(), 15, 15},          // the whole video
        {Rewarded(), 0.5, 0.5},
        {S(5, 0.5, 0, 60), 10, 5},
        {S(5, 2.0, 0, 60), 10, 10},    // fraction above 1 clamped to D
        {S(5, 0, 100, 60), 10, 10},    // min above D clamped to D
        {S(5, 0, 0, 60), 10, 0},       // no lock at all
        {S(5, -1, -1, 60), 10, 0},     // negative settings clamp to 0
    };
    for (size_t i = 0; i < sizeof(rows) / sizeof(rows[0]); i++) {
        XCTAssertEqualWithAccuracy(SoilAdsVideoLockSeconds(rows[i].s, rows[i].duration), rows[i].expected, 1e-9,
                                   @"row %zu", i);
    }
}

#pragma mark - unlocked

- (void)testImageRule
{
    SoilAdsLockSettings s = Interstitial();
    XCTAssertFalse(SoilAdsIsUnlocked(s, NO, 0, 0, 0, NO, NO));
    XCTAssertFalse(SoilAdsIsUnlocked(s, NO, 0, 4.999, 0, NO, NO));
    XCTAssertTrue(SoilAdsIsUnlocked(s, NO, 0, 5, 0, NO, NO));
    XCTAssertTrue(SoilAdsIsUnlocked(s, NO, 0, 30, 0, NO, NO));
    // Position, ended and a video duration mean nothing for image media.
    XCTAssertFalse(SoilAdsIsUnlocked(s, NO, 15, 1, 100, YES, NO));
    XCTAssertFalse(SoilAdsIsUnlocked(Rewarded(), NO, 0, 19.9, 0, NO, NO));
    XCTAssertTrue(SoilAdsIsUnlocked(Rewarded(), NO, 0, 20, 0, NO, NO));
    XCTAssertTrue(SoilAdsIsUnlocked(S(0, 0.8, 5, 60), NO, 0, 0, 0, NO, NO)); // no image lock
}

- (void)testVideoPositionRule
{
    SoilAdsLockSettings s = Interstitial(); // D = 15 -> lock 12
    XCTAssertFalse(SoilAdsIsUnlocked(s, YES, 15, 0, 0, NO, NO));
    XCTAssertFalse(SoilAdsIsUnlocked(s, YES, 15, 30, 11.99, NO, NO)); // visible time is irrelevant below max
    XCTAssertTrue(SoilAdsIsUnlocked(s, YES, 15, 0, 12, NO, NO));
    XCTAssertTrue(SoilAdsIsUnlocked(s, YES, 15, 0, 14, NO, NO));
    // Rewarded: the whole video.
    XCTAssertFalse(SoilAdsIsUnlocked(Rewarded(), YES, 15, 14.9, 14.9, NO, NO));
    XCTAssertTrue(SoilAdsIsUnlocked(Rewarded(), YES, 15, 15, 15, NO, NO));
}

- (void)testVideoEndedUnlocks
{
    XCTAssertTrue(SoilAdsIsUnlocked(Interstitial(), YES, 15, 0, 3, YES, NO));
    // The last frame may report a position a hair short of D.
    XCTAssertTrue(SoilAdsIsUnlocked(Rewarded(), YES, 15, 14.97, 14.97, YES, NO));
}

- (void)testMinVideoLock
{
    SoilAdsLockSettings s = Interstitial(); // D = 4 -> max(5, 3.2) = 5 -> clamped to 4
    XCTAssertFalse(SoilAdsIsUnlocked(s, YES, 4, 0, 3.9, NO, NO));
    XCTAssertTrue(SoilAdsIsUnlocked(s, YES, 4, 0, 4, NO, NO));
    SoilAdsLockSettings m = S(5, 0.1, 5, 60); // D = 20 -> max(5, 2) = 5
    XCTAssertFalse(SoilAdsIsUnlocked(m, YES, 20, 0, 4.9, NO, NO));
    XCTAssertTrue(SoilAdsIsUnlocked(m, YES, 20, 0, 5, NO, NO));
}

- (void)testZeroLengthVideoUnlocksImmediately
{
    XCTAssertTrue(SoilAdsIsUnlocked(Interstitial(), YES, 0, 0, 0, NO, NO));
}

- (void)testSafetyNet
{
    SoilAdsLockSettings s = Rewarded(); // a 100 s rewarded video would lock 100 s
    XCTAssertFalse(SoilAdsIsUnlocked(s, YES, 100, 59.99, 59.99, NO, NO));
    XCTAssertTrue(SoilAdsIsUnlocked(s, YES, 100, 60, 10, NO, NO));
    XCTAssertTrue(SoilAdsIsUnlocked(S(100, 1, 0, 60), NO, 0, 60, 0, NO, NO));
    XCTAssertTrue(SoilAdsIsUnlocked(S(100, 1, 0, 0), NO, 0, 0, 0, NO, NO)); // max 0: never locked
    XCTAssertTrue(SoilAdsIsUnlocked(S(100, 1, 0, 60), YES, 100, 60, 0, NO, YES));
}

- (void)testVideoFailedSwitchesToImageRule
{
    SoilAdsLockSettings s = Interstitial();
    XCTAssertFalse(SoilAdsIsUnlocked(s, YES, 15, 4.9, 14, NO, YES)); // position no longer counts
    XCTAssertTrue(SoilAdsIsUnlocked(s, YES, 15, 5, 0, NO, YES));
    XCTAssertFalse(SoilAdsIsUnlocked(s, YES, 15, 1, 0, YES, YES)); // ended does not count either
}

- (void)testNonFiniteInputsDoNotUnlock
{
    XCTAssertFalse(SoilAdsIsUnlocked(Interstitial(), NO, 0, NAN, 0, NO, NO));
    XCTAssertFalse(SoilAdsIsUnlocked(Interstitial(), YES, 15, 0, NAN, NO, NO));
}

#pragma mark - secondsRemaining

- (void)testCountdownImage
{
    SoilAdsLockSettings s = Interstitial();
    XCTAssertEqual(SoilAdsSecondsRemaining(s, NO, 0, 0, 0, NO), 5);
    XCTAssertEqual(SoilAdsSecondsRemaining(s, NO, 0, 0.1, 0, NO), 5);  // ceil(4.9)
    XCTAssertEqual(SoilAdsSecondsRemaining(s, NO, 0, 1, 0, NO), 4);
    XCTAssertEqual(SoilAdsSecondsRemaining(s, NO, 0, 4.2, 0, NO), 1);
    XCTAssertEqual(SoilAdsSecondsRemaining(s, NO, 0, 5, 0, NO), 0);
    XCTAssertEqual(SoilAdsSecondsRemaining(s, NO, 0, 7.5, 0, NO), 0);  // never negative
    XCTAssertEqual(SoilAdsSecondsRemaining(s, NO, 0, 1000, 0, NO), 0);
}

- (void)testCountdownVideo
{
    SoilAdsLockSettings s = Interstitial(); // lock 12 of 15
    XCTAssertEqual(SoilAdsSecondsRemaining(s, YES, 15, 0, 0, NO), 12);
    XCTAssertEqual(SoilAdsSecondsRemaining(s, YES, 15, 3, 0.5, NO), 12); // follows playback, not the clock
    XCTAssertEqual(SoilAdsSecondsRemaining(s, YES, 15, 0, 11.5, NO), 1);
    XCTAssertEqual(SoilAdsSecondsRemaining(s, YES, 15, 0, 12, NO), 0);
    XCTAssertEqual(SoilAdsSecondsRemaining(s, YES, 15, 0, 14, NO), 0);
}

- (void)testCountdownCappedByMaxLock
{
    // Rewarded 100 s video: 100 - 0 capped to 60 - 0.
    XCTAssertEqual(SoilAdsSecondsRemaining(Rewarded(), YES, 100, 0, 0, NO), 60);
    // Stalled video: position 10, visible 50 -> min(90, 10).
    XCTAssertEqual(SoilAdsSecondsRemaining(Rewarded(), YES, 100, 50, 10, NO), 10);
    XCTAssertEqual(SoilAdsSecondsRemaining(Rewarded(), YES, 100, 59.5, 10, NO), 1);
    XCTAssertEqual(SoilAdsSecondsRemaining(Rewarded(), YES, 100, 61, 10, NO), 0);
    // Image lock above max.
    XCTAssertEqual(SoilAdsSecondsRemaining(S(90, 1, 0, 30), NO, 0, 0, 0, NO), 30);
    XCTAssertEqual(SoilAdsSecondsRemaining(S(90, 1, 0, 0), NO, 0, 0, 0, NO), 0);
}

- (void)testCountdownAfterVideoFailure
{
    SoilAdsLockSettings s = Interstitial();
    XCTAssertEqual(SoilAdsSecondsRemaining(s, YES, 15, 1, 1, YES), 4); // image rule: 5 - 1
    XCTAssertEqual(SoilAdsSecondsRemaining(s, YES, 15, 6, 1, YES), 0);
}

- (void)testCountdownNonFinite
{
    XCTAssertEqual(SoilAdsSecondsRemaining(Interstitial(), NO, 0, NAN, 0, NO), 5);
    XCTAssertEqual(SoilAdsSecondsRemaining(Interstitial(), YES, NAN, 0, 0, NO), 0);
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
    XCTAssertTrue(p.unlocked);
    XCTAssertEqual(p.secondsRemaining, 0);
    XCTAssertFalse([p updateWithVisibleSeconds:6 positionSeconds:0 videoEnded:NO videoFailed:NO]);
    XCTAssertFalse([p updateWithVisibleSeconds:0 positionSeconds:0 videoEnded:NO videoFailed:NO]);
    XCTAssertTrue(p.unlocked, @"once unlocked it stays unlocked");
}

- (void)testRewardOnceOnFirstUnlock
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
    SoilAdsLockPolicy *p = [[SoilAdsLockPolicy alloc] initWithSettings:S(0, 0, 0, 60) isVideo:NO videoDuration:0 rewarded:NO];
    XCTAssertTrue(p.unlocked);
    XCTAssertFalse([p takeReward]);
}

- (void)testUnlockedFromTheStartStillRewardsOnce
{
    SoilAdsLockPolicy *p = [[SoilAdsLockPolicy alloc] initWithSettings:S(0, 0, 0, 0) isVideo:NO videoDuration:0 rewarded:YES];
    XCTAssertTrue(p.unlocked);
    XCTAssertTrue([p takeReward]);
    XCTAssertFalse([p takeReward]);
}

- (void)testMidPlayFailureContinuesUnderImageRuleFromFirstShown
{
    SoilAdsLockPolicy *p = [[SoilAdsLockPolicy alloc] initWithSettings:Interstitial() isVideo:YES videoDuration:15 rewarded:NO];
    [p updateWithVisibleSeconds:3 positionSeconds:3 videoEnded:NO videoFailed:NO];
    XCTAssertEqual(p.secondsRemaining, 9);
    [p updateWithVisibleSeconds:3.2 positionSeconds:3 videoEnded:NO videoFailed:YES];
    XCTAssertTrue(p.videoFailed);
    XCTAssertEqual(p.secondsRemaining, 2, @"5 s image lock measured from when the ad was first shown");
    // The failure is sticky even if a later sample says otherwise.
    XCTAssertFalse([p updateWithVisibleSeconds:4.9 positionSeconds:14 videoEnded:NO videoFailed:NO]);
    XCTAssertTrue(p.videoFailed);
    XCTAssertTrue([p updateWithVisibleSeconds:5 positionSeconds:3 videoEnded:NO videoFailed:NO]);
}

- (void)testEndedIsSticky
{
    SoilAdsLockPolicy *p = [[SoilAdsLockPolicy alloc] initWithSettings:Rewarded() isVideo:YES videoDuration:15 rewarded:NO];
    XCTAssertTrue([p updateWithVisibleSeconds:1 positionSeconds:1 videoEnded:YES videoFailed:NO]);
    [p updateWithVisibleSeconds:1 positionSeconds:0 videoEnded:NO videoFailed:NO];
    XCTAssertTrue(p.videoEnded);
    XCTAssertTrue(p.unlocked);
}

- (void)testCountdownNeverNegativeOverTime
{
    SoilAdsLockPolicy *p = [[SoilAdsLockPolicy alloc] initWithSettings:Interstitial() isVideo:YES videoDuration:15 rewarded:NO];
    NSInteger previous = p.secondsRemaining;
    for (double t = 0; t <= 20; t += 0.25) {
        [p updateWithVisibleSeconds:t positionSeconds:t videoEnded:NO videoFailed:NO];
        XCTAssertGreaterThanOrEqual(p.secondsRemaining, 0);
        XCTAssertLessThanOrEqual(p.secondsRemaining, previous, @"countdown only goes down");
        previous = p.secondsRemaining;
    }
    XCTAssertTrue(p.unlocked);
}

- (void)testNegativeAndNonFiniteMeasurementsAreSanitized
{
    SoilAdsLockPolicy *p = [[SoilAdsLockPolicy alloc] initWithSettings:Interstitial() isVideo:YES videoDuration:NAN rewarded:NO];
    XCTAssertEqual(p.videoDuration, 0);
    XCTAssertTrue(p.unlocked, @"a video without a usable duration has no video lock");
    SoilAdsLockPolicy *q = [[SoilAdsLockPolicy alloc] initWithSettings:Interstitial() isVideo:NO videoDuration:0 rewarded:NO];
    [q updateWithVisibleSeconds:-3 positionSeconds:NAN videoEnded:NO videoFailed:NO];
    XCTAssertEqual(q.visibleSeconds, 0);
    XCTAssertEqual(q.positionSeconds, 0);
    XCTAssertEqual(q.secondsRemaining, 5);
}

@end
