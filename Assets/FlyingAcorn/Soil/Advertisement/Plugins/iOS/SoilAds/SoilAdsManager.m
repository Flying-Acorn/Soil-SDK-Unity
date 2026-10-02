#import "SoilAdsManager.h"
#import "SoilAdsCreative.h"
#import "SoilAdsMediaLoader.h"
#import "SoilAdsFullscreenViewController.h"
#import "SoilAdsBannerView.h"

#define SoilAdsFormatCount (SoilAdsFormatRewarded + 1)
/// If UIKit has not reported the end of a dismissal by then, check whether the ad is still up.
static const NSTimeInterval SoilAdsDismissTimeout = 3.0;
/// While it is, dismiss it again (unanimated) this many times, this far apart, then give up.
static const NSInteger SoilAdsDismissRetries = 3;
static const NSTimeInterval SoilAdsDismissRetryInterval = 1.0;
/// The game is paused once C# has received `shown`, or this long after it was sent at the latest.
static const NSTimeInterval SoilAdsShownAckTimeout = 0.5;

/// One fullscreen show, from `show` to `closed` (or to `showFailed` if it never reached the screen).
@interface SoilAdsSession : NSObject
@property (nonatomic) SoilAdsFormat format;
@property (nonatomic, strong) SoilAdsFullscreenViewController *controller;
/// What the show took out of the slot, and the slot generation then: put back if nothing is shown.
@property (nonatomic, strong) SoilAdsLoadedMedia *content;
@property (nonatomic) NSUInteger generation;
/// Observes UIApplicationDidBecomeActiveNotification while a show waits for the app to be active.
@property (nonatomic, strong, nullable) id activeObserver;
/// The same, while the pause that follows `shown` waits for the app to be active.
@property (nonatomic, strong, nullable) id pauseObserver;
@property (nonatomic) BOOL presenting;
@property (nonatomic) BOOL presented;
@property (nonatomic) BOOL gamePaused;
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

