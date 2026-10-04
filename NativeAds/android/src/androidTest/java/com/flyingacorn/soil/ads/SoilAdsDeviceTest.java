package com.flyingacorn.soil.ads;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertNotSame;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;
import static org.junit.Assert.fail;

import android.app.Activity;
import android.app.Instrumentation;
import android.content.Context;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.Color;
import android.graphics.drawable.BitmapDrawable;
import android.graphics.drawable.Drawable;
import android.media.MediaPlayer;
import android.os.Build;
import android.os.SystemClock;
import android.view.KeyEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.widget.ImageView;
import android.widget.TextView;

import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;
import androidx.test.runner.lifecycle.ActivityLifecycleMonitorRegistry;
import androidx.test.runner.lifecycle.Stage;

import org.json.JSONObject;
import org.junit.After;
import org.junit.Before;
import org.junit.Test;
import org.junit.runner.RunWith;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;

/** Drives the real bridge, loader, Activity and banner on a device or emulator. */
@RunWith(AndroidJUnit4.class)
public class SoilAdsDeviceTest {
    private static final long TIMEOUT_MS = 30000; // generous: shared CI machines run emulators slowly
    private static final String CLICK_URL = "https://example.com/click?x=1";

    private final Instrumentation instrumentation = InstrumentationRegistry.getInstrumentation();
    private final Recorder events = new Recorder();
    private ActivityScenario<HostActivity> scenario;
    private HostActivity host;
    private File media;

    // ---- setup

    @Before
    public void setUp() throws Exception {
        Context context = instrumentation.getTargetContext();
        media = new File(context.getCacheDir(), "soil-ads-test");
        media.mkdirs();
        for (String name : new String[]{"video_3s.mp4", "video_10s.mp4", "audio_only.mp4", "image.png", "logo.png",
                "large.jpg"}) {
            copyAsset(name);
        }
        write("broken.mp4", "this is not a video");
        write("broken.png", "this is not an image");

        scenario = ActivityScenario.launch(HostActivity.class);
        final HostActivity[] holder = new HostActivity[1];
        scenario.onActivity(activity -> holder[0] = activity);
        host = holder[0];
        SoilAdsBridge.setEventSinkForTests(events);
        SoilAdsBridge.initialize(host, "SoilAds", "OnEvent");
        instrumentation.waitForIdleSync();
    }

    @After
    public void tearDown() {
        SoilAdActivity.failAtForTests = null;
        for (String format : new String[]{"banner", "interstitial", "rewarded"}) SoilAdsBridge.destroy(format);
        instrumentation.waitForIdleSync();
        SystemClock.sleep(300);
        SoilAdsBridge.setEventSinkForTests(null);
        if (scenario != null) scenario.close();
    }

    private void copyAsset(String name) throws Exception {
        try (InputStream in = instrumentation.getContext().getAssets().open(name);
             OutputStream out = new FileOutputStream(new File(media, name))) {
            byte[] buffer = new byte[8192];
            int read;
            while ((read = in.read(buffer)) > 0) out.write(buffer, 0, read);
        }
    }

    private void write(String name, String content) throws Exception {
        try (OutputStream out = new FileOutputStream(new File(media, name))) {
            out.write(content.getBytes("UTF-8"));
        }
    }

    private String path(String name) {
        return new File(media, name).getAbsolutePath();
    }

    /** Builds creative JSON from key/value pairs; file names become absolute paths. */
    private String creative(String... pairs) throws Exception {
        JSONObject json = new JSONObject();
        json.put("adId", "test-ad");
        for (int i = 0; i < pairs.length; i += 2) {
            String key = pairs[i];
            String value = pairs[i + 1];
            json.put(key, key.endsWith("Path") && value != null ? path(value) : value);
        }
        return json.toString();
    }

    private static String lockOptions(double imageLock, double videoFraction) throws Exception {
        JSONObject json = new JSONObject();
        json.put("imageLockSeconds", imageLock);
        json.put("videoLockFraction", videoFraction);
        json.put("minVideoLockSeconds", 0);
        json.put("maxLockSeconds", 0); // exactly the lock asked for: no 15 s interstitial cap
        json.put("startMuted", false);
        return json.toString();
    }

    private JSONObject load(String format, String creativeJson) throws Exception {
        events.clear();
        SoilAdsBridge.load(format, creativeJson);
        JSONObject event = events.awaitAny(format, "loaded", "loadFailed");
        events.clear();
        return event;
    }

    // ---- events

    /** Thread-safe list of received events. */
    private static final class Recorder implements EventSink {
        private final List<JSONObject> received = new ArrayList<>();

        @Override
        public synchronized void send(String json) {
            try {
                received.add(new JSONObject(json));
            } catch (Exception e) {
                throw new AssertionError("Not JSON: " + json);
            }
            notifyAll();
        }

        synchronized void clear() {
            received.clear();
        }

        synchronized List<String> names() {
            List<String> names = new ArrayList<>();
            for (JSONObject e : received) names.add(e.optString("format") + ":" + e.optString("event"));
            return names;
        }

        synchronized JSONObject awaitAny(String format, String... eventNames) throws InterruptedException {
            long deadline = SystemClock.uptimeMillis() + TIMEOUT_MS;
            while (true) {
                for (JSONObject e : received) {
                    if (format.equals(e.optString("format")) && Arrays.asList(eventNames).contains(e.optString("event"))) {
                        return e;
                    }
                }
                long left = deadline - SystemClock.uptimeMillis();
                if (left <= 0) {
                    throw new AssertionError("No " + format + ":" + Arrays.toString(eventNames) + " in " + names());
                }
                wait(left);
            }
        }

        JSONObject await(String format, String event) throws InterruptedException {
            return awaitAny(format, event);
        }
    }

    // ---- views

    private SoilAdActivity showFullscreen(String format, String options) throws Exception {
        Instrumentation.ActivityMonitor monitor =
                instrumentation.addMonitor(SoilAdActivity.class.getName(), null, false);
        try {
            SoilAdsBridge.show(format, options);
            Activity activity = monitor.waitForActivityWithTimeout(TIMEOUT_MS);
            assertNotNull("SoilAdActivity did not start", activity);
            events.await(format, "shown");
            return (SoilAdActivity) activity;
        } finally {
            instrumentation.removeMonitor(monitor);
        }
    }

