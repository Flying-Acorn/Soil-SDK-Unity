#import "SoilAdsUI.h"

NSString *const SoilAdsIdClose = @"soil_ad_close";
NSString *const SoilAdsIdCallToAction = @"soil_ad_cta";
NSString *const SoilAdsIdMedia = @"soil_ad_media";
NSString *const SoilAdsIdMute = @"soil_ad_mute";
NSString *const SoilAdsIdBanner = @"soil_ad_banner";
NSString *const SoilAdsBadgeText = @"\u062A\u0628\u0644\u06CC\u063A"; // تبلیغ

/// A label with padding around its text, for the badge.
@interface SoilAdsBadgeLabel : UILabel
@property (nonatomic) UIEdgeInsets insets;
@end

@implementation SoilAdsBadgeLabel
- (CGSize)intrinsicContentSize
{
    CGSize size = [super intrinsicContentSize];
    return CGSizeMake(ceil(size.width + _insets.left + _insets.right), ceil(size.height + _insets.top + _insets.bottom));
}
- (void)drawTextInRect:(CGRect)rect
{
    [super drawTextInRect:UIEdgeInsetsInsetRect(rect, _insets)];
}
@end

/// Unicode blocks whose letters are strong right-to-left (bidi classes R / AL).
static BOOL SoilAdsIsRightToLeftCodePoint(UTF32Char c)
{
    // Digits and separators inside the right-to-left blocks are not strong; skip them.
    if ((c >= 0x0660 && c <= 0x066C) || (c >= 0x06F0 && c <= 0x06F9)) return NO;
    return (c >= 0x0590 && c <= 0x08FF)      // right-to-left scripts of the basic plane
        || (c >= 0xFB1D && c <= 0xFDFF)      // presentation forms A
        || (c >= 0xFE70 && c <= 0xFEFF)      // presentation forms B
        || (c >= 0x10800 && c <= 0x10FFF)    // historic right-to-left scripts
        || (c >= 0x1E800 && c <= 0x1EFFF);   // more right-to-left scripts and math symbols
}

BOOL SoilAdsTextIsRightToLeft(NSString *text)
{
    if (![text isKindOfClass:[NSString class]]) return NO;
    NSCharacterSet *letters = [NSCharacterSet letterCharacterSet];
    NSUInteger length = text.length;
    for (NSUInteger i = 0; i < length; i++) {
        unichar unit = [text characterAtIndex:i];
        UTF32Char c = unit;
        if (CFStringIsSurrogateHighCharacter(unit) && i + 1 < length) {
            unichar low = [text characterAtIndex:i + 1];
            if (CFStringIsSurrogateLowCharacter(low)) {
                c = CFStringGetLongCharacterForSurrogatePair(unit, low);
                i++;
            }
        }
        if (SoilAdsIsRightToLeftCodePoint(c)) return YES;
        if ([letters longCharacterIsMember:c]) return NO;
    }
    return NO;
}

void SoilAdsSetDirectionalText(UILabel *label, NSString *text)
{
    text = [text isKindOfClass:[NSString class]] ? text : @"";
    BOOL rtl = SoilAdsTextIsRightToLeft(text);
    NSMutableParagraphStyle *style = [[NSMutableParagraphStyle alloc] init];
    style.alignment = rtl ? NSTextAlignmentRight : NSTextAlignmentLeft;
    style.baseWritingDirection = rtl ? NSWritingDirectionRightToLeft : NSWritingDirectionLeftToRight;
    style.lineBreakMode = NSLineBreakByTruncatingTail;
    UIFont *font = label.font ? label.font : [UIFont systemFontOfSize:14];
    // Wrapped lines get some air: Persian's tall marks crowd the line above at the default spacing.
    if (label.numberOfLines != 1) style.lineSpacing = round(font.pointSize * 0.2);
    NSDictionary *attributes = @{
        NSFontAttributeName: font,
        NSForegroundColorAttributeName: label.textColor ? label.textColor : [UIColor whiteColor],
        NSParagraphStyleAttributeName: style,
    };
    label.attributedText = [[NSAttributedString alloc] initWithString:text attributes:attributes];
    label.textAlignment = style.alignment;
    label.semanticContentAttribute = rtl ? UISemanticContentAttributeForceRightToLeft
                                         : UISemanticContentAttributeForceLeftToRight;
    label.hidden = text.length == 0;
}

