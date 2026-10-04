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

/// Runs `block`, catching any Objective-C exception it throws so it cannot take the game down
/// (UIKit and the players' callbacks run on the game's main thread, where an uncaught exception
/// ends the app). Returns NO, after logging what failed, when one was thrown. Hard crashes (a
/// signal in the OS media stack) cannot be caught by any app code.
/// Unity compiles plugins with Objective-C exceptions disabled, so SoilAdsTypes.m alone is built
/// with -fobjc-exceptions (its .meta CompileFlags); keep @try/@catch/@throw out of every other file.
FOUNDATION_EXPORT BOOL SoilAdsGuard(NSString *what, dispatch_block_t block);

NS_ASSUME_NONNULL_END
