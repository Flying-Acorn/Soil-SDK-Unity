#import <XCTest/XCTest.h>
#import <AVFoundation/AVFoundation.h>
#import "SoilAdsTestSupport.h"
#import "SoilAdsManager.h"
#import "SoilAdsFullscreenViewController.h"
#import "SoilAdsBannerView.h"
#import "SoilAdsMediaLoader.h"
#import "SoilAdsUI.h"

static const NSTimeInterval Timeout = 10;

// Private actions, called directly instead of synthesizing touches.
@interface SoilAdsFullscreenViewController (Testing)
- (void)clickTapped;
@end

@interface SoilAdsBannerView (Testing)
- (void)tapped;
@end

@interface SoilAdsManagerTests : XCTestCase
@property (nonatomic, strong) UIWindow *window;
@property (nonatomic, strong) UIViewController *root;
@property (nonatomic, strong) SoilAdsFakeHost *host;
@property (nonatomic, strong) SoilAdsEventRecorder *recorder;
@property (nonatomic, strong) SoilAdsManager *manager;
@end

@implementation SoilAdsManagerTests

- (void)setUp
{
    [super setUp];
    self.continueAfterFailure = NO;
    self.window = [[UIWindow alloc] initWithFrame:[UIScreen mainScreen].bounds];
    self.root = [[UIViewController alloc] init];
    self.root.view.backgroundColor = [UIColor grayColor];
    self.window.rootViewController = self.root;
    [self.window makeKeyAndVisible];
    self.host = [[SoilAdsFakeHost alloc] init];
    self.host.root = self.root;
    self.recorder = [[SoilAdsEventRecorder alloc] init];
    self.manager = [[SoilAdsManager alloc] initWithHost:self.host];
    self.manager.eventSink = self.recorder.sink;
}

- (void)tearDown
{
    for (NSString *format in @[@"banner", @"interstitial", @"rewarded"]) [self.manager destroyFormat:format];
    SoilAdsWaitUntil(Timeout, ^BOOL { return self.root.presentedViewController == nil; });
    self.window.hidden = YES;
    self.window = nil;
    [super tearDown];
}

#pragma mark - Helpers

- (NSString *)creative:(NSDictionary *)fields
{
    NSMutableDictionary *all = [@{@"adId": @"test-ad"} mutableCopy];
    [all addEntriesFromDictionary:fields];
    return SoilAdsJSON(all);
}

- (NSDictionary *)load:(NSString *)format fields:(NSDictionary *)fields
{
    NSUInteger before = self.recorder.events.count;
    [self.manager loadFormat:format creativeJSON:[self creative:fields]];
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return self.recorder.events.count > before; }), @"no load result");
    return self.recorder.events.lastObject;
}

- (NSString *)fullscreenOptions:(double)imageLock fraction:(double)fraction minVideo:(double)minVideo
{
    return SoilAdsJSON(@{@"imageLockSeconds": @(imageLock), @"videoLockFraction": @(fraction),
                         @"minVideoLockSeconds": @(minVideo), @"maxLockSeconds": @60, @"startMuted": @NO});
}

- (void)showFullscreen:(NSString *)format options:(NSString *)options
{
    NSString *before = [self.recorder sequenceForFormat:format];
    [self.manager showFormat:format optionsJSON:options];
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL {
        NSString *now = [self.recorder sequenceForFormat:format];
        return now.length > before.length && [[now substringFromIndex:before.length] containsString:@"shown"];
    }), @"not shown");
}

- (void)waitForClosed:(NSUInteger)count
{
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return [self.recorder countOf:@"closed"] >= count; }), @"not closed");
    SoilAdsSpin(0.3); // anything extra would arrive now
    XCTAssertEqual([self.recorder countOf:@"closed"], count);
}

- (NSDictionary *)imageAd
{
    return @{@"imagePath": SoilAdsResource(@"image.png"), @"logoPath": SoilAdsResource(@"logo.jpg"),
             @"title": @"Word Master", @"description": @"Train your brain every day", @"callToAction": @"Install",
             @"clickUrl": @"https://example.com/click?x=1"};
}

#pragma mark - Load

