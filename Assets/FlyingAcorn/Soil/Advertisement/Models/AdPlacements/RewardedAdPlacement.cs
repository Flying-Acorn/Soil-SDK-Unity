using System;
using System.Linq;
using UnityEngine;
using FlyingAcorn.Soil.Advertisement.Data;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

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

        private void Awake()
        {
            // Find AdDisplayComponent if not assigned
            if (adDisplayComponent == null)
                adDisplayComponent = GetComponentInChildren<AdDisplayComponent>();
                
            // Setup ad display component
            if (adDisplayComponent != null)
            {
                adDisplayComponent.adFormat = AdFormat.rewarded;
                adDisplayComponent.showCloseButton = true;
            }
            
            // Listen to format assets loaded event
            Events.OnAdFormatAssetsLoaded += OnAdFormatAssetsLoaded;
            
            // Check if assets are already ready
            _isFormatReady = Advertisement.IsFormatReady(AdFormat.rewarded);
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from events
            Events.OnAdFormatAssetsLoaded -= OnAdFormatAssetsLoaded;
        }
        
        private void OnAdFormatAssetsLoaded(AdFormat loadedFormat)
        {
            if (loadedFormat == AdFormat.rewarded)
            {
                _isFormatReady = true;
                Debug.Log("Rewarded ad format assets loaded - placement is now ready");
            }
        }

        public void Hide()
        {
            if (adDisplayComponent != null)
            {
                adDisplayComponent.HideAd();
                OnHidden?.Invoke();
                OnAdClosed?.Invoke();
                
                // Fire event
                var eventData = new AdEventData(AdFormat.rewarded);
                eventData.ad = _currentAd;
                Events.InvokeOnRewardedAdClosed(eventData);
            }
        }

        public bool IsReady()
        {
            // Use event-driven readiness status as primary check
            var eventReady = _isFormatReady;
            
            // Fallback to cache check
            var cacheReady = Advertisement.IsFormatReady(AdFormat.rewarded);
            
            // Return true if either indicates readiness
            return eventReady || cacheReady;
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
                    onClose: () => {
                        OnAdClosed?.Invoke();
                        OnHidden?.Invoke();
                        
                        var eventData = new AdEventData(AdFormat.rewarded);
                        eventData.ad = _currentAd;
                        Events.InvokeOnRewardedAdClosed(eventData);
                    },
                    onClick: () => {
                        OnClicked?.Invoke();
                        
                        var eventData = new AdEventData(AdFormat.rewarded);
                        eventData.ad = _currentAd;
                        Events.InvokeOnRewardedAdClicked(eventData);
                    },
                    onRewarded: () => {
                        OnRewardEarned?.Invoke();
                        
                        var eventData = new AdEventData(AdFormat.rewarded);
                        eventData.ad = _currentAd;
                        Events.InvokeOnRewardedAdRewarded(eventData);
                        
                        // Close button will be handled automatically by AdDisplayComponent countdown
                    },
                    onShown: () => {
                        OnShown?.Invoke();
                        
                        var eventData = new AdEventData(AdFormat.rewarded);
                        eventData.ad = _currentAd;
                        Events.InvokeOnRewardedAdShown(eventData);
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
            var mainAsset = cachedAssets.Find(a => a.AssetType == AssetType.image);
            var logoAsset = cachedAssets.Find(a => a.AssetType == AssetType.logo);

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
                main_header = !string.IsNullOrEmpty(mainHeaderText) ? new Asset { 
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
                } : new Asset {
                    asset_type = "text",
                    url = clickUrl, // Always need the URL for click functionality
                    text_content = null,
                    alt_text = null
                },
                description = !string.IsNullOrEmpty(descriptionText) ? new Asset { 
                    asset_type = "text", 
                    url = "", 
                    text_content = descriptionText,
                    alt_text = descriptionText 
                } : null,
                main_image = mainAsset != null ? new Asset
                {
                    id = mainAsset.Id,
                    url = mainAsset.OriginalUrl,
                    asset_type = "image"
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