+ (NSURL *)clickURLFromString:(NSString *)text
{
    if (![text isKindOfClass:[NSString class]]) return nil;
    text = [text stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
    if (text.length == 0) return nil;
    // Same rule as the other players: a link with control characters is never opened.
    if ([text rangeOfCharacterFromSet:[NSCharacterSet controlCharacterSet]].location != NSNotFound) return nil;
    NSURL *url = [NSURL URLWithString:text];
    if (!url) {
        // Before iOS 17 spaces and non-ASCII characters make URLWithString: fail; escape them
        // (keeping '#' and existing escapes) and try once more.
        NSMutableCharacterSet *allowed = [[NSCharacterSet URLFragmentAllowedCharacterSet] mutableCopy];
        [allowed addCharactersInString:@"#%"];
        NSString *escaped = [text stringByAddingPercentEncodingWithAllowedCharacters:allowed];
        url = escaped ? [NSURL URLWithString:escaped] : nil;
    }
    NSString *scheme = url.scheme.lowercaseString;
    if (!scheme || ![@[@"http", @"https", @"itms-apps", @"itms-appss"] containsObject:scheme]) return nil;
    return url;
}

- (void)openClickURL:(NSString *)clickUrl completion:(void (^)(BOOL opened))completion
{
    NSURL *url = [SoilAdsManager clickURLFromString:clickUrl];
    if (!url) {
        SoilAdsLog(@"click URL missing, invalid or not http(s)/itms-apps(s)");
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
    if (_banner) [self hideBanner]; // removed by someone else: its `shown` still gets its `closed`
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

    SoilAdsFullscreenViewController *controller =
        [[SoilAdsFullscreenViewController alloc] initWithContent:content
                                                         options:options
                                           supportedOrientations:root.supportedInterfaceOrientations];
    controller.delegate = self;
    SoilAdsSession *session = [[SoilAdsSession alloc] init];
    session.format = format;
    session.controller = controller;
    session.content = content;
    session.generation = _generation[format];

    [self setContent:nil format:format]; // fullscreen show consumes the slot
    _session = session;
    [self presentSession:session];
}

- (BOOL)appIsActive
{
    if ([_host respondsToSelector:@selector(soilAdsAppIsActive)]) return [_host soilAdsAppIsActive];
    return [UIApplication sharedApplication].applicationState == UIApplicationStateActive;
}

/// Presents as soon as UIKit can: once the app is active and no transition is running.
- (void)presentSession:(SoilAdsSession *)session
{
    if (session != _session || session.finished || session.presenting) return;
    __weak __typeof__(self) weakSelf = self;

    if (![self appIsActive]) {
        // Presenting from the background (or under a system alert) is unreliable, and pausing
        // Unity now would be undone by the trampoline when the app comes back.
        if (!session.activeObserver) {
            SoilAdsLog(@"app not active: the ad is shown when it becomes active");
            __weak SoilAdsSession *weakSession = session; // the session holds the observer
            session.activeObserver = [[NSNotificationCenter defaultCenter]
                addObserverForName:UIApplicationDidBecomeActiveNotification object:nil queue:[NSOperationQueue mainQueue]
                        usingBlock:^(NSNotification *note) {
                SoilAdsSession *waiting = weakSession;
                if (waiting) [weakSelf presentSession:waiting];
            }];
        }
        return;
    }
    [self stopWaitingForActive:session];

    UIViewController *root = [_host soilAdsRootViewController];
    if (!root || !root.view.window) {
        [self finishSession:session error:SoilAdsErrorNoHost message:@"no root view controller on screen"];
        return;
    }
    UIViewController *presenter = root;
    while (presenter.presentedViewController && !presenter.presentedViewController.isBeingDismissed)
        presenter = presenter.presentedViewController;

    // UIKit refuses to present from a controller that is being presented or dismissed: wait for it.
    id<UIViewControllerTransitionCoordinator> transition = presenter.transitionCoordinator;
    if (transition && [transition animateAlongsideTransition:nil completion:^(id<UIViewControllerTransitionCoordinatorContext> context) {
            [weakSelf presentSession:session];
        }]) {
        return;
    }

    // The game keeps running until `shown` has reached it (see -pauseGameForSession:), so the
    // event arrives when the ad appears, as on Android.
    session.presenting = YES;
    SoilAdsFullscreenViewController *controller = session.controller;
    [presenter presentViewController:controller animated:YES completion:^{
        [weakSelf sessionPresented:session];
    }];

    if (!controller.presentingViewController) {
        SoilAdsLog(@"presentation refused by UIKit");
        [self finishSession:session error:SoilAdsErrorInternal message:@"presentation failed"];
    }
}

- (void)stopWaitingForActive:(SoilAdsSession *)session
{
    if (!session.activeObserver) return;
    [[NSNotificationCenter defaultCenter] removeObserver:session.activeObserver];
    session.activeObserver = nil;
}

- (void)sessionPresented:(SoilAdsSession *)session
{
    if (session != _session || session.finished || session.presented) return;
    session.presented = YES;
    NSString *name = SoilAdsFormatName(session.format);
    [self emitFormat:name event:@"shown" extra:nil];
    if (session.rewardPending) [self sendRewardForSession:session];
    if (session.closeRequested) {
        [self dismissSession:session];
        return;
    }
    // C# acknowledges `shown` (-acknowledgeShownFormat:); if it does not, pause anyway.
    __weak __typeof__(self) weakSelf = self;
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(SoilAdsShownAckTimeout * NSEC_PER_SEC)),
                   dispatch_get_main_queue(), ^{
        [weakSelf pauseGameForSession:session];
    });
}

- (void)acknowledgeShownFormat:(NSString *)format
{
    SoilAdsSession *session = _session;
    if (!session || !session.presented || ![SoilAdsFormatName(session.format) isEqualToString:format]) return;
    [self pauseGameForSession:session];
}

