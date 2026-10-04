#import "SoilAdsFullscreenViewController.h"
#import "SoilAdsUI.h"
#import <AVFoundation/AVFoundation.h>
#import <QuartzCore/QuartzCore.h>
#include <math.h>

static const NSTimeInterval SoilAdsTickInterval = 0.25;

@interface SoilAdsPlayerView : UIView
@property (nonatomic, readonly) AVPlayerLayer *playerLayer;
@end

@implementation SoilAdsPlayerView
+ (Class)layerClass { return [AVPlayerLayer class]; }
- (AVPlayerLayer *)playerLayer { return (AVPlayerLayer *)self.layer; }
@end

@implementation SoilAdsFullscreenViewController {
    SoilAdsShowOptions *_options;
    UIInterfaceOrientationMask _orientations;
    NSTimer *_timer;
    CFTimeInterval _lastTick;
    double _visibleSeconds;
    BOOL _appeared;
    BOOL _appActive;
    BOOL _awayAfterClick;
    BOOL _videoEnded;
    BOOL _videoFailed;
    BOOL _tornDown;
    BOOL _closeShownUnlocked;
    BOOL _failed;
    SoilAdsPlayerView *_playerView;
    UIImageView *_imageView;
    NSMutableArray<id> *_observers;
}

- (instancetype)initWithContent:(SoilAdsLoadedMedia *)content
                        options:(SoilAdsShowOptions *)options
          supportedOrientations:(UIInterfaceOrientationMask)orientations
{
    if ((self = [super initWithNibName:nil bundle:nil])) {
        _content = content;
        _options = options;
        _orientations = orientations;
        _observers = [NSMutableArray array];
        BOOL isVideo = content.media == SoilAdsMediaVideo && content.videoURL != nil;
        _lockPolicy = [[SoilAdsLockPolicy alloc] initWithSettings:options.lockSettings
                                                          isVideo:isVideo
                                                    videoDuration:content.durationMs / 1000.0
                                                         rewarded:content.format == SoilAdsFormatRewarded];
        self.modalPresentationStyle = UIModalPresentationFullScreen;
        self.modalTransitionStyle = UIModalTransitionStyleCrossDissolve;
    }
    return self;
}

- (void)dealloc
{
    [_timer invalidate];
    for (id observer in _observers) [[NSNotificationCenter defaultCenter] removeObserver:observer];
}

#pragma mark - Appearance

- (BOOL)prefersStatusBarHidden { return YES; }
- (BOOL)prefersHomeIndicatorAutoHidden { return YES; }
- (BOOL)shouldAutorotate { return YES; }
- (UIInterfaceOrientationMask)supportedInterfaceOrientations
{
    return _orientations != 0 ? _orientations : UIInterfaceOrientationMaskAllButUpsideDown;
}

static NSString *SoilAdsFailAtForTests;

+ (NSString *)failAtForTests { return SoilAdsFailAtForTests; }
+ (void)setFailAtForTests:(NSString *)where { SoilAdsFailAtForTests = [where copy]; }

static void SoilAdsFaultForTests(NSString *where)
{
    if ([where isEqualToString:SoilAdsFailAtForTests])
        [NSException raise:@"SoilAdsInjectedFault" format:@"%@", where]; // no @throw: Unity builds without -fobjc-exceptions
}

/// Runs one of the screen's callbacks; an exception in it is reported once to the delegate, which
/// ends the show (`closed`, or `showFailed` if it never appeared) instead of letting it end the app.
- (void)guard:(NSString *)what block:(dispatch_block_t)block
{
    if (SoilAdsGuard(what, block) || _failed) return;
    _failed = YES;
    id<SoilAdsFullscreenDelegate> delegate = self.delegate;
    SoilAdsGuard(@"reporting the failure", ^{ [delegate fullscreenControllerDidFail:self]; });
}

- (void)viewDidLoad
{
    [super viewDidLoad];
    self.view.backgroundColor = [UIColor blackColor];
    [self guard:@"building the ad" block:^{
        SoilAdsFaultForTests(@"create");
        [self buildView];
    }];
}