    private View find(Activity activity, String description) {
        final View[] found = new View[1];
        instrumentation.runOnMainSync(() -> {
            ArrayList<View> views = new ArrayList<>();
            activity.getWindow().getDecorView().findViewsWithText(views, description,
                    View.FIND_VIEWS_WITH_CONTENT_DESCRIPTION);
            found[0] = views.isEmpty() ? null : views.get(0);
        });
        return found[0];
    }

    private void click(View view) {
        instrumentation.runOnMainSync(view::performClick);
        instrumentation.waitForIdleSync();
    }

    private String text(View view) {
        final String[] value = new String[1];
        instrumentation.runOnMainSync(() -> value[0] = ((TextView) view).getText().toString());
        return value[0];
    }

    private void awaitText(View view, String expected) {
        long deadline = SystemClock.uptimeMillis() + TIMEOUT_MS;
        while (!expected.equals(text(view))) {
            if (SystemClock.uptimeMillis() > deadline) fail("Text stayed '" + text(view) + "', expected " + expected);
            SystemClock.sleep(50);
        }
    }

    private void awaitGone(Activity activity) {
        long deadline = SystemClock.uptimeMillis() + TIMEOUT_MS;
        while (!activity.isDestroyed()) {
            if (SystemClock.uptimeMillis() > deadline) fail("Activity was not destroyed");
            SystemClock.sleep(50);
        }
    }

    // ---- load

    @Test
    public void loadVideoReportsVideoAndDuration() throws Exception {
        JSONObject event = load("interstitial", creative("videoPath", "video_3s.mp4", "imagePath", "image.png"));
        assertEquals("loaded", event.getString("event"));
        assertEquals("video", event.getString("media"));
        long duration = event.getLong("durationMs");
        assertTrue("duration " + duration, duration > 2800 && duration < 3300);
        assertTrue(SoilAdsBridge.isReady("interstitial"));
    }

    @Test
    public void videosLoadedTogetherBothReportVideo() throws Exception {
        // At start-up every fullscreen format is prepared at once. A device with few video
        // decoders could not read two videos' frames together, and one ad lost its video.
        for (int round = 0; round < 3; round++) {
            events.clear();
            SoilAdsBridge.load("interstitial", creative("videoPath", "video_3s.mp4", "imagePath", "image.png"));
            SoilAdsBridge.load("rewarded", creative("videoPath", "video_10s.mp4", "imagePath", "image.png"));
            JSONObject interstitial = events.awaitAny("interstitial", "loaded", "loadFailed");
            JSONObject rewarded = events.awaitAny("rewarded", "loaded", "loadFailed");
            assertEquals("round " + round, "video", interstitial.optString("media"));
            assertEquals("round " + round, "video", rewarded.optString("media"));
        }
    }

    @Test
    public void loadImageReportsImage() throws Exception {
        JSONObject event = load("rewarded", creative("imagePath", "image.png", "logoPath", "broken.png"));
        assertEquals("image", event.getString("media"));
        assertEquals(0, event.getLong("durationMs"));
    }

    @Test
    public void brokenVideoFallsBackToImage() throws Exception {
        assertEquals("image", load("interstitial", creative("videoPath", "broken.mp4", "imagePath", "image.png"))
                .getString("media"));
        assertEquals("image", load("interstitial", creative("videoPath", "audio_only.mp4", "imagePath", "image.png"))
                .getString("media"));
    }

    @Test
    public void brokenMediaFailsWithMediaUnreadable() throws Exception {
        for (String json : new String[]{
                creative("videoPath", "broken.mp4"),
                creative("videoPath", "audio_only.mp4"),
                creative("imagePath", "broken.png"),
                creative("videoPath", "missing.mp4", "imagePath", "missing.png"),
        }) {
            JSONObject event = load("interstitial", json);
            assertEquals(json, "loadFailed", event.getString("event"));
            assertEquals(json, "media_unreadable", event.getString("error"));
            assertFalse(SoilAdsBridge.isReady("interstitial"));
        }
    }

    @Test
    public void noMediaFailsWithInvalidCreative() throws Exception {
        JSONObject event = load("rewarded", creative("title", "Only text"));
        assertEquals("invalid_creative", event.getString("error"));
        event = load("rewarded", "{not json");
        assertEquals("invalid_creative", event.getString("error"));
    }

    @Test
    public void bannerTextAndBannerIgnoresVideo() throws Exception {
        assertEquals("text", load("banner", creative("title", "Word Master", "callToAction", "Install"))
                .getString("media"));
        assertEquals("text", load("banner", creative("imagePath", "broken.png", "title", "Word Master"))
                .getString("media"));
        assertEquals("invalid_creative", load("banner", creative("videoPath", "video_3s.mp4"))
                .getString("error"));
    }

    @Test
    public void unknownFormatFails() throws Exception {
        events.clear();
        SoilAdsBridge.load("native", creative("imagePath", "image.png"));
        assertEquals("invalid_format", events.await("native", "loadFailed").getString("error"));
        SoilAdsBridge.show("native", "{}");
        assertEquals("invalid_format", events.await("native", "showFailed").getString("error"));
        assertFalse(SoilAdsBridge.isReady("native"));
        assertFalse(SoilAdsBridge.isReady(null));
    }

    @Test
    public void largeImageIsDownsampled() {
        Bitmap bitmap = MediaLoader.decodeImage(path("large.jpg"), 1080, 1080L * 1920 * 2);
        assertNotNull(bitmap);
        assertTrue(bitmap.getWidth() + "x" + bitmap.getHeight(), bitmap.getWidth() <= 1500);
        bitmap.recycle();
    }

    // ---- fullscreen

