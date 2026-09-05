using FlyingAcorn.Soil.Advertisement.Data;

namespace FlyingAcorn.Soil.Advertisement
{
    /// <summary>
    /// Static class containing all advertisement-related events for subscribing to ad lifecycle events.
    /// </summary>
    public static class Events
    {
        /// <summary>
        /// Fired when the Advertisement service is successfully initialized.
        /// </summary>
        public static event System.Action OnInitialized;

        /// <summary>
        /// Fired when Advertisement service initialization fails.
        /// </summary>
        public static event System.Action<string> OnInitializeFailed;

        /// <summary>
        /// Fired when a banner ad is loaded and ready to be shown.
        /// </summary>
        public static event System.Action<AdEventData> OnBannerAdLoaded;

        /// <summary>
        /// Fired when a banner ad fails to load or show.
        /// </summary>
        public static event System.Action<AdEventData> OnBannerAdError;

        /// <summary>
        /// Fired when a banner ad is shown to the user.
        /// </summary>
        public static event System.Action<AdEventData> OnBannerAdShown;

        /// <summary>
        /// Fired when a banner ad is closed by the user.
        /// </summary>
        public static event System.Action<AdEventData> OnBannerAdClosed;

        /// <summary>
        /// Fired when a banner ad is clicked by the user.
        /// </summary>
        public static event System.Action<AdEventData> OnBannerAdClicked;

        /// <summary>
        /// Fired when an interstitial ad is loaded and ready to be shown.
        /// </summary>
        public static event System.Action<AdEventData> OnInterstitialAdLoaded;

        /// <summary>
        /// Fired when an interstitial ad fails to load or show.
        /// </summary>
        public static event System.Action<AdEventData> OnInterstitialAdError;

        /// <summary>
        /// Fired when an interstitial ad is shown to the user.
        /// </summary>
        public static event System.Action<AdEventData> OnInterstitialAdShown;

        /// <summary>
        /// Fired when an interstitial ad is closed by the user.
        /// </summary>
        public static event System.Action<AdEventData> OnInterstitialAdClosed;

        /// <summary>
        /// Fired when an interstitial ad is clicked by the user.
        /// </summary>
        public static event System.Action<AdEventData> OnInterstitialAdClicked;

        /// <summary>
        /// Fired when a rewarded ad is loaded and ready to be shown.
        /// </summary>
        public static event System.Action<AdEventData> OnRewardedAdLoaded;

        /// <summary>
        /// Fired when a rewarded ad fails to load or show.
        /// </summary>
        public static event System.Action<AdEventData> OnRewardedAdError;

        /// <summary>
        /// Fired when a rewarded ad is shown to the user.
        /// </summary>
        public static event System.Action<AdEventData> OnRewardedAdShown;

        /// <summary>
        /// Fired when a rewarded ad is closed by the user.
        /// </summary>
        public static event System.Action<AdEventData> OnRewardedAdClosed;

        /// <summary>
        /// Fired when a rewarded ad is clicked by the user.
        /// </summary>
        public static event System.Action<AdEventData> OnRewardedAdClicked;

        /// <summary>
        /// Fired when a rewarded ad is completed and rewards should be granted to the user.
        /// </summary>
        public static event System.Action<AdEventData> OnRewardedAdRewarded;

        /// <summary>
        /// Fired when a native ad is loaded and ready to be shown.
        /// </summary>
        public static event System.Action<AdEventData> OnNativeAdLoaded;

        /// <summary>
        /// Fired when a native ad fails to load or show.
        /// </summary>
        public static event System.Action<AdEventData> OnNativeAdError;

        /// <summary>
        /// Fired when a native ad is shown to the user.
        /// </summary>
        public static event System.Action<AdEventData> OnNativeAdShown;

        /// <summary>
        /// Fired when a native ad is closed/hidden.
        /// </summary>
        public static event System.Action<AdEventData> OnNativeAdClosed;

        /// <summary>
        /// Fired when a native ad is clicked by the user.
        /// </summary>
        public static event System.Action<AdEventData> OnNativeAdClicked;

