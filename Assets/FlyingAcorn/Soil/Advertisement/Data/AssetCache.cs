using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Advertisement.Models;
using UnityEngine;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Data
{
    [Serializable]
    public class AssetCacheEntry
    {
        public string Id;
        public AssetType AssetType;
        public AdFormat AdFormat;
        public string LocalPath;
        public string OriginalUrl;
        public string ClickUrl;
        public int? Width;
        public int? Height;
        public string AltText;
        public DateTime CachedAt;
        
        // Additional Ad-level data stored with the first asset of each format
        public string AdId;
        public string MainHeaderText;
        public string ActionButtonText;
        public string DescriptionText;

        public long FileSize 
        { 
            get 
            { 
                try 
                { 
                    return File.Exists(LocalPath) ? new FileInfo(LocalPath).Length : 0; 
                } 
                catch 
                { 
                    return 0; 
                } 
            } 
        }

        public bool IsValid => 
            !string.IsNullOrEmpty(LocalPath) && 
            (AssetType == AssetType.video 
                ? (LocalPath.StartsWith("http://") || LocalPath.StartsWith("https://")) // Videos: URL validation
                : File.Exists(LocalPath)); // Images/Logos: File existence validation

        public string DisplayName => $"{AdFormat}_{AssetType}_{Id}";

        public override string ToString()
        {
            return $"AssetCacheEntry [UUID: {Id}, Type: {AssetType}, Format: {AdFormat}, Valid: {IsValid}]";
        }
    }

    public static class AssetCache
    {
        private static readonly Dictionary<string, AssetCacheEntry> _cachedAssets = new Dictionary<string, AssetCacheEntry>();
        private static readonly HashSet<string> _currentlyDownloading = new HashSet<string>();
        private static readonly object _lockObject = new object();
        private static readonly string CacheDirectory = Path.Combine(Application.persistentDataPath, "SoilAssets");

        static AssetCache()
        {
            // Ensure cache directory exists
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }
        }

        /// <summary>
        /// Caches assets from a campaign, ensuring only one asset of each type per ad format (random ad group/ad)
        /// </summary>
        public static async Task CacheAssetsAsync(Campaign campaign, List<AdFormat> requestedFormats)
        {
            if (campaign?.ad_groups == null || !campaign.ad_groups.Any())
                return;

            var cachingTasks = new List<Task>();
            var random = new System.Random();

            foreach (var adFormat in requestedFormats)
            {
                // Get all ad groups that have at least one ad of this format
                var eligibleAdGroups = campaign.ad_groups
                    .Where(g => g.allAds != null && g.allAds.Any(a => Enum.TryParse<AdFormat>(a.format, true, out var f) && f == adFormat))
                    .ToList();
                if (!eligibleAdGroups.Any())
                    continue;
                // Pick a random ad group
                var adGroup = eligibleAdGroups[random.Next(eligibleAdGroups.Count)];
                // Get all ads in this group with the requested format
                var eligibleAds = adGroup.allAds
                    .Where(a => Enum.TryParse<AdFormat>(a.format, true, out var f) && f == adFormat)
                    .ToList();
                if (!eligibleAds.Any())
                    continue;
                // Pick a random ad
                var ad = eligibleAds[random.Next(eligibleAds.Count)];
                // Cache one asset of each type for this ad format
                var assetsToCache = GetAssetsToCache(ad, adFormat);
                foreach (var (asset, assetType) in assetsToCache)
                {
                    if (asset?.url != null && !string.IsNullOrEmpty(asset.url))
                    {
                        var cacheKey = GenerateCacheKey(adFormat, assetType, asset.id);
                        cachingTasks.Add(CacheAssetAsync(cacheKey, asset, assetType, adFormat, adGroup.click_url, ad));
                    }
                }
            }
            await Task.WhenAll(cachingTasks);
        }

        /// <summary>
        /// Caches assets for a specific ad format from multiple ads within the same ad group to ensure comprehensive asset coverage.
        /// This approach ensures that video ads have image fallbacks by caching from both video and image ads in the same group.
        /// </summary>
        public static async Task CacheAssetsForFormatAsync(Campaign campaign, AdFormat adFormat, Action<AdFormat> onFormatReady = null)
        {
            MyDebug.Verbose($"Starting CacheAssetsForFormatAsync for {adFormat}");
            
            if (campaign?.ad_groups == null || !campaign.ad_groups.Any())
            {
                MyDebug.LogWarning($"No campaign or ad groups available for {adFormat}");
                onFormatReady?.Invoke(adFormat);
                return;
            }
            
            MyDebug.Verbose($"Campaign has {campaign.ad_groups.Count} ad groups");
            
            var random = new System.Random();
            
            // Get all ad groups that have ads of this format in either image_ads or video_ads
            var eligibleAdGroups = campaign.ad_groups
                .Where(g => HasAdsForFormat(g, adFormat))
                .ToList();
                
            MyDebug.Verbose($"Found {eligibleAdGroups.Count} eligible ad groups for {adFormat}");
                
            if (!eligibleAdGroups.Any())
            {
                MyDebug.LogWarning($"No assets found to cache for {adFormat} format");
                onFormatReady?.Invoke(adFormat);
                return;
            }
            
            // Pick a random ad group
            var adGroup = eligibleAdGroups[random.Next(eligibleAdGroups.Count)];
            
            // Get all ads in this group with the requested format
            var eligibleAds = GetEligibleAdsForFormat(adGroup, adFormat);
            
            if (!eligibleAds.Any())
            {
                MyDebug.LogWarning($"No assets found to cache for {adFormat} format");
                onFormatReady?.Invoke(adFormat);
                return;
            }
            
            var cachingTasks = new List<Task>();
            
            // NEW APPROACH: Cache assets from multiple ads in the same ad group to ensure we have both video and image fallbacks
            // This ensures that even if one ad only has video, another ad in the same group provides the image fallback
            
            // Separate ads by their primary asset type
            var videoAds = eligibleAds.Where(ad => ad.main_video?.url != null).ToList();
            var imageAds = eligibleAds.Where(ad => ad.main_image?.url != null).ToList();
            
            MyDebug.Verbose($"Ad group breakdown - Video ads: {videoAds.Count}, Image ads: {imageAds.Count}");
            
            // Cache from video ad (if available) to get video + any accompanying assets
            if (videoAds.Any())
            {
                var videoAd = videoAds[random.Next(videoAds.Count)];
                MyDebug.Verbose($"Caching assets from video ad: {videoAd.id}");
                var videoAssetsToCache = GetAssetsToCache(videoAd, adFormat);
                
                foreach (var (asset, assetType) in videoAssetsToCache)
                {
                    if (asset?.url != null && !string.IsNullOrEmpty(asset.url))
                    {
                        var cacheKey = GenerateCacheKey(adFormat, assetType, asset.id);
                        cachingTasks.Add(CacheAssetAsync(cacheKey, asset, assetType, adFormat, adGroup.click_url, videoAd));
                    }
                }
            }
            
            // Cache from image ad (if available and different from video ad) to ensure image fallback
            if (imageAds.Any())
            {
                var imageAd = imageAds[random.Next(imageAds.Count)];
                
                // Only cache if it's different from the video ad (avoid duplicates) or if no video ad was processed
                if (!videoAds.Any() || !videoAds.Any(va => va.id == imageAd.id))
                {
                    MyDebug.Verbose($"Caching assets from image ad: {imageAd.id}");
                    var imageAssetsToCache = GetAssetsToCache(imageAd, adFormat);
                    
                    // Track which asset types we're already caching from video ad
                    var existingAssetTypes = new HashSet<AssetType>();
                    if (videoAds.Any())
                    {
                        var videoAssetsToCache = GetAssetsToCache(videoAds.First(), adFormat);
                        existingAssetTypes = videoAssetsToCache.Select(x => x.assetType).ToHashSet();
                    }
                    
                    foreach (var (asset, assetType) in imageAssetsToCache)
                    {
                        if (asset?.url != null && !string.IsNullOrEmpty(asset.url))
                        {
                            // Only cache if we don't already have this asset type from video ad
                            if (!existingAssetTypes.Contains(assetType))
                            {
                                var cacheKey = GenerateCacheKey(adFormat, assetType, asset.id);
                                cachingTasks.Add(CacheAssetAsync(cacheKey, asset, assetType, adFormat, adGroup.click_url, imageAd));
                                MyDebug.Verbose($"Adding {assetType} asset from image ad to ensure fallback coverage");
                            }
                            else
                            {
                                MyDebug.Verbose($"Skipping {assetType} asset - already covered by video ad");
                            }
                        }
                    }
                }
            }
            
            if (cachingTasks.Count > 0)
            {
                await Task.WhenAll(cachingTasks);
                MyDebug.Verbose($"Successfully cached assets for {adFormat} format");
            }
            else
            {
                MyDebug.LogWarning($"No assets found to cache for {adFormat} format");
            }
            
            // Persist the updated cache
            PersistCachedAssets();
            
            // Invoke callback to signal this format is ready
            onFormatReady?.Invoke(adFormat);
        }
        
        /// <summary>
        /// Checks if an ad group has ads for the specified format
        /// </summary>
        private static bool HasAdsForFormat(AdGroup adGroup, AdFormat adFormat)
        {
            // Add debugging to see what we're working with
            MyDebug.Verbose($"Checking ad group for {adFormat} format:");
            MyDebug.Verbose($"  - image_ads count: {adGroup.image_ads?.Count ?? 0}");
            MyDebug.Verbose($"  - video_ads count: {adGroup.video_ads?.Count ?? 0}");
            
            // Debug all format values we find
            if (adGroup.image_ads != null)
            {
                foreach (var ad in adGroup.image_ads)
                {
                    MyDebug.Verbose($"  - image_ad id: {ad.id}, format: '{ad.format ?? "NULL"}'");
                }
            }
            if (adGroup.video_ads != null)
            {
                foreach (var ad in adGroup.video_ads)
                {
                    MyDebug.Verbose($"  - video_ad id: {ad.id}, format: '{ad.format ?? "NULL"}'");
                }
            }
            
            // Check both new structure (image_ads/video_ads) and legacy structure (ads)
            var hasInImageAds = adGroup.image_ads?.Any(a => {
                // If format is null or empty, assume it matches (API might not set format for individual ads)
                if (string.IsNullOrEmpty(a.format))
                {
                    MyDebug.Verbose($"  - image_ad {a.id} has no format, assuming match for {adFormat}");
                    return true;
                }
                var formatMatches = Enum.TryParse<AdFormat>(a.format, true, out var f) && f == adFormat;
                MyDebug.Verbose($"  - image_ad {a.id} format: '{a.format}' -> {f} (matches: {formatMatches})");
                return formatMatches;
            }) == true;
            
            var hasInVideoAds = adGroup.video_ads?.Any(a => {
                // If format is null or empty, assume it matches (API might not set format for individual ads)
                if (string.IsNullOrEmpty(a.format))
                {
                    MyDebug.Verbose($"  - video_ad {a.id} has no format, assuming match for {adFormat}");
                    return true;
                }
                var formatMatches = Enum.TryParse<AdFormat>(a.format, true, out var f) && f == adFormat;
                MyDebug.Verbose($"  - video_ad {a.id} format: '{a.format}' -> {f} (matches: {formatMatches})");
                return formatMatches;
            }) == true;
            
            var result = hasInImageAds || hasInVideoAds;
            MyDebug.Verbose($"HasAdsForFormat({adFormat}): {result} (image: {hasInImageAds}, video: {hasInVideoAds}");
            
            return result;
        }
        
        /// <summary>
        /// Gets eligible ads for the format, including both video and image ads
        /// </summary>
        private static List<Ad> GetEligibleAdsForFormat(AdGroup adGroup, AdFormat adFormat)
        {
            var eligibleAds = new List<Ad>();
            
            MyDebug.Verbose($"Getting eligible ads for {adFormat} format");
            
            // Include both video and image ads for all formats
            // First collect video ads
            if (adGroup.video_ads != null)
            {
                var videoAds = adGroup.video_ads
                    .Where(a => {
                        // If format is null or empty, assume it matches (API might not set format for individual ads)
                        if (string.IsNullOrEmpty(a.format))
                        {
                            MyDebug.Verbose($"  - video_ad {a.id} has no format, including for {adFormat}");
                            return true;
                        }
                        var formatMatches = Enum.TryParse<AdFormat>(a.format, true, out var f) && f == adFormat;
                        MyDebug.Verbose($"  - video_ad {a.id} format: '{a.format}' -> {f} (matches: {formatMatches})");
                        return formatMatches;
                    })
                    .ToList();
                
                if (videoAds.Any())
                {
                    MyDebug.Verbose($"Found {videoAds.Count} matching video ads for {adFormat}");
                    eligibleAds.AddRange(videoAds);
                }
            }
            
            // Then collect image ads
            if (adGroup.image_ads != null)
            {
                var imageAds = adGroup.image_ads
                    .Where(a => {
                        // If format is null or empty, assume it matches (API might not set format for individual ads)
                        if (string.IsNullOrEmpty(a.format))
                        {
                            MyDebug.Verbose($"  - image_ad {a.id} has no format, including for {adFormat}");
                            return true;
                        }
                        var formatMatches = Enum.TryParse<AdFormat>(a.format, true, out var f) && f == adFormat;
                        MyDebug.Verbose($"  - image_ad {a.id} format: '{a.format}' -> {f} (matches: {formatMatches})");
                        return formatMatches;
                    })
                    .ToList();
                
                if (imageAds.Any())
                {
                    MyDebug.Verbose($"Found {imageAds.Count} matching image ads for {adFormat}");
                    eligibleAds.AddRange(imageAds);
                }
            }
            
            MyDebug.Verbose($"Total eligible ads for {adFormat}: {eligibleAds.Count} (including both videos and images)");
            return eligibleAds;
        }

        /// <summary>
        /// Gets the assets to cache for a given ad
        /// IMPORTANT: Videos are no longer cached to reduce storage footprint.
        /// Only images and logos are cached for offline display fallback.
        /// Videos will be streamed directly from URLs when online.
        /// However, we still create cache entries for videos to track metadata.
        /// </summary>
        private static List<(Asset asset, AssetType assetType)> GetAssetsToCache(Ad ad, AdFormat adFormat)
        {
            var assetsToCache = new List<(Asset, AssetType)>();

            MyDebug.Verbose($"GetAssetsToCache for {adFormat} ad {ad.id}:");
            MyDebug.Verbose($"  - main_image: {(ad.main_image?.url != null ? "available" : "null")}");
            MyDebug.Verbose($"  - main_video: {(ad.main_video?.url != null ? "available" : "null")}");
            MyDebug.Verbose($"  - logo: {(ad.logo?.url != null ? "available" : "null")}");

            // Map ad properties to asset types - INCLUDE VIDEO for metadata tracking
            var assetMappings = new Dictionary<AssetType, Asset>
            {
                { AssetType.image, ad.main_image },
                { AssetType.video, ad.main_video }, // Keep for metadata tracking
                { AssetType.logo, ad.logo }
            };

            // Cache images/logos, create metadata entries for videos
            foreach (var (assetType, asset) in assetMappings)
            {
                if (asset?.url != null && !string.IsNullOrEmpty(asset.url))
                {
                    // Determine actual asset type based on URL or asset_type field
                    var actualAssetType = DetermineAssetType(asset);
                    
                    if (actualAssetType == AssetType.image || actualAssetType == AssetType.logo)
                    {
                        // Cache images and logos normally
                        assetsToCache.Add((asset, actualAssetType));
                        MyDebug.Verbose($"  - Will cache {actualAssetType}: {asset.id}");
                    }
                    else if (actualAssetType == AssetType.video)
                    {
                        // Create metadata entry for video (URL will be stored directly, no file download)
                        assetsToCache.Add((asset, actualAssetType));
                        MyDebug.Verbose($"  - Will create metadata entry for video: {asset.id} (streaming only)");
                    }
                }
            }

            MyDebug.Verbose($"Total assets to process for {adFormat}: {assetsToCache.Count}");
            return assetsToCache;
        }

        /// <summary>
        /// Determines the actual asset type based on the asset's properties
        /// </summary>
        private static AssetType DetermineAssetType(Asset asset)
        {
            if (!string.IsNullOrEmpty(asset.asset_type))
            {
                if (Enum.TryParse<AssetType>(asset.asset_type, true, out var assetType))
                {
                    return assetType;
                }
            }

            // Fallback to URL extension detection
            if (!string.IsNullOrEmpty(asset.url))
            {
                var extension = Path.GetExtension(asset.url).ToLower();
                return extension switch
                {
                    ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => AssetType.image,
                    ".mp4" or ".webm" or ".mov" or ".avi" => AssetType.video,
                    _ => AssetType.image // Default to image
                };
            }

            return AssetType.image;
        }

        /// <summary>
        /// Caches a single asset
        /// </summary>
        private static async Task CacheAssetAsync(string cacheKey, Asset asset, AssetType assetType, AdFormat adFormat, string clickUrl = null, Ad ad = null)
        {
            try
            {
                // Thread-safe check for already cached or currently downloading
                lock (_lockObject)
                {
                    if (_cachedAssets.ContainsKey(cacheKey))
                    {
                        MyDebug.Verbose($"Asset already cached: {cacheKey}");
                        return;
                    }

                    if (_currentlyDownloading.Contains(cacheKey))
                    {
                        MyDebug.Verbose($"Asset already being downloaded: {cacheKey}");
                        return;
                    }

                    _currentlyDownloading.Add(cacheKey);
                }

                // Resolve URL (handle relative URLs)
                var resolvedUrl = ResolveAssetUrl(asset.url);
                Analytics.MyDebug.Verbose($"Processing asset {cacheKey} ({assetType}) from URL: {resolvedUrl}");

                // For videos, create metadata entry without downloading
                if (assetType == AssetType.video)
                {
                    Analytics.MyDebug.Verbose($"Creating metadata entry for video {cacheKey} (no download)");
                    
                    var cachedVideoAsset = new AssetCacheEntry
                    {
                        Id = asset.id,
                        AssetType = assetType,
                        AdFormat = adFormat,
                        LocalPath = resolvedUrl, // Store URL directly for streaming
                        OriginalUrl = resolvedUrl,
                        ClickUrl = clickUrl,
                        Width = asset.width,
                        Height = asset.height,
                        AltText = asset.alt_text,
                        CachedAt = DateTime.UtcNow,
                        // Store ad-level data for later use in placements
                        AdId = ad?.id,
                        MainHeaderText = ad?.main_header?.text_content,
                        ActionButtonText = ad?.action_button?.text_content,
                        DescriptionText = ad?.description?.text_content
                    };

                    lock (_lockObject)
                    {
                        _cachedAssets[cacheKey] = cachedVideoAsset;
                        _currentlyDownloading.Remove(cacheKey);
                    }
                    
                    Analytics.MyDebug.Verbose($"Video metadata entry created for {cacheKey}");
                    return;
                }

                // For images and logos, download and cache normally
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                var response = await client.GetAsync(resolvedUrl);
                response.EnsureSuccessStatusCode();

                var data = await response.Content.ReadAsByteArrayAsync();
                
                // Generate unique filename with timestamp to avoid conflicts
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                var originalFileName = Path.GetFileName(resolvedUrl);
                var extension = Path.GetExtension(originalFileName);
                var fileName = $"{cacheKey}_{timestamp}{extension}";
                var filePath = Path.Combine(CacheDirectory, fileName);

                // Ensure the file doesn't exist (additional safety check)
                var counter = 0;
                while (File.Exists(filePath) && counter < 100)
                {
                    fileName = $"{cacheKey}_{timestamp}_{counter}{extension}";
                    filePath = Path.Combine(CacheDirectory, fileName);
                    counter++;
                }

                // Write file with proper error handling
                await WriteFileWithRetry(filePath, data);

                var cachedAsset = new AssetCacheEntry
                {
                    Id = asset.id,
                    AssetType = assetType,
                    AdFormat = adFormat,
                    LocalPath = filePath,
                    OriginalUrl = resolvedUrl, // Store the resolved URL
                    ClickUrl = clickUrl, // Store the click URL from AdGroup
                    Width = asset.width,
                    Height = asset.height,
                    AltText = asset.alt_text,
                    CachedAt = DateTime.UtcNow,
                    // Store ad-level data for later use in placements
                    AdId = ad?.id,
                    MainHeaderText = ad?.main_header?.text_content,
                    ActionButtonText = ad?.action_button?.text_content,
                    DescriptionText = ad?.description?.text_content
                };

                lock (_lockObject)
                {
                    _cachedAssets[cacheKey] = cachedAsset;
                }
                
                // Persist the updated cache to PlayerPrefs
                PersistCachedAssets();

                MyDebug.Verbose($"Successfully cached asset: {cacheKey} -> {cachedAsset.Id}");
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Failed to cache asset {cacheKey}: {ex.Message}");
            }
            finally
            {
                // Always remove from downloading set
                lock (_lockObject)
                {
                    _currentlyDownloading.Remove(cacheKey);
                }
            }
        }

        /// <summary>
        /// Writes a file with retry logic to handle sharing violations
        /// </summary>
        private static async Task WriteFileWithRetry(string filePath, byte[] data, int maxRetries = 3)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    await File.WriteAllBytesAsync(filePath, data);
                    return; // Success, exit method
                }
                catch (IOException ex) when (ex.Message.Contains("sharing violation") || ex.Message.Contains("being used by another process"))
                {
                    if (attempt == maxRetries - 1)
                    {
                        throw; // Re-throw on final attempt
                    }
                    
                    // Wait before retry with exponential backoff
                    await Task.Delay(100 * (attempt + 1));
                }
            }
        }

        /// <summary>
        /// Resolves a URL by completing relative URLs with the AssetsBaseDomain
        /// </summary>
        private static string ResolveAssetUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;

            // Check if URL is already absolute (has protocol)
            if (url.StartsWith("http://") || url.StartsWith("https://"))
                return url;

            // If relative, prepend with AssetsBaseDomain
            var constants = new Constants();
            var baseDomain = constants.AssetsBaseDomain.TrimEnd('/');
            var relativePath = url.TrimStart('/');
            
            return $"{baseDomain}/{relativePath}";
        }

        /// <summary>
        /// Gets a cached asset by ad format and asset type (random)
        /// </summary>
        public static AssetCacheEntry GetCachedAsset(AdFormat adFormat, AssetType assetType)
        {
            var assets = _cachedAssets.Values.Where(a => a.AdFormat == adFormat && a.AssetType == assetType).ToList();
            if (!assets.Any())
            {
                MyDebug.Verbose($"No {assetType} asset found for {adFormat}. Available assets for this format: {_cachedAssets.Values.Count(a => a.AdFormat == adFormat)}");
                // List available assets for this format
                var assetsForFormat = _cachedAssets.Values.Where(a => a.AdFormat == adFormat).ToList();
                foreach (var entry in assetsForFormat)
                {
                    MyDebug.Verbose($"- Available: {entry.AssetType} ({entry.Id})");
                }
                return null;
            }
            // Pick a random asset
            var random = new System.Random();
            var asset = assets[random.Next(assets.Count)];
            MyDebug.Verbose($"Found {assetType} asset for {adFormat}: {asset.Id}");
            return asset;
        }

        /// <summary>
        /// Gets a cached asset by UUID
        /// </summary>
        public static AssetCacheEntry GetCachedAssetByUUID(string uuid)
        {
            var asset = _cachedAssets.Values.FirstOrDefault(a => a.Id == uuid);
            
            if (asset == null)
            {
                MyDebug.LogWarning($"Asset with UUID {uuid} not found in cache. Available assets: {_cachedAssets.Count}");
                foreach (var entry in _cachedAssets.Values.Take(5)) // Show first 5 for debugging
                {
                    MyDebug.Verbose($"- {entry.Id}: {entry.AssetType} for {entry.AdFormat}");
                }
            }
            else
            {
                MyDebug.Verbose($"Found asset {uuid}: {asset.AssetType} for {asset.AdFormat} at {asset.LocalPath}");
            }
            
            return asset;
        }

        /// <summary>
        /// Gets all cached assets for a specific ad format
        /// </summary>
        public static List<AssetCacheEntry> GetCachedAssets(AdFormat adFormat)
        {
            return _cachedAssets.Values.Where(a => a.AdFormat == adFormat).ToList();
        }

        /// <summary>
        /// Gets all cached assets
        /// </summary>
        public static List<AssetCacheEntry> GetAllCachedAssets()
        {
            return _cachedAssets.Values.ToList();
        }

        /// <summary>
        /// Removes a cached asset by UUID
        /// </summary>
        public static bool RemoveCachedAsset(string uuid)
        {
            AssetCacheEntry asset;
            string keyToRemove;
            
            lock (_lockObject)
            {
                asset = GetCachedAssetByUUID(uuid);
                if (asset == null)
                    return false;

                keyToRemove = _cachedAssets.FirstOrDefault(kvp => kvp.Value.Id == uuid).Key;
                if (keyToRemove != null)
                {
                    _cachedAssets.Remove(keyToRemove);
                }
            }

            try
            {
                // Remove file outside of lock
                if (File.Exists(asset.LocalPath))
                {
                    File.Delete(asset.LocalPath);
                }

                MyDebug.Verbose($"Removed cached asset: {uuid}");
                return true;
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Failed to remove cached asset {uuid}: {ex.Message}");
                
                // Re-add to cache if file deletion failed
                lock (_lockObject)
                {
                    if (keyToRemove != null)
                    {
                        _cachedAssets[keyToRemove] = asset;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Clears all cached assets
        /// </summary>
        public static void ClearCache()
        {
            List<AssetCacheEntry> assetsToDelete;
            
            lock (_lockObject)
            {
                assetsToDelete = _cachedAssets.Values.ToList();
                _cachedAssets.Clear();
                _currentlyDownloading.Clear();
            }

            try
            {
                foreach (var asset in assetsToDelete)
                {
                    try
                    {
                        if (File.Exists(asset.LocalPath))
                        {
                            File.Delete(asset.LocalPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        MyDebug.LogWarning($"Failed to delete cached file {asset.LocalPath}: {ex.Message}");
                    }
                }

                // Clean up directory
                if (Directory.Exists(CacheDirectory))
                {
                    try
                    {
                        Directory.Delete(CacheDirectory, true);
                        Directory.CreateDirectory(CacheDirectory);
                    }
                    catch (Exception ex)
                    {
                        MyDebug.LogWarning($"Failed to clean cache directory: {ex.Message}");
                    }
                }

                MyDebug.Verbose("Cleared all cached assets");
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Failed to clear cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads texture from cached asset
        /// </summary>
        public static Texture2D LoadTexture(string uuid)
        {
            var asset = GetCachedAssetByUUID(uuid);
            if (asset == null)
            {
                MyDebug.LogWarning($"Asset not found in cache: {uuid}");
                return null;
            }
            
            // Accept both image and logo assets for texture loading
            if (asset.AssetType != AssetType.image && asset.AssetType != AssetType.logo)
            {
                MyDebug.LogWarning($"Asset {uuid} is not a visual asset (type: {asset.AssetType})");
                return null;
            }

            try
            {
                if (!File.Exists(asset.LocalPath))
                {
                    MyDebug.LogError($"Asset file not found: {asset.LocalPath}");
                    return null;
                }

                var data = File.ReadAllBytes(asset.LocalPath);
                if (data == null || data.Length == 0)
                {
                    MyDebug.LogError($"Asset file is empty or could not be read: {asset.LocalPath}");
                    return null;
                }

                var texture = new Texture2D(2, 2);
                
                if (texture.LoadImage(data))
                {
                    MyDebug.Verbose($"Successfully loaded texture {uuid} ({texture.width}x{texture.height}) - {asset.AssetType}");
                    return texture;
                }
                
                MyDebug.LogError($"Failed to decode image data for asset {uuid}");
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Failed to load texture for asset {uuid}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the file path for a cached asset
        /// </summary>
        public static string GetAssetPath(string uuid)
        {
            var asset = GetCachedAssetByUUID(uuid);
            return asset?.LocalPath;
        }

        /// <summary>
        /// Generates a cache key for an asset
        /// </summary>
        private static string GenerateCacheKey(AdFormat adFormat, AssetType assetType, string assetId)
        {
            return $"{adFormat}_{assetType}_{assetId ?? "default"}";
        }

        /// <summary>
        /// Persists cached assets to PlayerPrefs
        /// </summary>
        public static void PersistCachedAssets()
        {
            try
            {
                var assets = GetAllCachedAssets();
                AdvertisementPlayerPrefs.CachedAssets = assets;
                MyDebug.Verbose($"Persisted {assets.Count} cached assets to PlayerPrefs");
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Failed to persist cached assets: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads cached assets from PlayerPrefs
        /// </summary>
        public static void LoadCachedAssets()
        {
            try
            {
                var persistedAssets = AdvertisementPlayerPrefs.CachedAssets;
                if (persistedAssets != null && persistedAssets.Count > 0)
                {
                    lock (_lockObject)
                    {
                        // Clear current cache
                        _cachedAssets.Clear();
                        
                        // Load valid assets
                        foreach (var asset in persistedAssets.Where(a => a.IsValid))
                        {
                            var cacheKey = GenerateCacheKey(asset.AdFormat, asset.AssetType, asset.Id);
                            _cachedAssets[cacheKey] = asset;
                        }
                    }
                    
                    MyDebug.Verbose($"Loaded {_cachedAssets.Count} cached assets from PlayerPrefs");
                }
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Failed to load cached assets: {ex.Message}");
            }
        }
    }
}
