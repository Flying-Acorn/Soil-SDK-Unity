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
    : new List<AdFormat> { AdFormat.banner, AdFormat.interstitial, AdFormat.rewarded, AdFormat.native };

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

#### Muting Your Game Audio

**Important**: The SDK does **not** automatically mute or duck your game's audio. You are responsible for silencing your own music/SFX while an ad is shown, using the same ad lifecycle events.

**Do not set `AudioListener.pause = true`** (or otherwise pause the `AudioListener`) to achieve this. On device, that pauses the native audio session, which can starve hardware-accelerated video decoding and cause ad video to freeze or stall — even if your own audio sources aren't involved. Instead, mute or pause your game's own `AudioSource`s (and any music/SFX manager) directly:

```csharp
private void HandleAdShown(AdEventData data)
{
    MyAudioManager.Instance.SetGameAudioMuted(true);
}

private void HandleAdClosed(AdEventData data)
{
    MyAudioManager.Instance.SetGameAudioMuted(false);
}
```

The ad's own audio is unaffected by anything you do here — it plays through a separate `AudioSource` that the SDK manages independently.

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

### Native Ads

Native ads are different from every other format: **the SDK does not draw them**. It hands you the
raw assets and *your* UI renders them, so the ad matches your game's look. You then tell the SDK
which GameObjects you rendered it into, so taps are attributed and open the advertiser's link.

> `Advertisement.ShowAd(AdFormat.native)` is **not** valid — it has no view to draw into and
> raises `OnNativeAdError` with `AdError.InvalidRequest`. Use `ShowNativeAd` below.

#### 1. Build your own layout

Lay out the ad however you like, and **use as many or as few of the assets as you want** — a
compact list row might show only the icon and title; a full card might show everything.

| Asset | Content | Always present? |
|---|---|---|
| Title | `content.Title` | yes |
| Call to action | `content.CallToAction` | yes |
| Icon | `content.Icon` (`Texture2D`) | yes |
| Description | `content.Description` | may be `null` |
| Main image | `content.MainImage` (`Texture2D`) | may be `null` — check `content.HasMainImage` |

Whichever assets you show, register the views you drew them into (next step) and every one of
them becomes clickable. Handle `Description` and `MainImage` being absent by hiding or collapsing
the view rather than showing an empty box.

#### 2. Load, then show

```csharp
using FlyingAcorn.Soil.Advertisement;
using FlyingAcorn.Soil.Advertisement.Data;
using FlyingAcorn.Soil.Advertisement.Models;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

[SerializeField] private RawImage iconImage;
[SerializeField] private RawImage mainImage;
[SerializeField] private TMP_Text titleText;
[SerializeField] private TMP_Text descriptionText;
[SerializeField] private TMP_Text callToActionText;

private void Start()
{
    Advertisement.Events.OnNativeAdLoaded += OnNativeReady;
    Advertisement.Events.OnNativeAdError += OnNativeError;
    Advertisement.LoadAd(AdFormat.native);
}

private void OnNativeReady(AdEventData data)
{
    if (!Advertisement.IsFormatReady(AdFormat.native))
        return;

    // Tell the SDK which views you rendered the ad into so taps are attributed.
    // Every argument is optional - register only what you actually rendered.
    var references = new NativeAdReferences(
        titleGameObject: titleText.gameObject,
        descriptionGameObject: descriptionText.gameObject,
        callToActionGameObject: callToActionText.gameObject,
        iconGameObject: iconImage.gameObject,
        mainImageGameObject: mainImage.gameObject);

    // ShowNativeAd returns the content directly and also raises OnNativeAdContentReady.
    var content = Advertisement.ShowNativeAd(references);
    if (content == null)
        return;

    titleText.text = content.Title;
    callToActionText.text = content.CallToAction;
    iconImage.texture = content.Icon;

    descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(content.Description));
    descriptionText.text = content.Description;

    mainImage.gameObject.SetActive(content.HasMainImage);
    if (content.HasMainImage)
        mainImage.texture = content.MainImage;
}

private void OnNativeError(AdEventData data)
{
    Debug.Log($"No native ad available: {data.AdError}");
}
```

Prefer subscribing to `OnNativeAdContentReady` instead of using the return value if the code that
renders the ad lives somewhere other than the code that shows it — both carry the same object.
`Advertisement.GetNativeAdContent()` returns the loaded content (or `null`) if you would rather poll.

#### 3. Hide and release

```csharp
// Stop attributing clicks and raise OnNativeAdClosed. The ad stays loaded and can be shown again.
Advertisement.HideNativeAd();

// Release the ad entirely; IsFormatReady(AdFormat.native) becomes false until you load again.
Advertisement.DestroyNativeAd();
```

Call `DestroyNativeAd()` when the player buys the ad-free upgrade, and before destroying the
GameObjects you passed in `NativeAdReferences`.

#### Clicks: make the whole ad clickable, or just parts

`ShowNativeAd` attaches a click handler to **every** GameObject you register — a native ad is one
clickable unit.

