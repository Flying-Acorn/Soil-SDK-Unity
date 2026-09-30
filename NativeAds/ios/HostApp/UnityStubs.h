#import <UIKit/UIKit.h>
#include <stdbool.h>

// Unity trampoline stand-ins (defined in UnityStubs.m).
UIViewController *UnityGetGLViewController(void);
void UnitySendMessage(const char *obj, const char *method, const char *msg);
void UnityPause(int pause);
void UnityUpdateMuteState(int mute);

// What the stubs recorded: [object, method, message] triples, pause and mute arguments.
NSArray<NSArray<NSString *> *> *SoilAdsStubMessages(void);
NSArray<NSNumber *> *SoilAdsStubPauseCalls(void);
NSArray<NSNumber *> *SoilAdsStubMuteCalls(void);
void SoilAdsStubReset(void);

// The bridge's C entry points (SoilAdsUnityBridge.m).
void SoilAds_Initialize(const char *receiverObject, const char *receiverMethod);
void SoilAds_Load(const char *format, const char *creativeJson);
void SoilAds_Show(const char *format, const char *optionsJson);
void SoilAds_Hide(const char *format);
void SoilAds_Destroy(const char *format);
bool SoilAds_IsReady(const char *format);
