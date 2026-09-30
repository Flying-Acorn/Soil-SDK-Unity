package com.flyingacorn.soil.ads;

/** Where event JSON goes: Unity in the game, a list in tests. */
interface EventSink {
    void send(String json);
}
