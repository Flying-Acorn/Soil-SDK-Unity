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
        private static string CacheKey(string leaderboardId) => PrefsPrefix + leaderboardId;

        internal static List<UserScore> CachedLeaderboardData(string leaderboardId)
        {
            var jsonString = PlayerPrefs.GetString(CacheKey(leaderboardId), "{}");
            return JsonConvert.DeserializeObject<List<UserScore>>(jsonString);
        }

        internal static void SetCachedLeaderboardData(string leaderboardId, string data)
        {
            PlayerPrefs.SetString(CacheKey(leaderboardId), data);
        }
    }
}