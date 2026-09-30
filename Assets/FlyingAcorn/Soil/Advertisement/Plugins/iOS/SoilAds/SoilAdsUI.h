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

/// "Ad", or "Ad, <title>": what VoiceOver reads for the clickable ad (banner, fullscreen media).
FOUNDATION_EXPORT NSString *SoilAdsAccessibilityLabel(NSString *_Nullable title);

/// The small rounded "Ad" badge.
FOUNDATION_EXPORT UILabel *SoilAdsMakeBadge(CGFloat fontSize);

/// Padding around a custom button's title.
FOUNDATION_EXPORT void SoilAdsSetButtonPadding(UIButton *button, UIEdgeInsets insets);

/// Rounded call-to-action button.
FOUNDATION_EXPORT UIButton *SoilAdsMakeCallToActionButton(NSString *title, CGFloat fontSize);

NS_ASSUME_NONNULL_END
