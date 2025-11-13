# Advertisement

## Introduction

Monetize your game with integrated advertisement solutions. Supports video ads and more.

## Features

- **Automatic Pause Management**: SDK automatically pauses gameplay (`Time.timeScale = 0`) during full-screen ads to prevent conflicts
- **Configurable Pause Behavior**: Customize or disable automatic pausing via `Advertisement.SetPauseGameplayDuringAds()`
- **Input Blocking**: Comprehensive input blocking during ads using UI overlays and physics shields
- **Event-Driven Architecture**: Rich event system for ad lifecycle management
- **Multi-Format Support**: Banner, interstitial, and rewarded ad formats

## Integration

See [Integration](Integration.md) for detailed setup and usage.

Demo scene: `Assets/FlyingAcorn/Soil/Advertisement/Demo/SoilAdvertisementExample.unity`

## Dependencies

- <a href="https://github.com/pnarimani/RTLTMPro/" target="_blank">RTL Text Mesh Pro</a> (for localized text)