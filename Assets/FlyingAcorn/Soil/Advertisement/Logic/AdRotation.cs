using System;
using System.Collections.Generic;

namespace FlyingAcorn.Soil.Advertisement.Logic
{
    /// <summary>
    /// The pure decisions of rotating fullscreen ads. When an interstitial or rewarded ad is shown,
    /// the SDK fetches the format's next ad group and downloads it next to the current one while
    /// the ad is on screen; after the close it switches the format to it, on a frame the slot has
    /// nothing ready - the slot is preparing again then, and a rewarded slot stays not ready through
    /// its cooldown - so a game that was told Loaded never finds its ad replaced under it, and the
    /// next show is a different app. A fetch that failed is tried again at the close. The server
    /// owns the choice: the SDK only reports what the player saw recently, which the server weighs
    /// down, not out.
    /// </summary>
    public static class AdRotation
    {
        /// <summary>How long a closed fullscreen ad waits for its next ad group at most (running time).</summary>
        public const double HoldAfterCloseSeconds = 6;

        /// <summary>
        /// Whether showing an ad of <paramref name="format"/> fetches its next ad group. Only
        /// fullscreen formats rotate, one fetch at a time, and not while a fetched ad group still
        /// waits to take over: that one has not been seen yet.
        /// </summary>
        public static bool ShouldFetchNext(string format, bool fetching, bool waitingToSwitch) =>
            AdFormats.IsFullscreen(format) && !fetching && !waitingToSwitch;

        /// <summary>
        /// Whether closing the ad fetches again: only when the fetch its show started failed (no
        /// network, a download that broke). One that found nothing new is not repeated, so an app
        /// with a single ad group costs one request per show.
        /// </summary>
        public static bool RetriesAfterClose(string format, bool lastFetchFailed, bool fetching, bool waitingToSwitch) =>
            lastFetchFailed && ShouldFetchNext(format, fetching, waitingToSwitch);

        /// <summary>
        /// Whether a closed fullscreen ad stays not ready because its next ad group is on its way
        /// (being fetched, or downloaded and waiting to take over). On Android a fullscreen ad
        /// pauses Unity, so the fetch its show started only runs once the ad has closed; without the
        /// hold the slot would prepare the old ad again within seconds, be ready, and keep it for
        /// one more show - for a rewarded ad too, whose cooldown runs on wall time and can end
        /// while another fullscreen ad pauses Unity. <paramref name="secondsSinceClose"/> is running
        /// time, which stands still while Unity is paused. Held at most
        /// <see cref="HoldAfterCloseSeconds"/>: a slower fetch leaves the cached ad to show again.
        /// </summary>
        public static bool HoldsAfterClose(string format, bool nextAdPending, double secondsSinceClose) =>
            AdFormats.IsFullscreen(format) && nextAdPending
            && secondsSinceClose >= 0 && secondsSinceClose < HoldAfterCloseSeconds;

        /// <summary>
        /// The ids to report as recently seen: the shown history plus <paramref name="alsoSeen"/>
        /// (the current ad, or this round's picks for other formats), without blanks or duplicates.
        /// </summary>
        public static List<string> SeenIds(IEnumerable<string> history, IEnumerable<string> alsoSeen)
        {
            var ids = new List<string>();
            Add(history);
            Add(alsoSeen);
            return ids;

            void Add(IEnumerable<string> source)
            {
                if (source == null) return;
                foreach (var id in source)
                    if (!string.IsNullOrEmpty(id) && !ids.Contains(id))
                        ids.Add(id);
            }
        }

        /// <summary>
        /// Whether to ask once more without seen ids: an ad server from before rotation excludes
        /// them outright, so with little inventory it answers "nothing" although it has ads.
        /// </summary>
        public static bool RetryWithoutSeenIds(bool gotAdGroup, int seenAdGroupCount, int seenCampaignCount) =>
            !gotAdGroup && (seenAdGroupCount > 0 || seenCampaignCount > 0);

        /// <summary>Whether a selected ad group replaces the current one: only a different one does.</summary>
        public static bool IsNewAdGroup(string currentAdGroupId, string selectedAdGroupId) =>
            !string.IsNullOrEmpty(selectedAdGroupId) && selectedAdGroupId != currentAdGroupId;

        /// <summary>
        /// Whether a downloaded ad group may take over its format now. Not while the slot is ready
        /// (the game may show it any moment) or on screen, nor while the format's cache is being
        /// written. Not while the slot is still preparing either: the player's answer to that load
        /// may already be on its way, and would be taken for the answer to the new one - the slot
        /// would report ready while the player is still decoding. After a close the slot prepares
        /// the old ad again and then stays not ready (a rewarded slot through its cooldown, an
        /// interstitial through its hold), so the switch normally happens then. Never while any
        /// fullscreen ad is on screen: switching makes the player decode the new ad, and a phone
        /// with few video decoders cannot read a video while another one plays.
        /// </summary>
        public static bool CanSwitchNow(bool slotReady, bool slotPreparing, bool slotShowing, bool formatCaching,
            bool fullscreenOnScreen) =>
            !slotReady && !slotPreparing && !slotShowing && !formatCaching && !fullscreenOnScreen;

        private static readonly double[] LaunchRetryDelays = { 5, 15, 30, 60, 120, 300 };

        /// <summary>A LoadAd retries a failed format no sooner than this after its last try.</summary>
        public const double LaunchRetryMinGapSeconds = 5;

        /// <summary>
        /// How long a format with no ad, because its ad group request or every download failed,
        /// waits before asking again, by how many tries failed in a row. Such a format is never
        /// shown, so rotation would never fetch it again.
        /// </summary>
        public static double LaunchRetryDelaySeconds(int failures) =>
            LaunchRetryDelays[Math.Max(0, Math.Min(failures - 1, LaunchRetryDelays.Length - 1))];

        /// <summary>
        /// Whether a failed format asks again now: on its own after the backoff, or sooner when the
        /// game asks for an ad, but never while it is already being fetched or cached.
        /// </summary>
        public static bool ShouldRetryLaunch(bool busy, double secondsSinceLastTry, int failures, bool loadRequested) =>
            !busy && secondsSinceLastTry >= (loadRequested ? LaunchRetryMinGapSeconds : LaunchRetryDelaySeconds(failures));
    }
}
