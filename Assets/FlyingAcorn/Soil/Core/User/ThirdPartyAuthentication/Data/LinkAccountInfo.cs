using System;
using Newtonsoft.Json;

// ReSharper disable InconsistentNaming

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data
{
    [Serializable]
    public class LinkAccountInfo
    {
        [JsonProperty] public string social_account_id { get; set; } = string.Empty;
        [JsonProperty] public string email { get; set; } = string.Empty;
        [JsonProperty] public string name { get; set; } = string.Empty;
        [JsonProperty] public string last_name { get; set; } = string.Empty;
        [JsonProperty] public string display_name { get; set; } = string.Empty;
        [JsonProperty] public string profile_picture { get; set; } = string.Empty;
        [JsonProperty] public string extra_data { get; set; } = string.Empty;
        // Signed by the provider; the server matches the link on its claims, not on social_account_id.
        [JsonProperty] public string id_token { get; set; } = string.Empty;
    }
}