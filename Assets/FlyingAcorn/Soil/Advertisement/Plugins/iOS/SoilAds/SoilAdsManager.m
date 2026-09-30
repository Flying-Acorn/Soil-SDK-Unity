#import "SoilAdsManager.h"
#import "SoilAdsCreative.h"
#import "SoilAdsMediaLoader.h"
#import "SoilAdsFullscreenViewController.h"
#import "SoilAdsBannerView.h"

#define SoilAdsFormatCount (SoilAdsFormatRewarded + 1)
/// Last resort if UIKit never reports the end of a dismissal.
static const NSTimeInterval SoilAdsDismissTimeout = 3.0;

/// One fullscreen show, from `show` to `closed`.
@interface SoilAdsSession : NSObject
@property (nonatomic) SoilAdsFormat format;
@property (nonatomic, strong) SoilAdsFullscreenViewController *controller;
@property (nonatomic) BOOL presented;
@property (nonatomic) BOOL closeRequested;
@property (nonatomic) BOOL dismissing;
@property (nonatomic) BOOL finished;
@property (nonatomic) BOOL rewardPending;
@property (nonatomic) BOOL rewardSent;
@end

@implementation SoilAdsSession
@end

@interface SoilAdsManager () <SoilAdsFullscreenDelegate>
@end

@implementation SoilAdsManager {
    NSUInteger _generation[SoilAdsFormatCount];
    SoilAdsLoadedMedia *_content[SoilAdsFormatCount];
    BOOL _ready[SoilAdsFormatCount];
    NSObject *_readyLock;
    SoilAdsSession *_session;
    SoilAdsBannerView *_banner;
}

- (instancetype)initWithHost:(id<SoilAdsHost>)host
{
    if ((self = [super init])) {
        _host = host;
        _readyLock = [[NSObject alloc] init];
    }
    return self;
}

- (SoilAdsFullscreenViewController *)fullscreenController { return _session.controller; }
- (SoilAdsBannerView *)bannerView { return _banner; }

#pragma mark - Events

- (void)emitFormat:(NSString *)format event:(NSString *)event extra:(NSDictionary *)extra
{
    NSMutableDictionary *payload = [NSMutableDictionary dictionary];
    payload[@"format"] = [format isKindOfClass:[NSString class]] ? format : @"";
    payload[@"event"] = event;
    if (extra) [payload addEntriesFromDictionary:extra];
    NSData *data = [NSJSONSerialization isValidJSONObject:payload]
        ? [NSJSONSerialization dataWithJSONObject:payload options:0 error:nil] : nil;
    if (!data) {
        payload[@"format"] = @"";
        data = [NSJSONSerialization dataWithJSONObject:payload options:0 error:nil];
    }
    NSString *json = data ? [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] : nil;
    if (!json) return;
    SoilAdsLog(@"%@", json);
    SoilAdsEventSink sink = self.eventSink;
    if (sink) sink(json);
}

- (void)emitFormat:(NSString *)format failure:(NSString *)event error:(NSString *)error message:(NSString *)message
{
    [self emitFormat:format event:event extra:@{@"error": error ? error : SoilAdsErrorInternal, @"message": message ? message : @""}];
}

#pragma mark - Slots

- (void)setContent:(SoilAdsLoadedMedia *)content format:(SoilAdsFormat)format
{
    _content[format] = content;
    @synchronized (_readyLock) {
        _ready[format] = content != nil;
    }
}

- (BOOL)isReady:(NSString *)name
{
    SoilAdsFormat format = SoilAdsFormatFromString(name);
    if (format == SoilAdsFormatInvalid) return NO;
    @synchronized (_readyLock) {
        return _ready[format];
    }
}

- (void)loadFormat:(NSString *)name creativeJSON:(NSString *)json
{
    SoilAdsFormat format = SoilAdsFormatFromString(name);
    if (format == SoilAdsFormatInvalid) {
        [self emitFormat:name failure:@"loadFailed" error:SoilAdsErrorInvalidFormat message:@"unknown ad format"];
        return;
    }
    NSUInteger generation = ++_generation[format];
    [self setContent:nil format:format];

    SoilAdsCreative *creative = [SoilAdsCreative creativeWithJSON:json];
    if (!creative) {
        [self emitFormat:name failure:@"loadFailed" error:SoilAdsErrorInvalidCreative message:@"creative is not a JSON object"];
        return;
    }
    __weak __typeof__(self) weakSelf = self;
    [SoilAdsMediaLoader loadCreative:creative format:format completion:^(SoilAdsLoadedMedia *media, NSString *error, NSString *message) {
        SoilAdsManager *self_ = weakSelf;
        if (!self_ || self_->_generation[format] != generation) return; // replaced or destroyed meanwhile
        if (media) {
            [self_ setContent:media format:format];
            [self_ emitFormat:name event:@"loaded"
                        extra:@{@"media": SoilAdsMediaName(media.media), @"durationMs": @(media.durationMs)}];
        } else {
            [self_ emitFormat:name failure:@"loadFailed" error:error message:message];
        }
    }];
}

