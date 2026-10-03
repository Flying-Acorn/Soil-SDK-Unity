package com.flyingacorn.soil.ads;

import static org.junit.Assume.assumeTrue;

import android.app.Activity;
import android.os.Bundle;
import android.os.SystemClock;

import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;

import org.junit.Test;
import org.junit.runner.RunWith;

import java.io.File;

/**
 * Not a test: shows one ad for design reviews and screenshots, and is skipped unless asked for.
 * Push the media to the test app's external files folder, then pass the creative with
 * {@code {dir}} standing for that folder:
 * <pre>
 * adb push image.jpg /sdcard/Android/data/com.flyingacorn.soil.ads.test/files/
 * adb shell am instrument -w -e class com.flyingacorn.soil.ads.SoilAdsPreviewTest \
 *   -e previewFormat interstitial \
 *   -e previewCreative '{"adId":"x","imagePath":"{dir}/image.jpg","title":"..."}' \
 *   com.flyingacorn.soil.ads.test/androidx.test.runner.AndroidJUnitRunner
 * </pre>
 * Optional: {@code previewOptions} (show options JSON), {@code previewSeconds} (default 20).
 */
@RunWith(AndroidJUnit4.class)
public class SoilAdsPreviewTest {

    @Test
    public void preview() {
        Bundle args = InstrumentationRegistry.getArguments();
        String format = args.getString("previewFormat");
        String creative = args.getString("previewCreative");
        assumeTrue("no preview asked for", format != null && creative != null);
        File dir = InstrumentationRegistry.getInstrumentation().getContext().getExternalFilesDir(null);
        creative = creative.replace("{dir}", dir.getAbsolutePath());
        String options = args.getString("previewOptions", "");
        long seconds = Long.parseLong(args.getString("previewSeconds", "20"));

        try (ActivityScenario<HostActivity> scenario = ActivityScenario.launch(HostActivity.class)) {
            final Activity[] host = new Activity[1];
            scenario.onActivity(activity -> host[0] = activity);
            SoilAdsBridge.initialize(host[0], "SoilAdsPreview", "OnEvent");
            SoilAdsBridge.load(format, creative);
            long deadline = SystemClock.uptimeMillis() + 30000; // a cold emulator checks videos slowly
            while (!SoilAdsBridge.isReady(format) && SystemClock.uptimeMillis() < deadline) SystemClock.sleep(100);
            SoilAdsBridge.show(format, options);
            SystemClock.sleep(seconds * 1000);
            SoilAdsBridge.destroy(format);
            SystemClock.sleep(500);
        }
    }
}
