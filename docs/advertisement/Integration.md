# Advertisement Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

## Initialization

Initialize the Advertisement service with the desired ad formats (banner, interstitial, rewarded):

```csharp
using FlyingAcorn.Soil.Advertisement;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

List<AdFormat> formats = new List<AdFormat> { AdFormat.banner, AdFormat.interstitial, AdFormat.rewarded };
Advertisement.InitializeAsync(formats);
```

## Ad Formats

### Banner Ads

#### Loading
```csharp
Advertisement.LoadAd(AdFormat.banner);
```

#### Checking Readiness
```csharp
bool isReady = Advertisement.IsFormatReady(AdFormat.banner);
```

#### Showing
```csharp
if (Advertisement.IsFormatReady(AdFormat.banner))
{
    Advertisement.ShowAd(AdFormat.banner);
}
```

#### Hiding
```csharp
Advertisement.HideAd(AdFormat.banner);
```

### Interstitial Ads

#### Loading
```csharp
Advertisement.LoadAd(AdFormat.interstitial);
```

#### Checking Readiness
```csharp
bool isReady = Advertisement.IsFormatReady(AdFormat.interstitial);
```

#### Showing
```csharp
if (Advertisement.IsFormatReady(AdFormat.interstitial))
{
    Advertisement.ShowAd(AdFormat.interstitial);
}
```

### Rewarded Ads

#### Loading
```csharp
Advertisement.LoadAd(AdFormat.rewarded);
```

#### Checking Readiness
```csharp
bool isReady = Advertisement.IsFormatReady(AdFormat.rewarded);
```

#### Showing
```csharp
if (Advertisement.IsFormatReady(AdFormat.rewarded))
{
    Advertisement.ShowAd(AdFormat.rewarded);
}
```

#### Cooldown Management
Check if rewarded ad is in cooldown (to prevent spam):
```csharp
if (Advertisement.IsRewardedAdInCooldown())
{
    float remaining = Advertisement.GetRewardedAdCooldownRemainingSeconds();
    // Show cooldown message
}
```

Reset cooldown if needed:
```csharp
Advertisement.ResetRewardedAdCooldown();
```

## Event Handling

Subscribe to ad events for better control. Events are available for each format:

```csharp
// Loading events
Advertisement.Events.OnBannerAdLoaded += HandleAdLoaded;
Advertisement.Events.OnInterstitialAdLoaded += HandleAdLoaded;
Advertisement.Events.OnRewardedAdLoaded += HandleAdLoaded;

// Error events
Advertisement.Events.OnBannerAdError += HandleAdError;
Advertisement.Events.OnInterstitialAdError += HandleAdError;
Advertisement.Events.OnRewardedAdError += HandleAdError;

// Display events
Advertisement.Events.OnBannerAdShown += HandleAdShown;
Advertisement.Events.OnInterstitialAdShown += HandleAdShown;
Advertisement.Events.OnRewardedAdShown += HandleAdShown;

// Interaction events
Advertisement.Events.OnBannerAdClicked += HandleAdClicked;
Advertisement.Events.OnInterstitialAdClicked += HandleAdClicked;
Advertisement.Events.OnRewardedAdClicked += HandleAdClicked;

// Close events
Advertisement.Events.OnBannerAdClosed += HandleAdClosed;
Advertisement.Events.OnInterstitialAdClosed += HandleAdClosed;
Advertisement.Events.OnRewardedAdClosed += HandleAdClosed;

// Rewarded specific
Advertisement.Events.OnRewardedAdRewarded += HandleAdRewarded;
```

## Demo Scene

See the [Advertisement Demo](../README.md#demo-scenes) (`SoilAdvertisementExample.unity`) for a complete working example of initialization, loading, showing ads, and event handling.

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.