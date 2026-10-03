package com.flyingacorn.soil.ads;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertSame;
import static org.junit.Assert.assertTrue;

import org.json.JSONObject;
import org.junit.Before;
import org.junit.Test;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

/** The slot / show state machine ("Slots" and "Events" in PROTOCOL.md), with fake loader and screen. */
public class SoilAdsManagerTest {
    private static final String CREATIVE = "{\"adId\":\"a\",\"imagePath\":\"/i.png\"}";

    private final List<String> events = new ArrayList<>();
    private FakeLoader loader;
    private FakePresenter presenter;
    private SoilAdsManager manager;

    @Before
    public void setUp() {
        loader = new FakeLoader();
        presenter = new FakePresenter();
        manager = new SoilAdsManager(loader, presenter, new EventSink() {
            @Override
            public void send(String json) {
                events.add(json);
            }
        });
    }

    // ---- helpers

    private static final class PendingLoad implements SoilAdsManager.Cancellable {
        final String format;
        final AdCreative creative;
        final SoilAdsManager.LoadCallback callback;
        boolean cancelled;

        PendingLoad(String format, AdCreative creative, SoilAdsManager.LoadCallback callback) {
            this.format = format;
            this.creative = creative;
            this.callback = callback;
        }

        @Override
        public void cancel() {
            cancelled = true;
        }

        LoadedAd succeed(String media, long durationMs) {
            LoadedAd ad = new LoadedAd(creative, media, durationMs, null, null);
            callback.onLoaded(ad);
            return ad;
        }
    }

    private static final class FakeLoader implements SoilAdsManager.Loader {
        final List<PendingLoad> loads = new ArrayList<>();

        @Override
        public SoilAdsManager.Cancellable load(String format, AdCreative creative,
                                               SoilAdsManager.LoadCallback callback) {
            PendingLoad load = new PendingLoad(format, creative, callback);
            loads.add(load);
            return load;
        }

        PendingLoad last() {
            return loads.get(loads.size() - 1);
        }
    }

    private static final class FakePresenter implements SoilAdsManager.Presenter {
        String error;
        LoadedAd bannerAd;
        String bannerPosition;
        SoilAdsManager.PresentationListener bannerListener;
        LoadedAd fullscreenAd;
        ShowOptions fullscreenOptions;
        SoilAdsManager.PresentationListener fullscreenListener;
        int dismissCalls;
        int removeCalls;
        boolean reportCloseOnDismiss = true;

        @Override
        public String showBanner(LoadedAd ad, String position, SoilAdsManager.PresentationListener listener) {
            if (error != null) return error;
            bannerAd = ad;
            bannerPosition = position;
            bannerListener = listener;
            listener.onShown();
            return null;
        }

        @Override
        public void moveBanner(String position) {
            bannerPosition = position;
        }

        @Override
        public void removeBanner() {
            removeCalls++;
            if (bannerListener != null) bannerListener.onClosed();
            bannerListener = null;
        }

        @Override
        public String presentFullscreen(String format, LoadedAd ad, ShowOptions options,
                                        SoilAdsManager.PresentationListener listener) {
            if (error != null) return error;
            fullscreenAd = ad;
            fullscreenOptions = options;
            fullscreenListener = listener;
            return null;
        }

        @Override
        public void dismissFullscreen() {
            dismissCalls++;
            if (reportCloseOnDismiss && fullscreenListener != null) fullscreenListener.onClosed();
        }
    }

    private List<String> eventNames() {
        List<String> names = new ArrayList<>();
        for (String json : events) names.add(parse(json).optString("format") + ":" + parse(json).optString("event"));
        return names;
    }

    private static JSONObject parse(String json) {
        try {
            return new JSONObject(json);
        } catch (Exception e) {
            throw new AssertionError(json);
        }
    }

    private JSONObject lastEvent() {
        return parse(events.get(events.size() - 1));
    }

    private LoadedAd loadReady(String format, String media, long durationMs) {
        manager.load(format, CREATIVE);
        LoadedAd ad = loader.last().succeed(media, durationMs);
        events.clear();
        return ad;
    }

    // ---- formats

    @Test
    public void unknownFormatsAreRejected() throws Exception {
        manager.load("native", CREATIVE);
        assertEquals("loadFailed", lastEvent().getString("event"));
        assertEquals("invalid_format", lastEvent().getString("error"));
        assertEquals("native", lastEvent().getString("format"));

        manager.show("popup", "{}");
        assertEquals("showFailed", lastEvent().getString("event"));
        assertEquals("invalid_format", lastEvent().getString("error"));

        manager.load(null, CREATIVE);
        assertEquals("invalid_format", lastEvent().getString("error"));
        manager.show(null, null);
        assertEquals("invalid_format", lastEvent().getString("error"));

        int before = events.size();
        manager.hide("popup");
        manager.hide(null);
        manager.destroy("popup");
        manager.destroy(null);
        assertEquals(before, events.size());
        assertFalse(manager.isReady(null));
        assertFalse(manager.isReady("native"));
        assertTrue(loader.loads.isEmpty());
    }

