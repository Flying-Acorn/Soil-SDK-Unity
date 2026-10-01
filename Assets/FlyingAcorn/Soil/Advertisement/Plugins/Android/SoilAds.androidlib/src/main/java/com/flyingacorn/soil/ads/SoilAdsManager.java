package com.flyingacorn.soil.ads;

import java.util.HashMap;
import java.util.Map;

/**
 * Slots and show state per format ("Slots" in PROTOCOL.md). Main thread only, except
 * {@link #isReady}. Decoding and drawing are injected so this runs in plain JVM tests.
 */
final class SoilAdsManager {

    interface Cancellable {
        void cancel();
    }

    interface LoadCallback {
        void onLoaded(LoadedAd ad);

        void onFailed(String error, String message);
    }

    /** Decodes a creative off the main thread and calls back on the main thread. */
    interface Loader {
        Cancellable load(String format, AdCreative creative, LoadCallback callback);
    }

    /** What the screen reports about one show. Calls may repeat; only the first of each counts. */
    interface PresentationListener {
        void onShown();

        void onClicked();

        void onUnlocked();

        void onClosed();
    }

    /**
     * Draws ads. The show methods return null on success or an error code; on success the presenter
     * owns the ad reference it was given (banners retain their own) and reports through the listener.
     */
    interface Presenter {
        String showBanner(LoadedAd ad, String position, PresentationListener listener);

        void moveBanner(String position);

        void removeBanner();

        String presentFullscreen(String format, LoadedAd ad, ShowOptions options, PresentationListener listener);

        void dismissFullscreen();
    }

    private static final class Slot {
        volatile LoadedAd ad;
        Cancellable pendingLoad;
        int loadToken;
    }

    private final Loader loader;
    private final Presenter presenter;
    private final EventSink events;
    private final Map<String, Slot> slots = new HashMap<>();

    private Show banner;
    private Show fullscreen;

    SoilAdsManager(Loader loader, Presenter presenter, EventSink events) {
        this.loader = loader;
        this.presenter = presenter;
        this.events = events;
        slots.put(AdFormats.BANNER, new Slot());
        slots.put(AdFormats.INTERSTITIAL, new Slot());
        slots.put(AdFormats.REWARDED, new Slot());
    }

    /** Safe from any thread. */
    boolean isReady(String format) {
        Slot slot = format == null ? null : slots.get(format);
        return slot != null && slot.ad != null;
    }

    void load(final String format, String creativeJson) {
        final Slot slot = format == null ? null : slots.get(format);
        if (slot == null) {
            emit(AdEvents.failure(format, AdEvents.LOAD_FAILED, AdFormats.ERROR_INVALID_FORMAT,
                    "Unknown format: " + format));
            return;
        }
        clearSlot(slot);
        final int token = ++slot.loadToken;
        AdCreative creative = AdCreative.parse(creativeJson);
        if (creative == null) {
            emit(AdEvents.failure(format, AdEvents.LOAD_FAILED, AdFormats.ERROR_INVALID_CREATIVE,
                    "Creative JSON is not an object"));
            return;
        }
        slot.pendingLoad = loader.load(format, creative, new LoadCallback() {
            @Override
            public void onLoaded(LoadedAd ad) {
                if (token != slot.loadToken) {
                    ad.release();
                    return;
                }
                slot.pendingLoad = null;
                slot.ad = ad;
                emit(AdEvents.loaded(format, ad.media, ad.isVideo() ? ad.durationMs : 0));
            }

            @Override
            public void onFailed(String error, String message) {
                if (token != slot.loadToken) return;
                slot.pendingLoad = null;
                emit(AdEvents.failure(format, AdEvents.LOAD_FAILED, error, message));
            }
        });
    }

    void show(String format, String optionsJson) {
        Slot slot = format == null ? null : slots.get(format);
        if (slot == null) {
            showFailed(format, AdFormats.ERROR_INVALID_FORMAT, "Unknown format: " + format);
            return;
        }
        if (AdFormats.BANNER.equals(format)) {
            showBanner(slot, ShowOptions.bannerPosition(optionsJson));
        } else {
            showFullscreen(format, slot, ShowOptions.parse(format, optionsJson));
        }
    }

