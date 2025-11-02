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
        [UsedImplicitly]
        public static bool Ready => _campaignSelectionSucceeded;
        private static string AdvertisementBaseUrl => $"{Core.Data.Constants.ApiUrl}/advertisement/";
        private static string CampaignsUrl => $"{AdvertisementBaseUrl}campaigns/";
        private static string CampaignsSelectUrl => $"{CampaignsUrl}select/";
        private static bool _campaignRequested;
        private static bool _isInitializing;
        private static Campaign availableCampaign = null;
        private static bool _campaignSelectionSucceeded;
        private static List<AdFormat> _requestedFormats;
        private static UniTask _cachedAssetsTask;

        // Ad placement instances
        private static SoilAdManager _adPlacementManager;
        private static BannerAdPlacement _bannerPlacement;
        private static InterstitialAdPlacement _interstitialPlacement;
        private static RewardedAdPlacement _rewardedPlacement;

        // Persistent canvas for all ad placements
        private static Canvas _persistentAdCanvas;

        // Track active ad placement instances
        private static readonly Dictionary<AdFormat, GameObject> _activePlacements = new();

        // Rewarded ad cooldown tracking
        private static DateTime _lastRewardedAdShownTime = DateTime.MinValue;
        private static readonly float RewardedAdCooldownSeconds = 10f;

        public static void InitializeAsync(List<AdFormat> adFormats)
        {
            if (adFormats == null || adFormats.Count == 0)
            {
                Events.InvokeOnInitializeFailed("No ad formats specified for initialization");
                return;
            }

            if (availableCampaign != null)
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
            try
            {
                var campaignResponse = await SelectCampaignAsync();
                if (campaignResponse?.campaign == null)
                {
                    await ClearAssetCacheAsync();
                    availableCampaign = null;
                    AdvertisementPlayerPrefs.CachedCampaign = null;
                    _campaignSelectionSucceeded = true; // success path (no active campaign available)
                    _campaignRequested = false;
                    _isInitializing = false;
                    Events.InvokeOnInitialized();
                    return;
                }
                availableCampaign = campaignResponse.campaign;
                _campaignSelectionSucceeded = true; // success path (campaign obtained)
            }
            catch (SoilException ex)
            {
                _campaignRequested = false;
                _isInitializing = false;
                _campaignSelectionSucceeded = false; // failed attempt
                Events.InvokeOnInitializeFailed($"Failed to select campaign: {ex.Message} - {ex.ErrorCode}");
                return;
            }
            catch (Exception ex)
            {
                _campaignRequested = false;
                _isInitializing = false;
                _campaignSelectionSucceeded = false; // failed attempt
                Events.InvokeOnInitializeFailed($"Unexpected error selecting campaign: {ex.Message}");
                return;
            }
            GetOrCreatePersistentAdCanvas();
            Events.InvokeOnInitialized();
            // Start asset caching in background - don't block initialization on this
            CacheAds(availableCampaign).Forget();
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

            _adPlacementManager = UnityEngine.Object.FindObjectOfType<SoilAdManager>();
            if (_adPlacementManager == null)
                throw new SoilException("SoilAdManager not found in the scene. Please add it to your scene before initializing Advertisement.",
                    SoilExceptionErrorCode.NotFound);
            _bannerPlacement = _adPlacementManager.bannerAdPlacement;
            _interstitialPlacement = _adPlacementManager.interstitialAdPlacement;
            _rewardedPlacement = _adPlacementManager.rewardedAdPlacement;
        }

        // Downloads and caches ads for the selected campaign.
        // This method caches each format separately and invokes events as each format becomes ready.
        private static async UniTask CacheAds(Campaign availableCampaign)
        {
            // Simple and reliable cache management
            bool isDifferentCampaign = AdvertisementPlayerPrefs.CachedCampaign?.id != availableCampaign.id;

            if (isDifferentCampaign)
            {
                // Clear all for different campaigns - simple and predictable
                await ClearAssetCacheAsync();
            }

            AdvertisementPlayerPrefs.CachedCampaign = availableCampaign;

            // Pre-filter requested formats to only include those that are actually available in the campaign
            var availableFormats = GetAvailableFormatsInCampaign(availableCampaign, _requestedFormats);

            if (!availableFormats.Any())
            {
                MyDebug.LogWarning("No requested ad formats are available in the selected campaign");
                return;
            }

            MyDebug.Verbose($"Caching assets for available formats: {string.Join(", ", availableFormats)}");

            // Cache assets for each AVAILABLE format separately
            var cachingTasks = new List<UniTask>();

            foreach (var adFormat in availableFormats) // ← Now only processes available formats
            {
                // Start caching task for this format
                var formatTask = CacheFormatAssetsAsync(availableCampaign, adFormat);
                cachingTasks.Add(formatTask);
            }

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
        /// Pre-filters requested formats to only include those that are actually available in the campaign
        /// </summary>
        private static List<AdFormat> GetAvailableFormatsInCampaign(Campaign campaign, List<AdFormat> requestedFormats)
        {
            if (campaign?.ad_groups == null || !campaign.ad_groups.Any())
                return new List<AdFormat>();

            var availableFormats = new List<AdFormat>();

            foreach (var requestedFormat in requestedFormats)
            {
                // Check if any ad group has ads for this format

                bool formatAvailable = campaign.ad_groups.Any(adGroup =>
                    AssetCache.HasAdsForFormat(adGroup, requestedFormat));

                if (formatAvailable)
                    availableFormats.Add(requestedFormat);
            }

            return availableFormats;
        }

        /// <summary>
        /// Caches assets for a specific ad format and invokes the loaded event when ready
        /// </summary>
        private static async UniTask CacheFormatAssetsAsync(Campaign campaign, AdFormat adFormat)
        {
            await AssetCache.CacheAssetsForFormatAsync(campaign, adFormat, OnFormatAssetsReady);
        }

        /// <summary>
        /// Called when assets for a specific format are ready
        /// </summary>
        private static void OnFormatAssetsReady(AdFormat adFormat)
        {
            // For rewarded ads, only invoke loaded if not in cooldown
            if (adFormat == AdFormat.rewarded && IsRewardedAdInCooldown())
            {
                MyDebug.Verbose($"[OnFormatAssetsReady] Rewarded ad is in cooldown, not firing loaded event.");
                return;
            }
            if (IsFormatReady(adFormat))
            {
                // Instantiate and preload the ad prefab for this format (hidden, prepared)
                PreloadAndPrepareAdInstance(adFormat);
                Events.InvokeOnAdFormatAssetsLoaded(adFormat);
            }
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
                        _bannerPlacement = existingBanner;
                        if (_bannerPlacement != null)
                        {
                            _bannerPlacement.Load();
                        }
                    }
                    else if (existing.TryGetComponent(out InterstitialAdPlacement existingInterstitial) && adFormat == AdFormat.interstitial)
                    {
                        _interstitialPlacement = existingInterstitial;
                        if (_interstitialPlacement != null)
                        {
                            _interstitialPlacement.Load();
                        }
                    }
                    else if (existing.TryGetComponent(out RewardedAdPlacement existingRewarded) && adFormat == AdFormat.rewarded)
                    {
                        _rewardedPlacement = existingRewarded;
                        if (_rewardedPlacement != null)
                        {
                            _rewardedPlacement.Load();
                        }
                    }
                }
                return; // Instance already present and refreshed
            }

            var instance = adFormat switch
            {
                AdFormat.banner => _bannerPlacement?.gameObject,
                AdFormat.interstitial => _interstitialPlacement?.gameObject,
                AdFormat.rewarded => _rewardedPlacement?.gameObject,
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
                _bannerPlacement = banner;
                _bannerPlacement.Load();
            }
            else if (instance.TryGetComponent(out InterstitialAdPlacement interstitial) && adFormat == AdFormat.interstitial)
            {
                _interstitialPlacement = interstitial;
                _interstitialPlacement.Load(); // Prepares ad and video in background
            }
            else if (instance.TryGetComponent(out RewardedAdPlacement rewarded) && adFormat == AdFormat.rewarded)
            {
                _rewardedPlacement = rewarded;
                _rewardedPlacement.Load(); // Prepares ad and video in background
            }

            var layer = targetCanvas.gameObject.layer;
            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        private static async UniTask<CampaignSelectResponse> SelectCampaignAsync()
        {
            if (!SoilServices.Ready)
                throw new SoilException("Soil services are not ready. Please initialize Soil first.", SoilExceptionErrorCode.NotReady);

            var body = new { previous_campaigns = new List<string>() }; // TODO: Populate from prefs/session
            var jsonBody = JsonConvert.SerializeObject(body);

            using var request = new UnityWebRequest(CampaignsSelectUrl, UnityWebRequest.kHttpVerbPOST)
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
                throw new SoilException($"Unexpected error while selecting campaign: {ex.Message}", SoilExceptionErrorCode.TransportError);
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
                throw new SoilException($"Failed to select campaign: {request.responseCode} - {responseText}", errorCode);
            }

            var content = request.downloadHandler?.text ?? string.Empty;
            try
            {
                if (string.IsNullOrEmpty(content))
                    return new CampaignSelectResponse { campaign = null, selection_reason = SelectionReason.only_eligible };

                var result = JsonConvert.DeserializeObject<CampaignSelectResponse>(content);
                return result;
            }
            catch (Exception)
            {
                MyDebug.LogError($"[Advertisement] Failed to parse campaign response. Treating as no active campaign. Raw: {content}");
                return new CampaignSelectResponse { campaign = null, selection_reason = SelectionReason.only_eligible };
            }
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
                instance = _activePlacements[adFormat];
            }
            if (instance == null)
            {
                var errorData = new AdEventData(adFormat, AdError.InternalError);
                InvokeAdErrorEvent(adFormat, errorData);
                return;
            }
            if (instance.activeInHierarchy)
                return;

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
                _bannerPlacement = banner;
                _bannerPlacement.Show();
            }
            else if (instance.TryGetComponent(out InterstitialAdPlacement interstitial) && adFormat == AdFormat.interstitial)
            {
                _interstitialPlacement = interstitial;
                _interstitialPlacement.Show(); // Will play video if ready, or show image
            }
            else if (instance.TryGetComponent(out RewardedAdPlacement rewarded) && adFormat == AdFormat.rewarded)
            {
                _rewardedPlacement = rewarded;
                _rewardedPlacement.Show(); // Will play video if ready, or show image
                // Track when rewarded ad was shown for cooldown
                _lastRewardedAdShownTime = DateTime.Now;
            }
        }

        public static void HideAd(AdFormat adFormat)
        {
            if (_activePlacements.TryGetValue(adFormat, out var instance) && instance != null)
            {
                // Call Hide and clear placement reference
                if (instance.TryGetComponent(out BannerAdPlacement banner) && adFormat == AdFormat.banner)
                {
                    banner.Hide();
                    _bannerPlacement = null;
                    instance.SetActive(false);
                }
                else if (instance.TryGetComponent(out InterstitialAdPlacement interstitial) && adFormat == AdFormat.interstitial)
                {
                    interstitial.Hide();
                    _interstitialPlacement = null;
                    instance.SetActive(false);
                }
                else if (instance.TryGetComponent(out RewardedAdPlacement rewarded) && adFormat == AdFormat.rewarded)
                {
                    rewarded.Hide();
                    _rewardedPlacement = null;
                    instance.SetActive(false);

                    _lastRewardedAdShownTime = DateTime.Now;
                }

                UpdatePersistentCanvasVisibility();
            }
        }

        public static void LoadAd(AdFormat adFormat)
        {
            // Check rewarded ad cooldown only (don't block loading for other formats)
            if (adFormat == AdFormat.rewarded && IsRewardedAdInCooldown())
            {
                var errorData = new AdEventData(adFormat, AdError.AdNotReady);
                InvokeAdErrorEvent(adFormat, errorData);
                return;
            }

            if (adFormat == AdFormat.banner)
            {
                if (_bannerPlacement != null)
                    _bannerPlacement.Load();
            }
            else if (adFormat == AdFormat.interstitial)
            {
                if (_interstitialPlacement != null)
                {
                    _interstitialPlacement.Load();
                    // TODO: Start video preparation here if not already prepared (preload video)
                }
            }
            else if (adFormat == AdFormat.rewarded)
            {
                if (_rewardedPlacement != null)
                {
                    _rewardedPlacement.Load();
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
        /// Checks if assets for a specific ad format are cached and ready
        /// For rewarded ads, also checks cooldown period
        /// IMPORTANT: Every ad format must have at least one cached image before being considered ready,
        /// even for video ads. This ensures a fallback image is available while video loads/renders.
        /// </summary>
        /// <param name="adFormat">The ad format to check</param>
        /// <returns>True if assets are cached for this format and not in cooldown</returns>
        public static bool IsFormatReady(AdFormat adFormat)
        {
            var assets = AssetCache.GetCachedAssets(adFormat);
            // Step 1: Must have at least one cached image asset
            var hasImageAsset = assets != null && assets.Any(asset => asset.AssetType == AssetType.image);
            if (!hasImageAsset)
            {
                MyDebug.Verbose($"Format {adFormat} not ready: no cached image asset found (required for fallback)");
                return false;
            }

            // Step 2: If the current campaign has a video asset for this format, it must be cached (partly or fully)
            bool campaignHasVideo = false;
            if (availableCampaign != null && availableCampaign.ad_groups != null)
            {
                foreach (var adGroup in availableCampaign.ad_groups)
                {
                    // Check all ads in image_ads and video_ads for this format
                    var allAds = adGroup.allAds;
                    if (allAds != null && allAds.Any(ad => ad.format == adFormat.ToString() && ad.main_video != null))
                    {
                        campaignHasVideo = true;
                        break;
                    }
                }
            }
            if (campaignHasVideo)
            {
                var hasVideoAsset = assets.Any(asset => asset.AssetType == AssetType.video && asset.IsValid);
                if (!hasVideoAsset)
                {
                    MyDebug.Verbose($"Format {adFormat} not ready: campaign has video asset but none cached");
                    return false;
                }
            }

            // Step 3: For rewarded ads, also check cooldown
            if (adFormat == AdFormat.rewarded)
            {
                return !IsRewardedAdInCooldown();
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
