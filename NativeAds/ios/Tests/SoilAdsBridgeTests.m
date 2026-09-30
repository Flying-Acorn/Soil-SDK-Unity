// Drives the real C entry points of SoilAdsUnityBridge.m against the Unity stubs in HostApp/UnityStubs.m.
#import <XCTest/XCTest.h>
#import "SoilAdsTestSupport.h"
#import "UnityStubs.h"

@interface SoilAdsBridgeTests : XCTestCase
@end

@implementation SoilAdsBridgeTests

- (void)setUp
{
    [super setUp];
    self.continueAfterFailure = NO;
    SoilAds_Initialize("SoilAdsReceiver", "OnNativeEvent");
    for (NSString *format in @[@"banner", @"interstitial", @"rewarded"]) SoilAds_Destroy(format.UTF8String);
    SoilAdsSpin(0.8);
    SoilAdsStubReset();
}

- (void)tearDown
{
    for (NSString *format in @[@"banner", @"interstitial", @"rewarded"]) SoilAds_Destroy(format.UTF8String);
    UIViewController *root = UnityGetGLViewController();
    SoilAdsWaitUntil(10, ^BOOL { return root.presentedViewController == nil; });
    [super tearDown];
}

- (NSArray<NSDictionary *> *)events
{
    NSMutableArray *events = [NSMutableArray array];
    for (NSArray<NSString *> *message in SoilAdsStubMessages()) {
        XCTAssertEqualObjects(message[0], @"SoilAdsReceiver");
        XCTAssertEqualObjects(message[1], @"OnNativeEvent");
        id event = [NSJSONSerialization JSONObjectWithData:[message[2] dataUsingEncoding:NSUTF8StringEncoding] options:0 error:nil];
        XCTAssertTrue([event isKindOfClass:[NSDictionary class]], @"%@", message[2]);
        [events addObject:event];
    }
    return events;
}

- (NSDictionary *)waitForEventCount:(NSUInteger)count
{
    XCTAssertTrue(SoilAdsWaitUntil(10, ^BOOL { return SoilAdsStubMessages().count >= count; }),
                  @"expected %lu events, got %@", (unsigned long)count, SoilAdsStubMessages());
    return [self events].lastObject;
}

- (void)testBannerRoundTripThroughCFunctions
{
    NSString *json = SoilAdsJSON(@{@"adId": @"b1", @"title": @"Word Master", @"callToAction": @"Install"});
    SoilAds_Load("banner", json.UTF8String);
    NSDictionary *loaded = [self waitForEventCount:1];
    XCTAssertEqualObjects(loaded[@"format"], @"banner");
    XCTAssertEqualObjects(loaded[@"event"], @"loaded");
    XCTAssertEqualObjects(loaded[@"media"], @"text");
    XCTAssertTrue(SoilAds_IsReady("banner"));
    XCTAssertFalse(SoilAds_IsReady("rewarded"));
    XCTAssertFalse(SoilAds_IsReady(NULL));
    XCTAssertFalse(SoilAds_IsReady("garbage"));

    SoilAds_Show("banner", NULL);
    XCTAssertEqualObjects([self waitForEventCount:2][@"event"], @"shown");
    UIView *rootView = UnityGetGLViewController().view;
    XCTAssertEqual(SoilAdsFindViews(rootView, @"soil_ad_banner").count, 1u);
    SoilAds_Hide("banner");
    XCTAssertEqualObjects([self waitForEventCount:3][@"event"], @"closed");
    XCTAssertEqual(SoilAdsFindViews(rootView, @"soil_ad_banner").count, 0u);
    XCTAssertEqual(SoilAdsStubPauseCalls().count, 0u);
}

- (void)testNullAndGarbageArguments
{
    SoilAds_Load(NULL, NULL);
    NSDictionary *a = [self waitForEventCount:1];
    XCTAssertEqualObjects(a[@"event"], @"loadFailed");
    XCTAssertEqualObjects(a[@"error"], @"invalid_format");
    XCTAssertEqualObjects(a[@"format"], @"");

    SoilAds_Load("rewarded", NULL);
    NSDictionary *b = [self waitForEventCount:2];
    XCTAssertEqualObjects(b[@"error"], @"invalid_creative");

    SoilAds_Load("rewarded", "{{{");
    XCTAssertEqualObjects([self waitForEventCount:3][@"error"], @"invalid_creative");

    const char invalidUTF8[] = {(char)0xC3, (char)0x28, 0};
    SoilAds_Load(invalidUTF8, "{}");
    XCTAssertEqualObjects([self waitForEventCount:4][@"error"], @"invalid_format");

    SoilAds_Show(NULL, NULL);
    XCTAssertEqualObjects([self waitForEventCount:5][@"error"], @"invalid_format");
    SoilAds_Show("interstitial", "not json");
    XCTAssertEqualObjects([self waitForEventCount:6][@"error"], @"not_loaded");

    SoilAds_Hide(NULL);
    SoilAds_Destroy(NULL);
    SoilAds_Hide("interstitial");
    SoilAdsSpin(0.3);
    XCTAssertEqual(SoilAdsStubMessages().count, 6u);
}

