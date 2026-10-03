package com.flyingacorn.soil.ads;

import android.util.Log;

/**
 * Keeps an unexpected exception in the player from taking the game down. Android calls the
 * player's callbacks (lifecycle, clicks, media and load results) on the game's main thread, where
 * an uncaught exception kills the whole process; every such entry point runs through here instead,
 * logs what failed and carries on. Hard native crashes (a signal in the OS media stack) cannot be
 * caught by any app code.
 */
final class Guard {
    private Guard() {
    }

    /** Runs {@code body}; returns false (after logging) if it threw. */
    static boolean run(String what, Runnable body) {
        try {
            body.run();
            return true;
        } catch (Throwable t) {
            Log.e(SoilAdsBridge.TAG, what + " failed", t);
            return false;
        }
    }

    /** {@code body}, run through {@link #run} whenever it is called. */
    static Runnable wrap(final String what, final Runnable body) {
        return new Runnable() {
            @Override
            public void run() {
                Guard.run(what, body);
            }
        };
    }
}