- (void)buildView
{
    UILayoutGuide *safe = self.view.safeAreaLayoutGuide;
    [self addBackdrop];

    _mediaView = [[UIView alloc] init];
    _mediaView.translatesAutoresizingMaskIntoConstraints = NO;
    _mediaView.backgroundColor = [UIColor clearColor];
    _mediaView.clipsToBounds = YES;
    _mediaView.isAccessibilityElement = YES;
    _mediaView.accessibilityIdentifier = SoilAdsIdMedia;
    _mediaView.accessibilityTraits = UIAccessibilityTraitButton;
    _mediaView.accessibilityLabel = SoilAdsAccessibilityLabel(_content.creative.title);
    [_mediaView addGestureRecognizer:[[UITapGestureRecognizer alloc] initWithTarget:self action:@selector(clickTapped)]];
    [self.view addSubview:_mediaView];

    _imageView = [[UIImageView alloc] initWithImage:_content.image];
    _imageView.contentMode = UIViewContentModeScaleAspectFit;
    [self pin:_imageView inside:_mediaView];

    if (_lockPolicy.isVideo) {
        _player = [AVPlayer playerWithURL:_content.videoURL];
        _player.muted = _options.startMuted;
        _player.actionAtItemEnd = AVPlayerActionAtItemEndPause;
        _playerView = [[SoilAdsPlayerView alloc] init];
        _playerView.playerLayer.videoGravity = AVLayerVideoGravityResizeAspect;
        _playerView.playerLayer.player = _player;
        [self pin:_playerView inside:_mediaView];
        _imageView.hidden = YES;
    }
    [self observeNotifications];

    _infoCard = [self makeInfoCard];
    NSMutableArray<NSLayoutConstraint *> *constraints = [NSMutableArray arrayWithArray:@[
        [_mediaView.topAnchor constraintEqualToAnchor:safe.topAnchor],
        [_mediaView.leadingAnchor constraintEqualToAnchor:safe.leadingAnchor],
        [_mediaView.trailingAnchor constraintEqualToAnchor:safe.trailingAnchor],
    ]];
    if (_infoCard) {
        [self.view addSubview:_infoCard];
        // At most a readable width, centered, on tablets and in landscape.
        NSLayoutConstraint *fill = [_infoCard.widthAnchor constraintEqualToAnchor:safe.widthAnchor constant:-24];
        fill.priority = UILayoutPriorityDefaultHigh;
        [constraints addObjectsFromArray:@[
            fill,
            // Never wider than the screen: a long call to action truncates instead.
            [_infoCard.widthAnchor constraintLessThanOrEqualToAnchor:safe.widthAnchor constant:-24],
            [_infoCard.widthAnchor constraintLessThanOrEqualToConstant:520],
            [_infoCard.centerXAnchor constraintEqualToAnchor:safe.centerXAnchor],
            [_infoCard.bottomAnchor constraintEqualToAnchor:safe.bottomAnchor constant:-12],
            [_mediaView.bottomAnchor constraintEqualToAnchor:_infoCard.topAnchor constant:-12],
        ]];
    } else {
        [constraints addObject:[_mediaView.bottomAnchor constraintEqualToAnchor:safe.bottomAnchor]];
    }

    UILabel *badge = SoilAdsMakeBadge(12);
    [self.view addSubview:badge];
    [constraints addObjectsFromArray:@[
        [badge.topAnchor constraintEqualToAnchor:safe.topAnchor constant:16],
        [badge.leadingAnchor constraintEqualToAnchor:safe.leadingAnchor constant:12],
    ]];

    if (_lockPolicy.isVideo) {
        _muteButton = [self makeRoundButton:36];
        _muteButton.accessibilityIdentifier = SoilAdsIdMute;
        [_muteButton addTarget:self action:@selector(muteTapped) forControlEvents:UIControlEventTouchUpInside];
        [self.view addSubview:_muteButton];
        [constraints addObjectsFromArray:@[
            [_muteButton.leadingAnchor constraintEqualToAnchor:badge.trailingAnchor constant:10],
            [_muteButton.centerYAnchor constraintEqualToAnchor:badge.centerYAnchor],
        ]];
        [self refreshMuteButton];
    }

    _closeButton = [self makeRoundButton:44]; // the minimum touch target
    _closeButton.titleLabel.font = [UIFont boldSystemFontOfSize:16];
    _closeButton.accessibilityIdentifier = SoilAdsIdClose;
    _closeButton.accessibilityLabel = @"Close";
    [_closeButton addTarget:self action:@selector(closeTapped) forControlEvents:UIControlEventTouchUpInside];
    [self.view addSubview:_closeButton];
    [constraints addObjectsFromArray:@[
        [_closeButton.topAnchor constraintEqualToAnchor:safe.topAnchor constant:8],
        [_closeButton.trailingAnchor constraintEqualToAnchor:safe.trailingAnchor constant:-8],
    ]];

    [NSLayoutConstraint activateConstraints:constraints];
    [self refreshCloseButton];
}

