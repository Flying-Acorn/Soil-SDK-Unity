using FlyingAcorn.Soil.Core.User;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FlyingAcorn.Soil.RemoteConfig
{
    internal static class RemoteConfigPlayerPrefs
    {
        internal static string PrefsPrefix => $"{UserPlayerPrefs.GetKeysPrefix()}remoteconfig_";
        private static string CacheKey => PrefsPrefix + "latest_remote_config_data";

        [UsedImplicitly] public static JObject ReceivedRemoteConfigData;
        
        internal static JObject CachedRemoteConfigData
        {
            get
            {
                if (ReceivedRemoteConfigData != null) return ReceivedRemoteConfigData;
                var jsonString = PlayerPrefs.GetString(CacheKey, "{}");
                ReceivedRemoteConfigData = JObject.Parse(jsonString);
                return ReceivedRemoteConfigData;
            }
            set
            {
                ReceivedRemoteConfigData = value;
                PlayerPrefs.SetString(CacheKey, ReceivedRemoteConfigData.ToString(Formatting.None));
            }
        }
    }
}