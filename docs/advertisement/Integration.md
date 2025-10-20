# Advertisement Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

**Service Enablement**: Ensure the Advertisement service is enabled for your account. Reach out to your Soil contact to enable the service for you.

## Complete Ad Integration Flow

Follow this complete flow for implementing advertisement in your game:

### 1. Initialization

Initialize the Advertisement service with the desired ad formats. Consider conditional initialization based on user preferences or purchases:

```csharp
using FlyingAcorn.Soil.Advertisement;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

// Subscribe to events
Advertisement.Events.OnInitialized += OnAdsInitialized;
Advertisement.Events.OnInitializeFailed += OnAdsInitFailed;

// Determine formats based on ad purchase status
bool adsPurchased = CheckIfAdsPurchased(); // Your purchase logic
List<AdFormat> formats = adsPurchased 
    ? new List<AdFormat> { AdFormat.rewarded }  // Only rewarded if purchased
    : new List<AdFormat> { AdFormat.banner, AdFormat.interstitial, AdFormat.rewarded };

Advertisement.InitializeAsync(formats);
```

Define the event handlers:

```csharp
private void OnAdsInitialized()
{
    Debug.Log("Advertisement service initialized successfully");
    // Proceed to load ads
    LoadAllAds();
}

private void OnAdsInitFailed(string error)
{
    Debug.LogError($"Advertisement initialization failed: {error}");
}
```

### 2. Loading Ads

Load ads for all initialized formats. For optimal user experience, load ads immediately after initialization:

```csharp
private void LoadAllAds()
{
    Advertisement.LoadAd(AdFormat.banner);
    Advertisement.LoadAd(AdFormat.interstitial);
    Advertisement.LoadAd(AdFormat.rewarded);
}

// Call this in OnAdsInitialized for proactive loading
private void OnAdsInitialized()
{
    Debug.Log("Advertisement service initialized successfully");
    LoadAllAds(); // Load all formats immediately
}
```

Subscribe to loading events:

```csharp
Advertisement.Events.OnBannerAdLoaded += OnAdLoaded;
Advertisement.Events.OnInterstitialAdLoaded += OnAdLoaded;
Advertisement.Events.OnRewardedAdLoaded += OnAdLoaded;

private void OnAdLoaded(AdEventData data)
{
    Debug.Log($"{data.AdFormat} ad loaded successfully");
    // Ad is now ready to show
    // Reset any retry counters for this format
}
```

### 3. Checking Ad Readiness

Before showing an ad, always check if it's ready:

```csharp
private bool IsAdReady(AdFormat format)
{
    return Advertisement.IsFormatReady(format);
}
```

### 4. Showing Ads

Show an ad when ready and appropriate:

```csharp
private void ShowAdIfReady(AdFormat format)
{
    if (Advertisement.IsFormatReady(format))
    {
        Advertisement.ShowAd(format);
    }
    else
    {
        Debug.Log($"{format} ad is not ready yet");
        // For rewarded ads, this includes cooldown checks
        if (format == AdFormat.rewarded)
        {
            float remaining = Advertisement.GetRewardedAdCooldownRemainingSeconds();
            if (remaining > 0)
            {
                Debug.Log($"Rewarded ad in cooldown. Remaining: {remaining} seconds");
            }
        }
    }
}
```

Subscribe to display events:

```csharp
Advertisement.Events.OnBannerAdShown += OnAdShown;
Advertisement.Events.OnInterstitialAdShown += OnAdShown;
Advertisement.Events.OnRewardedAdShown += OnAdShown;

private void OnAdShown(AdEventData data)
{
    Debug.Log($"{data.AdFormat} ad is now showing");
}
```

### 5. Handling Ad Completion

Handle ad closure and rewards:

