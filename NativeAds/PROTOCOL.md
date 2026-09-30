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
| Min OS | API 22 (Unity 2022.3 minimum) | iOS 12 (Unity 2022.3 minimum) |
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
| `showFailed` | `error`, `message` | Nothing was shown. |
| `clicked` | — | User tapped the call to action or media; the click URL was opened (or could not be). |
| `rewarded` | — | Rewarded only. Sent once per show, always before `closed`. |
| `closed` | — | Ad left the screen (user close, back button, `hide`, `destroy`). Sent exactly once per `shown`. |

Error codes (`error`): `invalid_format`, `invalid_creative`, `media_unreadable`, `not_loaded`,
`already_showing`, `no_host` (no Activity / no root view controller), `internal`.

Events for one show are always ordered: `shown` → (`clicked`)* → (`rewarded`)? → `closed`.

## Slots

Each format has one **slot** holding at most one loaded creative.

- `load` replaces whatever is in the slot (the previous slot content is released; an ad that is
  on screen is not affected). A second `load` while a first one is still decoding cancels the
  first; only the last one reports `loaded`/`loadFailed`.
- Fullscreen `show` **consumes** the slot: after `shown`, `isReady` is false until the next `load`.
- Banner `show` does **not** consume the slot: the banner can be hidden and shown again.
  Showing a banner that is already visible moves it to the new position and sends nothing.
- `hide(banner)` removes the banner view (`closed`). `hide(interstitial|rewarded)` dismisses a
  fullscreen ad that is on screen (`closed`, no `rewarded` unless already granted).
- `destroy` = `hide` + empty the slot.
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
- Images are decoded off the main thread and downsampled to the screen size.

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
  "maxLockSeconds": 60,
  "startMuted": false
}
```
Defaults C# sends: interstitial `5 / 0.8 / 5 / 60`, rewarded `20 / 1.0 / 0 / 60`.

## Fullscreen lock policy (identical on both platforms, unit-tested on both)

The close button is **locked** for a while, then unlocks. Rewarded ads grant the reward at the
moment they unlock.

Inputs: media (`video`/`image`), video duration `D` seconds, `visibleSeconds` (time the ad has been
on screen while the app was in the foreground — does not advance while backgrounded or while the
user is away after a click), `positionSeconds` (video playback position), `videoEnded`,
`videoFailed`.

```
videoLock = clamp(max(minVideoLockSeconds, videoLockFraction * D), 0, D)
unlocked =
     visibleSeconds >= maxLockSeconds                     // safety net: never trap the user
  || (media == video && !videoFailed && (videoEnded || positionSeconds >= videoLock))
  || ((media == image || videoFailed) && visibleSeconds >= imageLockSeconds)
secondsRemaining (for the countdown label) =
     media == video && !videoFailed ? ceil(videoLock - positionSeconds)
                                    : ceil(imageLockSeconds - visibleSeconds),
     never below 0, never above ceil(maxLockSeconds - visibleSeconds)
```

Once unlocked it stays unlocked. `rewarded` is sent once, on the first unlock of a rewarded ad.
A video that fails mid-play switches to the fallback image (or keeps the last frame) and continues
under the image rule, measured from when the ad was first shown.

## Fullscreen presentation

- Android: a dedicated `SoilAdActivity` (declared in the androidlib manifest, framework theme
  `@android:style/Theme.Black.NoTitleBar.Fullscreen`, `configChanges` covering orientation/size so
  it is not recreated, `screenOrientation="behind"`). Unity's activity pauses underneath, which
  pauses the game loop and game audio. Immersive sticky mode; content laid out inside display
  cutout insets.
- iOS: a `UIViewController` presented `UIModalPresentationFullScreen` from Unity's root view
  controller, same supported orientations as Unity's, status bar and home indicator hidden, content
  inside the safe area. `UnityPause(1)` on present, `UnityPause(0)` after dismissal.
- Black background; media aspect-fit and centered; tapping media = click.
- Top-left: small "Ad" badge. Top-right: round close button showing the countdown number while
  locked, `✕` when unlocked. Android back button closes only when unlocked.
- Video ads: a mute toggle next to the badge; sound on unless `startMuted`. The video pauses
  when the app goes to background or after a click, and resumes when the ad is visible again.
  When the video ends it stays on its last frame (or the image, if there is one).
- Bottom bar (when any of logo/title/description/callToAction exists): logo (rounded, square),
  title (bold, 1 line) and description (2 lines), then a call-to-action button. Text alignment is
  natural and follows the text's first strong character, so right-to-left text lines up right.
- Click: open `clickUrl` with the system (Android `Intent.ACTION_VIEW` + `FLAG_ACTIVITY_NEW_TASK`,
  catching `ActivityNotFoundException`; iOS `-[UIApplication openURL:options:completionHandler:]`).
  `clicked` is sent even if no app can open it.
- Accessibility identifiers / content descriptions: `soil_ad_close`, `soil_ad_cta`,
  `soil_ad_media`, `soil_ad_mute`, `soil_ad_banner` (used by UI automation tests).

## Banner presentation

- Full safe-area width, height 50 dp/pt on phones and 90 dp/pt when the smallest screen side is
  at least 600 dp/pt (tablets). Placed at the bottom, top or vertical center of the safe area.
- Android: `activity.addContentView(view, FrameLayout.LayoutParams(MATCH_PARENT, h, gravity))`,
  top/bottom margins from display cutout insets (API 28+). iOS: subview of Unity's root view
  controller view, Auto Layout against `safeAreaLayoutGuide`.
- With an image: the image aspect-fit on a dark background. Without: logo, title/description and
  call-to-action laid out in a row. Small "Ad" badge in a corner. The whole banner is clickable.
- The banner never steals touches outside its own rectangle.
