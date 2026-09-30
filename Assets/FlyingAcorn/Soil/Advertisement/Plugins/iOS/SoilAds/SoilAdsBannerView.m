#import "SoilAdsBannerView.h"
#import "SoilAdsUI.h"
#include <math.h>

@implementation SoilAdsBannerView {
    NSLayoutConstraint *_positionConstraint;
    BOOL _built;
}

+ (CGFloat)heightForScreenSize:(CGSize)screenSize
{
    return fmin(screenSize.width, screenSize.height) >= 600 ? 90 : 50;
}

+ (CGFloat)heightForHostView:(UIView *)hostView
{
    CGSize size = hostView.window ? hostView.window.bounds.size : hostView.bounds.size;
    if (size.width <= 0 || size.height <= 0) size = [UIScreen mainScreen].bounds.size;
    return [self heightForScreenSize:size];
}

- (instancetype)initWithContent:(SoilAdsLoadedMedia *)content
{
    if ((self = [super initWithFrame:CGRectZero])) {
        _content = content;
        self.translatesAutoresizingMaskIntoConstraints = NO;
        self.backgroundColor = [UIColor colorWithWhite:0.11 alpha:1];
        self.clipsToBounds = YES;
        self.isAccessibilityElement = YES;
        self.accessibilityIdentifier = SoilAdsIdBanner;
        self.accessibilityTraits = UIAccessibilityTraitButton;
        self.accessibilityLabel = SoilAdsAccessibilityLabel(content.creative.title);
        [self addGestureRecognizer:[[UITapGestureRecognizer alloc] initWithTarget:self action:@selector(tapped)]];
    }
    return self;
}

/// Built on first attach, when the host (and so the banner's size class) is known.
- (void)buildContent:(BOOL)tall
{
    if (_content.image) {
        UIImageView *imageView = [[UIImageView alloc] initWithImage:_content.image];
        imageView.translatesAutoresizingMaskIntoConstraints = NO;
        imageView.contentMode = UIViewContentModeScaleAspectFit;
        [self addSubview:imageView];
        [NSLayoutConstraint activateConstraints:@[
            [imageView.topAnchor constraintEqualToAnchor:self.topAnchor],
            [imageView.bottomAnchor constraintEqualToAnchor:self.bottomAnchor],
            [imageView.leadingAnchor constraintEqualToAnchor:self.leadingAnchor],
            [imageView.trailingAnchor constraintEqualToAnchor:self.trailingAnchor],
        ]];
    } else {
        [self buildTextRow:tall];
    }

    UILabel *badge = SoilAdsMakeBadge(tall ? 11 : 9);
    [self addSubview:badge];
    [NSLayoutConstraint activateConstraints:@[
        [badge.topAnchor constraintEqualToAnchor:self.topAnchor constant:2],
        [badge.leadingAnchor constraintEqualToAnchor:self.leadingAnchor constant:2],
    ]];
}

