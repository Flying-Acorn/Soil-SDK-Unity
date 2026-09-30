package com.flyingacorn.soil.ads;

import org.json.JSONException;
import org.json.JSONObject;

/** Builds the event JSON sent to Unity ("Events" in PROTOCOL.md). */
final class AdEvents {
    static final String LOADED = "loaded";
    static final String LOAD_FAILED = "loadFailed";
    static final String SHOWN = "shown";
    static final String SHOW_FAILED = "showFailed";
    static final String CLICKED = "clicked";
    static final String REWARDED = "rewarded";
    static final String CLOSED = "closed";

    private AdEvents() {
    }

    static String simple(String format, String event) {
        return base(format, event).toString();
    }

    static String loaded(String format, String media, long durationMs) {
        JSONObject json = base(format, LOADED);
        put(json, "media", media);
        put(json, "durationMs", durationMs);
        return json.toString();
    }

    static String failure(String format, String event, String error, String message) {
        JSONObject json = base(format, event);
        put(json, "error", error);
        put(json, "message", message == null ? "" : message);
        return json.toString();
    }

    private static JSONObject base(String format, String event) {
        JSONObject json = new JSONObject();
        put(json, "format", format == null ? "" : format);
        put(json, "event", event);
        return json;
    }

    private static void put(JSONObject json, String key, Object value) {
        try {
            json.put(key, value);
        } catch (JSONException ignored) {
            // Only thrown for non-finite numbers, which are never passed here.
        }
    }
}