    @Test
    public void fullscreenImageLocksThenCloses() throws Exception {
        load("interstitial", creative("imagePath", "image.png", "logoPath", "logo.png", "title", "Word Master",
                "description", "بازی کنید", "callToAction", "Install"));
        // 5 s leaves room for a slow emulator to bring the Activity up while still locked.
        SoilAdActivity ad = showFullscreen("interstitial", lockOptions(5, 0.8));
        assertFalse(SoilAdsBridge.isReady("interstitial"));
        assertNotNull(find(ad, "soil_ad_media"));
        assertNotNull(find(ad, "soil_ad_cta"));
        assertNull(find(ad, "soil_ad_mute"));

        View close = find(ad, "soil_ad_close");
        assertTrue(text(close), text(close).matches("[1-5]"));
        click(close); // locked: ignored
        SystemClock.sleep(200);
        assertFalse(ad.isFinishing());
        awaitText(close, "✕");
        click(close);
        events.await("interstitial", "closed");
        awaitGone(ad);
        SystemClock.sleep(300);
        assertEquals(Arrays.asList("interstitial:shown", "interstitial:closed"), events.names());
    }

    @Test
    public void rewardedVideoRewardsOnceBeforeClosed() throws Exception {
        load("rewarded", creative("videoPath", "video_3s.mp4"));
        SoilAdActivity ad = showFullscreen("rewarded", lockOptions(20, 1.0));
        assertNotNull(find(ad, "soil_ad_mute"));
        View close = find(ad, "soil_ad_close");
        assertFalse("\u2715".equals(text(close)));
        events.await("rewarded", "rewarded"); // at the end of the 3 s video
        awaitText(close, "✕");
        click(close);
        events.await("rewarded", "closed");
        awaitGone(ad);
        assertEquals(Arrays.asList("rewarded:shown", "rewarded:rewarded", "rewarded:closed"), events.names());
    }

    @Test
    public void muteToggles() throws Exception {
        load("interstitial", creative("videoPath", "video_3s.mp4"));
        SoilAdActivity ad = showFullscreen("interstitial",
                "{\"imageLockSeconds\":5,\"videoLockFraction\":0.8,\"minVideoLockSeconds\":5,"
                        + "\"startMuted\":true}");
        View mute = find(ad, "soil_ad_mute");
        String muted = text(mute);
        click(mute);
        assertFalse(muted.equals(text(mute)));
        click(mute);
        assertEquals(muted, text(mute));
    }

    @Test
    public void hideWhileShowingClosesOnceWithoutReward() throws Exception {
        load("rewarded", creative("imagePath", "image.png"));
        SoilAdActivity ad = showFullscreen("rewarded", lockOptions(20, 1.0));
        SoilAdsBridge.hide("rewarded");
        SoilAdsBridge.hide("rewarded");
        events.await("rewarded", "closed");
        awaitGone(ad);
        SystemClock.sleep(500);
        assertEquals(Arrays.asList("rewarded:shown", "rewarded:closed"), events.names());
    }

    @Test
    public void destroyWhileShowingClosesOnce() throws Exception {
        load("interstitial", creative("videoPath", "video_3s.mp4"));
        SoilAdActivity ad = showFullscreen("interstitial", lockOptions(20, 1.0));
        SoilAdsBridge.destroy("interstitial");
        events.await("interstitial", "closed");
        awaitGone(ad);
        SystemClock.sleep(300);
        assertEquals(Arrays.asList("interstitial:shown", "interstitial:closed"), events.names());
        assertFalse(SoilAdsBridge.isReady("interstitial"));
    }

    @Test
    public void backClosesOnlyWhenUnlocked() throws Exception {
        load("rewarded", creative("imagePath", "image.png"));
        SoilAdActivity ad = showFullscreen("rewarded", lockOptions(8, 1.0));
        instrumentation.sendKeyDownUpSync(KeyEvent.KEYCODE_BACK);
        SystemClock.sleep(300);
        assertFalse(ad.isFinishing());
        assertEquals(Collections.singletonList("rewarded:shown"), events.names());
        events.await("rewarded", "rewarded");
        instrumentation.sendKeyDownUpSync(KeyEvent.KEYCODE_BACK);
        events.await("rewarded", "closed");
        awaitGone(ad);
        assertEquals(Arrays.asList("rewarded:shown", "rewarded:rewarded", "rewarded:closed"), events.names());
    }

    /** Catches every link the player opens (API 26+), without starting anything. */
    private static final class LinkMonitor extends Instrumentation.ActivityMonitor {
        final List<Intent> opened = Collections.synchronizedList(new ArrayList<>());

        @Override
        public Instrumentation.ActivityResult onStartActivity(Intent intent) {
            if (!Intent.ACTION_VIEW.equals(intent.getAction())) return null;
            opened.add(new Intent(intent));
            return new Instrumentation.ActivityResult(Activity.RESULT_CANCELED, null);
        }
    }

    private LinkMonitor addLinkMonitor() {
        LinkMonitor monitor = new LinkMonitor();
        instrumentation.addMonitor(monitor);
        return monitor;
    }

    @Test
    public void clickOpensUrlAndReportsClicked() throws Exception {
        LinkMonitor browser = addLinkMonitor();
        try {
            load("interstitial", creative("imagePath", "image.png", "callToAction", "Install", "clickUrl", CLICK_URL));
            SoilAdActivity ad = showFullscreen("interstitial", lockOptions(20, 1.0));
            click(find(ad, "soil_ad_cta"));
            click(find(ad, "soil_ad_media"));
            assertEquals(2, browser.opened.size());
            for (Intent intent : browser.opened) {
                assertEquals(CLICK_URL, intent.getDataString());
                // Only activities that accept links from a browser may handle an ad click.
                assertTrue(intent.hasCategory(Intent.CATEGORY_BROWSABLE));
            }
            assertEquals(Arrays.asList("interstitial:shown", "interstitial:clicked", "interstitial:clicked"),
                    events.names());
        } finally {
            instrumentation.removeMonitor(browser);
        }
    }

