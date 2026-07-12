using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using JetBrains.Annotations;
using Newtonsoft.Json;
using FlyingAcorn.Soil.Advertisement.Models;
using FlyingAcorn.Soil.Advertisement.Models.AdPlacements;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;
using System.Linq;
using FlyingAcorn.Soil.Advertisement.Data;
using UnityEngine;
using UnityEngine.UI;
using FlyingAcorn.Analytics;
namespace FlyingAcorn.Soil.Advertisement
{
    public class Advertisement
    {
        /// <summary>
        /// Gets whether the Advertisement service is ready for use.
        /// </summary>
        [UsedImplicitly]
        public static bool Ready => _campaignSelectionSucceeded;
        private static string AdvertisementBaseUrl => $"{Core.Data.Constants.ApiUrl}/advertisement/";
        private static string AdGroupsUrl => $"{AdvertisementBaseUrl}adgroups/";
        private static string AdGroupsSelectUrl => $"{AdGroupsUrl}select/";
        private static bool _campaignRequested;
        private static bool _isInitializing;
        private static readonly Dictionary<AdFormat, AdGroup> _selectedAdGroups = new();
        private static bool _campaignSelectionSucceeded;
        private static List<AdFormat> _requestedFormats;
        private static UniTask _cachedAssetsTask;

        // Ad placement instances
        private static SoilAdManager _adPlacementManager;
        private static GameObject _bannerPlacementGO;
        private static GameObject _interstitialPlacementGO;
        private static GameObject _rewardedPlacementGO;

        // Persistent canvas for all ad placements
        private static Canvas _persistentAdCanvas;

        // Track active ad placement instances
        private static readonly Dictionary<AdFormat, GameObject> _activePlacements = new();

        // Rewarded ad cooldown tracking
        private static DateTime _lastRewardedAdShownTime = DateTime.MinValue;
        private static readonly float RewardedAdCooldownSeconds = 10f;


        /// <summary>
        /// Initializes the Advertisement service with the desired ad formats. Consider conditional initialization based on user preferences or purchases.
        /// </summary>
        /// <param name="adFormats">List of ad formats to initialize (banner, interstitial, rewarded).</param>
        public static void InitializeAsync(List<AdFormat> adFormats)
        {
            if (adFormats == null || adFormats.Count == 0)
            {
                Events.InvokeOnInitializeFailed("No ad formats specified for initialization");
                return;
            }

            if (_selectedAdGroups.Count > 0)
                return;

            if (_isInitializing)
                return;

            _isInitializing = true;
            _requestedFormats = adFormats.Distinct().ToList();

            // Create ad placement manager if it doesn't exist
            AssignAdPlacementManager();

            // Start loading cached assets in background; we'll await inside the success handler
            _cachedAssetsTask = AssetCache.LoadCachedAssetsAsync();

            // If SoilServices already ready, proceed immediately
            if (SoilServices.Ready)
            {
                // run the continuation on the thread pool to avoid blocking caller
                HandleServicesReadyAsync(_cachedAssetsTask).Forget();
                return;
            }

            // Otherwise subscribe to services events
            UnlistenCore();
            SoilServices.OnInitializationFailed += SoilInitFailed;
            SoilServices.OnServicesReady += SoilInitSuccess;

            // Trigger services initialization if not already started
            SoilServices.InitializeAsync();
        }

        private static void UnlistenCore()
        {
            SoilServices.OnInitializationFailed -= SoilInitFailed;
            SoilServices.OnServicesReady -= SoilInitSuccess;
        }

        private static void SoilInitFailed(Exception exception)
        {
            UnlistenCore();
            _isInitializing = false;
            _campaignRequested = false;
            Events.InvokeOnInitializeFailed(exception?.Message ?? "Initialization failed");
        }

        private static async void SoilInitSuccess()
        {
            // Now we can pass the stored cached assets task
            await HandleServicesReadyAsync(_cachedAssetsTask);
        }

