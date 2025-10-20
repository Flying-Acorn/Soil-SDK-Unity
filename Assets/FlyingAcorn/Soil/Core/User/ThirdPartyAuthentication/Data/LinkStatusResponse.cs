using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using FlyingAcorn.Soil.Core.Data;

// ReSharper disable InconsistentNaming

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data
{
    [Serializable]
    public class LinkStatusResponse
    {
        [JsonProperty] public Constants.LinkStatus code;
        [JsonProperty] public string message;
        [AllowNull] [JsonProperty] public AppParty app_party;
    }
}