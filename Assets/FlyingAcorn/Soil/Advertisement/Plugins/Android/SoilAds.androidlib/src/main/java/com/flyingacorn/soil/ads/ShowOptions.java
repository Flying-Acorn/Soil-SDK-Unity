package com.flyingacorn.soil.ads;

import org.json.JSONException;
import org.json.JSONObject;

/** Fullscreen show options and banner position ("Show options JSON" in PROTOCOL.md). */
final class ShowOptions {
    static final String POSITION_BOTTOM = "bottom";
    static final String POSITION_TOP = "top";
    static final String POSITION_CENTER = "center";

    final double imageLockSeconds;
    final double videoLockFraction;
    final double minVideoLockSeconds;
    final double maxLockSeconds;
    final boolean startMuted;

    ShowOptions(double imageLockSeconds, double videoLockFraction, double minVideoLockSeconds,
                double maxLockSeconds, boolean startMuted) {
        this.imageLockSeconds = imageLockSeconds;
        this.videoLockFraction = videoLockFraction;
        this.minVideoLockSeconds = minVideoLockSeconds;
        this.maxLockSeconds = maxLockSeconds;
        this.startMuted = startMuted;
    }

    /** The values C# sends by default, used for any field that is missing or not a number. */
    static ShowOptions defaults(String format) {
        return AdFormats.REWARDED.equals(format)
                ? new ShowOptions(20, 1.0, 0, 60, false)
                : new ShowOptions(5, 0.8, 5, 60, false);
    }

    static ShowOptions parse(String format, String json) {
        ShowOptions d = defaults(format);
        JSONObject o = parseObject(json);
        if (o == null) return d;
        return new ShowOptions(
                number(o, "imageLockSeconds", d.imageLockSeconds),
                number(o, "videoLockFraction", d.videoLockFraction),
                number(o, "minVideoLockSeconds", d.minVideoLockSeconds),
                number(o, "maxLockSeconds", d.maxLockSeconds),
                o.optBoolean("startMuted", d.startMuted));
    }

    /** {@code bottom} (default), {@code top} or {@code center}. */
    static String bannerPosition(String json) {
        JSONObject o = parseObject(json);
        String position = o == null || o.isNull("position") ? null : o.optString("position", null);
        if (POSITION_TOP.equals(position) || POSITION_CENTER.equals(position)) return position;
        return POSITION_BOTTOM;
    }

    private static double number(JSONObject o, String key, double fallback) {
        double value = o.optDouble(key, Double.NaN);
        return Double.isNaN(value) || Double.isInfinite(value) ? fallback : value;
    }

    static JSONObject parseObject(String json) {
        if (json == null || json.trim().isEmpty()) return null;
        try {
            return new JSONObject(json);
        } catch (JSONException e) {
            return null;
        }
    }
}
