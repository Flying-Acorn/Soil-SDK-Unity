package com.flyingacorn.soil.ads;

import android.app.Activity;
import android.os.Handler;
import android.util.Log;

/** Puts ads on the host (Unity) Activity: the banner as an overlay view, fullscreen as its own Activity. */
final class AndroidPresenter implements SoilAdsManager.Presenter {

    interface HostProvider {
        Activity host();
    }

    private final Handler main;
    private final HostProvider hostProvider;
    private BannerView banner;
    private FullscreenSession fullscreen;

    AndroidPresenter(Handler main, HostProvider hostProvider) {
        this.main = main;
        this.hostProvider = hostProvider;
    }

    private Activity usableHost() {
        Activity host = hostProvider.host();
        return host == null || host.isFinishing() || host.isDestroyed() ? null : host;
    }

    @Override
    public String showBanner(LoadedAd ad, String position, SoilAdsManager.PresentationListener listener) {
        Activity host = usableHost();
        if (host == null) return AdFormats.ERROR_NO_HOST;
        Forgetting tracked = new Forgetting(listener);
        BannerView view = new BannerView(host, ad, tracked);
        tracked.owner = view;
        try {
            view.show(position);
        } catch (RuntimeException e) {
            Log.e(SoilAdsBridge.TAG, "Could not attach the banner", e);
            view.remove();
            return AdFormats.ERROR_INTERNAL;
        }
        banner = view;
        tracked.onShown();
        return null;
    }

    @Override
    public void moveBanner(String position) {
        if (banner != null) banner.move(position);
    }

    @Override
    public void removeBanner() {
        BannerView view = banner;
        banner = null;
        if (view != null) view.remove();
    }

    @Override
    public String presentFullscreen(String format, LoadedAd ad, ShowOptions options,
                                    SoilAdsManager.PresentationListener listener) {
        Activity host = usableHost();
        if (host == null) return AdFormats.ERROR_NO_HOST;
        Forgetting tracked = new Forgetting(listener);
        FullscreenSession session = FullscreenSession.start(host, main, format, ad, options, tracked);
        if (session == null) return AdFormats.ERROR_NO_HOST;
        tracked.owner = session;
        fullscreen = session;
        return null;
    }

    @Override
    public void dismissFullscreen() {
        FullscreenSession session = fullscreen;
        fullscreen = null;
        if (session != null) session.end();
    }

    /**
     * Drops the presenter's reference when an ad closes on its own (close button, back, the host
     * Activity going away), so a finished banner or show is never moved, removed or kept alive
     * later. Hide and destroy clear the reference first and then end the ad, which lands here too.
     */
    private final class Forgetting implements SoilAdsManager.PresentationListener {
        private final SoilAdsManager.PresentationListener inner;
        Object owner;

        Forgetting(SoilAdsManager.PresentationListener inner) {
            this.inner = inner;
        }

        @Override
        public void onShown() {
            inner.onShown();
        }

        @Override
        public void onClicked() {
            inner.onClicked();
        }

        @Override
        public void onUnlocked() {
            inner.onUnlocked();
        }

        @Override
        public void onClosed() {
            if (owner != null && banner == owner) banner = null;
            if (owner != null && fullscreen == owner) fullscreen = null;
            inner.onClosed();
        }
    }
}
