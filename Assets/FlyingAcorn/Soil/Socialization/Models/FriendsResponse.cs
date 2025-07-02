using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;

// ReSharper disable InconsistentNaming

namespace FlyingAcorn.Soil.Socialization.Models
{
    public class FriendsResponse
    {
        [JsonProperty] public FriendshipStatusResponse detail;
        [JsonProperty][CanBeNull] public List<FriendInfo> friends;
    }
}