BOOL SoilAdsCreativeIsRightToLeft(NSString *title, NSString *adDescription, NSString *callToAction)
{
    NSString *texts[] = {title, adDescription, callToAction};
    for (size_t i = 0; i < sizeof(texts) / sizeof(texts[0]); i++) {
        NSString *text = texts[i];
        if ([text isKindOfClass:[NSString class]] && text.length > 0) return SoilAdsTextIsRightToLeft(text);
    }
    return NO;
}

UISemanticContentAttribute SoilAdsSemanticAttribute(BOOL rightToLeft)
{
    return rightToLeft ? UISemanticContentAttributeForceRightToLeft : UISemanticContentAttributeForceLeftToRight;
}

NSString *SoilAdsAccessibilityLabel(NSString *title)
{
    if (![title isKindOfClass:[NSString class]] || title.length == 0) return @"Ad";
    return [NSString stringWithFormat:@"Ad, %@", title];
}

UILabel *SoilAdsMakeBadge(CGFloat fontSize)
{
    SoilAdsBadgeLabel *badge = [[SoilAdsBadgeLabel alloc] init];
    badge.translatesAutoresizingMaskIntoConstraints = NO;
    badge.insets = UIEdgeInsetsMake(round(fontSize * 0.15), round(fontSize * 0.5),
                                    round(fontSize * 0.15), round(fontSize * 0.5));
    badge.text = SoilAdsBadgeText;
    badge.font = [UIFont systemFontOfSize:fontSize weight:UIFontWeightBold];
    badge.textColor = [UIColor blackColor];
    badge.backgroundColor = [UIColor colorWithRed:1.0 green:0.8 blue:0.2 alpha:1.0];
    badge.layer.cornerRadius = round(fontSize * 0.4);
    badge.layer.masksToBounds = YES;
    badge.textAlignment = NSTextAlignmentCenter;
    badge.accessibilityLabel = @"Ad";
    [badge setContentHuggingPriority:UILayoutPriorityRequired forAxis:UILayoutConstraintAxisHorizontal];
    [badge setContentCompressionResistancePriority:UILayoutPriorityRequired forAxis:UILayoutConstraintAxisHorizontal];
    return badge;
}

void SoilAdsSetButtonPadding(UIButton *button, UIEdgeInsets insets)
{
    // Still honored for buttons without a UIButtonConfiguration, which iOS 12 does not have.
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
    button.contentEdgeInsets = insets;
#pragma clang diagnostic pop
}

UIColor *SoilAdsCallToActionColor(void)
{
    return [UIColor colorWithRed:0.04 green:0.52 blue:1.0 alpha:1.0];
}

UIButton *SoilAdsMakeCallToActionButton(NSString *title, CGFloat fontSize)
{
    UIButton *button = [UIButton buttonWithType:UIButtonTypeCustom];
    button.translatesAutoresizingMaskIntoConstraints = NO;
    [button setTitle:title forState:UIControlStateNormal];
    [button setTitleColor:[UIColor whiteColor] forState:UIControlStateNormal];
    button.titleLabel.font = [UIFont systemFontOfSize:fontSize weight:UIFontWeightSemibold];
    button.titleLabel.lineBreakMode = NSLineBreakByTruncatingTail;
    button.backgroundColor = SoilAdsCallToActionColor();
    button.layer.cornerRadius = 8;
    SoilAdsSetButtonPadding(button, UIEdgeInsetsMake(8, 14, 8, 14));
    button.accessibilityIdentifier = SoilAdsIdCallToAction;
    return button;
}

UIButton *SoilAdsMakeCompactCallToActionButton(NSString *title, CGFloat fontSize)
{
    UIButton *button = SoilAdsMakeCallToActionButton(title, fontSize);
    [button setContentHuggingPriority:UILayoutPriorityRequired forAxis:UILayoutConstraintAxisHorizontal];
    [button setContentCompressionResistancePriority:UILayoutPriorityDefaultHigh + 1
                                            forAxis:UILayoutConstraintAxisHorizontal];
    // A long call to action must not squeeze the title and description away.
    [button.widthAnchor constraintLessThanOrEqualToConstant:160].active = YES;
    return button;
}
