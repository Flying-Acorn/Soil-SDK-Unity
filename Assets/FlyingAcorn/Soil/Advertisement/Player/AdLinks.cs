using FlyingAcorn.Soil.Advertisement.Logic;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using FlyingAcorn.Analytics;
#endif

namespace FlyingAcorn.Soil.Advertisement.Player
{
    /// <summary>
    /// Opens an ad's click link. An ad's link is usually the advertiser's click tracker, so in the
    /// Editor it is only logged: a test click must never count as a real one. Click events are
    /// raised either way, so game logic can still be exercised. Only web and app store links are
    /// opened (<see cref="AdLinkPolicy"/>). On Android the link goes through the Java player,
    /// which opens it as a browsable link like its own ads' links.
    /// </summary>
    internal static class AdLinks
    {
        internal const string EditorNotice = "[Advertisement] Editor: ad click not sent, so it is not counted:";

        internal static void Open(string url)
        {
            url = AdLinkPolicy.Sanitize(url);
            if (url == null) return;
#if UNITY_EDITOR
            // Plain Debug.Log: MyDebug would forward this as an analytics event.
            Debug.Log($"{EditorNotice} {url}");
#elif UNITY_ANDROID
            OpenOnAndroid(url);
#else
            Application.OpenURL(url);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private const string BridgeClass = "com.flyingacorn.soil.ads.SoilAdsBridge";

        /// <summary>
        /// Application.OpenURL sends a plain ACTION_VIEW any activity may take; the player adds
        /// CATEGORY_BROWSABLE, so only browsers, stores and apps' link handlers can. The system
        /// opener is used only when the player is not in the build.
        /// </summary>
        private static void OpenOnAndroid(string url)
        {
            try
            {
                using var bridge = new AndroidJavaClass(BridgeClass);
                if (!bridge.CallStatic<bool>("openLink", url))
                    MyDebug.Verbose("[Advertisement] The Android player refused the ad's link");
                return;
            }
            catch (Exception e)
            {
                MyDebug.LogWarning($"[Advertisement] Android ad player unavailable, opening the link with the system: {e.Message}");
            }

            Application.OpenURL(url);
        }
#endif
    }
}
