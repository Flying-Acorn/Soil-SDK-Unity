using System.Collections.Generic;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Leaderboard.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FlyingAcorn.Soil.Leaderboard
{
    public static class LeaderboardPlayerPrefs
    {
        private static string PrefsPrefix => $"{UserPlayerPrefs.GetKeysPrefix()}leaderboard_";

        private static string CacheKey(string leaderboardId, bool relative)
        {
            return PrefsPrefix + leaderboardId + (relative ? "_relative" : "");
        }

        internal static List<UserScore> CachedLeaderboardData(string leaderboardId, bool relative)
        {
            var jsonString = PlayerPrefs.GetString(CacheKey(leaderboardId, relative), "{}");
            return JsonConvert.DeserializeObject<List<UserScore>>(jsonString);
        }

        internal static void SetCachedLeaderboardData(string leaderboardId, string data, bool relative)
        {
            PlayerPrefs.SetString(CacheKey(leaderboardId, relative), data);
        }
    }
}