using System;
using JetBrains.Annotations;

// ReSharper disable InconsistentNaming

namespace FlyingAcorn.Soil.Leaderboard.Models
{
    [UsedImplicitly]
    [Serializable]
    public class UserScore
    {
        public string name;
        public string uuid;
        public string avatar_asset;
        // public string score; Deprecated, use score_scientific instead
        public ScientificScore score_scientific;
        public long rank;
    }
}