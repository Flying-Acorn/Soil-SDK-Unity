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
import android.content.IntentFilter;
import android.graphics.Bitmap;
import android.os.SystemClock;
import android.view.KeyEvent;
import android.view.View;
import android.view.ViewGroup;
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
        for (String name : new String[]{"video_3s.mp4", "audio_only.mp4", "image.png", "logo.png", "large.jpg"}) {
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

    @Test
    public void clickOpensUrlAndReportsClicked() throws Exception {
        IntentFilter filter = new IntentFilter(Intent.ACTION_VIEW);
        filter.addDataScheme("https");
        Instrumentation.ActivityMonitor browser = instrumentation.addMonitor(filter,
                new Instrumentation.ActivityResult(Activity.RESULT_CANCELED, null), true);
        try {
            load("interstitial", creative("imagePath", "image.png", "callToAction", "Install", "clickUrl", CLICK_URL));
            SoilAdActivity ad = showFullscreen("interstitial", lockOptions(20, 1.0));
            click(find(ad, "soil_ad_cta"));
            click(find(ad, "soil_ad_media"));
            assertEquals(2, browser.getHits());
            assertEquals(Arrays.asList("interstitial:shown", "interstitial:clicked", "interstitial:clicked"),
                    events.names());
        } finally {
            instrumentation.removeMonitor(browser);
        }
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
        int[] safe = Ui.bannerInsets(host.getWindow().getDecorView());
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
        assertEquals(game.getHeight() / 2f, (again.getTop() + again.getBottom()) / 2f, 2f);

        SoilAdsBridge.destroy("banner");
        instrumentation.waitForIdleSync();
        assertNull(find(host, "soil_ad_banner"));
        assertFalse(SoilAdsBridge.isReady("banner"));
        assertEquals(Arrays.asList("banner:shown", "banner:closed", "banner:shown", "banner:closed"),
                events.names());
    }

    @Test
    public void textBannerClicks() throws Exception {
        IntentFilter filter = new IntentFilter(Intent.ACTION_VIEW);
        filter.addDataScheme("https");
        Instrumentation.ActivityMonitor browser = instrumentation.addMonitor(filter,
                new Instrumentation.ActivityResult(Activity.RESULT_CANCELED, null), true);
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
            assertEquals(1, browser.getHits());
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
