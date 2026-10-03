using System;
using UnityEngine;

namespace FlyingAcorn.Soil.Advertisement
{
    /// <summary>
    /// Whether an ad currently covers the game. Interstitial and rewarded ads are drawn by the
    /// native player on top of Unity (on Android in their own activity, on iOS with the player
    /// paused), so gameplay receives no input while they are up; this flag is for game code that
    /// wants to know anyway.
    /// </summary>
    /// <remarks>
    /// Pause and audio are best handled with the ad events:
    /// <code>
    /// Events.OnInterstitialAdShown += _ => PauseGame();
    /// Events.OnInterstitialAdClosed += _ => ResumeGame();
    /// </code>
    /// </remarks>
    public static class SoilAdInputBlocker
    {
        private const string Automatic =
            "Input blocking is automatic now: fullscreen ads are drawn natively over a paused game. " +
            "This call does nothing; read IsBlocked or use the ad events.";

        /// <summary>True while an interstitial or rewarded ad is on screen.</summary>
        public static bool IsBlocked => Advertisement.IsFullscreenAdShowing;

        /// <summary>Does nothing: blocking follows the ad on screen.</summary>
        [Obsolete(Automatic)]
        public static void Block(Canvas adCanvas = null)
        {
        }

        /// <summary>Does nothing: blocking follows the ad on screen.</summary>
        [Obsolete(Automatic)]
        public static void Unblock()
        {
        }

        /// <summary>Does nothing: blocking follows the ad on screen.</summary>
        [Obsolete(Automatic)]
        public static void ForceUnblock()
        {
        }

        /// <summary>Does nothing: the SDK has its own watchdog for an ad that never answers.</summary>
        [Obsolete(Automatic)]
        public static void FailsafeTick()
        {
        }
    }
}
