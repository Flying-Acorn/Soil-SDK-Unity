# Soil native ad players: protocol

The Unity side (C#) owns **decisions**: which ad group and ad to show, downloading and caching
its files, frequency capping, cooldowns, public events. The native side owns **presentation**:
banner, interstitial and rewarded ads are drawn by one small native player per platform.
The `native` ad format is rendered by the game in its own Unity UI and never reaches these players.

The native players make **no network requests** and depend on **nothing but the OS**:

| | Android | iOS |
|---|---|---|
| Language | Java 8, `android.*` only (no AndroidX, no Google Play services, no Kotlin) | Objective-C, system frameworks only (UIKit, AVFoundation) |
| Video | `MediaPlayer` on a `TextureView` | `AVPlayer` + `AVPlayerLayer` |
| Min OS | API 22 (Unity 2022.3 minimum); compiled against API 33+ (Target API Level Automatic or 33+) | iOS 12 (Unity 2022.3 minimum) |
| Ships as | `Plugins/Android/SoilAds.androidlib` (sources) | `Plugins/iOS/SoilAds/*.h/*.m` (sources) |

Every file is compiled from source by the game's own build. Nothing is downloaded at build time
or at run time, so the players work the same from every country and every store.

**iOS binary hygiene:** no symbol, string, comment-derived resource or file name in the iOS sources
may mention a country, a regional store, a regional ad network or a regional domain. Keep all
strings neutral (`SoilAds…`).

## Formats

`banner`, `interstitial`, `rewarded` (lowercase strings, identical to C# `AdFormat` names).
Any other format string is rejected with `loadFailed`/`showFailed` and error `invalid_format`.

## Calls: Unity → native

All calls are fire-and-forget, may arrive on any thread, and are marshalled by the player onto
its UI/main thread. Results come back only as events.

| Operation | Android (`com.flyingacorn.soil.ads.SoilAdsBridge`, static) | iOS (C functions, `__Internal`) |
|---|---|---|
| Initialize | `initialize(Activity activity, String receiverObject, String receiverMethod)` | `void SoilAds_Initialize(const char* receiverObject, const char* receiverMethod)` |
| Load | `load(String format, String creativeJson)` | `void SoilAds_Load(const char* format, const char* creativeJson)` |
| Show | `show(String format, String optionsJson)` | `void SoilAds_Show(const char* format, const char* optionsJson)` |
| Hide | `hide(String format)` | `void SoilAds_Hide(const char* format)` |
| Destroy | `destroy(String format)` | `void SoilAds_Destroy(const char* format)` |
| Is ready | `boolean isReady(String format)` | `bool SoilAds_IsReady(const char* format)` |
| Shown received | — | `void SoilAds_ShownReceived(const char* format)`: C# got a fullscreen `shown`, so the game can be paused |

`initialize` may be called more than once; the last receiver wins. Calls made before
`initialize` still work; their events are dropped until a receiver exists.

## Events: native → Unity

One channel. Android: `UnityPlayer.UnitySendMessage(receiverObject, receiverMethod, json)`
(called through reflection so the player compiles without Unity's classes).
iOS: `UnitySendMessage(receiverObject, receiverMethod, json)`.

```json
{"format":"rewarded","event":"loaded","media":"video","durationMs":15000}
```

| `event` | Extra fields | Meaning |
|---|---|---|
| `loaded` | `media` = `video` \| `image` \| `text`, `durationMs` (video only, else 0) | Creative decoded and ready; slot filled. `media` is what will actually play (a broken video with a usable image reports `image`). |
| `loadFailed` | `error`, `message` | Slot left empty. |
| `shown` | — | Ad is visible on screen (banner: view attached; fullscreen: presentation finished). |
| `showFailed` | `error`, `message` | Nothing was shown: the ad never reached the screen. The slot keeps its ad (fullscreen too), unless it was reloaded or destroyed meanwhile. |
| `clicked` | — | User tapped the call to action or media. Sent whether or not a link was opened (see *Click links*). |
| `rewarded` | — | Rewarded only. Sent once per show, always before `closed`. |
| `closed` | — | Ad left the screen (user close, back button, `hide`, `destroy`, or the system finishing the ad, e.g. Android relaunching a `singleTask` game from the launcher). Sent exactly once per `shown`. A reward granted at unlock stays granted; it is not persisted across process death, as with ordinary ad networks. |

Error codes (`error`): `invalid_format`, `invalid_creative`, `media_unreadable`, `not_loaded`,
`already_showing`, `no_host` (no Activity / no root view controller), `internal`.
The Unity side adds `no_fill`, `network` and `timeout` (see *Unity-side watchdogs*).

Events for one show are always ordered: `shown` → (`clicked`)* → (`rewarded`)? → `closed`.

## Slots

Each format has one **slot** holding at most one loaded creative.

- `load` replaces whatever is in the slot (the previous slot content is released; an ad that is
  on screen is not affected). A second `load` while a first one is still decoding cancels the
  first; only the last one reports `loaded`/`loadFailed`.
- Fullscreen `show` **consumes** the slot when the show is accepted: from then on `isReady` is false
  until the next `load`. If the show ends in `showFailed` (it never reached the screen), the ad goes
  back into the slot.
- Banner `show` does **not** consume the slot: the banner can be hidden and shown again.
  Showing a banner that is already visible moves it to the new position and sends nothing. If the
  banner view was removed by something else, the next `show` first sends `closed` for it, then
  `shown` for the new view.
- `hide(banner)` removes the banner view (`closed`). `hide(interstitial|rewarded)` dismisses a
  fullscreen ad that is on screen (`closed`, no `rewarded` unless already granted).
- `destroy` = `hide` + empty the slot.
- A fullscreen `hide`/`destroy` that arrives before the presentation has started (iOS: waiting for
  the app to become active) cancels it with `showFailed`/`internal`, and nothing appears. Once the
  presentation has started, `hide` still gives `shown` then `closed`.
- Only one fullscreen ad (interstitial or rewarded) can be on screen at a time; a second `show`
  gets `showFailed`/`already_showing`.

## Creative JSON (`load`)

Built by C#. Paths are absolute local file paths that already exist on disk.

```json
{
  "adId": "3f2c…",
  "videoPath": "/data/user/0/…/SoilAssets/rewarded_video_x.mp4",
  "imagePath": "/data/user/0/…/SoilAssets/rewarded_image_y.jpg",
  "logoPath": "/data/user/0/…/SoilAssets/rewarded_logo_z.png",
  "title": "Word Master",
  "description": "Train your brain every day",
  "callToAction": "Install",
  "clickUrl": "https://example.com/click?x=1"
}
```

Every field except `adId` may be missing, `null` or empty.

Validation on `load`:
- `videoPath` (fullscreen only; ignored for banners): readable and decodable with a video track →
  `media: video`. If it fails and an image is usable → `media: image`. The player keeps the image
  either way as the video's poster / fallback.
- else `imagePath` decodable → `media: image`.
- else banner with a non-empty `title` → `media: text`.
- else `loadFailed` / `invalid_creative` (no media) or `media_unreadable` (media given but broken).
- `logoPath` is optional decoration; a broken logo is ignored, never a failure.
- Images are decoded off the main thread and downsampled to the screen size. Each format decodes
  on its own background thread, so a slow video check never delays a banner.
- Android's video check decodes one frame. Only one format checks a video at a time, because a phone
  with few video decoders cannot decode two at once and the loser would wrongly fall back to its
  image; a check that gets no frame tries again a few times. If it still gets none while another
  fullscreen ad is on screen (its video holding the decoder) but the file has a video track and a
  duration, the video counts as usable: a video that then fails to play shows the image instead.
  iOS checks the asset's playable flag, video track and duration without decoding a frame, so
  it is not affected.

## Show options JSON (`show`)

Banner:
```json
{"position": "bottom"}
```
`position`: `bottom` (default) | `top` | `center`.

Fullscreen (interstitial and rewarded; C# always sends every field):
```json
{
  "imageLockSeconds": 5,
  "videoLockFraction": 0.8,
  "minVideoLockSeconds": 5,
  "maxLockSeconds": 15,
  "startMuted": false
}
```
Defaults C# sends: interstitial `5 / 0.8 / 5 / max 15`, rewarded `20 / 1.0 / 0 / max 0` (no cap).
A missing field takes the format's default.

## Fullscreen lock policy (identical on all platforms, unit-tested on all)

The close button is **locked** for a while, then unlocks. Rewarded ads grant the reward at the
moment they unlock. The timing is the one the Unity-drawn ads always had - interstitial: 5 s, or
80% of a video but never under 5 s; rewarded: 20 s, or the whole video - except that only time
the ad is on screen counts, and an interstitial is always closable by 15 s (`maxLockSeconds`):
Google Play does not allow interstitials that cannot be closed after 15 s. Rewarded ads are
opt-in and have no cap.

Inputs: media (`video`/`image`), video duration `D` seconds, `visibleSeconds` (time the ad has been
on screen while the app was in the foreground — does not advance while backgrounded or while the
user is away after a click), `positionSeconds` (video playback position), `videoEnded`,
`videoFailed`.

```
cap(x)     = maxLockSeconds > 0 ? min(x, maxLockSeconds) : x
videoLock  = cap(max(minVideoLockSeconds, videoLockFraction * D))   // may outlast a short video
screenLock = cap(media == video ? max(videoLock, imageLockSeconds) : imageLockSeconds)
playing    = media == video && !videoFailed && D > 0
progress   = videoEnded ? max(D, visibleSeconds) : positionSeconds

unlocked = visibleSeconds >= screenLock                  // the countdown; also the net under a stalled/failed video
        || (playing && progress >= videoLock)             // watched far enough (rewarded: to the end)

secondsRemaining = max(0, ceil(min(screenLock - visibleSeconds,
                                   playing ? videoLock - progress : infinity)))
```

| | Image | Video of D seconds |
|---|---|---|
| Interstitial | 5 s | 0.8·D on screen or of playback, at least 5 s and at most 15 s (a 3 s video: 5 s; a 30 s video: 15 s) |
| Rewarded | 20 s | end of the video; a stalled video: max(20, D) s on screen |

There is no other cap. Once unlocked it stays unlocked; `videoEnded` and `videoFailed` are sticky.
`rewarded` is sent once, on the first unlock of a rewarded ad. A video that fails mid-play shows the
fallback image (or keeps the last frame) and unlocks on the countdown, measured from when the ad
was first shown.

## Fullscreen presentation

- Android: a dedicated `SoilAdActivity` (declared in the androidlib manifest, framework theme
  `@android:style/Theme.Black.NoTitleBar.Fullscreen`, `configChanges` covering orientation/size so
  it is not recreated, `screenOrientation="behind"`). Unity's activity pauses underneath, which
  pauses the game loop and game audio, so the game gets `OnApplicationPause(true)` and then
  `false`. Immersive sticky mode; content laid out inside the display cutout and any system bars
  that are visible (split screen, freeform windows, revealed bars); hidden immersive bars take no
  room.
- iOS: a `UIViewController` presented `UIModalPresentationFullScreen` from Unity's root view
  controller, same supported orientations as Unity's, status bar and home indicator hidden, content
  inside the safe area. The ad is presented only while the app is active: a `show` made while it is
  inactive (backgrounded, a system alert, Control Center) waits until it becomes active, with no
  event until then. If a view controller transition is running, it presents when that transition
  ends. The game keeps running until `shown` has reached it: C# calls `SoilAds_ShownReceived`,
  or after 0.5 s at the latest, the player calls `UnityPause(1)` (only if `UnityIsPaused()` was 0,
  and only once the app is active), so `shown` arrives when the ad appears, as on Android.
  `UnityPause(0)` after dismissal, only if the player paused Unity, so a game that was already
  paused stays paused. If UIKit has not finished the dismissal after 3 s and the ad is still on screen, the
  dismissal is retried without animation up to 3 times, 1 s apart; the game resumes and `closed` is
  sent once the ad is off screen, or after the last retry. Known limitations: while the ad holds
  Unity paused, backgrounding the app does not reach the game's `OnApplicationPause`; ad audio
  follows the ringer/silent switch (Unity owns the audio session).
- Background: when the ad has an image (an image ad, or a video's poster), that image
  aspect-filled edge to edge, blurred and darkened (iOS: `UIBlurEffectStyleDark` plus 35% black;
  Android: shrunk to 40 px, box-blurred 3 times and dimmed to 50% at load, then scaled up), so a
  4:5 cover image or a letterboxed video sits on its own colors. Black without an image. Media
  aspect-fit and centered on it; tapping media = click.
- Image-only ads (interstitial or rewarded with no usable video) show the image with the image
  lock (`imageLockSeconds`: 5 s interstitial, 20 s rewarded); with no texts, logo or call to
  action they have no info card and the image takes the whole safe area.
- Top-left: the ad badge, `تبلیغ` ("ad"), black on yellow, on every format. Top-right: round
  close button (44 pt on iOS) showing the countdown number while locked, `✕` when unlocked.
  Android back button closes only when unlocked.
- Video ads: a mute toggle next to the badge; sound on unless `startMuted`. The video pauses
  when the app goes to background or after a click, and resumes when the ad is visible again.
  When the video ends its player is released and never reopened; the ad keeps showing its image,
  or a copy of the last frame if there is no image, also after backgrounding. A resumed video
  continues from where it was left (Android API 26+ seeks to the exact position, not the previous
  key frame); playback progress never moves backwards.
- Info card under the media (when any of logo/title/description/callToAction exists): a rounded
  dark card, at most 520 dp/pt wide, 12 dp/pt from the safe-area edges. A row with the logo
  (56, rounded, square), the title (bold 18, up to 2 lines) and the description (14, up to 3
  lines); under it the call to action as a full-width button (at least 50 tall). No call to action
  in the creative means no button (the media and texts stay clickable through the media).
- Text direction: each label is aligned by its own first strong character, so Persian lines up
  right and English left, and mixed text keeps its order. Rows (logo, texts, button) follow the
  creative's direction: the title's, else the description's, else the call to action's; for a
  right-to-left creative the logo is on the right. Android lays these rows out by hand, because a
  game's manifest rarely declares `supportsRtl`. Wrapped lines get a little extra spacing so
  Persian marks do not touch the line above. Fonts are the system's (no bundled fonts).
- Click: open `clickUrl` with the system (Android `Intent.ACTION_VIEW` + `CATEGORY_BROWSABLE` +
  `FLAG_ACTIVITY_NEW_TASK`, catching `ActivityNotFoundException`; iOS
  `-[UIApplication openURL:options:completionHandler:]`). `clicked` is sent even if nothing opens.
  See *Click links* for which links are opened.
- Accessibility identifiers / content descriptions: `soil_ad_close`, `soil_ad_cta`,
  `soil_ad_media`, `soil_ad_mute`, `soil_ad_banner`, and on Android `soil_ad_info` (the card) and
  `soil_ad_backdrop` (used by UI automation tests).
- Accessibility: the badge reads "Ad". On iOS the banner and the fullscreen media read "Ad" or
  "Ad, <title>". While
  locked, the close button is marked not enabled and its value is the countdown; when it unlocks
  it becomes a plain "Close" button and screen-reader focus moves to it.

## Click links

Click URLs come from the ad server, so the player opens only links that cannot act on the device.
The link is trimmed; a link with control characters, no valid RFC 3986 scheme, or nothing after the
scheme is never opened.

- iOS: only `http`, `https`, `itms-apps`, `itms-appss` (case-insensitive). A URL that
  `+URLWithString:` rejects (spaces, non-ASCII on iOS 12-16) is percent-encoded and tried again.
- Android: `http`, `https`, `market` and any other app or store scheme, except the ones that act on
  the device: `javascript`, `vbscript`, `data`, `file`, `content`, `intent`, `android-app`,
  `about`, `blob`, `tel`, `sms`, `smsto`, `mms`, `mmsto`, `mailto`, `wtai`. Opened as a browsable
  link only.
- C# applies the same rule before the creative reaches the player (`AdLinkPolicy`; app schemes are
  allowed on Android builds only), and a refused link is treated as no link.
- Native-format ads are drawn by the game, so C# opens their links itself: on Android through
  `SoilAdsBridge.openLink(String url)` (static, any thread; returns `false` for a refused link,
  otherwise opens it on the main thread from the host activity exactly as a player click does),
  falling back to `Application.OpenURL` only when the player is missing from the build; elsewhere
  with `Application.OpenURL`.

`clicked` is sent for every tap, whether or not a link opened.

## Banner presentation

- Full safe-area width, height 50 dp/pt on phones and 90 dp/pt when the smallest side of the app's
  window is at least 600 dp/pt (tablets; on iOS the host window's bounds, so Split View counts,
  with the screen only as a fallback). Placed at the bottom, top or vertical center of the safe area.
- Android: `activity.addContentView(view, FrameLayout.LayoutParams(MATCH_PARENT, h, gravity))`,
  margins from the display cutout only (like CAS: a visible navigation bar does not lift it). iOS: subview of Unity's root view
  controller view, Auto Layout against `safeAreaLayoutGuide`.
- With an image: the image aspect-fit and centered at the banner's height, with the `تبلیغ`
  badge on the image's top-left corner. The rest of the banner (the sides of a 320x50 image on a
  wider screen) shows the same image filled, blurred and darkened, as behind fullscreen ads; the
  banner's size never changes. Without: logo, title/description and call-to-action laid out in a row that follows the
  creative's direction (logo on the right for Persian), with the badge before the title on its
  line, clear of the logo and button. The whole banner is clickable.
- The banner never steals touches outside its own rectangle.

## Failures inside the players

No exception may leave a player into the game, whose main thread an uncaught one would end:

- Every call from Unity, and every callback the OS makes into the player (Activity / view
  controller lifecycle, the tick, buttons, back, insets, media and surface events, load results,
  delayed blocks and notifications), runs inside a guard (Android `Guard`, iOS `SoilAdsGuard`).
- A failure in a fullscreen ad ends the show the way a close does: `closed` if it was shown, else
  `showFailed` / `internal` with the ad back in its slot. The ad screen goes away and the game
  resumes (iOS: `UnityPause(0)` if the player paused it; Android: the ad Activity finishes).
- A failure while loading reports `loadFailed` / `internal`; a load result is delivered once.
- A failure elsewhere (a banner callback, `isReady`) is logged and the call answers with its
  neutral value (`isReady` false).
- C# catches exceptions from event handlers and the frame tick, so one failing listener does not
  stop the others; the Unity-side watchdogs below still answer a call the player never answers.

Hard crashes (a signal inside the OS media stack) cannot be caught by app code.

## Unity-side watchdogs

C# never waits forever on a player. Only time while the game runs counts (unscaled, at most a
fraction of a second per frame, so time paused under a fullscreen ad does not count).

- `load`: no `loaded`/`loadFailed` within 60 s → the load fails with `timeout`, the native slot is
  destroyed if nothing is on screen, and a late answer is ignored.
- Fullscreen `show`: no answer at all (`shown`, `showFailed` or `closed`) within 30 s → `showFailed`
  with `timeout`, the player is told to `hide`, and later events of that show are swallowed (a late
  `shown` is hidden again). An ad that did appear is never timed out.
- A `loadFailed` with `media_unreadable` or `invalid_creative` for a creative built from the cache
  drops the broken files and re-caches them, at most twice per format until the next `loaded`.

## Minification (Android)

The SDK's Editor script (`Advertisement/Editor/SoilAdsProguardRules.cs`) adds
`-keep class com.flyingacorn.soil.ads.** { *; }` to unityLibrary's `proguard-unity.txt`, which
Unity uses as a consumer ProGuard file, so R8 keeps the bridge C# reaches by name. Games with a
custom Gradle setup that does not use that file must add the rule themselves.
