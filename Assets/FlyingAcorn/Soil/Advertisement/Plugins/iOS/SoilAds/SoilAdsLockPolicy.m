#import "SoilAdsLockPolicy.h"
#include <math.h>

static double SoilAdsFiniteOrZero(double value)
{
    return isfinite(value) ? value : 0;
}

double SoilAdsVideoLockSeconds(SoilAdsLockSettings settings, double durationSeconds)
{
    double duration = fmax(0, SoilAdsFiniteOrZero(durationSeconds));
    return fmax(0, fmax(SoilAdsFiniteOrZero(settings.minVideoLockSeconds),
                        SoilAdsFiniteOrZero(settings.videoLockFraction) * duration));
}

double SoilAdsScreenLockSeconds(SoilAdsLockSettings settings, BOOL isVideo, double durationSeconds)
{
    double image = SoilAdsFiniteOrZero(settings.imageLockSeconds);
    return isVideo ? fmax(SoilAdsVideoLockSeconds(settings, durationSeconds), image) : image;
}

/// A video of unknown length cannot be followed; it is timed like a failed one.
static BOOL SoilAdsPlaysVideo(BOOL isVideo, double durationSeconds, BOOL videoFailed)
{
    return isVideo && !videoFailed && SoilAdsFiniteOrZero(durationSeconds) > 0;
}

/// After its end a video keeps counting on-screen time, so a short one still unlocks at its lock.
static double SoilAdsProgress(double durationSeconds, double visibleSeconds, double positionSeconds, BOOL videoEnded)
{
    return videoEnded ? fmax(SoilAdsFiniteOrZero(durationSeconds), visibleSeconds) : positionSeconds;
}

BOOL SoilAdsIsUnlocked(SoilAdsLockSettings settings, BOOL isVideo, double durationSeconds,
                       double visibleSeconds, double positionSeconds, BOOL videoEnded, BOOL videoFailed)
{
    visibleSeconds = SoilAdsFiniteOrZero(visibleSeconds);
    positionSeconds = SoilAdsFiniteOrZero(positionSeconds);
    if (visibleSeconds >= SoilAdsScreenLockSeconds(settings, isVideo, durationSeconds)) return YES;
    return SoilAdsPlaysVideo(isVideo, durationSeconds, videoFailed)
        && SoilAdsProgress(durationSeconds, visibleSeconds, positionSeconds, videoEnded)
           >= SoilAdsVideoLockSeconds(settings, durationSeconds);
}

NSInteger SoilAdsSecondsRemaining(SoilAdsLockSettings settings, BOOL isVideo, double durationSeconds,
                                  double visibleSeconds, double positionSeconds, BOOL videoEnded, BOOL videoFailed)
{
    visibleSeconds = SoilAdsFiniteOrZero(visibleSeconds);
    positionSeconds = SoilAdsFiniteOrZero(positionSeconds);
    double remaining = SoilAdsScreenLockSeconds(settings, isVideo, durationSeconds) - visibleSeconds;
    if (SoilAdsPlaysVideo(isVideo, durationSeconds, videoFailed)) {
        remaining = fmin(remaining, SoilAdsVideoLockSeconds(settings, durationSeconds)
                                    - SoilAdsProgress(durationSeconds, visibleSeconds, positionSeconds, videoEnded));
    }
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
                                   _videoEnded, _videoFailed);
}

- (BOOL)takeReward
{
    if (!_rewarded || !_unlocked || _rewardTaken) return NO;
    _rewardTaken = YES;
    return YES;
}

@end
