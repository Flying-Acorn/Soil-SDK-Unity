#import "SoilAdsCreative.h"
#include <math.h>

static NSDictionary *SoilAdsJSONObject(NSString *json)
{
    if (![json isKindOfClass:[NSString class]] || json.length == 0) return nil;
    NSData *data = [json dataUsingEncoding:NSUTF8StringEncoding];
    if (!data) return nil;
    NSError *error = nil;
    id object = [NSJSONSerialization JSONObjectWithData:data options:0 error:&error];
    if (![object isKindOfClass:[NSDictionary class]]) return nil;
    return object;
}

static NSString *SoilAdsString(NSDictionary *dictionary, NSString *key, BOOL trim)
{
    id value = dictionary[key];
    NSString *text = nil;
    if ([value isKindOfClass:[NSString class]]) text = value;
    else if ([value isKindOfClass:[NSNumber class]]) text = [value stringValue];
    if (!text) return @"";
    return trim ? [text stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]] : [text copy];
}

/// A non-negative finite number from a JSON number or numeric string, else `fallback`.
static double SoilAdsNumber(NSDictionary *dictionary, NSString *key, double fallback)
{
    id value = dictionary[key];
    double number;
    if ([value isKindOfClass:[NSNumber class]]) {
        number = [value doubleValue];
    } else if ([value isKindOfClass:[NSString class]]) {
        NSScanner *scanner = [NSScanner scannerWithString:value];
        if (![scanner scanDouble:&number]) return fallback;
    } else {
        return fallback;
    }
    return (isfinite(number) && number >= 0) ? number : fallback;
}

static BOOL SoilAdsBool(NSDictionary *dictionary, NSString *key, BOOL fallback)
{
    id value = dictionary[key];
    if ([value isKindOfClass:[NSNumber class]]) return [value boolValue];
    if ([value isKindOfClass:[NSString class]]) {
        NSString *lower = [value lowercaseString];
        if ([lower isEqualToString:@"true"] || [lower isEqualToString:@"1"]) return YES;
        if ([lower isEqualToString:@"false"] || [lower isEqualToString:@"0"]) return NO;
    }
    return fallback;
}

@implementation SoilAdsCreative

+ (instancetype)creativeWithJSON:(NSString *)json
{
    NSDictionary *dictionary = SoilAdsJSONObject(json);
    return dictionary ? [self creativeWithDictionary:dictionary] : nil;
}

+ (instancetype)creativeWithDictionary:(NSDictionary *)dictionary
{
    SoilAdsCreative *creative = [[self alloc] init];
    if (![dictionary isKindOfClass:[NSDictionary class]]) dictionary = @{};
    creative->_adId = SoilAdsString(dictionary, @"adId", YES);
    creative->_videoPath = SoilAdsString(dictionary, @"videoPath", YES);
    creative->_imagePath = SoilAdsString(dictionary, @"imagePath", YES);
    creative->_logoPath = SoilAdsString(dictionary, @"logoPath", YES);
    creative->_title = SoilAdsString(dictionary, @"title", NO);
    creative->_adDescription = SoilAdsString(dictionary, @"description", NO);
    creative->_callToAction = SoilAdsString(dictionary, @"callToAction", NO);
    creative->_clickUrl = SoilAdsString(dictionary, @"clickUrl", YES);
    return creative;
}

- (NSString *)description
{
    return [NSString stringWithFormat:@"<SoilAdsCreative %@ video=%@ image=%@>", _adId, _videoPath, _imagePath];
}

@end

@implementation SoilAdsShowOptions

+ (instancetype)defaultsForFormat:(SoilAdsFormat)format
{
    SoilAdsShowOptions *options = [[self alloc] init];
    options->_format = format;
    options->_position = SoilAdsBannerPositionBottom;
    options->_startMuted = NO;
    if (format == SoilAdsFormatRewarded) {
        options->_imageLockSeconds = 20;
        options->_videoLockFraction = 1.0;
        options->_minVideoLockSeconds = 0;
    } else {
        options->_imageLockSeconds = 5;
        options->_videoLockFraction = 0.8;
        options->_minVideoLockSeconds = 5;
    }
    return options;
}

+ (instancetype)optionsWithJSON:(NSString *)json format:(SoilAdsFormat)format
{
    SoilAdsShowOptions *options = [self defaultsForFormat:format];
    NSDictionary *dictionary = SoilAdsJSONObject(json);
    if (!dictionary) return options;

    NSString *position = [SoilAdsString(dictionary, @"position", YES) lowercaseString];
    if ([position isEqualToString:@"top"]) options->_position = SoilAdsBannerPositionTop;
    else if ([position isEqualToString:@"center"]) options->_position = SoilAdsBannerPositionCenter;
    else options->_position = SoilAdsBannerPositionBottom;

    options->_imageLockSeconds = SoilAdsNumber(dictionary, @"imageLockSeconds", options->_imageLockSeconds);
    options->_videoLockFraction = SoilAdsNumber(dictionary, @"videoLockFraction", options->_videoLockFraction);
    options->_minVideoLockSeconds = SoilAdsNumber(dictionary, @"minVideoLockSeconds", options->_minVideoLockSeconds);
    options->_startMuted = SoilAdsBool(dictionary, @"startMuted", options->_startMuted);
    return options;
}

- (SoilAdsLockSettings)lockSettings
{
    SoilAdsLockSettings settings;
    settings.imageLockSeconds = _imageLockSeconds;
    settings.videoLockFraction = _videoLockFraction;
    settings.minVideoLockSeconds = _minVideoLockSeconds;
    return settings;
}

@end
