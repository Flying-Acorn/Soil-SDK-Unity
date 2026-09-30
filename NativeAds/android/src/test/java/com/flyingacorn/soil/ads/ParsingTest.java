package com.flyingacorn.soil.ads;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

import org.json.JSONObject;
import org.junit.Test;

/** AdCreative, ShowOptions and event JSON. */
public class ParsingTest {
    private static final double EPS = 1e-9;

    @Test
    public void creativeParsesEveryField() {
        AdCreative c = AdCreative.parse("{\"adId\":\"a1\",\"videoPath\":\"/v.mp4\",\"imagePath\":\"/i.jpg\","
                + "\"logoPath\":\"/l.png\",\"title\":\"Word Master\",\"description\":\"Train\","
                + "\"callToAction\":\"Install\",\"clickUrl\":\"https://example.com/c?x=1\"}");
        assertEquals("a1", c.adId);
        assertEquals("/v.mp4", c.videoPath);
        assertEquals("/i.jpg", c.imagePath);
        assertEquals("/l.png", c.logoPath);
        assertEquals("Word Master", c.title);
        assertEquals("Train", c.description);
        assertEquals("Install", c.callToAction);
        assertEquals("https://example.com/c?x=1", c.clickUrl);
        assertTrue(c.hasBottomBarText());
    }

    @Test
    public void creativeMissingNullAndEmptyFieldsAreNull() {
        AdCreative c = AdCreative.parse("{\"adId\":\"a1\",\"videoPath\":null,\"imagePath\":\"\","
                + "\"title\":\"   \",\"description\":null}");
        assertNull(c.videoPath);
        assertNull(c.imagePath);
        assertNull(c.logoPath);
        assertNull(c.title);
        assertNull(c.description);
        assertNull(c.callToAction);
        assertNull(c.clickUrl);
        assertFalse(c.hasBottomBarText());
    }

    @Test
    public void creativeKeepsRightToLeftText() {
        AdCreative c = AdCreative.parse("{\"adId\":\"a\",\"title\":\"بازی\"}");
        assertEquals("بازی", c.title);
    }

    @Test
    public void creativeRejectsAnythingButAnObject() {
        assertNull(AdCreative.parse(null));
        assertNull(AdCreative.parse(""));
        assertNull(AdCreative.parse("   "));
        assertNull(AdCreative.parse("null"));
        assertNull(AdCreative.parse("[1,2]"));
        assertNull(AdCreative.parse("{not json"));
        assertNull(AdCreative.parse("\"text\""));
    }

    @Test
    public void creativeWithoutAdIdStillParses() {
        AdCreative c = AdCreative.parse("{}");
        assertNull(c.adId);
    }

    @Test
    public void showOptionsParseEveryField() {
        ShowOptions o = ShowOptions.parse("interstitial", "{\"imageLockSeconds\":3,\"videoLockFraction\":0.5,"
                + "\"minVideoLockSeconds\":2,\"maxLockSeconds\":30,\"startMuted\":true}");
        assertEquals(3, o.imageLockSeconds, EPS);
        assertEquals(0.5, o.videoLockFraction, EPS);
        assertEquals(2, o.minVideoLockSeconds, EPS);
        assertEquals(30, o.maxLockSeconds, EPS);
        assertTrue(o.startMuted);
    }

    @Test
    public void showOptionsDefaultPerFormat() {
        ShowOptions interstitial = ShowOptions.parse("interstitial", null);
        assertEquals(5, interstitial.imageLockSeconds, EPS);
        assertEquals(0.8, interstitial.videoLockFraction, EPS);
        assertEquals(5, interstitial.minVideoLockSeconds, EPS);
        assertEquals(60, interstitial.maxLockSeconds, EPS);
        assertFalse(interstitial.startMuted);

        ShowOptions rewarded = ShowOptions.parse("rewarded", "garbage");
        assertEquals(20, rewarded.imageLockSeconds, EPS);
        assertEquals(1.0, rewarded.videoLockFraction, EPS);
        assertEquals(0, rewarded.minVideoLockSeconds, EPS);
        assertEquals(60, rewarded.maxLockSeconds, EPS);
    }

    @Test
    public void showOptionsFillMissingOrInvalidFieldsFromDefaults() {
        ShowOptions o = ShowOptions.parse("rewarded", "{\"imageLockSeconds\":\"abc\",\"maxLockSeconds\":null,"
                + "\"videoLockFraction\":0.25}");
        assertEquals(20, o.imageLockSeconds, EPS);
        assertEquals(60, o.maxLockSeconds, EPS);
        assertEquals(0.25, o.videoLockFraction, EPS);
        assertEquals(0, o.minVideoLockSeconds, EPS);
    }

    @Test
    public void showOptionsAcceptFloats() {
        ShowOptions o = ShowOptions.parse("interstitial", "{\"imageLockSeconds\":0.2}");
        assertEquals(0.2, o.imageLockSeconds, EPS);
    }

    @Test
    public void bannerPositions() {
        assertEquals("top", ShowOptions.bannerPosition("{\"position\":\"top\"}"));
        assertEquals("center", ShowOptions.bannerPosition("{\"position\":\"center\"}"));
        assertEquals("bottom", ShowOptions.bannerPosition("{\"position\":\"bottom\"}"));
        assertEquals("bottom", ShowOptions.bannerPosition("{\"position\":\"sideways\"}"));
        assertEquals("bottom", ShowOptions.bannerPosition("{\"position\":null}"));
        assertEquals("bottom", ShowOptions.bannerPosition("{}"));
        assertEquals("bottom", ShowOptions.bannerPosition(null));
        assertEquals("bottom", ShowOptions.bannerPosition("]["));
    }

    @Test
    public void loadedEventShape() throws Exception {
        JSONObject e = new JSONObject(AdEvents.loaded("rewarded", "video", 15000));
        assertEquals(4, e.length());
        assertEquals("rewarded", e.getString("format"));
        assertEquals("loaded", e.getString("event"));
        assertEquals("video", e.getString("media"));
        assertEquals(15000, e.getLong("durationMs"));
    }

    @Test
    public void failureEventShape() throws Exception {
        JSONObject e = new JSONObject(AdEvents.failure("banner", "showFailed", "not_loaded", null));
        assertEquals(4, e.length());
        assertEquals("banner", e.getString("format"));
        assertEquals("showFailed", e.getString("event"));
        assertEquals("not_loaded", e.getString("error"));
        assertEquals("", e.getString("message"));
    }

    @Test
    public void simpleEventShapeAndNullFormat() throws Exception {
        JSONObject e = new JSONObject(AdEvents.simple(null, "closed"));
        assertEquals(2, e.length());
        assertEquals("", e.getString("format"));
        assertEquals("closed", e.getString("event"));
    }

    @Test
    public void sampleSizeKeepsScreenResolution() {
        assertEquals(1, MediaLoader.sampleSize(1080, 1920, 2400, 5_184_000));
        assertEquals(4, MediaLoader.sampleSize(8000, 6000, 2400, 5_184_000)); // 2x on the side, 4x for pixels
        assertEquals(8, MediaLoader.sampleSize(8000, 6000, 2400, 1_000_000));
        assertEquals(1, MediaLoader.sampleSize(100, 100, 256, 256L * 256));
        assertEquals(16, MediaLoader.sampleSize(4096, 4096, 256, 256L * 256));
        assertEquals(1, MediaLoader.sampleSize(0, 0, 0, 0));
    }
}
