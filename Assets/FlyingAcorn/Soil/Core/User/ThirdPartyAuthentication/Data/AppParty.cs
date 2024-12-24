using System;
using Newtonsoft.Json;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data
{
    [Serializable]
    public class AppParty
    {
        [JsonProperty] public string id;
        [JsonProperty] public string party;
    }
}