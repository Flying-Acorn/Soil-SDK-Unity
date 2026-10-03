using System.Collections.Generic;

namespace FlyingAcorn.Soil.Advertisement.Logic
{
    /// <summary>
    /// The pure decisions of rotating fullscreen ads. The SDK keeps the next ad group of an
    /// interstitial or rewarded format downloaded next to the current one, and switches the format
    /// to it while its slot has nothing ready - after a close the slot is preparing again, and a
    /// rewarded slot stays not ready through its cooldown - so a game that was told Loaded never
    /// finds its ad replaced under it. The next one is fetched after a close; an interstitial,
    /// which has no cooldown to switch in, also fetches the following one right after switching,
    /// so its next close finds one ready. The server owns the choice: the
    /// SDK only reports what the player saw recently, which the server weighs down, not out.
    /// </summary>
    public static class AdRotation
    {
        /// <summary>
        /// Whether to fetch the next ad group of <paramref name="format"/> now. Only fullscreen
        /// formats rotate, one fetch at a time, and not while a fetched ad group still waits to
        /// take over.
        /// </summary>
        public static bool ShouldFetchNext(string format, bool fetching, bool waitingToSwitch) =>
            AdFormats.IsFullscreen(format) && !fetching && !waitingToSwitch;

        /// <summary>
        /// Whether switching to a fetched ad group fetches the following one at once. Only an
        /// interstitial does: it is ready again moments after a close, so without one fetched ahead
        /// every ad would show twice. A rewarded ad switches during its cooldown instead, and
        /// fetching ahead would download an ad group (up to a video) the player may never watch.
        /// </summary>
        public static bool FetchesAheadAfterSwitch(string format) => format == AdFormats.Interstitial;

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
        /// written. After a close the slot is preparing again, and a rewarded slot stays not ready
        /// through its cooldown, so the switch normally happens then.
        /// </summary>
        public static bool CanSwitchNow(bool slotReady, bool slotShowing, bool formatCaching) =>
            !slotReady && !slotShowing && !formatCaching;
    }
}
