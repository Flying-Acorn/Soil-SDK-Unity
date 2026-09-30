// Banner ("Banner presentation" in NativeAds/PROTOCOL.md).
#import <UIKit/UIKit.h>
#import "SoilAdsMediaLoader.h"

NS_ASSUME_NONNULL_BEGIN

@interface SoilAdsBannerView : UIView

/// 50 pt on phones, 90 pt when the smallest side of `screenSize` is at least 600 pt.
+ (CGFloat)heightForScreenSize:(CGSize)screenSize;

- (instancetype)initWithContent:(SoilAdsLoadedMedia *)content NS_DESIGNATED_INITIALIZER;
- (instancetype)initWithFrame:(CGRect)frame NS_UNAVAILABLE;
- (instancetype)initWithCoder:(NSCoder *)coder NS_UNAVAILABLE;

@property (nonatomic, readonly) SoilAdsLoadedMedia *content;
@property (nonatomic, readonly) SoilAdsBannerPosition position;
@property (nonatomic, copy, nullable) void (^onClick)(void);

/// Adds the banner to `hostView`, full safe-area width at `position`.
- (void)attachToView:(UIView *)hostView position:(SoilAdsBannerPosition)position;
/// Moves an attached banner.
- (void)moveToPosition:(SoilAdsBannerPosition)position;

@end

NS_ASSUME_NONNULL_END
