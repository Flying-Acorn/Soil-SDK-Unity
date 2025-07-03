using System;
using System.Collections.Generic;
using FlyingAcorn.Soil.Advertisement.Data;
using FlyingAcorn.Soil.Advertisement.Models.AdPlacements;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Demo
{
    public class AdvertisementExample : MonoBehaviour
    {
        public Button getCampaignButton;
        public BannerAdPlacement bannerAdPlacement;
        public InterstitialAdPlacement interstitialAdPlacement;
        public RewardedAdPlacement rewardedAdPlacement;
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

            Events.OnRewardedAdCompleted += HandleAdCompleted;
            Init();
        }

        private void HandleAdCompleted(AdEventData data)
        {
            statusText.text += $"\n{data.AdFormat} ad completed. Reward granted!";
        }

        private void HandleAdClosed(AdEventData data)
        {
            statusText.text += $"\n{data.AdFormat} ad closed.";
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
            statusText.text += $"\n{data.AdFormat} ad loaded successfully.";
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
            statusText.text = "Advertisement initialized successfully.\nLoading ads...";
            getCampaignButton.interactable = false;
        }

        public void HideAll()
        {
            bannerAdPlacement.Hide();
            interstitialAdPlacement.Hide();
            rewardedAdPlacement.Hide();
        }
    }
}