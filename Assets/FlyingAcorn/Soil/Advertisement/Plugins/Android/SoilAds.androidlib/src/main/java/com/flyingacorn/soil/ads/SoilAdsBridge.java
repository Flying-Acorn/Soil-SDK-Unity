package com.flyingacorn.soil.ads;

import android.app.Activity;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

import java.lang.ref.WeakReference;
import java.lang.reflect.Field;
import java.lang.reflect.Method;

/**
 * Entry points called from C# ("Calls: Unity → native" in PROTOCOL.md). Calls may come from any
 * thread; each one is posted to the main thread and never throws back into Unity. Results go back
 * as JSON through {@code UnityPlayer.UnitySendMessage}.
 */
public final class SoilAdsBridge {
    static final String TAG = "SoilAds";

    private static final Handler main = new Handler(Looper.getMainLooper());
    private static final UnitySink unitySink = new UnitySink();
    private static volatile EventSink sink = unitySink;
    private static volatile SoilAdsManager manager;
    private static WeakReference<Activity> hostActivity = new WeakReference<>(null);

    private SoilAdsBridge() {
    }

    public static void initialize(final Activity activity, String receiverObject, String receiverMethod) {
        unitySink.setReceiver(receiverObject, receiverMethod);
        post(new Runnable() {
            @Override
            public void run() {
                if (activity != null) hostActivity = new WeakReference<>(activity);
                manager();
            }
        });
    }

    public static void load(final String format, final String creativeJson) {
        post(new Runnable() {
            @Override
            public void run() {
                manager().load(format, creativeJson);
            }
        });
    }

    public static void show(final String format, final String optionsJson) {
        post(new Runnable() {
            @Override
            public void run() {
                manager().show(format, optionsJson);
            }
        });
    }

    public static void hide(final String format) {
        post(new Runnable() {
            @Override
            public void run() {
                manager().hide(format);
            }
        });
    }

    public static void destroy(final String format) {
        post(new Runnable() {
            @Override
            public void run() {
                manager().destroy(format);
            }
        });
    }

    public static boolean isReady(String format) {
        SoilAdsManager current = manager;
        return current != null && current.isReady(format);
    }

    /** Tests capture events here instead of Unity; null restores Unity. */
    static void setEventSinkForTests(EventSink testSink) {
        sink = testSink == null ? unitySink : testSink;
    }

    private static SoilAdsManager manager() {
        if (manager == null) {
            AndroidPresenter presenter = new AndroidPresenter(main, new AndroidPresenter.HostProvider() {
                @Override
                public Activity host() {
                    // Unity's activity can be recreated after initialize: a finished one gives way
                    // to Unity's current activity instead of blocking every show until GC.
                    Activity activity = hostActivity.get();
                    boolean usable = activity != null && !activity.isFinishing() && !activity.isDestroyed();
                    return usable ? activity : unityCurrentActivity();
                }
            });
            manager = new SoilAdsManager(new MediaLoader(main), presenter, new EventSink() {
                @Override
                public void send(String json) {
                    sink.send(json);
                }
            });
        }
        return manager;
    }

    private static void post(final Runnable call) {
        try {
            main.post(new Runnable() {
                @Override
                public void run() {
                    try {
                        call.run();
                    } catch (Throwable t) {
                        Log.e(TAG, "Ad call failed", t);
                    }
                }
            });
        } catch (Throwable t) {
            Log.e(TAG, "Could not schedule ad call", t);
        }
    }

    /** Fallback when initialize was given no Activity (or it went away): Unity's own current one. */
    private static Activity unityCurrentActivity() {
        try {
            Field field = Class.forName("com.unity3d.player.UnityPlayer").getField("currentActivity");
            return (Activity) field.get(null);
        } catch (Throwable t) {
            return null;
        }
    }

    /** Sends to Unity through reflection so this library compiles without Unity's classes. */
    private static final class UnitySink implements EventSink {
        private volatile String receiverObject;
        private volatile String receiverMethod;
        private Method sendMessage;

        void setReceiver(String receiverObject, String receiverMethod) {
            this.receiverObject = receiverObject;
            this.receiverMethod = receiverMethod;
        }

        @Override
        public synchronized void send(String json) {
            String object = receiverObject;
            String method = receiverMethod;
            if (object == null || method == null) {
                Log.d(TAG, "No receiver yet, dropping " + json);
                return;
            }
            try {
                if (sendMessage == null) {
                    sendMessage = Class.forName("com.unity3d.player.UnityPlayer")
                            .getMethod("UnitySendMessage", String.class, String.class, String.class);
                }
                sendMessage.invoke(null, object, method, json);
            } catch (Throwable t) {
                Log.w(TAG, "Could not send " + json + " to Unity: " + t);
            }
        }
    }
}
