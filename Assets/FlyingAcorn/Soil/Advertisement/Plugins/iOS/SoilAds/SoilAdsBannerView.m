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
        // Behind the image: the same image, filled, blurred and darkened, so a 320x50 picture on a
        // wider screen does not sit between black bars.
        UIImageView *backdrop = [[UIImageView alloc] initWithImage:_content.image];
        backdrop.contentMode = UIViewContentModeScaleAspectFill;
        [self pinToEdges:backdrop];
        [self pinToEdges:[[UIVisualEffectView alloc] initWithEffect:[UIBlurEffect effectWithStyle:UIBlurEffectStyleDark]]];
        UIView *dim = [[UIView alloc] init];
        dim.backgroundColor = [UIColor colorWithWhite:0 alpha:0.25];
        [self pinToEdges:dim];

        // The image itself, aspect-fit and centered, with the badge on its corner.
        UIImageView *imageView = [[UIImageView alloc] initWithImage:_content.image];
        imageView.translatesAutoresizingMaskIntoConstraints = NO;
        imageView.contentMode = UIViewContentModeScaleAspectFit;
        [self addSubview:imageView];
        CGSize size = _content.image.size;
        CGFloat aspect = size.height > 0 ? size.width / size.height : 1;
        NSLayoutConstraint *fullHeight = [imageView.heightAnchor constraintEqualToAnchor:self.heightAnchor];
        fullHeight.priority = UILayoutPriorityDefaultHigh; // gives way on a screen too narrow for it
        [NSLayoutConstraint activateConstraints:@[
            [imageView.centerXAnchor constraintEqualToAnchor:self.centerXAnchor],
            [imageView.centerYAnchor constraintEqualToAnchor:self.centerYAnchor],
            [imageView.widthAnchor constraintEqualToAnchor:imageView.heightAnchor multiplier:aspect],
            [imageView.widthAnchor constraintLessThanOrEqualToAnchor:self.widthAnchor],
            [imageView.heightAnchor constraintLessThanOrEqualToAnchor:self.heightAnchor],
            fullHeight,
        ]];
        UILabel *badge = SoilAdsMakeBadge(tall ? 12 : 9);
        [self addSubview:badge];
        [NSLayoutConstraint activateConstraints:@[
            [badge.topAnchor constraintEqualToAnchor:imageView.topAnchor constant:2],
            [badge.leadingAnchor constraintEqualToAnchor:imageView.leadingAnchor constant:2],
        ]];
    } else {
        [self buildTextRow:tall]; // the badge goes before the title, clear of the logo and button
    }
}

- (void)pinToEdges:(UIView *)view
{
    view.translatesAutoresizingMaskIntoConstraints = NO;
    view.userInteractionEnabled = NO;
    [self addSubview:view];
    [NSLayoutConstraint activateConstraints:@[
        [view.topAnchor constraintEqualToAnchor:self.topAnchor],
        [view.bottomAnchor constraintEqualToAnchor:self.bottomAnchor],
        [view.leadingAnchor constraintEqualToAnchor:self.leadingAnchor],
        [view.trailingAnchor constraintEqualToAnchor:self.trailingAnchor],
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
    // Logo, texts and button run in the text's direction: the logo on the right for Persian.
    BOOL rtl = SoilAdsCreativeIsRightToLeft(creative.title, creative.adDescription, creative.callToAction);
    row.semanticContentAttribute = SoilAdsSemanticAttribute(rtl);
    [self addSubview:row];
    CGFloat inset = tall ? 10 : 5;
    CGFloat side = tall ? 12 : 8;
    [NSLayoutConstraint activateConstraints:@[
        [row.topAnchor constraintEqualToAnchor:self.topAnchor constant:inset],
        [row.bottomAnchor constraintEqualToAnchor:self.bottomAnchor constant:-inset],
        [row.leadingAnchor constraintEqualToAnchor:self.leadingAnchor constant:side],
        [row.trailingAnchor constraintEqualToAnchor:self.trailingAnchor constant:-side],
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
    title.font = [UIFont systemFontOfSize:tall ? 18 : 14 weight:UIFontWeightBold];
    title.textColor = [UIColor whiteColor];
    title.numberOfLines = 1;
    SoilAdsSetDirectionalText(title, creative.title);
    title.hidden = NO; // keeps the badge at the start of the line when there is no title
    [title setContentHuggingPriority:UILayoutPriorityDefaultLow forAxis:UILayoutConstraintAxisHorizontal];
    UIStackView *headline = [[UIStackView alloc] init];
    headline.axis = UILayoutConstraintAxisHorizontal;
    headline.alignment = UIStackViewAlignmentCenter;
    headline.spacing = tall ? 8 : 6;
    headline.semanticContentAttribute = SoilAdsSemanticAttribute(rtl);
    [headline addArrangedSubview:SoilAdsMakeBadge(tall ? 12 : 9)];
    [headline addArrangedSubview:title];
    UILabel *body = [[UILabel alloc] init];
    body.font = [UIFont systemFontOfSize:tall ? 15 : 11];
    body.textColor = [UIColor colorWithWhite:1 alpha:0.72];
    body.numberOfLines = tall ? 2 : 1;
    SoilAdsSetDirectionalText(body, creative.adDescription);
    [texts addArrangedSubview:headline];
    [texts addArrangedSubview:body];
    [texts setContentHuggingPriority:UILayoutPriorityDefaultLow forAxis:UILayoutConstraintAxisHorizontal];
    [texts setContentCompressionResistancePriority:UILayoutPriorityDefaultLow forAxis:UILayoutConstraintAxisHorizontal];
    [row addArrangedSubview:texts];

    if (creative.callToAction.length > 0) {
        UIButton *cta = SoilAdsMakeCompactCallToActionButton(creative.callToAction, tall ? 16 : 13);
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
