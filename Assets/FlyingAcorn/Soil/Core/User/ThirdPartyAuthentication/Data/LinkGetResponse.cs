using System.Collections.Generic;
using Newtonsoft.Json;
// ReSharper disable InconsistentNaming

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data
{
    public class LinkGetResponse
    {
        [JsonProperty] public LinkStatusResponse detail;
        [JsonProperty] public List<LinkPostResponse> linked_accounts;
    }
}