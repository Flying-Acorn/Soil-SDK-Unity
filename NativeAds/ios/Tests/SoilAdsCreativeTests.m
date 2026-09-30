#import <XCTest/XCTest.h>
#import "SoilAdsCreative.h"
#import "SoilAdsUI.h"

@interface SoilAdsCreativeTests : XCTestCase
@end

@implementation SoilAdsCreativeTests

#pragma mark - Creative

- (void)testFullCreative
{
    NSString *json = @"{\"adId\":\"3f2c\",\"videoPath\":\"/tmp/v.mp4\",\"imagePath\":\"/tmp/i.jpg\",\"logoPath\":\"/tmp/l.png\","
                     @"\"title\":\"Word Master\",\"description\":\"Train your brain\",\"callToAction\":\"Install\","
                     @"\"clickUrl\":\"https://example.com/click?x=1\"}";
    SoilAdsCreative *c = [SoilAdsCreative creativeWithJSON:json];
    XCTAssertNotNil(c);
    XCTAssertEqualObjects(c.adId, @"3f2c");
    XCTAssertEqualObjects(c.videoPath, @"/tmp/v.mp4");
    XCTAssertEqualObjects(c.imagePath, @"/tmp/i.jpg");
    XCTAssertEqualObjects(c.logoPath, @"/tmp/l.png");
    XCTAssertEqualObjects(c.title, @"Word Master");
    XCTAssertEqualObjects(c.adDescription, @"Train your brain");
    XCTAssertEqualObjects(c.callToAction, @"Install");
    XCTAssertEqualObjects(c.clickUrl, @"https://example.com/click?x=1");
}

- (void)testMissingNullEmptyAndWrongTypes
{
    SoilAdsCreative *c = [SoilAdsCreative creativeWithJSON:
        @"{\"adId\":\"a\",\"videoPath\":null,\"imagePath\":\"\",\"title\":[1,2],\"description\":{\"x\":1},"
        @"\"callToAction\":7,\"clickUrl\":true}"];
    XCTAssertNotNil(c);
    XCTAssertEqualObjects(c.videoPath, @"");
    XCTAssertEqualObjects(c.imagePath, @"");
    XCTAssertEqualObjects(c.logoPath, @"");
    XCTAssertEqualObjects(c.title, @"");
    XCTAssertEqualObjects(c.adDescription, @"");
    XCTAssertEqualObjects(c.callToAction, @"7");
    XCTAssertEqualObjects(c.clickUrl, @"1");

    SoilAdsCreative *empty = [SoilAdsCreative creativeWithJSON:@"{}"];
    XCTAssertNotNil(empty, @"missing adId is tolerated");
    XCTAssertEqualObjects(empty.adId, @"");
    XCTAssertEqualObjects(empty.title, @"");
}

- (void)testPathsTrimmedTextKept
{
    SoilAdsCreative *c = [SoilAdsCreative creativeWithJSON:@"{\"imagePath\":\"  /tmp/a.png\\n\",\"title\":\" Hi \"}"];
    XCTAssertEqualObjects(c.imagePath, @"/tmp/a.png");
    XCTAssertEqualObjects(c.title, @" Hi ");
}

