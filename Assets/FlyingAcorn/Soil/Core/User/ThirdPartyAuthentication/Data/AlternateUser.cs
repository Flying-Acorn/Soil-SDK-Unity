using System;
using FlyingAcorn.Soil.Core.Data;
using Newtonsoft.Json;
// ReSharper disable InconsistentNaming

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data
{
    [Serializable]
    public class AlternateUser : UserInfo
    {
        [JsonProperty] public TokenData tokens;
    }
}