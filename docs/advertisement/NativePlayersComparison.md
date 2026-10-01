# Native ad players compared with the Unity-drawn ads

Banner, interstitial and rewarded ads moved from Unity-drawn canvases (VideoPlayer + RenderTexture prefabs) to one native player per platform. The `native` format was already rendered by the game and is unchanged.

| Area | Native players (new) | Unity-drawn canvas (old) |
|---|---|---|
| Game during an ad | ✅ Unity paused: game loop, physics, timers and sound stop | ❌ Game kept running and its sound played under the ad |
| Input blocking | ✅ The OS blocks taps to the game | ❌ Blocker canvas that could leak taps or get stuck |
| Layering | ✅ Unaffected by the game's UI, cameras or scene loads | ❌ Sort-order and scene conflicts |
| Performance | ✅ Hardware video straight to screen, Unity stops rendering | ❌ VideoPlayer and RenderTexture copied every frame, more GC |
| Video delivery | ✅ Downloaded once, played from disk | ❌ Streamed from its URL on every play |
| Memory and build size | ✅ Smaller: no prefabs, RenderTexture or TMP fonts | ❌ Larger |
| Notch and system bars | ✅ Handled by the OS, flush, tablet sizes | ❌ Not handled |
| Persian (right-to-left) text and screen readers | ✅ OS rendering, VoiceOver and TalkBack | ❌ Needs RTLTMPro and fonts, no accessibility |
| Video robustness | ✅ Checked at load, image fallback, exact resume, mute | ⚠️ Basic |
| Close-button wait | ✅ Only time on screen, same rule on both platforms, unit-tested | ⚠️ One-second ticks in Unity, similar in practice |
| Events | ✅ AdMob-like order, exactly one close, watchdogs | ⚠️ No ordering guarantee or watchdogs |
| Click safety | ✅ Browsable links only, device-action links blocked | ❌ Any link opened |
| Downloads and cache | ✅ Reuse, retries, self-repair, iCloud-excluded | ⚠️ Images only |
| Dependencies and sanctions | ✅ OS only on device (RTLTMPro only for the Editor placeholder), no regional strings on iOS | ❌ TMP and RTLTMPro at runtime |
| Tests | ✅ Unit, device, end-to-end and minified | ❌ Few |
| Codebases | ❌ C#, Java and Objective-C | ✅ C# only |
| Visual customization | ❌ Native code changes | ✅ Prefabs editable per game |
| Editor preview | ❌ Placeholder only | ✅ Real look |
| Desktop, WebGL, consoles | ❌ No fill for banner, interstitial and rewarded | ✅ Worked |
| API compatibility | ❌ Placement prefabs and types removed | ✅ No change needed |
| Android pause callbacks | ❌ Every fullscreen ad fires `OnApplicationPause` | ✅ No pause |
| iOS while backgrounded during an ad | ❌ Game misses `OnApplicationPause`; ad sound follows the silent switch | ✅ Normal |
| Game reacting during an ad | ❌ Rewarded and Closed arrive only at close | ✅ Live |
| Banner layering | ❌ Always on top of the game | ✅ Part of the Unity layout |
| Rendering paths | ❌ Two: native players plus the Unity-drawn `native` format | ✅ One |
| Device differences | ⚠️ Decoders and OEMs vary | ⚠️ Unity's VideoPlayer varies too |
| Build pipeline | ⚠️ Keep rule and C11 compatibility needed | ✅ Plain Unity |
| Debugging | ⚠️ Native logs in logcat and Xcode | ✅ Unity console |
| `native` ad format | Same: the SDK hands over content and the game draws it | Same |
| Offline start, reward on process death | Same | Same |
