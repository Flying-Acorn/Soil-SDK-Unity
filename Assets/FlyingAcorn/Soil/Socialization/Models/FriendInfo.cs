using FlyingAcorn.Soil.Core.User;
using Newtonsoft.Json;

namespace FlyingAcorn.Soil.Socialization.Models
{
    public class FriendInfo : UserInfo
    {
        [JsonProperty] internal string friendship_created_at;
    }
}