- (void)testLoadVideo
{
    NSDictionary *e = [self load:@"interstitial" fields:@{@"videoPath": SoilAdsResource(@"video.mp4"),
                                                          @"imagePath": SoilAdsResource(@"image.png")}];
    XCTAssertEqualObjects(e[@"format"], @"interstitial");
    XCTAssertEqualObjects(e[@"event"], @"loaded");
    XCTAssertEqualObjects(e[@"media"], @"video");
    XCTAssertEqualWithAccuracy([e[@"durationMs"] doubleValue], 2500, 150);
    XCTAssertTrue([self.manager isReady:@"interstitial"]);
    XCTAssertFalse([self.manager isReady:@"rewarded"]);
}

- (void)testLoadImage
{
    NSDictionary *e = [self load:@"rewarded" fields:[self imageAd]];
    XCTAssertEqualObjects(e[@"event"], @"loaded");
    XCTAssertEqualObjects(e[@"media"], @"image");
    XCTAssertEqualObjects(e[@"durationMs"], @0);
}

- (void)testBannerIgnoresVideo
{
    NSDictionary *e = [self load:@"banner" fields:@{@"videoPath": SoilAdsResource(@"video.mp4"),
                                                    @"imagePath": SoilAdsResource(@"image.png")}];
    XCTAssertEqualObjects(e[@"media"], @"image");
    NSDictionary *f = [self load:@"banner" fields:@{@"videoPath": SoilAdsResource(@"video.mp4")}];
    XCTAssertEqualObjects(f[@"event"], @"loadFailed");
    XCTAssertEqualObjects(f[@"error"], @"invalid_creative");
}

- (void)testLoadTextBanner
{
    NSDictionary *e = [self load:@"banner" fields:@{@"title": @"Word Master", @"logoPath": SoilAdsResource(@"broken.png")}];
    XCTAssertEqualObjects(e[@"event"], @"loaded", @"a broken logo is ignored");
    XCTAssertEqualObjects(e[@"media"], @"text");
    XCTAssertEqualObjects(e[@"durationMs"], @0);
}

- (void)testTextOnlyIsNotEnoughForFullscreen
{
    NSDictionary *e = [self load:@"interstitial" fields:@{@"title": @"Word Master"}];
    XCTAssertEqualObjects(e[@"event"], @"loadFailed");
    XCTAssertEqualObjects(e[@"error"], @"invalid_creative");
    XCTAssertFalse([self.manager isReady:@"interstitial"]);
}

- (void)testBrokenVideoFallsBackToImage
{
    NSDictionary *e = [self load:@"rewarded" fields:@{@"videoPath": SoilAdsResource(@"broken.mp4"),
                                                      @"imagePath": SoilAdsResource(@"image.png")}];
    XCTAssertEqualObjects(e[@"event"], @"loaded");
    XCTAssertEqualObjects(e[@"media"], @"image");
    XCTAssertEqualObjects(e[@"durationMs"], @0);
}

- (void)testAudioOnlyFileIsNotAVideo
{
    NSDictionary *e = [self load:@"rewarded" fields:@{@"videoPath": SoilAdsResource(@"audio_only.m4a")}];
    XCTAssertEqualObjects(e[@"event"], @"loadFailed");
    XCTAssertEqualObjects(e[@"error"], @"media_unreadable");
}

- (void)testBrokenMediaFails
{
    NSDictionary *a = [self load:@"interstitial" fields:@{@"videoPath": SoilAdsResource(@"broken.mp4")}];
    XCTAssertEqualObjects(a[@"error"], @"media_unreadable");
    NSDictionary *b = [self load:@"banner" fields:@{@"imagePath": SoilAdsResource(@"broken.png")}];
    XCTAssertEqualObjects(b[@"error"], @"media_unreadable");
    NSDictionary *c = [self load:@"rewarded" fields:@{@"imagePath": @"/does/not/exist.png"}];
    XCTAssertEqualObjects(c[@"error"], @"media_unreadable");
    XCTAssertEqualObjects(c[@"event"], @"loadFailed");
    XCTAssertNotNil(c[@"message"]);
}

