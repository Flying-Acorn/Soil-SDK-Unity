using System;
using Newtonsoft.Json;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data
{
    [Serializable]
    public class UnlinkResponse
    {
        [JsonProperty] public LinkStatusResponse detail;
    }
}