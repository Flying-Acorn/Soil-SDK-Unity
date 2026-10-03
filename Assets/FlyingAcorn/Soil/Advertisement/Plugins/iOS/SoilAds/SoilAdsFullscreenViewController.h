// Interstitial / rewarded player ("Fullscreen presentation" in NativeAds/PROTOCOL.md).
#import <UIKit/UIKit.h>
#import "SoilAdsMediaLoader.h"
#import "SoilAdsCreative.h"
#import "SoilAdsLockPolicy.h"

NS_ASSUME_NONNULL_BEGIN

@class AVPlayer;
@class SoilAdsFullscreenViewController;

@protocol SoilAdsFullscreenDelegate <NSObject>
/// The unlocked close button was tapped.
- (void)fullscreenControllerDidRequestClose:(SoilAdsFullscreenViewController *)controller;
/// Media or call to action tapped. The controller has paused itself; call -resumeAfterClick if nothing opened.
- (void)fullscreenControllerDidClick:(SoilAdsFullscreenViewController *)controller;
/// Rewarded ads: the lock opened for the first time.
- (void)fullscreenControllerDidEarnReward:(SoilAdsFullscreenViewController *)controller;
/// The controller left the screen by being dismissed (by us or by anyone else).
- (void)fullscreenControllerDidDisappear:(SoilAdsFullscreenViewController *)controller;
/// One of the controller's callbacks threw: the show must end (see -[SoilAdsManager recoverFromFailure]).
- (void)fullscreenControllerDidFail:(SoilAdsFullscreenViewController *)controller;
@end

@interface SoilAdsFullscreenViewController : UIViewController

- (instancetype)initWithContent:(SoilAdsLoadedMedia *)content
                        options:(SoilAdsShowOptions *)options
          supportedOrientations:(UIInterfaceOrientationMask)orientations NS_DESIGNATED_INITIALIZER;
- (instancetype)initWithNibName:(nullable NSString *)nibNameOrNil bundle:(nullable NSBundle *)nibBundleOrNil NS_UNAVAILABLE;
- (instancetype)initWithCoder:(NSCoder *)coder NS_UNAVAILABLE;

@property (nonatomic, weak, nullable) id<SoilAdsFullscreenDelegate> delegate;
@property (nonatomic, readonly) SoilAdsLoadedMedia *content;
@property (nonatomic, readonly) SoilAdsLockPolicy *lockPolicy;

@property (nonatomic, readonly) UIButton *closeButton;
@property (nonatomic, readonly, nullable) UIButton *muteButton;
@property (nonatomic, readonly, nullable) UIButton *callToActionButton;
@property (nonatomic, readonly) UIView *mediaView;
/// Logo, title, description and call to action under the media; nil when the ad has none of them.
@property (nonatomic, readonly, nullable) UIView *infoCard;
/// The blurred, screen-filling copy of the ad's image behind the media; nil without an image.
@property (nonatomic, readonly, nullable) UIImageView *backdropView;
/// Video only.
@property (nonatomic, readonly, nullable) AVPlayer *player;

/// Continues the visible clock and the video after a click that opened nothing.
- (void)resumeAfterClick;
/// Stops the clock, the video and all observers. Idempotent; no delegate calls afterwards.
- (void)teardown;

/// Test hook: the callback (`create` or `tick`) that throws, to prove the show still ends cleanly.
@property (class, nonatomic, copy, nullable) NSString *failAtForTests;

@end

NS_ASSUME_NONNULL_END
