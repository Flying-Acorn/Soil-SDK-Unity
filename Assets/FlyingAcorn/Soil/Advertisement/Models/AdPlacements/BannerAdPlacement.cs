using System;
using System.Linq;
using UnityEngine;
using FlyingAcorn.Soil.Advertisement.Data;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;
using FlyingAcorn.Analytics;

namespace FlyingAcorn.Soil.Advertisement.Models.AdPlacements
{
    public class BannerAdPlacement : MonoBehaviour, IAdPlacement
    {
        [Header("Banner Configuration")]
        [SerializeField] private string placementId = "banner_placement";
        [SerializeField] private string placementName = "Banner Ad";
        [SerializeField] private AdDisplayComponent adDisplayComponent;

        private Ad _currentAd;
        private bool _isFormatReady = false;

        public string Id => placementId;
        public string Name => placementName;
        public AdFormat AdFormat => AdFormat.banner;

        // Public property to access the display component
        public AdDisplayComponent DisplayComponent => adDisplayComponent;

        public Action OnError { get; set; }
        public Action OnLoaded { get; set; }
        public Action OnShown { get; set; }
        public Action OnHidden { get; set; }
        public Action OnClicked { get; set; }
        public Action OnAdClosed { get; set; }

        private void OnEnable()
        {
            adDisplayComponent.adFormat = AdFormat.banner;
            adDisplayComponent.showCloseButton = true;
            Events.OnAdFormatAssetsLoaded += OnAdFormatAssetsLoaded;
            _isFormatReady = Advertisement.IsFormatReady(AdFormat.banner);
        }

        private void OnDisable()
        {
            Events.OnAdFormatAssetsLoaded -= OnAdFormatAssetsLoaded;
        }

        private void OnAdFormatAssetsLoaded(AdFormat loadedFormat)
        {
            if (loadedFormat == AdFormat.banner)
            {
                _isFormatReady = true;
                MyDebug.Verbose("Banner ad format assets loaded - placement is now ready");
            }
        }

        public void Hide()
        {
            MyDebug.Verbose($"[BannerAdPlacement] Hide called");
            if (adDisplayComponent != null)
            {
                adDisplayComponent.HideAd();
                OnHidden?.Invoke();
                OnAdClosed?.Invoke();

                // Fire event
                var eventData = new AdEventData(AdFormat.banner);
                eventData.ad = _currentAd;
                Events.InvokeOnBannerAdClosed(eventData);
            }
        }

        public bool IsReady()
        {
            // Use event-driven readiness status as primary check
            var eventReady = _isFormatReady;

            // Fallback to cache check
            var cacheReady = Advertisement.IsFormatReady(AdFormat.banner);

            // Return true if either indicates readiness
            return eventReady || cacheReady;
        }

        public void Load()
        {
            if (IsReady())
            {
                // Get the first ad from cached assets
                var cachedAssets = Advertisement.GetCachedAssets(AdFormat.banner);

                if (cachedAssets.Count > 0)
                {
                    // We need to reconstruct the ad data from cached info
                    // For now, create a simple ad object
                    _currentAd = CreateAdFromCachedAssets(cachedAssets);

                    OnLoaded?.Invoke();

                    var eventData = new AdEventData(AdFormat.banner);
                    eventData.ad = _currentAd;
                    Events.InvokeOnBannerAdLoaded(eventData);
                }
                else
                {
                    OnError?.Invoke();
                    var errorData = new AdEventData(AdFormat.banner, AdError.NoFill);
                    Events.InvokeOnBannerAdError(errorData);
                }
            }
            else
            {
                OnError?.Invoke();
                var errorData = new AdEventData(AdFormat.banner, AdError.AdNotReady);
                Events.InvokeOnBannerAdError(errorData);
            }
        }

