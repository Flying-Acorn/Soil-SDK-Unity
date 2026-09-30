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
        BannerView view = new BannerView(host, ad, listener);
        try {
            view.show(position);
        } catch (RuntimeException e) {
            Log.e(SoilAdsBridge.TAG, "Could not attach the banner", e);
            view.remove();
            return AdFormats.ERROR_INTERNAL;
        }
        banner = view;
        listener.onShown();
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
        FullscreenSession session = FullscreenSession.start(host, main, format, ad, options, listener);
        if (session == null) return AdFormats.ERROR_NO_HOST;
        fullscreen = session;
        return null;
    }

    @Override
    public void dismissFullscreen() {
        FullscreenSession session = fullscreen;
        fullscreen = null;
        if (session != null) session.end();
    }
}
