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
        public string score;
        public long rank;
    }
}