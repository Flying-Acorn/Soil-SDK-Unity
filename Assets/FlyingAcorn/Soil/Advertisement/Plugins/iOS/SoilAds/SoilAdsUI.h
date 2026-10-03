// Small UIKit helpers shared by the banner and the fullscreen player.
#import <UIKit/UIKit.h>

NS_ASSUME_NONNULL_BEGIN

FOUNDATION_EXPORT NSString *const SoilAdsIdClose;
FOUNDATION_EXPORT NSString *const SoilAdsIdCallToAction;
FOUNDATION_EXPORT NSString *const SoilAdsIdMedia;
FOUNDATION_EXPORT NSString *const SoilAdsIdMute;
FOUNDATION_EXPORT NSString *const SoilAdsIdBanner;

/// YES when the first strong directional character of `text` belongs to a right-to-left script.
/// Neutral characters (digits, punctuation, spaces, symbols) are skipped.
FOUNDATION_EXPORT BOOL SoilAdsTextIsRightToLeft(NSString *_Nullable text);

/// Sets `text` on `label` aligned (and with base direction) following its first strong character.
FOUNDATION_EXPORT void SoilAdsSetDirectionalText(UILabel *label, NSString *_Nullable text);

/// Whether an ad's text block reads right to left: the direction of its title, else of its
/// description, else of its call to action. Logos and buttons are laid out to match.
FOUNDATION_EXPORT BOOL SoilAdsCreativeIsRightToLeft(NSString *_Nullable title, NSString *_Nullable adDescription,
                                                    NSString *_Nullable callToAction);

/// The forced layout direction for a row of ad views reading in that direction.
FOUNDATION_EXPORT UISemanticContentAttribute SoilAdsSemanticAttribute(BOOL rightToLeft);

/// "Ad", or "Ad, <title>": what VoiceOver reads for the clickable ad (banner, fullscreen media).
FOUNDATION_EXPORT NSString *SoilAdsAccessibilityLabel(NSString *_Nullable title);

/// The text of the ad badge on every format: Persian for "ad".
FOUNDATION_EXPORT NSString *const SoilAdsBadgeText;

/// The small rounded ad badge (SoilAdsBadgeText). VoiceOver reads it as "Ad".
FOUNDATION_EXPORT UILabel *SoilAdsMakeBadge(CGFloat fontSize);

/// Padding around a custom button's title.
FOUNDATION_EXPORT void SoilAdsSetButtonPadding(UIButton *button, UIEdgeInsets insets);

/// The call-to-action color shared by the banner and the fullscreen player.
FOUNDATION_EXPORT UIColor *SoilAdsCallToActionColor(void);

/// Rounded call-to-action button that takes the width it is given (fullscreen).
FOUNDATION_EXPORT UIButton *SoilAdsMakeCallToActionButton(NSString *title, CGFloat fontSize);

/// Rounded call-to-action button as wide as its title, at most 160 points (banner row).
FOUNDATION_EXPORT UIButton *SoilAdsMakeCompactCallToActionButton(NSString *title, CGFloat fontSize);

NS_ASSUME_NONNULL_END
