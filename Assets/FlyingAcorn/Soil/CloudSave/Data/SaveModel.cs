using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FlyingAcorn.Soil.CloudSave.Data
{
    [Serializable]
    public class SaveModel
    {
        [JsonProperty] public string key;
        [JsonProperty] public object value;
    }
}