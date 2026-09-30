// Creative JSON (`load`) and show options JSON (`show`) from NativeAds/PROTOCOL.md.
#import <Foundation/Foundation.h>
#import "SoilAdsTypes.h"
#import "SoilAdsLockPolicy.h"

NS_ASSUME_NONNULL_BEGIN

/// Every string property is non-nil; a missing, null or non-string field becomes @"".
@interface SoilAdsCreative : NSObject

@property (nonatomic, copy, readonly) NSString *adId;
@property (nonatomic, copy, readonly) NSString *videoPath;
@property (nonatomic, copy, readonly) NSString *imagePath;
@property (nonatomic, copy, readonly) NSString *logoPath;
@property (nonatomic, copy, readonly) NSString *title;
/// The JSON `description` field.
@property (nonatomic, copy, readonly) NSString *adDescription;
@property (nonatomic, copy, readonly) NSString *callToAction;
@property (nonatomic, copy, readonly) NSString *clickUrl;

/// nil when the text is not a JSON object.
+ (nullable instancetype)creativeWithJSON:(nullable NSString *)json;
+ (instancetype)creativeWithDictionary:(NSDictionary *)dictionary;

@end

@interface SoilAdsShowOptions : NSObject

@property (nonatomic, readonly) SoilAdsFormat format;
@property (nonatomic, readonly) SoilAdsBannerPosition position;
@property (nonatomic, readonly) double imageLockSeconds;
@property (nonatomic, readonly) double videoLockFraction;
@property (nonatomic, readonly) double minVideoLockSeconds;
@property (nonatomic, readonly) double maxLockSeconds;
@property (nonatomic, readonly) BOOL startMuted;
@property (nonatomic, readonly) SoilAdsLockSettings lockSettings;

/// The values C# sends by default: interstitial 5 / 0.8 / 5 / 60, rewarded 20 / 1.0 / 0 / 60, banner bottom.
+ (instancetype)defaultsForFormat:(SoilAdsFormat)format;
/// Missing, invalid or negative fields keep the defaults; bad JSON gives the defaults.
+ (instancetype)optionsWithJSON:(nullable NSString *)json format:(SoilAdsFormat)format;

@end

NS_ASSUME_NONNULL_END