    // ---- load

    @Test
    public void badCreativeJsonFailsLoad() throws Exception {
        manager.load("interstitial", "{oops");
        assertEquals("loadFailed", lastEvent().getString("event"));
        assertEquals("invalid_creative", lastEvent().getString("error"));
        manager.load("interstitial", null);
        assertEquals("invalid_creative", lastEvent().getString("error"));
        assertFalse(manager.isReady("interstitial"));
    }

    @Test
    public void successfulLoadReportsMediaAndDuration() throws Exception {
        manager.load("rewarded", CREATIVE);
        assertFalse(manager.isReady("rewarded"));
        loader.last().succeed("video", 15000);
        assertTrue(manager.isReady("rewarded"));
        JSONObject e = lastEvent();
        assertEquals("rewarded", e.getString("format"));
        assertEquals("loaded", e.getString("event"));
        assertEquals("video", e.getString("media"));
        assertEquals(15000, e.getLong("durationMs"));
    }

    @Test
    public void imageLoadReportsZeroDuration() throws Exception {
        manager.load("interstitial", CREATIVE);
        loader.last().succeed("image", 999);
        assertEquals("image", lastEvent().getString("media"));
        assertEquals(0, lastEvent().getLong("durationMs"));
    }

    @Test
    public void loaderFailureIsReported() throws Exception {
        manager.load("banner", CREATIVE);
        loader.last().callback.onFailed("media_unreadable", "broken");
        assertEquals("loadFailed", lastEvent().getString("event"));
        assertEquals("media_unreadable", lastEvent().getString("error"));
        assertEquals("broken", lastEvent().getString("message"));
        assertFalse(manager.isReady("banner"));
    }

    @Test
    public void secondLoadCancelsFirstAndOnlyLastReports() {
        manager.load("interstitial", CREATIVE);
        PendingLoad first = loader.last();
        manager.load("interstitial", CREATIVE);
        PendingLoad second = loader.last();
        assertTrue(first.cancelled);

        LoadedAd late = first.succeed("image", 0);
        assertTrue(events.isEmpty());
        assertTrue(late.isReleased());
        first.callback.onFailed("internal", "late");
        assertTrue(events.isEmpty());

        second.succeed("image", 0);
        assertEquals(Arrays.asList("interstitial:loaded"), eventNames());
    }

    @Test
    public void badSecondLoadStillCancelsFirst() {
        manager.load("interstitial", CREATIVE);
        PendingLoad first = loader.last();
        manager.load("interstitial", "not json");
        assertTrue(first.cancelled);
        first.succeed("image", 0);
        assertEquals(Arrays.asList("interstitial:loadFailed"), eventNames());
    }

    @Test
    public void loadReplacesAndReleasesSlotContent() {
        LoadedAd old = loadReady("interstitial", "image", 0);
        manager.load("interstitial", CREATIVE);
        assertTrue(old.isReleased());
        assertFalse(manager.isReady("interstitial"));
    }

    @Test
    public void slotsAreIndependentPerFormat() {
        loadReady("interstitial", "image", 0);
        assertTrue(manager.isReady("interstitial"));
        assertFalse(manager.isReady("rewarded"));
        assertFalse(manager.isReady("banner"));
    }

    // ---- fullscreen

    @Test
    public void showWithoutLoadFails() throws Exception {
        manager.show("rewarded", "{}");
        assertEquals("showFailed", lastEvent().getString("event"));
        assertEquals("not_loaded", lastEvent().getString("error"));
        assertNull(presenter.fullscreenListener);
    }

    @Test
    public void fullscreenShowConsumesSlotAndOrdersEvents() {
        LoadedAd ad = loadReady("rewarded", "video", 3000);
        manager.show("rewarded", "{\"imageLockSeconds\":1}");
        assertSame(ad, presenter.fullscreenAd);
        assertEquals(1, presenter.fullscreenOptions.imageLockSeconds, 1e-9);
        assertFalse(manager.isReady("rewarded"));
        assertTrue(events.isEmpty()); // shown only once the screen says so

        SoilAdsManager.PresentationListener screen = presenter.fullscreenListener;
        screen.onClicked(); // before shown: ignored
        screen.onShown();
        screen.onShown();
        screen.onClicked();
        screen.onUnlocked();
        screen.onUnlocked();
        screen.onClicked();
        screen.onClosed();
        screen.onClosed();
        screen.onClicked();
        screen.onUnlocked();
        assertEquals(Arrays.asList("rewarded:shown", "rewarded:clicked", "rewarded:rewarded", "rewarded:clicked",
                "rewarded:closed"), eventNames());
    }

