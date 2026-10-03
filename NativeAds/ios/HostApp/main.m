// Minimal app that hosts the unit tests: one window with a plain root view controller,
// standing in for Unity's root view controller.
//
// Launched on its own it can also preview an ad, for design reviews and screenshots:
//   SIMCTL_CHILD_SOILADS_PREVIEW_FORMAT=interstitial \
//   SIMCTL_CHILD_SOILADS_PREVIEW_CREATIVE='{"adId":"x","imagePath":"/abs/image.jpg","title":"..."}' \
//   xcrun simctl launch booted com.flyingacorn.soil.ads.harness
// SOILADS_PREVIEW_OPTIONS (show options JSON) is optional.
#import <UIKit/UIKit.h>

void SoilAds_Initialize(const char *receiverObject, const char *receiverMethod);
void SoilAds_Load(const char *format, const char *creativeJson);
void SoilAds_Show(const char *format, const char *optionsJson);

@interface SoilAdsHarnessRootViewController : UIViewController
@end

@implementation SoilAdsHarnessRootViewController
- (void)viewDidLoad
{
    [super viewDidLoad];
    self.view.backgroundColor = [UIColor darkGrayColor];
}
- (UIInterfaceOrientationMask)supportedInterfaceOrientations
{
    return UIInterfaceOrientationMaskAllButUpsideDown;
}
@end

@interface SoilAdsHarnessAppDelegate : UIResponder <UIApplicationDelegate>
@property (nonatomic, strong) UIWindow *window;
@end

@implementation SoilAdsHarnessAppDelegate
- (BOOL)application:(UIApplication *)application didFinishLaunchingWithOptions:(NSDictionary *)launchOptions
{
    self.window = [[UIWindow alloc] initWithFrame:[UIScreen mainScreen].bounds];
    self.window.rootViewController = [[SoilAdsHarnessRootViewController alloc] init];
    [self.window makeKeyAndVisible];
    [self startPreviewIfAsked];
    return YES;
}

- (void)startPreviewIfAsked
{
    NSDictionary<NSString *, NSString *> *env = [NSProcessInfo processInfo].environment;
    NSString *format = env[@"SOILADS_PREVIEW_FORMAT"];
    NSString *creative = env[@"SOILADS_PREVIEW_CREATIVE"];
    if (format.length == 0 || creative.length == 0) return;
    NSString *options = env[@"SOILADS_PREVIEW_OPTIONS"] != nil ? env[@"SOILADS_PREVIEW_OPTIONS"] : @"";
    SoilAds_Initialize("SoilAdsPreview", "OnEvent");
    SoilAds_Load(format.UTF8String, creative.UTF8String);
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        SoilAds_Show(format.UTF8String, options.UTF8String);
    });
}
@end

int main(int argc, char *argv[])
{
    @autoreleasepool {
        return UIApplicationMain(argc, argv, nil, NSStringFromClass([SoilAdsHarnessAppDelegate class]));
    }
}
