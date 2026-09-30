// The only file that knows about Unity: the C entry points C# calls through [DllImport("__Internal")]
// ("Calls" in NativeAds/PROTOCOL.md) and the host wiring to Unity's trampoline.
#import <UIKit/UIKit.h>
#import <AVFoundation/AVFoundation.h>
#include <stdbool.h>
#import "SoilAdsManager.h"

// Provided by Unity's trampoline (Classes/Unity/UnityInterface.h, C linkage) at link time.
extern UIViewController *UnityGetGLViewController(void);
extern void UnitySendMessage(const char *obj, const char *method, const char *msg);
extern void UnityPause(int pause);
extern int UnityIsPaused(void);
extern void UnityUpdateMuteState(int mute);

// Entry points called from C# (see NativeAds/PROTOCOL.md). `bool` is a 1-byte C bool:
// the C# import should use [return: MarshalAs(UnmanagedType.I1)].
void SoilAds_Initialize(const char *receiverObject, const char *receiverMethod);
void SoilAds_Load(const char *format, const char *creativeJson);
void SoilAds_Show(const char *format, const char *optionsJson);
void SoilAds_Hide(const char *format);
void SoilAds_Destroy(const char *format);
bool SoilAds_IsReady(const char *format);

@interface SoilAdsUnityHost : NSObject <SoilAdsHost>
@end

@implementation SoilAdsUnityHost {
    BOOL _pausedByAds;
}

- (UIViewController *)soilAdsRootViewController
{
    return UnityGetGLViewController();
}

// Known limitation, on purpose: while an ad holds Unity paused, the trampoline finds the player
// already paused when the app goes to the background (and leaves it paused when the app comes
// back), so a background trip during an ad does not reach the game's OnApplicationPause.
// We do not call Unity internals to fake it.
- (void)soilAdsSetGamePaused:(BOOL)paused
{
    if (paused) {
        // Leave the game as we found it: only a pause we made is undone afterwards.
        _pausedByAds = UnityIsPaused() == 0;
        if (_pausedByAds) UnityPause(1);
        return;
    }
    if (_pausedByAds) {
        _pausedByAds = NO;
        UnityPause(0);
    }
    // Same expression the trampoline uses, so game audio follows the device volume again.
    UnityUpdateMuteState([[AVAudioSession sharedInstance] outputVolume] < 0.01f ? 1 : 0);
}

@end

static NSString *SoilAdsReceiverObject;
static NSString *SoilAdsReceiverMethod;

static SoilAdsManager *SoilAdsSharedManager(void)
{
    static SoilAdsManager *manager;
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        manager = [[SoilAdsManager alloc] initWithHost:[[SoilAdsUnityHost alloc] init]];
        manager.eventSink = ^(NSString *json) {
            NSString *object = SoilAdsReceiverObject;
            NSString *method = SoilAdsReceiverMethod;
            if (object.length == 0 || method.length == 0) return; // no receiver yet: dropped
            UnitySendMessage(object.UTF8String, method.UTF8String, json.UTF8String);
        };
    });
    return manager;
}

/// Copies a C string right away; the caller's buffer is not valid after the call returns.
static NSString *SoilAdsCopyString(const char *text)
{
    if (!text) return nil;
    NSString *copy = [NSString stringWithUTF8String:text]; // nil for invalid UTF-8
    return copy ? copy : @"";
}

static void SoilAdsOnMain(dispatch_block_t block)
{
    if ([NSThread isMainThread]) block();
    else dispatch_async(dispatch_get_main_queue(), block);
}

void SoilAds_Initialize(const char *receiverObject, const char *receiverMethod)
{
    NSString *object = SoilAdsCopyString(receiverObject);
    NSString *method = SoilAdsCopyString(receiverMethod);
    SoilAdsOnMain(^{
        SoilAdsReceiverObject = object;
        SoilAdsReceiverMethod = method;
        SoilAdsSharedManager();
    });
}

void SoilAds_Load(const char *format, const char *creativeJson)
{
    NSString *formatName = SoilAdsCopyString(format);
    NSString *json = SoilAdsCopyString(creativeJson);
    SoilAdsOnMain(^{ [SoilAdsSharedManager() loadFormat:formatName creativeJSON:json]; });
}

void SoilAds_Show(const char *format, const char *optionsJson)
{
    NSString *formatName = SoilAdsCopyString(format);
    NSString *json = SoilAdsCopyString(optionsJson);
    SoilAdsOnMain(^{ [SoilAdsSharedManager() showFormat:formatName optionsJSON:json]; });
}

void SoilAds_Hide(const char *format)
{
    NSString *formatName = SoilAdsCopyString(format);
    SoilAdsOnMain(^{ [SoilAdsSharedManager() hideFormat:formatName]; });
}

void SoilAds_Destroy(const char *format)
{
    NSString *formatName = SoilAdsCopyString(format);
    SoilAdsOnMain(^{ [SoilAdsSharedManager() destroyFormat:formatName]; });
}

bool SoilAds_IsReady(const char *format)
{
    return [SoilAdsSharedManager() isReady:SoilAdsCopyString(format)] ? true : false;
}
