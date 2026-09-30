#import <XCTest/XCTest.h>
#import <UIKit/UIKit.h>
#import "SoilAdsManager.h"

NS_ASSUME_NONNULL_BEGIN

/// Spins the main run loop until `condition` holds or `timeout` passes. Returns the final value.
BOOL SoilAdsWaitUntil(NSTimeInterval timeout, BOOL (^condition)(void));
/// Spins the main run loop for `seconds`.
void SoilAdsSpin(NSTimeInterval seconds);
/// Absolute path of a file in the test bundle's resources.
NSString *SoilAdsResource(NSString *name);
/// Compact JSON text of a dictionary.
NSString *SoilAdsJSON(NSDictionary *object);
/// Every descendant of `view` (including itself) with this accessibility identifier.
NSArray<UIView *> *SoilAdsFindViews(UIView *view, NSString *identifier);

@interface SoilAdsFakeHost : NSObject <SoilAdsHost>
@property (nonatomic, strong, nullable) UIViewController *root;
@property (nonatomic, readonly) NSMutableArray<NSNumber *> *pauseCalls;
@property (nonatomic, readonly) NSMutableArray<NSURL *> *openedURLs;
@property (nonatomic) BOOL openResult;
@end

@interface SoilAdsEventRecorder : NSObject
@property (nonatomic, readonly) NSMutableArray<NSDictionary *> *events;
@property (nonatomic, readonly) SoilAdsEventSink sink;
- (NSArray<NSDictionary *> *)eventsNamed:(NSString *)name;
- (NSUInteger)countOf:(NSString *)name;
/// "shown,clicked,closed" for the given format.
- (NSString *)sequenceForFormat:(NSString *)format;
- (nullable NSDictionary *)last;
@end

NS_ASSUME_NONNULL_END