        /// <summary>
        /// Fired when a native ad's content is ready to be rendered. Unlike the other formats,
        /// the SDK does not draw a native ad - the game receives this payload and renders the
        /// title, description, call to action, icon and image in its own UI.
        /// </summary>
        public static event System.Action<NativeAdContent> OnNativeAdContentReady;

        /// <summary>
        /// Fired when assets for an ad format have been loaded and cached. For advanced implementations.
        /// </summary>
        public static event System.Action<Constants.AdFormat> OnAdFormatAssetsLoaded;

        // Internal methods to safely invoke events
        internal static void InvokeOnInitialized()
        {
            OnInitialized?.Invoke();
        }

        internal static void InvokeOnInitializeFailed(string errorMessage)
        {
            OnInitializeFailed?.Invoke(errorMessage);
        }

        internal static void InvokeOnBannerAdLoaded(AdEventData data)
        {
            OnBannerAdLoaded?.Invoke(data);
        }

        internal static void InvokeOnBannerAdError(AdEventData data)
        {
            OnBannerAdError?.Invoke(data);
        }

        internal static void InvokeOnBannerAdShown(AdEventData data)
        {
            OnBannerAdShown?.Invoke(data);
        }

        internal static void InvokeOnBannerAdClosed(AdEventData data)
        {
            OnBannerAdClosed?.Invoke(data);
        }

        internal static void InvokeOnBannerAdClicked(AdEventData data)
        {
            OnBannerAdClicked?.Invoke(data);
        }

        internal static void InvokeOnInterstitialAdLoaded(AdEventData data)
        {
            OnInterstitialAdLoaded?.Invoke(data);
        }

        internal static void InvokeOnInterstitialAdError(AdEventData data)
        {
            OnInterstitialAdError?.Invoke(data);
        }

        internal static void InvokeOnInterstitialAdShown(AdEventData data)
        {
            OnInterstitialAdShown?.Invoke(data);
        }

        internal static void InvokeOnInterstitialAdClosed(AdEventData data)
        {
            OnInterstitialAdClosed?.Invoke(data);
        }

        internal static void InvokeOnInterstitialAdClicked(AdEventData data)
        {
            OnInterstitialAdClicked?.Invoke(data);
        }

        internal static void InvokeOnRewardedAdLoaded(AdEventData data)
        {
            OnRewardedAdLoaded?.Invoke(data);
        }

        internal static void InvokeOnRewardedAdError(AdEventData data)
        {
            OnRewardedAdError?.Invoke(data);
        }

        internal static void InvokeOnRewardedAdShown(AdEventData data)
        {
            OnRewardedAdShown?.Invoke(data);
        }

        internal static void InvokeOnRewardedAdClosed(AdEventData data)
        {
            OnRewardedAdClosed?.Invoke(data);
        }

        internal static void InvokeOnRewardedAdClicked(AdEventData data)
        {
            OnRewardedAdClicked?.Invoke(data);
        }

        internal static void InvokeOnRewardedAdRewarded(AdEventData data)
        {
            OnRewardedAdRewarded?.Invoke(data);
        }

        internal static void InvokeOnNativeAdLoaded(AdEventData data)
        {
            OnNativeAdLoaded?.Invoke(data);
        }

        internal static void InvokeOnNativeAdError(AdEventData data)
        {
            OnNativeAdError?.Invoke(data);
        }

        internal static void InvokeOnNativeAdShown(AdEventData data)
        {
            OnNativeAdShown?.Invoke(data);
        }

        internal static void InvokeOnNativeAdClosed(AdEventData data)
        {
            OnNativeAdClosed?.Invoke(data);
        }

        internal static void InvokeOnNativeAdClicked(AdEventData data)
        {
            OnNativeAdClicked?.Invoke(data);
        }

        internal static void InvokeOnNativeAdContentReady(NativeAdContent content)
        {
            OnNativeAdContentReady?.Invoke(content);
        }

        // Internal methods for asset loading events
        internal static void InvokeOnAdFormatAssetsLoaded(Constants.AdFormat adFormat)
        {
            OnAdFormatAssetsLoaded?.Invoke(adFormat);
        }
    }
}