    @Test
    public void linksThatActOnTheDeviceAreNotOpenedButEveryClickIsReported() throws Exception {
        LinkMonitor browser = addLinkMonitor();
        try {
            String[] refused = {
                    "intent:#Intent;component=com.flyingacorn.soil.ads.test/com.flyingacorn.soil.ads.CoverActivity;end",
                    "file:///sdcard/secret.txt",
                    "content://com.example.provider/secret",
                    "javascript:alert(1)",
            };
            for (String url : refused) {
                load("banner", creative("title", "Word Master", "clickUrl", url));
                SoilAdsBridge.show("banner", null);
                events.await("banner", "shown");
                instrumentation.waitForIdleSync();
                click(find(host, "soil_ad_banner"));
                events.await("banner", "clicked");
                SoilAdsBridge.hide("banner");
                events.await("banner", "closed");
            }
            assertTrue("opened " + browser.opened, browser.opened.isEmpty());

            // Store and app deep links open, as browsable links.
            String[] opened = {"market://details?id=com.example.game", "storeapp://details?id=com.example.game"};
            for (String url : opened) {
                load("banner", creative("title", "Word Master", "clickUrl", " " + url + " "));
                SoilAdsBridge.show("banner", null);
                events.await("banner", "shown");
                instrumentation.waitForIdleSync();
                click(find(host, "soil_ad_banner"));
                events.await("banner", "clicked");
                SoilAdsBridge.hide("banner");
                events.await("banner", "closed");
            }
            assertEquals(opened.length, browser.opened.size());
            for (int i = 0; i < opened.length; i++) {
                assertEquals(opened[i], browser.opened.get(i).getDataString());
                assertTrue(browser.opened.get(i).hasCategory(Intent.CATEGORY_BROWSABLE));
            }
        } finally {
            instrumentation.removeMonitor(browser);
        }
    }

    @Test
    public void longCallToActionGetsItsOwnRow() throws Exception {
        load("interstitial", creative("imagePath", "image.png", "title", "Word Master",
                "callToAction", "دانلود و نصب رایگان همین حالا با تخفیف ویژه امروز"));
        SoilAdActivity ad = showFullscreen("interstitial", lockOptions(20, 1.0));
        View cta = find(ad, "soil_ad_cta");
        View card = find(ad, "soil_ad_info");
        instrumentation.waitForIdleSync();
        float density = ad.getResources().getDisplayMetrics().density;
        assertEquals("full width inside the card", card.getWidth() - card.getPaddingLeft() - card.getPaddingRight(),
                cta.getWidth());
        assertTrue("cta " + cta.getHeight() + "px", cta.getHeight() >= Math.floor(50 * density));
        View title = findText(ad.getWindow().getDecorView(), "Word Master");
        assertNotNull(title);
        assertTrue("title " + title.getWidth() + "px", title.getWidth() > 150 * density);
        assertTrue("the button sits under the texts", windowRect(title)[3] <= windowRect(cta)[1]);
    }

    @Test
    public void infoCardFollowsTheTextDirection() throws Exception {
        load("interstitial", creative("imagePath", "image.png", "logoPath", "logo.png", "title", "واژه‌باف",
                "description", "بازی حدس کلمه", "callToAction", "نصب"));
        SoilAdActivity persian = showFullscreen("interstitial", lockOptions(1, 1.0));
        instrumentation.waitForIdleSync();
        View title = findText(persian.getWindow().getDecorView(), "واژه‌باف");
        View logo = ((android.view.ViewGroup) title.getParent().getParent()).getChildAt(1);
        assertTrue("Persian: the logo is on the right", windowRect(logo)[0] >= windowRect(title)[2]);
        assertTrue("Persian is right aligned", ((android.widget.TextView) title).getLayout().getLineLeft(0) > 0);
    }

    @Test
    public void infoCardIsLeftToRightForEnglish() throws Exception {
        load("interstitial", creative("imagePath", "image.png", "logoPath", "logo.png", "title", "Word Master",
                "callToAction", "Install"));
        SoilAdActivity english = showFullscreen("interstitial", lockOptions(1, 1.0));
        instrumentation.waitForIdleSync();
        View englishTitle = findText(english.getWindow().getDecorView(), "Word Master");
        View englishLogo = ((android.view.ViewGroup) englishTitle.getParent().getParent()).getChildAt(0);
        assertTrue("English: the logo is on the left", windowRect(englishLogo)[2] <= windowRect(englishTitle)[0]);
    }

    @Test
    public void imageOnlyAdHasNoCardAndABlurredBackdrop() throws Exception {
        load("rewarded", creative("imagePath", "image.png"));
        SoilAdActivity ad = showFullscreen("rewarded", lockOptions(20, 1.0));
        instrumentation.waitForIdleSync();
        assertNull(find(ad, "soil_ad_info"));
        assertNull(find(ad, "soil_ad_cta"));
        View backdrop = find(ad, "soil_ad_backdrop");
        assertNotNull(backdrop);
        View root = ad.getWindow().getDecorView();
        assertEquals("the backdrop fills the window", root.getWidth(), backdrop.getWidth());
        assertEquals(root.getHeight(), backdrop.getHeight());
        assertTrue("locked for the rewarded image time",
                Integer.parseInt(text(find(ad, "soil_ad_close"))) >= 19);
    }

    @Test
    public void videoWithoutImageHasNoBackdrop() throws Exception {
        load("interstitial", creative("videoPath", "video_3s.mp4"));
        SoilAdActivity ad = showFullscreen("interstitial", lockOptions(5, 0.8));
        assertNull(find(ad, "soil_ad_backdrop"));
    }

    @Test
    public void imageBannerKeepsTheStandardSizeWithABlurredFillAndTheBadgeOnTheImage() throws Exception {
        load("banner", creative("imagePath", "image.png"));
        SoilAdsBridge.show("banner", "{\"position\":\"bottom\"}");
        events.await("banner", "shown");
        instrumentation.waitForIdleSync();
        View banner = find(host, "soil_ad_banner");
        float density = host.getResources().getDisplayMetrics().density;
        assertEquals(Math.round(50 * density), banner.getHeight());
        View badge = findText(banner, "تبلیغ");
        assertNotNull(badge);
        android.view.ViewGroup framed = (android.view.ViewGroup) badge.getParent();
        View image = framed.getChildAt(0);
        assertTrue("the image is aspect-fit: " + image.getWidth() + "x" + image.getHeight(),
                Math.abs(image.getWidth() - image.getHeight() * 320f / 480f) <= 2);
        int[] imageRect = windowRect(image);
        int[] badgeRect = windowRect(badge);
        assertTrue("the badge sits on the image", badgeRect[0] >= imageRect[0] && badgeRect[1] >= imageRect[1]
                && badgeRect[2] <= imageRect[2] && badgeRect[3] <= imageRect[3]);
        View backdrop = ((android.view.ViewGroup) banner).getChildAt(0);
        assertTrue("a blurred fill behind it", backdrop instanceof ImageView && backdrop.getWidth() == banner.getWidth());
    }

