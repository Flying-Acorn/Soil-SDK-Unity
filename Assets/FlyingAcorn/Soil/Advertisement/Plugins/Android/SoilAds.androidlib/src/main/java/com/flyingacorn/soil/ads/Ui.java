package com.flyingacorn.soil.ads;

import android.content.ActivityNotFoundException;
import android.content.Context;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.Build;
import android.text.TextUtils;
import android.util.Log;
import android.util.TypedValue;
import android.view.DisplayCutout;
import android.view.Gravity;
import android.view.View;
import android.view.WindowInsets;
import android.widget.ImageView;
import android.widget.TextView;

/** Small view-building helpers shared by the banner and the fullscreen player. */
final class Ui {
    static final int CTA_COLOR = 0xFF1E88E5;

    private Ui() {
    }

    static int dp(Context context, float dp) {
        return Math.round(dp * context.getResources().getDisplayMetrics().density);
    }

    static GradientDrawable rounded(int color, float radiusPx) {
        GradientDrawable drawable = new GradientDrawable();
        drawable.setColor(color);
        drawable.setCornerRadius(radiusPx);
        return drawable;
    }

    static GradientDrawable circle(int color) {
        GradientDrawable drawable = new GradientDrawable();
        drawable.setShape(GradientDrawable.OVAL);
        drawable.setColor(color);
        return drawable;
    }

    /**
     * Text whose alignment follows its own first strong character, so Persian or Arabic lines up
     * right even inside a left-to-right app.
     */
    static TextView naturalText(Context context, String text, float sp, int color, boolean bold, int maxLines) {
        TextView view = new TextView(context);
        view.setText(text);
        view.setTextSize(TypedValue.COMPLEX_UNIT_SP, sp);
        view.setTextColor(color);
        if (bold) view.setTypeface(Typeface.DEFAULT_BOLD);
        view.setMaxLines(maxLines);
        view.setEllipsize(TextUtils.TruncateAt.END);
        view.setTextDirection(View.TEXT_DIRECTION_FIRST_STRONG);
        view.setTextAlignment(View.TEXT_ALIGNMENT_TEXT_START);
        view.setGravity(Gravity.START | Gravity.CENTER_VERTICAL);
        return view;
    }

    static TextView badge(Context context, float sp) {
        TextView view = new TextView(context);
        view.setText("Ad");
        view.setTextSize(TypedValue.COMPLEX_UNIT_SP, sp);
        view.setTextColor(0xFFFFFFFF);
        view.setTypeface(Typeface.DEFAULT_BOLD);
        view.setBackground(rounded(0x99000000, dp(context, 3)));
        view.setPadding(dp(context, 5), dp(context, 1), dp(context, 5), dp(context, 1));
        return view;
    }

    static TextView ctaButton(Context context, String text, float sp) {
        TextView view = new TextView(context);
        view.setText(text);
        view.setTextSize(TypedValue.COMPLEX_UNIT_SP, sp);
        view.setTextColor(0xFFFFFFFF);
        view.setTypeface(Typeface.DEFAULT_BOLD);
        view.setMaxLines(1);
        view.setEllipsize(TextUtils.TruncateAt.END);
        view.setGravity(Gravity.CENTER);
        view.setBackground(rounded(CTA_COLOR, dp(context, 20)));
        view.setPadding(dp(context, 14), dp(context, 8), dp(context, 14), dp(context, 8));
        view.setContentDescription("soil_ad_cta");
        return view;
    }

    static ImageView roundedImage(Context context, Bitmap bitmap, float radiusDp) {
        ImageView view = new ImageView(context);
        view.setScaleType(ImageView.ScaleType.CENTER_CROP);
        view.setImageBitmap(bitmap);
        view.setBackground(rounded(0xFF333333, dp(context, radiusDp)));
        view.setClipToOutline(true);
        return view;
    }

    /** Left, top, right, bottom display cutout insets; zeros before API 28 or without a cutout. */
    static int[] cutoutInsets(WindowInsets insets) {
        int[] result = new int[4];
        if (insets == null || Build.VERSION.SDK_INT < 28) return result;
        DisplayCutout cutout = insets.getDisplayCutout();
        if (cutout == null) return result;
        result[0] = cutout.getSafeInsetLeft();
        result[1] = cutout.getSafeInsetTop();
        result[2] = cutout.getSafeInsetRight();
        result[3] = cutout.getSafeInsetBottom();
        return result;
    }

    static int[] cutoutInsets(View anyAttachedView) {
        if (Build.VERSION.SDK_INT < 28 || anyAttachedView == null) return new int[4];
        return cutoutInsets(anyAttachedView.getRootWindowInsets());
    }

    /**
     * Left, top, right, bottom insets a banner must keep clear of: the display cutout plus any
     * system bar that is actually visible. Hidden (immersive) bars count as zero, so fullscreen
     * games are unaffected, while a game that keeps its bars (edge-to-edge on Android 15+) gets
     * its banner above the navigation bar instead of under it.
     */
    @SuppressWarnings("deprecation")
    static int[] bannerInsets(WindowInsets insets) {
        int[] result = cutoutInsets(insets);
        if (insets == null) return result;
        int[] bars;
        if (Build.VERSION.SDK_INT >= 30) {
            android.graphics.Insets visible = insets.getInsets(WindowInsets.Type.systemBars());
            bars = new int[]{visible.left, visible.top, visible.right, visible.bottom};
        } else {
            bars = new int[]{insets.getSystemWindowInsetLeft(), insets.getSystemWindowInsetTop(),
                    insets.getSystemWindowInsetRight(), insets.getSystemWindowInsetBottom()};
        }
        for (int i = 0; i < 4; i++) result[i] = Math.max(result[i], bars[i]);
        return result;
    }

    static int[] bannerInsets(View anyAttachedView) {
        if (Build.VERSION.SDK_INT < 23 || anyAttachedView == null) return new int[4];
        return bannerInsets(anyAttachedView.getRootWindowInsets());
    }

    /** Opens the click URL with the system; failures are logged, never thrown. */
    static void openUrl(Context context, String url) {
        if (url == null) return;
        try {
            Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(url));
            intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            context.startActivity(intent);
        } catch (ActivityNotFoundException e) {
            Log.w(SoilAdsBridge.TAG, "No app can open " + url);
        } catch (RuntimeException e) {
            Log.w(SoilAdsBridge.TAG, "Could not open " + url, e);
        }
    }
}
