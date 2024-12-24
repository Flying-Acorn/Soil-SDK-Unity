// ReSharper disable InconsistentNaming

using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using UnityEngine.Serialization;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data
{
    [Serializable]
    public class LinkPostResponse
    {
        [JsonProperty] public LinkStatusResponse detail;
        [JsonProperty] public LinkAccountInfo social_account_info;
        [AllowNull] [JsonProperty] public AlternateUser alternate_user;
    }
}