- (void)pin:(UIView *)view inside:(UIView *)container
{
    view.translatesAutoresizingMaskIntoConstraints = NO;
    [container addSubview:view];
    [NSLayoutConstraint activateConstraints:@[
        [view.topAnchor constraintEqualToAnchor:container.topAnchor],
        [view.bottomAnchor constraintEqualToAnchor:container.bottomAnchor],
        [view.leadingAnchor constraintEqualToAnchor:container.leadingAnchor],
        [view.trailingAnchor constraintEqualToAnchor:container.trailingAnchor],
    ]];
}

- (UIButton *)makeRoundButton:(CGFloat)size
{
    UIButton *button = [UIButton buttonWithType:UIButtonTypeCustom];
    button.translatesAutoresizingMaskIntoConstraints = NO;
    button.backgroundColor = [UIColor colorWithWhite:0 alpha:0.6];
    button.layer.cornerRadius = size / 2;
    button.layer.borderWidth = 1;
    button.layer.borderColor = [UIColor colorWithWhite:1 alpha:0.5].CGColor;
    button.tintColor = [UIColor whiteColor];
    [button setTitleColor:[UIColor whiteColor] forState:UIControlStateNormal];
    [NSLayoutConstraint activateConstraints:@[
        [button.widthAnchor constraintEqualToConstant:size],
        [button.heightAnchor constraintEqualToConstant:size],
    ]];
    return button;
}

/// The ad's image, aspect-filled, blurred and dimmed behind everything: a 4:5 cover image or a
/// letterboxed video then sits on its own colors instead of black bars.
- (void)addBackdrop
{
    if (!_content.image) return;
    _backdropView = [[UIImageView alloc] initWithImage:_content.image];
    _backdropView.contentMode = UIViewContentModeScaleAspectFill;
    _backdropView.clipsToBounds = YES;
    [self pin:_backdropView inside:self.view];
    UIVisualEffectView *blur = [[UIVisualEffectView alloc] initWithEffect:[UIBlurEffect effectWithStyle:UIBlurEffectStyleDark]];
    [self pin:blur inside:self.view];
    UIView *dim = [[UIView alloc] init];
    dim.backgroundColor = [UIColor colorWithWhite:0 alpha:0.35];
    [self pin:dim inside:self.view];
}