- (void)testUnicodeText
{
    NSString *title = @"مرحبا ❤️ \U0001F680";
    SoilAdsCreative *c = [SoilAdsCreative creativeWithDictionary:@{@"title": title}];
    XCTAssertEqualObjects(c.title, title);
    NSData *data = [NSJSONSerialization dataWithJSONObject:@{@"title": title} options:0 error:nil];
    SoilAdsCreative *d = [SoilAdsCreative creativeWithJSON:[[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding]];
    XCTAssertEqualObjects(d.title, title);
}

- (void)testBadJSON
{
    XCTAssertNil([SoilAdsCreative creativeWithJSON:nil]);
    XCTAssertNil([SoilAdsCreative creativeWithJSON:@""]);
    XCTAssertNil([SoilAdsCreative creativeWithJSON:@"{"]);
    XCTAssertNil([SoilAdsCreative creativeWithJSON:@"not json"]);
    XCTAssertNil([SoilAdsCreative creativeWithJSON:@"[]"]);
    XCTAssertNil([SoilAdsCreative creativeWithJSON:@"null"]);
    XCTAssertNil([SoilAdsCreative creativeWithJSON:@"42"]);
    XCTAssertNil([SoilAdsCreative creativeWithJSON:@"\"text\""]);
    XCTAssertNil([SoilAdsCreative creativeWithJSON:(NSString *)(id)@42]);
}

#pragma mark - Show options

- (void)testDefaults
{
    SoilAdsShowOptions *i = [SoilAdsShowOptions defaultsForFormat:SoilAdsFormatInterstitial];
    XCTAssertEqual(i.imageLockSeconds, 5);
    XCTAssertEqual(i.videoLockFraction, 0.8);
    XCTAssertEqual(i.minVideoLockSeconds, 5);
    XCTAssertFalse(i.startMuted);
    SoilAdsShowOptions *r = [SoilAdsShowOptions defaultsForFormat:SoilAdsFormatRewarded];
    XCTAssertEqual(r.imageLockSeconds, 20);
    XCTAssertEqual(r.videoLockFraction, 1.0);
    XCTAssertEqual(r.minVideoLockSeconds, 0);
    XCTAssertEqual([SoilAdsShowOptions defaultsForFormat:SoilAdsFormatBanner].position, SoilAdsBannerPositionBottom);
}

- (void)testFullOptions
{
    SoilAdsShowOptions *o = [SoilAdsShowOptions optionsWithJSON:
        @"{\"imageLockSeconds\":3,\"videoLockFraction\":0.5,\"minVideoLockSeconds\":2,\"startMuted\":true}"
                                                         format:SoilAdsFormatRewarded];
    XCTAssertEqual(o.imageLockSeconds, 3);
    XCTAssertEqual(o.videoLockFraction, 0.5);
    XCTAssertEqual(o.minVideoLockSeconds, 2);
    XCTAssertTrue(o.startMuted);
    SoilAdsLockSettings s = o.lockSettings;
    XCTAssertEqual(s.imageLockSeconds, 3);
    XCTAssertEqual(s.videoLockFraction, 0.5);
    XCTAssertEqual(s.minVideoLockSeconds, 2);
}

- (void)testPartialInvalidAndStringOptions
{
    SoilAdsShowOptions *o = [SoilAdsShowOptions optionsWithJSON:
        @"{\"imageLockSeconds\":\"7\",\"videoLockFraction\":-1,\"minVideoLockSeconds\":null,\"startMuted\":\"TRUE\"}"
                                                         format:SoilAdsFormatInterstitial];
    XCTAssertEqual(o.imageLockSeconds, 7);
    XCTAssertEqual(o.videoLockFraction, 0.8, @"negative keeps the default");
    XCTAssertEqual(o.minVideoLockSeconds, 5, @"null keeps the default");
    XCTAssertTrue(o.startMuted);
    XCTAssertTrue([SoilAdsShowOptions optionsWithJSON:@"{\"startMuted\":1}" format:SoilAdsFormatRewarded].startMuted);
    XCTAssertFalse([SoilAdsShowOptions optionsWithJSON:@"{\"startMuted\":\"no\"}" format:SoilAdsFormatRewarded].startMuted);
}

- (void)testBadOptionsJSONGivesDefaults
{
    for (NSString *json in @[@"", @"{", @"[]", @"null", @"garbage"]) {
        SoilAdsShowOptions *o = [SoilAdsShowOptions optionsWithJSON:json format:SoilAdsFormatRewarded];
        XCTAssertEqual(o.imageLockSeconds, 20, @"%@", json);
        XCTAssertEqual(o.position, SoilAdsBannerPositionBottom, @"%@", json);
    }
    XCTAssertEqual([SoilAdsShowOptions optionsWithJSON:nil format:SoilAdsFormatInterstitial].imageLockSeconds, 5);
}

- (void)testBannerPosition
{
    struct { NSString *json; SoilAdsBannerPosition expected; } rows[] = {
        {@"{\"position\":\"bottom\"}", SoilAdsBannerPositionBottom},
        {@"{\"position\":\"top\"}", SoilAdsBannerPositionTop},
        {@"{\"position\":\"center\"}", SoilAdsBannerPositionCenter},
        {@"{\"position\":\"TOP\"}", SoilAdsBannerPositionTop},
        {@"{\"position\":\"left\"}", SoilAdsBannerPositionBottom},
        {@"{\"position\":null}", SoilAdsBannerPositionBottom},
        {@"{}", SoilAdsBannerPositionBottom},
    };
    for (size_t i = 0; i < sizeof(rows) / sizeof(rows[0]); i++)
        XCTAssertEqual([SoilAdsShowOptions optionsWithJSON:rows[i].json format:SoilAdsFormatBanner].position,
                       rows[i].expected, @"%@", rows[i].json);
}

#pragma mark - Formats

- (void)testFormatNames
{
    XCTAssertEqual(SoilAdsFormatFromString(@"banner"), SoilAdsFormatBanner);
    XCTAssertEqual(SoilAdsFormatFromString(@"interstitial"), SoilAdsFormatInterstitial);
    XCTAssertEqual(SoilAdsFormatFromString(@"rewarded"), SoilAdsFormatRewarded);
    XCTAssertEqual(SoilAdsFormatFromString(@"native"), SoilAdsFormatInvalid);
    XCTAssertEqual(SoilAdsFormatFromString(@"Banner"), SoilAdsFormatInvalid);
    XCTAssertEqual(SoilAdsFormatFromString(@""), SoilAdsFormatInvalid);
    XCTAssertEqual(SoilAdsFormatFromString(nil), SoilAdsFormatInvalid);
    XCTAssertEqualObjects(SoilAdsFormatName(SoilAdsFormatRewarded), @"rewarded");
    XCTAssertEqualObjects(SoilAdsMediaName(SoilAdsMediaVideo), @"video");
    XCTAssertEqualObjects(SoilAdsMediaName(SoilAdsMediaImage), @"image");
    XCTAssertEqualObjects(SoilAdsMediaName(SoilAdsMediaText), @"text");
}

#pragma mark - Text direction

- (void)testFirstStrongCharacterDecidesDirection
{
    NSString *rtlWord = @"שלום";         // right-to-left letters
    NSString *rtlWord2 = @"مرحبا";   // another right-to-left script
    XCTAssertFalse(SoilAdsTextIsRightToLeft(@"Hello"));
    XCTAssertTrue(SoilAdsTextIsRightToLeft(rtlWord));
    XCTAssertTrue(SoilAdsTextIsRightToLeft(rtlWord2));
    XCTAssertTrue(SoilAdsTextIsRightToLeft([@"123 - " stringByAppendingString:rtlWord]), @"digits are neutral");
    XCTAssertTrue(SoilAdsTextIsRightToLeft([@"٣٤ " stringByAppendingString:rtlWord2]), @"script digits are neutral");
    XCTAssertFalse(SoilAdsTextIsRightToLeft([@"٣٤ abc " stringByAppendingString:rtlWord2]));
    XCTAssertFalse(SoilAdsTextIsRightToLeft([@"Play " stringByAppendingString:rtlWord]));
    XCTAssertTrue(SoilAdsTextIsRightToLeft([rtlWord stringByAppendingString:@" Play"]));
    XCTAssertTrue(SoilAdsTextIsRightToLeft(@"\U00010900"), @"supplementary-plane right-to-left letter");
    XCTAssertFalse(SoilAdsTextIsRightToLeft(@"\U0001F680 !?"), @"only neutrals");
    XCTAssertFalse(SoilAdsTextIsRightToLeft(@""));
    XCTAssertFalse(SoilAdsTextIsRightToLeft(nil));
}

- (void)testLabelsAlignWithTheirText
{
    UILabel *label = [[UILabel alloc] init];
    SoilAdsSetDirectionalText(label, @"שלום");
    XCTAssertEqual(label.textAlignment, NSTextAlignmentRight);
    XCTAssertFalse(label.hidden);
    SoilAdsSetDirectionalText(label, @"Hello");
    XCTAssertEqual(label.textAlignment, NSTextAlignmentLeft);
    SoilAdsSetDirectionalText(label, nil);
    XCTAssertTrue(label.hidden);
}

@end
