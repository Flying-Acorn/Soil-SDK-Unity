// Fullscreen close-button lock ("Fullscreen lock policy" in NativeAds/PROTOCOL.md).
// Pure logic, no UIKit. Mirrors Logic/FullscreenLockPolicy.cs on the C# side.
#import <Foundation/Foundation.h>
#import "SoilAdsTypes.h"

NS_ASSUME_NONNULL_BEGIN

typedef struct {
    double imageLockSeconds;
    double videoLockFraction;
    double minVideoLockSeconds;
    double maxLockSeconds;
} SoilAdsLockSettings;

/// clamp(max(minVideoLockSeconds, videoLockFraction * D), 0, D)
FOUNDATION_EXPORT double SoilAdsVideoLockSeconds(SoilAdsLockSettings settings, double durationSeconds);

FOUNDATION_EXPORT BOOL SoilAdsIsUnlocked(SoilAdsLockSettings settings, BOOL isVideo, double durationSeconds,
                                         double visibleSeconds, double positionSeconds,
                                         BOOL videoEnded, BOOL videoFailed);

/// The countdown number: never below 0, never above ceil(maxLockSeconds - visibleSeconds).
FOUNDATION_EXPORT NSInteger SoilAdsSecondsRemaining(SoilAdsLockSettings settings, BOOL isVideo, double durationSeconds,
                                                    double visibleSeconds, double positionSeconds, BOOL videoFailed);

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
