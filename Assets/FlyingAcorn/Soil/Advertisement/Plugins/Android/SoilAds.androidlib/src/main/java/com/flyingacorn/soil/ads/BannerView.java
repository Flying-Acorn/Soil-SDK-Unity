package com.flyingacorn.soil.ads;

import android.app.Activity;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;

import java.util.Arrays;

/**
 * The banner, added on top of Unity's view with {@code addContentView} ("Banner presentation" in
 * PROTOCOL.md). It only covers its own rectangle, so touches elsewhere still reach the game.
 */
final class BannerView extends FrameLayout {
    private final Activity activity;
    private final LoadedAd ad;
    private final SoilAdsManager.PresentationListener listener;
    private final float scale;
    private String position = ShowOptions.POSITION_BOTTOM;
    private int[] safeInsets = new int[4];
    private boolean attached;
    private boolean closed;

    BannerView(Activity activity, LoadedAd ad, SoilAdsManager.PresentationListener listener) {
        super(activity);
        this.activity = activity;
        this.ad = ad;
        this.listener = listener;
        this.scale = isTablet() ? 1.5f : 1f;
        setContentDescription("soil_ad_banner");
        setBackgroundColor(0xFF1A1A1A);
        setClickable(true);
        setOnClickListener(new OnClickListener() {
            @Override
            public void onClick(View v) {
                if (!attached || closed) return;
                // Reported even without a URL or an app to open it, as the SDK always did.
                BannerView.this.listener.onClicked();
                Ui.openUrl(getContext(), ad.creative.clickUrl);
            }
        });
        buildContent();
        setOnApplyWindowInsetsListener(new OnApplyWindowInsetsListener() {
            @Override
            public WindowInsets onApplyWindowInsets(View view, WindowInsets insets) {
                int[] insetsNow = Ui.safeInsets(insets);
                if (!Arrays.equals(insetsNow, safeInsets)) {
                    safeInsets = insetsNow;
                    post(new Runnable() {
                        @Override
                        public void run() {
                            if (!closed) setLayoutParams(layoutParams());
                        }
                    });
                }
                return insets;
            }
        });
        addOnAttachStateChangeListener(new OnAttachStateChangeListener() {
            @Override
            public void onViewAttachedToWindow(View view) {
            }

            @Override
            public void onViewDetachedFromWindow(View view) {
                close(); // also covers Unity's Activity going away underneath us
            }
        });
    }

    void show(String position) {
        this.position = position;
        safeInsets = Ui.safeInsets(activity.getWindow().getDecorView());
        activity.addContentView(this, layoutParams());
        ad.retain();
        attached = true;
    }

    void move(String position) {
        this.position = position;
        safeInsets = Ui.safeInsets(activity.getWindow().getDecorView());
        setLayoutParams(layoutParams());
    }

    void remove() {
        ViewGroup parent = (ViewGroup) getParent();
        if (parent != null) parent.removeView(this);
        close();
    }

    /** Reports {@code closed} once for a banner that made it on screen. */
    private void close() {
        if (!attached || closed) return;
        closed = true;
        clearImages(this);
        ad.release();
        listener.onClosed();
    }

    private boolean isTablet() {
        return getResources().getConfiguration().smallestScreenWidthDp >= 600;
    }

    private FrameLayout.LayoutParams layoutParams() {
        int gravity;
        if (ShowOptions.POSITION_TOP.equals(position)) gravity = Gravity.TOP;
        else if (ShowOptions.POSITION_CENTER.equals(position)) gravity = Gravity.CENTER_VERTICAL;
        else gravity = Gravity.BOTTOM;
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT,
                Ui.dp(getContext(), isTablet() ? 90 : 50), gravity | Gravity.CENTER_HORIZONTAL);
        params.leftMargin = safeInsets[0];
        params.rightMargin = safeInsets[2];
        if (gravity == Gravity.TOP) params.topMargin = safeInsets[1];
        if (gravity == Gravity.BOTTOM) params.bottomMargin = safeInsets[3];
        return params;
    }

    private void buildContent() {
        if (ad.image != null) {
            ImageView image = new ImageView(getContext());
            image.setScaleType(ImageView.ScaleType.FIT_CENTER);
            image.setImageBitmap(ad.image);
            addView(image, new LayoutParams(-1, -1));
        } else {
            addView(buildRow(), new LayoutParams(-1, -1));
        }
        LayoutParams badgeParams = new LayoutParams(-2, -2, Gravity.TOP | Gravity.LEFT);
        badgeParams.setMargins(Ui.dp(getContext(), 2), Ui.dp(getContext(), 2), 0, 0);
        addView(Ui.badge(getContext(), 8 * scale), badgeParams);
    }

    private View buildRow() {
        AdCreative creative = ad.creative;
        int padding = Ui.dp(getContext(), 6 * scale);
        LinearLayout row = new LinearLayout(getContext());
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setPadding(padding * 2, padding, padding, padding);

        if (ad.logo != null) {
            int size = Ui.dp(getContext(), (isTablet() ? 90 : 50) - 12 * scale);
            LinearLayout.LayoutParams logoParams = new LinearLayout.LayoutParams(size, size);
            logoParams.setMarginEnd(padding);
            row.addView(Ui.roundedImage(getContext(), ad.logo, 6 * scale), logoParams);
        }
        LinearLayout texts = new LinearLayout(getContext());
        texts.setOrientation(LinearLayout.VERTICAL);
        texts.setGravity(Gravity.CENTER_VERTICAL);
        if (creative.title != null) {
            texts.addView(Ui.naturalText(getContext(), creative.title, 14 * scale, 0xFFFFFFFF, true, 1),
                    new LinearLayout.LayoutParams(-1, -2));
        }
        if (creative.description != null) {
            texts.addView(Ui.naturalText(getContext(), creative.description, 11 * scale, 0xCCFFFFFF, false, 1),
                    new LinearLayout.LayoutParams(-1, -2));
        }
        row.addView(texts, new LinearLayout.LayoutParams(0, -1, 1f));
        if (creative.callToAction != null) {
            // Not clickable itself: the tap falls through to the banner, which handles it.
            LinearLayout.LayoutParams ctaParams = new LinearLayout.LayoutParams(-2, -2);
            ctaParams.setMarginStart(padding);
            row.addView(Ui.ctaButton(getContext(), creative.callToAction, 13 * scale), ctaParams);
        }
        return row;
    }

    private static void clearImages(ViewGroup group) {
        for (int i = 0; i < group.getChildCount(); i++) {
            View child = group.getChildAt(i);
            if (child instanceof ImageView) ((ImageView) child).setImageDrawable(null);
            else if (child instanceof ViewGroup) clearImages((ViewGroup) child);
        }
    }
}
