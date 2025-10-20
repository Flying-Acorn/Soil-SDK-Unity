using System;
using Newtonsoft.Json;
using static FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data
{
    [Serializable]
    public class AppParty
    {
        [JsonProperty] public string id;
        [JsonProperty] public ThirdParty party;
    }
}