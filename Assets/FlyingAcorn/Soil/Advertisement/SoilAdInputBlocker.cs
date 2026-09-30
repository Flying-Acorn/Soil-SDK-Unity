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
        /// <summary>True while an interstitial or rewarded ad is on screen.</summary>
        public static bool IsBlocked => Advertisement.IsFullscreenAdShowing;
    }
}
