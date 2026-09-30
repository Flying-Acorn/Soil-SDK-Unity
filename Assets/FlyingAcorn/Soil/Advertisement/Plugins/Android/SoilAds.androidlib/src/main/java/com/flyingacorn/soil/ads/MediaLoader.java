package com.flyingacorn.soil.ads;

import android.content.res.Resources;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.media.MediaMetadataRetriever;
import android.os.Build;
import android.os.Handler;
import android.util.DisplayMetrics;
import android.util.Log;

import java.io.File;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.ThreadFactory;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Validates and decodes creatives on background threads ("Validation on load" in PROTOCOL.md)
 * and delivers the result on the main thread. Each format has its own thread, so a slow video
 * check for a fullscreen ad never holds up a banner.
 */
final class MediaLoader implements SoilAdsManager.Loader {
    private static final int LOGO_MAX_SIDE = 256;
    /** Size of the frame grabbed to prove a video decodes; small is enough (API 27+). */
    private static final int PROBE_FRAME_SIDE = 64;

    private final Handler main;
    private final Map<String, ExecutorService> executors = new HashMap<>();

    MediaLoader(Handler main) {
        this.main = main;
    }

    /** Main thread only, like {@link #load}. */
    private ExecutorService executor(final String format) {
        ExecutorService executor = executors.get(format);
        if (executor == null) {
            executor = Executors.newSingleThreadExecutor(new ThreadFactory() {
                @Override
                public Thread newThread(Runnable runnable) {
                    Thread thread = new Thread(runnable, "SoilAdsLoader-" + format);
                    thread.setDaemon(true);
                    return thread;
                }
            });
            executors.put(format, executor);
        }
        return executor;
    }

    @Override
    public SoilAdsManager.Cancellable load(final String format, final AdCreative creative,
                                           final SoilAdsManager.LoadCallback callback) {
        final AtomicBoolean cancelled = new AtomicBoolean();
        final Future<?> task = executor(format).submit(new Runnable() {
            @Override
            public void run() {
                if (cancelled.get()) return;
                final Result result = decodeSafely(format, creative);
                main.post(new Runnable() {
                    @Override
                    public void run() {
                        if (cancelled.get()) {
                            if (result.ad != null) result.ad.release();
                        } else if (result.ad != null) {
                            callback.onLoaded(result.ad);
                        } else {
                            callback.onFailed(result.error, result.message);
                        }
                    }
                });
            }
        });
        return new SoilAdsManager.Cancellable() {
            @Override
            public void cancel() {
                cancelled.set(true);
                task.cancel(false);
            }
        };
    }

    private static final class Result {
        final LoadedAd ad;
        final String error;
        final String message;

        Result(LoadedAd ad, String error, String message) {
            this.ad = ad;
            this.error = error;
            this.message = message;
        }
    }

    private static Result decodeSafely(String format, AdCreative creative) {
        try {
            return decode(format, creative);
        } catch (Throwable t) {
            Log.e(SoilAdsBridge.TAG, "Decoding the " + format + " creative failed", t);
            return new Result(null, AdFormats.ERROR_INTERNAL, String.valueOf(t));
        }
    }

    private static Result decode(String format, AdCreative creative) {
        boolean banner = AdFormats.BANNER.equals(format);
        DisplayMetrics screen = Resources.getSystem().getDisplayMetrics();
        int screenLongSide = Math.max(screen.widthPixels, screen.heightPixels);
        // Aspect-fit never shows more pixels than the screen has; 2x leaves room for power-of-two steps.
        long maxImagePixels = 2L * screen.widthPixels * screen.heightPixels;

        String videoPath = banner ? null : creative.videoPath;
        long durationMs = probeVideoDurationMs(videoPath);
        Bitmap image = decodeImage(creative.imagePath, screenLongSide, maxImagePixels);
        Bitmap logo = decodeImage(creative.logoPath, LOGO_MAX_SIDE, (long) LOGO_MAX_SIDE * LOGO_MAX_SIDE);

        String media = null;
        if (durationMs > 0) media = AdFormats.MEDIA_VIDEO;
        else if (image != null) media = AdFormats.MEDIA_IMAGE;
        else if (banner && creative.title != null) media = AdFormats.MEDIA_TEXT;

        if (media == null) {
            if (logo != null) logo.recycle();
            boolean mediaGiven = videoPath != null || creative.imagePath != null;
            return mediaGiven
                    ? new Result(null, AdFormats.ERROR_MEDIA_UNREADABLE, "The ad's media could not be decoded")
                    : new Result(null, AdFormats.ERROR_INVALID_CREATIVE, "The creative has no media");
        }
        if (videoPath != null && durationMs <= 0) {
            Log.w(SoilAdsBridge.TAG, "Video unusable, falling back to the image: " + videoPath);
        }
        return new Result(new LoadedAd(creative, media, Math.max(durationMs, 0), image, logo), null, null);
    }