        private static async UniTask HandleServicesReadyAsync(UniTask cachedAssetsTask)
        {
            UnlistenCore();

            // Await cached assets task (ignore failures) without unnecessary null check
            try { await cachedAssetsTask; } catch { /* ignore cached asset load failures */ }

            if (_campaignRequested)
            {
                _isInitializing = false;
                return;
            }

            _campaignRequested = true;
            _campaignSelectionSucceeded = false; // reset before attempt
            _selectedAdGroups.Clear();

            // Select formats sequentially (not in parallel): each selection is added to this round's
            // exclusion set immediately, so the next format's request prefers a different ad group
            // (and thus a different advertised app). Frequency capping is best-effort - see
            // SelectAdGroupWithFrequencyCapAsync - so limited inventory falls back to showing an ad
            // rather than starving. A single format's failure is logged and skipped rather than
            // aborting the whole round, since the other formats' requests are independent - but if
            // every single format failed with an exception (e.g. total network outage), that's
            // reported as an init failure rather than silently "no ads available".
            var recentAdGroups = AdvertisementPlayerPrefs.RecentAdGroupIds;
            var recentCampaigns = AdvertisementPlayerPrefs.RecentCampaignIds;
            var roundAdGroups = new List<string>();
            var roundCampaigns = new List<string>();
            Exception lastException = null;
            var failureCount = 0;
            foreach (var format in _requestedFormats)
            {
                try
                {
                    var adGroup = await SelectAdGroupWithFrequencyCapAsync(
                        format, recentAdGroups, recentCampaigns, roundAdGroups, roundCampaigns);
                    if (adGroup == null) continue;

                    _selectedAdGroups[format] = adGroup;

                    // Exclude this app from the remaining formats in THIS round to avoid duplicates.
                    if (!string.IsNullOrEmpty(adGroup.id)) roundAdGroups.Add(adGroup.id);
                    if (!string.IsNullOrEmpty(adGroup.campaign_id)) roundCampaigns.Add(adGroup.campaign_id);

                    // Persist for cross-session variety preference.
                    AdvertisementPlayerPrefs.RecordShownAdGroup(adGroup);
                }
                catch (Exception ex)
                {
                    failureCount++;
                    lastException = ex;
                    MyDebug.LogWarning($"Failed to select ad group for format {format}: {ex.Message}");
                }
            }

            if (failureCount == _requestedFormats.Count && lastException != null)
            {
                _campaignRequested = false;
                _isInitializing = false;
                _campaignSelectionSucceeded = false;
                Events.InvokeOnInitializeFailed($"Failed to select ad groups: {lastException.Message}");
                return;
            }

            _campaignSelectionSucceeded = true; // success path, regardless of per-format availability

            if (_selectedAdGroups.Count == 0)
            {
                await ClearAssetCacheAsync();
                AdvertisementPlayerPrefs.CachedAdGroups = new Dictionary<AdFormat, AdGroup>();
                _campaignRequested = false;
                _isInitializing = false;
                Events.InvokeOnInitialized();
                return;
            }

            GetOrCreatePersistentAdCanvas();
            Events.InvokeOnInitialized();
            // Start asset caching in background - don't block initialization on this
            CacheAds().Forget();
            _campaignRequested = false;
            _isInitializing = false;
        }


        /// <summary>
        /// Creates the persistent ad placement manager GameObject with all ad placements as children
        /// </summary>
        private static void AssignAdPlacementManager()
        {
            if (_adPlacementManager != null)
                return;

            _adPlacementManager = UnityEngine.Object.FindFirstObjectByType(typeof(SoilAdManager)) as SoilAdManager;
            if (_adPlacementManager == null)
                throw new SoilException("SoilAdManager not found in the scene. Please add it to your scene before initializing Advertisement.",
                    SoilExceptionErrorCode.NotFound);
            _bannerPlacementGO = _adPlacementManager.bannerAdPlacement?.gameObject;
            _interstitialPlacementGO = _adPlacementManager.interstitialAdPlacement?.gameObject;
            _rewardedPlacementGO = _adPlacementManager.rewardedAdPlacement?.gameObject;
        }

        // Downloads and caches ads for each format's selected ad group.
        // This method caches each format separately and invokes events as each format becomes ready.
        private static async UniTask CacheAds()
        {
            var cachedAdGroups = AdvertisementPlayerPrefs.CachedAdGroups;
            var updatedCachedAdGroups = new Dictionary<AdFormat, AdGroup>(cachedAdGroups);
            var cachingTasks = new List<UniTask>();

            foreach (var (adFormat, adGroup) in _selectedAdGroups)
            {
                // Only re-cache if the ad group selected for this format actually changed, AND we still
                // have cached assets for it (they may have been evicted by ClearOldAssetsAsync/RemoveCachedAsset
                // even though the AdGroup pointer itself didn't change) - otherwise the placement would
                // preload against an empty cache.
                bool isSameAdGroupStillCached = cachedAdGroups.TryGetValue(adFormat, out var previousAdGroup)
                    && previousAdGroup?.id == adGroup.id
                    && AssetCache.GetCachedAssets(adFormat).Any();

                if (!isSameAdGroupStillCached)
                {
                    cachingTasks.Add(CacheFormatAssetsAsync(adGroup, adFormat));
                }
                else
                {
                    // Assets already cached from a previous session; just (re)preload the placement.
                    OnFormatAssetsReady(adFormat);
                }

                updatedCachedAdGroups[adFormat] = adGroup;
            }

            AdvertisementPlayerPrefs.CachedAdGroups = updatedCachedAdGroups;

            MyDebug.Verbose($"Caching assets for formats: {string.Join(", ", _selectedAdGroups.Keys)}");

            // Wait for all formats to complete (though each will fire events individually)
            try
            {
                await UniTask.WhenAll(cachingTasks);
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Error during asset caching: {ex.Message}");
            }
        }

