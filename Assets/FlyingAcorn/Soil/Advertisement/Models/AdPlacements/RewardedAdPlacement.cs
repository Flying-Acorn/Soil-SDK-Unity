using System;
using System.Linq;
using UnityEngine;
using FlyingAcorn.Soil.Advertisement.Data;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Advertisement;

namespace FlyingAcorn.Soil.Advertisement.Models.AdPlacements
{

    public class RewardedAdPlacement : MonoBehaviour, IAdPlacement
    {

        [Header("Rewarded Configuration")]
        [SerializeField] private string placementId = "rewarded_placement";
        [SerializeField] private string placementName = "Rewarded Ad";
        [SerializeField] private AdDisplayComponent adDisplayComponent;

        private Ad _currentAd;
        private bool _isFormatReady = false;
        public Action OnRewardEarned { get; set; }

        public string Id => placementId;
        public string Name => placementName;
        public AdFormat AdFormat => AdFormat.rewarded;

        public Action OnError { get; set; }
        public Action OnLoaded { get; set; }
        public Action OnShown { get; set; }
        public Action OnHidden { get; set; }
        public Action OnClicked { get; set; }
        public Action OnAdClosed { get; set; }

        // Public property to access the display component
        public AdDisplayComponent DisplayComponent => adDisplayComponent;

        private void OnEnable()
        {
            adDisplayComponent.adFormat = AdFormat.rewarded;
            adDisplayComponent.showCloseButton = true;
            Events.OnAdFormatAssetsLoaded += OnAdFormatAssetsLoaded;
            _isFormatReady = Advertisement.IsFormatReady(AdFormat.rewarded);
        }

        private void OnDisable()
        {
            Events.OnAdFormatAssetsLoaded -= OnAdFormatAssetsLoaded;
        }

        private void OnDestroy()
        {
            // Ensure we deregister from static events per analyzer guidance
            Events.OnAdFormatAssetsLoaded -= OnAdFormatAssetsLoaded;
        }

        private void OnAdFormatAssetsLoaded(AdFormat loadedFormat)
        {
            if (loadedFormat == AdFormat.rewarded)
            {
                _isFormatReady = true;
                MyDebug.Verbose("Rewarded ad format assets loaded - placement is now ready");
            }
        }

        public void Hide()
        {
            if (adDisplayComponent != null)
            {
                adDisplayComponent.HideAd();
                // Restore time/input before notifying listeners
                SoilAdInputBlocker.Unblock();
                OnHidden?.Invoke();
            }
        }

        public bool IsReady()
        {
            // Use event-driven readiness status as primary check
            var eventReady = _isFormatReady;

            // Fallback to cache check - must have at least one cached image asset
            var assets = Advertisement.GetCachedAssets(AdFormat.rewarded);
            var cacheReady = assets != null && assets.Any(asset => asset.AssetType == AssetType.image);

            // Check if assets are ready
            var assetsReady = eventReady || cacheReady;

            // For rewarded ads, also check cooldown
            if (assetsReady)
            {
                return !Advertisement.IsRewardedAdInCooldown();
            }

            return false;
        }

        public void Load()
        {
            if (IsReady())
            {
                // Get the first ad from cached assets
                var cachedAssets = Advertisement.GetCachedAssets(AdFormat.rewarded);
                if (cachedAssets.Count > 0)
                {
                    // Create ad object from cached assets
                    _currentAd = CreateAdFromCachedAssets(cachedAssets);

                    OnLoaded?.Invoke();

                    var eventData = new AdEventData(AdFormat.rewarded);
                    eventData.ad = _currentAd;
                    Events.InvokeOnRewardedAdLoaded(eventData);
                }
                else
                {
                    OnError?.Invoke();
                    var errorData = new AdEventData(AdFormat.rewarded, AdError.NoFill);
                    Events.InvokeOnRewardedAdError(errorData);
                }
            }
            else
            {
                OnError?.Invoke();
                var errorData = new AdEventData(AdFormat.rewarded, AdError.AdNotReady);
                Events.InvokeOnRewardedAdError(errorData);
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
                        // Restore time/input first so listeners run with normal gameplay
                        SoilAdInputBlocker.Unblock();

                        OnAdClosed?.Invoke();
                        OnHidden?.Invoke();
                        var eventData = new AdEventData(AdFormat.rewarded);
                        eventData.ad = _currentAd;
                        Events.InvokeOnRewardedAdClosed(eventData);
                        Advertisement.HideAd(AdFormat.rewarded);

                        // Ensure no stray blockers remain in case of nested flows
                        SoilAdInputBlocker.Unblock();
                    },
                    onClick: () =>
                    {
                        OnClicked?.Invoke();
                        var eventData = new AdEventData(AdFormat.rewarded);
                        eventData.ad = _currentAd;
                        Events.InvokeOnRewardedAdClicked(eventData);
                    },
                    onRewarded: () =>
                    {
                        OnRewardEarned?.Invoke();
                        var eventData = new AdEventData(AdFormat.rewarded);
                        eventData.ad = _currentAd;
                        Events.InvokeOnRewardedAdRewarded(eventData);
                    },
                    onShown: () =>
                    {
                        OnShown?.Invoke();
                        var eventData = new AdEventData(AdFormat.rewarded);
                        eventData.ad = _currentAd;
                        Events.InvokeOnRewardedAdShown(eventData);

                        // Disable gameplay input while ad is visible
                        var canvas = adDisplayComponent ? adDisplayComponent.GetComponentInParent<Canvas>() : null;
                        SoilAdInputBlocker.Block(canvas);
                    }
                );
            }
            else
            {
                OnError?.Invoke();
                var errorData = new AdEventData(AdFormat.rewarded, AdError.AdNotReady);
                Events.InvokeOnRewardedAdError(errorData);
            }
        }

        private Ad CreateAdFromCachedAssets(System.Collections.Generic.List<AssetCacheEntry> cachedAssets)
        {
            // Create a basic ad object with information from cached assets
            // Priority: video > image for main content
            var videoAsset = cachedAssets.Find(a => a.AssetType == AssetType.video);
            var imageAsset = cachedAssets.Find(a => a.AssetType == AssetType.image);
            var logoAsset = cachedAssets.Find(a => a.AssetType == AssetType.logo);

            // Primary asset is video if available, otherwise image
            var mainAsset = videoAsset ?? imageAsset;

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
                format = AdFormat.rewarded.ToString(),
                main_header = !string.IsNullOrEmpty(mainHeaderText) ? new Asset
                {
                    asset_type = "text",
                    url = "",
                    text_content = mainHeaderText,
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
                    asset_type = imageAsset.AssetType.ToString()
                } : null,
                main_video = videoAsset != null ? new Asset
                {
                    id = videoAsset.Id,
                    url = videoAsset.OriginalUrl,
                    asset_type = videoAsset.AssetType.ToString()
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