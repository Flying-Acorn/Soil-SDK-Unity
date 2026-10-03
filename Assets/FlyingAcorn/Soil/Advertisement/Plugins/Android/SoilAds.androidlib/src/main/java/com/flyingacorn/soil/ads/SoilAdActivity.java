package com.flyingacorn.soil.ads;

import android.app.Activity;
import android.graphics.Bitmap;
import android.graphics.Color;
import android.graphics.Matrix;
import android.graphics.SurfaceTexture;
import android.media.MediaPlayer;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.util.Log;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.Surface;
import android.view.TextureView;
import android.view.View;
import android.view.WindowInsets;
import android.view.WindowManager;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.window.OnBackInvokedCallback;
import android.window.OnBackInvokedDispatcher;

/**
 * Fullscreen interstitial / rewarded player ("Fullscreen presentation" in PROTOCOL.md). Views are
 * built in code; the show itself lives in a {@link FullscreenSession}.
 */
public final class SoilAdActivity extends Activity implements TextureView.SurfaceTextureListener {
    static final String EXTRA_SESSION_ID = "com.flyingacorn.soil.ads.SESSION_ID";
    private static final long TICK_MS = 100;
    private static final String CLOSE_GLYPH = "✕";
    private static final String SOUND_ON = "🔊";
    private static final String SOUND_OFF = "🔇";

    private final Handler handler = new Handler(Looper.getMainLooper());
    private final Runnable ticker = new Runnable() {
        @Override
        public void run() {
            tick();
            if (resumed) handler.postDelayed(this, TICK_MS);
        }
    };

    private FullscreenSession session;
    private FrameLayout content;
    private TextureView textureView;
    private ImageView imageView;
    private ImageView logoView;
    private ImageView backdropView;
    private TextView closeButton;
    private TextView muteButton;

    private MediaPlayer player;
    private Surface surface;
    private boolean prepared;
    private boolean firstFrameRendered;
    private int videoWidth;
    private int videoHeight;