- (void)testNoMediaAndBadJSON
{
    NSDictionary *a = [self load:@"rewarded" fields:@{}];
    XCTAssertEqualObjects(a[@"error"], @"invalid_creative");
    [self.manager loadFormat:@"banner" creativeJSON:@"{not json"];
    XCTAssertEqualObjects(self.recorder.last[@"event"], @"loadFailed");
    XCTAssertEqualObjects(self.recorder.last[@"error"], @"invalid_creative");
    [self.manager loadFormat:@"banner" creativeJSON:nil];
    XCTAssertEqualObjects(self.recorder.last[@"error"], @"invalid_creative");
}

- (void)testInvalidFormat
{
    [self.manager loadFormat:@"native" creativeJSON:[self creative:[self imageAd]]];
    XCTAssertEqualObjects(self.recorder.last[@"event"], @"loadFailed");
    XCTAssertEqualObjects(self.recorder.last[@"error"], @"invalid_format");
    XCTAssertEqualObjects(self.recorder.last[@"format"], @"native");
    [self.manager showFormat:nil optionsJSON:nil];
    XCTAssertEqualObjects(self.recorder.last[@"event"], @"showFailed");
    XCTAssertEqualObjects(self.recorder.last[@"error"], @"invalid_format");
    XCTAssertEqualObjects(self.recorder.last[@"format"], @"");
    [self.manager hideFormat:@"nope"];
    [self.manager destroyFormat:nil];
    XCTAssertFalse([self.manager isReady:@"nope"]);
    XCTAssertFalse([self.manager isReady:nil]);
}

- (void)testOnlyTheLastLoadReports
{
    [self.manager loadFormat:@"interstitial" creativeJSON:[self creative:@{@"videoPath": SoilAdsResource(@"video.mp4")}]];
    [self.manager loadFormat:@"interstitial" creativeJSON:[self creative:[self imageAd]]];
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return self.recorder.events.count > 0; }));
    SoilAdsSpin(1.5);
    XCTAssertEqual(self.recorder.events.count, 1u);
    XCTAssertEqualObjects(self.recorder.last[@"media"], @"image");
}

- (void)testDestroyCancelsALoad
{
    [self.manager loadFormat:@"rewarded" creativeJSON:[self creative:[self imageAd]]];
    [self.manager destroyFormat:@"rewarded"];
    SoilAdsSpin(1.0);
    XCTAssertEqual(self.recorder.events.count, 0u);
    XCTAssertFalse([self.manager isReady:@"rewarded"]);
}

- (void)testLoadEmptiesTheSlotRightAway
{
    [self load:@"rewarded" fields:[self imageAd]];
    XCTAssertTrue([self.manager isReady:@"rewarded"]);
    [self.manager loadFormat:@"rewarded" creativeJSON:[self creative:@{@"imagePath": SoilAdsResource(@"broken.png")}]];
    XCTAssertFalse([self.manager isReady:@"rewarded"], @"previous content released");
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return [self.recorder countOf:@"loadFailed"] == 1; }));
    XCTAssertFalse([self.manager isReady:@"rewarded"]);
}

- (void)testIsReadyFromAnotherThread
{
    [self load:@"banner" fields:[self imageAd]];
    __block BOOL ready = NO;
    XCTestExpectation *done = [self expectationWithDescription:@"background"];
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_DEFAULT, 0), ^{
        ready = [self.manager isReady:@"banner"];
        [done fulfill];
    });
    [self waitForExpectations:@[done] timeout:Timeout];
    XCTAssertTrue(ready);
}

#pragma mark - Show errors

- (void)testShowBeforeLoad
{
    for (NSString *format in @[@"banner", @"interstitial", @"rewarded"]) {
        [self.manager showFormat:format optionsJSON:nil];
        XCTAssertEqualObjects(self.recorder.last[@"format"], format);
        XCTAssertEqualObjects(self.recorder.last[@"event"], @"showFailed");
        XCTAssertEqualObjects(self.recorder.last[@"error"], @"not_loaded");
    }
    XCTAssertTrue(self.host.pauseCalls.count == 0);
}

