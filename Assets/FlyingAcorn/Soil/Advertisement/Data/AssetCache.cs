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

        public bool IsValid => !string.IsNullOrEmpty(LocalPath) && File.Exists(LocalPath);

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
                    .Where(g => g.ads != null && g.ads.Any(a => Enum.TryParse<AdFormat>(a.format, true, out var f) && f == adFormat))
                    .ToList();
                if (!eligibleAdGroups.Any())
                    continue;
                // Pick a random ad group
                var adGroup = eligibleAdGroups[random.Next(eligibleAdGroups.Count)];
                // Get all ads in this group with the requested format
                var eligibleAds = adGroup.ads
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
        /// Caches assets for a specific ad format and invokes callback when complete (random ad group/ad)
        /// </summary>
        public static async Task CacheAssetsForFormatAsync(Campaign campaign, AdFormat adFormat, Action<AdFormat> onFormatReady = null)
        {
            if (campaign?.ad_groups == null || !campaign.ad_groups.Any())
            {
                onFormatReady?.Invoke(adFormat);
                return;
            }
            var random = new System.Random();
            // Get all ad groups that have at least one ad of this format
            var eligibleAdGroups = campaign.ad_groups
                .Where(g => g.ads != null && g.ads.Any(a => Enum.TryParse<AdFormat>(a.format, true, out var f) && f == adFormat))
                .ToList();
            if (!eligibleAdGroups.Any())
            {
                MyDebug.LogWarning($"No assets found to cache for {adFormat} format");
                onFormatReady?.Invoke(adFormat);
                return;
            }
            // Pick a random ad group
            var adGroup = eligibleAdGroups[random.Next(eligibleAdGroups.Count)];
            // Get all ads in this group with the requested format
            var eligibleAds = adGroup.ads
                .Where(a => Enum.TryParse<AdFormat>(a.format, true, out var f) && f == adFormat)
                .ToList();
            if (!eligibleAds.Any())
            {
                MyDebug.LogWarning($"No assets found to cache for {adFormat} format");
                onFormatReady?.Invoke(adFormat);
                return;
            }
            // Pick a random ad
            var ad = eligibleAds[random.Next(eligibleAds.Count)];
            var cachingTasks = new List<Task>();
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
        /// Gets the assets to cache for a given ad
        /// </summary>
        private static List<(Asset asset, AssetType assetType)> GetAssetsToCache(Ad ad, AdFormat adFormat)
        {
            var assetsToCache = new List<(Asset, AssetType)>();

            // Map ad properties to asset types
            var assetMappings = new Dictionary<AssetType, Asset>
            {
                { AssetType.image, ad.main_image },
                { AssetType.video, ad.main_image }, // Assuming main_image can be video too
                { AssetType.logo, ad.logo }
            };

            // Only cache assets that have URLs and are media assets (image/video/logo)
            foreach (var (assetType, asset) in assetMappings)
            {
                if (asset?.url != null && !string.IsNullOrEmpty(asset.url))
                {
                    // Determine actual asset type based on URL or asset_type field
                    var actualAssetType = DetermineAssetType(asset);
                    if (actualAssetType == AssetType.image || actualAssetType == AssetType.video || actualAssetType == AssetType.logo)
                    {
                        assetsToCache.Add((asset, actualAssetType));
                    }
                }
            }

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
                Analytics.MyDebug.Verbose($"Caching asset {cacheKey} from URL: {resolvedUrl}");

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
                MyDebug.LogWarning($"No {assetType} asset found for {adFormat}. Available assets for this format: {_cachedAssets.Values.Count(a => a.AdFormat == adFormat)}");
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
