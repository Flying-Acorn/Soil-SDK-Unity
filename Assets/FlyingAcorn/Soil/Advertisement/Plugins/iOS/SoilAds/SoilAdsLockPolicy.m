#import "SoilAdsLockPolicy.h"
#include <math.h>

static double SoilAdsFiniteOrZero(double value)
{
    return isfinite(value) ? value : 0;
}

double SoilAdsVideoLockSeconds(SoilAdsLockSettings settings, double durationSeconds)
{
    double duration = fmax(0, SoilAdsFiniteOrZero(durationSeconds));
    double lock = fmax(SoilAdsFiniteOrZero(settings.minVideoLockSeconds),
                       SoilAdsFiniteOrZero(settings.videoLockFraction) * duration);
    return fmin(fmax(lock, 0), duration);
}

BOOL SoilAdsIsUnlocked(SoilAdsLockSettings settings, BOOL isVideo, double durationSeconds,
                       double visibleSeconds, double positionSeconds, BOOL videoEnded, BOOL videoFailed)
{
    visibleSeconds = SoilAdsFiniteOrZero(visibleSeconds);
    positionSeconds = SoilAdsFiniteOrZero(positionSeconds);
    if (visibleSeconds >= settings.maxLockSeconds) return YES;
    if (isVideo && !videoFailed)
        return videoEnded || positionSeconds >= SoilAdsVideoLockSeconds(settings, durationSeconds);
    return visibleSeconds >= settings.imageLockSeconds;
}

NSInteger SoilAdsSecondsRemaining(SoilAdsLockSettings settings, BOOL isVideo, double durationSeconds,
                                  double visibleSeconds, double positionSeconds, BOOL videoFailed)
{
    visibleSeconds = SoilAdsFiniteOrZero(visibleSeconds);
    positionSeconds = SoilAdsFiniteOrZero(positionSeconds);
    double remaining = (isVideo && !videoFailed)
        ? SoilAdsVideoLockSeconds(settings, durationSeconds) - positionSeconds
        : settings.imageLockSeconds - visibleSeconds;
    remaining = fmin(remaining, settings.maxLockSeconds - visibleSeconds);
    remaining = ceil(remaining);
    if (!(remaining > 0)) return 0; // also catches NaN
    if (remaining > (double)NSIntegerMax) return NSIntegerMax;
    return (NSInteger)remaining;
}

@implementation SoilAdsLockPolicy {
    BOOL _rewardTaken;
}

- (instancetype)initWithSettings:(SoilAdsLockSettings)settings
                         isVideo:(BOOL)isVideo
                   videoDuration:(double)durationSeconds
                        rewarded:(BOOL)rewarded
{
    if ((self = [super init])) {
        _settings = settings;
        _isVideo = isVideo;
        _videoDuration = fmax(0, SoilAdsFiniteOrZero(durationSeconds));
        _rewarded = rewarded;
        [self evaluate];
    }
    return self;
}

- (BOOL)evaluate
{
    if (_unlocked) return NO;
    _unlocked = SoilAdsIsUnlocked(_settings, _isVideo, _videoDuration, _visibleSeconds, _positionSeconds,
                                  _videoEnded, _videoFailed);
    return _unlocked;
}

- (BOOL)updateWithVisibleSeconds:(double)visibleSeconds
                 positionSeconds:(double)positionSeconds
                      videoEnded:(BOOL)videoEnded
                     videoFailed:(BOOL)videoFailed
{
    _visibleSeconds = fmax(0, SoilAdsFiniteOrZero(visibleSeconds));
    _positionSeconds = fmax(0, SoilAdsFiniteOrZero(positionSeconds));
    _videoEnded = _videoEnded || videoEnded;
    _videoFailed = _videoFailed || videoFailed;
    return [self evaluate];
}

- (NSInteger)secondsRemaining
{
    if (_unlocked) return 0;
    return SoilAdsSecondsRemaining(_settings, _isVideo, _videoDuration, _visibleSeconds, _positionSeconds,
                                   _videoFailed);
}

- (BOOL)takeReward
{
    if (!_rewarded || !_unlocked || _rewardTaken) return NO;
    _rewardTaken = YES;
    return YES;
}

@end