    private boolean resumed;
    private long resumedAtMs;
    private Object backCallback;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        session = FullscreenSession.find(getIntent().getIntExtra(EXTRA_SESSION_ID, -1));
        if (session == null) {
            // E.g. the process was recreated: the show this Activity belonged to no longer exists.
            Log.w(SoilAdsBridge.TAG, "No ad to show; closing the ad activity");
            finish();
            return;
        }
        session.attach(this);
        if (Build.VERSION.SDK_INT >= 28) {
            WindowManager.LayoutParams attributes = getWindow().getAttributes();
            attributes.layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
            getWindow().setAttributes(attributes);
        }
        setContentView(buildLayout());
        enterImmersive();
        registerBackCallback();
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (session == null) return;
        resumed = true;
        resumedAtMs = SystemClock.elapsedRealtime();
        enterImmersive();
        session.reportShown();
        startPlaybackIfReady();
        handler.removeCallbacks(ticker);
        handler.post(ticker);
    }

    @Override
    protected void onPause() {
        if (session != null && resumed) {
            session.visibleMs += SystemClock.elapsedRealtime() - resumedAtMs;
            resumed = false;
            handler.removeCallbacks(ticker);
            if (player != null && prepared) {
                session.recordVideoPosition(safePosition());
                if (player.isPlaying()) player.pause();
            }
        }
        super.onPause();
    }

    @Override
    protected void onDestroy() {
        handler.removeCallbacksAndMessages(null);
        releasePlayer();
        if (surface != null) {
            surface.release();
            surface = null;
        }
        if (imageView != null) imageView.setImageDrawable(null);
        if (logoView != null) logoView.setImageDrawable(null);
        if (backdropView != null) backdropView.setImageDrawable(null);
        unregisterBackCallback();
        FullscreenSession current = session;
        session = null;
        if (current != null) {
            // A configuration change hands the show to the next instance; anything else ends it.
            if (!isChangingConfigurations()) current.end();
            current.detach(this);
        }
        super.onDestroy();
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) enterImmersive();
    }

    @SuppressWarnings("deprecation")
    @Override
    public void onBackPressed() {
        onBackRequested(); // API < 33; newer versions go through the OnBackInvokedCallback
    }

    private void onBackRequested() {
        if (session != null && session.lock.isUnlocked()) session.end();
    }

    private void registerBackCallback() {
        if (Build.VERSION.SDK_INT < 33) return;
        OnBackInvokedCallback callback = new OnBackInvokedCallback() {
            @Override
            public void onBackInvoked() {
                onBackRequested();
            }
        };
        getOnBackInvokedDispatcher().registerOnBackInvokedCallback(OnBackInvokedDispatcher.PRIORITY_DEFAULT, callback);
        backCallback = callback;
    }

    private void unregisterBackCallback() {
        if (Build.VERSION.SDK_INT < 33 || backCallback == null) return;
        getOnBackInvokedDispatcher().unregisterOnBackInvokedCallback((OnBackInvokedCallback) backCallback);
        backCallback = null;
    }

    /**
     * No {@code SYSTEM_UI_FLAG_LAYOUT_STABLE}: before API 30 it would report the bars' insets even
     * while they are hidden, and the content is padded by the bars that are actually visible.
     */
    @SuppressWarnings("deprecation")
    private void enterImmersive() {
        getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_FULLSCREEN
                | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
    }

    // ---- Layout ---------------------------------------------------------------------------------

    private View buildLayout() {
        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(Color.BLACK);
        if (session.ad.backdrop != null) {
            // Edge to edge, under the cutout and bars too; see Backdrop.
            backdropView = new ImageView(this);
            backdropView.setScaleType(ImageView.ScaleType.CENTER_CROP);
            backdropView.setImageBitmap(session.ad.backdrop);
            backdropView.setContentDescription("soil_ad_backdrop");
            backdropView.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            root.addView(backdropView, new FrameLayout.LayoutParams(-1, -1));
        }
        content = new FrameLayout(this);
        root.addView(content, new FrameLayout.LayoutParams(-1, -1));
        root.setOnApplyWindowInsetsListener(new View.OnApplyWindowInsetsListener() {
            @Override
            public WindowInsets onApplyWindowInsets(View view, WindowInsets insets) {
                // Cutout plus visible bars: immersive hides them, but split screen, freeform
                // windows or a transient reveal can leave them over the ad's controls.
                int[] safe = Ui.safeInsets(insets);
                content.setPadding(safe[0], safe[1], safe[2], safe[3]);
                return insets;
            }
        });

        LinearLayout column = new LinearLayout(this);
        column.setOrientation(LinearLayout.VERTICAL);
        content.addView(column, new FrameLayout.LayoutParams(-1, -1));
        column.addView(buildMedia(), new LinearLayout.LayoutParams(-1, 0, 1f));
        View infoCard = buildInfoCard();
        if (infoCard != null) {
            int cardMargin = Ui.dp(this, 12);
            LinearLayout.LayoutParams cardParams = new LinearLayout.LayoutParams(-1, -2);
            cardParams.gravity = Gravity.CENTER_HORIZONTAL;
            cardParams.setMargins(cardMargin, cardMargin, cardMargin, cardMargin);
            column.addView(infoCard, cardParams);
        }

        int margin = Ui.dp(this, 12);
        LinearLayout topLeft = new LinearLayout(this);
        topLeft.setOrientation(LinearLayout.HORIZONTAL);
        topLeft.setGravity(Gravity.CENTER_VERTICAL);
        topLeft.addView(Ui.badge(this, 12));
        if (session.ad.isVideo()) {
            muteButton = roundButton(SOUND_ON, 16);
            muteButton.setContentDescription("soil_ad_mute");
            muteButton.setOnClickListener(new View.OnClickListener() {
                @Override
                public void onClick(View v) {
                    session.muted = !session.muted;
                    applyVolume();
                }
            });
            LinearLayout.LayoutParams muteParams = new LinearLayout.LayoutParams(Ui.dp(this, 36), Ui.dp(this, 36));
            muteParams.leftMargin = Ui.dp(this, 8);
            topLeft.addView(muteButton, muteParams);
            applyVolume();
        }
        FrameLayout.LayoutParams topLeftParams = new FrameLayout.LayoutParams(-2, -2, Gravity.TOP | Gravity.LEFT);
        topLeftParams.setMargins(margin, margin, margin, margin);
        content.addView(topLeft, topLeftParams);

        closeButton = roundButton("", 16);
        closeButton.setContentDescription("soil_ad_close");
        closeButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                if (session != null && session.lock.isUnlocked()) session.end();
            }
        });
        FrameLayout.LayoutParams closeParams = new FrameLayout.LayoutParams(Ui.dp(this, 36), Ui.dp(this, 36),
                Gravity.TOP | Gravity.RIGHT);
        closeParams.setMargins(margin, margin, margin, margin);
        content.addView(closeButton, closeParams);
        updateCloseButton();
        return root;
    }

    private View buildMedia() {
        FrameLayout media = new FrameLayout(this);
        media.setContentDescription("soil_ad_media");
        media.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                onAdClicked();
            }
        });
        // An ended video is never reopened: its last frame (or the image) stays on screen.
        if (session.ad.isVideo() && !session.videoFailed && !session.videoEnded) {
            textureView = new TextureView(this);
            textureView.setOpaque(false); // the backdrop shows around an aspect-fit video
            textureView.setSurfaceTextureListener(this);
            textureView.addOnLayoutChangeListener(new View.OnLayoutChangeListener() {
                @Override
                public void onLayoutChange(View v, int l, int t, int r, int b, int ol, int ot, int or, int ob) {
                    applyVideoTransform();
                }
            });
            media.addView(textureView, new FrameLayout.LayoutParams(-1, -1));
        }
        imageView = new ImageView(this);
        imageView.setScaleType(ImageView.ScaleType.FIT_CENTER);
        if (stillImage() != null) imageView.setImageBitmap(stillImage());
        media.addView(imageView, new FrameLayout.LayoutParams(-1, -1));
        updateImageVisibility();
        return media;
    }

    /**
     * A rounded card: logo, title and description in a row that follows the text's direction (the
     * logo on the right for Persian), and the call to action as a full-width button under it.
     * Null when the ad has none of them.
     */
    private View buildInfoCard() {
        AdCreative creative = session.ad.creative;
        boolean hasText = creative.title != null || creative.description != null;
        boolean hasHeader = hasText || session.ad.logo != null;
        if (!hasHeader && creative.callToAction == null) return null;
        boolean rtl = Ui.creativeIsRightToLeft(creative);

        LinearLayout card = new MaxWidthLinearLayout(this, Ui.dp(this, 520));
        card.setOrientation(LinearLayout.VERTICAL);
        card.setBackground(Ui.rounded(0xF01C1C1F, Ui.dp(this, 18)));
        int padding = Ui.dp(this, 16);
        card.setPadding(padding, padding, padding, padding);
        card.setContentDescription("soil_ad_info");

        if (hasHeader) {
            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setGravity(Gravity.CENTER_VERTICAL);
            View logo = null;
            if (session.ad.logo != null) {
                logoView = Ui.roundedImage(this, session.ad.logo, 12);
                logo = logoView;
            }
            LinearLayout texts = null;
            if (hasText) {
                texts = new LinearLayout(this);
                texts.setOrientation(LinearLayout.VERTICAL);
                if (creative.title != null) {
                    texts.addView(Ui.naturalText(this, creative.title, 18, 0xFFFFFFFF, true, 2),
                            new LinearLayout.LayoutParams(-1, -2));
                }
                if (creative.description != null) {
                    LinearLayout.LayoutParams bodyParams = new LinearLayout.LayoutParams(-1, -2);
                    if (creative.title != null) bodyParams.topMargin = Ui.dp(this, 4);
                    texts.addView(Ui.naturalText(this, creative.description, 14, 0xB8FFFFFF, false, 3), bodyParams);
                }
            }
            int logoSide = Ui.dp(this, 56);
            Ui.addInReadingOrder(row, rtl, Ui.dp(this, 12), new View[]{logo, texts},
                    new LinearLayout.LayoutParams[]{
                            new LinearLayout.LayoutParams(logoSide, logoSide),
                            new LinearLayout.LayoutParams(0, -2, 1f)});
            card.addView(row, new LinearLayout.LayoutParams(-1, -2));
        }
        if (creative.callToAction != null) {
            TextView cta = Ui.ctaButton(this, creative.callToAction, 17);
            cta.setMinHeight(Ui.dp(this, 50));
            cta.setOnClickListener(new View.OnClickListener() {
                @Override
                public void onClick(View v) {
                    onAdClicked();
                }
            });
            LinearLayout.LayoutParams ctaParams = new LinearLayout.LayoutParams(-1, -2);
            if (hasHeader) ctaParams.topMargin = Ui.dp(this, 14);
            card.addView(cta, ctaParams);
        }
        return card;
    }

    /** A LinearLayout no wider than {@code maxWidthPx}: a readable card on tablets and in landscape. */
    private static final class MaxWidthLinearLayout extends LinearLayout {
        private final int maxWidthPx;

        MaxWidthLinearLayout(android.content.Context context, int maxWidthPx) {
            super(context);
            this.maxWidthPx = maxWidthPx;
        }

        @Override
        protected void onMeasure(int widthMeasureSpec, int heightMeasureSpec) {
            int width = MeasureSpec.getSize(widthMeasureSpec);
            if (width > maxWidthPx) widthMeasureSpec = MeasureSpec.makeMeasureSpec(maxWidthPx, MeasureSpec.EXACTLY);
            super.onMeasure(widthMeasureSpec, heightMeasureSpec);
        }
    }

    private TextView roundButton(String text, float sp) {
        TextView button = new TextView(this);
        button.setText(text);
        button.setTextSize(TypedValue.COMPLEX_UNIT_SP, sp);
        button.setTextColor(Color.WHITE);
        button.setGravity(Gravity.CENTER);
        button.setBackground(Ui.circle(0x99000000));
        return button;
    }

    // ---- Lock, clicks -----------------------------------------------------------------------------

    private void tick() {
        if (session == null || session.isEnded()) return;
        if (player != null && prepared && !session.videoEnded) session.recordVideoPosition(safePosition());
        double visibleSeconds = (session.visibleMs
                + (resumed ? SystemClock.elapsedRealtime() - resumedAtMs : 0)) / 1000.0;
        boolean justUnlocked = session.lock.update(visibleSeconds, session.videoPositionMs / 1000.0,
                session.videoEnded, session.videoFailed);
        updateCloseButton();
        if (justUnlocked) session.reportUnlocked();
    }

    private void updateCloseButton() {
        if (closeButton == null || session == null) return;
        closeButton.setText(session.lock.isUnlocked()
                ? CLOSE_GLYPH : String.valueOf(Math.max(1, session.lock.secondsRemaining())));
    }

    private void onAdClicked() {
        if (session == null || session.isEnded()) return;
        // Reported even without a URL or an app to open it, as the SDK always did.
        session.reportClicked();
        Ui.openUrl(this, session.ad.creative.clickUrl);
    }

    // ---- Video ------------------------------------------------------------------------------------

    @Override
    public void onSurfaceTextureAvailable(SurfaceTexture texture, int width, int height) {
        surface = new Surface(texture);
        openPlayer();
    }

    @Override
    public void onSurfaceTextureSizeChanged(SurfaceTexture texture, int width, int height) {
        applyVideoTransform();
    }

    @Override
    public boolean onSurfaceTextureDestroyed(SurfaceTexture texture) {
        if (player != null && prepared && session != null && !session.videoEnded) {
            session.recordVideoPosition(safePosition());
        }
        releasePlayer();
        if (surface != null) {
            surface.release();
            surface = null;
        }
        return true;
    }

    @Override
    public void onSurfaceTextureUpdated(SurfaceTexture texture) {
        if (!firstFrameRendered) {
            firstFrameRendered = true;
            updateImageVisibility();
        }
    }

    private void openPlayer() {
        if (session == null || session.videoFailed || session.videoEnded || surface == null || player != null) return;
        MediaPlayer mediaPlayer = new MediaPlayer();
        player = mediaPlayer;
        prepared = false;
        try {
            mediaPlayer.setDataSource(session.ad.creative.videoPath);
            mediaPlayer.setSurface(surface);
            mediaPlayer.setOnPreparedListener(new MediaPlayer.OnPreparedListener() {
                @Override
                public void onPrepared(MediaPlayer mp) {
                    if (mp != player) return;
                    prepared = true;
                    applyVolume();
                    if (session.videoPositionMs > 0) seekTo(mp, session.videoPositionMs);
                    startPlaybackIfReady();
                }
            });
            mediaPlayer.setOnVideoSizeChangedListener(new MediaPlayer.OnVideoSizeChangedListener() {
                @Override
                public void onVideoSizeChanged(MediaPlayer mp, int width, int height) {
                    videoWidth = width;
                    videoHeight = height;
                    applyVideoTransform();
                }
            });
            mediaPlayer.setOnCompletionListener(new MediaPlayer.OnCompletionListener() {
                @Override
                public void onCompletion(MediaPlayer mp) {
                    if (mp != player || session == null) return;
                    session.videoEnded = true;
                    keepLastFrame();
                    updateImageVisibility();
                    releasePlayer(); // frees the decoder; the ended video is not played again
                    tick();
                }
            });
            mediaPlayer.setOnErrorListener(new MediaPlayer.OnErrorListener() {
                @Override
                public boolean onError(MediaPlayer mp, int what, int extra) {
                    if (mp == player) onVideoFailed("MediaPlayer error " + what + "/" + extra);
                    return true;
                }
            });
            mediaPlayer.prepareAsync();
        } catch (Exception e) {
            onVideoFailed(String.valueOf(e));
        }
    }

    private void startPlaybackIfReady() {
        if (player == null || !prepared || !resumed || session == null || session.videoEnded) return;
        try {
            if (!player.isPlaying()) player.start();
        } catch (IllegalStateException e) {
            onVideoFailed(String.valueOf(e));
        }
    }

    /** Resumes where the viewer left off, not at the key frame before it (API 26+). */
    private static void seekTo(MediaPlayer mp, int positionMs) {
        if (Build.VERSION.SDK_INT >= 26) mp.seekTo(positionMs, MediaPlayer.SEEK_CLOSEST);
        else mp.seekTo(positionMs);
    }

    /** Continues under the image rule, with the image if there is one, else the last frame. */
    private void onVideoFailed(String reason) {
        Log.w(SoilAdsBridge.TAG, "Video playback failed: " + reason);
        if (session != null) session.videoFailed = true;
        keepLastFrame();
        releasePlayer();
        if (muteButton != null) muteButton.setVisibility(View.GONE);
        updateImageVisibility();
        tick();
    }

    private void releasePlayer() {
        MediaPlayer mediaPlayer = player;
        player = null;
        prepared = false;
        if (mediaPlayer == null) return;
        try {
            mediaPlayer.release();
        } catch (RuntimeException e) {
            Log.w(SoilAdsBridge.TAG, "MediaPlayer release failed", e);
        }
    }

    private int safePosition() {
        try {
            return player.getCurrentPosition();
        } catch (RuntimeException e) {
            return session.videoPositionMs;
        }
    }

    private void applyVolume() {
        if (session == null) return;
        float volume = session.muted ? 0f : 1f;
        if (player != null && prepared) player.setVolume(volume, volume);
        if (muteButton != null) muteButton.setText(session.muted ? SOUND_OFF : SOUND_ON);
    }

    /** What the image view shows: the creative's image, else the captured last video frame. */
    private Bitmap stillImage() {
        if (session == null) return null;
        return session.ad.image != null ? session.ad.image : session.lastFrame;
    }

    /**
     * Copies the frame on screen into the session when there is no image to stand in for the
     * video, so it survives the player being released and the TextureView losing its surface
     * (backgrounding, recreation). The copy is taken at the video's aspect-fit size: a TextureView
     * bitmap is the raw frame stretched to the requested size, without the view's transform.
     */
    private void keepLastFrame() {
        if (session == null || session.ad.image != null || session.lastFrame != null) return;
        if (textureView == null || !firstFrameRendered || !textureView.isAvailable()) return;
        int width = textureView.getWidth();
        int height = textureView.getHeight();
        if (width == 0 || height == 0 || videoWidth == 0 || videoHeight == 0) return;
        float scale = Math.min(1f, Math.min((float) width / videoWidth, (float) height / videoHeight));
        try {
            Bitmap frame = textureView.getBitmap(Math.max(1, Math.round(videoWidth * scale)),
                    Math.max(1, Math.round(videoHeight * scale)));
            if (frame == null) return;
            session.lastFrame = frame;
            if (imageView != null) imageView.setImageBitmap(frame);
        } catch (RuntimeException | OutOfMemoryError e) {
            Log.w(SoilAdsBridge.TAG, "Could not keep the video's last frame: " + e);
        }
    }

    private void updateImageVisibility() {
        if (imageView == null || session == null) return;
        boolean videoOnScreen = session.ad.isVideo() && !session.videoFailed && !session.videoEnded
                && firstFrameRendered;
        boolean show = stillImage() != null && (!session.ad.isVideo() || !videoOnScreen);
        imageView.setVisibility(show ? View.VISIBLE : View.GONE);
        // Once the image stands in for an ended or failed video, the last video frame must not
        // show around it (an aspect-fit image rarely covers the whole area).
        if (textureView != null && firstFrameRendered) {
            textureView.setVisibility(show ? View.INVISIBLE : View.VISIBLE);
        }
    }

    /** Aspect-fit: a TextureView stretches its content, so scale it back around the center. */
    private void applyVideoTransform() {
        if (textureView == null) return;
        int width = textureView.getWidth();
        int height = textureView.getHeight();
        if (width == 0 || height == 0 || videoWidth == 0 || videoHeight == 0) return;
        float scale = Math.min((float) width / videoWidth, (float) height / videoHeight);
        Matrix matrix = new Matrix();
        matrix.setScale(videoWidth * scale / width, videoHeight * scale / height, width / 2f, height / 2f);
        textureView.setTransform(matrix);
    }
}
