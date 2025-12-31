using System;
using System.Collections.Generic;

namespace FlyingAcorn.Soil.Leaderboard.Models
{
    [System.Serializable]
    public class LeaderboardResponse
    {
        public List<UserScore> user_scores;
        public long iteration;
        public long? next_reset; // Unix timestamp, null if leaderboard doesn't reset
    }
}