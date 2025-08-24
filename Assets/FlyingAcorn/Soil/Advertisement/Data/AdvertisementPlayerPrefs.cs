using System.Collections.Generic;
using FlyingAcorn.Soil.Advertisement.Models;
using FlyingAcorn.Soil.Core.User;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Advertisement.Data
{
    public static class AdvertisementPlayerPrefs
    {
        private static string AdvertisementKey => $"{UserPlayerPrefs.GetKeysPrefix()}advertisement";
        private static string CachedAssetsKey => $"{UserPlayerPrefs.GetKeysPrefix()}cached_assets";

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

        /// <summary>
        /// Gets or sets the cached assets metadata
        /// </summary>
        internal static List<AssetCacheEntry> CachedAssets
        {
            get
            {
                var assetsString = PlayerPrefs.GetString(CachedAssetsKey, string.Empty);
                return string.IsNullOrEmpty(assetsString)
                    ? new List<AssetCacheEntry>()
                    : JsonConvert.DeserializeObject<List<AssetCacheEntry>>(assetsString);
            }
            set
            {
                var assetsString = JsonConvert.SerializeObject(value);
                PlayerPrefs.SetString(CachedAssetsKey, assetsString);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Clears all cached advertisement data
        /// </summary>
        internal static void ClearAll()
        {
            PlayerPrefs.DeleteKey(AdvertisementKey);
            PlayerPrefs.DeleteKey(CachedAssetsKey);
            PlayerPrefs.Save();
        }
    }
}