- (void)testNoHost
{
    [self load:@"banner" fields:[self imageAd]];
    [self load:@"interstitial" fields:[self imageAd]];
    self.host.root = nil;
    [self.manager showFormat:@"banner" optionsJSON:nil];
    XCTAssertEqualObjects(self.recorder.last[@"error"], @"no_host");
    [self.manager showFormat:@"interstitial" optionsJSON:nil];
    XCTAssertEqualObjects(self.recorder.last[@"error"], @"no_host");
    XCTAssertTrue([self.manager isReady:@"interstitial"], @"a failed show keeps the slot");
}

#pragma mark - Fullscreen

- (void)testMediaFillsTheScreenAndTheBottomBarHugsItsContent
{
    [self load:@"interstitial" fields:[self imageAd]];
    [self showFullscreen:@"interstitial" options:[self fullscreenOptions:5 fraction:0.8 minVideo:5]];
    SoilAdsFullscreenViewController *vc = self.manager.fullscreenController;
    [vc.view layoutIfNeeded];

    CGFloat screen = vc.view.safeAreaLayoutGuide.layoutFrame.size.height;
    CGFloat bar = vc.callToActionButton.superview.superview.frame.size.height;
    XCTAssertGreaterThan(bar, 0);
    XCTAssertLessThan(bar, 120, @"the bar is as tall as its content, not stretched");
    XCTAssertGreaterThan(vc.mediaView.frame.size.height, screen * 0.7, @"the media takes the rest of the screen");
}


- (void)testFullscreenShowPresentsAndConsumesTheSlot
{
    [self load:@"interstitial" fields:[self imageAd]];
    [self showFullscreen:@"interstitial" options:[self fullscreenOptions:5 fraction:0.8 minVideo:5]];
    SoilAdsFullscreenViewController *vc = self.manager.fullscreenController;
    XCTAssertNotNil(vc);
    XCTAssertEqual(self.root.presentedViewController, vc);
    XCTAssertEqual(vc.modalPresentationStyle, UIModalPresentationFullScreen);
    XCTAssertNotNil(vc.view.window);
    XCTAssertTrue(vc.prefersStatusBarHidden);
    XCTAssertTrue(vc.prefersHomeIndicatorAutoHidden);
    XCTAssertEqual(vc.supportedInterfaceOrientations, self.root.supportedInterfaceOrientations);
    XCTAssertEqualObjects(self.host.pauseCalls, @[@YES]);
    XCTAssertFalse([self.manager isReady:@"interstitial"], @"fullscreen show consumes the slot");

    // Locked: the countdown shows, the close button does nothing.
    XCTAssertEqualObjects([vc.closeButton titleForState:UIControlStateNormal], @"5");
    [vc.closeButton sendActionsForControlEvents:UIControlEventTouchUpInside];
    SoilAdsSpin(0.3);
    XCTAssertEqual([self.recorder countOf:@"closed"], 0u);

    // Accessibility identifiers.
    XCTAssertEqualObjects(vc.closeButton.accessibilityIdentifier, @"soil_ad_close");
    XCTAssertEqual(SoilAdsFindViews(vc.view, @"soil_ad_media").count, 1u);
    XCTAssertEqual(SoilAdsFindViews(vc.view, @"soil_ad_cta").count, 1u);
    XCTAssertEqual(SoilAdsFindViews(vc.view, @"soil_ad_mute").count, 0u, @"no mute toggle for images");

    // Media and close button stay inside the safe area.
    [vc.view layoutIfNeeded];
    CGRect safe = UIEdgeInsetsInsetRect(vc.view.bounds, vc.view.safeAreaInsets);
    XCTAssertTrue(CGRectContainsRect(safe, [vc.view convertRect:vc.mediaView.bounds fromView:vc.mediaView]));
    XCTAssertTrue(CGRectContainsRect(safe, [vc.view convertRect:vc.closeButton.bounds fromView:vc.closeButton]));

    [self.manager hideFormat:@"interstitial"];
    [self waitForClosed:1];
    XCTAssertEqualObjects([self.recorder sequenceForFormat:@"interstitial"], @"loaded,shown,closed");
    XCTAssertEqual([self.recorder countOf:@"rewarded"], 0u, @"hidden while locked: no reward");
    XCTAssertEqualObjects(self.host.pauseCalls, (@[@YES, @NO]));
    XCTAssertNil(self.root.presentedViewController);
    XCTAssertNil(self.manager.fullscreenController);
}

