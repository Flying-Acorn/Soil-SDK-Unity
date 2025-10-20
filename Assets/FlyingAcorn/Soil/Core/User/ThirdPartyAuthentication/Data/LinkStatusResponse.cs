using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using static FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

// ReSharper disable InconsistentNaming

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data
{
    [Serializable]
    public class LinkStatusResponse
    {
        [JsonProperty] public LinkStatus code;
        [JsonProperty] public string message;
        [AllowNull] [JsonProperty] public AppParty app_party;
    }
}