- (void)showFormat:(NSString *)name optionsJSON:(NSString *)json
{
    SoilAdsFormat format = SoilAdsFormatFromString(name);
    if (format == SoilAdsFormatInvalid) {
        [self emitFormat:name failure:@"showFailed" error:SoilAdsErrorInvalidFormat message:@"unknown ad format"];
        return;
    }
    SoilAdsShowOptions *options = [SoilAdsShowOptions optionsWithJSON:json format:format];
    if (format == SoilAdsFormatBanner) [self showBannerWithOptions:options];
    else [self showFullscreen:format options:options];
}

- (void)hideFormat:(NSString *)name
{
    SoilAdsFormat format = SoilAdsFormatFromString(name);
    if (format == SoilAdsFormatInvalid) {
        SoilAdsLog(@"hide ignored: unknown ad format");
        return;
    }
    if (format == SoilAdsFormatBanner) [self hideBanner];
    else if (_session && _session.format == format) [self dismissSession:_session];
}

- (void)destroyFormat:(NSString *)name
{
    SoilAdsFormat format = SoilAdsFormatFromString(name);
    if (format == SoilAdsFormatInvalid) {
        SoilAdsLog(@"destroy ignored: unknown ad format");
        return;
    }
    [self hideFormat:name];
    _generation[format]++;
    [self setContent:nil format:format];
}

#pragma mark - Clicks

- (void)openClickURL:(NSString *)clickUrl completion:(void (^)(BOOL opened))completion
{
    NSURL *url = clickUrl.length > 0 ? [NSURL URLWithString:clickUrl] : nil;
    if (!url || url.scheme.length == 0) {
        SoilAdsLog(@"click URL missing or invalid");
        if (completion) completion(NO);
        return;
    }
    void (^done)(BOOL) = ^(BOOL opened) {
        if (!opened) SoilAdsLog(@"click URL could not be opened");
        if (completion) completion(opened);
    };
    if ([_host respondsToSelector:@selector(soilAdsOpenURL:completion:)]) {
        [_host soilAdsOpenURL:url completion:done];
    } else {
        [[UIApplication sharedApplication] openURL:url options:@{} completionHandler:done];
    }
}

#pragma mark - Banner

- (void)showBannerWithOptions:(SoilAdsShowOptions *)options
{
    NSString *name = SoilAdsFormatName(SoilAdsFormatBanner);
    if (_banner.superview) {
        [_banner moveToPosition:options.position];
        return;
    }
    _banner = nil;
    SoilAdsLoadedMedia *content = _content[SoilAdsFormatBanner];
    if (!content) {
        [self emitFormat:name failure:@"showFailed" error:SoilAdsErrorNotLoaded message:@"no banner loaded"];
        return;
    }
    UIView *hostView = [_host soilAdsRootViewController].view;
    if (!hostView) {
        [self emitFormat:name failure:@"showFailed" error:SoilAdsErrorNoHost message:@"no root view controller"];
        return;
    }
    SoilAdsBannerView *banner = [[SoilAdsBannerView alloc] initWithContent:content];
    __weak __typeof__(self) weakSelf = self;
    __weak SoilAdsBannerView *weakBanner = banner;
    banner.onClick = ^{ [weakSelf bannerClicked:weakBanner]; };
    [banner attachToView:hostView position:options.position];
    _banner = banner;
    [self emitFormat:name event:@"shown" extra:nil];
}

- (void)bannerClicked:(SoilAdsBannerView *)banner
{
    if (!banner || banner != _banner) return;
    [self emitFormat:SoilAdsFormatName(SoilAdsFormatBanner) event:@"clicked" extra:nil];
    [self openClickURL:banner.content.creative.clickUrl completion:nil];
}

- (void)hideBanner
{
    if (!_banner) return;
    SoilAdsBannerView *banner = _banner;
    _banner = nil;
    banner.onClick = nil;
    [banner removeFromSuperview];
    [self emitFormat:SoilAdsFormatName(SoilAdsFormatBanner) event:@"closed" extra:nil];
}

#pragma mark - Fullscreen

