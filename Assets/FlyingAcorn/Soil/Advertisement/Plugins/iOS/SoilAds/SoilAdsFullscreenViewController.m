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

- (void)viewDidLoad
{
    [super viewDidLoad];
    self.view.backgroundColor = [UIColor blackColor];
    UILayoutGuide *safe = self.view.safeAreaLayoutGuide;

    _mediaView = [[UIView alloc] init];
    _mediaView.translatesAutoresizingMaskIntoConstraints = NO;
    _mediaView.backgroundColor = [UIColor blackColor];
    _mediaView.clipsToBounds = YES;
    _mediaView.isAccessibilityElement = YES;
    _mediaView.accessibilityIdentifier = SoilAdsIdMedia;
    _mediaView.accessibilityTraits = UIAccessibilityTraitButton;
    _mediaView.accessibilityLabel = _content.creative.title.length > 0 ? _content.creative.title : @"Ad";
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

    UIView *bottomBar = [self makeBottomBar];
    NSMutableArray<NSLayoutConstraint *> *constraints = [NSMutableArray arrayWithArray:@[
        [_mediaView.topAnchor constraintEqualToAnchor:safe.topAnchor],
        [_mediaView.leadingAnchor constraintEqualToAnchor:safe.leadingAnchor],
        [_mediaView.trailingAnchor constraintEqualToAnchor:safe.trailingAnchor],
    ]];
    if (bottomBar) {
        [self.view addSubview:bottomBar];
        [constraints addObjectsFromArray:@[
            [bottomBar.leadingAnchor constraintEqualToAnchor:safe.leadingAnchor constant:12],
            [bottomBar.trailingAnchor constraintEqualToAnchor:safe.trailingAnchor constant:-12],
            [bottomBar.bottomAnchor constraintEqualToAnchor:safe.bottomAnchor constant:-12],
            [_mediaView.bottomAnchor constraintEqualToAnchor:bottomBar.topAnchor constant:-8],
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

    _closeButton = [self makeRoundButton:40];
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

- (nullable UIView *)makeBottomBar
{
    SoilAdsCreative *creative = _content.creative;
    BOOL hasText = creative.title.length > 0 || creative.adDescription.length > 0;
    if (!_content.logo && !hasText && creative.callToAction.length == 0) return nil;

    UIView *bar = [[UIView alloc] init];
    bar.translatesAutoresizingMaskIntoConstraints = NO;
    bar.backgroundColor = [UIColor colorWithWhite:1 alpha:0.1];
    bar.layer.cornerRadius = 12;

    UIStackView *row = [[UIStackView alloc] init];
    row.translatesAutoresizingMaskIntoConstraints = NO;
    row.axis = UILayoutConstraintAxisHorizontal;
    row.alignment = UIStackViewAlignmentCenter;
    row.spacing = 12;
    [bar addSubview:row];
    // The bar is as short as its content allows; the media view takes the rest. Neither has an
    // intrinsic height (a stack view reports none), so without this Auto Layout may stretch the
    // bar over half the screen. Low priority: the labels' compression resistance still wins.
    NSLayoutConstraint *shortest = [bar.heightAnchor constraintEqualToConstant:0];
    shortest.priority = UILayoutPriorityDefaultLow;
    shortest.active = YES;
    [NSLayoutConstraint activateConstraints:@[
        [row.topAnchor constraintEqualToAnchor:bar.topAnchor constant:10],
        [row.bottomAnchor constraintEqualToAnchor:bar.bottomAnchor constant:-10],
        [row.leadingAnchor constraintEqualToAnchor:bar.leadingAnchor constant:10],
        [row.trailingAnchor constraintEqualToAnchor:bar.trailingAnchor constant:-10],
    ]];

    if (_content.logo) {
        UIImageView *logo = [[UIImageView alloc] initWithImage:_content.logo];
        logo.translatesAutoresizingMaskIntoConstraints = NO;
        logo.contentMode = UIViewContentModeScaleAspectFill;
        logo.layer.cornerRadius = 10;
        logo.clipsToBounds = YES;
        [NSLayoutConstraint activateConstraints:@[
            [logo.widthAnchor constraintEqualToConstant:48],
            [logo.heightAnchor constraintEqualToConstant:48],
        ]];
        [row addArrangedSubview:logo];
    }

    if (hasText) {
        UIStackView *texts = [[UIStackView alloc] init];
        texts.axis = UILayoutConstraintAxisVertical;
        texts.spacing = 2;
        UILabel *title = [[UILabel alloc] init];
        title.font = [UIFont boldSystemFontOfSize:16];
        title.textColor = [UIColor whiteColor];
        title.numberOfLines = 1;
        SoilAdsSetDirectionalText(title, creative.title);
        UILabel *body = [[UILabel alloc] init];
        body.font = [UIFont systemFontOfSize:13];
        body.textColor = [UIColor colorWithWhite:0.8 alpha:1];
        body.numberOfLines = 2;
        SoilAdsSetDirectionalText(body, creative.adDescription);
        [texts addArrangedSubview:title];
        [texts addArrangedSubview:body];
        [texts setContentHuggingPriority:UILayoutPriorityDefaultLow forAxis:UILayoutConstraintAxisHorizontal];
        [texts setContentCompressionResistancePriority:UILayoutPriorityDefaultLow forAxis:UILayoutConstraintAxisHorizontal];
        [row addArrangedSubview:texts];
    } else {
        UIView *spacer = [[UIView alloc] init];
        [spacer setContentHuggingPriority:UILayoutPriorityDefaultLow forAxis:UILayoutConstraintAxisHorizontal];
        [row addArrangedSubview:spacer];
    }

    if (creative.callToAction.length > 0) {
        _callToActionButton = SoilAdsMakeCallToActionButton(creative.callToAction, 15);
        [_callToActionButton addTarget:self action:@selector(clickTapped) forControlEvents:UIControlEventTouchUpInside];
        [row addArrangedSubview:_callToActionButton];
    }
    return bar;
}

#pragma mark - Lifecycle

- (void)viewDidAppear:(BOOL)animated
{
    [super viewDidAppear:animated];
    if (_tornDown) return;
    _appeared = YES;
    _appActive = [UIApplication sharedApplication].applicationState == UIApplicationStateActive;
    _lastTick = CACurrentMediaTime();
    if (!_timer) {
        __weak __typeof__(self) weakSelf = self;
        _timer = [NSTimer timerWithTimeInterval:SoilAdsTickInterval repeats:YES block:^(NSTimer *timer) {
            [weakSelf tick];
        }];
        [[NSRunLoop mainRunLoop] addTimer:_timer forMode:NSRunLoopCommonModes];
    }
    [self updatePlayback];
    [self tick];
}

- (void)viewDidDisappear:(BOOL)animated
{
    [super viewDidDisappear:animated];
    if (_tornDown) return;
    [self tick];
    _appeared = NO;
    [self updatePlayback];
    BOOL dismissed = self.isBeingDismissed || self.presentingViewController == nil
        || self.presentingViewController.isBeingDismissed;
    if (dismissed) [self.delegate fullscreenControllerDidDisappear:self];
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
                                          usingBlock:^(NSNotification *note) { [weakSelf appWillResignActive]; }]];
    [_observers addObject:[center addObserverForName:UIApplicationDidBecomeActiveNotification object:nil queue:main
                                          usingBlock:^(NSNotification *note) { [weakSelf appDidBecomeActive]; }]];
    AVPlayerItem *item = _player.currentItem;
    if (!item) return;
    [_observers addObject:[center addObserverForName:AVPlayerItemDidPlayToEndTimeNotification object:item queue:main
                                          usingBlock:^(NSNotification *note) { [weakSelf videoDidEnd]; }]];
    [_observers addObject:[center addObserverForName:AVPlayerItemFailedToPlayToEndTimeNotification object:item queue:main
                                          usingBlock:^(NSNotification *note) { [weakSelf videoDidFail]; }]];
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
    NSString *title = _lockPolicy.unlocked ? @"✕" : [NSString stringWithFormat:@"%ld", (long)_lockPolicy.secondsRemaining];
    if (![[_closeButton titleForState:UIControlStateNormal] isEqualToString:title])
        [_closeButton setTitle:title forState:UIControlStateNormal];
    _closeButton.accessibilityValue = _lockPolicy.unlocked ? @"unlocked" : title;
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
    if (_tornDown || !_player) return;
    _player.muted = !_player.muted;
    [self refreshMuteButton];
}

- (void)closeTapped
{
    if (_tornDown) return;
    [self tick];
    if (_tornDown || !_lockPolicy.unlocked) return;
    [self.delegate fullscreenControllerDidRequestClose:self];
}

- (void)clickTapped
{
    if (_tornDown) return;
    [self tick];
    _awayAfterClick = YES;
    [self updatePlayback];
    [self.delegate fullscreenControllerDidClick:self];
}

@end