        /// <summary>
        /// Caches assets for a specific ad format and marks them ready when done
        /// </summary>
        private static async UniTask CacheFormatAssetsAsync(AdGroup adGroup, AdFormat adFormat)
        {
            await AssetCache.ClearFormatCacheAsync(adFormat);
            await AssetCache.CacheAssetsForAdGroupAsync(adGroup, adFormat, OnFormatAssetsReady);
        }

        /// <summary>
        /// Called when assets for a specific format are cached and ready for use.
        /// Does not fire any public *Loaded events; those are only fired from explicit LoadAd calls.
        /// </summary>
        private static void OnFormatAssetsReady(AdFormat adFormat)
        {
            // Notify internal listeners that assets are ready BEFORE preloading the placement below.
            // Placements set an internal "_isFormatReady" flag from this event and consult it inside
            // their own (synchronous, for banner/interstitial) Load() call; firing it after would let
            // Load() run against a stale flag and could report AdNotReady even though assets are cached.
            // This should NOT be used to fire OnXAdLoaded events; those only come from
            // explicit LoadAd/placement.Load calls.
            Events.InvokeOnAdFormatAssetsLoaded(adFormat);

            // Instantiate and preload the ad prefab for this format (hidden, prepared)
            PreloadAndPrepareAdInstance(adFormat);
        }

        /// <summary>
        /// Instantiates, preloads, and prepares the ad prefab for the given format. Keeps it hidden and ready for ShowAd.
        /// </summary>
        private static void PreloadAndPrepareAdInstance(AdFormat adFormat)
        {
            // If an instance already exists, ensure it reloads to pick up freshly cached assets
            if (_activePlacements.ContainsKey(adFormat) && _activePlacements[adFormat])
            {
                var existing = _activePlacements[adFormat];
                if (existing)
                {
                    // Reparent to persistent canvas in case it was recreated
                    var targetCanvasExisting = GetOrCreatePersistentAdCanvas();
                    if (targetCanvasExisting && existing.transform.parent != targetCanvasExisting.transform)
                    {
                        existing.transform.SetParent(targetCanvasExisting.transform, false);
                        existing.transform.SetAsLastSibling();
                        if (existing.TryGetComponent(out RectTransform rectTransform))
                        {
                            rectTransform.anchorMin = Vector2.zero;
                            rectTransform.anchorMax = Vector2.one;
                            rectTransform.offsetMin = Vector2.zero;
                            rectTransform.offsetMax = Vector2.zero;
                        }
                        var layerExisting = targetCanvasExisting.gameObject.layer;
                        foreach (var child in existing.GetComponentsInChildren<Transform>(true))
                            child.gameObject.layer = layerExisting;
                    }

                    // Force a reload so placement picks up the newest cached assets (e.g., when ad group changes)
                    if (existing.TryGetComponent(out BannerAdPlacement existingBanner) && adFormat == AdFormat.banner)
                    {
                        existingBanner.Load();
                    }
                    else if (existing.TryGetComponent(out InterstitialAdPlacement existingInterstitial) && adFormat == AdFormat.interstitial)
                    {
                        existingInterstitial.Load();
                    }
                    else if (existing.TryGetComponent(out RewardedAdPlacement existingRewarded) && adFormat == AdFormat.rewarded)
                    {
                        existingRewarded.Load();
                    }
                }
                return; // Instance already present and refreshed
            }

            var instance = adFormat switch
            {
                AdFormat.banner => _bannerPlacementGO,
                AdFormat.interstitial => _interstitialPlacementGO,
                AdFormat.rewarded => _rewardedPlacementGO,
                _ => null
            };
            if (!instance)
            {
                MyDebug.LogError($"[Advertisement] No ad placement instance found for format: {adFormat}");
                return;
            }
            instance.SetActive(false); // Keep hidden until ShowAd

            var targetCanvas = GetOrCreatePersistentAdCanvas();
            if (targetCanvas)
            {
                instance.transform.SetParent(targetCanvas.transform, false);
                instance.transform.SetAsLastSibling();
                if (instance.TryGetComponent(out RectTransform rectTransform))
                {
                    rectTransform.anchorMin = Vector2.zero;
                    rectTransform.anchorMax = Vector2.one;
                    rectTransform.offsetMin = Vector2.zero;
                    rectTransform.offsetMax = Vector2.zero;
                }
            }
            _activePlacements[adFormat] = instance;

            // Preload and prepare video/image asynchronously
            if (instance.TryGetComponent(out BannerAdPlacement banner) && adFormat == AdFormat.banner)
            {
                banner.Load();
            }
            else if (instance.TryGetComponent(out InterstitialAdPlacement interstitial) && adFormat == AdFormat.interstitial)
            {
                interstitial.Load(); // Prepares ad and video in background
            }
            else if (instance.TryGetComponent(out RewardedAdPlacement rewarded) && adFormat == AdFormat.rewarded)
            {
                rewarded.Load(); // Prepares ad and video in background
            }

            var layer = targetCanvas.gameObject.layer;
            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        private static async UniTask<AdGroupSelectResponse> SelectAdGroupAsync(
            AdFormat adFormat, List<string> previousAdGroups, List<string> previousCampaigns)
        {
            if (!SoilServices.Ready)
                throw new SoilException("Soil services are not ready. Please initialize Soil first.", SoilExceptionErrorCode.NotReady);

            var body = new
            {
                format = adFormat.ToString(),
                previous_ad_groups = previousAdGroups ?? new List<string>(),
                previous_campaigns = previousCampaigns ?? new List<string>()
            };
            var jsonBody = JsonConvert.SerializeObject(body);

            using var request = new UnityWebRequest(AdGroupsSelectUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            var authHeader = Authenticate.GetAuthorizationHeaderString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Content-Type", "application/json");

            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout * 2);
            }
            catch (SoilException sx)
            {
                // Preserve specific SoilException types
                throw sx.ErrorCode == SoilExceptionErrorCode.Timeout ? sx : sx;
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while selecting ad group: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            // Map non-success status codes
            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                var responseText = request.downloadHandler?.text ?? "No response content";
                var errorCode = (System.Net.HttpStatusCode)request.responseCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => SoilExceptionErrorCode.InvalidToken,
                    System.Net.HttpStatusCode.Forbidden => SoilExceptionErrorCode.Forbidden,
                    System.Net.HttpStatusCode.NotFound => SoilExceptionErrorCode.NotFound,
                    System.Net.HttpStatusCode.BadRequest => SoilExceptionErrorCode.InvalidRequest,
                    System.Net.HttpStatusCode.ServiceUnavailable => SoilExceptionErrorCode.ServiceUnavailable,
                    _ => SoilExceptionErrorCode.TransportError
                };
                throw new SoilException($"Failed to select ad group: {request.responseCode} - {responseText}", errorCode);
            }