/// A rounded card: logo, title and description in a row that follows the text's direction (the
/// logo on the right for Persian), and the call to action as a full-width button under it.
- (nullable UIView *)makeInfoCard
{
    SoilAdsCreative *creative = _content.creative;
    BOOL hasText = creative.title.length > 0 || creative.adDescription.length > 0;
    BOOL hasHeader = hasText || _content.logo != nil;
    if (!hasHeader && creative.callToAction.length == 0) return nil;
    BOOL rtl = SoilAdsCreativeIsRightToLeft(creative.title, creative.adDescription, creative.callToAction);

    UIView *card = [[UIView alloc] init];
    card.translatesAutoresizingMaskIntoConstraints = NO;
    card.backgroundColor = [UIColor colorWithRed:0.11 green:0.11 blue:0.12 alpha:0.94];
    card.layer.cornerRadius = 18;
    if (@available(iOS 13.0, *)) card.layer.cornerCurve = kCACornerCurveContinuous;
    card.semanticContentAttribute = SoilAdsSemanticAttribute(rtl);

    UIStackView *column = [[UIStackView alloc] init];
    column.translatesAutoresizingMaskIntoConstraints = NO;
    column.axis = UILayoutConstraintAxisVertical;
    column.spacing = 14;
    [card addSubview:column];
    // The card is as short as its content allows; the media view takes the rest. Neither has an
    // intrinsic height (a stack view reports none), so without this Auto Layout may stretch the
    // card over half the screen. Low priority: the labels' compression resistance still wins.
    NSLayoutConstraint *shortest = [card.heightAnchor constraintEqualToConstant:0];
    shortest.priority = UILayoutPriorityDefaultLow;
    shortest.active = YES;
    [NSLayoutConstraint activateConstraints:@[
        [column.topAnchor constraintEqualToAnchor:card.topAnchor constant:16],
        [column.bottomAnchor constraintEqualToAnchor:card.bottomAnchor constant:-16],
        [column.leadingAnchor constraintEqualToAnchor:card.leadingAnchor constant:16],
        [column.trailingAnchor constraintEqualToAnchor:card.trailingAnchor constant:-16],
    ]];

    if (hasHeader) {
        UIStackView *row = [[UIStackView alloc] init];
        row.axis = UILayoutConstraintAxisHorizontal;
        row.alignment = UIStackViewAlignmentCenter;
        row.spacing = 12;
        row.semanticContentAttribute = SoilAdsSemanticAttribute(rtl);
        if (_content.logo) {
            UIImageView *logo = [[UIImageView alloc] initWithImage:_content.logo];
            logo.translatesAutoresizingMaskIntoConstraints = NO;
            logo.contentMode = UIViewContentModeScaleAspectFill;
            logo.layer.cornerRadius = 12;
            if (@available(iOS 13.0, *)) logo.layer.cornerCurve = kCACornerCurveContinuous;
            logo.clipsToBounds = YES;
            [NSLayoutConstraint activateConstraints:@[
                [logo.widthAnchor constraintEqualToConstant:56],
                [logo.heightAnchor constraintEqualToConstant:56],
            ]];
            [row addArrangedSubview:logo];
        }
        if (hasText) {
            UIStackView *texts = [[UIStackView alloc] init];
            texts.axis = UILayoutConstraintAxisVertical;
            texts.spacing = 4;
            UILabel *title = [[UILabel alloc] init];
            title.font = [UIFont systemFontOfSize:18 weight:UIFontWeightBold];
            title.textColor = [UIColor whiteColor];
            title.numberOfLines = 2;
            SoilAdsSetDirectionalText(title, creative.title);
            UILabel *body = [[UILabel alloc] init];
            body.font = [UIFont systemFontOfSize:14];
            body.textColor = [UIColor colorWithWhite:1 alpha:0.72];
            body.numberOfLines = 3;
            SoilAdsSetDirectionalText(body, creative.adDescription);
            [texts addArrangedSubview:title];
            [texts addArrangedSubview:body];
            [texts setContentHuggingPriority:UILayoutPriorityDefaultLow forAxis:UILayoutConstraintAxisHorizontal];
            [texts setContentCompressionResistancePriority:UILayoutPriorityDefaultLow forAxis:UILayoutConstraintAxisHorizontal];
            [row addArrangedSubview:texts];
        }
        [column addArrangedSubview:row];
    }

    if (creative.callToAction.length > 0) {
        UIButton *cta = SoilAdsMakeCallToActionButton(creative.callToAction, 17);
        cta.layer.cornerRadius = 14;
        if (@available(iOS 13.0, *)) cta.layer.cornerCurve = kCACornerCurveContinuous;
        [cta.heightAnchor constraintGreaterThanOrEqualToConstant:50].active = YES;
        [column addArrangedSubview:cta];
        _callToActionButton = cta;
        [_callToActionButton addTarget:self action:@selector(clickTapped) forControlEvents:UIControlEventTouchUpInside];
    }
    return card;
}

#pragma mark - Lifecycle

- (void)viewDidAppear:(BOOL)animated
{
    [super viewDidAppear:animated];
    [self guard:@"appearing" block:^{
        if (_tornDown) return;
        _appeared = YES;
        _appActive = [UIApplication sharedApplication].applicationState == UIApplicationStateActive;
        _lastTick = CACurrentMediaTime();
        if (!_timer) {
            __weak __typeof__(self) weakSelf = self;
            _timer = [NSTimer timerWithTimeInterval:SoilAdsTickInterval repeats:YES block:^(NSTimer *timer) {
                __typeof__(self) self_ = weakSelf;
                [self_ guard:@"tick" block:^{ [self_ tick]; }];
            }];
            [[NSRunLoop mainRunLoop] addTimer:_timer forMode:NSRunLoopCommonModes];
        }
        [self updatePlayback];
        [self tick];
    }];
}

