package com.flyingacorn.soil.ads;

import org.json.JSONObject;

/** The creative JSON sent with {@code load}. Missing, null and empty strings all become null. */
final class AdCreative {
    final String adId;
    final String videoPath;
    final String imagePath;
    final String logoPath;
    final String title;
    final String description;
    final String callToAction;
    final String clickUrl;

    private AdCreative(JSONObject o) {
        adId = string(o, "adId");
        videoPath = string(o, "videoPath");
        imagePath = string(o, "imagePath");
        logoPath = string(o, "logoPath");
        title = string(o, "title");
        description = string(o, "description");
        callToAction = string(o, "callToAction");
        clickUrl = string(o, "clickUrl");
    }

    /** Returns null when the JSON is not an object. */
    static AdCreative parse(String json) {
        JSONObject o = ShowOptions.parseObject(json);
        return o == null ? null : new AdCreative(o);
    }

    private static String string(JSONObject o, String key) {
        if (o.isNull(key)) return null;
        String value = o.optString(key, "").trim();
        return value.isEmpty() ? null : value;
    }
}
