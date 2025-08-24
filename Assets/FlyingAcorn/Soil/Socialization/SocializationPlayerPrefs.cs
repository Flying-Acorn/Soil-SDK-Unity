using System.Collections.Generic;
using FlyingAcorn.Soil.Core.User;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Socialization
{
    public static class SocializationPlayerPrefs
    {
    // Dynamic to reflect per-user prefix changes after account switch. If accessed in a hot path,
    // consider caching the computed value on user change to avoid repeated string allocations.
    private static string FriendsKey => $"{UserPlayerPrefs.GetKeysPrefix()}friends";
        private static string CacheKey(string leaderboardId, bool relative)
        {
            return FriendsKey + '_' + leaderboardId + (relative ? "_relative" : "");
        }

        public static List<UserInfo> Friends
        {
            get
            {
                var friendsString = PlayerPrefs.GetString(FriendsKey, string.Empty);
                return string.IsNullOrEmpty(friendsString)
                    ? new List<UserInfo>()
                    : JsonConvert.DeserializeObject<List<UserInfo>>(friendsString);
            }
            set
            {
                var friendsString = JsonConvert.SerializeObject(value);
                PlayerPrefs.SetString(FriendsKey, friendsString);
                PlayerPrefs.Save();
            }
        }

        internal static void SetCachedLeaderboardData(string leaderboardId, string data, bool relative)
        {
            PlayerPrefs.SetString(CacheKey(leaderboardId, relative), data);
        }

    }
}