- (void)viewDidDisappear:(BOOL)animated
{
    [super viewDidDisappear:animated];
    [self guard:@"disappearing" block:^{
        if (_tornDown) return;
        [self tick];
        _appeared = NO;
        [self updatePlayback];
        BOOL dismissed = self.isBeingDismissed || self.presentingViewController == nil
            || self.presentingViewController.isBeingDismissed;
        if (dismissed) [self.delegate fullscreenControllerDidDisappear:self];
    }];
}

- (void)teardown
{
    if (_tornDown) return;
    _tornDown = YES;
    [_timer invalidate];
    _timer = nil;
    for (id observer in _observers) [[NSNotificationCenter defaultCenter] removeObserver:observer];
    [_observers removeAllObjects];
    [_player pause];
}

#pragma mark - Clock and video

/// App foreground state for every ad, plus end / failure of the video item when there is one.
- (void)observeNotifications
{
    NSNotificationCenter *center = [NSNotificationCenter defaultCenter];
    NSOperationQueue *main = [NSOperationQueue mainQueue];
    __weak __typeof__(self) weakSelf = self;
    [_observers addObject:[center addObserverForName:UIApplicationWillResignActiveNotification object:nil queue:main
                                          usingBlock:^(NSNotification *note) {
        __typeof__(self) self_ = weakSelf;
        [self_ guard:@"appWillResignActive" block:^{ [self_ appWillResignActive]; }];
    }]];
    [_observers addObject:[center addObserverForName:UIApplicationDidBecomeActiveNotification object:nil queue:main
                                          usingBlock:^(NSNotification *note) {
        __typeof__(self) self_ = weakSelf;
        [self_ guard:@"appDidBecomeActive" block:^{ [self_ appDidBecomeActive]; }];
    }]];
    AVPlayerItem *item = _player.currentItem;
    if (!item) return;
    [_observers addObject:[center addObserverForName:AVPlayerItemDidPlayToEndTimeNotification object:item queue:main
                                          usingBlock:^(NSNotification *note) {
        __typeof__(self) self_ = weakSelf;
        [self_ guard:@"videoDidEnd" block:^{ [self_ videoDidEnd]; }];
    }]];
    [_observers addObject:[center addObserverForName:AVPlayerItemFailedToPlayToEndTimeNotification object:item queue:main
                                          usingBlock:^(NSNotification *note) {
        __typeof__(self) self_ = weakSelf;
        [self_ guard:@"videoDidFail" block:^{ [self_ videoDidFail]; }];
    }]];
}

- (void)appWillResignActive
{
    if (_tornDown) return;
    [self tick];
    _appActive = NO;
    [self updatePlayback];
}

- (void)appDidBecomeActive
{
    if (_tornDown) return;
    _lastTick = CACurrentMediaTime();
    _appActive = YES;
    _awayAfterClick = NO;
    [self updatePlayback];
}

- (void)resumeAfterClick
{
    if (_tornDown || !_awayAfterClick) return;
    _lastTick = CACurrentMediaTime();
    _awayAfterClick = NO;
    [self updatePlayback];
}

- (BOOL)isCounting
{
    return _appeared && _appActive && !_awayAfterClick && !_tornDown;
}

- (void)updatePlayback
{
    if (!_player) return;
    BOOL play = [self isCounting] && !_videoEnded && !_videoFailed;
    if (play) [_player play];
    else [_player pause];
}

- (void)videoDidEnd
{
    if (_tornDown || _videoEnded) return;
    _videoEnded = YES;
    if (_content.image) {
        _imageView.hidden = NO;
        _playerView.hidden = YES;
    }
    [self tick];
}

