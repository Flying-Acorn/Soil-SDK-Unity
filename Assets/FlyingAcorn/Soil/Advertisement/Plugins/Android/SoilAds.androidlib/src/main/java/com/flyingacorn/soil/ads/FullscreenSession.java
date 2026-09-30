package com.flyingacorn.soil.ads;

import android.app.Activity;
import android.content.Intent;
import android.os.Handler;
import android.util.Log;

import java.util.HashMap;
import java.util.Map;

/**
 * One fullscreen show, handed to {@link SoilAdActivity} through a static registry keyed by an id in
 * the Intent (Intents cannot carry bitmaps or callbacks). Holds everything that must survive the
 * Activity being recreated. Main thread only.
 */
final class FullscreenSession {
    /** If the Activity never starts (e.g. blocked while the app is in the background), give up. */
    private static final long ATTACH_TIMEOUT_MS = 10000;

    private static final Map<Integer, FullscreenSession> registry = new HashMap<>();
    private static int nextId = 1;

    final int id;
    final String format;
    final LoadedAd ad;
    final ShowOptions options;
    final LockPolicy lock;
    private final SoilAdsManager.PresentationListener listener;
    private final Handler handler;

    // Playback state kept across Activity instances.
    long visibleMs;
    int videoPositionMs;
    boolean videoEnded;
    boolean videoFailed;
    boolean muted;

    private SoilAdActivity activity;
    private boolean attachedOnce;
    private boolean ended;
    private boolean adReleased;

    private final Runnable attachTimeout = new Runnable() {
        @Override
        public void run() {
            if (attachedOnce) return;
            Log.w(SoilAdsBridge.TAG, "The ad activity did not start; giving up on this show");
            end();
        }
    };

    private FullscreenSession(String format, LoadedAd ad, ShowOptions options,
                              SoilAdsManager.PresentationListener listener, Handler handler) {
        this.id = nextId++;
        this.format = format;
        this.ad = ad;
        this.options = options;
        this.listener = listener;
        this.handler = handler;
        this.lock = new LockPolicy(options, ad.isVideo(), ad.durationMs / 1000.0);
        this.muted = options.startMuted;
    }

    /** Starts the ad Activity; returns the session, or null (and owns nothing) when it could not. */
    static FullscreenSession start(Activity host, Handler handler, String format, LoadedAd ad, ShowOptions options,
                                   SoilAdsManager.PresentationListener listener) {
        FullscreenSession session = new FullscreenSession(format, ad, options, listener, handler);
        registry.put(session.id, session);
        try {
            Intent intent = new Intent(host, SoilAdActivity.class);
            intent.putExtra(SoilAdActivity.EXTRA_SESSION_ID, session.id);
            host.startActivity(intent);
        } catch (RuntimeException e) {
            Log.e(SoilAdsBridge.TAG, "Could not start the ad activity", e);
            registry.remove(session.id);
            return null;
        }
        handler.postDelayed(session.attachTimeout, ATTACH_TIMEOUT_MS);
        return session;
    }

    static FullscreenSession find(int id) {
        return registry.get(id);
    }

    void attach(SoilAdActivity activity) {
        this.activity = activity;
        attachedOnce = true;
        handler.removeCallbacks(attachTimeout);
    }

    /** Called from the Activity's onDestroy, after its views let go of the bitmaps. */
    void detach(SoilAdActivity activity) {
        if (this.activity == activity) this.activity = null;
        if (ended && this.activity == null) releaseAd();
    }

    void reportShown() {
        if (!ended) listener.onShown();
    }

    void reportClicked() {
        if (!ended) listener.onClicked();
    }

    void reportUnlocked() {
        if (!ended) listener.onUnlocked();
    }

    boolean isEnded() {
        return ended;
    }

    /** Ends the show from any path (close, back, hide, destroy, system). Safe to call repeatedly. */
    void end() {
        if (ended) return;
        ended = true;
        registry.remove(id);
        handler.removeCallbacks(attachTimeout);
        SoilAdActivity current = activity;
        if (current == null) releaseAd();
        else if (!current.isFinishing()) current.finish();
        listener.onClosed();
    }

    private void releaseAd() {
        if (adReleased) return;
        adReleased = true;
        ad.release();
    }
}
