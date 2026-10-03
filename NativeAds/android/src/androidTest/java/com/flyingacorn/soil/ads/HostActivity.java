package com.flyingacorn.soil.ads;

import android.app.Activity;
import android.graphics.Color;
import android.os.Bundle;
import android.widget.FrameLayout;

/** Plays the part of Unity's activity: a full-screen "game" view the banner is added on top of. */
public class HostActivity extends Activity {
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        FrameLayout game = new FrameLayout(this);
        game.setBackgroundColor(Color.DKGRAY);
        setContentView(game);
    }
}
