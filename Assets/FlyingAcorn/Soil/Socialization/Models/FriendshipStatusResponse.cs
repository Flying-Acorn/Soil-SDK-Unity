using System;
using Newtonsoft.Json;

namespace FlyingAcorn.Soil.Socialization.Models
{
    [Serializable]
    public class FriendshipStatusResponse
    {
        [JsonProperty] public Constants.FriendshipStatus code;
        [JsonProperty] public string message;
    }
}