            var content = request.downloadHandler?.text ?? string.Empty;
            try
            {
                if (string.IsNullOrEmpty(content))
                    return new AdGroupSelectResponse { ad_group = null, selection_reason = SelectionReason.only_eligible };

                var result = JsonConvert.DeserializeObject<AdGroupSelectResponse>(content);
                return result;
            }
            catch (Exception)
            {
                MyDebug.LogError($"[Advertisement] Failed to parse ad group response. Treating as no available ad group. Raw: {content}");
                return new AdGroupSelectResponse { ad_group = null, selection_reason = SelectionReason.only_eligible };
            }
        }

        /// <summary>
        /// Selects an ad group for a format, applying frequency capping as a best-effort PREFERENCE
        /// rather than a hard filter. The backend hard-excludes previous_ad_groups/previous_campaigns,
        /// so with limited inventory an accumulated exclusion list would starve selection and return
        /// nothing. We therefore try progressively weaker exclusion sets and stop at the first that
        /// yields an ad group:
        ///   1. cross-session recent history + this round's already-selected apps (best variety)
        ///   2. only this round's already-selected apps (still avoids the same app across formats now)
        ///   3. no exclusions (guarantees an ad whenever any eligible inventory exists)
        /// Duplicate exclusion sets are skipped so we never issue the same request twice.
        /// </summary>
        private static async UniTask<AdGroup> SelectAdGroupWithFrequencyCapAsync(
            AdFormat adFormat,
            List<string> recentAdGroups, List<string> recentCampaigns,
            List<string> roundAdGroups, List<string> roundCampaigns)
        {
            var attempts = new List<(List<string> adGroups, List<string> campaigns)>
            {
                (recentAdGroups.Concat(roundAdGroups).Distinct().ToList(),
                 recentCampaigns.Concat(roundCampaigns).Distinct().ToList()),
                (roundAdGroups, roundCampaigns),
                (new List<string>(), new List<string>()),
            };

            var triedSignatures = new HashSet<string>();
            foreach (var (adGroups, campaigns) in attempts)
            {
                // Skip an attempt whose exclusion set is identical to one we already tried.
                var signature = string.Join(",", adGroups.OrderBy(x => x))
                    + "|" + string.Join(",", campaigns.OrderBy(x => x));
                if (!triedSignatures.Add(signature))
                    continue;

                var response = await SelectAdGroupAsync(adFormat, adGroups, campaigns);
                if (response?.ad_group != null)
                    return response.ad_group;
            }

            return null;
        }