- (void)testRewardedImageUnlocksRewardsThenClosesOnce
{
    [self load:@"rewarded" fields:[self imageAd]];
    [self showFullscreen:@"rewarded" options:[self fullscreenOptions:0.4 fraction:1 minVideo:0]];
    SoilAdsFullscreenViewController *vc = self.manager.fullscreenController;
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return vc.lockPolicy.unlocked; }));
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return [self.recorder countOf:@"rewarded"] == 1; }));
    XCTAssertEqualObjects([vc.closeButton titleForState:UIControlStateNormal], @"✕");

    [vc.closeButton sendActionsForControlEvents:UIControlEventTouchUpInside];
    [vc.closeButton sendActionsForControlEvents:UIControlEventTouchUpInside]; // double tap
    [self.manager hideFormat:@"rewarded"];                                   // and a late hide
    [self waitForClosed:1];
    XCTAssertEqualObjects([self.recorder sequenceForFormat:@"rewarded"], @"loaded,shown,rewarded,closed");
    [self.manager destroyFormat:@"rewarded"];
    SoilAdsSpin(0.2);
    XCTAssertEqual([self.recorder countOf:@"closed"], 1u);
    XCTAssertEqual([self.recorder countOf:@"rewarded"], 1u);
}

- (void)testInterstitialNeverSendsRewarded
{
    [self load:@"interstitial" fields:[self imageAd]];
    [self showFullscreen:@"interstitial" options:[self fullscreenOptions:0.2 fraction:1 minVideo:0]];
    SoilAdsFullscreenViewController *vc = self.manager.fullscreenController;
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return vc.lockPolicy.unlocked; }));
    [vc.closeButton sendActionsForControlEvents:UIControlEventTouchUpInside];
    [self waitForClosed:1];
    XCTAssertEqualObjects([self.recorder sequenceForFormat:@"interstitial"], @"loaded,shown,closed");
}

- (void)testSecondFullscreenShowIsAlreadyShowing
{
    [self load:@"rewarded" fields:[self imageAd]];
    [self load:@"interstitial" fields:[self imageAd]];
    [self showFullscreen:@"rewarded" options:nil];
    [self.manager showFormat:@"interstitial" optionsJSON:nil];
    XCTAssertEqualObjects(self.recorder.last[@"format"], @"interstitial");
    XCTAssertEqualObjects(self.recorder.last[@"error"], @"already_showing");
    XCTAssertTrue([self.manager isReady:@"interstitial"], @"the refused slot keeps its ad");
    [self.manager showFormat:@"rewarded" optionsJSON:nil];
    XCTAssertEqualObjects(self.recorder.last[@"error"], @"already_showing");
    [self.manager hideFormat:@"interstitial"]; // hides nothing: a different format is on screen
    SoilAdsSpin(0.3);
    XCTAssertEqual([self.recorder countOf:@"closed"], 0u);
    [self.manager hideFormat:@"rewarded"];
    [self waitForClosed:1];
}

- (void)testLoadWhileShowingDoesNotTouchTheScreen
{
    [self load:@"rewarded" fields:[self imageAd]];
    [self showFullscreen:@"rewarded" options:nil];
    SoilAdsFullscreenViewController *vc = self.manager.fullscreenController;
    NSDictionary *e = [self load:@"rewarded" fields:@{@"imagePath": SoilAdsResource(@"image.png")}];
    XCTAssertEqualObjects(e[@"event"], @"loaded");
    XCTAssertTrue([self.manager isReady:@"rewarded"]);
    XCTAssertEqual(self.manager.fullscreenController, vc);
    XCTAssertEqual(self.root.presentedViewController, vc);
    XCTAssertEqualObjects(vc.content.creative.title, @"Word Master");
    [self.manager hideFormat:@"rewarded"];
    [self waitForClosed:1];
    XCTAssertTrue([self.manager isReady:@"rewarded"], @"the new ad is still loaded");
}

