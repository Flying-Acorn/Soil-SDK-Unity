// Minimal app that hosts the unit tests: one window with a plain root view controller,
// standing in for Unity's root view controller.
#import <UIKit/UIKit.h>

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
    return YES;
}
@end

int main(int argc, char *argv[])
{
    @autoreleasepool {
        return UIApplicationMain(argc, argv, nil, NSStringFromClass([SoilAdsHarnessAppDelegate class]));
    }
}
