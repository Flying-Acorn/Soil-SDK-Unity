// Stand-ins for the Unity trampoline functions SoilAdsUnityBridge.m links against,
// with the exact signatures from Unity's Classes/Unity/UnityInterface.h. They record calls for the tests.
#import <UIKit/UIKit.h>
#import "UnityStubs.h"

static NSMutableArray<NSArray<NSString *> *> *SoilAdsStubMessageLog;
static NSMutableArray<NSNumber *> *SoilAdsStubPauseLog;
static NSMutableArray<NSNumber *> *SoilAdsStubMuteLog;

static void SoilAdsStubEnsure(void)
{
    if (!SoilAdsStubMessageLog) {
        SoilAdsStubMessageLog = [NSMutableArray array];
        SoilAdsStubPauseLog = [NSMutableArray array];
        SoilAdsStubMuteLog = [NSMutableArray array];
    }
}

UIViewController *UnityGetGLViewController(void)
{
    return [UIApplication sharedApplication].delegate.window.rootViewController;
}

void UnitySendMessage(const char *obj, const char *method, const char *msg)
{
    NSString *object = obj ? @(obj) : @"";
    NSString *name = method ? @(method) : @"";
    NSString *message = msg ? @(msg) : @"";
    @synchronized ([UIApplication class]) {
        SoilAdsStubEnsure();
        [SoilAdsStubMessageLog addObject:@[object, name, message]];
    }
}

void UnityPause(int pause)
{
    @synchronized ([UIApplication class]) {
        SoilAdsStubEnsure();
        [SoilAdsStubPauseLog addObject:@(pause)];
    }
}

void UnityUpdateMuteState(int mute)
{
    @synchronized ([UIApplication class]) {
        SoilAdsStubEnsure();
        [SoilAdsStubMuteLog addObject:@(mute)];
    }
}

NSArray<NSArray<NSString *> *> *SoilAdsStubMessages(void)
{
    @synchronized ([UIApplication class]) {
        SoilAdsStubEnsure();
        return [SoilAdsStubMessageLog copy];
    }
}

NSArray<NSNumber *> *SoilAdsStubPauseCalls(void)
{
    @synchronized ([UIApplication class]) {
        SoilAdsStubEnsure();
        return [SoilAdsStubPauseLog copy];
    }
}

NSArray<NSNumber *> *SoilAdsStubMuteCalls(void)
{
    @synchronized ([UIApplication class]) {
        SoilAdsStubEnsure();
        return [SoilAdsStubMuteLog copy];
    }
}

void SoilAdsStubReset(void)
{
    @synchronized ([UIApplication class]) {
        SoilAdsStubEnsure();
        [SoilAdsStubMessageLog removeAllObjects];
        [SoilAdsStubPauseLog removeAllObjects];
        [SoilAdsStubMuteLog removeAllObjects];
    }
}
