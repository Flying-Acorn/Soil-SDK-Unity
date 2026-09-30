#import "SoilAdsTestSupport.h"

BOOL SoilAdsWaitUntil(NSTimeInterval timeout, BOOL (^condition)(void))
{
    NSDate *deadline = [NSDate dateWithTimeIntervalSinceNow:timeout];
    while (!condition() && [deadline timeIntervalSinceNow] > 0) {
        [[NSRunLoop mainRunLoop] runMode:NSDefaultRunLoopMode beforeDate:[NSDate dateWithTimeIntervalSinceNow:0.02]];
    }
    return condition();
}

void SoilAdsSpin(NSTimeInterval seconds)
{
    NSDate *deadline = [NSDate dateWithTimeIntervalSinceNow:seconds];
    while ([deadline timeIntervalSinceNow] > 0) {
        [[NSRunLoop mainRunLoop] runMode:NSDefaultRunLoopMode beforeDate:[NSDate dateWithTimeIntervalSinceNow:0.02]];
    }
}

NSString *SoilAdsResource(NSString *name)
{
    NSBundle *bundle = [NSBundle bundleForClass:[SoilAdsFakeHost class]];
    NSString *path = [bundle pathForResource:[name stringByDeletingPathExtension] ofType:[name pathExtension]];
    NSCAssert(path != nil, @"missing test resource %@", name);
    return path ?: @"";
}

NSString *SoilAdsJSON(NSDictionary *object)
{
    NSData *data = [NSJSONSerialization dataWithJSONObject:object options:0 error:nil];
    return [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
}

static void SoilAdsCollect(UIView *view, NSString *identifier, NSMutableArray *out)
{
    if ([view.accessibilityIdentifier isEqualToString:identifier]) [out addObject:view];
    for (UIView *child in view.subviews) SoilAdsCollect(child, identifier, out);
}

NSArray<UIView *> *SoilAdsFindViews(UIView *view, NSString *identifier)
{
    NSMutableArray *out = [NSMutableArray array];
    if (view) SoilAdsCollect(view, identifier, out);
    return out;
}

@implementation SoilAdsFakeHost

- (instancetype)init
{
    if ((self = [super init])) {
        _pauseCalls = [NSMutableArray array];
        _openedURLs = [NSMutableArray array];
    }
    return self;
}

- (UIViewController *)soilAdsRootViewController { return self.root; }

- (void)soilAdsSetGamePaused:(BOOL)paused { [self.pauseCalls addObject:@(paused)]; }

- (void)soilAdsOpenURL:(NSURL *)url completion:(void (^)(BOOL))completion
{
    [self.openedURLs addObject:url];
    BOOL result = self.openResult;
    dispatch_async(dispatch_get_main_queue(), ^{ completion(result); });
}

@end

@implementation SoilAdsEventRecorder

- (instancetype)init
{
    if ((self = [super init])) {
        _events = [NSMutableArray array];
        __weak __typeof__(self) weakSelf = self;
        _sink = ^(NSString *json) {
            NSCAssert([NSThread isMainThread], @"events must arrive on the main thread");
            NSDictionary *event = [NSJSONSerialization JSONObjectWithData:[json dataUsingEncoding:NSUTF8StringEncoding]
                                                                  options:0 error:nil];
            NSCAssert([event isKindOfClass:[NSDictionary class]], @"event is not a JSON object: %@", json);
            [weakSelf.events addObject:event];
        };
    }
    return self;
}

- (NSArray<NSDictionary *> *)eventsNamed:(NSString *)name
{
    return [self.events filteredArrayUsingPredicate:[NSPredicate predicateWithFormat:@"event == %@", name]];
}

- (NSUInteger)countOf:(NSString *)name { return [self eventsNamed:name].count; }

- (NSString *)sequenceForFormat:(NSString *)format
{
    NSMutableArray *names = [NSMutableArray array];
    for (NSDictionary *event in self.events)
        if ([event[@"format"] isEqual:format]) [names addObject:event[@"event"]];
    return [names componentsJoinedByString:@","];
}

- (NSDictionary *)last { return self.events.lastObject; }

@end
