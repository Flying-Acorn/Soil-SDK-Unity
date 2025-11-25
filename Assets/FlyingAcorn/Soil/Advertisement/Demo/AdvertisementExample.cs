using System.Collections.Generic;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Advertisement.Data;
using FlyingAcorn.Soil.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Demo
{
    public class AdvertisementExample : MonoBehaviour
    {
        // Track processed ad formats to prevent duplicate messages
        private HashSet<AdFormat> readyFormats = new HashSet<AdFormat>();
        // Track last closed ad format to prevent duplicate closed messages in quick succession
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

            getCampaignButton.onClick.AddListener(GetCampaignButtonListener);
            showBannerButton.onClick.AddListener(BannerButtonListener);
            showInterstitialButton.onClick.AddListener(InterstitialButtonListener);
            showRewardedButton.onClick.AddListener(RewardedButtonListener);

            Events.OnInitialized += OnInitialized;
            Events.OnInitializeFailed += OnInitializeFailed;

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

        private void OnDisable()
        {
            Advertisement.HideAd(AdFormat.banner);
        }

        private void OnDestroy()
        {
            // Clean up SoilServices event subscription
            SoilServices.OnServicesReady -= OnSoilServicesReady;
            
            // Unsubscribe from all events to prevent memory leaks
            getCampaignButton.onClick.RemoveListener(GetCampaignButtonListener);
            showBannerButton.onClick.RemoveListener(BannerButtonListener);
            showInterstitialButton.onClick.RemoveListener(InterstitialButtonListener);
            showRewardedButton.onClick.RemoveListener(RewardedButtonListener);
            // Comprehensive event unsubscription to prevent memory leaks and threading issues
            Events.OnInitialized -= OnInitialized;
            Events.OnInitializeFailed -= OnInitializeFailed;

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
            readyFormats.Clear();
        }

        private void HandleAdShown(AdEventData data)
        {
            statusText.text += $"\n{data.AdFormat} shown.";
            MyDebug.Info($"[AdDemo] {data.AdFormat} ad shown event fired");
        }

        private void HandleAdClicked(AdEventData data)
        {
            statusText.text += $"\n{data.AdFormat} clicked.";
            MyDebug.Info($"[AdDemo] {data.AdFormat} ad clicked event fired");
        }

        private void HandleAdCompleted(AdEventData data)
        {
            statusText.text += $"\n{data.AdFormat} done. Reward!";
        }

        private void HandleAdClosed(AdEventData data)
        {
            statusText.text += $"\n{data.AdFormat} closed.";
            MyDebug.Info($"[AdDemo] {data.AdFormat} ad closed event fired");
            
            // Load next ad of the same format
            statusText.text += $"\nLoading next {data.AdFormat}...";
            Advertisement.LoadAd(data.AdFormat);
        }

        private void HandleAdError(AdEventData data)
        {
            statusText.text += $"\n{data.AdFormat} error: {data.AdError}";
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
            statusText.text += $"\n{data.AdFormat} loaded.";
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
            if (Advertisement.Ready)
            {
                OnInitialized();
                return;
            }

            statusText.text = "Initializing...";
            
            // Always initialize SoilServices first if not ready, then Advertisement
            if (!SoilServices.Ready)
            {
                statusText.text = "Initializing Soil SDK...";
                // Subscribe to SoilServices events to know when it's ready
                SoilServices.OnServicesReady -= OnSoilServicesReady;
                SoilServices.OnServicesReady += OnSoilServicesReady;
                SoilServices.InitializeAsync();
            }
            else
            {
                // SoilServices is ready, proceed with Advertisement initialization
                InitializeAdvertisement();
            }
        }

        private void OnSoilServicesReady()
        {
            // Unsubscribe to avoid multiple calls
            SoilServices.OnServicesReady -= OnSoilServicesReady;
            
            // Now that SoilServices is ready, initialize Advertisement
            InitializeAdvertisement();
        }

        private void InitializeAdvertisement()
        {
            statusText.text = "Initializing Advertisement...";
            
            // OPTIONAL: Configure pause behavior before initialization
            // By default (true), the SDK will pause gameplay (Time.timeScale=0) during ads
            // Set to false if you want to handle pausing yourself via ad events
            // Advertisement.SetPauseGameplayDuringAds(false);
            
            Advertisement.InitializeAsync(new List<AdFormat> { AdFormat.banner, AdFormat.interstitial, AdFormat.rewarded });
        }

        private void OnInitializeFailed(string error)
        {
            statusText.text = $"Init failed: {error}";
            getCampaignButton.interactable = true;
        }

        private void OnInitialized()
        {
            statusText.text = "Initialized.\nLoading...";
            getCampaignButton.interactable = false;

            EnableAnyLoadedFormat();
            LoadAds();
        }

        private void EnableAnyLoadedFormat()
        {
            showBannerButton.interactable = Advertisement.IsFormatReady(AdFormat.banner);
            showInterstitialButton.interactable = Advertisement.IsFormatReady(AdFormat.interstitial);
            showRewardedButton.interactable = Advertisement.IsFormatReady(AdFormat.rewarded);
        }

        private void LoadAds()
        {
            Advertisement.LoadAd(AdFormat.banner);
            Advertisement.LoadAd(AdFormat.interstitial);
            Advertisement.LoadAd(AdFormat.rewarded);
        }

        private void GetCampaignButtonListener()
        {
            if (statusText != null) statusText.text += "\nGetCampaign button clicked.";
            getCampaignButton.interactable = false;
            Init();
        }

        private void BannerButtonListener()
        {
            if (statusText != null) statusText.text += "\nShow Banner clicked. Showing...";
            showBannerButton.interactable = false;
            Advertisement.ShowAd(AdFormat.banner);
        }

        private void InterstitialButtonListener()
        {
            if (statusText != null) statusText.text += "\nShow Interstitial clicked. Showing...";
            showInterstitialButton.interactable = false;
            Advertisement.ShowAd(AdFormat.interstitial);
        }

        private void RewardedButtonListener()
        {
            if (statusText != null) statusText.text += "\nShow Rewarded clicked. Showing...";
            showRewardedButton.interactable = false;
            Advertisement.ShowAd(AdFormat.rewarded);
        }
    }
}