    /**
     * Returns the duration of a readable file with a decodable video track, or 0 when it is not one.
     * Grabbing one frame proves the device can actually decode it.
     */
    static long probeVideoDurationMs(String path) {
        if (path == null || !new File(path).canRead()) return 0;
        MediaMetadataRetriever retriever = new MediaMetadataRetriever();
        try {
            retriever.setDataSource(path);
            if (!"yes".equals(retriever.extractMetadata(MediaMetadataRetriever.METADATA_KEY_HAS_VIDEO))) return 0;
            String duration = retriever.extractMetadata(MediaMetadataRetriever.METADATA_KEY_DURATION);
            long durationMs = duration == null ? 0 : Long.parseLong(duration.trim());
            if (durationMs <= 0) return 0;
            Bitmap frame = Build.VERSION.SDK_INT >= 27
                    ? retriever.getScaledFrameAtTime(0, MediaMetadataRetriever.OPTION_CLOSEST_SYNC,
                    PROBE_FRAME_SIDE, PROBE_FRAME_SIDE)
                    : retriever.getFrameAtTime(0, MediaMetadataRetriever.OPTION_CLOSEST_SYNC);
            if (frame == null) return 0;
            frame.recycle();
            return durationMs;
        } catch (Exception e) {
            Log.w(SoilAdsBridge.TAG, "Video is not readable: " + path + " (" + e + ")");
            return 0;
        } finally {
            try {
                retriever.release();
            } catch (Throwable ignored) {
                // Declared to throw IOException on newer SDKs; nothing to do.
            }
        }
    }

    /** Decodes an image no larger than needed for the screen; null when missing or broken. */
    static Bitmap decodeImage(String path, int maxSide, long maxPixels) {
        if (path == null || !new File(path).canRead()) return null;
        try {
            BitmapFactory.Options bounds = new BitmapFactory.Options();
            bounds.inJustDecodeBounds = true;
            BitmapFactory.decodeFile(path, bounds);
            if (bounds.outWidth <= 0 || bounds.outHeight <= 0) return null;
            BitmapFactory.Options options = new BitmapFactory.Options();
            options.inSampleSize = sampleSize(bounds.outWidth, bounds.outHeight, maxSide, maxPixels);
            return BitmapFactory.decodeFile(path, options);
        } catch (OutOfMemoryError e) {
            Log.w(SoilAdsBridge.TAG, "Image too large to decode: " + path);
            return null;
        } catch (Exception e) {
            Log.w(SoilAdsBridge.TAG, "Image is not readable: " + path + " (" + e + ")");
            return null;
        }
    }

    /**
     * Largest power-of-two step that keeps the long side at least {@code maxSide}, then more steps
     * until the pixel count fits {@code maxPixels}.
     */
    static int sampleSize(int width, int height, int maxSide, long maxPixels) {
        int sample = 1;
        int longSide = Math.max(width, height);
        while (sample < 1024 && longSide / (sample * 2) >= Math.max(maxSide, 1)) sample *= 2;
        while (sample < 1024 && ((long) width / sample) * ((long) height / sample) > Math.max(maxPixels, 1)) {
            sample *= 2;
        }
        return sample;
    }
}