        /// <summary>
        /// Creates or gets the persistent ad canvas that survives scene changes and handles all ad types
        /// </summary>
        private static Canvas GetOrCreatePersistentAdCanvas()
        {
            if (_persistentAdCanvas)
                return _persistentAdCanvas;

            // Create a new root GameObject for the persistent canvas
            var canvasObject = new GameObject("PersistentAdCanvas");
            UnityEngine.Object.DontDestroyOnLoad(canvasObject);

            // Add Canvas component
            _persistentAdCanvas = canvasObject.AddComponent<Canvas>();
            _persistentAdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _persistentAdCanvas.sortingOrder = 1000; // High sorting order to appear above other UI

            // Copy canvas scaler settings from SoilAdManager's canvas reference
            var canvasScaler = canvasObject.AddComponent<CanvasScaler>();
            if (_adPlacementManager && _adPlacementManager.canvasReferences != null)
            {
                canvasScaler.uiScaleMode = _adPlacementManager.canvasReferences.UIScaleMode;
                canvasScaler.referenceResolution = _adPlacementManager.canvasReferences.ReferenceResolution;
                canvasScaler.screenMatchMode = _adPlacementManager.canvasReferences.ScreenMatchMode;
                canvasScaler.matchWidthOrHeight = _adPlacementManager.canvasReferences.MatchWidthOrHeight;
                canvasScaler.referencePixelsPerUnit = _adPlacementManager.canvasReferences.ReferencePixelsPerUnit;

                var layer = _adPlacementManager.canvasReferences.Layer;
                foreach (var child in canvasObject.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = layer;
            }
            else
            {
                // Fallback to default settings
                SetDefaultCanvasScalerSettings(canvasScaler);
            }

            // Add GraphicRaycaster for UI interactions
            canvasObject.AddComponent<GraphicRaycaster>();

            // Start with canvas disabled
            canvasObject.SetActive(false);

            return _persistentAdCanvas;
        }

        /// <summary>
        /// Sets default canvas scaler settings as fallback
        /// </summary>
        private static void SetDefaultCanvasScalerSettings(CanvasScaler canvasScaler)
        {
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1170, 2532);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasScaler.referencePixelsPerUnit = 100;
        }

        /// <summary>
        /// Enables or disables the persistent ad canvas based on active ads
        /// </summary>
        private static void UpdatePersistentCanvasVisibility()
        {
            if (_persistentAdCanvas == null) return;

            bool hasActiveAds = _activePlacements.Any(kvp => kvp.Value != null && kvp.Value.activeSelf);

            if (hasActiveAds && !_persistentAdCanvas.gameObject.activeInHierarchy)
                _persistentAdCanvas.gameObject.SetActive(true);
            else if (!hasActiveAds && _persistentAdCanvas.gameObject.activeInHierarchy)
                _persistentAdCanvas.gameObject.SetActive(false);
        }

        /// <summary>
        /// Checks if rewarded ads are currently in cooldown period
        /// </summary>
        /// <returns>True if in cooldown, false if available</returns>
        public static bool IsRewardedAdInCooldown()
        {
            if (_lastRewardedAdShownTime == DateTime.MinValue)
                return false;

            var timeSinceLastShown = (DateTime.Now - _lastRewardedAdShownTime).TotalSeconds;
            return timeSinceLastShown < RewardedAdCooldownSeconds;
        }

        /// <summary>
        /// Gets the remaining cooldown time for rewarded ads in seconds
        /// </summary>
        /// <returns>Remaining cooldown time in seconds, 0 if no cooldown</returns>
        public static float GetRewardedAdCooldownRemainingSeconds()
        {
            if (_lastRewardedAdShownTime == DateTime.MinValue)
                return 0f;

            var timeSinceLastShown = (DateTime.Now - _lastRewardedAdShownTime).TotalSeconds;
            var remainingTime = RewardedAdCooldownSeconds - timeSinceLastShown;
            return remainingTime > 0 ? (float)remainingTime : 0f;
        }

        /// <summary>
        /// Sets the rewarded ad cooldown timer (called when a rewarded ad is closed)
        /// </summary>
        public static void SetRewardedAdCooldown()
        {
            _lastRewardedAdShownTime = DateTime.Now;
        }

        /// <summary>
        /// Resets the rewarded ad cooldown timer (useful for testing or administrative purposes)
        /// </summary>
        public static void ResetRewardedAdCooldown()
        {
            _lastRewardedAdShownTime = DateTime.MinValue;
        }

        /// <summary>
        /// Helper method to invoke the appropriate error event based on ad format
        /// </summary>
        private static void InvokeAdErrorEvent(AdFormat adFormat, AdEventData errorData)
        {
            switch (adFormat)
            {
                case AdFormat.banner:
                    Events.InvokeOnBannerAdError(errorData);
                    break;
                case AdFormat.interstitial:
                    Events.InvokeOnInterstitialAdError(errorData);
                    break;
                case AdFormat.rewarded:
                    Events.InvokeOnRewardedAdError(errorData);
                    break;
                default:
                    MyDebug.LogError($"Unknown ad format for error event: {adFormat}");
                    break;
            }
        }

