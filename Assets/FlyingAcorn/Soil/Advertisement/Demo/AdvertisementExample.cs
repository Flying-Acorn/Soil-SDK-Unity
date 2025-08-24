using System.Collections.Generic;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Advertisement.Data;
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
        private AdFormat? lastClosedFormat = null;
        private float lastClosedTime = 0f;
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
            // Unsubscribe from all events to prevent memory leaks
            getCampaignButton.onClick.RemoveListener(Init);
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
            lastClosedFormat = null;
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
            // Prevent duplicate closed messages within a short time window (0.2s)
            if (lastClosedFormat == data.AdFormat && (Time.unscaledTime - lastClosedTime) < 0.2f)
                return;
            lastClosedFormat = data.AdFormat;
            lastClosedTime = Time.unscaledTime;
            statusText.text += $"\n{data.AdFormat} closed.";
            MyDebug.Info($"[AdDemo] {data.AdFormat} ad closed event fired");
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