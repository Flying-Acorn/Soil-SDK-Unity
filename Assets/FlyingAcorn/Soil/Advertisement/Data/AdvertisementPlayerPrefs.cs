using System.Collections.Generic;
using System.Linq;
using FlyingAcorn.Soil.Advertisement.Models;
using FlyingAcorn.Soil.Core.User;
using Newtonsoft.Json;
using UnityEngine;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Data
{
    public static class AdvertisementPlayerPrefs
    {
        private const int MaxHistorySize = 10;

        private static string CachedAdGroupsKey => $"{UserPlayerPrefs.GetKeysPrefix()}advertisement_ad_groups";
        private static string CachedAssetsKey => $"{UserPlayerPrefs.GetKeysPrefix()}cached_assets";
        private static string RecentAdGroupIdsKey => $"{UserPlayerPrefs.GetKeysPrefix()}recent_ad_group_ids";
        private static string RecentCampaignIdsKey => $"{UserPlayerPrefs.GetKeysPrefix()}recent_campaign_ids";

        /// <summary>
        /// Gets or sets the currently cached ad group selected for each ad format
        /// </summary>
        internal static Dictionary<AdFormat, AdGroup> CachedAdGroups
        {
            get
            {
                var jsonString = PlayerPrefs.GetString(CachedAdGroupsKey, string.Empty);
                if (string.IsNullOrEmpty(jsonString)) return new Dictionary<AdFormat, AdGroup>();
                try
                {
                    return JsonConvert.DeserializeObject<Dictionary<AdFormat, AdGroup>>(jsonString)
                           ?? new Dictionary<AdFormat, AdGroup>();
                }
                catch
                {
                    Debug.LogError($"Failed to deserialize cached ad groups: {jsonString}");
                    return new Dictionary<AdFormat, AdGroup>();
                }
            }
            set
            {
                var jsonString = JsonConvert.SerializeObject(value);
                PlayerPrefs.SetString(CachedAdGroupsKey, jsonString);
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
                if (string.IsNullOrEmpty(assetsString)) return new List<AssetCacheEntry>();
                try
                {
                    return JsonConvert.DeserializeObject<List<AssetCacheEntry>>(assetsString)
                           ?? new List<AssetCacheEntry>();
                }
                catch
                {
                    Debug.LogError($"Failed to deserialize cached assets: {assetsString}");
                    return new List<AssetCacheEntry>();
                }
            }
            set
            {
                var assetsString = JsonConvert.SerializeObject(value);
                PlayerPrefs.SetString(CachedAssetsKey, assetsString);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Rolling list of recently shown ad group IDs, used for frequency-capping requests to the AdGroup selection API
        /// </summary>
        internal static List<string> RecentAdGroupIds
        {
            get
            {
                var jsonString = PlayerPrefs.GetString(RecentAdGroupIdsKey, string.Empty);
                return string.IsNullOrEmpty(jsonString)
                    ? new List<string>()
                    : JsonConvert.DeserializeObject<List<string>>(jsonString);
            }
            private set
            {
                var jsonString = JsonConvert.SerializeObject(value);
                PlayerPrefs.SetString(RecentAdGroupIdsKey, jsonString);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Rolling list of recently shown campaign IDs, used for frequency-capping requests to the AdGroup selection API
        /// </summary>
        internal static List<string> RecentCampaignIds
        {
            get
            {
                var jsonString = PlayerPrefs.GetString(RecentCampaignIdsKey, string.Empty);
                return string.IsNullOrEmpty(jsonString)
                    ? new List<string>()
                    : JsonConvert.DeserializeObject<List<string>>(jsonString);
            }
            private set
            {
                var jsonString = JsonConvert.SerializeObject(value);
                PlayerPrefs.SetString(RecentCampaignIdsKey, jsonString);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Records that an ad group (and its campaign) was just shown, appending to the rolling
        /// frequency-capping history and trimming to <see cref="MaxHistorySize"/>.
        /// </summary>
        internal static void RecordShownAdGroup(AdGroup adGroup)
        {
            if (adGroup == null) return;

            if (!string.IsNullOrEmpty(adGroup.id))
            {
                var adGroupIds = RecentAdGroupIds;
                adGroupIds.RemoveAll(id => id == adGroup.id);
                adGroupIds.Add(adGroup.id);
                if (adGroupIds.Count > MaxHistorySize)
                    adGroupIds = adGroupIds.Skip(adGroupIds.Count - MaxHistorySize).ToList();
                RecentAdGroupIds = adGroupIds;
            }

            if (!string.IsNullOrEmpty(adGroup.campaign_id))
            {
                var campaignIds = RecentCampaignIds;
                campaignIds.RemoveAll(id => id == adGroup.campaign_id);
                campaignIds.Add(adGroup.campaign_id);
                if (campaignIds.Count > MaxHistorySize)
                    campaignIds = campaignIds.Skip(campaignIds.Count - MaxHistorySize).ToList();
                RecentCampaignIds = campaignIds;
            }
        }

        /// <summary>
        /// Clears all cached advertisement data
        /// </summary>
        internal static void ClearAll()
        {
            PlayerPrefs.DeleteKey(CachedAdGroupsKey);
            PlayerPrefs.DeleteKey(CachedAssetsKey);
            PlayerPrefs.DeleteKey(RecentAdGroupIdsKey);
            PlayerPrefs.DeleteKey(RecentCampaignIdsKey);
            PlayerPrefs.Save();
        }
    }
}
