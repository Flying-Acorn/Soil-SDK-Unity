using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Advertisement.Models;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.Data; // DataUtils
using UnityEngine;
using UnityEngine.Networking;
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

        // Every asset, videos included, is a downloaded file. Entries from older SDK versions that
        // stored a video's streaming URL here are invalid and get downloaded again.
        public bool IsValid => !string.IsNullOrEmpty(LocalPath) && File.Exists(LocalPath);

        public string DisplayName => $"{AdFormat}_{AssetType}_{Id}";

        public override string ToString()
        {
            return $"AssetCacheEntry [UUID: {Id}, Type: {AssetType}, Format: {AdFormat}, Valid: {IsValid}]";
        }
    }

    public static class AssetCache
    {
        private static readonly Dictionary<string, AssetCacheEntry> _cachedAssets = new();
        private static readonly HashSet<string> _currentlyDownloading = new();
        private static readonly object _lockObject = new();
        private static string CacheDirectory => Path.Combine(Application.persistentDataPath, "SoilAssets");

        static AssetCache()
        {
            // Ensure cache directory exists
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }
        }

        /// <summary>
        /// Caches assets for a specific ad format from multiple ads within the same ad group to ensure comprehensive asset coverage.
        /// This approach ensures that video ads have image fallbacks by caching from both video and image ads in the same group.
        /// The ad group itself is expected to already be selected (server-side, weighted) for this format.
        /// </summary>
        public static async UniTask CacheAssetsForAdGroupAsync(AdGroup adGroup, AdFormat adFormat, Action<AdFormat> onFormatReady = null)
        {
            MyDebug.Verbose($"Starting CacheAssetsForAdGroupAsync for {adFormat}");

            if (adGroup == null || !HasAdsForFormat(adGroup, adFormat))
            {
                MyDebug.LogWarning($"No ad group or matching ads available for {adFormat}");
                onFormatReady?.Invoke(adFormat);
                return;
            }

            var random = new System.Random();

            // Get all ads in this group with the requested format
            var eligibleAds = GetEligibleAdsForFormat(adGroup, adFormat);

            if (!eligibleAds.Any())
            {
                MyDebug.LogWarning($"No assets found to cache for {adFormat} format");
                onFormatReady?.Invoke(adFormat);
                return;
            }

            var cachingTasks = new List<UniTask>();

            // Native ads are cached whole, from a single randomly chosen ad. The video/image
            // fallback pairing below exists so a video ad can borrow another ad's image; native
            // ads have no video and are rendered by the game as one creative, so mixing assets
            // across ads here would put one advertiser's icon next to another's headline.
            if (adFormat == AdFormat.native)
            {
                var nativeAd = eligibleAds[random.Next(eligibleAds.Count)];
                MyDebug.Verbose($"Caching native assets from ad: {nativeAd.id}");
                foreach (var (asset, assetType) in GetAssetsToCache(nativeAd, adFormat))
                {
                    if (!string.IsNullOrEmpty(asset?.url))
                    {
                        var nativeCacheKey = GenerateCacheKey(adFormat, assetType, asset.id);
                        cachingTasks.Add(CacheAssetAsync(nativeCacheKey, asset, assetType, adFormat, adGroup.click_url, nativeAd));
                    }
                }

                if (cachingTasks.Count > 0)
                    await UniTask.WhenAll(cachingTasks);
                else
                    MyDebug.LogWarning($"No assets found to cache for {adFormat} format");

                PersistCachedAssets();
                onFormatReady?.Invoke(adFormat);
                return;
            }

            // NEW APPROACH: Cache assets from multiple ads in the same ad group to ensure we have both video and image fallbacks
            // This ensures that even if one ad only has video, another ad in the same group provides the image fallback

            // Separate ads by their primary asset type
            var videoAds = eligibleAds.Where(ad => ad.main_video?.url != null).ToList();
            var imageAds = eligibleAds.Where(ad => ad.main_image?.url != null).ToList();

            MyDebug.Verbose($"Ad group breakdown - Video ads: {videoAds.Count}, Image ads: {imageAds.Count}");

            // Cache from video ad (if available) to get video + any accompanying assets
            Ad videoAd = null;
            HashSet<AssetType> videoAdAssetTypes = new();
            if (videoAds.Any())
            {
                videoAd = videoAds[random.Next(videoAds.Count)];
                MyDebug.Verbose($"Caching assets from video ad: {videoAd.id}");
                var videoAssetsToCache = GetAssetsToCache(videoAd, adFormat);
                videoAdAssetTypes = videoAssetsToCache.Select(x => x.assetType).ToHashSet();

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
                if (videoAd == null || videoAd.id != imageAd.id)
                {
                    MyDebug.Verbose($"Caching assets from image ad: {imageAd.id}");
                    var imageAssetsToCache = GetAssetsToCache(imageAd, adFormat);

                    // Track which asset types the actually-cached video ad already covers (must match
                    // the same videoAd instance used above, not just any video ad in the group, otherwise
                    // this can either skip caching a needed fallback image or cache a second, mismatched
                    // image alongside the video's own image).
                    var existingAssetTypes = videoAdAssetTypes;

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
                await UniTask.WhenAll(cachingTasks);
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
        public static bool HasAdsForFormat(AdGroup adGroup, AdFormat adFormat)
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
            var hasInImageAds = adGroup.image_ads?.Any(a =>
            {
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

            var hasInVideoAds = adGroup.video_ads?.Any(a =>
            {
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

            // native_ads carry no format field server-side (a NativeAd is always 'native'),
            // so they count for the native format and nothing else.
            var hasInNativeAds = adFormat == AdFormat.native && adGroup.native_ads?.Any() == true;

            var result = hasInImageAds || hasInVideoAds || hasInNativeAds;
            MyDebug.Verbose($"HasAdsForFormat({adFormat}): {result} (image: {hasInImageAds}, video: {hasInVideoAds}, native: {hasInNativeAds})");

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
                    .Where(a =>
                    {
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
                    .Where(a =>
                    {
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

            // Native ads only ever satisfy the native format.
            if (adFormat == AdFormat.native && adGroup.native_ads != null && adGroup.native_ads.Any())
            {
                MyDebug.Verbose($"Found {adGroup.native_ads.Count} native ads");
                eligibleAds.AddRange(adGroup.native_ads);
            }

            MyDebug.Verbose($"Total eligible ads for {adFormat}: {eligibleAds.Count} (including both videos and images)");
            return eligibleAds;
        }

        /// <summary>
        /// Gets the assets to cache for a given ad. Everything is downloaded, videos included, since
        /// the native players only play local files; banners never play video, so theirs is skipped.
        /// </summary>
        private static List<(Asset asset, AssetType assetType)> GetAssetsToCache(Ad ad, AdFormat adFormat)
        {
            var assetsToCache = new List<(Asset, AssetType)>();

            MyDebug.Verbose($"GetAssetsToCache for {adFormat} ad {ad.id}:");
            MyDebug.Verbose($"  - main_image: {(ad.main_image?.url != null ? "available" : "null")}");
            MyDebug.Verbose($"  - main_video: {(ad.main_video?.url != null ? "available" : "null")}");
            MyDebug.Verbose($"  - logo: {(ad.logo?.url != null ? "available" : "null")}");

            var assetMappings = new Dictionary<AssetType, Asset>
            {
                { AssetType.image, ad.main_image },
                { AssetType.video, adFormat == AdFormat.banner ? null : ad.main_video },
                { AssetType.logo, ad.logo },
                // Native ads deliver their square icon here; the real type comes from the
                // asset's own asset_type, so this key is only a slot placeholder.
                { AssetType.native_icon, ad.icon }
            };

            foreach (var (assetType, asset) in assetMappings)
            {
                if (asset?.url != null && !string.IsNullOrEmpty(asset.url))
                {
                    // Determine actual asset type based on URL or asset_type field
                    var actualAssetType = DetermineAssetType(asset);

                    if (actualAssetType == AssetType.image || actualAssetType == AssetType.logo
                        || actualAssetType == AssetType.native_icon || actualAssetType == AssetType.native_image)
                    {
                        // Cache images, logos and native icon/image assets normally
                        assetsToCache.Add((asset, actualAssetType));
                        MyDebug.Verbose($"  - Will cache {actualAssetType}: {asset.id}");
                    }
                    else if (actualAssetType == AssetType.video)
                    {
                        assetsToCache.Add((asset, actualAssetType));
                        MyDebug.Verbose($"  - Will cache video: {asset.id}");
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
        private static async UniTask CacheAssetAsync(string cacheKey, Asset asset, AssetType assetType, AdFormat adFormat, string clickUrl = null, Ad ad = null)
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

                // Every file - videos included - is on disk before its ad counts as ready: the
                // native players only play local files, so a slow network delays an ad instead of
                // stalling it on screen. Downloads stream straight to disk.
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                var extension = Path.GetExtension(new Uri(resolvedUrl).AbsolutePath);
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

                // Videos are megabytes where images are kilobytes; give them time on slow networks.
                var timeoutSeconds = assetType == AssetType.video
                    ? Math.Max(60, UserPlayerPrefs.RequestTimeout * 6)
                    : (int)(UserPlayerPrefs.RequestTimeout * 1.5f);
                await DownloadToFileAsync(resolvedUrl, filePath, timeoutSeconds);

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
            catch
            {
                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.InfoSeverity, $"AssetCache_FailedToCacheAsset_{cacheKey}");
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
        /// Downloads a URL to a file, through a temporary file so a failed or partial download
        /// never leaves something that looks cached.
        /// </summary>
        private static async UniTask DownloadToFileAsync(string url, string filePath, int timeoutSeconds)
        {
            var partialPath = filePath + ".part";
            using var request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerFile(partialPath) { removeFileOnAbort = true };
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, timeoutSeconds);

                if (request.result != UnityWebRequest.Result.Success
                    || request.responseCode < 200 || request.responseCode >= 300)
                    throw new Exception($"Failed to download asset from {url}: {request.responseCode} {request.error}");

                if (!File.Exists(partialPath) || new FileInfo(partialPath).Length == 0)
                    throw new Exception($"Failed to download asset from {url}: No data received");

                if (File.Exists(filePath)) File.Delete(filePath);
                File.Move(partialPath, filePath);
            }
            catch (SoilException sx)
            {
                throw new Exception($"Failed to download asset from {url}: {sx.Message}");
            }
            finally
            {
                if (File.Exists(partialPath))
                {
                    try { File.Delete(partialPath); } catch { /* best effort */ }
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
            catch
            {
                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, $"AssetCache_FailedToRemoveAsset_{uuid}");

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
        /// Clears all cached assets asynchronously to avoid blocking the main thread
        /// </summary>
        public static async UniTask ClearCacheAsync()
        {
            List<AssetCacheEntry> assetsToDelete;

            lock (_lockObject)
            {
                assetsToDelete = _cachedAssets.Values.ToList();
                _cachedAssets.Clear();
                _currentlyDownloading.Clear();
            }

            List<string> deleteErrors = new List<string>();
            Exception clearException = null;

            // Cache the directory path on the main thread before entering background thread
            string cacheDirectoryPath = CacheDirectory;

            await UniTask.RunOnThreadPool(() =>
            {
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
                            deleteErrors.Add($"Failed to delete cached file {asset.LocalPath}: {ex.Message}");
                        }
                    }

                    // Clean up directory using the cached path
                    if (Directory.Exists(cacheDirectoryPath))
                    {
                        try
                        {
                            Directory.Delete(cacheDirectoryPath, true);
                            Directory.CreateDirectory(cacheDirectoryPath);
                        }
                        catch (Exception ex)
                        {
                            deleteErrors.Add($"Failed to clean cache directory: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    clearException = ex;
                }
            });

            // Log results back on the main thread
            if (clearException != null)
            {
                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, "AssetCache_FailedToClearCache");
            }
            else
            {
                // Log any individual file deletion errors
                foreach (var error in deleteErrors)
                {
                    MyDebug.LogWarning(error);
                }

                if (deleteErrors.Count == 0)
                {
                    MyDebug.Verbose("Cleared all cached assets");
                }
            }
        }

        /// <summary>
        /// Clears cached assets for a single ad format asynchronously, leaving other formats' assets untouched.
        /// Used when the ad group selected for a format changes without affecting other formats.
        /// </summary>
        public static async UniTask ClearFormatCacheAsync(AdFormat adFormat)
        {
            List<string> keysToRemove;
            List<AssetCacheEntry> assetsToDelete;

            lock (_lockObject)
            {
                keysToRemove = _cachedAssets
                    .Where(kvp => kvp.Value.AdFormat == adFormat)
                    .Select(kvp => kvp.Key)
                    .ToList();
                assetsToDelete = keysToRemove.Select(key => _cachedAssets[key]).ToList();
                foreach (var key in keysToRemove)
                {
                    _cachedAssets.Remove(key);
                }
            }

            await UniTask.RunOnThreadPool(() =>
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
            });

            PersistCachedAssets();
            MyDebug.Verbose($"Cleared {assetsToDelete.Count} cached assets for format {adFormat}");
        }

        /// Clears old cached assets based on age (older than specified days)
        /// </summary>
        public static async UniTask ClearOldAssetsAsync(int olderThanDays = 7)
        {
            var cutoffDate = DateTime.Now.AddDays(-olderThanDays);
            List<AssetCacheEntry> assetsToDelete;

            lock (_lockObject)
            {
                assetsToDelete = _cachedAssets.Values
                    .Where(asset => asset.CachedAt < cutoffDate)
                    .ToList();

                var keysToRemove = _cachedAssets
                    .Where(kvp => kvp.Value.CachedAt < cutoffDate)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _cachedAssets.Remove(key);
                }
            }

            await UniTask.RunOnThreadPool(() =>
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
                        MyDebug.LogWarning($"Failed to delete old cached file {asset.LocalPath}: {ex.Message}");
                    }
                }
            });

            MyDebug.Verbose($"Cleared {assetsToDelete.Count} old cached assets (older than {olderThanDays} days)");
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
            if (asset.AssetType != AssetType.image && asset.AssetType != AssetType.logo
                && asset.AssetType != AssetType.native_icon && asset.AssetType != AssetType.native_image)
            {
                MyDebug.LogWarning($"Asset {uuid} is not a visual asset (type: {asset.AssetType})");
                return null;
            }

            try
            {
                if (!File.Exists(asset.LocalPath))
                {
                    AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, $"AssetCache_AssetFileNotFound_{uuid}");
                    return null;
                }

                var data = File.ReadAllBytes(asset.LocalPath);
                if (data == null || data.Length == 0)
                {
                    AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, $"AssetCache_AssetFileEmpty_{uuid}");
                    return null;
                }

                var texture = new Texture2D(2, 2);

                if (texture.LoadImage(data))
                {
                    MyDebug.Verbose($"Successfully loaded texture {uuid} ({texture.width}x{texture.height}) - {asset.AssetType}");
                    return texture;
                }

                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, $"AssetCache_FailedToDecodeImage_{uuid}");
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }
            catch
            {
                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, $"AssetCache_FailedToLoadTexture_{uuid}");
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
            catch
            {
                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, "AssetCache_FailedToPersistAssets");
            }
        }

        /// <summary>
        /// Loads cached assets from PlayerPrefs asynchronously to avoid blocking the main thread
        /// </summary>
        public static async UniTask LoadCachedAssetsAsync()
        {
            List<AssetCacheEntry> persistedAssets = null;
            Exception loadException = null;
            int loadedCount = 0;

            // First, get the persisted assets on the main thread (PlayerPrefs access)
            try
            {
                persistedAssets = AdvertisementPlayerPrefs.CachedAssets;
            }
            catch
            {
                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, "AssetCache_FailedToLoadAssetsFromPlayerPrefs");
                return;
            }

            // Then process them on a background thread
            await UniTask.RunOnThreadPool(() =>
            {
                try
                {
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

                            loadedCount = _cachedAssets.Count;
                        }
                    }
                }
                catch (Exception ex)
                {
                    loadException = ex;
                }
            });

            // Finally, log the results back on the main thread
            if (loadException != null)
            {
                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, "AssetCache_FailedToProcessAssets");
            }
            else if (loadedCount > 0)
            {
                MyDebug.Verbose($"Loaded {loadedCount} cached assets from PlayerPrefs");
            }
        }
    }
}