- (void)videoDidFail
{
    if (_tornDown || _videoFailed) return;
    SoilAdsLog(@"video failed while playing: %@", (_player.currentItem.error ? _player.currentItem.error.localizedDescription : @""));
    _videoFailed = YES;
    [_player pause];
    if (_content.image) {
        _imageView.hidden = NO;
        _playerView.hidden = YES;
    }
    [self tick];
}

- (double)positionSeconds
{
    if (!_player) return 0;
    double seconds = CMTimeGetSeconds(_player.currentTime);
    return isfinite(seconds) ? seconds : 0;
}

- (void)tick
{
    if (_tornDown) return;
    SoilAdsFaultForTests(@"tick");
    CFTimeInterval now = CACurrentMediaTime();
    double elapsed = now - _lastTick;
    _lastTick = now;
    if ([self isCounting] && elapsed > 0) _visibleSeconds += fmin(elapsed, 1.0);

    if (_player && !_videoFailed) {
        AVPlayerItem *item = _player.currentItem;
        if (!item || item.status == AVPlayerItemStatusFailed || _player.status == AVPlayerStatusFailed) {
            [self videoDidFail];
            return; // videoDidFail ticks again
        }
    }

    BOOL unlockedNow = [_lockPolicy updateWithVisibleSeconds:_visibleSeconds
                                             positionSeconds:[self positionSeconds]
                                                  videoEnded:_videoEnded
                                                 videoFailed:_videoFailed];
    [self refreshCloseButton];
    if (unlockedNow && [_lockPolicy takeReward]) [self.delegate fullscreenControllerDidEarnReward:self];
}

#pragma mark - Controls

- (void)refreshCloseButton
{
    if (!_closeButton) return;
    BOOL unlocked = _lockPolicy.unlocked;
    NSString *title = unlocked ? @"✕" : [NSString stringWithFormat:@"%ld", (long)_lockPolicy.secondsRemaining];
    if (![[_closeButton titleForState:UIControlStateNormal] isEqualToString:title])
        [_closeButton setTitle:title forState:UIControlStateNormal];
    // Locked: a dimmed button whose value is the countdown. Unlocked: a plain "Close" button,
    // and VoiceOver is moved to it.
    _closeButton.accessibilityValue = unlocked ? nil : title;
    _closeButton.accessibilityTraits = unlocked ? UIAccessibilityTraitButton
                                                : UIAccessibilityTraitButton | UIAccessibilityTraitNotEnabled;
    if (unlocked && !_closeShownUnlocked) {
        _closeShownUnlocked = YES;
        UIAccessibilityPostNotification(UIAccessibilityLayoutChangedNotification, _closeButton);
    }
}

- (void)refreshMuteButton
{
    if (!_muteButton) return;
    BOOL muted = _player.muted;
    if (@available(iOS 13.0, *)) {
        UIImage *icon = [UIImage systemImageNamed:muted ? @"speaker.slash.fill" : @"speaker.wave.2.fill"];
        [_muteButton setImage:icon forState:UIControlStateNormal];
    } else {
        [_muteButton setTitle:muted ? @"\U0001F507" : @"\U0001F50A" forState:UIControlStateNormal];
    }
    _muteButton.accessibilityLabel = muted ? @"Unmute" : @"Mute";
    _muteButton.accessibilityValue = muted ? @"muted" : @"sound";
}

- (void)muteTapped
{
    [self guard:@"mute" block:^{
        if (_tornDown || !_player) return;
        _player.muted = !_player.muted;
        [self refreshMuteButton];
    }];
}

- (void)closeTapped
{
    [self guard:@"close" block:^{
        if (_tornDown) {
            // The session already ended but UIKit never dismissed the ad (the player gave up
            // retrying): the button still takes it off screen, with no further events.
            if (self.presentingViewController.presentedViewController == self && !self.isBeingDismissed)
                [self.presentingViewController dismissViewControllerAnimated:NO completion:nil];
            return;
        }
        [self tick];
        if (_tornDown || !_lockPolicy.unlocked) return;
        [self.delegate fullscreenControllerDidRequestClose:self];
    }];
}

- (void)clickTapped
{
    [self guard:@"click" block:^{
        if (_tornDown) return;
        [self tick];
        _awayAfterClick = YES;
        [self updatePlayback];
        [self.delegate fullscreenControllerDidClick:self];
    }];
}

@end
