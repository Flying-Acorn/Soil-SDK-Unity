using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
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
        public static bool Ready => SoilServices.Ready;
        private static readonly string AdvertisementBaseUrl = $"{Core.Data.Constants.ApiUrl}/advertisement/";
        private static readonly string CampaignsUrl = $"{AdvertisementBaseUrl}campaigns/";
        private static readonly string CampaignsSelectUrl = $"{CampaignsUrl}select/";
        private static bool _campaignRequested;
        private static Campaign availableCampaign = null;
        private static List<AdFormat> _requestedFormats;
        
        // Ad placement instances
        private static SoilAdManager _adPlacementManager;
        private static BannerAdPlacement _bannerPlacement;
        private static InterstitialAdPlacement _interstitialPlacement;
        private static RewardedAdPlacement _rewardedPlacement;

        // Shared canvas for all ad placements
        private static Canvas _sharedAdCanvas;

        public static async void InitializeAsync(List<AdFormat> adFormats)
        {
            if (adFormats == null || adFormats.Count == 0)
                throw new SoilException("Ad formats cannot be null or empty.", SoilExceptionErrorCode.InvalidRequest);

            if (availableCampaign != null)
                return;

            _requestedFormats = adFormats?.Distinct().ToList() ?? new List<AdFormat>();

            // Create ad placement manager if it doesn't exist
            AssignAdPlacementManager();

            // Load cached assets from previous session
            AssetCache.LoadCachedAssets();

            try
            {
                await SoilServices.Initialize();
            }
            catch (Exception e)
            {
                Events.InvokeOnInitializeFailed(e.Message);
                return;
            }

            if (_campaignRequested)
                return;

            _campaignRequested = true;
            try
            {
                availableCampaign = (await SelectCampaignAsync()).campaign;
            }
            catch (SoilException ex)
            {
                _campaignRequested = false;
                Events.InvokeOnInitializeFailed($"Failed to select campaign: {ex.Message} - {ex.ErrorCode}");
                return;
            }
            catch (Exception ex)
            {
                _campaignRequested = false;
                Events.InvokeOnInitializeFailed($"Unexpected error while initializing advertisement: {ex.Message}");
                return;
            }

            Events.InvokeOnInitialized();
            CacheAds(availableCampaign);
            _campaignRequested = false;
        }

        /// <summary>
        /// Creates the persistent ad placement manager GameObject with all ad placements as children
        /// </summary>
        private static void AssignAdPlacementManager()
        {
            if (_adPlacementManager != null)
            {
                Analytics.MyDebug.Info("Ad placement manager already exists. Reusing existing instance.");
                return;
            }
            _adPlacementManager = UnityEngine.Object.FindObjectOfType<SoilAdManager>();
            if (_adPlacementManager == null)
            {
                throw new SoilException("SoilAdManager not found in the scene. Please add it to your scene before initializing Advertisement.",
                    SoilExceptionErrorCode.NotFound);
            }
            _bannerPlacement = _adPlacementManager.GetComponentInChildren<BannerAdPlacement>();
            _interstitialPlacement = _adPlacementManager.GetComponentInChildren<InterstitialAdPlacement>();
            _rewardedPlacement = _adPlacementManager.GetComponentInChildren<RewardedAdPlacement>();
            UnityEngine.Object.DontDestroyOnLoad(_adPlacementManager);

            AssignSharedAdCanvas();
        }

        private static void AssignSharedAdCanvas()
        {
            _sharedAdCanvas = _adPlacementManager.GetComponent<Canvas>();
            _sharedAdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _sharedAdCanvas.sortingOrder = 1000; // High sorting order to appear on top
            
            CanvasScaler canvasScaler = _sharedAdCanvas.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);

            _bannerPlacement.DisplayComponent.SetCanvas(_sharedAdCanvas);
            _interstitialPlacement.DisplayComponent.SetCanvas(_sharedAdCanvas);
            _rewardedPlacement.DisplayComponent.SetCanvas(_sharedAdCanvas);

            _sharedAdCanvas.enabled = false;
        }

        // Downloads and caches ads for the selected campaign.
        // This method caches each format separately and invokes events as each format becomes ready.
        private static async void CacheAds(Campaign availableCampaign)
        {
            AdvertisementPlayerPrefs.CachedCampaign = availableCampaign;

            // Cache assets for each requested format separately
            var cachingTasks = new List<Task>();

            foreach (var adFormat in _requestedFormats)
            {
                // Start caching task for this format
                var formatTask = CacheFormatAssetsAsync(availableCampaign, adFormat);
                cachingTasks.Add(formatTask);
            }

            // Wait for all formats to complete (though each will fire events individually)
            try
            {
                await Task.WhenAll(cachingTasks);
                MyDebug.Info($"Completed caching for all {_requestedFormats.Count} requested formats");
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Error during asset caching: {ex.Message}");
            }
        }

        /// <summary>
        /// Caches assets for a specific ad format and invokes the loaded event when ready
        /// </summary>
        private static async Task CacheFormatAssetsAsync(Campaign campaign, AdFormat adFormat)
        {
            try
            {
                await AssetCache.CacheAssetsForFormatAsync(campaign, adFormat, OnFormatAssetsReady);
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Failed to cache assets for {adFormat} format: {ex}");
                // Still invoke the event even if caching failed, so the app can handle the error state
                OnFormatAssetsReady(adFormat);
            }
        }

        /// <summary>
        /// Called when assets for a specific format are ready
        /// </summary>
        private static void OnFormatAssetsReady(AdFormat adFormat)
        {
            MyDebug.Info($"Assets ready for {adFormat} format");
            Events.InvokeOnAdFormatAssetsLoaded(adFormat);
        }

        private static async Task<CampaignSelectResponse> SelectCampaignAsync()
        {
            if (!Ready)
            {
                throw new SoilException("Soil services are not ready. Please initialize Soil first.",
                    SoilExceptionErrorCode.NotReady);
            }

            using var campaignRequest = new HttpClient();
            campaignRequest.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            campaignRequest.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            campaignRequest.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Post, CampaignsSelectUrl);

            var body = new
            {
                previous_campaigns = new List<string>() // TODO: Get previous campaigns from user preferences or session
            };

            request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            string content;

            try
            {
                response = await campaignRequest.SendAsync(request);
                content = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new SoilException("Request timed out while selecting campaign",
                    SoilExceptionErrorCode.Timeout);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while selecting campaign: {ex.Message}",
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while selecting campaign: {ex.Message}",
                    SoilExceptionErrorCode.Unknown);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorCode = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => SoilExceptionErrorCode.InvalidToken,
                    System.Net.HttpStatusCode.Forbidden => SoilExceptionErrorCode.Forbidden,
                    System.Net.HttpStatusCode.NotFound => SoilExceptionErrorCode.NotFound,
                    System.Net.HttpStatusCode.BadRequest => SoilExceptionErrorCode.InvalidRequest,
                    System.Net.HttpStatusCode.ServiceUnavailable => SoilExceptionErrorCode.ServiceUnavailable,
                    _ => SoilExceptionErrorCode.TransportError
                };

                throw new SoilException($"Failed to select campaign: {response.StatusCode} - {content}", errorCode);
            }

            try
            {
                return JsonConvert.DeserializeObject<CampaignSelectResponse>(content);
            }
            catch (Exception)
            {
                throw new SoilException($"Invalid response format while selecting campaign. Response: {content}",
                    SoilExceptionErrorCode.InvalidResponse);
            }
        }

        public static void LoadAd(AdFormat adFormat)
        {
            if (adFormat == AdFormat.banner)
            {
                _bannerPlacement.Load();
            }
            else if (adFormat == AdFormat.interstitial)
            {
                _interstitialPlacement.Load();
            }
            else if (adFormat == AdFormat.rewarded)
            {
                _rewardedPlacement.Load();
            }
            else
            {
                throw new SoilException($"Unsupported ad format: {adFormat}", SoilExceptionErrorCode.InvalidRequest);
            }
        }

        public static void ShowAd(AdFormat adFormat)
        {
            if (adFormat == AdFormat.banner)
            {
                _bannerPlacement.Show();
            }
            else if (adFormat == AdFormat.interstitial)
            {
                _interstitialPlacement.Show();
            }
            else if (adFormat == AdFormat.rewarded)
            {
                _rewardedPlacement.Show();
            }
            else
            {
                throw new SoilException($"Unsupported ad format: {adFormat}", SoilExceptionErrorCode.InvalidRequest);
            }
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
        /// Clears all cached assets
        /// </summary>
        public static void ClearAssetCache()
        {
            AssetCache.ClearCache();
            AdvertisementPlayerPrefs.CachedAssets = new List<AssetCacheEntry>();
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
                        // Some assets are no longer valid, update the cache
                        AdvertisementPlayerPrefs.CachedAssets = validAssets;
                        MyDebug.Info($"Cleaned up {persistedAssets.Count - validAssets.Count} invalid cached assets");
                    }
                    MyDebug.Info($"Initialized asset cache with {validAssets.Count} valid assets");
                }
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Failed to initialize asset cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if assets for a specific ad format are cached and ready
        /// </summary>
        /// <param name="adFormat">The ad format to check</param>
        /// <returns>True if assets are cached for this format</returns>
        public static bool IsFormatReady(AdFormat adFormat)
        {
            var assets = AssetCache.GetCachedAssets(adFormat);
            return assets != null && assets.Count > 0;
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
