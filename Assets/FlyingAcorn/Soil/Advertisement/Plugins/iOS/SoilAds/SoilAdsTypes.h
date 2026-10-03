// Shared vocabulary of the Soil native ad player (see NativeAds/PROTOCOL.md).
#import <Foundation/Foundation.h>

NS_ASSUME_NONNULL_BEGIN

typedef NS_ENUM(NSInteger, SoilAdsFormat) {
    SoilAdsFormatInvalid = 0,
    SoilAdsFormatBanner,
    SoilAdsFormatInterstitial,
    SoilAdsFormatRewarded,
};

typedef NS_ENUM(NSInteger, SoilAdsMedia) {
    SoilAdsMediaNone = 0,
    SoilAdsMediaVideo,
    SoilAdsMediaImage,
    SoilAdsMediaText,
};

typedef NS_ENUM(NSInteger, SoilAdsBannerPosition) {
    SoilAdsBannerPositionBottom = 0,
    SoilAdsBannerPositionTop,
    SoilAdsBannerPositionCenter,
};

FOUNDATION_EXPORT NSString *const SoilAdsErrorInvalidFormat;
FOUNDATION_EXPORT NSString *const SoilAdsErrorInvalidCreative;
FOUNDATION_EXPORT NSString *const SoilAdsErrorMediaUnreadable;
FOUNDATION_EXPORT NSString *const SoilAdsErrorNotLoaded;
FOUNDATION_EXPORT NSString *const SoilAdsErrorAlreadyShowing;
FOUNDATION_EXPORT NSString *const SoilAdsErrorNoHost;
FOUNDATION_EXPORT NSString *const SoilAdsErrorInternal;

/// Parses a protocol format name (`banner`, `interstitial`, `rewarded`); anything else is Invalid.
FOUNDATION_EXPORT SoilAdsFormat SoilAdsFormatFromString(NSString *_Nullable name);
FOUNDATION_EXPORT NSString *SoilAdsFormatName(SoilAdsFormat format);
FOUNDATION_EXPORT NSString *SoilAdsMediaName(SoilAdsMedia media);
FOUNDATION_EXPORT BOOL SoilAdsFormatIsFullscreen(SoilAdsFormat format);

/// The first argument must be a string literal.
#define SoilAdsLog(...) NSLog(@"[SoilAds] " __VA_ARGS__)

NS_ASSUME_NONNULL_END
