package com.flyingacorn.soil.ads;

import android.graphics.Bitmap;

/**
 * A decoded creative sitting in a slot or on screen. Reference counted (main thread only) because a
 * banner keeps showing its creative after the slot has been reloaded; bitmaps are recycled when the
 * last holder lets go.
 */
final class LoadedAd {
    final AdCreative creative;
    final String media;
    final long durationMs;
    final Bitmap image;
    final Bitmap logo;

    private int references = 1;

    LoadedAd(AdCreative creative, String media, long durationMs, Bitmap image, Bitmap logo) {
        this.creative = creative;
        this.media = media;
        this.durationMs = durationMs;
        this.image = image;
        this.logo = logo;
    }

    boolean isVideo() {
        return AdFormats.MEDIA_VIDEO.equals(media);
    }

    void retain() {
        references++;
    }

    void release() {
        if (references <= 0) return;
        if (--references > 0) return;
        if (image != null) image.recycle();
        if (logo != null) logo.recycle();
    }

    boolean isReleased() {
        return references <= 0;
    }
}
