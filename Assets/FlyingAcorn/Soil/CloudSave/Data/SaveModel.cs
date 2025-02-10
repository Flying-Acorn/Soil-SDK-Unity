using System;
using System.Diagnostics.CodeAnalysis;
using FlyingAcorn.Soil.Core.User;
using Newtonsoft.Json;

namespace FlyingAcorn.Soil.CloudSave.Data
{
    [Serializable]
    public class SaveModel
    {
        [JsonProperty] public string key;
        [JsonProperty] public object value;
        [JsonProperty][AllowNull] public UserInfo publicPlayerInfo;
    }
}