Pointer events bubble up to the nearest ancestor handler, so the simplest way to make the entire
ad clickable — including custom views the SDK knows nothing about, such as a badge, a rating row
or a background panel — is to register your layout root:

```csharp
// The whole card is clickable, whatever you put inside it.
Advertisement.ShowNativeAd(NativeAdReferences.ForContainer(adCardRoot));

// Or mix: a container plus extra views that live outside it.
var references = new NativeAdReferences(
    iconGameObject: iconImage.gameObject,
    containerGameObject: adCardRoot,
    additionalGameObjects: new[] { floatingCtaButton.gameObject });
```

Registering nothing at all is also valid — pass `null` and handle clicks yourself using
`content.ClickUrl` with `Application.OpenURL`. Fire your own reporting in that case, since the SDK
cannot see those taps.

Clicks travel through uGUI, so each registered view (or one of its children) needs a
**raycast-target `Graphic`**, and the scene needs an `EventSystem`. `Image`, `RawImage` and
TextMeshPro text all qualify by default; a bare empty GameObject does not. The SDK logs a warning
naming any registered view that cannot receive clicks.

#### Asset standard

The backend rejects off-spec uploads and the SDK re-checks them:

| Asset | Dimensions | Max size |
|---|---|---|
| Icon | any square (1:1), up to 2000×2000 | 1 MB |
| Main image | exactly 1200×628 (1.91:1) | 2 MB |

The main image is pinned because your layout reserves a fixed slot for it; 1.91:1 at 1200×628 is
the most widely used landscape ad-image size, so advertisers can reuse creatives they already
have. The icon only has to be square — it is scaled into a small view, so only the 1:1 shape
matters.

An off-spec **main image** is dropped and the ad still shows icon-only. An off-spec **icon** makes
the ad unusable and raises `OnNativeAdError` with `AdError.InvalidRequest` — the creative needs
fixing in the dashboard, so retrying will not help. `AdError.NoFill` means there is simply no
native inventory and retrying later is worthwhile.

Every field on a single `NativeAdContent` always comes from one advertiser — the SDK never pairs
one creative's icon with another's headline.

## Additional Event Types

For more granular control, subscribe to additional events:

```csharp
// Click events
Advertisement.Events.OnBannerAdClicked += OnAdClicked;
Advertisement.Events.OnInterstitialAdClicked += OnAdClicked;
Advertisement.Events.OnRewardedAdClicked += OnAdClicked;
Advertisement.Events.OnNativeAdClicked += OnAdClicked;

// Native ad lifecycle (see Native Ads above)
Advertisement.Events.OnNativeAdLoaded += OnNativeAdLoaded;
Advertisement.Events.OnNativeAdError += OnNativeAdError;
Advertisement.Events.OnNativeAdShown += OnNativeAdShown;
Advertisement.Events.OnNativeAdClosed += OnNativeAdClosed;
Advertisement.Events.OnNativeAdContentReady += OnNativeAdContentReady;  // Action<NativeAdContent>

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
- ✅ Verify native ad taps open the advertiser link from every view you registered
- ✅ Verify your native layout still looks right when `Description` or `MainImage` is `null`
- ✅ Test that your pause implementation works correctly with ad events
- ✅ Verify your game audio is muted/ducked during ads and restored after close (without using `AudioListener.pause`)

## Compatibility Notes

- The SDK uses `Object.FindObjectsByType` on Unity 2023.1+ and falls back to `FindObjectsOfType` on older versions
- Expect minor runtime differences across Unity versions — test accordingly
- If using the old Input API (`Input.GetKey`, `Input.GetMouseButton`, etc.), implement your own input blocking

## Common Issues

**Ad clicks don't work**: Check that you are not setting `Time.timeScale = 0` globally while ads are showing. This is the most common cause of broken ad clickability.

**Events fire multiple times**: Ensure you unsubscribe from events when scenes unload if you attach listeners on objects that are destroyed.

**Input remains blocked**: Check the failsafe is working by calling `SoilAdInputBlocker.FailsafeTick()` in an Update loop (this is done automatically by `SoilAdManager`).

**Ad video freezes or stalls**: Check that you (or a plugin/mediation SDK) are not setting `AudioListener.pause = true` while an ad is showing. This can starve the native video decoder and freeze the ad, independent of any `ignoreListenerPause` settings on individual audio sources. Mute your own audio sources directly instead — see [Muting Your Game Audio](#muting-your-game-audio).

## Example Script

`Assets/FlyingAcorn/Soil/Advertisement/Demo/NativeAdExample.cs` is a complete, runnable native ad
integration: initialization, load, show, render, click registration, hide, and every event. Drop it
on a panel under a Canvas, wire the icon/image/text fields, and it works.

## Demo Scene

See the [Advertisement Demo](../README.md#demo-scenes) (`SoilAdvertisementExample.unity`) for a complete working example of initialization, loading, showing ads, event handling, and automatic reload patterns.

For advanced implementation patterns including retry logic, health monitoring, and mediation layers, refer to the `SoilMediation.cs` example file, which demonstrates a production-ready ad management system.

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.