- (void)testDestroyWhileShowing
{
    [self load:@"interstitial" fields:[self imageAd]];
    [self showFullscreen:@"interstitial" options:nil];
    [self load:@"interstitial" fields:[self imageAd]];
    [self.manager destroyFormat:@"interstitial"];
    [self waitForClosed:1];
    XCTAssertFalse([self.manager isReady:@"interstitial"]);
}

- (void)testHideDuringPresentationStillPairsShownAndClosed
{
    [self load:@"interstitial" fields:[self imageAd]];
    [self.manager showFormat:@"interstitial" optionsJSON:nil];
    [self.manager hideFormat:@"interstitial"];
    [self waitForClosed:1];
    XCTAssertEqualObjects([self.recorder sequenceForFormat:@"interstitial"], @"loaded,shown,closed");
    XCTAssertEqualObjects(self.host.pauseCalls, (@[@YES, @NO]));
}

- (void)testSystemDismissalSendsClosed
{
    [self load:@"rewarded" fields:[self imageAd]];
    [self showFullscreen:@"rewarded" options:nil];
    [self.root dismissViewControllerAnimated:YES completion:nil];
    [self waitForClosed:1];
    XCTAssertEqualObjects([self.recorder sequenceForFormat:@"rewarded"], @"loaded,shown,closed");
    XCTAssertEqualObjects(self.host.pauseCalls, (@[@YES, @NO]));
    XCTAssertNil(self.manager.fullscreenController);
    // A new show works afterwards.
    [self load:@"rewarded" fields:[self imageAd]];
    [self.manager showFormat:@"rewarded" optionsJSON:nil];
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return [self.recorder countOf:@"shown"] == 2; }));
    [self.manager hideFormat:@"rewarded"];
    [self waitForClosed:2];
}

- (void)testClicksOnMediaAndCallToAction
{
    self.host.openResult = NO;
    [self load:@"interstitial" fields:[self imageAd]];
    [self showFullscreen:@"interstitial" options:nil];
    SoilAdsFullscreenViewController *vc = self.manager.fullscreenController;
    [vc.callToActionButton sendActionsForControlEvents:UIControlEventTouchUpInside];
    XCTAssertEqual([self.recorder countOf:@"clicked"], 1u);
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return self.host.openedURLs.count == 1; }));
    XCTAssertEqualObjects(self.host.openedURLs.firstObject.absoluteString, @"https://example.com/click?x=1");
    UITapGestureRecognizer *tap = (UITapGestureRecognizer *)vc.mediaView.gestureRecognizers.firstObject;
    XCTAssertNotNil(tap);
    // Simulate the media tap through the recognizer's action.
    [vc clickTapped];
    XCTAssertEqual([self.recorder countOf:@"clicked"], 2u);
    [self.manager hideFormat:@"interstitial"];
    [self waitForClosed:1];
    XCTAssertEqualObjects([self.recorder sequenceForFormat:@"interstitial"], @"loaded,shown,clicked,clicked,closed");
}

- (void)testClickWithoutURLStillReportsClicked
{
    [self load:@"interstitial" fields:@{@"imagePath": SoilAdsResource(@"image.png")}];
    [self showFullscreen:@"interstitial" options:nil];
    [self.manager.fullscreenController clickTapped];
    XCTAssertEqual([self.recorder countOf:@"clicked"], 1u);
    XCTAssertEqual(self.host.openedURLs.count, 0u);
}

