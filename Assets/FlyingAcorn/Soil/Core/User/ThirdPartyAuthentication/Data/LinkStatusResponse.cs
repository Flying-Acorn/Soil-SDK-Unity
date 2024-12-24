using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using UnityEngine.Serialization;

// ReSharper disable InconsistentNaming

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data
{
    [Serializable]
    public class LinkStatusResponse
    {
        [JsonProperty] public int code;
        [JsonProperty] public string message;
        [AllowNull] [JsonProperty] public AppParty app_party;
    }
}