    @Test
    public void interstitialIsClosableBy15SecondsByDefault() throws Exception {
        load("interstitial", creative("imagePath", "image.png"));
        // C# leaves maxLockSeconds out only in older SDKs; the player still applies the 15 s default.
        SoilAdActivity ad = showFullscreen("interstitial", "{\"imageLockSeconds\":30}");
        int remaining = Integer.parseInt(text(find(ad, "soil_ad_close")));
        assertTrue("countdown " + remaining, remaining <= 15 && remaining >= 13);
    }

    @Test
    public void adScreenThatFailsToOpenReportsShowFailedAndKeepsTheAd() throws Exception {
        load("interstitial", creative("imagePath", "image.png"));
        SoilAdActivity.failAtForTests = "create";
        SoilAdsBridge.show("interstitial", lockOptions(5, 0.8));
        JSONObject failed = events.awaitAny("interstitial", "showFailed", "shown");
        assertEquals("showFailed", failed.getString("event"));
        SystemClock.sleep(500);
        assertEquals(Arrays.asList("interstitial:showFailed"), events.names());
        assertTrue("the ad goes back to its slot", SoilAdsBridge.isReady("interstitial"));
    }

    @Test
    public void adScreenThatFailsOnScreenClosesOnceAndTheGameCarriesOn() throws Exception {
        load("rewarded", creative("imagePath", "image.png"));
        SoilAdActivity ad = showFullscreen("rewarded", lockOptions(20, 1.0));
        SoilAdActivity.failAtForTests = "tick";
        events.await("rewarded", "closed");
        awaitGone(ad);
        SystemClock.sleep(300);
        assertEquals(Arrays.asList("rewarded:shown", "rewarded:closed"), events.names());
        assertTrue("the game's activity is back", !host.isFinishing());
    }

    @Test
    public void badgeSaysAdInPersian() throws Exception {
        load("interstitial", creative("imagePath", "image.png"));
        SoilAdActivity ad = showFullscreen("interstitial", lockOptions(5, 0.8));
        assertNotNull(findText(ad.getWindow().getDecorView(), "تبلیغ"));
    }

    private static View findText(View view, String text) {
        if (view instanceof android.widget.TextView && text.contentEquals(((android.widget.TextView) view).getText()))
            return view;
        if (view instanceof android.view.ViewGroup) {
            android.view.ViewGroup group = (android.view.ViewGroup) view;
            for (int i = 0; i < group.getChildCount(); i++) {
                View found = findText(group.getChildAt(i), text);
                if (found != null) return found;
            }
        }
        return null;
    }

    @Test
    public void clickWithoutUrlStillReportsClicked() throws Exception {
        load("interstitial", creative("imagePath", "image.png", "callToAction", "Install"));
        SoilAdActivity ad = showFullscreen("interstitial", lockOptions(20, 1.0));
        click(find(ad, "soil_ad_cta"));
        assertFalse("nothing to open, the ad stays", ad.isFinishing());
        assertEquals(Arrays.asList("interstitial:shown", "interstitial:clicked"), events.names());
    }

    @Test
    public void onlyOneFullscreenAndNotLoaded() throws Exception {
        events.clear();
        SoilAdsBridge.show("rewarded", null);
        assertEquals("not_loaded", events.await("rewarded", "showFailed").getString("error"));
        load("interstitial", creative("imagePath", "image.png"));
        load("rewarded", creative("imagePath", "image.png"));
        showFullscreen("interstitial", lockOptions(20, 1.0));
        SoilAdsBridge.show("rewarded", lockOptions(20, 1.0));
        assertEquals("already_showing", events.await("rewarded", "showFailed").getString("error"));
        assertTrue(SoilAdsBridge.isReady("rewarded"));
    }

    @Test
    public void loadDuringShowRefillsSlot() throws Exception {
        load("interstitial", creative("imagePath", "image.png"));
        showFullscreen("interstitial", lockOptions(20, 1.0));
        SoilAdsBridge.load("interstitial", creative("videoPath", "video_3s.mp4"));
        events.await("interstitial", "loaded");
        assertTrue(SoilAdsBridge.isReady("interstitial"));
    }

    @Test
    public void rapidShowHideEndsWithOneTerminalEvent() throws Exception {
        for (int i = 0; i < 3; i++) {
            load("interstitial", creative("imagePath", "image.png"));
            SoilAdsBridge.show("interstitial", lockOptions(20, 1.0));
            SoilAdsBridge.hide("interstitial");
            events.awaitAny("interstitial", "closed", "showFailed");
            SystemClock.sleep(1500);
            List<String> names = events.names();
            int terminal = Collections.frequency(names, "interstitial:closed")
                    + Collections.frequency(names, "interstitial:showFailed");
            assertEquals(names.toString(), 1, terminal);
        }
    }

    @Test
    public void backgroundTimeDoesNotCount() throws Exception {
        load("interstitial", creative("imagePath", "image.png"));
        SoilAdActivity ad = showFullscreen("interstitial", lockOptions(30, 1.0));
        Instrumentation.ActivityMonitor coverMonitor =
                instrumentation.addMonitor(CoverActivity.class.getName(), null, false);
        instrumentation.runOnMainSync(() -> ad.startActivity(new Intent(ad, CoverActivity.class)));
        Activity cover = coverMonitor.waitForActivityWithTimeout(TIMEOUT_MS);
        instrumentation.removeMonitor(coverMonitor);
        assertNotNull(cover);
        awaitStage(ad, Stage.STOPPED);

        long visibleBefore = visibleMs(ad);
        SystemClock.sleep(10000); // the ad is not on screen: its clock must not move
        assertEquals(visibleBefore, visibleMs(ad));

        instrumentation.runOnMainSync(cover::finish);
        awaitStage(ad, Stage.RESUMED);
        // Had the 10 s in the background counted, at most 30 - before - 10 seconds would be left.
        int remaining = Integer.parseInt(text(find(ad, "soil_ad_close")));
        int withoutBackground = (int) Math.ceil(30 - visibleBefore / 1000.0);
        assertTrue(remaining + " vs " + withoutBackground, remaining > withoutBackground - 6);
    }