- (void)testCallsFromABackgroundThread
{
    NSString *json = SoilAdsJSON(@{@"adId": @"i1", @"imagePath": SoilAdsResource(@"image.png")});
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_DEFAULT, 0), ^{
        char *copy = strdup(json.UTF8String); // the bridge must copy before returning
        SoilAds_Load("interstitial", copy);
        memset(copy, 0, strlen(copy));
        free(copy);
    });
    NSDictionary *loaded = [self waitForEventCount:1];
    XCTAssertEqualObjects(loaded[@"event"], @"loaded");
    XCTAssertEqualObjects(loaded[@"media"], @"image");
    __block bool ready = false;
    XCTestExpectation *done = [self expectationWithDescription:@"isReady"];
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_DEFAULT, 0), ^{
        ready = SoilAds_IsReady("interstitial");
        [done fulfill];
    });
    [self waitForExpectations:@[done] timeout:10];
    XCTAssertTrue(ready);
}

- (void)testFullscreenPausesAndResumesUnity
{
    NSString *json = SoilAdsJSON(@{@"adId": @"r1", @"imagePath": SoilAdsResource(@"image.png")});
    SoilAds_Load("rewarded", json.UTF8String);
    [self waitForEventCount:1];
    SoilAds_Show("rewarded", "{\"imageLockSeconds\":0.3,\"videoLockFraction\":1,\"minVideoLockSeconds\":0,\"startMuted\":false}");
    XCTAssertEqualObjects([self waitForEventCount:2][@"event"], @"shown");
    XCTAssertEqualObjects(SoilAdsStubPauseCalls(), @[@1]);
    XCTAssertFalse(SoilAds_IsReady("rewarded"));
    XCTAssertEqualObjects([self waitForEventCount:3][@"event"], @"rewarded");
    SoilAds_Hide("rewarded");
    XCTAssertEqualObjects([self waitForEventCount:4][@"event"], @"closed");
    XCTAssertEqualObjects(SoilAdsStubPauseCalls(), (@[@1, @0]));
    XCTAssertEqual(SoilAdsStubMuteCalls().count, 1u, @"mute state restored after unpausing");
}

- (void)testAlreadyPausedGameIsLeftPaused
{
    NSString *json = SoilAdsJSON(@{@"adId": @"i1", @"imagePath": SoilAdsResource(@"image.png")});
    SoilAds_Load("interstitial", json.UTF8String);
    [self waitForEventCount:1];
    SoilAdsStubSetPaused(1); // paused by the game or the trampoline, not by the ad
    SoilAds_Show("interstitial", NULL);
    XCTAssertEqualObjects([self waitForEventCount:2][@"event"], @"shown");
    SoilAds_Hide("interstitial");
    XCTAssertEqualObjects([self waitForEventCount:3][@"event"], @"closed");
    XCTAssertEqual(SoilAdsStubPauseCalls().count, 0u, @"neither paused nor resumed by the ad");
    XCTAssertEqual(UnityIsPaused(), 1);

    // The next ad, with the game running again, pauses and resumes it.
    SoilAdsStubSetPaused(0);
    SoilAds_Load("interstitial", json.UTF8String);
    [self waitForEventCount:4];
    SoilAds_Show("interstitial", NULL);
    XCTAssertEqualObjects([self waitForEventCount:5][@"event"], @"shown");
    XCTAssertEqual(UnityIsPaused(), 1);
    SoilAds_Hide("interstitial");
    XCTAssertEqualObjects([self waitForEventCount:6][@"event"], @"closed");
    XCTAssertEqualObjects(SoilAdsStubPauseCalls(), (@[@1, @0]));
    XCTAssertEqual(UnityIsPaused(), 0);
}

- (void)testEventsWithoutReceiverAreDropped
{
    SoilAds_Initialize("", "");
    SoilAds_Load("banner", NULL);
    SoilAdsSpin(0.3);
    XCTAssertEqual(SoilAdsStubMessages().count, 0u);
    SoilAds_Initialize("Other", "Method");
    SoilAds_Load("banner", NULL);
    SoilAdsWaitUntil(5, ^BOOL { return SoilAdsStubMessages().count == 1; });
    XCTAssertEqualObjects(SoilAdsStubMessages().firstObject[0], @"Other", @"the last receiver wins");
    SoilAds_Initialize("SoilAdsReceiver", "OnNativeEvent");
}

@end
