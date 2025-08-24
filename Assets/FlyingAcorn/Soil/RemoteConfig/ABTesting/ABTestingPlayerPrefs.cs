using System.Collections.Generic;
using System.Linq;
using FlyingAcorn.Soil.Core.User;
using UnityEngine;

namespace FlyingAcorn.Soil.RemoteConfig.ABTesting
{
    public static class ABTestingPlayerPrefs
    {
        private static string KeysPrefix => $"{UserPlayerPrefs.GetKeysPrefix()}ab_testing_";
        private static string LastExperimentKey => $"{KeysPrefix}last_experiment";
        private static string SeenChallengersKey => $"{KeysPrefix}seen_challengers";


        internal static string GetLastExperimentId()
        {
            return PlayerPrefs.GetString(LastExperimentKey, "");
        }

        internal static void SetLastExperimentId(string experimentId)
        {
            PlayerPrefs.SetString(LastExperimentKey, experimentId);
        }

        internal static void SetSeenChallengers(IEnumerable<string> challengersIds)
        {
            var enumerable = challengersIds as string[] ?? challengersIds.ToArray();
            if (!enumerable.Any()) return;

            PlayerPrefs.SetString(SeenChallengersKey, string.Join(",", enumerable));
        }

        internal static List<string> GetSeenChallengers()
        {
            var raw = PlayerPrefs.GetString(SeenChallengersKey, "");
            if (string.IsNullOrEmpty(raw)) return new List<string>();
            return raw.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
}