    private void awaitStage(Activity activity, Stage stage) {
        long deadline = SystemClock.uptimeMillis() + TIMEOUT_MS;
        final Stage[] current = new Stage[1];
        while (true) {
            instrumentation.runOnMainSync(() ->
                    current[0] = ActivityLifecycleMonitorRegistry.getInstance().getLifecycleStageOf(activity));
            if (current[0] == stage) return;
            if (SystemClock.uptimeMillis() > deadline) fail("Stage stayed " + current[0] + ", expected " + stage);
            SystemClock.sleep(50);
        }
    }

    private Activity awaitResumedAdOtherThan(Activity previous) {
        long deadline = SystemClock.uptimeMillis() + TIMEOUT_MS;
        final Activity[] found = new Activity[1];
        while (found[0] == null) {
            if (SystemClock.uptimeMillis() > deadline) fail("No new SoilAdActivity was resumed");
            SystemClock.sleep(50);
            instrumentation.runOnMainSync(() -> {
                for (Activity a : ActivityLifecycleMonitorRegistry.getInstance().getActivitiesInStage(Stage.RESUMED)) {
                    if (a instanceof SoilAdActivity && a != previous) found[0] = a;
                }
            });
        }
        return found[0];
    }

    /** Accumulated on-screen time of the show, read from the Activity's session. */
    private long visibleMs(SoilAdActivity activity) throws Exception {
        java.lang.reflect.Field field = SoilAdActivity.class.getDeclaredField("session");
        field.setAccessible(true);
        final long[] value = new long[1];
        instrumentation.runOnMainSync(() -> {
            try {
                value[0] = ((FullscreenSession) field.get(activity)).visibleMs;
            } catch (IllegalAccessException e) {
                throw new AssertionError(e);
            }
        });
        return value[0];
    }

    @Test
    public void recreatedActivityKeepsTheShow() throws Exception {
        load("rewarded", creative("imagePath", "image.png"));
        SoilAdActivity first = showFullscreen("rewarded", lockOptions(2, 1.0));
        instrumentation.runOnMainSync(first::recreate);
        Activity second = awaitResumedAdOtherThan(first);
        events.await("rewarded", "rewarded");
        click(find(second, "soil_ad_close"));
        events.await("rewarded", "closed");
        assertEquals(Arrays.asList("rewarded:shown", "rewarded:rewarded", "rewarded:closed"), events.names());
    }

    @Test
    public void activityWithoutSessionFinishesQuietly() {
        Instrumentation.ActivityMonitor monitor =
                instrumentation.addMonitor(SoilAdActivity.class.getName(), null, false);
        instrumentation.runOnMainSync(() -> host.startActivity(
                new Intent(host, SoilAdActivity.class).putExtra(SoilAdActivity.EXTRA_SESSION_ID, 987654)));
        Activity orphan = monitor.waitForActivityWithTimeout(TIMEOUT_MS);
        instrumentation.removeMonitor(monitor);
        assertNotNull(orphan);
        assertTrue(orphan.isFinishing() || orphan.isDestroyed());
        assertTrue(events.names().isEmpty());
    }

    /** Reads a private field on the main thread. */
    private Object field(Object target, String name) throws Exception {
        java.lang.reflect.Field field = target.getClass().getDeclaredField(name);
        field.setAccessible(true);
        final Object[] value = new Object[1];
        instrumentation.runOnMainSync(() -> {
            try {
                value[0] = field.get(target);
            } catch (IllegalAccessException e) {
                throw new AssertionError(e);
            }
        });
        return value[0];
    }

    private FullscreenSession session(Activity ad) throws Exception {
        return (FullscreenSession) field(ad, "session");
    }

    private int savedPositionMs(Activity ad) throws Exception {
        final FullscreenSession session = session(ad);
        final int[] value = new int[1];
        instrumentation.runOnMainSync(() -> value[0] = session.videoPositionMs);
        return value[0];
    }

    private AndroidPresenter presenter() throws Exception {
        java.lang.reflect.Field managerField = SoilAdsBridge.class.getDeclaredField("manager");
        managerField.setAccessible(true);
        return (AndroidPresenter) field(managerField.get(null), "presenter");
    }

    private static void awaitTrue(String what, java.util.concurrent.Callable<Boolean> condition) throws Exception {
        long deadline = SystemClock.uptimeMillis() + TIMEOUT_MS;
        while (!condition.call()) {
            if (SystemClock.uptimeMillis() > deadline) fail(what);
            SystemClock.sleep(50);
        }
    }

    @Test
    public void resumeContinuesWhereTheVideoWasLeft() throws Exception {
        // One key frame at 0 s: a resume that seeks to the previous key frame would start over.
        load("interstitial", creative("videoPath", "video_10s.mp4"));
        SoilAdActivity first = showFullscreen("interstitial", lockOptions(30, 1.0));
        awaitTrue("the video did not play 2 s", () -> savedPositionMs(first) >= 2000);
        instrumentation.runOnMainSync(first::recreate); // a new player on a new surface
        SoilAdActivity second = (SoilAdActivity) awaitResumedAdOtherThan(first);
        int saved = savedPositionMs(second);
        assertTrue("saved " + saved, saved >= 2000);

        List<Integer> positions = new ArrayList<>();
        long until = SystemClock.uptimeMillis() + 3000;
        while (SystemClock.uptimeMillis() < until) {
            final int[] position = {-1};
            final MediaPlayer player = (MediaPlayer) field(second, "player");
            final boolean prepared = (Boolean) field(second, "prepared");
            if (player != null && prepared) {
                instrumentation.runOnMainSync(() -> position[0] = player.getCurrentPosition());
                positions.add(position[0]);
            }
            assertTrue("saved position moved back", savedPositionMs(second) >= saved);
            SystemClock.sleep(50);
        }
        assertFalse("the video never resumed", positions.isEmpty());
        for (int position : positions) {
            assertTrue("resumed at " + position + " ms after leaving at " + saved + " ms: " + positions,
                    position >= saved - 300);
        }
    }

