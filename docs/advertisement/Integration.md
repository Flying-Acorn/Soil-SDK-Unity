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

### 1.5 Implementing Game Pause During Ads

**Important**: The SDK does **not** automatically pause your game. You must implement pause behavior in your game code using ad lifecycle events.

#### Why Manual Pause Control?

Setting `Time.timeScale = 0` was found to break UI input and ad clickability in some Unity configurations. To ensure ads work reliably across platforms, the SDK leaves pause control to your game code.

#### Input Blocking Behavior

The SDK helps prevent gameplay input during ads by:
- Disabling `PlayerInput` components (New Input System only)
- Exposing `SoilAdInputBlocker.IsBlocked` for checking blocked state
- Including a 40-second failsafe timeout to prevent permanent blocking

**Note**: If you use the old Input API, custom input handlers, or direct raycasts, you must implement your own input blocking.

#### Recommended Pause Implementation

Subscribe to ad events and disable gameplay systems explicitly:

```csharp
void Start()
{
    // Subscribe to ad lifecycle events
    Advertisement.Events.OnInterstitialAdShown += HandleAdShown;
    Advertisement.Events.OnRewardedAdShown += HandleAdShown;
    Advertisement.Events.OnInterstitialAdClosed += HandleAdClosed;
    Advertisement.Events.OnRewardedAdClosed += HandleAdClosed;
}

private void HandleAdShown(AdEventData data)
{
    // Disable gameplay systems explicitly
    PlayerController.Instance.enabled = false;
    EnemySpawner.Instance.SetEnabled(false);
    // Disable physics-based controls, AI, timers, etc.
}

private void HandleAdClosed(AdEventData data)
{
    // Re-enable gameplay systems
    PlayerController.Instance.enabled = true;
    EnemySpawner.Instance.SetEnabled(true);
}

void OnDestroy()
{
    // Always unsubscribe to prevent memory leaks
    Advertisement.Events.OnInterstitialAdShown -= HandleAdShown;
    Advertisement.Events.OnRewardedAdShown -= HandleAdShown;
    Advertisement.Events.OnInterstitialAdClosed -= HandleAdClosed;
    Advertisement.Events.OnRewardedAdClosed -= HandleAdClosed;
}
```

#### Alternative: Check Input Blocker in Update

```csharp
void Update()
{
    // Skip gameplay logic while ads are shown
    if (SoilAdInputBlocker.IsBlocked)
        return;
    
    // Normal gameplay code here
    HandlePlayerInput();
    UpdateGameLogic();
}
```

**Warning**: Do not use `Time.timeScale = 0` to pause during ads, as it can break ad clickability and UI interactions.

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

**Note**: After an ad is closed, you should call `LoadAd` again to prepare the next ad. The demo scene shows automatic reload in the `OnAdClosed` event handler.

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
    
    // Reload the ad for next use - ads automatically clear on close
    Advertisement.LoadAd(data.AdFormat);
    
    // For rewarded ads during cooldown, LoadAd will wait automatically
    // until cooldown expires before firing OnRewardedAdLoaded
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

### Failsafe Timeout

The SDK includes a 40-second failsafe timeout that automatically unblocks input if an ad fails to close properly. This is enforced by `SoilAdManager` calling `SoilAdInputBlocker.FailsafeTick()` each frame. Test long ads and failure scenarios to ensure this works as expected in your game.

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

Player-initiated ads that grant rewards upon completion. Rewarded ads have a 10-second cooldown after closing.

```csharp
// Check readiness (includes cooldown check)
if (Advertisement.IsFormatReady(AdFormat.rewarded))
{
    Advertisement.ShowAd(AdFormat.rewarded);
}

// Check cooldown status
if (Advertisement.IsRewardedAdInCooldown())
{
    float remaining = Advertisement.GetRewardedAdCooldownRemainingSeconds();
    Debug.Log($"Rewarded ad in cooldown. Remaining: {remaining} seconds");
}

// Reset cooldown for testing (admin purposes)
Advertisement.ResetRewardedAdCooldown();
```

**Automatic Cooldown Handling**: When you call `LoadAd(AdFormat.rewarded)` during cooldown, the SDK automatically waits for the cooldown to expire before firing the `OnRewardedAdLoaded` event. You don't need to manually wait for cooldown.

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
    Debug.Log($"Assets cached for {format}");
    // This fires when assets are cached during initialization
    // It does NOT fire OnBannerAdLoaded/OnInterstitialAdLoaded/OnRewardedAdLoaded
    // Those events only fire from explicit LoadAd() calls
}
```

## Testing Checklist

Before shipping, thoroughly test the following:

- ✅ Verify ad click-throughs work on all target platforms (iOS/Android/editor)
- ✅ Verify ad close behavior returns control reliably across scenes
- ✅ Test with both New Input System and legacy Input API if your project uses both
- ✅ Test long ads and simulate failures to ensure failsafe unblocks input after ~40s
- ✅ Test multi-scene flows and `DontDestroyOnLoad` objects to ensure events work correctly
- ✅ Verify rewarded ad cooldown works as expected
- ✅ Test that your pause implementation works correctly with ad events

## Compatibility Notes

- The SDK uses `Object.FindObjectsByType` on Unity 2023.1+ and falls back to `FindObjectsOfType` on older versions
- Expect minor runtime differences across Unity versions — test accordingly
- If using the old Input API (`Input.GetKey`, `Input.GetMouseButton`, etc.), implement your own input blocking

## Common Issues

**Ad clicks don't work**: Check that you are not setting `Time.timeScale = 0` globally while ads are showing. This is the most common cause of broken ad clickability.

**Events fire multiple times**: Ensure you unsubscribe from events when scenes unload if you attach listeners on objects that are destroyed.

**Input remains blocked**: Check the failsafe is working by calling `SoilAdInputBlocker.FailsafeTick()` in an Update loop (this is done automatically by `SoilAdManager`).

## Demo Scene

See the [Advertisement Demo](../README.md#demo-scenes) (`SoilAdvertisementExample.unity`) for a complete working example of initialization, loading, showing ads, event handling, and automatic reload patterns.

For advanced implementation patterns including retry logic, health monitoring, and mediation layers, refer to the `SoilMediation.cs` example file, which demonstrates a production-ready ad management system.

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.