    @Test
    public void interstitialNeverRewards() {
        loadReady("interstitial", "image", 0);
        manager.show("interstitial", null);
        presenter.fullscreenListener.onShown();
        presenter.fullscreenListener.onUnlocked();
        presenter.fullscreenListener.onClosed();
        assertEquals(Arrays.asList("interstitial:shown", "interstitial:closed"), eventNames());
    }

    @Test
    public void onlyOneFullscreenAtATime() throws Exception {
        loadReady("interstitial", "image", 0);
        loadReady("rewarded", "image", 0);
        manager.show("interstitial", null);
        presenter.fullscreenListener.onShown();
        manager.show("rewarded", null);
        assertEquals("already_showing", lastEvent().getString("error"));
        assertEquals("rewarded", lastEvent().getString("format"));
        assertTrue(manager.isReady("rewarded"));
        manager.show("interstitial", null);
        assertEquals("already_showing", lastEvent().getString("error"));
    }

    @Test
    public void nextFullscreenCanShowAfterClose() {
        loadReady("interstitial", "image", 0);
        loadReady("rewarded", "image", 0);
        manager.show("interstitial", null);
        presenter.fullscreenListener.onShown();
        presenter.fullscreenListener.onClosed();
        manager.show("rewarded", null);
        presenter.fullscreenListener.onShown();
        assertEquals(Arrays.asList("interstitial:shown", "interstitial:closed", "rewarded:shown"), eventNames());
    }

    @Test
    public void hideWhileShowingClosesExactlyOnceWithoutReward() {
        loadReady("rewarded", "video", 3000);
        manager.show("rewarded", null);
        presenter.fullscreenListener.onShown();
        manager.hide("rewarded");
        manager.hide("rewarded");
        presenter.fullscreenListener.onClosed(); // the Activity going away later
        assertEquals(1, presenter.dismissCalls);
        assertEquals(Arrays.asList("rewarded:shown", "rewarded:closed"), eventNames());
    }

    @Test
    public void hideClosesEvenIfPresenterStaysSilent() {
        presenter.reportCloseOnDismiss = false;
        loadReady("interstitial", "image", 0);
        manager.show("interstitial", null);
        presenter.fullscreenListener.onShown();
        manager.hide("interstitial");
        assertEquals(Arrays.asList("interstitial:shown", "interstitial:closed"), eventNames());
    }

    @Test
    public void hideAfterRewardKeepsRewardBeforeClosed() {
        loadReady("rewarded", "image", 0);
        manager.show("rewarded", null);
        presenter.fullscreenListener.onShown();
        presenter.fullscreenListener.onUnlocked();
        manager.hide("rewarded");
        assertEquals(Arrays.asList("rewarded:shown", "rewarded:rewarded", "rewarded:closed"), eventNames());
    }

    @Test
    public void hideOfOtherFullscreenFormatDoesNothing() {
        loadReady("interstitial", "image", 0);
        manager.show("interstitial", null);
        presenter.fullscreenListener.onShown();
        manager.hide("rewarded");
        manager.hide("banner");
        assertEquals(0, presenter.dismissCalls);
        assertEquals(Arrays.asList("interstitial:shown"), eventNames());
    }

    @Test
    public void hideBeforeShownReportsShowFailed() throws Exception {
        loadReady("interstitial", "image", 0);
        manager.show("interstitial", null);
        manager.hide("interstitial");
        assertEquals(Arrays.asList("interstitial:showFailed"), eventNames());
        assertEquals("internal", lastEvent().getString("error"));
    }

    @Test
    public void adThatNeverAppearedGoesBackIntoItsSlot() throws Exception {
        LoadedAd ad = loadReady("interstitial", "image", 0);
        manager.show("interstitial", null);
        assertFalse(manager.isReady("interstitial"));
        manager.hide("interstitial");
        assertTrue(manager.isReady("interstitial"));
        assertFalse(ad.isReleased());

        events.clear();
        manager.show("interstitial", null);
        assertSame(ad, presenter.fullscreenAd);
    }

    @Test
    public void adThatNeverAppearedStaysOutOfAReloadedOrDestroyedSlot() throws Exception {
        loadReady("interstitial", "image", 0);
        manager.show("interstitial", null);
        LoadedAd second = loadReady("interstitial", "image", 0);
        manager.hide("interstitial");
        assertTrue(manager.isReady("interstitial"));
        manager.show("interstitial", null);
        assertSame("the newer load wins", second, presenter.fullscreenAd);

        manager.hide("interstitial");
        manager.destroy("interstitial");
        assertFalse(manager.isReady("interstitial"));
    }

