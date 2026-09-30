// Slots, load/show/hide/destroy and events ("Slots" and "Events" in NativeAds/PROTOCOL.md).
// Knows nothing about Unity: the host and the event sink are injected.
#import <UIKit/UIKit.h>
#import "SoilAdsTypes.h"

NS_ASSUME_NONNULL_BEGIN

@class SoilAdsFullscreenViewController;
@class SoilAdsBannerView;

@protocol SoilAdsHost <NSObject>
/// The view controller banners attach to and fullscreen ads are presented from.
- (nullable UIViewController *)soilAdsRootViewController;
/// YES right before a fullscreen ad is presented, NO after it is dismissed; always paired.
/// The host should leave the game as it found it (not resume a game that was already paused).
- (void)soilAdsSetGamePaused:(BOOL)paused;
@optional
/// Whether the app is active. Defaults to UIApplication's applicationState. A fullscreen show made
/// while inactive waits for UIApplicationDidBecomeActiveNotification.
- (BOOL)soilAdsAppIsActive;
/// Opens a click URL. Defaults to -[UIApplication openURL:options:completionHandler:].
- (void)soilAdsOpenURL:(NSURL *)url completion:(void (^)(BOOL opened))completion;
@end

/// Receives one protocol event JSON object per call, always on the main thread.
typedef void (^SoilAdsEventSink)(NSString *json);

@interface SoilAdsManager : NSObject

- (instancetype)initWithHost:(id<SoilAdsHost>)host NS_DESIGNATED_INITIALIZER;
- (instancetype)init NS_UNAVAILABLE;

@property (nonatomic, readonly) id<SoilAdsHost> host;
@property (atomic, copy, nullable) SoilAdsEventSink eventSink;

// Main thread only. Unknown or nil formats are reported as invalid_format.
- (void)loadFormat:(nullable NSString *)format creativeJSON:(nullable NSString *)json;
- (void)showFormat:(nullable NSString *)format optionsJSON:(nullable NSString *)json;
- (void)hideFormat:(nullable NSString *)format;
- (void)destroyFormat:(nullable NSString *)format;

/// Any thread.
- (BOOL)isReady:(nullable NSString *)format;

/// The URL a click opens: http, https, itms-apps or itms-appss only, percent-encoded if needed.
/// nil when missing, unparseable or any other scheme.
+ (nullable NSURL *)clickURLFromString:(nullable NSString *)text;

/// What is on screen right now (for tests and diagnostics).
@property (nonatomic, readonly, nullable) SoilAdsFullscreenViewController *fullscreenController;
@property (nonatomic, readonly, nullable) SoilAdsBannerView *bannerView;

@end

NS_ASSUME_NONNULL_END
