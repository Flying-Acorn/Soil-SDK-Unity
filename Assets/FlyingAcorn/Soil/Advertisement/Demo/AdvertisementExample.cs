using System;
using System.Collections.Generic;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Advertisement.Data;
using FlyingAcorn.Soil.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Demo
{
    /// <summary>
    /// Banner, interstitial and rewarded ads, wired the way a game uses them:
    /// initialize Soil, then Advertisement; enable a show button when its format's Loaded event
    /// arrives; load again after an ad closes. Every LoadAd is answered with Loaded or Error.
    ///
    /// The status lines name the ad each format has loaded. Interstitial and rewarded ads rotate:
    /// while one is on screen the SDK fetches the next ad in the background, so showing a format
    /// again usually brings a different app. The rewarded button counts down its 10-second
    /// cooldown.
    /// </summary>
    public class AdvertisementExample : MonoBehaviour
    {
        private const int MaxStatusLines = 14;

        [FormerlySerializedAs("getCampaignButton")]
        public Button initializeButton;
        public TextMeshProUGUI statusText;
        public Button showBannerButton;
        public Button showInterstitialButton;
        public Button showRewardedButton;

        private readonly Queue<string> _statusLines = new();
        private readonly Dictionary<AdFormat, string> _loadedAds = new();
        private TMP_Text _rewardedLabel;
        private string _rewardedLabelText;
        private int _shownCooldownSeconds = -1;

        private void Awake()
        {
            initializeButton.interactable = true;
            SetShowButtons(false);

            _rewardedLabel = showRewardedButton.GetComponentInChildren<TMP_Text>();
            _rewardedLabelText = _rewardedLabel ? _rewardedLabel.text : null;

            initializeButton.onClick.AddListener(OnInitializeClicked);
            showBannerButton.onClick.AddListener(() => Show(AdFormat.banner));
            showInterstitialButton.onClick.AddListener(() => Show(AdFormat.interstitial));
            showRewardedButton.onClick.AddListener(() => Show(AdFormat.rewarded));

            Events.OnInitialized += OnInitialized;
            Events.OnInitializeFailed += OnInitializeFailed;

            Events.OnBannerAdLoaded += OnAdLoaded;
            Events.OnInterstitialAdLoaded += OnAdLoaded;
            Events.OnRewardedAdLoaded += OnAdLoaded;

            Events.OnBannerAdError += OnAdError;
            Events.OnInterstitialAdError += OnAdError;
            Events.OnRewardedAdError += OnAdError;

            Events.OnBannerAdShown += OnAdShown;
            Events.OnInterstitialAdShown += OnAdShown;
            Events.OnRewardedAdShown += OnAdShown;

            Events.OnBannerAdClicked += OnAdClicked;
            Events.OnInterstitialAdClicked += OnAdClicked;
            Events.OnRewardedAdClicked += OnAdClicked;

            Events.OnBannerAdClosed += OnAdClosed;
            Events.OnInterstitialAdClosed += OnAdClosed;
            Events.OnRewardedAdClosed += OnAdClosed;

            Events.OnRewardedAdRewarded += OnRewarded;

            Initialize();
        }

        private void OnDisable()
        {
            Advertisement.HideAd(AdFormat.banner);
        }

        private void OnDestroy()
        {
            SoilServices.OnServicesReady -= OnSoilServicesReady;

            Events.OnInitialized -= OnInitialized;
            Events.OnInitializeFailed -= OnInitializeFailed;

            Events.OnBannerAdLoaded -= OnAdLoaded;
            Events.OnInterstitialAdLoaded -= OnAdLoaded;
            Events.OnRewardedAdLoaded -= OnAdLoaded;

            Events.OnBannerAdError -= OnAdError;
            Events.OnInterstitialAdError -= OnAdError;
            Events.OnRewardedAdError -= OnAdError;

            Events.OnBannerAdShown -= OnAdShown;
            Events.OnInterstitialAdShown -= OnAdShown;
            Events.OnRewardedAdShown -= OnAdShown;

            Events.OnBannerAdClicked -= OnAdClicked;
            Events.OnInterstitialAdClicked -= OnAdClicked;
            Events.OnRewardedAdClicked -= OnAdClicked;

            Events.OnBannerAdClosed -= OnAdClosed;
            Events.OnInterstitialAdClosed -= OnAdClosed;
            Events.OnRewardedAdClosed -= OnAdClosed;

            Events.OnRewardedAdRewarded -= OnRewarded;
        }

        private void Update()
        {
            // The rewarded format is not ready during its cooldown; its Loaded arrives when it ends.
            if (!_rewardedLabel || _rewardedLabelText == null) return;
            var seconds = Mathf.CeilToInt(Advertisement.GetRewardedAdCooldownRemainingSeconds());
            if (seconds == _shownCooldownSeconds) return;
            _shownCooldownSeconds = seconds;
            _rewardedLabel.text = seconds > 0 ? $"{_rewardedLabelText} ({seconds}s)" : _rewardedLabelText;
        }

        private void Initialize()
        {
            if (Advertisement.Ready)
            {
                OnInitialized();
                return;
            }

            // Soil first, then Advertisement.
            if (!SoilServices.Ready)
            {
                Status("Initializing Soil...");
                SoilServices.OnServicesReady -= OnSoilServicesReady;
                SoilServices.OnServicesReady += OnSoilServicesReady;
                SoilServices.InitializeAsync();
                return;
            }

            InitializeAdvertisement();
        }

        private void OnSoilServicesReady()
        {
            SoilServices.OnServicesReady -= OnSoilServicesReady;
            InitializeAdvertisement();
        }

        private void InitializeAdvertisement()
        {
            Status("Initializing Advertisement...");
            Advertisement.InitializeAsync(new List<AdFormat> { AdFormat.banner, AdFormat.interstitial, AdFormat.rewarded });
        }

        private void OnInitializeClicked()
        {
            initializeButton.interactable = false;
            Initialize();
        }

        private void OnInitialized()
        {
            Status("Advertisement ready. Loading ads...");
            initializeButton.interactable = false;

            showBannerButton.interactable = Advertisement.IsFormatReady(AdFormat.banner);
            showInterstitialButton.interactable = Advertisement.IsFormatReady(AdFormat.interstitial);
            showRewardedButton.interactable = Advertisement.IsFormatReady(AdFormat.rewarded);

            Advertisement.LoadAd(AdFormat.banner);
            Advertisement.LoadAd(AdFormat.interstitial);
            Advertisement.LoadAd(AdFormat.rewarded);
        }

        private void OnInitializeFailed(string error)
        {
            Status($"Initialization failed: {error}");
            initializeButton.interactable = true;
        }

        private void Show(AdFormat format)
        {
            ButtonFor(format).interactable = false;
            Status($"Showing {format}...");
            Advertisement.ShowAd(format);
        }

        private void OnAdLoaded(AdEventData data)
        {
            var name = Describe(data);
            var changed = _loadedAds.TryGetValue(data.AdFormat, out var previous) && previous != name;
            _loadedAds[data.AdFormat] = name;
            Status(changed
                ? $"{data.AdFormat} loaded: {name} (rotated from {previous})"
                : $"{data.AdFormat} loaded: {name}");
            ButtonFor(data.AdFormat).interactable = true;
        }

        private void OnAdError(AdEventData data)
        {
            Status($"{data.AdFormat} error: {data.AdError}");
            ButtonFor(data.AdFormat).interactable = false;
        }

        private void OnAdShown(AdEventData data) => Status($"{data.AdFormat} shown.");

        private void OnAdClicked(AdEventData data) => Status($"{data.AdFormat} clicked.");

        private void OnRewarded(AdEventData data) => Status("Rewarded ad watched to the end: grant the reward.");

        private void OnAdClosed(AdEventData data)
        {
            Status($"{data.AdFormat} closed. Loading the next one...");
            // Answered with Loaded once the next ad is ready (after the cooldown, for rewarded).
            Advertisement.LoadAd(data.AdFormat);
        }

        private Button ButtonFor(AdFormat format) => format switch
        {
            AdFormat.banner => showBannerButton,
            AdFormat.interstitial => showInterstitialButton,
            _ => showRewardedButton
        };

        /// <summary>A short name for the loaded ad: its headline, else the host of its link.</summary>
        private static string Describe(AdEventData data)
        {
            var title = data?.ad?.main_header?.text_content;
            if (!string.IsNullOrWhiteSpace(title)) return $"\"{title.Trim()}\"";
            var link = data?.ad?.action_button?.url;
            if (!string.IsNullOrEmpty(link) && Uri.TryCreate(link, UriKind.Absolute, out var uri)) return uri.Host;
            return data?.ad?.id ?? "an ad";
        }

        private void Status(string line)
        {
            MyDebug.Info($"[AdDemo] {line}");
            if (!statusText) return;
            _statusLines.Enqueue($"{DateTime.Now:HH:mm:ss} {line}");
            while (_statusLines.Count > MaxStatusLines) _statusLines.Dequeue();
            statusText.text = string.Join("\n", _statusLines);
        }

        private void SetShowButtons(bool interactable)
        {
            showBannerButton.interactable = interactable;
            showInterstitialButton.interactable = interactable;
            showRewardedButton.interactable = interactable;
        }
    }
}