    @Test
    public void presenterFailureKeepsTheAdLoaded() throws Exception {
        LoadedAd ad = loadReady("interstitial", "image", 0);
        presenter.error = "no_host";
        manager.show("interstitial", null);
        assertEquals("showFailed", lastEvent().getString("event"));
        assertEquals("no_host", lastEvent().getString("error"));
        assertTrue(manager.isReady("interstitial"));
        assertFalse(ad.isReleased());

        presenter.error = null;
        manager.show("interstitial", null);
        assertSame(ad, presenter.fullscreenAd);
    }

    @Test
    public void loadDuringShowFillsSlotWithoutTouchingScreen() {
        LoadedAd onScreen = loadReady("interstitial", "image", 0);
        manager.show("interstitial", null);
        presenter.fullscreenListener.onShown();
        manager.load("interstitial", CREATIVE);
        loader.last().succeed("image", 0);
        assertFalse(onScreen.isReleased());
        assertTrue(manager.isReady("interstitial"));
        assertEquals(Arrays.asList("interstitial:shown", "interstitial:loaded"), eventNames());
    }

    @Test
    public void destroyHidesAndEmptiesSlot() {
        loadReady("interstitial", "image", 0);
        manager.show("interstitial", null);
        presenter.fullscreenListener.onShown();
        manager.load("interstitial", CREATIVE);
        PendingLoad pending = loader.last();
        manager.destroy("interstitial");
        assertTrue(pending.cancelled);
        pending.succeed("image", 0);
        assertFalse(manager.isReady("interstitial"));
        assertEquals(Arrays.asList("interstitial:shown", "interstitial:closed"), eventNames());
        manager.destroy("interstitial");
        assertEquals(2, events.size());
    }

    @Test
    public void destroyReleasesLoadedAd() {
        LoadedAd ad = loadReady("rewarded", "image", 0);
        manager.destroy("rewarded");
        assertTrue(ad.isReleased());
        assertFalse(manager.isReady("rewarded"));
        assertTrue(events.isEmpty());
    }

    // ---- banner

    @Test
    public void bannerShowDoesNotConsumeAndRepeatedShowMoves() {
        loadReady("banner", "text", 0);
        manager.show("banner", "{\"position\":\"top\"}");
        assertEquals("top", presenter.bannerPosition);
        assertTrue(manager.isReady("banner"));
        manager.show("banner", "{\"position\":\"center\"}");
        assertEquals("center", presenter.bannerPosition);
        assertEquals(Arrays.asList("banner:shown"), eventNames());

        manager.hide("banner");
        manager.hide("banner");
        assertEquals(1, presenter.removeCalls);
        manager.show("banner", null);
        assertEquals("bottom", presenter.bannerPosition);
        assertEquals(Arrays.asList("banner:shown", "banner:closed", "banner:shown"), eventNames());
    }

    @Test
    public void bannerWithoutLoadFails() throws Exception {
        manager.show("banner", null);
        assertEquals("not_loaded", lastEvent().getString("error"));
    }

    @Test
    public void bannerNoHost() throws Exception {
        loadReady("banner", "image", 0);
        presenter.error = "no_host";
        manager.show("banner", null);
        assertEquals("no_host", lastEvent().getString("error"));
        presenter.error = null;
        manager.show("banner", null);
        assertEquals("shown", lastEvent().getString("event"));
    }

    @Test
    public void bannerClosedBySystemCanShowAgain() {
        loadReady("banner", "image", 0);
        manager.show("banner", null);
        presenter.bannerListener.onClosed(); // e.g. the host Activity was destroyed
        manager.show("banner", null);
        assertEquals(Arrays.asList("banner:shown", "banner:closed", "banner:shown"), eventNames());
    }

    @Test
    public void bannerAndFullscreenAreIndependent() {
        loadReady("banner", "image", 0);
        loadReady("rewarded", "image", 0);
        manager.show("banner", null);
        manager.show("rewarded", null);
        presenter.fullscreenListener.onShown();
        manager.hide("banner");
        assertEquals(0, presenter.dismissCalls);
        assertNotNull(presenter.fullscreenListener);
        assertEquals(Arrays.asList("banner:shown", "rewarded:shown", "banner:closed"), eventNames());
    }

    @Test
    public void destroyBannerWhileShowing() {
        loadReady("banner", "image", 0);
        manager.show("banner", null);
        manager.destroy("banner");
        assertFalse(manager.isReady("banner"));
        assertEquals(Arrays.asList("banner:shown", "banner:closed"), eventNames());
    }
}