```csharp
Advertisement.Events.OnBannerAdClosed += OnAdClosed;
Advertisement.Events.OnInterstitialAdClosed += OnAdClosed;
Advertisement.Events.OnRewardedAdClosed += OnAdClosed;
Advertisement.Events.OnRewardedAdRewarded += OnAdRewarded;

private void OnAdClosed(AdEventData data)
{
    Debug.Log($"{data.AdFormat} ad closed");
    // Ad can be shown again after reloading
    
    // For interstitial and rewarded ads, reload immediately for next use
    if (data.AdFormat == AdFormat.interstitial || data.AdFormat == AdFormat.rewarded)
    {
        Advertisement.LoadAd(data.AdFormat);
    }
}

private void OnAdRewarded(AdEventData data)
{
    Debug.Log("Rewarded ad completed - grant reward to player");
    // Grant in-game rewards (coins, items, etc.)
    GrantReward();
}
```

### 6. Error Handling

Handle ad errors appropriately:

```csharp
Advertisement.Events.OnBannerAdError += OnAdError;
Advertisement.Events.OnInterstitialAdError += OnAdError;
Advertisement.Events.OnRewardedAdError += OnAdError;

private void OnAdError(AdEventData data)
{
    Debug.LogError($"{data.AdFormat} ad error: {data.AdError}");
    // Handle error (retry loading, show fallback, etc.)
}
```

## Advanced Error Handling and Retry Logic

For production applications, implement robust retry logic to handle temporary failures. The SDK supports various error types that may benefit from different retry strategies:

### Error Types and Retry Recommendations

- **NoFill**: Always retry - indicates no ad available currently, but inventory may become available
- **NetworkError**: Retry with backoff - temporary connectivity issues
- **Timeout**: Retry with backoff - server response delays
- **InternalError**: Limited retries - may indicate configuration issues
- **AdNotReady**: Retry immediately - ad failed to load but can be retried

## Ad Purchase Integration

If your game offers ad removal purchases, disable banner ads when purchased:

```csharp
private void OnAdPurchaseCompleted()
{
    // Disable banner ads
    Advertisement.HideAd(AdFormat.banner);
    // Disable interstitial ads
    Advertisement.HideAd(AdFormat.interstitial);
}
```

## Ad Format Details

### Banner Ads

Banner ads are typically shown at the top or bottom of the screen and can be hidden/shown as needed:

```csharp
// Hide banner (useful during gameplay)
Advertisement.HideAd(AdFormat.banner);

// Show banner again
Advertisement.ShowAd(AdFormat.banner);
```

### Interstitial Ads

Full-screen ads shown between game levels or at natural breaks:

```csharp
if (Advertisement.IsFormatReady(AdFormat.interstitial))
{
    Advertisement.ShowAd(AdFormat.interstitial);
}
```

### Rewarded Ads

Player-initiated ads that grant rewards upon completion:

```csharp
// Check cooldown before offering rewarded ad
if (!Advertisement.IsRewardedAdInCooldown())
{
    // Offer rewarded ad to player
    ShowRewardedAdOffer();
}

// Reset cooldown for testing (admin purposes)
Advertisement.ResetRewardedAdCooldown();
```

## Additional Event Types

For more granular control, subscribe to additional events:

```csharp
// Click events
Advertisement.Events.OnBannerAdClicked += OnAdClicked;
Advertisement.Events.OnInterstitialAdClicked += OnAdClicked;
Advertisement.Events.OnRewardedAdClicked += OnAdClicked;

// General events
Advertisement.Events.OnInitialized += OnAdsInitialized;
Advertisement.Events.OnInitializeFailed += OnAdsInitFailed;

// Asset loading events (for advanced implementations)
Advertisement.Events.OnAdFormatAssetsLoaded += OnAdFormatAssetsLoaded;

private void OnAdFormatAssetsLoaded(AdFormat format)
{
    Debug.Log($"Assets loaded for {format}");
    // Handle asset-specific logic if needed
}
```

## Demo Scene

See the [Advertisement Demo](../README.md#demo-scenes) (`SoilAdvertisementExample.unity`) for a complete working example of initialization, loading, showing ads, and event handling.

For advanced implementation patterns including retry logic, health monitoring, and mediation layers, refer to the `SoilMediation.cs` example file, which demonstrates a production-ready ad management system.

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.