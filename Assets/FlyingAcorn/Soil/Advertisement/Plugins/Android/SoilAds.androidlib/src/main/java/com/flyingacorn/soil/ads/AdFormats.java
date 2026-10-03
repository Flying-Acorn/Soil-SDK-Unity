package com.flyingacorn.soil.ads;

/** Format names, media kinds and error codes shared with C# (see PROTOCOL.md). */
final class AdFormats {
    static final String BANNER = "banner";
    static final String INTERSTITIAL = "interstitial";
    static final String REWARDED = "rewarded";

    static final String MEDIA_VIDEO = "video";
    static final String MEDIA_IMAGE = "image";
    static final String MEDIA_TEXT = "text";

    static final String ERROR_INVALID_FORMAT = "invalid_format";
    static final String ERROR_INVALID_CREATIVE = "invalid_creative";
    static final String ERROR_MEDIA_UNREADABLE = "media_unreadable";
    static final String ERROR_NOT_LOADED = "not_loaded";
    static final String ERROR_ALREADY_SHOWING = "already_showing";
    static final String ERROR_NO_HOST = "no_host";
    static final String ERROR_INTERNAL = "internal";

    private AdFormats() {
    }

    static boolean isValid(String format) {
        return BANNER.equals(format) || isFullscreen(format);
    }

    static boolean isFullscreen(String format) {
        return INTERSTITIAL.equals(format) || REWARDED.equals(format);
    }
}
