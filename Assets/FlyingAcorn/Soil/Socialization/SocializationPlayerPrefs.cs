using System;
using System.Collections.Generic;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Leaderboard.Models;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Socialization
{
    public static class SocializationPlayerPrefs
    {
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

        /// <summary>
        /// Gets cached friends leaderboard data. Handles backward compatibility with old cache format.
        /// </summary>
        internal static LeaderboardResponse CachedLeaderboardData(string leaderboardId, bool relative)
        {
            var jsonString = PlayerPrefs.GetString(CacheKey(leaderboardId, relative), null);
            if (string.IsNullOrEmpty(jsonString))
                return null;

            try
            {
                // Try parsing as new LeaderboardResponse format first
                var response = JsonConvert.DeserializeObject<LeaderboardResponse>(jsonString);
                if (response?.user_scores != null)
                    return response;
            }
            catch (Exception)
            {
                // Parsing as LeaderboardResponse failed, try old format
            }

            try
            {
                // Fallback: try parsing as old List<UserScore> format and wrap it
                var scores = JsonConvert.DeserializeObject<List<UserScore>>(jsonString);
                if (scores != null)
                {
                    return new LeaderboardResponse
                    {
                        user_scores = scores,
                        iteration = 0,
                        next_reset = 0
                    };
                }
            }
            catch (Exception)
            {
                // Both formats failed, return null
            }

            return null;
        }

        internal static void SetCachedLeaderboardData(string leaderboardId, string data, bool relative)
        {
            PlayerPrefs.SetString(CacheKey(leaderboardId, relative), data);
        }

    }
}