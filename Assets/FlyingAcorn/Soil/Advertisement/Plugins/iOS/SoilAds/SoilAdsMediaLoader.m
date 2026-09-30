#import "SoilAdsMediaLoader.h"
#import <AVFoundation/AVFoundation.h>
#import <ImageIO/ImageIO.h>
#include <math.h>

@implementation SoilAdsLoadedMedia

- (instancetype)initWithFormat:(SoilAdsFormat)format
                      creative:(SoilAdsCreative *)creative
                         media:(SoilAdsMedia)media
                    durationMs:(long long)durationMs
                      videoURL:(NSURL *)videoURL
                         image:(UIImage *)image
                          logo:(UIImage *)logo
{
    if ((self = [super init])) {
        _format = format;
        _creative = creative;
        _media = media;
        _durationMs = durationMs;
        _videoURL = videoURL;
        _image = image;
        _logo = logo;
    }
    return self;
}

@end

@implementation SoilAdsMediaLoader

+ (UIImage *)decodeImageAtPath:(NSString *)path maxPixelSize:(CGFloat)maxPixelSize scale:(CGFloat)scale
{
    if (path.length == 0) return nil;
    if (![[NSFileManager defaultManager] isReadableFileAtPath:path]) return nil;
    NSURL *url = [NSURL fileURLWithPath:path];
    NSDictionary *sourceOptions = @{(__bridge NSString *)kCGImageSourceShouldCache: @NO};
    CGImageSourceRef source = CGImageSourceCreateWithURL((__bridge CFURLRef)url, (__bridge CFDictionaryRef)sourceOptions);
    if (!source) return nil;
    CGImageRef image = NULL;
    if (CGImageSourceGetCount(source) > 0) {
        NSDictionary *options = @{
            (__bridge NSString *)kCGImageSourceCreateThumbnailFromImageAlways: @YES,
            (__bridge NSString *)kCGImageSourceCreateThumbnailWithTransform: @YES,
            (__bridge NSString *)kCGImageSourceShouldCacheImmediately: @YES,
            (__bridge NSString *)kCGImageSourceThumbnailMaxPixelSize: @((NSInteger)fmax(1, ceil(maxPixelSize))),
        };
        image = CGImageSourceCreateThumbnailAtIndex(source, 0, (__bridge CFDictionaryRef)options);
    }
    CFRelease(source);
    if (!image) return nil;
    UIImage *result = nil;
    if (CGImageGetWidth(image) > 0 && CGImageGetHeight(image) > 0)
        result = [UIImage imageWithCGImage:image scale:(scale > 0 ? scale : 1) orientation:UIImageOrientationUp];
    CGImageRelease(image);
    return result;
}

+ (void)validateVideoAtPath:(NSString *)path completion:(void (^)(double))completion
{
    if (path.length == 0 || ![[NSFileManager defaultManager] isReadableFileAtPath:path]) {
        completion(-1);
        return;
    }
    AVURLAsset *asset = [AVURLAsset URLAssetWithURL:[NSURL fileURLWithPath:path]
                                            options:@{AVURLAssetPreferPreciseDurationAndTimingKey: @YES}];
    NSArray<NSString *> *keys = @[@"playable", @"duration", @"tracks"];
    [asset loadValuesAsynchronouslyForKeys:keys completionHandler:^{
        for (NSString *key in keys) {
            NSError *error = nil;
            if ([asset statusOfValueForKey:key error:&error] != AVKeyValueStatusLoaded) {
                SoilAdsLog(@"video %@ not loaded: %@", key, (error ? error.localizedDescription : @""));
                completion(-1);
                return;
            }
        }
        double duration = CMTimeGetSeconds(asset.duration);
        BOOL hasVideoTrack = [asset tracksWithMediaType:AVMediaTypeVideo].count > 0;
        if (!asset.playable || !hasVideoTrack || !isfinite(duration) || duration <= 0) {
            completion(-1);
            return;
        }
        completion(duration);
    }];
}

+ (void)loadCreative:(SoilAdsCreative *)creative format:(SoilAdsFormat)format completion:(SoilAdsLoadCompletion)completion
{
    UIScreen *screen = [UIScreen mainScreen];
    CGFloat scale = screen.scale > 0 ? screen.scale : 1;
    CGFloat maxPixels = fmax(screen.bounds.size.width, screen.bounds.size.height) * scale;
    CGFloat logoPixels = 128 * scale;
    BOOL fullscreen = SoilAdsFormatIsFullscreen(format);

    void (^finish)(SoilAdsLoadedMedia *, NSString *, NSString *) = ^(SoilAdsLoadedMedia *media, NSString *code, NSString *message) {
        dispatch_async(dispatch_get_main_queue(), ^{ completion(media, code, message); });
    };

    dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
        @autoreleasepool {
            UIImage *image = [self decodeImageAtPath:creative.imagePath maxPixelSize:maxPixels scale:scale];
            UIImage *logo = [self decodeImageAtPath:creative.logoPath maxPixelSize:logoPixels scale:scale];
            if (creative.logoPath.length > 0 && !logo) SoilAdsLog(@"logo ignored (unreadable)");
            if (creative.imagePath.length > 0 && !image) SoilAdsLog(@"image unreadable");

            // Without a usable video: image, then text (banner only), else fail.
            SoilAdsLoadedMedia * (^withoutVideo)(void) = ^SoilAdsLoadedMedia *{
                if (image)
                    return [[SoilAdsLoadedMedia alloc] initWithFormat:format creative:creative media:SoilAdsMediaImage
                                                           durationMs:0 videoURL:nil image:image logo:logo];
                if (format == SoilAdsFormatBanner && creative.title.length > 0)
                    return [[SoilAdsLoadedMedia alloc] initWithFormat:format creative:creative media:SoilAdsMediaText
                                                           durationMs:0 videoURL:nil image:nil logo:logo];
                return nil;
            };
            void (^failOrFallBack)(void) = ^{
                SoilAdsLoadedMedia *media = withoutVideo();
                if (media) { finish(media, nil, nil); return; }
                BOOL mediaGiven = creative.imagePath.length > 0 || (fullscreen && creative.videoPath.length > 0);
                if (mediaGiven) finish(nil, SoilAdsErrorMediaUnreadable, @"media file missing or undecodable");
                else finish(nil, SoilAdsErrorInvalidCreative, @"creative has no media");
            };

            if (fullscreen && creative.videoPath.length > 0) {
                [self validateVideoAtPath:creative.videoPath completion:^(double duration) {
                    if (duration > 0) {
                        long long durationMs = llround(duration * 1000.0);
                        NSURL *url = [NSURL fileURLWithPath:creative.videoPath];
                        finish([[SoilAdsLoadedMedia alloc] initWithFormat:format creative:creative media:SoilAdsMediaVideo
                                                               durationMs:durationMs videoURL:url image:image logo:logo],
                               nil, nil);
                    } else {
                        SoilAdsLog(@"video unreadable, trying the image");
                        failOrFallBack();
                    }
                }];
            } else {
                failOrFallBack();
            }
        }
    });
}

@end
