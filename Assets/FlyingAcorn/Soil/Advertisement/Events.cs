using FlyingAcorn.Soil.Advertisement.Data;

namespace FlyingAcorn.Soil.Advertisement
{
    public static class Events
    {
        public static event System.Action OnInitialized;
        public static event System.Action<string> OnInitializeFailed;

        public static event System.Action<AdEventData> OnBannerAdLoaded;
        public static event System.Action<AdEventData> OnBannerAdError;
        public static event System.Action<AdEventData> OnBannerAdShown;
        public static event System.Action<AdEventData> OnBannerAdClosed;
        public static event System.Action<AdEventData> OnBannerAdClicked;

        public static event System.Action<AdEventData> OnInterstitialAdLoaded;
        public static event System.Action<AdEventData> OnInterstitialAdError;
        public static event System.Action<AdEventData> OnInterstitialAdShown;
        public static event System.Action<AdEventData> OnInterstitialAdClosed;
        public static event System.Action<AdEventData> OnInterstitialAdClicked;

        public static event System.Action<AdEventData> OnRewardedAdLoaded;
        public static event System.Action<AdEventData> OnRewardedAdError;
        public static event System.Action<AdEventData> OnRewardedAdShown;
        public static event System.Action<AdEventData> OnRewardedAdClosed;
        public static event System.Action<AdEventData> OnRewardedAdClicked;
        public static event System.Action<AdEventData> OnRewardedAdCompleted;

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

        internal static void InvokeOnRewardedAdCompleted(AdEventData data)
        {
            OnRewardedAdCompleted?.Invoke(data);
        }
    }
}
