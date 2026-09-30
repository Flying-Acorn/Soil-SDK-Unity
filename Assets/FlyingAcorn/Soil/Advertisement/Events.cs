using System;
using FlyingAcorn.Soil.Advertisement.Data;
using UnityEngine;

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

        // Every subscriber is called on its own: one that throws is logged and does not stop the
        // others, nor the ad state machine that raised the event.
        private static void Raise(Action handlers)
        {
            if (handlers == null) return;
            foreach (var handler in handlers.GetInvocationList())
            {
                try { ((Action)handler)(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        private static void Raise<T>(Action<T> handlers, T arg)
        {
            if (handlers == null) return;
            foreach (var handler in handlers.GetInvocationList())
            {
                try { ((Action<T>)handler)(arg); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary>
        /// Drops every subscriber. Only for a new play session without a domain reload, before any
        /// game code has subscribed.
        /// </summary>
        internal static void ResetSubscribers()
        {
            OnInitialized = null;
            OnInitializeFailed = null;
            OnBannerAdLoaded = null;
            OnBannerAdError = null;
            OnBannerAdShown = null;
            OnBannerAdClosed = null;
            OnBannerAdClicked = null;
            OnInterstitialAdLoaded = null;
            OnInterstitialAdError = null;
            OnInterstitialAdShown = null;
            OnInterstitialAdClosed = null;
            OnInterstitialAdClicked = null;
            OnRewardedAdLoaded = null;
            OnRewardedAdError = null;
            OnRewardedAdShown = null;
            OnRewardedAdClosed = null;
            OnRewardedAdClicked = null;
            OnRewardedAdRewarded = null;
            OnNativeAdLoaded = null;
            OnNativeAdError = null;
            OnNativeAdShown = null;
            OnNativeAdClosed = null;
            OnNativeAdClicked = null;
            OnNativeAdContentReady = null;
            OnAdFormatAssetsLoaded = null;
        }

        // Internal methods to safely invoke events
        internal static void InvokeOnInitialized()
        {
            Raise(OnInitialized);
        }

        internal static void InvokeOnInitializeFailed(string errorMessage)
        {
            Raise(OnInitializeFailed, errorMessage);
        }

        internal static void InvokeOnBannerAdLoaded(AdEventData data)
        {
            Raise(OnBannerAdLoaded, data);
        }

        internal static void InvokeOnBannerAdError(AdEventData data)
        {
            Raise(OnBannerAdError, data);
        }

        internal static void InvokeOnBannerAdShown(AdEventData data)
        {
            Raise(OnBannerAdShown, data);
        }

        internal static void InvokeOnBannerAdClosed(AdEventData data)
        {
            Raise(OnBannerAdClosed, data);
        }

        internal static void InvokeOnBannerAdClicked(AdEventData data)
        {
            Raise(OnBannerAdClicked, data);
        }

        internal static void InvokeOnInterstitialAdLoaded(AdEventData data)
        {
            Raise(OnInterstitialAdLoaded, data);
        }

        internal static void InvokeOnInterstitialAdError(AdEventData data)
        {
            Raise(OnInterstitialAdError, data);
        }

        internal static void InvokeOnInterstitialAdShown(AdEventData data)
        {
            Raise(OnInterstitialAdShown, data);
        }

        internal static void InvokeOnInterstitialAdClosed(AdEventData data)
        {
            Raise(OnInterstitialAdClosed, data);
        }

        internal static void InvokeOnInterstitialAdClicked(AdEventData data)
        {
            Raise(OnInterstitialAdClicked, data);
        }

        internal static void InvokeOnRewardedAdLoaded(AdEventData data)
        {
            Raise(OnRewardedAdLoaded, data);
        }

        internal static void InvokeOnRewardedAdError(AdEventData data)
        {
            Raise(OnRewardedAdError, data);
        }

        internal static void InvokeOnRewardedAdShown(AdEventData data)
        {
            Raise(OnRewardedAdShown, data);
        }

        internal static void InvokeOnRewardedAdClosed(AdEventData data)
        {
            Raise(OnRewardedAdClosed, data);
        }

        internal static void InvokeOnRewardedAdClicked(AdEventData data)
        {
            Raise(OnRewardedAdClicked, data);
        }

        internal static void InvokeOnRewardedAdRewarded(AdEventData data)
        {
            Raise(OnRewardedAdRewarded, data);
        }

        internal static void InvokeOnNativeAdLoaded(AdEventData data)
        {
            Raise(OnNativeAdLoaded, data);
        }

        internal static void InvokeOnNativeAdError(AdEventData data)
        {
            Raise(OnNativeAdError, data);
        }

        internal static void InvokeOnNativeAdShown(AdEventData data)
        {
            Raise(OnNativeAdShown, data);
        }

        internal static void InvokeOnNativeAdClosed(AdEventData data)
        {
            Raise(OnNativeAdClosed, data);
        }

        internal static void InvokeOnNativeAdClicked(AdEventData data)
        {
            Raise(OnNativeAdClicked, data);
        }

        internal static void InvokeOnNativeAdContentReady(NativeAdContent content)
        {
            Raise(OnNativeAdContentReady, content);
        }

        // Internal methods for asset loading events
        internal static void InvokeOnAdFormatAssetsLoaded(Constants.AdFormat adFormat)
        {
            Raise(OnAdFormatAssetsLoaded, adFormat);
        }
    }
}
