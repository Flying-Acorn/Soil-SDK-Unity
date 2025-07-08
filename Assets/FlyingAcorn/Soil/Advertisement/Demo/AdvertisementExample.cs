using System.Collections.Generic;
using FlyingAcorn.Soil.Advertisement.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Demo
{
    public class AdvertisementExample : MonoBehaviour
    {
        public Button getCampaignButton;
        public TextMeshProUGUI statusText;
        public Button showBannerButton;
        public Button showInterstitialButton;
        public Button showRewardedButton;
        
        private void Awake()
        {
            getCampaignButton.interactable = true;
            showBannerButton.interactable = false;
            showInterstitialButton.interactable = false;
            showRewardedButton.interactable = false;
            
            getCampaignButton.onClick.AddListener(Init);
            showBannerButton.onClick.AddListener(BannerButtonListener);
            showInterstitialButton.onClick.AddListener(InterstitialButtonListener);
            showRewardedButton.onClick.AddListener(RewardedButtonListener);
            
            Events.OnInitialized += OnInitialized;
            Events.OnInitializeFailed += OnInitializeFailed;
            
            // Subscribe to format assets loaded events
            Events.OnAdFormatAssetsLoaded += OnAdFormatAssetsLoaded;

            Events.OnBannerAdLoaded += HandleAdLoaded;
            Events.OnInterstitialAdLoaded += HandleAdLoaded;
            Events.OnRewardedAdLoaded += HandleAdLoaded;

            Events.OnBannerAdError += HandleAdError;
            Events.OnInterstitialAdError += HandleAdError;
            Events.OnRewardedAdError += HandleAdError;

            Events.OnRewardedAdClosed += HandleAdClosed;
            Events.OnInterstitialAdClosed += HandleAdClosed;
            Events.OnBannerAdClosed += HandleAdClosed;

            Events.OnRewardedAdRewarded += HandleAdCompleted;
            
            // Add more event logging
            Events.OnBannerAdShown += HandleAdShown;
            Events.OnInterstitialAdShown += HandleAdShown;
            Events.OnRewardedAdShown += HandleAdShown;
            
            Events.OnBannerAdClicked += HandleAdClicked;
            Events.OnInterstitialAdClicked += HandleAdClicked;
            Events.OnRewardedAdClicked += HandleAdClicked;
            
            Init();
        }

        private void OnDestroy()
        {
            // Unsubscribe from all events to prevent memory leaks
            getCampaignButton.onClick.RemoveListener(Init);
            showBannerButton.onClick.RemoveListener(BannerButtonListener);
            showInterstitialButton.onClick.RemoveListener(InterstitialButtonListener);
            showRewardedButton.onClick.RemoveListener(RewardedButtonListener);
            Events.OnInitialized -= OnInitialized;
            Events.OnInitializeFailed -= OnInitializeFailed;
            Events.OnAdFormatAssetsLoaded -= OnAdFormatAssetsLoaded;
            Events.OnBannerAdLoaded -= HandleAdLoaded;
            Events.OnInterstitialAdLoaded -= HandleAdLoaded;
            Events.OnRewardedAdLoaded -= HandleAdLoaded;
            Events.OnBannerAdError -= HandleAdError;
            Events.OnInterstitialAdError -= HandleAdError;
            Events.OnRewardedAdError -= HandleAdError;
            Events.OnRewardedAdClosed -= HandleAdClosed;
            Events.OnInterstitialAdClosed -= HandleAdClosed;
            Events.OnBannerAdClosed -= HandleAdClosed;
            Events.OnRewardedAdRewarded -= HandleAdCompleted;
            Events.OnBannerAdShown -= HandleAdShown;
            Events.OnInterstitialAdShown -= HandleAdShown;
            Events.OnRewardedAdShown -= HandleAdShown;
            Events.OnBannerAdClicked -= HandleAdClicked;
            Events.OnInterstitialAdClicked -= HandleAdClicked;
            Events.OnRewardedAdClicked -= HandleAdClicked;
        }

        private void HandleAdShown(AdEventData data)
        {
            statusText.text += $"\n{data.AdFormat} ad shown.";
            Debug.Log($"[AdDemo] {data.AdFormat} ad shown event fired");
        }
        
        private void HandleAdClicked(AdEventData data)
        {
            statusText.text += $"\n{data.AdFormat} ad clicked.";
            Debug.Log($"[AdDemo] {data.AdFormat} ad clicked event fired");
        }

        private void HandleAdCompleted(AdEventData data)
        {
            statusText.text += $"\n{data.AdFormat} ad completed. Reward granted!";
        }

        private void HandleAdClosed(AdEventData data)
        {
            statusText.text += $"\n{data.AdFormat} ad closed.";
            Debug.Log($"[AdDemo] {data.AdFormat} ad closed event fired");
            switch (data.AdFormat)
            {
                case AdFormat.banner:
                    showBannerButton.interactable = true;
                    break;
                case AdFormat.interstitial:
                    showInterstitialButton.interactable = true;
                    break;
                case AdFormat.rewarded:
                    showRewardedButton.interactable = true;
                    break;
            }
        }

        private void HandleAdError(AdEventData data)
        {
            statusText.text += $"\n{data.AdFormat} ad error: {data.AdError}";
            switch (data.AdFormat)
            {
                case AdFormat.banner:
                    showBannerButton.interactable = false;
                    break;
                case AdFormat.interstitial:
                    showInterstitialButton.interactable = false;
                    break;
                case AdFormat.rewarded:
                    showRewardedButton.interactable = false;
                    break;
            }
        }

        private void HandleAdLoaded(AdEventData data)
        {
            statusText.text += $"\n{data} ad loaded successfully.";
            switch (data?.AdFormat)
            {
                case AdFormat.banner:
                    showBannerButton.interactable = true;
                    break;
                case AdFormat.interstitial:
                    showInterstitialButton.interactable = true;
                    break;
                case AdFormat.rewarded:
                    showRewardedButton.interactable = true;
                    break;
            }
        }

        private void Init()
        {
            Advertisement.InitializeAsync(new List<AdFormat> { AdFormat.banner, AdFormat.interstitial, AdFormat.rewarded });
            getCampaignButton.interactable = false;
            statusText.text = "Initializing advertisement...";
        }

        private void OnInitializeFailed(string error)
        {
            statusText.text = $"Advertisement initialization failed: {error}";
            getCampaignButton.interactable = true;
        }

        private void OnInitialized()
        {
            statusText.text = "Advertisement initialized successfully.\nLoading assets...";
            getCampaignButton.interactable = false;
            
            // Check if any formats are already ready (in case of cached assets)
            CheckFormatReadiness();
        }
        
        private void CheckFormatReadiness()
        {
            var readiness = Advertisement.GetFormatReadiness();
            foreach (var kvp in readiness)
            {
                if (kvp.Value)
                {
                    OnAdFormatAssetsLoaded(kvp.Key);
                }
            }
        }

        private void OnAdFormatAssetsLoaded(AdFormat adFormat)
        {
            statusText.text += $"\n{adFormat} assets loaded and ready!";
            
            // Enable the show button for this format
            switch (adFormat)
            {
                case AdFormat.banner:
                    if (Advertisement.IsFormatReady(AdFormat.banner))
                    {
                        showBannerButton.interactable = true;
                        Advertisement.LoadAd(AdFormat.banner); // Pre-load the ad
                    }
                    break;
                case AdFormat.interstitial:
                    if (Advertisement.IsFormatReady(AdFormat.banner))
                    {
                        showInterstitialButton.interactable = true;
                        Advertisement.LoadAd(AdFormat.interstitial); // Pre-load the ad
                    }
                    break;
                case AdFormat.rewarded:
                    if (Advertisement.IsFormatReady(AdFormat.rewarded))
                    {
                        showRewardedButton.interactable = true;
                        Advertisement.LoadAd(AdFormat.rewarded); // Pre-load the ad
                    }
                    break;
            }
        }

        private void BannerButtonListener() => Advertisement.ShowAd(AdFormat.banner);
        private void InterstitialButtonListener() => Advertisement.ShowAd(AdFormat.interstitial);
        private void RewardedButtonListener() => Advertisement.ShowAd(AdFormat.rewarded);
    }
}