- (void)testVideoPlaysUnlocksByPositionAndMutes
{
    [self load:@"interstitial" fields:@{@"videoPath": SoilAdsResource(@"video.mp4"), @"title": @"Word Master"}];
    // Lock 20 % of 2.5 s = 0.5 s of playback.
    [self showFullscreen:@"interstitial" options:[self fullscreenOptions:30 fraction:0.2 minVideo:0]];
    SoilAdsFullscreenViewController *vc = self.manager.fullscreenController;
    XCTAssertNotNil(vc.player);
    XCTAssertTrue(vc.lockPolicy.isVideo);
    XCTAssertEqualWithAccuracy(vc.lockPolicy.videoDuration, 2.5, 0.15);
    XCTAssertFalse(vc.player.muted);
    XCTAssertEqual(SoilAdsFindViews(vc.view, @"soil_ad_mute").count, 1u);
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return CMTimeGetSeconds(vc.player.currentTime) > 0.1; }), @"video does not play");
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return vc.lockPolicy.unlocked; }));
    XCTAssertFalse(vc.lockPolicy.videoFailed);
    XCTAssertGreaterThanOrEqual(vc.lockPolicy.positionSeconds, 0.5 - 0.01);
    XCTAssertLessThan(vc.lockPolicy.visibleSeconds, 30);

    [vc.muteButton sendActionsForControlEvents:UIControlEventTouchUpInside];
    XCTAssertTrue(vc.player.muted);
    [vc.muteButton sendActionsForControlEvents:UIControlEventTouchUpInside];
    XCTAssertFalse(vc.player.muted);

    [vc.closeButton sendActionsForControlEvents:UIControlEventTouchUpInside];
    [self waitForClosed:1];
    XCTAssertEqual(vc.player.rate, 0.0f, @"stopped after close");
}

- (void)testRewardedVideoUnlocksWhenItEnds
{
    [self load:@"rewarded" fields:@{@"videoPath": SoilAdsResource(@"video.mp4"), @"imagePath": SoilAdsResource(@"image.png")}];
    [self.manager showFormat:@"rewarded"
                 optionsJSON:SoilAdsJSON(@{@"imageLockSeconds": @20, @"videoLockFraction": @1.0, @"minVideoLockSeconds": @0,
                                           @"maxLockSeconds": @60, @"startMuted": @YES})];
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return [self.recorder countOf:@"shown"] == 1; }));
    SoilAdsFullscreenViewController *vc = self.manager.fullscreenController;
    XCTAssertTrue(vc.player.muted, @"startMuted");
    XCTAssertFalse(vc.lockPolicy.unlocked);
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return [self.recorder countOf:@"rewarded"] == 1; }));
    XCTAssertTrue(vc.lockPolicy.videoEnded || vc.lockPolicy.positionSeconds >= vc.lockPolicy.videoDuration - 0.05);
    [vc.closeButton sendActionsForControlEvents:UIControlEventTouchUpInside];
    [self waitForClosed:1];
    XCTAssertEqualObjects([self.recorder sequenceForFormat:@"rewarded"], @"loaded,shown,rewarded,closed");
}

- (void)testVideoFileVanishingAfterLoadFallsBackToImageRule
{
    NSString *copy = [NSTemporaryDirectory() stringByAppendingPathComponent:@"soil_vanishing.mp4"];
    [[NSFileManager defaultManager] removeItemAtPath:copy error:nil];
    XCTAssertTrue([[NSFileManager defaultManager] copyItemAtPath:SoilAdsResource(@"video.mp4") toPath:copy error:nil]);
    [self load:@"rewarded" fields:@{@"videoPath": copy, @"imagePath": SoilAdsResource(@"image.png")}];
    [[NSFileManager defaultManager] removeItemAtPath:copy error:nil];
    [self showFullscreen:@"rewarded" options:[self fullscreenOptions:0.5 fraction:1 minVideo:0]];
    SoilAdsFullscreenViewController *vc = self.manager.fullscreenController;
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return vc.lockPolicy.videoFailed; }), @"failure not detected");
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return [self.recorder countOf:@"rewarded"] == 1; }));
    [self.manager hideFormat:@"rewarded"];
    [self waitForClosed:1];
}

#pragma mark - Banner

- (CGRect)bannerFrame
{
    [self.root.view layoutIfNeeded];
    return self.manager.bannerView.frame;
}