    private void showBanner(Slot slot, String position) {
        if (banner != null) {
            presenter.moveBanner(position);
            return;
        }
        if (slot.ad == null) {
            showFailed(AdFormats.BANNER, AdFormats.ERROR_NOT_LOADED, "No banner is loaded");
            return;
        }
        Show show = new Show(AdFormats.BANNER);
        banner = show;
        String error = presenter.showBanner(slot.ad, position, show);
        if (error != null) {
            banner = null;
            showFailed(AdFormats.BANNER, error, "The banner could not be attached");
        }
    }

    private void showFullscreen(String format, Slot slot, ShowOptions options) {
        if (fullscreen != null) {
            showFailed(format, AdFormats.ERROR_ALREADY_SHOWING, "A fullscreen ad is already on screen");
            return;
        }
        LoadedAd ad = slot.ad;
        if (ad == null) {
            showFailed(format, AdFormats.ERROR_NOT_LOADED, "No " + format + " is loaded");
            return;
        }
        slot.ad = null;
        Show show = new Show(format);
        show.returnToSlotIfNeverShown(slot, ad);
        fullscreen = show;
        String error = presenter.presentFullscreen(format, ad, options, show);
        if (error != null) {
            fullscreen = null;
            slot.ad = ad; // nothing was shown, so the creative stays loaded for another try
            showFailed(format, error, "The ad could not be presented");
        }
    }

    void hide(String format) {
        if (AdFormats.BANNER.equals(format)) {
            Show show = banner;
            if (show == null) return;
            presenter.removeBanner();
            show.finish();
        } else if (AdFormats.isFullscreen(format)) {
            Show show = fullscreen;
            if (show == null || !show.format.equals(format)) return;
            presenter.dismissFullscreen();
            show.finish();
        }
    }

    void destroy(String format) {
        hide(format);
        Slot slot = format == null ? null : slots.get(format);
        if (slot == null) return;
        clearSlot(slot);
        slot.loadToken++;
    }

    private void clearSlot(Slot slot) {
        if (slot.pendingLoad != null) {
            slot.pendingLoad.cancel();
            slot.pendingLoad = null;
        }
        LoadedAd ad = slot.ad;
        slot.ad = null;
        if (ad != null) ad.release();
    }

    private void showFailed(String format, String error, String message) {
        emit(AdEvents.failure(format, AdEvents.SHOW_FAILED, error, message));
    }

    private void emit(String json) {
        events.send(json);
    }

    /** One show of one ad; keeps the event order shown → clicked* → rewarded? → closed. */
    private final class Show implements PresentationListener {
        final String format;
        private boolean shown;
        private boolean rewarded;
        private boolean finished;
        private Slot slot;
        private LoadedAd ad;
        private int loadToken;

        Show(String format) {
            this.format = format;
        }

        /** A fullscreen ad that never reaches the screen goes back into its slot, unless reloaded. */
        void returnToSlotIfNeverShown(Slot slot, LoadedAd ad) {
            this.slot = slot;
            this.ad = ad;
            this.loadToken = slot.loadToken;
        }

        @Override
        public void onShown() {
            if (shown || finished) return;
            shown = true;
            emit(AdEvents.simple(format, AdEvents.SHOWN));
        }

        @Override
        public void onClicked() {
            if (shown && !finished) emit(AdEvents.simple(format, AdEvents.CLICKED));
        }

        @Override
        public void onUnlocked() {
            if (!AdFormats.REWARDED.equals(format) || !shown || finished || rewarded) return;
            rewarded = true;
            emit(AdEvents.simple(format, AdEvents.REWARDED));
        }

        @Override
        public void onClosed() {
            finish();
        }

        void finish() {
            if (finished) return;
            finished = true;
            if (banner == this) banner = null;
            if (fullscreen == this) fullscreen = null;
            if (shown) {
                emit(AdEvents.simple(format, AdEvents.CLOSED));
            } else {
                if (slot != null && slot.ad == null && slot.loadToken == loadToken && !ad.isReleased()) {
                    ad.retain();
                    slot.ad = ad;
                }
                showFailed(format, AdFormats.ERROR_INTERNAL, "The ad was dismissed before it appeared");
            }
        }
    }
}
