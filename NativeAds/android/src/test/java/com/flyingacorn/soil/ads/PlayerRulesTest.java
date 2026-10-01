package com.flyingacorn.soil.ads;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

/** Click URL filter, saved video position and image format rules of the Android player. */
public class PlayerRulesTest {

    @Test
    public void webStoreAndAppLinksCanBeOpened() {
        for (String url : new String[]{
                "https://example.com/click?x=1",
                "http://example.com",
                "HTTPS://EXAMPLE.COM",
                "market://details?id=com.example.game",
                "storeapp://details?id=com.example.game",
                "some-app+x.y://open/page",
                "httpsx://example.com",
        }) {
            assertEquals(url, Ui.clickUrlToOpen(url));
            assertTrue(url, Ui.isOpenableClickUrl(url));
        }
    }

    @Test
    public void linksThatActOnTheDeviceAreRefused() {
        for (String url : new String[]{
                "javascript:alert(1)",
                "JavaScript:alert(1)",
                "vbscript:x",
                "data:text/html,x",
                "file:///data/data/com.example/shared_prefs/x.xml",
                "content://com.example.provider/secret",
                "intent://scan/#Intent;scheme=zxing;package=com.example;end",
                "intent:#Intent;component=com.example/.Secret;end",
                "android-app://com.example/https/x",
                "about:blank",
                "blob:https://example.com/x",
                "tel:+100",
                "sms:+100",
                "smsto:+100",
                "mms:+100",
                "mmsto:+100",
                "mailto:a@example.com",
                "wtai://wp/mc;100",
        }) {
            assertNull(url, Ui.clickUrlToOpen(url));
        }
    }

    @Test
    public void malformedLinksAreRefused() {
        for (String url : new String[]{
                null, "", "   ",
                "example.com/no-scheme",
                "//example.com",
                "https:",
                "storeapp:",
                "https://exa\nmple.com",
                "app://exa\u0000mple",
                "1app://x",
                "my app://x",
                ":x",
        }) {
            assertNull(String.valueOf(url), Ui.clickUrlToOpen(url));
        }
    }

    @Test
    public void bridgeOpensOnlyTheLinksThePlayersOpen() {
        assertTrue(SoilAdsBridge.openLink("https://example.com/click?x=1"));
        assertTrue(SoilAdsBridge.openLink("market://details?id=com.example.game"));
        assertTrue(SoilAdsBridge.openLink(" storeapp://details?id=x "));
        for (String url : new String[]{
                null, "", "javascript:alert(1)", "intent:#Intent;component=com.example/.Secret;end",
                "file:///data/x", "tel:+100", "https:", "https://exa\nmple.com",
        }) {
            assertFalse(String.valueOf(url), SoilAdsBridge.openLink(url));
        }
    }

    @Test
    public void surroundingSpaceIsTrimmed() {
        assertEquals("https://example.com", Ui.clickUrlToOpen("  https://example.com \t"));
        assertEquals("storeapp://details?id=x", Ui.clickUrlToOpen("\u00a0storeapp://details?id=x\n"));
    }

    @Test
    public void schemeIsLowercaseAndValidated() {
        assertEquals("https", Ui.scheme("HtTpS://x"));
        assertEquals("a+b.c-d", Ui.scheme("a+b.c-d:x"));
        assertNull(Ui.scheme(":x"));
        assertNull(Ui.scheme("no scheme"));
        assertNull(Ui.scheme("a b:x"));
    }

    @Test
    public void savedVideoPositionNeverMovesBack() {
        FullscreenSession session = new FullscreenSession(AdFormats.INTERSTITIAL,
                new LoadedAd(AdCreative.parse("{\"adId\":\"a\"}"), AdFormats.MEDIA_VIDEO, 10000, null, null),
                ShowOptions.defaults(AdFormats.INTERSTITIAL), null, null);
        assertEquals(0, session.videoPositionMs);
        session.recordVideoPosition(2500);
        assertEquals(2500, session.videoPositionMs);
        session.recordVideoPosition(0); // a player still seeking after a resume
        assertEquals(2500, session.videoPositionMs);
        session.recordVideoPosition(2000); // resumed from the key frame before the saved position
        assertEquals(2500, session.videoPositionMs);
        session.recordVideoPosition(2600);
        assertEquals(2600, session.videoPositionMs);
    }
}
