using FlyingAcorn.Soil.Advertisement.Models;
using FlyingAcorn.Soil.Core.User;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Advertisement.Data
{
    public static class AdvertisementPlayerPrefs
    {
        private static readonly string AdvertisementKey = $"{UserPlayerPrefs.GetKeysPrefix()}advertisement";

        internal static Campaign CachedCampaign
        {
            get
            {
                var campaignString = PlayerPrefs.GetString(AdvertisementKey, string.Empty);
                return string.IsNullOrEmpty(campaignString)
                    ? null
                    : JsonConvert.DeserializeObject<Campaign>(campaignString);
            }
            set
            {
                var campaignString = JsonConvert.SerializeObject(value);
                PlayerPrefs.SetString(AdvertisementKey, campaignString);
                PlayerPrefs.Save();
            }
        }
    }
}