- (void)buildTextRow:(BOOL)tall
{
    SoilAdsCreative *creative = _content.creative;
    UIStackView *row = [[UIStackView alloc] init];
    row.translatesAutoresizingMaskIntoConstraints = NO;
    row.axis = UILayoutConstraintAxisHorizontal;
    row.alignment = UIStackViewAlignmentCenter;
    row.spacing = 8;
    [self addSubview:row];
    CGFloat inset = tall ? 10 : 5;
    [NSLayoutConstraint activateConstraints:@[
        [row.topAnchor constraintEqualToAnchor:self.topAnchor constant:inset],
        [row.bottomAnchor constraintEqualToAnchor:self.bottomAnchor constant:-inset],
        [row.leadingAnchor constraintEqualToAnchor:self.leadingAnchor constant:16],
        [row.trailingAnchor constraintEqualToAnchor:self.trailingAnchor constant:-8],
    ]];

    if (_content.logo) {
        UIImageView *logo = [[UIImageView alloc] initWithImage:_content.logo];
        logo.translatesAutoresizingMaskIntoConstraints = NO;
        logo.contentMode = UIViewContentModeScaleAspectFill;
        logo.layer.cornerRadius = 6;
        logo.clipsToBounds = YES;
        [row addArrangedSubview:logo]; // before constraining it to the row
        [NSLayoutConstraint activateConstraints:@[
            [logo.widthAnchor constraintEqualToAnchor:logo.heightAnchor],
            [logo.heightAnchor constraintEqualToAnchor:row.heightAnchor],
        ]];
    }

    UIStackView *texts = [[UIStackView alloc] init];
    texts.axis = UILayoutConstraintAxisVertical;
    texts.spacing = 1;
    UILabel *title = [[UILabel alloc] init];
    title.font = [UIFont boldSystemFontOfSize:tall ? 18 : 14];
    title.textColor = [UIColor whiteColor];
    title.numberOfLines = 1;
    SoilAdsSetDirectionalText(title, creative.title);
    UILabel *body = [[UILabel alloc] init];
    body.font = [UIFont systemFontOfSize:tall ? 15 : 11];
    body.textColor = [UIColor colorWithWhite:0.8 alpha:1];
    body.numberOfLines = tall ? 2 : 1;
    SoilAdsSetDirectionalText(body, creative.adDescription);
    [texts addArrangedSubview:title];
    [texts addArrangedSubview:body];
    [texts setContentHuggingPriority:UILayoutPriorityDefaultLow forAxis:UILayoutConstraintAxisHorizontal];
    [texts setContentCompressionResistancePriority:UILayoutPriorityDefaultLow forAxis:UILayoutConstraintAxisHorizontal];
    [row addArrangedSubview:texts];

    if (creative.callToAction.length > 0) {
        UIButton *cta = SoilAdsMakeCallToActionButton(creative.callToAction, tall ? 16 : 13);
        SoilAdsSetButtonPadding(cta, UIEdgeInsetsMake(4, 10, 4, 10));
        cta.userInteractionEnabled = NO; // the whole banner is the click target
        [row addArrangedSubview:cta];
    }
}

- (void)attachToView:(UIView *)hostView position:(SoilAdsBannerPosition)position
{
    [self removeFromSuperview];
    _positionConstraint = nil;
    CGFloat height = [SoilAdsBannerView heightForHostView:hostView];
    if (!_built) {
        _built = YES;
        [self buildContent:height > 50];
    }
    [hostView addSubview:self];
    UILayoutGuide *safe = hostView.safeAreaLayoutGuide;
    [NSLayoutConstraint activateConstraints:@[
        [self.leadingAnchor constraintEqualToAnchor:safe.leadingAnchor],
        [self.trailingAnchor constraintEqualToAnchor:safe.trailingAnchor],
        [self.heightAnchor constraintEqualToConstant:height],
    ]];
    [self moveToPosition:position];
}

- (void)moveToPosition:(SoilAdsBannerPosition)position
{
    _position = position;
    UIView *hostView = self.superview;
    if (!hostView) return;
    _positionConstraint.active = NO;
    UILayoutGuide *safe = hostView.safeAreaLayoutGuide;
    switch (position) {
        case SoilAdsBannerPositionTop:
            _positionConstraint = [self.topAnchor constraintEqualToAnchor:safe.topAnchor];
            break;
        case SoilAdsBannerPositionCenter:
            _positionConstraint = [self.centerYAnchor constraintEqualToAnchor:safe.centerYAnchor];
            break;
        default:
            _positionConstraint = [self.bottomAnchor constraintEqualToAnchor:safe.bottomAnchor];
            break;
    }
    _positionConstraint.active = YES;
    [hostView bringSubviewToFront:self];
    [hostView setNeedsLayout];
}

- (void)tapped
{
    if (self.onClick) self.onClick();
}

@end