- (void)testBannerShowMoveHide
{
    [self load:@"banner" fields:[self imageAd]];
    [self.manager showFormat:@"banner" optionsJSON:@"{\"position\":\"bottom\"}"];
    XCTAssertEqualObjects(self.recorder.last[@"event"], @"shown");
    SoilAdsBannerView *banner = self.manager.bannerView;
    XCTAssertEqual(banner.superview, self.root.view);
    XCTAssertEqualObjects(banner.accessibilityIdentifier, @"soil_ad_banner");
    XCTAssertEqual(self.host.pauseCalls.count, 0u, @"banners never pause the game");

    UIEdgeInsets insets = self.root.view.safeAreaInsets;
    CGRect bounds = self.root.view.bounds;
    CGFloat expectedHeight = [SoilAdsBannerView heightForScreenSize:[UIScreen mainScreen].bounds.size];
    CGRect frame = [self bannerFrame];
    XCTAssertEqualWithAccuracy(frame.size.height, expectedHeight, 0.5);
    XCTAssertEqualWithAccuracy(frame.size.width, bounds.size.width - insets.left - insets.right, 0.5);
    XCTAssertEqualWithAccuracy(CGRectGetMaxY(frame), bounds.size.height - insets.bottom, 0.5);

    NSUInteger events = self.recorder.events.count;
    [self.manager showFormat:@"banner" optionsJSON:@"{\"position\":\"top\"}"];
    XCTAssertEqual(self.recorder.events.count, events, @"moving a visible banner sends nothing");
    XCTAssertEqual(self.manager.bannerView, banner);
    XCTAssertEqualWithAccuracy(CGRectGetMinY([self bannerFrame]), insets.top, 0.5);

    [self.manager showFormat:@"banner" optionsJSON:@"{\"position\":\"center\"}"];
    CGFloat safeMid = insets.top + (bounds.size.height - insets.top - insets.bottom) / 2;
    XCTAssertEqualWithAccuracy(CGRectGetMidY([self bannerFrame]), safeMid, 0.5);

    [self.manager hideFormat:@"banner"];
    XCTAssertEqualObjects(self.recorder.last[@"event"], @"closed");
    XCTAssertNil(banner.superview);
    XCTAssertNil(self.manager.bannerView);
    [self.manager hideFormat:@"banner"];
    XCTAssertEqual([self.recorder countOf:@"closed"], 1u);

    XCTAssertTrue([self.manager isReady:@"banner"], @"banner show does not consume the slot");
    [self.manager showFormat:@"banner" optionsJSON:nil];
    XCTAssertEqual([self.recorder countOf:@"shown"], 2u);
    [self.manager destroyFormat:@"banner"];
    XCTAssertEqual([self.recorder countOf:@"closed"], 2u);
    XCTAssertFalse([self.manager isReady:@"banner"]);
    [self.manager showFormat:@"banner" optionsJSON:nil];
    XCTAssertEqualObjects(self.recorder.last[@"error"], @"not_loaded");
}

- (void)testTextBannerClick
{
    [self load:@"banner" fields:@{@"title": @"Word Master", @"description": @"Train", @"callToAction": @"Install",
                                  @"logoPath": SoilAdsResource(@"logo.jpg"), @"clickUrl": @"https://example.com/b"}];
    [self.manager showFormat:@"banner" optionsJSON:nil];
    SoilAdsBannerView *banner = self.manager.bannerView;
    XCTAssertEqual(banner.content.media, SoilAdsMediaText);
    [self.root.view layoutIfNeeded];
    XCTAssertEqual(SoilAdsFindViews(banner, @"soil_ad_cta").count, 1u);
    [banner tapped];
    XCTAssertEqualObjects([self.recorder sequenceForFormat:@"banner"], @"loaded,shown,clicked");
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return self.host.openedURLs.count == 1; }));
    // The banner only covers its own rectangle.
    CGPoint outside = CGPointMake(CGRectGetMidX(self.root.view.bounds), CGRectGetMinY(banner.frame) - 20);
    XCTAssertNotEqual([self.root.view hitTest:outside withEvent:nil], banner);
}

- (void)testBannerAndFullscreenTogether
{
    [self load:@"banner" fields:[self imageAd]];
    [self load:@"interstitial" fields:[self imageAd]];
    [self.manager showFormat:@"banner" optionsJSON:nil];
    [self showFullscreen:@"interstitial" options:nil];
    [self.manager hideFormat:@"interstitial"];
    XCTAssertTrue(SoilAdsWaitUntil(Timeout, ^BOOL { return [self.recorder countOf:@"closed"] == 1; }));
    XCTAssertNotNil(self.manager.bannerView.superview, @"hiding the fullscreen ad leaves the banner");
    XCTAssertEqualObjects([self.recorder sequenceForFormat:@"banner"], @"loaded,shown");
}

@end
