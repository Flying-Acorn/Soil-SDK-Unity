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

### 1.5 Game Pause, Input and Audio During Ads

Interstitial and rewarded ads are drawn natively **on top of Unity**:

- **Android**: the ad opens in its own activity. Unity's activity pauses underneath, which pauses the game loop and the game's audio.
- **iOS**: the ad is presented over Unity's view controller and Unity is paused (`UnityPause`) until it closes.

So gameplay receives no input and makes no sound while a fullscreen ad is up, without any code in your game. Events raised while Unity is paused (`Shown`, `Rewarded`, `Clicked`) are delivered, in order, as soon as it resumes — right before `Closed`.

`SoilAdInputBlocker.IsBlocked` (or `Advertisement.IsFullscreenAdShowing`) is true while a fullscreen ad is on screen, for game code that wants to know. If your game keeps its own timers in real time, pause them from the events:

```csharp
Advertisement.Events.OnInterstitialAdShown += _ => PauseGame();
Advertisement.Events.OnInterstitialAdClosed += _ => ResumeGame();
Advertisement.Events.OnRewardedAdShown += _ => PauseGame();
Advertisement.Events.OnRewardedAdClosed += _ => ResumeGame();
```

Banners are native views over the game and never pause it.

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

**Every `LoadAd` is answered** with the format's Loaded or Error event. If the ad's files are still downloading (for example right after initialization), the answer comes when they are ready. After an interstitial or rewarded ad closes, the SDK prepares the same ad again by itself; calling `LoadAd` from `OnAdClosed` is still the simplest pattern and is answered as soon as the ad is ready.

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

Banner ads span the width of the safe area — 50 dp/pt high on phones, 90 on tablets — at the bottom (default), top or middle of the screen, and can be hidden/shown as needed:

```csharp
// Where banners go (TopCenter, MiddleCenter or BottomCenter)
Advertisement.BannerPosition = AdPosition.TopCenter;

// Hide banner (useful during gameplay)
Advertisement.HideAd(AdFormat.banner);

// Show banner again; showing a visible banner moves it to BannerPosition
Advertisement.ShowAd(AdFormat.banner);

// Or both at once
Advertisement.ShowBanner(AdPosition.BottomCenter);
```

A banner with an image shows the image; one without shows the advertiser's logo, title and call to action. Tapping anywhere on it opens the ad.

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

### When the Close Button Unlocks

Fullscreen ads keep their close button locked for a moment, showing a countdown; a rewarded ad grants its reward (`OnRewardedAdRewarded`, always before `OnRewardedAdClosed`) at the moment it unlocks.

| | Image ad | Video ad |
|---|---|---|
| Interstitial | after 5 s on screen | at 80% of the video (at least 5 s) |
| Rewarded | after 20 s on screen | at the end of the video |

Only time the ad is actually visible counts: the countdown stops while the app is in the background or the player has left to the advertiser's page. A video that fails mid-play falls back to the ad's image and the image rule. As a safety net the close button always unlocks after 60 s on screen. Android's back button closes an ad only once it is unlocked. Video ads have a mute button and start with sound on (on iOS the device's silent switch is respected, as for the game).

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

#### 3. Showing the ad in more than one place

One loaded native ad can be rendered in several places at once — a native banner and a leaderboard
row, say — each showing whichever assets suit it. The row might use only the icon and headline; the
banner the full set. Nothing extra to configure: call `ShowNativeAd` from each view with **its own**
`NativeAdReferences`, and each registers its clicks independently.

```csharp
Advertisement.ShowNativeAd(bannerReferences);       // icon + image + headline + body + CTA
Advertisement.ShowNativeAd(leaderboardReferences);  // icon + headline only
```

Keep the references object your view created — you need it to release just that view.

#### 4. Hide and release

```csharp
// Release ONE view's clicks. Other places showing this ad keep working.
Advertisement.HideNativeAd(myReferences);

// Release every view's registration, keeping the ad loaded.
Advertisement.HideNativeAd();

// Release the ad itself, everywhere.
Advertisement.DestroyNativeAd();
```

Call `HideNativeAd(myReferences)` from a view's `OnDisable`, **not** `DestroyNativeAd()` — the
latter takes the ad away from every other place showing it. Reserve `DestroyNativeAd()` for
teardown and the ad-free purchase.

> One ad shared across places means the same advertiser appears in each. Fine for places the
> player reaches separately. If a **single screen** shows several native slots at once they would
> all render the identical creative — show one slot per screen.

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
    // Fires when a format's files are cached during initialization. The ad is handed to the
    // native player at the same moment; its Loaded event follows once the player has it ready.
}
```

## Testing Checklist

Before shipping, thoroughly test the following on real Android and iOS devices:

- ✅ Banner, interstitial and rewarded ads appear, and tapping them opens the advertiser link
- ✅ Banners sit inside the safe area (notch, home indicator) in every orientation your game supports
- ✅ Fullscreen ads cover the game, the countdown unlocks the close button, and closing returns to the game
- ✅ `OnRewardedAdRewarded` grants the reward exactly once, before `OnRewardedAdClosed`
- ✅ Leaving the app during a video ad and coming back resumes it without skipping the countdown
- ✅ Rewarded ad cooldown works as expected
- ✅ Verify native ad taps open the advertiser link from every view you registered
- ✅ Verify your native layout still looks right when `Description` or `MainImage` is `null`
- ✅ Test multi-scene flows and `DontDestroyOnLoad` objects to ensure events work correctly

## Common Issues

**Nothing shows on device but the Editor works**: Check the player log for `[Advertisement] Android ad player unavailable` / `iOS ad player unavailable`. It means the native player was stripped from the build — keep `Assets/FlyingAcorn/Soil/Advertisement/Plugins` in the project.

**Events fire multiple times**: Ensure you unsubscribe from events when scenes unload if you attach listeners on objects that are destroyed.

**`OnXAdLoaded` fires without a `LoadAd` call**: The SDK announces each ad it prepares once: after initialization, and again after an ad closes (a closed ad is used up and prepared again, banners included). Treat Loaded as "ready to show", not as an answer to one specific call.

## Example Script

`Assets/FlyingAcorn/Soil/Advertisement/Demo/NativeAdExample.cs` is a complete, runnable native ad
integration: initialization, load, show, render, click registration, hide, and every event. Drop it
on a panel under a Canvas, wire the icon/image/text fields, and it works.

## Demo Scene

See the [Advertisement Demo](../README.md#demo-scenes) (`SoilAdvertisementExample.unity`) for a complete working example of initialization, loading, showing ads, event handling, and automatic reload patterns.

For advanced implementation patterns including retry logic, health monitoring, and mediation layers, refer to the `SoilMediation.cs` example file, which demonstrates a production-ready ad management system.

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.