- (void)showFullscreen:(SoilAdsFormat)format options:(SoilAdsShowOptions *)options
{
    NSString *name = SoilAdsFormatName(format);
    if (_session) {
        [self emitFormat:name failure:@"showFailed" error:SoilAdsErrorAlreadyShowing message:@"a fullscreen ad is on screen"];
        return;
    }
    SoilAdsLoadedMedia *content = _content[format];
    if (!content) {
        [self emitFormat:name failure:@"showFailed" error:SoilAdsErrorNotLoaded message:@"nothing loaded"];
        return;
    }
    UIViewController *root = [_host soilAdsRootViewController];
    if (!root || !root.view.window) {
        [self emitFormat:name failure:@"showFailed" error:SoilAdsErrorNoHost message:@"no root view controller on screen"];
        return;
    }
    UIViewController *presenter = root;
    while (presenter.presentedViewController && !presenter.presentedViewController.isBeingDismissed)
        presenter = presenter.presentedViewController;

    SoilAdsFullscreenViewController *controller =
        [[SoilAdsFullscreenViewController alloc] initWithContent:content
                                                         options:options
                                           supportedOrientations:root.supportedInterfaceOrientations];
    controller.delegate = self;
    SoilAdsSession *session = [[SoilAdsSession alloc] init];
    session.format = format;
    session.controller = controller;

    NSUInteger generation = _generation[format];
    [self setContent:nil format:format]; // fullscreen show consumes the slot
    _session = session;
    [_host soilAdsSetGamePaused:YES];

    __weak __typeof__(self) weakSelf = self;
    [presenter presentViewController:controller animated:YES completion:^{
        [weakSelf sessionPresented:session];
    }];

    if (!controller.presentingViewController) {
        SoilAdsLog(@"presentation refused by UIKit");
        session.finished = YES;
        [controller teardown];
        _session = nil;
        [_host soilAdsSetGamePaused:NO];
        if (_generation[format] == generation && !_content[format]) [self setContent:content format:format];
        [self emitFormat:name failure:@"showFailed" error:SoilAdsErrorInternal message:@"presentation failed"];
    }
}

- (void)sessionPresented:(SoilAdsSession *)session
{
    if (session != _session || session.finished || session.presented) return;
    session.presented = YES;
    NSString *name = SoilAdsFormatName(session.format);
    [self emitFormat:name event:@"shown" extra:nil];
    if (session.rewardPending) [self sendRewardForSession:session];
    if (session.closeRequested) [self dismissSession:session];
}

- (void)sendRewardForSession:(SoilAdsSession *)session
{
    if (session.rewardSent || session.finished || session.format != SoilAdsFormatRewarded) return;
    if (!session.presented) {
        session.rewardPending = YES; // `shown` must come first
        return;
    }
    session.rewardSent = YES;
    [self emitFormat:SoilAdsFormatName(session.format) event:@"rewarded" extra:nil];
}

- (void)dismissSession:(SoilAdsSession *)session
{
    if (session.finished || session.dismissing) return;
    if (!session.presented) {
        session.closeRequested = YES; // dismissed as soon as the presentation completes
        return;
    }
    session.dismissing = YES;
    SoilAdsFullscreenViewController *controller = session.controller;
    [controller teardown];
    UIViewController *presenting = controller.presentingViewController;
    if (!presenting) {
        [self finishSession:session];
        return;
    }
    __weak __typeof__(self) weakSelf = self;
    [presenting dismissViewControllerAnimated:YES completion:^{
        [weakSelf finishSession:session];
    }];
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(SoilAdsDismissTimeout * NSEC_PER_SEC)),
                   dispatch_get_main_queue(), ^{
        if (!session.finished) SoilAdsLog(@"dismissal did not complete in time");
        [weakSelf finishSession:session];
    });
}

- (void)finishSession:(SoilAdsSession *)session
{
    if (session.finished) return;
    session.finished = YES;
    [session.controller teardown];
    if (_session == session) _session = nil;
    [_host soilAdsSetGamePaused:NO];
    if (session.presented) [self emitFormat:SoilAdsFormatName(session.format) event:@"closed" extra:nil];
}

- (SoilAdsSession *)sessionFor:(SoilAdsFullscreenViewController *)controller
{
    return (_session && _session.controller == controller && !_session.finished) ? _session : nil;
}

#pragma mark - SoilAdsFullscreenDelegate

- (void)fullscreenControllerDidRequestClose:(SoilAdsFullscreenViewController *)controller
{
    SoilAdsSession *session = [self sessionFor:controller];
    if (session) [self dismissSession:session];
}

- (void)fullscreenControllerDidClick:(SoilAdsFullscreenViewController *)controller
{
    SoilAdsSession *session = [self sessionFor:controller];
    if (!session || !session.presented || session.dismissing) {
        [controller resumeAfterClick];
        return;
    }
    [self emitFormat:SoilAdsFormatName(session.format) event:@"clicked" extra:nil];
    __weak SoilAdsFullscreenViewController *weakController = controller;
    [self openClickURL:controller.content.creative.clickUrl completion:^(BOOL opened) {
        if (!opened) {
            [weakController resumeAfterClick];
            return;
        }
        // Normally the app resigns active and the controller resumes on return; if it never left, resume anyway.
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(2 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            if ([UIApplication sharedApplication].applicationState == UIApplicationStateActive)
                [weakController resumeAfterClick];
        });
    }];
}

- (void)fullscreenControllerDidEarnReward:(SoilAdsFullscreenViewController *)controller
{
    SoilAdsSession *session = [self sessionFor:controller];
    if (session) [self sendRewardForSession:session];
}

- (void)fullscreenControllerDidDisappear:(SoilAdsFullscreenViewController *)controller
{
    SoilAdsSession *session = [self sessionFor:controller];
    if (session) [self finishSession:session];
}

@end
