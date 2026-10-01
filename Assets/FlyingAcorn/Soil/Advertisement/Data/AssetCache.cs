using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Advertisement.Logic;
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
        // stored a video's streaming URL here are invalid: they are dropped when the cache loads,
        // and the next caching round of that format downloads the file (only the missing ones,
        // see AssetCache.CacheAssetsForAdGroupAsync).
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

        private const string PartialFileSuffix = ".part";

        // Downloads give up when no byte arrives for this long (unscaled, foreground time), or when
        // they take longer than the cap altogether; failed downloads are retried with a backoff.
        private const float DownloadInactivitySeconds = 30f;
        private const float VideoDownloadCapSeconds = 600f;
        private const float ImageDownloadCapSeconds = 120f;
        private static readonly float[] DownloadRetryDelaysSeconds = { 2f, 6f };

        // A frame longer than this (the app was in the background, or paused under a fullscreen
        // ad) counts only this much towards a download's timeouts.
        private const float MaxCountedFrameSeconds = 0.5f;

        private static bool _directoryExcludedFromBackup;

        private static void EnsureCacheDirectory()
        {
            var directory = CacheDirectory;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _directoryExcludedFromBackup = false;
            }

            // Once per session, so directories (and files) of older SDK versions are covered too:
            // excluding a directory excludes everything in it.
            if (_directoryExcludedFromBackup) return;
            ExcludeFromBackup(directory);
            _directoryExcludedFromBackup = true;
        }

        /// <summary>
        /// Ad files can be downloaded again at any time, so they must not take the player's iCloud
        /// backup space (Apple's iOS Data Storage Guidelines).
        /// </summary>
        private static void ExcludeFromBackup(string path)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try { UnityEngine.iOS.Device.SetNoBackupFlag(path); }
            catch (Exception ex) { MyDebug.LogWarning($"[Advertisement] Could not exclude {path} from backup: {ex.Message}"); }
