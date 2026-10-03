package com.flyingacorn.soil.ads;

import android.graphics.Bitmap;
import android.util.Log;

/**
 * The blurred, darkened copy of a fullscreen ad's image that fills the screen behind it, so a 4:5
 * cover image or a letterboxed video sits on its own colors instead of black bars. Made once at
 * load on the loader thread: the image is shrunk to a few dozen pixels, box-blurred and dimmed;
 * the view scales it back up with filtering, which smooths it further. Works on every API level
 * (RenderEffect blur needs API 31).
 */
final class Backdrop {
    /** Long side of the blurred bitmap, in pixels. */
    static final int SIDE = 40;
    static final int RADIUS = 2;
    static final int PASSES = 3;
    /** Share of the original brightness kept, so white text and buttons stand out on it. */
    static final float BRIGHTNESS = 0.5f;

    private Backdrop() {
    }

    /** Null when there is no image or it could not be made. */
    static Bitmap from(Bitmap image) {
        if (image == null || image.getWidth() <= 0 || image.getHeight() <= 0) return null;
        try {
            float scale = (float) SIDE / Math.max(image.getWidth(), image.getHeight());
            int width = Math.max(1, Math.round(image.getWidth() * scale));
            int height = Math.max(1, Math.round(image.getHeight() * scale));
            Bitmap small = Bitmap.createScaledBitmap(image, width, height, true);
            int[] pixels = new int[width * height];
            small.getPixels(pixels, 0, width, 0, 0, width, height);
            if (small != image) small.recycle();
            for (int i = 0; i < PASSES; i++) blur(pixels, width, height, RADIUS);
            dim(pixels, BRIGHTNESS);
            return Bitmap.createBitmap(pixels, width, height, Bitmap.Config.ARGB_8888);
        } catch (RuntimeException | OutOfMemoryError e) {
            Log.w(SoilAdsBridge.TAG, "Could not make the ad's backdrop: " + e);
            return null;
        }
    }

    /** One horizontal and one vertical box-blur pass over opaque ARGB pixels, edges clamped. */
    static void blur(int[] pixels, int width, int height, int radius) {
        int[] line = new int[Math.max(width, height)];
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) line[x] = pixels[y * width + x];
            for (int x = 0; x < width; x++) pixels[y * width + x] = average(line, width, x, radius);
        }
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) line[y] = pixels[y * width + x];
            for (int y = 0; y < height; y++) pixels[y * width + x] = average(line, height, y, radius);
        }
    }

    private static int average(int[] line, int length, int center, int radius) {
        int r = 0, g = 0, b = 0;
        int count = 2 * radius + 1;
        for (int k = -radius; k <= radius; k++) {
            int c = line[Math.min(length - 1, Math.max(0, center + k))];
            r += (c >> 16) & 0xFF;
            g += (c >> 8) & 0xFF;
            b += c & 0xFF;
        }
        return 0xFF000000 | ((r / count) << 16) | ((g / count) << 8) | (b / count);
    }

    static void dim(int[] pixels, float brightness) {
        for (int i = 0; i < pixels.length; i++) {
            int c = pixels[i];
            int r = Math.round(((c >> 16) & 0xFF) * brightness);
            int g = Math.round(((c >> 8) & 0xFF) * brightness);
            int b = Math.round((c & 0xFF) * brightness);
            pixels[i] = 0xFF000000 | (r << 16) | (g << 8) | b;
        }
    }
}
