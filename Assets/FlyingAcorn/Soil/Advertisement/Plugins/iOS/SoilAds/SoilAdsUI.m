#import "SoilAdsUI.h"

NSString *const SoilAdsIdClose = @"soil_ad_close";
NSString *const SoilAdsIdCallToAction = @"soil_ad_cta";
NSString *const SoilAdsIdMedia = @"soil_ad_media";
NSString *const SoilAdsIdMute = @"soil_ad_mute";
NSString *const SoilAdsIdBanner = @"soil_ad_banner";

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
    NSDictionary *attributes = @{
        NSFontAttributeName: label.font ? label.font : [UIFont systemFontOfSize:14],
        NSForegroundColorAttributeName: label.textColor ? label.textColor : [UIColor whiteColor],
        NSParagraphStyleAttributeName: style,
    };
    label.attributedText = [[NSAttributedString alloc] initWithString:text attributes:attributes];
    label.textAlignment = style.alignment;
    label.semanticContentAttribute = rtl ? UISemanticContentAttributeForceRightToLeft
                                         : UISemanticContentAttributeForceLeftToRight;
    label.hidden = text.length == 0;
}

UILabel *SoilAdsMakeBadge(CGFloat fontSize)
{
    UILabel *badge = [[UILabel alloc] init];
    badge.translatesAutoresizingMaskIntoConstraints = NO;
    badge.text = @" Ad ";
    badge.font = [UIFont boldSystemFontOfSize:fontSize];
    badge.textColor = [UIColor blackColor];
    badge.backgroundColor = [UIColor colorWithRed:1.0 green:0.8 blue:0.2 alpha:1.0];
    badge.layer.cornerRadius = 3;
    badge.layer.masksToBounds = YES;
    badge.textAlignment = NSTextAlignmentCenter;
    badge.accessibilityLabel = @"Ad";
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

UIButton *SoilAdsMakeCallToActionButton(NSString *title, CGFloat fontSize)
{
    UIButton *button = [UIButton buttonWithType:UIButtonTypeCustom];
    button.translatesAutoresizingMaskIntoConstraints = NO;
    [button setTitle:title forState:UIControlStateNormal];
    [button setTitleColor:[UIColor whiteColor] forState:UIControlStateNormal];
    button.titleLabel.font = [UIFont boldSystemFontOfSize:fontSize];
    button.titleLabel.lineBreakMode = NSLineBreakByTruncatingTail;
    button.backgroundColor = [UIColor colorWithRed:0.0 green:0.48 blue:1.0 alpha:1.0];
    button.layer.cornerRadius = 8;
    SoilAdsSetButtonPadding(button, UIEdgeInsetsMake(8, 14, 8, 14));
    button.accessibilityIdentifier = SoilAdsIdCallToAction;
    [button setContentHuggingPriority:UILayoutPriorityRequired forAxis:UILayoutConstraintAxisHorizontal];
    [button setContentCompressionResistancePriority:UILayoutPriorityDefaultHigh + 1
                                            forAxis:UILayoutConstraintAxisHorizontal];
    // A long call to action must not squeeze the title and description away.
    [button.widthAnchor constraintLessThanOrEqualToConstant:160].active = YES;
    return button;
}