        public void Show()
        {
            if (_currentAd == null)
            {
                Load();
            }

            if (_currentAd != null && adDisplayComponent != null)
            {
                adDisplayComponent.ShowAd(
                    _currentAd,
                    onClose: () =>
                    {
                        OnAdClosed?.Invoke();
                        OnHidden?.Invoke();
                        var eventData = new AdEventData(AdFormat.banner);
                        eventData.ad = _currentAd;
                        Events.InvokeOnBannerAdClosed(eventData);
                        // Ensure ad is hidden and destroyed
                        Advertisement.HideAd(AdFormat.banner);
                    },
                    onClick: () =>
                    {
                        OnClicked?.Invoke();
                        var eventData = new AdEventData(AdFormat.banner);
                        eventData.ad = _currentAd;
                        Events.InvokeOnBannerAdClicked(eventData);
                    },
                    onRewarded: null, // Banners don't have completion
                    onShown: () =>
                    {
                        OnShown?.Invoke();
                        var eventData = new AdEventData(AdFormat.banner);
                        eventData.ad = _currentAd;
                        Events.InvokeOnBannerAdShown(eventData);
                    }
                );
            }
            else
            {
                OnError?.Invoke();
                var errorData = new AdEventData(AdFormat.banner, AdError.NoFill);
                Events.InvokeOnBannerAdError(errorData);
            }
        }

        private Ad CreateAdFromCachedAssets(System.Collections.Generic.List<AssetCacheEntry> cachedAssets)
        {
            // Create a basic ad object with information from cached assets
            var videoAsset = cachedAssets.Find(a => a.AssetType == AssetType.video);
            var imageAsset = cachedAssets.Find(a => a.AssetType == AssetType.image);
            var logoAsset = cachedAssets.Find(a => a.AssetType == AssetType.logo);

            // Primary asset is image for banners, but support video too
            var mainAsset = imageAsset ?? videoAsset;

            // Get the click URL from any cached asset (they should all have the same click URL from the same AdGroup)
            var clickUrl = mainAsset?.ClickUrl ?? logoAsset?.ClickUrl;
            if (string.IsNullOrEmpty(clickUrl))
                Analytics.MyDebug.Info("No click URL found in cached assets");

            // Get ad-level text content from cached assets (use first available asset that has this data)
            var assetWithAdData = cachedAssets.FirstOrDefault(a => !string.IsNullOrEmpty(a.AdId)) ?? mainAsset ?? logoAsset;
            var mainHeaderText = assetWithAdData?.MainHeaderText;
            var actionButtonText = assetWithAdData?.ActionButtonText;
            var descriptionText = assetWithAdData?.DescriptionText;

            return new Ad
            {
                id = assetWithAdData?.AdId ?? mainAsset?.Id ?? Guid.NewGuid().ToString(),
                format = AdFormat.banner.ToString(),
                main_header = !string.IsNullOrEmpty(mainHeaderText) ? new Asset
                {
                    asset_type = "text",
                    url = "",
                    text_content = "Best HEADER",
                    alt_text = mainHeaderText
                } : null,
                action_button = !string.IsNullOrEmpty(actionButtonText) ? new Asset
                {
                    asset_type = "text",
                    url = clickUrl, // Use real click URL from campaign
                    text_content = actionButtonText,
                    alt_text = actionButtonText
                } : new Asset
                {
                    asset_type = "text",
                    url = clickUrl, // Always need the URL for click functionality
                    text_content = null,
                    alt_text = null
                },
                description = !string.IsNullOrEmpty(descriptionText) ? new Asset
                {
                    asset_type = "text",
                    url = "",
                    text_content = descriptionText,
                    alt_text = descriptionText
                } : null,
                main_image = imageAsset != null ? new Asset
                {
                    id = imageAsset.Id,
                    url = imageAsset.OriginalUrl,
                    asset_type = "image"
                } : null,
                main_video = videoAsset != null ? new Asset
                {
                    id = videoAsset.Id,
                    url = videoAsset.OriginalUrl,
                    asset_type = "video"
                } : null,
                logo = logoAsset != null ? new Asset
                {
                    id = logoAsset.Id,
                    url = logoAsset.OriginalUrl,
                    asset_type = "logo"
                } : null
            };
        }
    }
}