        /// <summary>
        /// Preloads the fallback image for interstitial/rewarded ads to prevent blank moments when ShowAd is called
        /// </summary>
        private static void PreloadFallbackImageForAd(GameObject adInstance, AdFormat adFormat)
        {
            if (adInstance == null) return;

            var fallbackImageAsset = GetCachedAsset(adFormat, AssetType.image);
            if (fallbackImageAsset == null) return;

            // Find the AdDisplayComponent and preload the fallback image
            var displayComponent = adInstance.GetComponent<AdDisplayComponent>();
            if (displayComponent != null && displayComponent.rawAssetImage != null)
            {
                var texture = LoadTexture(fallbackImageAsset.Id);
                if (texture != null)
                {
                    displayComponent.rawAssetImage.texture = texture;
                    displayComponent.rawAssetImage.gameObject.SetActive(true);
                    MyDebug.Verbose($"[Advertisement] Preloaded fallback image for {adFormat} ad to prevent blank display");
                }
            }
        }

        /// <summary>
        /// Shows an ad of the specified format if it is ready.
        /// </summary>
        /// <param name="adFormat">The ad format to show (banner, interstitial, rewarded).</param>
        public static void ShowAd(AdFormat adFormat)
        {
            // Check if assets are available for this ad format
            if (!IsFormatReady(adFormat))
            {
                var errorData = new AdEventData(adFormat, AdError.AdNotReady);
                InvokeAdErrorEvent(adFormat, errorData);
                return;
            }

            // Check rewarded ad cooldown
            if (adFormat == AdFormat.rewarded && IsRewardedAdInCooldown())
            {
                var errorData = new AdEventData(adFormat, AdError.AdNotReady);
                InvokeAdErrorEvent(adFormat, errorData);
                return;
            }

            if (!_activePlacements.TryGetValue(adFormat, out GameObject instance) || instance == null)
            {
                // If not preloaded for some reason, preload now
                PreloadAndPrepareAdInstance(adFormat);
                // Preload may fail; use TryGetValue to avoid KeyNotFoundException
                _activePlacements.TryGetValue(adFormat, out instance);
            }
            if (instance == null)
            {
                var errorData = new AdEventData(adFormat, AdError.InternalError);
                InvokeAdErrorEvent(adFormat, errorData);
                return;
            }

            // For interstitial and rewarded ads, preload fallback image immediately to prevent blank moments
            if (adFormat == AdFormat.interstitial || adFormat == AdFormat.rewarded)
            {
                PreloadFallbackImageForAd(instance, adFormat);
            }

            instance.SetActive(true);

            // Update canvas visibility
            UpdatePersistentCanvasVisibility();

            // Show the already-prepared ad (play video or show image)
            if (instance.TryGetComponent(out BannerAdPlacement banner) && adFormat == AdFormat.banner)
            {
                banner.Show();
            }
            else if (instance.TryGetComponent(out InterstitialAdPlacement interstitial) && adFormat == AdFormat.interstitial)
            {
                interstitial.Show(); // Will play video if ready, or show image
            }
            else if (instance.TryGetComponent(out RewardedAdPlacement rewarded) && adFormat == AdFormat.rewarded)
            {
                rewarded.Show(); // Will play video if ready, or show image
                // Cooldown timer is set in the placement's onClose callback
            }
        }

        /// <summary>
        /// Hides an ad of the specified format. Useful for banner ads during gameplay.
        /// </summary>
        /// <param name="adFormat">The ad format to hide (typically banner).</param>
        public static void HideAd(AdFormat adFormat)
        {
            if (_activePlacements.TryGetValue(adFormat, out var instance) && instance != null)
            {
                // Call Hide but keep placement GameObject active so it can reload
                if (instance.TryGetComponent(out BannerAdPlacement banner) && adFormat == AdFormat.banner)
                {
                    banner.Hide();
                    // Don't deactivate - placement needs to stay active for reloading
                }
                else if (instance.TryGetComponent(out InterstitialAdPlacement interstitial) && adFormat == AdFormat.interstitial)
                {
                    interstitial.Hide();
                    // Don't deactivate - placement needs to stay active for reloading
                }
                else if (instance.TryGetComponent(out RewardedAdPlacement rewarded) && adFormat == AdFormat.rewarded)
                {
                    rewarded.Hide();
                    // Don't deactivate - placement needs to stay active for reloading
                }

                UpdatePersistentCanvasVisibility();
            }
        }