    @Test
    public void endedVideoIsNotReopenedAndKeepsItsLastFrame() throws Exception {
        load("rewarded", creative("videoPath", "video_3s.mp4"));
        SoilAdActivity ad = showFullscreen("rewarded", lockOptions(20, 1.0));
        events.await("rewarded", "rewarded"); // at the end of the video
        awaitTrue("the player was not released after the video ended", () -> field(ad, "player") == null);
        Bitmap frame = session(ad).lastFrame;
        assertNotNull("no last frame kept", frame);
        // The copy is the video frame itself, at the video's aspect ratio, not the letterboxed view.
        assertEquals(320f / 240f, (float) frame.getWidth() / frame.getHeight(), 0.05f);
        assertTrue(colourfulRow(frame, 1) > 0.5f);
        assertTrue(colourfulRow(frame, frame.getHeight() - 2) > 0.5f);
        assertStillShows(ad, frame);

        // Leaving and coming back destroys and recreates the TextureView's surface: nothing reopens.
        Instrumentation.ActivityMonitor coverMonitor =
                instrumentation.addMonitor(CoverActivity.class.getName(), null, false);
        instrumentation.runOnMainSync(() -> ad.startActivity(new Intent(ad, CoverActivity.class)));
        Activity cover = coverMonitor.waitForActivityWithTimeout(TIMEOUT_MS);
        instrumentation.removeMonitor(coverMonitor);
        assertNotNull(cover);
        awaitStage(ad, Stage.STOPPED);
        instrumentation.runOnMainSync(cover::finish);
        awaitStage(ad, Stage.RESUMED);
        SystemClock.sleep(1000);
        assertNull(field(ad, "player"));
        assertStillShows(ad, frame);

        instrumentation.runOnMainSync(ad::recreate);
        SoilAdActivity second = (SoilAdActivity) awaitResumedAdOtherThan(ad);
        SystemClock.sleep(1000);
        assertNull(field(second, "player"));
        assertNull("no video view for an ended video", field(second, "textureView"));
        assertStillShows(second, frame);

        click(find(second, "soil_ad_close"));
        events.await("rewarded", "closed");
        awaitGone(second);
        assertTrue("the kept frame is released with the show", frame.isRecycled());
        assertEquals(Arrays.asList("rewarded:shown", "rewarded:rewarded", "rewarded:closed"), events.names());
    }

    private void assertStillShows(Activity ad, Bitmap expected) throws Exception {
        ImageView image = (ImageView) field(ad, "imageView");
        final Drawable[] drawable = new Drawable[1];
        final int[] visibility = new int[1];
        instrumentation.runOnMainSync(() -> {
            drawable[0] = image.getDrawable();
            visibility[0] = image.getVisibility();
        });
        assertEquals(View.VISIBLE, visibility[0]);
        assertTrue(drawable[0] instanceof BitmapDrawable);
        assertTrue(((BitmapDrawable) drawable[0]).getBitmap() == expected);
    }

    /** Share of a row's pixels that are neither black nor transparent. */
    private static float colourfulRow(Bitmap bitmap, int y) {
        int colourful = 0;
        for (int x = 0; x < bitmap.getWidth(); x++) {
            int pixel = bitmap.getPixel(x, y);
            if (Color.alpha(pixel) > 200 && Color.red(pixel) + Color.green(pixel) + Color.blue(pixel) > 60) colourful++;
        }
        return (float) colourful / bitmap.getWidth();
    }

    @Test
    public void fullscreenControlsStayClearOfVisibleSystemBars() throws Exception {
        org.junit.Assume.assumeTrue("WindowInsets.Builder.setInsets needs API 30", Build.VERSION.SDK_INT >= 30);
        load("interstitial", creative("imagePath", "image.png", "title", "Word Master", "callToAction", "Install"));
        SoilAdActivity ad = showFullscreen("interstitial", lockOptions(20, 1.0));
        View close = find(ad, "soil_ad_close");
        View content = (View) close.getParent();
        View root = (View) content.getParent();
        int density = Math.round(ad.getResources().getDisplayMetrics().density);
        // What split screen, a freeform window or a revealed bar hands the ad: bars that show.
        final WindowInsets visibleBars = new WindowInsets.Builder()
                .setInsets(WindowInsets.Type.statusBars(), android.graphics.Insets.of(0, 30 * density, 0, 0))
                .setInsets(WindowInsets.Type.navigationBars(), android.graphics.Insets.of(0, 0, 20 * density, 48 * density))
                .build();
        settleWindowInsets();
        instrumentation.runOnMainSync(() -> root.dispatchApplyWindowInsets(visibleBars));
        instrumentation.waitForIdleSync();

        int[] cta = windowRect(find(ad, "soil_ad_cta"));
        int[] closeRect = windowRect(close);
        assertEquals(48 * density, content.getPaddingBottom());
        assertTrue("cta bottom " + cta[3], cta[3] <= root.getHeight() - 48 * density);
        assertTrue("close top " + closeRect[1], closeRect[1] >= 30 * density);
        assertTrue("close right " + closeRect[2], closeRect[2] <= root.getWidth() - 20 * density);

        // Hidden (immersive) bars take no room.
        final WindowInsets hiddenBars = new WindowInsets.Builder()
                .setInsetsIgnoringVisibility(WindowInsets.Type.systemBars(), android.graphics.Insets.of(0, 30, 0, 48))
                .setVisible(WindowInsets.Type.systemBars(), false)
                .build();
        settleWindowInsets();
        instrumentation.runOnMainSync(() -> root.dispatchApplyWindowInsets(hiddenBars));
        instrumentation.waitForIdleSync();
        assertEquals(0, content.getPaddingBottom());
        assertEquals(0, content.getPaddingTop());
    }

    /** Lets the window's own inset updates (immersive mode settling) land before a test sends its own. */
    private void settleWindowInsets() {
        instrumentation.waitForIdleSync();
        android.os.SystemClock.sleep(1000);
        instrumentation.waitForIdleSync();
    }

    private int[] windowRect(View view) {
        final int[] rect = new int[4];
        instrumentation.runOnMainSync(() -> {
            int[] location = new int[2];
            view.getLocationInWindow(location);
            rect[0] = location[0];
            rect[1] = location[1];
            rect[2] = location[0] + view.getWidth();
            rect[3] = location[1] + view.getHeight();
        });
        return rect;
    }

