# Advertisement

## Introduction

Monetize your game with integrated advertisement solutions. Supports video ads and more.

**⚠️ Experimental Feature**: The Advertisement SDK is experimental. Test thoroughly on all target platforms before shipping.

## Features

- **Native Players**: Banner, interstitial and rewarded ads are drawn by a small native player on each platform — Android `MediaPlayer` on a `TextureView`, iOS `AVPlayer` — instead of Unity canvases and `VideoPlayer`. Smooth video, correct audio, right-to-left text and safe areas come from the OS.
- **No Dependencies, No Network in Native Code**: The players use only the operating system (no AndroidX, Google Play services, CocoaPods or third-party SDKs) and only play files the SDK has already downloaded from Soil, so they work the same in every country and store.
- **Pauses the Game for Fullscreen Ads**: On Android the ad opens in its own activity and on iOS Unity is paused while it is on screen, so gameplay, input and game audio stop without any setup.
- **Event-Driven Architecture**: Rich event system for ad lifecycle management; every `LoadAd` is answered with a Loaded or Error event
- **Multi-Format Support**: Banner, interstitial, rewarded, and native ad formats
- **Native Ads**: Raw assets (icon, image, headline, body, call to action) delivered to your own UI, so ads match your game's look
- **Multiple Native Surfaces**: Render one native ad in several places at once, each using the assets that suit it, with independent clicks
- **Automatic Ad Reload**: A closed interstitial or rewarded ad is prepared again right away
- **Ad Rotation**: The next interstitial and rewarded ad is downloaded in the background, so each close moves on to a different app
- **Rewarded Ad Cooldown**: 10-second cooldown between rewarded ads with automatic wait handling
- **Editor Simulation**: In the Editor a simulated player draws placeholders and follows the same rules, so game flows can be tested without a device

## Integration

See [Integration](Integration.md) for detailed setup and usage.

Demo scene: `Assets/FlyingAcorn/Soil/Advertisement/Demo/SoilAdvertisementExample.unity`

## Platform Requirements

- Android 5.1 (API 22) or newer — no extra Gradle dependencies.
- iOS 12 or newer — no CocoaPods.
- Android builds with minification (R8) keep the player automatically: the SDK adds its keep rule to Unity's `proguard-unity.txt`.
- Android Target API Level must be Automatic or 33+ (the player is compiled against Android 13 APIs; it still runs on API 22+). The build logs an error if it is set lower.
- Desktop, WebGL and consoles: no banner, interstitial or rewarded ads (they answer no fill); native ads work.
- How the native players work, and the contract between C# and them: [`NativeAds/PROTOCOL.md`](../../NativeAds/PROTOCOL.md).
- The whole picture for maintainers (architecture, libraries, testing, debugging): [`NativePlayersOverview.md`](NativePlayersOverview.md).

## Dependencies

- Core SDK
- Unity UI (`com.unity.ugui`)

The device players use only the operating system. RTL Text Mesh Pro is no longer needed: the Editor placeholder shows English stand-ins for Persian texts, and devices draw the Persian texts through the OS.