        /// <summary>
        /// Loads an ad for the specified format. For optimal user experience, load ads immediately after initialization.
        /// For rewarded ads, if in cooldown, the load will wait until cooldown expires before firing the loaded event.
        /// </summary>
        /// <param name="adFormat">The ad format to load (banner, interstitial, rewarded).</param>
        public static void LoadAd(AdFormat adFormat)
        {
            // Let placements handle their own readiness checks (including rewarded cooldown)
            if (adFormat == AdFormat.banner)
            {
                if (_bannerPlacementGO != null && _bannerPlacementGO.TryGetComponent(out BannerAdPlacement banner))
                    banner.Load();
            }
            else if (adFormat == AdFormat.interstitial)
            {
                if (_interstitialPlacementGO != null && _interstitialPlacementGO.TryGetComponent(out InterstitialAdPlacement interstitial))
                {
                    interstitial.Load();
                    // TODO: Start video preparation here if not already prepared (preload video)
                }
            }
            else if (adFormat == AdFormat.rewarded)
            {
                if (_rewardedPlacementGO != null && _rewardedPlacementGO.TryGetComponent(out RewardedAdPlacement rewarded))
                {
                    rewarded.Load();
                    // TODO: Start video preparation here if not already prepared (preload video)
                }
            }
            else
            {
                var errorData = new AdEventData(adFormat, AdError.InvalidRequest);
                InvokeAdErrorEvent(adFormat, errorData);
                return;
            }
        }
        /// <summary>
        /// Downloads a video from the given URL and caches it locally. Returns the local file path if successful, otherwise null.
        /// </summary>
        public static System.Collections.IEnumerator DownloadAndCacheVideoAsync(string id, string url, Action<string> onComplete)
        {
            string cacheDir = System.IO.Path.Combine(Application.persistentDataPath, "AdVideoCache");
            if (!System.IO.Directory.Exists(cacheDir))
                System.IO.Directory.CreateDirectory(cacheDir);
            string fileName = id + ".mp4";
            string filePath = System.IO.Path.Combine(cacheDir, fileName);
            if (System.IO.File.Exists(filePath) && new System.IO.FileInfo(filePath).Length > 0)
            {
                onComplete?.Invoke(filePath);
                yield break;
            }
            using (var uwr = UnityEngine.Networking.UnityWebRequest.Get(url))
            {
                uwr.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(filePath);
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityEngine.Networking.UnityWebRequest.Result.Success && System.IO.File.Exists(filePath))
                {
                    onComplete?.Invoke(filePath);
                }
                else
                {
                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);
                    onComplete?.Invoke(null);
                }
            }
        }

        /// <summary>
        /// Checks if a video is already cached locally for the given id.
        /// This method now runs file operations on a background thread to avoid UI freezes.
        /// </summary>
        public static async UniTask<bool> IsVideoCachedAsync(string id)
        {
            // Cache the directory path on the main thread before entering background thread
            string cacheDir = System.IO.Path.Combine(Application.persistentDataPath, "AdVideoCache");

            return await UniTask.RunOnThreadPool(() =>
            {
                string fileName = id + ".mp4";
                string filePath = System.IO.Path.Combine(cacheDir, fileName);
                return System.IO.File.Exists(filePath) && new System.IO.FileInfo(filePath).Length > 0;
            });
        }

        /// <summary>
        /// Gets a cached asset by ad format and asset type
        /// </summary>
        /// <param name="adFormat">The ad format (banner, interstitial, rewarded)</param>
        /// <param name="assetType">The asset type (image, video, logo)</param>
        /// <returns>The cached asset or null if not found</returns>
        public static AssetCacheEntry GetCachedAsset(AdFormat adFormat, AssetType assetType)
        {
            return AssetCache.GetCachedAsset(adFormat, assetType);
        }

        /// <summary>
        /// Gets a cached asset by its UUID
        /// </summary>
        /// <param name="uuid">The UUID of the cached asset</param>
        /// <returns>The cached asset or null if not found</returns>
        public static AssetCacheEntry GetCachedAssetByUUID(string uuid)
        {
            return AssetCache.GetCachedAssetByUUID(uuid);
        }

        /// <summary>
        /// Gets all cached assets for a specific ad format
        /// </summary>
        /// <param name="adFormat">The ad format</param>
        /// <returns>List of cached assets for the format</returns>
        public static List<AssetCacheEntry> GetCachedAssets(AdFormat adFormat)
        {
            return AssetCache.GetCachedAssets(adFormat);
        }

        /// <summary>
        /// Gets all cached assets
        /// </summary>
        /// <returns>List of all cached assets</returns>
        public static List<AssetCacheEntry> GetAllCachedAssets()
        {
            return AssetCache.GetAllCachedAssets();
        }

        /// <summary>
        /// Removes a cached asset by UUID
        /// </summary>
        /// <param name="uuid">The UUID of the asset to remove</param>
        /// <returns>True if the asset was removed, false if not found</returns>
        public static bool RemoveCachedAsset(string uuid)
        {
            return AssetCache.RemoveCachedAsset(uuid);
        }

        /// <summary>
        /// Clears all cached assets asynchronously
        /// </summary>
        public static async UniTask ClearAssetCacheAsync()
        {
            await AssetCache.ClearCacheAsync();
            AdvertisementPlayerPrefs.CachedAssets = new List<AssetCacheEntry>();
        }


        /// <summary>
        /// Clears old cached assets based on age (older than specified days)
        /// </summary>
        public static async UniTask ClearOldAssetsAsync(int olderThanDays = 7)
        {
            await AssetCache.ClearOldAssetsAsync(olderThanDays);

            // Update persisted cache
            var remainingAssets = AssetCache.GetAllCachedAssets();
            AdvertisementPlayerPrefs.CachedAssets = remainingAssets;
        }

        /// <summary>
        /// Loads a texture from a cached asset
        /// </summary>
        /// <param name="uuid">The UUID of the cached asset</param>
        /// <returns>Texture2D or null if not found or failed to load</returns>
        public static Texture2D LoadTexture(string uuid)
        {
            return AssetCache.LoadTexture(uuid);
        }

        /// <summary>
        /// Loads a video clip URL from a cached asset for video player
        /// </summary>
        /// <param name="uuid">The UUID of the cached asset</param>
        /// <returns>Video file URL or null if not found</returns>
        public static string LoadVideoUrl(string uuid)
        {
            var asset = AssetCache.GetCachedAssetByUUID(uuid);
            if (asset == null)
                return null;

            if (asset.AssetType != AssetType.video)
                return null;

            // For videos, check if it's a local file or URL
            if (asset.LocalPath.StartsWith("http://") || asset.LocalPath.StartsWith("https://"))
            {
                // Direct URL streaming - return as is
                return asset.LocalPath;
            }
            else if (System.IO.File.Exists(asset.LocalPath))
            {
                // Local cached file - return with file:// protocol for cross-platform compatibility
                var filePath = asset.LocalPath.Replace('\\', '/');
                return "file://" + filePath;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the file path for a cached asset
        /// </summary>
        /// <param name="uuid">The UUID of the cached asset</param>
        /// <returns>File path or null if not found</returns>
        public static string GetAssetPath(string uuid)
        {
            return AssetCache.GetAssetPath(uuid);
        }

        /// <summary>
        /// Initializes the asset cache from persisted data
        /// </summary>
        public static void InitializeAssetCache()
        {
            try
            {
                var persistedAssets = AdvertisementPlayerPrefs.CachedAssets;
                if (persistedAssets != null && persistedAssets.Count > 0)
                {
                    // Validate that cached files still exist
                    var validAssets = persistedAssets.Where(a => a.IsValid).ToList();
                    if (validAssets.Count != persistedAssets.Count)
                    {
                        AdvertisementPlayerPrefs.CachedAssets = validAssets;
                    }
                }
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Failed to initialize asset cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if an ad format is ready to be shown. This checks BOTH that an ad instance
        /// is loaded in the placement AND that all conditions are met (assets available, cooldown cleared for rewarded).
        /// Always check this before calling ShowAd.
        /// </summary>
        /// <param name="adFormat">The ad format to check</param>
        /// <returns>True if the ad can be shown immediately</returns>
        public static bool IsFormatReady(AdFormat adFormat)
        {
            // Step 1: Check if placement instance has a loaded ad ready to show
            if (_activePlacements.TryGetValue(adFormat, out GameObject instance) && instance != null)
            {
                bool instanceReady = adFormat switch
                {
                    AdFormat.banner => instance.TryGetComponent(out BannerAdPlacement banner) && banner.IsReady(),
                    AdFormat.interstitial => instance.TryGetComponent(out InterstitialAdPlacement interstitial) && interstitial.IsReady(),
                    AdFormat.rewarded => instance.TryGetComponent(out RewardedAdPlacement rewarded) && rewarded.IsReady(),
                    _ => false
                };
                
                if (!instanceReady)
                {
                    MyDebug.Verbose($"Format {adFormat} not ready: no loaded ad instance in placement");
                    return false;
                }
            }
            else
            {
                MyDebug.Verbose($"Format {adFormat} not ready: placement instance not found");
                return false;
            }

            // Step 2: For rewarded ads, also check cooldown
            if (adFormat == AdFormat.rewarded && IsRewardedAdInCooldown())
            {
                MyDebug.Verbose($"Format {adFormat} not ready: in cooldown");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the readiness status of all requested ad formats
        /// </summary>
        /// <returns>Dictionary mapping ad formats to their ready status</returns>
        public static Dictionary<AdFormat, bool> GetFormatReadiness()
        {
            var readiness = new Dictionary<AdFormat, bool>();

            if (_requestedFormats != null)
            {
                foreach (var format in _requestedFormats)
                {
                    readiness[format] = IsFormatReady(format);
                }
            }

            return readiness;
        }
    }
}
