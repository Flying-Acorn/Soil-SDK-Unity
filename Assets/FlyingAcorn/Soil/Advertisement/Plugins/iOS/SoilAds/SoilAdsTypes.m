#import "SoilAdsTypes.h"

#if !__has_feature(objc_arc)
#error "SoilAds must be compiled with ARC (-fobjc-arc)"
#endif

NSString *const SoilAdsErrorInvalidFormat = @"invalid_format";
NSString *const SoilAdsErrorInvalidCreative = @"invalid_creative";
NSString *const SoilAdsErrorMediaUnreadable = @"media_unreadable";
NSString *const SoilAdsErrorNotLoaded = @"not_loaded";
NSString *const SoilAdsErrorAlreadyShowing = @"already_showing";
NSString *const SoilAdsErrorNoHost = @"no_host";
NSString *const SoilAdsErrorInternal = @"internal";

SoilAdsFormat SoilAdsFormatFromString(NSString *name)
{
    if (![name isKindOfClass:[NSString class]]) return SoilAdsFormatInvalid;
    if ([name isEqualToString:@"banner"]) return SoilAdsFormatBanner;
    if ([name isEqualToString:@"interstitial"]) return SoilAdsFormatInterstitial;
    if ([name isEqualToString:@"rewarded"]) return SoilAdsFormatRewarded;
    return SoilAdsFormatInvalid;
}

NSString *SoilAdsFormatName(SoilAdsFormat format)
{
    switch (format) {
        case SoilAdsFormatBanner: return @"banner";
        case SoilAdsFormatInterstitial: return @"interstitial";
        case SoilAdsFormatRewarded: return @"rewarded";
        default: return @"";
    }
}

NSString *SoilAdsMediaName(SoilAdsMedia media)
{
    switch (media) {
        case SoilAdsMediaVideo: return @"video";
        case SoilAdsMediaImage: return @"image";
        case SoilAdsMediaText: return @"text";
        default: return @"";
    }
}

BOOL SoilAdsFormatIsFullscreen(SoilAdsFormat format)
{
    return format == SoilAdsFormatInterstitial || format == SoilAdsFormatRewarded;
}

BOOL SoilAdsGuard(NSString *what, dispatch_block_t block)
{
    @try {
        if (block) block();
        return YES;
    } @catch (NSException *exception) {
        SoilAdsLog(@"%@ failed: %@ (%@)", what, exception.name, exception.reason);
        return NO;
    }
}
