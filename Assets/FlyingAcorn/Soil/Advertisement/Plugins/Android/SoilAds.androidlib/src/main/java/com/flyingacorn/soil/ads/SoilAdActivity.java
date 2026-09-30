package com.flyingacorn.soil.ads;

import android.app.Activity;
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
                session.videoPositionMs = safePosition();
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

    @SuppressWarnings("deprecation")
    private void enterImmersive() {
        getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_FULLSCREEN
                | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
    }

    // ---- Layout ---------------------------------------------------------------------------------

    private View buildLayout() {
        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(Color.BLACK);
        content = new FrameLayout(this);
        root.addView(content, new FrameLayout.LayoutParams(-1, -1));
        root.setOnApplyWindowInsetsListener(new View.OnApplyWindowInsetsListener() {
            @Override
            public WindowInsets onApplyWindowInsets(View view, WindowInsets insets) {
                int[] safe = Ui.cutoutInsets(insets);
                content.setPadding(safe[0], safe[1], safe[2], safe[3]);
                return insets;
            }
        });

        LinearLayout column = new LinearLayout(this);
        column.setOrientation(LinearLayout.VERTICAL);
        content.addView(column, new FrameLayout.LayoutParams(-1, -1));
        column.addView(buildMedia(), new LinearLayout.LayoutParams(-1, 0, 1f));
        View bottomBar = buildBottomBar();
        if (bottomBar != null) column.addView(bottomBar, new LinearLayout.LayoutParams(-1, -2));

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
        if (session.ad.isVideo() && !session.videoFailed) {
            textureView = new TextureView(this);
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
        if (session.ad.image != null) imageView.setImageBitmap(session.ad.image);
        media.addView(imageView, new FrameLayout.LayoutParams(-1, -1));
        updateImageVisibility();
        return media;
    }

    private View buildBottomBar() {
        AdCreative creative = session.ad.creative;
        if (session.ad.logo == null && !creative.hasBottomBarText()) return null;
        LinearLayout bar = new LinearLayout(this);
        bar.setOrientation(LinearLayout.HORIZONTAL);
        bar.setGravity(Gravity.CENTER_VERTICAL);
        bar.setBackgroundColor(0xFF121212);
        int padding = Ui.dp(this, 12);
        bar.setPadding(padding, padding, padding, padding);

        if (session.ad.logo != null) {
            logoView = Ui.roundedImage(this, session.ad.logo, 10);
            LinearLayout.LayoutParams logoParams = new LinearLayout.LayoutParams(Ui.dp(this, 48), Ui.dp(this, 48));
            logoParams.setMarginEnd(padding);
            bar.addView(logoView, logoParams);
        }
        LinearLayout texts = new LinearLayout(this);
        texts.setOrientation(LinearLayout.VERTICAL);
        if (creative.title != null) {
            texts.addView(Ui.naturalText(this, creative.title, 16, 0xFFFFFFFF, true, 1),
                    new LinearLayout.LayoutParams(-1, -2));
        }
        if (creative.description != null) {
            texts.addView(Ui.naturalText(this, creative.description, 13, 0xCCFFFFFF, false, 2),
                    new LinearLayout.LayoutParams(-1, -2));
        }
        bar.addView(texts, new LinearLayout.LayoutParams(0, -2, 1f));
        if (creative.callToAction != null) {
            TextView cta = Ui.ctaButton(this, creative.callToAction, 15);
            cta.setOnClickListener(new View.OnClickListener() {
                @Override
                public void onClick(View v) {
                    onAdClicked();
                }
            });
            LinearLayout.LayoutParams ctaParams = new LinearLayout.LayoutParams(-2, -2);
            ctaParams.setMarginStart(padding);
            bar.addView(cta, ctaParams);
        }
        return bar;
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
        if (player != null && prepared && !session.videoEnded) session.videoPositionMs = safePosition();
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
        if (session == null || session.isEnded() || session.ad.creative.clickUrl == null) return;
        Ui.openUrl(this, session.ad.creative.clickUrl);
        session.reportClicked();
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
            session.videoPositionMs = safePosition();
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
        if (session == null || session.videoFailed || surface == null || player != null) return;
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
                    if (session.videoPositionMs > 0) mp.seekTo(session.videoPositionMs);
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
                    updateImageVisibility();
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

    /** Continues under the image rule, with the image if there is one, else the last frame. */
    private void onVideoFailed(String reason) {
        Log.w(SoilAdsBridge.TAG, "Video playback failed: " + reason);
        if (session != null) session.videoFailed = true;
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

    private void updateImageVisibility() {
        if (imageView == null || session == null) return;
        boolean videoOnScreen = session.ad.isVideo() && !session.videoFailed && !session.videoEnded
                && firstFrameRendered;
        boolean show = session.ad.image != null && (!session.ad.isVideo() || !videoOnScreen);
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
