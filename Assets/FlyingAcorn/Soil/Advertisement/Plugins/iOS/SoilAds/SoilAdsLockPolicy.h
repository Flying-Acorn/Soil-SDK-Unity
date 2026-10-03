// Fullscreen close-button lock ("Fullscreen lock policy" in NativeAds/PROTOCOL.md): the timing the
// Unity-drawn ads always had (interstitial 5 s, or 80% of a video but never under 5 s; rewarded
// 20 s, or the whole video), counting only time the ad is on screen and never longer than
// maxLockSeconds (interstitial 15 s). Pure logic, no UIKit.
// Mirrors Logic/FullscreenLockPolicy.cs on the C# side.
#import <Foundation/Foundation.h>
#import "SoilAdsTypes.h"

NS_ASSUME_NONNULL_BEGIN

typedef struct {
    double imageLockSeconds;
    double videoLockFraction;
    double minVideoLockSeconds;
    /// No lock lasts longer than this; 0 for no cap.
    double maxLockSeconds;
} SoilAdsLockSettings;

/// Playback needed to unlock a video: max(minVideoLockSeconds, videoLockFraction * D); may outlast a short video.
FOUNDATION_EXPORT double SoilAdsVideoLockSeconds(SoilAdsLockSettings settings, double durationSeconds);

/// On-screen time that unlocks regardless of playback; also the net under a stalled or failed video.
FOUNDATION_EXPORT double SoilAdsScreenLockSeconds(SoilAdsLockSettings settings, BOOL isVideo, double durationSeconds);

FOUNDATION_EXPORT BOOL SoilAdsIsUnlocked(SoilAdsLockSettings settings, BOOL isVideo, double durationSeconds,
                                         double visibleSeconds, double positionSeconds,
                                         BOOL videoEnded, BOOL videoFailed);

/// The countdown number: the closer of the two rules, never below 0.
FOUNDATION_EXPORT NSInteger SoilAdsSecondsRemaining(SoilAdsLockSettings settings, BOOL isVideo, double durationSeconds,
                                                    double visibleSeconds, double positionSeconds,
                                                    BOOL videoEnded, BOOL videoFailed);

/// Stateful tracker for one show: unlock is sticky, a failed/ended video stays failed/ended,
/// and the reward is handed out once.
@interface SoilAdsLockPolicy : NSObject

- (instancetype)initWithSettings:(SoilAdsLockSettings)settings
                         isVideo:(BOOL)isVideo
                   videoDuration:(double)durationSeconds
                        rewarded:(BOOL)rewarded NS_DESIGNATED_INITIALIZER;
- (instancetype)init NS_UNAVAILABLE;

@property (nonatomic, readonly) SoilAdsLockSettings settings;
@property (nonatomic, readonly) BOOL isVideo;
@property (nonatomic, readonly) double videoDuration;
@property (nonatomic, readonly) BOOL rewarded;

@property (nonatomic, readonly) double visibleSeconds;
@property (nonatomic, readonly) double positionSeconds;
@property (nonatomic, readonly) BOOL videoEnded;
@property (nonatomic, readonly) BOOL videoFailed;

@property (nonatomic, readonly) BOOL unlocked;
/// 0 once unlocked.
@property (nonatomic, readonly) NSInteger secondsRemaining;

/// Feeds the latest measurements. `videoEnded`/`videoFailed` are sticky once set.
/// Returns YES only on the call that unlocks.
- (BOOL)updateWithVisibleSeconds:(double)visibleSeconds
                 positionSeconds:(double)positionSeconds
                      videoEnded:(BOOL)videoEnded
                     videoFailed:(BOOL)videoFailed;

/// YES exactly once per show: for rewarded ads, after the first unlock.
- (BOOL)takeReward;

@end

NS_ASSUME_NONNULL_END