#endif
        }

        /// <summary>Forgets the in-memory index; for a new play session without a domain reload.</summary>
        internal static void ResetStatics()
        {
            lock (_lockObject)
            {
                _cachedAssets.Clear();
                _currentlyDownloading.Clear();
            }
            _directoryExcludedFromBackup = false;
        }

        /// <summary>
        /// Caches assets for a specific ad format from multiple ads within the same ad group to ensure comprehensive asset coverage.
        /// This approach ensures that video ads have image fallbacks by caching from both video and image ads in the same group.
        /// The ad group itself is expected to already be selected (server-side, weighted) for this format.
        ///
        /// Caching is incremental: ads whose files are already cached for the format are preferred,
        /// files already on disk are kept, only the missing ones are downloaded (e.g. a video an
        /// older SDK kept as a streaming URL), and cached files of the format the plan no longer
        /// uses are deleted. With everything on disk this finishes without touching the network.
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

            // Get all ads in this group with the requested format
            var eligibleAds = GetEligibleAdsForFormat(adGroup, adFormat);

            if (!eligibleAds.Any())
            {
                MyDebug.LogWarning($"No assets found to cache for {adFormat} format");
                onFormatReady?.Invoke(adFormat);
                return;
            }

            var plan = PlanFormatAssets(eligibleAds, adFormat);

            // Files of ads the plan no longer uses would otherwise be picked up by the creative
            // builder next to the planned ones.
            HashSet<string> plannedKeys = new(plan.Select(p => p.cacheKey));
            List<string> cachedKeys;
            lock (_lockObject)
            {
                cachedKeys = _cachedAssets.Where(kvp => kvp.Value.AdFormat == adFormat).Select(kvp => kvp.Key).ToList();
            }
            foreach (var staleKey in AssetCachePlan.Stale(cachedKeys, plannedKeys))
                RemoveEntry(staleKey);

            var missing = AssetCachePlan.Missing(plan.Select(p => p.cacheKey), IsCachedAndValid);
            var cachingTasks = new List<UniTask>();
            foreach (var (cacheKey, asset, assetType, ad) in plan)
            {
                if (!missing.Contains(cacheKey))
                {
                    // A kept file keeps its bytes, but the ad around it may have changed since it
                    // was downloaded (link, texts), and an asset several ads share now belongs to
                    // the ad it is planned for.
                    RefreshMetadata(cacheKey, asset, adGroup.click_url, ad);
                    continue;
                }
                cachingTasks.Add(CacheAssetAsync(cacheKey, asset, assetType, adFormat, adGroup.click_url, ad));
            }

            if (cachingTasks.Count > 0)
            {
                MyDebug.Verbose($"Downloading {cachingTasks.Count} of {plan.Count} {adFormat} assets");
                await UniTask.WhenAll(cachingTasks);
            }
            else if (plan.Count > 0)
            {
                MyDebug.Verbose($"All {plan.Count} {adFormat} assets already cached");
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
        /// Which files the format needs: native ads come whole from one ad; the other formats take
        /// a video ad's files plus, from an image ad of the same group, whatever kinds of asset the
        /// video ad lacks (so a video has an image fallback). Ads already cached are preferred.
        /// </summary>
        private static List<(string cacheKey, Asset asset, AssetType assetType, Ad ad)> PlanFormatAssets(
            List<Ad> eligibleAds, AdFormat adFormat)
        {
            var random = new System.Random();
            var plan = new List<(string, Asset, AssetType, Ad)>();

            // How much of each ad is already cached for this format; a cached video counts most,
            // since it is by far the largest file.
            var cachedFiles = new Dictionary<string, int>();
            var cachedVideos = new HashSet<string>();
            lock (_lockObject)
            {
                foreach (var entry in _cachedAssets.Values)
                {
                    if (entry.AdFormat != adFormat || string.IsNullOrEmpty(entry.AdId)) continue;
                    cachedFiles[entry.AdId] = cachedFiles.TryGetValue(entry.AdId, out var n) ? n + 1 : 1;
                    if (entry.AssetType == AssetType.video) cachedVideos.Add(entry.AdId);
                }
            }

            int CachedScore(string adId) =>
                (cachedFiles.TryGetValue(adId, out var n) ? n : 0) + (cachedVideos.Contains(adId) ? 1000 : 0);

            Ad Pick(List<Ad> ads, Func<string, int> score)
            {
                var index = AssetCachePlan.PickCandidate(ads.Select(a => a.id).ToList(), score, random.Next);
                return index < 0 ? null : ads[index];
            }

            void Add(Ad ad, (Asset asset, AssetType assetType) item)
            {
                if (string.IsNullOrEmpty(item.asset?.url)) return;
                var key = GenerateCacheKey(adFormat, item.assetType, item.asset.id);
                if (plan.Any(p => p.Item1 == key)) return;
                plan.Add((key, item.asset, item.assetType, ad));
            }

            // Native ads are cached whole, from a single chosen ad. The video/image fallback
            // pairing below exists so a video ad can borrow another ad's image; native ads have no
            // video and are rendered by the game as one creative, so mixing assets across ads here
            // would put one advertiser's icon next to another's headline.
            if (adFormat == AdFormat.native)
            {
                var nativeAd = Pick(eligibleAds, CachedScore);
                MyDebug.Verbose($"Caching native assets from ad: {nativeAd.id}");
                foreach (var item in GetAssetsToCache(nativeAd, adFormat))
                    Add(nativeAd, item);
                return plan;
            }

            // Separate ads by their primary asset type
            var videoAds = eligibleAds.Where(ad => ad.main_video?.url != null).ToList();
            var imageAds = eligibleAds.Where(ad => ad.main_image?.url != null).ToList();
            MyDebug.Verbose($"Ad group breakdown - Video ads: {videoAds.Count}, Image ads: {imageAds.Count}");

            // The video ad's files, if the group has a video ad.
            Ad videoAd = null;
            HashSet<AssetType> videoAdAssetTypes = new();
            if (videoAds.Any())
            {
                videoAd = Pick(videoAds, CachedScore);
                MyDebug.Verbose($"Caching assets from video ad: {videoAd.id}");
                var videoAssetsToCache = GetAssetsToCache(videoAd, adFormat);
                videoAdAssetTypes = videoAssetsToCache.Select(x => x.assetType).ToHashSet();
                foreach (var item in videoAssetsToCache)
                    Add(videoAd, item);
            }

            // The image ad's files the video ad does not cover, so a video has an image fallback.
            if (imageAds.Any())
            {
                // Prefers the image ad cached last time; the video ad itself does not count here.
                var imageAd = Pick(imageAds, id => videoAd != null && id == videoAd.id ? 0 : CachedScore(id));

                // Only if it's different from the video ad (avoid duplicates) or if there is no video ad.
                if (videoAd == null || videoAd.id != imageAd.id)
                {
                    MyDebug.Verbose($"Caching assets from image ad: {imageAd.id}");
                    // Must be compared against the same videoAd instance used above, not just any
                    // video ad in the group, otherwise this can either skip a needed fallback image
                    // or cache a second, mismatched image alongside the video's own image.
                    foreach (var item in GetAssetsToCache(imageAd, adFormat))
                    {
                        if (videoAdAssetTypes.Contains(item.assetType))
                        {
                            MyDebug.Verbose($"Skipping {item.assetType} asset - already covered by video ad");
                            continue;
                        }
                        Add(imageAd, item);
                    }
                }
            }

            return plan;
        }

        /// <summary>
        /// Rewrites the ad-level data of a cached entry from the current ad group, as a fresh
        /// download would record it. The file is left alone.
        /// </summary>
        private static void RefreshMetadata(string cacheKey, Asset asset, string clickUrl, Ad ad)
        {
            lock (_lockObject)
            {
                if (!_cachedAssets.TryGetValue(cacheKey, out var entry)) return;
                ApplyMetadata(entry, asset, clickUrl, ad);
            }
        }

        private static void ApplyMetadata(AssetCacheEntry entry, Asset asset, string clickUrl, Ad ad)
        {
            entry.ClickUrl = clickUrl;
            entry.Width = asset.width;
            entry.Height = asset.height;
            entry.AltText = asset.alt_text;
            entry.AdId = ad?.id;
            entry.MainHeaderText = ad?.main_header?.text_content;
            entry.ActionButtonText = ad?.action_button?.text_content;
            entry.DescriptionText = ad?.description?.text_content;
        }

        private static bool IsCachedAndValid(string cacheKey)
        {
            AssetCacheEntry entry;
            lock (_lockObject)
            {
                _cachedAssets.TryGetValue(cacheKey, out entry);
            }
            return entry != null && entry.IsValid;
        }

        /// <summary>Drops one entry from the index and deletes its file.</summary>
        private static void RemoveEntry(string cacheKey)
        {
            AssetCacheEntry entry;
            lock (_lockObject)
            {
                if (!_cachedAssets.TryGetValue(cacheKey, out entry)) return;
                _cachedAssets.Remove(cacheKey);
            }

            DeleteFileQuietly(entry.LocalPath);
            MyDebug.Verbose($"Removed cached asset {cacheKey}");
        }

        private static void DeleteFileQuietly(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && IsInsideCacheDirectory(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                MyDebug.LogWarning($"Failed to delete cached file {path}: {ex.Message}");
            }
        }

        private static bool IsInsideCacheDirectory(string path)
        {
            try
            {
                var directory = Path.GetFullPath(CacheDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return Path.GetFullPath(path).StartsWith(directory, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Removes the entry of one format's asset (and its file), leaving the same asset of other
        /// formats alone.
        /// </summary>
        internal static bool RemoveCachedAsset(AdFormat adFormat, string assetId)
        {
            List<string> keys;
            lock (_lockObject)
            {
                keys = _cachedAssets.Where(kvp => kvp.Value.AdFormat == adFormat && kvp.Value.Id == assetId)
                    .Select(kvp => kvp.Key).ToList();
            }

            foreach (var key in keys)
                RemoveEntry(key);
            if (keys.Count > 0) PersistCachedAssets();
            return keys.Count > 0;
        }

        /// <summary>Drops the entries of a format whose files are gone; returns how many.</summary>
        internal static int RemoveMissingFiles(AdFormat adFormat)
        {
            List<string> keys;
            lock (_lockObject)
            {
                keys = _cachedAssets.Where(kvp => kvp.Value.AdFormat == adFormat && !kvp.Value.IsValid)
                    .Select(kvp => kvp.Key).ToList();
                foreach (var key in keys)
                    _cachedAssets.Remove(key);
            }

            if (keys.Count > 0) PersistCachedAssets();
            return keys.Count;
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
                AssetCacheEntry unusable = null;
                lock (_lockObject)
                {
                    if (_cachedAssets.TryGetValue(cacheKey, out var existing))
                    {
                        if (existing.IsValid)
                        {
                            MyDebug.Verbose($"Asset already cached: {cacheKey}");
                            return;
                        }

                        // Its file is gone (or it is an older SDK's streaming-URL entry): download again.
                        _cachedAssets.Remove(cacheKey);
                        unusable = existing;
                    }

                    if (_currentlyDownloading.Contains(cacheKey))
                    {
                        MyDebug.Verbose($"Asset already being downloaded: {cacheKey}");
                        return;
                    }

                    _currentlyDownloading.Add(cacheKey);
                }

                if (unusable != null) DeleteFileQuietly(unusable.LocalPath);
                EnsureCacheDirectory();

                // Resolve URL (handle relative URLs)
                var resolvedUrl = ResolveAssetUrl(asset.url);
                Analytics.MyDebug.Verbose($"Processing asset {cacheKey} ({assetType}) from URL: {resolvedUrl}");

                // Every file - videos included - is on disk before its ad counts as ready: the
                // native players only play local files, so a slow network delays an ad instead of
                // stalling it on screen. Downloads stream straight to disk.
                // File names never take a server string as is: the id is reduced to [A-Za-z0-9_-]
                // (or hashed) and the extension to a plain one, so nothing can escape SoilAssets.
                // A URL without one still gets one: iOS picks the video decoder by extension,
                // while image decoders read the content and only need a neutral name.
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                var extension = AssetCachePlan.SafeExtension(Path.GetExtension(new Uri(resolvedUrl).AbsolutePath),
                    assetType == AssetType.video ? ".mp4" : ".img");
                var baseName = $"{adFormat}_{assetType}_{AssetCachePlan.SafeFileId(asset.id)}";
                var fileName = $"{baseName}_{timestamp}{extension}";
                var filePath = Path.Combine(CacheDirectory, fileName);

                // Ensure the file doesn't exist (additional safety check)
                var counter = 0;
                while (File.Exists(filePath) && counter < 100)
                {
                    fileName = $"{baseName}_{timestamp}_{counter}{extension}";
                    filePath = Path.Combine(CacheDirectory, fileName);
                    counter++;
                }

                // Videos are megabytes where images are kilobytes: both give up only when the
                // download stalls, but a video may take much longer overall on a slow network.
                var capSeconds = assetType == AssetType.video ? VideoDownloadCapSeconds : ImageDownloadCapSeconds;
                await DownloadToFileAsync(resolvedUrl, filePath, capSeconds);

                var cachedAsset = new AssetCacheEntry
                {
                    Id = asset.id,
                    AssetType = assetType,
                    AdFormat = adFormat,
                    LocalPath = filePath,
                    OriginalUrl = resolvedUrl, // Store the resolved URL
                    CachedAt = DateTime.UtcNow
                };
                // The click URL of the ad group and the ad-level data of the ad it was cached for.
                ApplyMetadata(cachedAsset, asset, clickUrl, ad);

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
        /// never leaves something that looks cached. A download that stalls (no bytes for
        /// <see cref="DownloadInactivitySeconds"/>) or outlasts <paramref name="capSeconds"/> is
        /// aborted; transient failures are retried with a backoff. Timeouts count unscaled
        /// foreground time, so a paused game (timeScale 0) does not stop them.
        /// </summary>
        private static async UniTask DownloadToFileAsync(string url, string filePath, float capSeconds)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await DownloadOnceAsync(url, filePath, capSeconds);
                    ExcludeFromBackup(filePath);
                    return;
                }
                catch (DownloadException ex) when (ex.Retryable && attempt < DownloadRetryDelaysSeconds.Length)
                {
                    var delay = DownloadRetryDelaysSeconds[attempt];
                    MyDebug.Verbose($"[Advertisement] Download failed ({ex.Message}); retrying in {delay} s");
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), DelayType.Realtime);
                }
            }
        }

        private sealed class DownloadException : Exception
        {
            public readonly bool Retryable;

            public DownloadException(string message, bool retryable) : base(message)
            {
                Retryable = retryable;
            }
        }

        private static async UniTask DownloadOnceAsync(string url, string filePath, float capSeconds)
        {
            var partialPath = filePath + PartialFileSuffix;
            using var request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerFile(partialPath) { removeFileOnAbort = true };
            try
            {
                var operation = request.SendWebRequest();
                var elapsed = 0f;
                var idle = 0f;
                ulong lastBytes = 0;
                while (!operation.isDone)
                {
                    await UniTask.Yield();
                    if (operation.isDone) break;

                    var frame = Mathf.Min(Time.unscaledDeltaTime, MaxCountedFrameSeconds);
                    elapsed += frame;
                    var bytes = request.downloadedBytes;
                    if (bytes != lastBytes)
                    {
                        lastBytes = bytes;
                        idle = 0f;
                    }
                    else
                    {
                        idle += frame;
                    }

                    if (idle >= DownloadInactivitySeconds)
                    {
                        request.Abort();
                        throw new DownloadException($"no data for {DownloadInactivitySeconds} s from {url}", true);
                    }

                    if (elapsed >= capSeconds)
                    {
                        request.Abort();
                        throw new DownloadException($"took longer than {capSeconds} s: {url}", false);
                    }
                }

                var code = request.responseCode;
                if (request.result != UnityWebRequest.Result.Success || code < 200 || code >= 300)
                {
                    // No response, a server error or throttling may pass; a 4xx will not.
                    var retryable = request.result == UnityWebRequest.Result.ConnectionError
                                    || code == 0 || code == 408 || code == 429 || code >= 500;
                    throw new DownloadException($"Failed to download asset from {url}: {code} {request.error}", retryable);
                }

                if (!File.Exists(partialPath) || new FileInfo(partialPath).Length == 0)
                    throw new DownloadException($"Failed to download asset from {url}: No data received", true);

                if (File.Exists(filePath)) File.Delete(filePath);
                File.Move(partialPath, filePath);
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
            var assetsForFormat = GetCachedAssets(adFormat);
            var assets = assetsForFormat.Where(a => a.AssetType == assetType).ToList();
            if (!assets.Any())
            {
                MyDebug.Verbose($"No {assetType} asset found for {adFormat}. Available assets for this format: {assetsForFormat.Count}");
                // List available assets for this format
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
            var all = GetAllCachedAssets();
            var asset = all.FirstOrDefault(a => a.Id == uuid);

            if (asset == null)
            {
                MyDebug.LogWarning($"Asset with UUID {uuid} not found in cache. Available assets: {all.Count}");
                foreach (var entry in all.Take(5)) // Show first 5 for debugging
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
        /// Gets all cached assets for a specific ad format. The list is a snapshot: later caching
        /// does not change it.
        /// </summary>
        public static List<AssetCacheEntry> GetCachedAssets(AdFormat adFormat)
        {
            lock (_lockObject)
            {
                return _cachedAssets.Values.Where(a => a.AdFormat == adFormat).ToList();
            }
        }

        /// <summary>
        /// Gets all cached assets. The list is a snapshot: later caching does not change it.
        /// </summary>
        public static List<AssetCacheEntry> GetAllCachedAssets()
        {
            lock (_lockObject)
            {
                return _cachedAssets.Values.ToList();
            }
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
                var found = _cachedAssets.FirstOrDefault(kvp => kvp.Value.Id == uuid);
                asset = found.Value;
                keyToRemove = found.Key;
                if (keyToRemove != null)
                {
                    _cachedAssets.Remove(keyToRemove);
                }
            }

            if (asset == null)
            {
                MyDebug.LogWarning($"Asset with UUID {uuid} not found in cache.");
                return false;
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

            // The directory was recreated: exclude it from backups again.
            _directoryExcludedFromBackup = false;
            if (Directory.Exists(cacheDirectoryPath)) EnsureCacheDirectory();

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

            return LoadTexture(asset);
        }

        /// <summary>Decodes one cached entry's file into a new texture, or null.</summary>
        internal static Texture2D LoadTexture(AssetCacheEntry asset)
        {
            if (asset == null) return null;
            var uuid = asset.Id;

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
        /// Loads cached assets from PlayerPrefs asynchronously to avoid blocking the main thread.
        /// Entries whose file moved with the app's data container (iOS moves it on app updates)
        /// are pointed at the file's new place; entries without a file are dropped. Then, unless a
        /// download is running, files in SoilAssets that no entry references (leftovers of
        /// interrupted downloads and of dropped entries) are deleted.
        /// </summary>
        public static async UniTask LoadCachedAssetsAsync()
        {
            List<AssetCacheEntry> persistedAssets = null;
            Exception loadException = null;
            int loadedCount = 0;
            int relocatedCount = 0;
            int deletedCount = 0;

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

            // Application paths may only be read on the main thread.
            string cacheDirectory;
            try
            {
                EnsureCacheDirectory();
                cacheDirectory = CacheDirectory;
            }
            catch (Exception ex)
            {
                MyDebug.LogWarning($"[Advertisement] Asset cache directory unavailable: {ex.Message}");
                cacheDirectory = null;
            }

            // Then process them on a background thread
            await UniTask.RunOnThreadPool(() =>
            {
                try
                {
                    lock (_lockObject)
                    {
                        if (persistedAssets != null && persistedAssets.Count > 0)
                        {
                            // Clear current cache
                            _cachedAssets.Clear();

                            foreach (var asset in persistedAssets)
                            {
                                if (asset == null) continue;
                                if (!asset.IsValid && cacheDirectory != null && Relocate(asset, cacheDirectory))
                                    relocatedCount++;
                                if (!asset.IsValid) continue;

                                var cacheKey = GenerateCacheKey(asset.AdFormat, asset.AssetType, asset.Id);
                                _cachedAssets[cacheKey] = asset;
                            }

                            loadedCount = _cachedAssets.Count;
                        }

                        // Only while nothing is being written into the directory.
                        if (cacheDirectory != null && _currentlyDownloading.Count == 0)
                            deletedCount = DeleteOrphanedFiles(cacheDirectory);
                    }
                }
                catch (Exception ex)
                {
                    loadException = ex;
                }
            });

            if (relocatedCount > 0) PersistCachedAssets();

            // Finally, log the results back on the main thread
            if (loadException != null)
            {
                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, "AssetCache_FailedToProcessAssets");
            }
            else
            {
                if (loadedCount > 0)
                    MyDebug.Verbose($"Loaded {loadedCount} cached assets from PlayerPrefs ({relocatedCount} relocated)");
                if (deletedCount > 0)
                    MyDebug.Verbose($"Deleted {deletedCount} orphaned files from the asset cache");
            }
        }

        /// <summary>
        /// Points an entry at its file in the current cache directory when the file is there under
        /// the same name (the data container moved). Callers hold the lock.
        /// </summary>
        private static bool Relocate(AssetCacheEntry asset, string cacheDirectory)
        {
            if (string.IsNullOrEmpty(asset.LocalPath)) return false;
            if (asset.LocalPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || asset.LocalPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return false;

            string moved;
            try
            {
                moved = Path.Combine(cacheDirectory, Path.GetFileName(asset.LocalPath));
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (moved == asset.LocalPath || !File.Exists(moved)) return false;
            asset.LocalPath = moved;
            return true;
        }

        /// <summary>
        /// Deletes the files directly in the cache directory that no entry references, and every
        /// partial download. Sub-directories are left alone. Callers hold the lock.
        /// </summary>
        private static int DeleteOrphanedFiles(string cacheDirectory)
        {
            if (!Directory.Exists(cacheDirectory)) return 0;

            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in _cachedAssets.Values)
            {
                if (string.IsNullOrEmpty(entry.LocalPath)) continue;
                try { referenced.Add(Path.GetFullPath(entry.LocalPath)); }
                catch { /* not a path */ }
            }

            var deleted = 0;
            foreach (var file in Directory.GetFiles(cacheDirectory))
            {
                string fullPath;
                try { fullPath = Path.GetFullPath(file); }
                catch { continue; }

                var isPartial = fullPath.EndsWith(PartialFileSuffix, StringComparison.OrdinalIgnoreCase);
                if (!isPartial && referenced.Contains(fullPath)) continue;

                try
                {
                    File.Delete(fullPath);
                    deleted++;
                }
                catch (Exception)
                {
                    // In use or protected; tried again next launch.
                }
            }

            return deleted;
        }
    }
}