/// Pauses the game under a fullscreen ad that is on screen, once.
- (void)pauseGameForSession:(SoilAdsSession *)session
{
    if (session != _session || session.finished || session.gamePaused || !session.presented) return;
    if (![self appIsActive]) {
        // The trampoline resumes a game it paused itself when the app comes back, which would
        // undo a pause made now: pause once the app is active again.
        if (!session.pauseObserver) {
            __weak __typeof__(self) weakSelf = self;
            __weak SoilAdsSession *weakSession = session; // the session holds the observer
            session.pauseObserver = [[NSNotificationCenter defaultCenter]
                addObserverForName:UIApplicationDidBecomeActiveNotification object:nil queue:[NSOperationQueue mainQueue]
                        usingBlock:^(NSNotification *note) {
                SoilAdsSession *waiting = weakSession;
                if (waiting) [weakSelf pauseGameForSession:waiting];
            }];
        }
        return;
    }
    [self stopWaitingToPause:session];
    session.gamePaused = YES;
    [_host soilAdsSetGamePaused:YES];
}

- (void)stopWaitingToPause:(SoilAdsSession *)session
{
    if (!session.pauseObserver) return;
    [[NSNotificationCenter defaultCenter] removeObserver:session.pauseObserver];
    session.pauseObserver = nil;
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
        if (session.presenting) {
            session.closeRequested = YES; // dismissed as soon as the presentation completes
        } else {
            // Still waiting to be presented: nothing is on screen, so the show fails.
            [self finishSession:session error:SoilAdsErrorInternal message:@"hidden before it was shown"];
        }
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
        [weakSelf checkDismissal:session attempt:0];
    });
}

/// The game resumes only once the ad is really off screen (a dismissal UIKit ignored, e.g. one that
/// collided with another transition, is retried); after the last retry it resumes anyway.
- (void)checkDismissal:(SoilAdsSession *)session attempt:(NSInteger)attempt
{
    if (session.finished) return;
    UIViewController *presenting = session.controller.presentingViewController;
    if (!presenting) {
        [self finishSession:session];
        return;
    }
    if (attempt >= SoilAdsDismissRetries) {
        SoilAdsLog(@"dismissal never completed: closing the session with the ad still on screen");
        [self finishSession:session];
        return;
    }
    SoilAdsLog(@"dismissal did not complete in time, retrying");
    __weak __typeof__(self) weakSelf = self;
    [presenting dismissViewControllerAnimated:NO completion:^{
        [weakSelf finishSession:session];
    }];
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(SoilAdsDismissRetryInterval * NSEC_PER_SEC)),
                   dispatch_get_main_queue(), ^{
        [weakSelf checkDismissal:session attempt:attempt + 1];
    });
}

- (void)finishSession:(SoilAdsSession *)session
{
    [self finishSession:session error:SoilAdsErrorInternal message:@"left the screen before it was shown"];
}

/// Ends a session: `closed` if it was shown, else `showFailed` (error, message) and the slot gets
/// its ad back unless it was reloaded or destroyed meanwhile.
- (void)finishSession:(SoilAdsSession *)session error:(NSString *)error message:(NSString *)message
{
    if (session.finished) return;
    session.finished = YES;
    [self stopWaitingForActive:session];
    [self stopWaitingToPause:session];
    [session.controller teardown];
    if (_session == session) _session = nil;
    if (session.gamePaused) {
        session.gamePaused = NO;
        [_host soilAdsSetGamePaused:NO];
    }
    NSString *name = SoilAdsFormatName(session.format);
    if (session.presented) {
        [self emitFormat:name event:@"closed" extra:nil];
        return;
    }
    SoilAdsFormat format = session.format;
    if (_generation[format] == session.generation && !_content[format]) [self setContent:session.content format:format];
    [self emitFormat:name failure:@"showFailed" error:error message:message];
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
