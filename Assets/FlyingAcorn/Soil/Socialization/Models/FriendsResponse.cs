using System.Collections.Generic;
using FlyingAcorn.Soil.Core.User;
using JetBrains.Annotations;
using Newtonsoft.Json;

// ReSharper disable InconsistentNaming

namespace FlyingAcorn.Soil.Socialization.Models
{
    public class FriendsResponse
    {
        [JsonProperty] public FriendshipStatusResponse detail;
        [JsonProperty][CanBeNull] public List<UserInfo> friends;
    }
}