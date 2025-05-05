using System.Collections.Generic;
using FlyingAcorn.Soil.Core.User;
using Newtonsoft.Json;

// ReSharper disable InconsistentNaming

namespace FlyingAcorn.Soil.Socialization.Models
{
    public class FriendsResponse
    {
        [JsonProperty] public FriendshipStatusResponse detail;
        [JsonProperty] public List<UserInfo> friends;
    }
}