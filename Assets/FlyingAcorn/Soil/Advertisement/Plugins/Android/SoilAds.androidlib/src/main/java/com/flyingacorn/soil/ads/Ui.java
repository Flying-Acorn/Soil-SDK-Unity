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
import android.widget.LinearLayout;
import android.widget.TextView;

/** Small view-building helpers shared by the banner and the fullscreen player. */
final class Ui {
    static final int CTA_COLOR = 0xFF0A85FF;
    static final int BADGE_COLOR = 0xFFFFCC33;
    /** The text of the ad badge on every format: Persian for "ad". */
    static final String BADGE_TEXT = "\u062A\u0628\u0644\u06CC\u063A"; // تبلیغ

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
        // Wrapped lines get some air: Persian's tall marks crowd the line above at the default spacing.
        if (maxLines > 1) view.setLineSpacing(0, 1.12f);
        view.setTextDirection(View.TEXT_DIRECTION_FIRST_STRONG);
        view.setTextAlignment(View.TEXT_ALIGNMENT_TEXT_START);
        view.setGravity(Gravity.START | Gravity.CENTER_VERTICAL);
        return view;
    }

    /**
     * Whether {@code text} reads right to left: its first strong directional character belongs to
     * a right-to-left script. Digits, punctuation, spaces and symbols are skipped.
     */
    static boolean isRightToLeft(String text) {
        if (text == null) return false;
        for (int i = 0; i < text.length(); ) {
            int c = text.codePointAt(i);
            i += Character.charCount(c);
            byte direction = Character.getDirectionality(c);
            if (direction == Character.DIRECTIONALITY_RIGHT_TO_LEFT
                    || direction == Character.DIRECTIONALITY_RIGHT_TO_LEFT_ARABIC) return true;
            if (direction == Character.DIRECTIONALITY_LEFT_TO_RIGHT) return false;
        }
        return false;
    }

    /**
     * Whether an ad's text block reads right to left: the direction of its title, else of its
     * description, else of its call to action. Logos and buttons are laid out to match.
     */
    static boolean creativeIsRightToLeft(AdCreative creative) {
        if (creative.title != null) return isRightToLeft(creative.title);
        if (creative.description != null) return isRightToLeft(creative.description);
        return isRightToLeft(creative.callToAction);
    }

    /**
     * Adds {@code views} to a horizontal row in reading order: left to right, or right to left (the
     * logo on the right for Persian). Done by hand because a game's manifest rarely declares
     * {@code supportsRtl}, without which Android lays every row out left to right. {@code gapPx}
     * goes between neighbours; null views are skipped.
     */
    static void addInReadingOrder(LinearLayout row, boolean rightToLeft, int gapPx, View[] views,
                                  LinearLayout.LayoutParams[] params) {
        boolean first = true;
        for (int k = 0; k < views.length; k++) {
            int i = rightToLeft ? views.length - 1 - k : k;
            if (views[i] == null) continue;
            if (!first) params[i].leftMargin = gapPx;
            first = false;
            row.addView(views[i], params[i]);
        }
    }

    static TextView badge(Context context, float sp) {
        TextView view = new TextView(context);
        view.setText(BADGE_TEXT);
        view.setTextSize(TypedValue.COMPLEX_UNIT_SP, sp);
        view.setTextColor(0xFF000000);
        view.setTypeface(Typeface.DEFAULT_BOLD);
        view.setIncludeFontPadding(false);
        view.setGravity(Gravity.CENTER);
        view.setBackground(rounded(BADGE_COLOR, dp(context, sp * 0.4f)));
        int horizontal = dp(context, sp * 0.5f);
        int vertical = dp(context, sp * 0.12f);
        view.setPadding(horizontal, vertical, horizontal, vertical);
        view.setContentDescription("Ad");
        return view;
    }

    /** Rounded call-to-action button that takes the width it is given (fullscreen). */
    static TextView ctaButton(Context context, String text, float sp) {
        TextView view = new TextView(context);
        view.setText(text);
        view.setTextSize(TypedValue.COMPLEX_UNIT_SP, sp);
        view.setTextColor(0xFFFFFFFF);
        view.setTypeface(Typeface.DEFAULT_BOLD);
        view.setMaxLines(1);
        view.setEllipsize(TextUtils.TruncateAt.END);
        view.setGravity(Gravity.CENTER);
        view.setBackground(rounded(CTA_COLOR, dp(context, 14)));
        view.setPadding(dp(context, 16), dp(context, 8), dp(context, 16), dp(context, 8));
        view.setContentDescription("soil_ad_cta");
        return view;
    }

    /** Rounded call-to-action button as wide as its title, at most 160dp (banner row). */
    static TextView compactCtaButton(Context context, String text, float sp) {
        TextView view = ctaButton(context, text, sp);
        // A long call to action must not squeeze the title and description away.
        view.setMaxWidth(dp(context, 160));
        view.setBackground(rounded(CTA_COLOR, dp(context, 8)));
        view.setPadding(dp(context, 14), dp(context, 8), dp(context, 14), dp(context, 8));
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

    /**
     * Left, top, right, bottom insets ad content must keep clear of: the display cutout plus any
     * system bar that is actually visible. Hidden (immersive) bars count as zero, so fullscreen
     * games and the immersive ad player are unaffected, while a window whose bars show (a game that
     * keeps them, edge-to-edge on Android 15+, split screen) keeps its banner and ad controls off
     * the bars instead of under them. Before API 30 the visible bars are the system window insets,
     * which only drop to zero for hidden bars when the window does not ask for
     * {@code SYSTEM_UI_FLAG_LAYOUT_STABLE}.
     */
    @SuppressWarnings("deprecation")
    static int[] safeInsets(WindowInsets insets) {
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

    static int[] safeInsets(View anyAttachedView) {
        if (Build.VERSION.SDK_INT < 23 || anyAttachedView == null) return new int[4];
        return safeInsets(anyAttachedView.getRootWindowInsets());
    }

    /** Web and store links: always opened. */
    private static final String[] WEB_AND_STORE_SCHEMES = {"http", "https", "market"};

    /** Schemes that act on the device rather than open a page or an app: never opened. */
    private static final String[] DEVICE_SCHEMES = {
            "javascript", "vbscript", "data", "file", "content", "intent", "android-app", "about", "blob",
            "tel", "sms", "smsto", "mms", "mmsto", "mailto", "wtai"
    };

    /**
     * The click URL to open, trimmed, or null when it must not be opened. Web and store links
     * pass, and so do other app schemes (stores and apps deep-link with their own), except the
     * ones that act on the device: starting arbitrary components ({@code intent:}), reading local
     * files or content providers, running script, calling or messaging. A link with control
     * characters or nothing after its scheme is refused too. Same rule as the C# AdLinkPolicy
     * with app links allowed.
     */
    static String clickUrlToOpen(String url) {
        if (url == null) return null;
        String trimmed = trim(url);
        int colon = trimmed.indexOf(':');
        if (colon <= 0 || colon == trimmed.length() - 1) return null;
        for (int i = 0; i < trimmed.length(); i++) {
            if (Character.isISOControl(trimmed.charAt(i))) return null;
        }
        String scheme = scheme(trimmed);
        if (scheme == null) return null;
        if (contains(WEB_AND_STORE_SCHEMES, scheme)) return trimmed;
        return contains(DEVICE_SCHEMES, scheme) ? null : trimmed;
    }

    static boolean isOpenableClickUrl(String url) {
        return clickUrlToOpen(url) != null;
    }

    private static boolean contains(String[] schemes, String scheme) {
        for (String s : schemes) {
            if (s.equals(scheme)) return true;
        }
        return false;
    }

    /** Trims whitespace, Unicode spaces included. */
    private static String trim(String text) {
        int start = 0;
        int end = text.length();
        while (start < end && isSpace(text.charAt(start))) start++;
        while (end > start && isSpace(text.charAt(end - 1))) end--;
        return text.substring(start, end);
    }

    private static boolean isSpace(char c) {
        return Character.isWhitespace(c) || Character.isSpaceChar(c);
    }

    /** The lowercase RFC 3986 scheme of {@code url}, or null when it has none. */
    static String scheme(String url) {
        if (url == null) return null;
        int colon = url.indexOf(':');
        if (colon <= 0) return null;
        for (int i = 0; i < colon; i++) {
            char c = url.charAt(i);
            boolean letter = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
            boolean valid = letter || (i > 0 && ((c >= '0' && c <= '9') || c == '+' || c == '-' || c == '.'));
            if (!valid) return null;
        }
        return url.substring(0, colon).toLowerCase(java.util.Locale.ROOT);
    }

    /**
     * Opens the click URL as a link from a browser would; refused links open nothing. Failures are
     * logged, never thrown.
     */
    static void openUrl(Context context, String url) {
        if (url == null) return;
        String link = clickUrlToOpen(url);
        if (link == null) {
            Log.w(SoilAdsBridge.TAG, "Not opening a click URL that is not a web or app link: " + url);
            return;
        }
        try {
            Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(link).normalizeScheme());
            // Only activities that accept links from a browser (browsers, stores, apps' deep links)
            // may handle it.
            intent.addCategory(Intent.CATEGORY_BROWSABLE);
            intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            context.startActivity(intent);
        } catch (ActivityNotFoundException e) {
            Log.w(SoilAdsBridge.TAG, "No app can open " + link);
        } catch (RuntimeException e) {
            Log.w(SoilAdsBridge.TAG, "Could not open " + link, e);
        }
    }
}
