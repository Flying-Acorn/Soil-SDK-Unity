using System;
using Newtonsoft.Json;

namespace FlyingAcorn.Soil.Core.Data
{
    [Serializable]
    public class AppParty
    {
        [JsonProperty] public string id;
        [JsonProperty] public ThirdPartyType party;
    }
}