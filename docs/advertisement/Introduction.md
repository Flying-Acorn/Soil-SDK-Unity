# Advertisement

## Introduction

Monetize your game with integrated advertisement solutions. Supports video ads and more.

**⚠️ Experimental Feature**: The Advertisement SDK is experimental. Test thoroughly on all target platforms before shipping.

## Features

- **Manual Pause Control**: Game developers control pause behavior using ad lifecycle events
- **Input Blocking**: SDK disables `PlayerInput` components (New Input System) during ads
- **Event-Driven Architecture**: Rich event system for ad lifecycle management
- **Multi-Format Support**: Banner, interstitial, rewarded, and native ad formats
- **Native Ads**: Raw assets (icon, image, headline, body, call to action) delivered to your own UI, so ads match your game's look
- **Automatic Ad Reload**: Ads can be reloaded after closing for seamless user experience
- **Rewarded Ad Cooldown**: 10-second cooldown between rewarded ads with automatic wait handling

## Integration

See [Integration](Integration.md) for detailed setup and usage.

Demo scene: `Assets/FlyingAcorn/Soil/Advertisement/Demo/SoilAdvertisementExample.unity`

## Dependencies

- <a href="https://github.com/pnarimani/RTLTMPro/" target="_blank">RTL Text Mesh Pro</a> (for localized text)