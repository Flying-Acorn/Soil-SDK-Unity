using System.Collections.Generic;
using System.Linq;
using FlyingAcorn.Soil.Core.User;
using UnityEngine;

namespace FlyingAcorn.Soil.RemoteConfig.ABTesting
{
    /// <summary>
    /// Static class for managing A/B testing data in local player preferences.
    /// </summary>
    public static class ABTestingPlayerPrefs
    {
        private static string KeysPrefix => $"{UserPlayerPrefs.GetKeysPrefix()}ab_testing_";
        private static string LastExperimentKey => $"{KeysPrefix}last_experiment";
        private static string SeenChallengersKey => $"{KeysPrefix}seen_challengers";


        /// <summary>
        /// Gets the ID of the last experiment cohort the user was assigned to.
        /// </summary>
        /// <returns>The experiment ID, or default if none.</returns>
        public static string GetLastExperimentId()
        {
            return PlayerPrefs.GetString(LastExperimentKey, Constants.NoCohortName);
        }

        internal static void SetLastExperimentId(string experimentId)
        {
            // Flushed here rather than left to Unity's own shutdown write. HandleCohortChange
            // rewrites the same value on every launch, so the guard below makes the steady
            // state cheaper than it was, and the one write that matters -- the first
            // assignment -- becomes durable immediately instead of depending on an unrelated
            // module calling PlayerPrefs.Save() before the process dies.
            if (PlayerPrefs.GetString(LastExperimentKey, Constants.NoCohortName) == experimentId)
                return;

            PlayerPrefs.SetString(LastExperimentKey, experimentId);
            PlayerPrefs.Save();
        }

        internal static void SetSeenChallengers(IEnumerable<string> challengersIds)
        {
            var enumerable = challengersIds as string[] ?? challengersIds.ToArray();
            if (!enumerable.Any()) return;

            var joined = string.Join(",", enumerable);
            if (PlayerPrefs.GetString(SeenChallengersKey, "") == joined)
                return;

            PlayerPrefs.SetString(SeenChallengersKey, joined);
            PlayerPrefs.Save();
        }

        internal static List<string> GetSeenChallengers()
        {
            var raw = PlayerPrefs.GetString(SeenChallengersKey, "");
            if (string.IsNullOrEmpty(raw)) return new List<string>();
            return raw.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
}