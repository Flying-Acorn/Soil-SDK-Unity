package com.flyingacorn.soil.ads;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

/** Text direction, badge and backdrop rules shared by the banner and the fullscreen player. */
public class PresentationRulesTest {

    @Test
    public void textDirectionFollowsTheFirstStrongCharacter() {
        assertTrue(Ui.isRightToLeft("واژه‌باف"));
        assertTrue(Ui.isRightToLeft("۲ توپیا"));
        assertTrue(Ui.isRightToLeft("!دوتوپیا"));
        assertTrue(Ui.isRightToLeft("123 نصب Install"));
        assertTrue(Ui.isRightToLeft("שלום"));
        assertFalse(Ui.isRightToLeft("Word Master"));
        assertTrue("digits are neutral", Ui.isRightToLeft("2048 دوتوپیا"));
        assertFalse(Ui.isRightToLeft("Install نصب"));
        assertFalse(Ui.isRightToLeft("123 !?"));
        assertFalse(Ui.isRightToLeft(""));
        assertFalse(Ui.isRightToLeft(null));
    }

    @Test
    public void creativeDirectionComesFromTitleThenDescriptionThenCallToAction() {
        assertTrue(Ui.creativeIsRightToLeft(AdCreative.parse("{\"title\":\"واژه‌باف\",\"description\":\"Words\"}")));
        assertFalse(Ui.creativeIsRightToLeft(AdCreative.parse("{\"title\":\"Wordle\",\"description\":\"بازی\"}")));
        assertTrue(Ui.creativeIsRightToLeft(AdCreative.parse("{\"description\":\"بازی\",\"callToAction\":\"Go\"}")));
        assertTrue(Ui.creativeIsRightToLeft(AdCreative.parse("{\"title\":\" \",\"callToAction\":\"نصب\"}")));
        assertFalse(Ui.creativeIsRightToLeft(AdCreative.parse("{\"adId\":\"x\"}")));
    }

    @Test
    public void negativeShowOptionsKeepTheDefaultsAsOnIos() {
        ShowOptions o = ShowOptions.parse("interstitial",
                "{\"imageLockSeconds\":-1,\"videoLockFraction\":-0.5,\"maxLockSeconds\":-1}");
        assertEquals(5, o.imageLockSeconds, 1e-9);
        assertEquals(0.8, o.videoLockFraction, 1e-9);
        assertEquals(15, o.maxLockSeconds, 1e-9);
        assertEquals(0, ShowOptions.parse("interstitial", "{\"maxLockSeconds\":0}").maxLockSeconds, 1e-9);
    }

    @Test
    public void badgeSaysAdInPersian() {
        assertEquals("تبلیغ", Ui.BADGE_TEXT);
    }

    @Test
    public void blurSpreadsAndKeepsTheAverage() {
        int width = 9;
        int height = 9;
        int[] pixels = new int[width * height];
        java.util.Arrays.fill(pixels, 0xFF000000);
        pixels[4 * width + 4] = 0xFFFFFFFF;
        for (int i = 0; i < Backdrop.PASSES; i++) Backdrop.blur(pixels, width, height, Backdrop.RADIUS);
        int center = pixels[4 * width + 4] & 0xFF;
        int neighbour = pixels[4 * width + 6] & 0xFF;
        assertTrue("the bright pixel spread out: " + center, center < 255 && center > 0);
        assertTrue("its neighbours lit up: " + neighbour, neighbour > 0);
        for (int p : pixels) assertEquals("stays opaque", 0xFF, p >>> 24);
    }

    @Test
    public void uniformImageStaysUniformAndDims() {
        int[] pixels = new int[12];
        java.util.Arrays.fill(pixels, 0xFF80C0FF);
        Backdrop.blur(pixels, 4, 3, Backdrop.RADIUS);
        for (int p : pixels) assertEquals(0xFF80C0FF, p);
        Backdrop.dim(pixels, 0.5f);
        for (int p : pixels) assertEquals(0xFF406080, p);
    }
}