    @Test
    public void relaunchingTheGameClosesTheAd() throws Exception {
        // A launcher tap on a singleTask game clears the task down to the game's Activity.
        load("rewarded", creative("imagePath", "image.png"));
        SoilAdActivity ad = showFullscreen("rewarded", lockOptions(20, 1.0));
        instrumentation.runOnMainSync(() -> host.startActivity(new Intent(host, HostActivity.class)
                .addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP)));
        events.await("rewarded", "closed");
        awaitGone(ad);
        SystemClock.sleep(300);
        assertEquals(Arrays.asList("rewarded:shown", "rewarded:closed"), events.names());
        assertNull(field(presenter(), "fullscreen"));
        assertFalse(SoilAdsBridge.isReady("rewarded"));
    }

    @Test
    public void presenterForgetsAdsThatCloseOnTheirOwn() throws Exception {
        load("interstitial", creative("imagePath", "image.png"));
        SoilAdActivity ad = showFullscreen("interstitial", lockOptions(1, 1.0));
        assertNotNull(field(presenter(), "fullscreen"));
        View close = find(ad, "soil_ad_close");
        awaitText(close, "✕");
        click(close);
        events.await("interstitial", "closed");
        assertNull(field(presenter(), "fullscreen"));

        load("banner", creative("title", "Word Master"));
        SoilAdsBridge.show("banner", null);
        events.await("banner", "shown");
        instrumentation.waitForIdleSync();
        assertNotNull(field(presenter(), "banner"));
        View banner = find(host, "soil_ad_banner");
        // The game's view hierarchy drops the banner (e.g. Unity's Activity is torn down).
        instrumentation.runOnMainSync(() -> ((ViewGroup) banner.getParent()).removeView(banner));
        events.await("banner", "closed");
        assertNull(field(presenter(), "banner"));
        // The slot is untouched: the banner can be shown again.
        SoilAdsBridge.show("banner", null);
        instrumentation.waitForIdleSync();
        assertEquals(2, java.util.Collections.frequency(events.names(), "banner:shown"));
        assertNotNull(find(host, "soil_ad_banner"));
    }

    @Test
    public void imagesAreDecodedInFullColour() {
        // Ad creatives keep their gradients: no 16-bit decoding, even for a JPEG.
        Bitmap jpeg = MediaLoader.decodeImage(path("large.jpg"), 1080, 1080L * 1920 * 2);
        assertNotNull(jpeg);
        assertEquals(Bitmap.Config.ARGB_8888, jpeg.getConfig());
        jpeg.recycle();
        Bitmap png = MediaLoader.decodeImage(path("image.png"), 1080, 1080L * 1920 * 2);
        assertNotNull(png);
        assertEquals(Bitmap.Config.ARGB_8888, png.getConfig());
        png.recycle();
    }

    // ---- banner

    @Test
    public void bannerShowMoveHideDestroy() throws Exception {
        load("banner", creative("imagePath", "image.png", "clickUrl", CLICK_URL));
        events.clear();
        SoilAdsBridge.show("banner", "{\"position\":\"bottom\"}");
        events.await("banner", "shown");
        instrumentation.waitForIdleSync();
        View banner = find(host, "soil_ad_banner");
        assertNotNull(banner);
        View game = host.findViewById(android.R.id.content);
        int expectedHeight = Math.round(50 * host.getResources().getDisplayMetrics().density);
        assertEquals(expectedHeight, banner.getHeight());
        // Above a visible navigation bar (and any cutout), flush with the edge when there is none.
        int[] safe = Ui.safeInsets(host.getWindow().getDecorView());
        assertEquals(game.getHeight() - safe[3], banner.getBottom());
        assertTrue(SoilAdsBridge.isReady("banner"));

        SoilAdsBridge.show("banner", "{\"position\":\"top\"}");
        instrumentation.waitForIdleSync();
        SystemClock.sleep(200);
        instrumentation.waitForIdleSync();
        assertEquals(safe[1], banner.getTop());
        assertEquals(Collections.singletonList("banner:shown"), events.names());

        SoilAdsBridge.hide("banner");
        events.await("banner", "closed");
        instrumentation.waitForIdleSync();
        assertNull(banner.getParent());
        assertNull(find(host, "soil_ad_banner"));

        SoilAdsBridge.show("banner", "{\"position\":\"center\"}");
        instrumentation.waitForIdleSync();
        View again = find(host, "soil_ad_banner");
        assertNotNull(again);
        // Centered in the safe area: between the top and bottom insets, not the whole window.
        float safeMiddle = safe[1] + (game.getHeight() - safe[1] - safe[3]) / 2f;
        assertEquals(safeMiddle, (again.getTop() + again.getBottom()) / 2f, 2f);

        SoilAdsBridge.destroy("banner");
        instrumentation.waitForIdleSync();
        assertNull(find(host, "soil_ad_banner"));
        assertFalse(SoilAdsBridge.isReady("banner"));
        assertEquals(Arrays.asList("banner:shown", "banner:closed", "banner:shown", "banner:closed"),
                events.names());
    }

    @Test
    public void textBannerClicks() throws Exception {
        LinkMonitor browser = addLinkMonitor();
        try {
            load("banner", creative("logoPath", "logo.png", "title", "بازی",
                    "description", "Train your brain", "callToAction", "Install", "clickUrl", CLICK_URL));
            SoilAdsBridge.show("banner", null);
            events.await("banner", "shown");
            instrumentation.waitForIdleSync();
            View banner = find(host, "soil_ad_banner");
            assertNotNull(find(host, "soil_ad_cta"));
            click(banner);
            events.await("banner", "clicked");
            assertEquals(1, browser.opened.size());
            // Touches outside the banner still reach the game.
            assertTrue(((ViewGroup) banner.getParent()).getHeight() > banner.getHeight());
        } finally {
            instrumentation.removeMonitor(browser);
        }
    }

    @Test
    public void callsWithNothingLoadedNeverCrash() throws Exception {
        events.clear();
        SoilAdsBridge.hide("banner");
        SoilAdsBridge.destroy("banner");
        SoilAdsBridge.show("banner", "not json");
        assertEquals("not_loaded", events.await("banner", "showFailed").getString("error"));
    }
}
