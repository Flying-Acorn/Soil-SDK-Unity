// Turns a creative into decoded, ready-to-show media (validation rules in NativeAds/PROTOCOL.md "Creative JSON").
#import <UIKit/UIKit.h>
#import "SoilAdsTypes.h"
#import "SoilAdsCreative.h"

NS_ASSUME_NONNULL_BEGIN

/// A slot's content: what `loaded` reported plus the decoded pixels.
@interface SoilAdsLoadedMedia : NSObject
@property (nonatomic, readonly) SoilAdsFormat format;
@property (nonatomic, readonly) SoilAdsCreative *creative;
@property (nonatomic, readonly) SoilAdsMedia media;
/// Video only, else 0.
@property (nonatomic, readonly) long long durationMs;
/// Set when media == video.
@property (nonatomic, readonly, nullable) NSURL *videoURL;
/// The image ad, or the video's poster / fallback.
@property (nonatomic, readonly, nullable) UIImage *image;
@property (nonatomic, readonly, nullable) UIImage *logo;

- (instancetype)initWithFormat:(SoilAdsFormat)format
                      creative:(SoilAdsCreative *)creative
                         media:(SoilAdsMedia)media
                    durationMs:(long long)durationMs
                      videoURL:(nullable NSURL *)videoURL
                         image:(nullable UIImage *)image
                          logo:(nullable UIImage *)logo;
@end

typedef void (^SoilAdsLoadCompletion)(SoilAdsLoadedMedia *_Nullable media,
                                      NSString *_Nullable errorCode,
                                      NSString *_Nullable message);

@interface SoilAdsMediaLoader : NSObject

/// Decodes on a background queue; `completion` runs on the main queue. Call from the main thread.
+ (void)loadCreative:(SoilAdsCreative *)creative
              format:(SoilAdsFormat)format
          completion:(SoilAdsLoadCompletion)completion;

/// Synchronous ImageIO decode, downsampled so the longest side is at most `maxPixelSize` pixels.
/// Returns nil for a missing path or an undecodable file. Safe off the main thread.
+ (nullable UIImage *)decodeImageAtPath:(nullable NSString *)path
                           maxPixelSize:(CGFloat)maxPixelSize
                                  scale:(CGFloat)scale;

/// Calls `completion` (on an arbitrary queue) with the duration in seconds, or a negative value
/// when the file is not a playable video with a video track and a positive duration.
+ (void)validateVideoAtPath:(nullable NSString *)path completion:(void (^)(double durationSeconds))completion;

@end

NS_ASSUME_NONNULL_END
