using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Purchasing.Models;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Purchasing
{
    public static class PurchasingPlayerPrefs
    {
        private static string PrefsPrefix => $"{UserPlayerPrefs.GetKeysPrefix()}purchasing_";

        public static PurchasingSettings SavedSettings
        {
            get
            {
                var jsonString = PlayerPrefs.GetString(PrefsPrefix + "purchasingSettings", null);
                if (string.IsNullOrEmpty(jsonString) || jsonString == "{}")
                    return new PurchasingSettings(Constants.ApiUrl);
                try
                {
                    return JsonConvert.DeserializeObject<PurchasingSettings>(jsonString);
                }
                catch (Exception e)
                {
                    MyDebug.LogWarning($"Soil ====> Failed to deserialize purchasing settings. Error: {e.Message}");
                    PlayerPrefs.SetString(PrefsPrefix + "purchasingSettings", "{}");
                    return new PurchasingSettings(Constants.ApiUrl);
                }
            }
            private set => PlayerPrefs.SetString(PrefsPrefix + "purchasingSettings", JsonConvert.SerializeObject(value));
        }

        /// <summary>
        /// Purchases awaiting verification. Always a set: ids are unique, non-empty and
        /// deduplicated on both read and write, so no caller can introduce a duplicate.
        /// </summary>
        public static List<string> UnverifiedPurchaseIds
        {
            get
            {
                var jsonString = PlayerPrefs.GetString(PrefsPrefix + "unverifiedPurchaseIds", "[]");
                try
                {
                    return Normalize(JsonConvert.DeserializeObject<List<string>>(jsonString));
                }
                catch (Exception e)
                {
                    MyDebug.LogWarning($"Soil ====> Failed to deserialize unverified purchase ids. Error: {e.Message}");
                    PlayerPrefs.SetString(PrefsPrefix + "unverifiedPurchaseIds", "[]");
                    return new List<string>();
                }
            }
            private set
            {
                PlayerPrefs.SetString(PrefsPrefix + "unverifiedPurchaseIds", JsonConvert.SerializeObject(Normalize(value)));
                PlayerPrefs.Save();
            }
        }

        private static List<string> Normalize(IEnumerable<string> ids)
        {
            if (ids == null)
                return new List<string>();
            return ids.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        }

        public static void RemoveUnverifiedPurchaseId(string purchaseID)
        {
            var unverifiedPurchaseIds = UnverifiedPurchaseIds;
            if (unverifiedPurchaseIds.RemoveAll(id => id == purchaseID) == 0)
                return;
            UnverifiedPurchaseIds = unverifiedPurchaseIds;
        }

        public static void AddUnverifiedPurchaseId(string purchaseId)
        {
            if (string.IsNullOrEmpty(purchaseId))
                return;
            var unverifiedPurchaseIds = UnverifiedPurchaseIds;
            if (unverifiedPurchaseIds.Contains(purchaseId))
                return;
            unverifiedPurchaseIds.Add(purchaseId);
            UnverifiedPurchaseIds = unverifiedPurchaseIds;
        }

        /// <summary>
        /// Unions server-reported pending ids into the local set in a single write.
        /// Returns the ids that were not already known, in the order supplied.
        /// </summary>
        internal static List<string> MergeUnverifiedPurchaseIds(IEnumerable<string> purchaseIds)
        {
            if (purchaseIds == null)
                return new List<string>();
            var unverifiedPurchaseIds = UnverifiedPurchaseIds;
            var known = new HashSet<string>(unverifiedPurchaseIds);
            var added = Normalize(purchaseIds).Where(id => known.Add(id)).ToList();
            if (added.Count == 0)
                return added;
            unverifiedPurchaseIds.AddRange(added);
            UnverifiedPurchaseIds = unverifiedPurchaseIds;
            return added;
        }

        public static string GetPurchaseDeeplink()
        {
            var settings = Resources.Load<SDKSettings>(nameof(SDKSettings));
            if (settings == null)
            {
                return null;
            }

            if (!settings.DeepLinkEnabled)
            {
                return null;
            }

            var bundleId = Application.identifier.ToLower();
            
            if (string.IsNullOrEmpty(settings.PaymentDeeplinkRoot))
            {
                return new Uri($"{bundleId}://").ToString();
            }
            
            var uri = new Uri($"{bundleId}://{settings.PaymentDeeplinkRoot}");
            return uri.ToString();
        }

        internal static async UniTask SetAlternateSettings(PurchasingSettings alternateSettings)
        {
            if (alternateSettings == null)
            {
                MyDebug.Verbose("Soil ====> No alternate settings provided, keeping existing settings.");
                return;
            }

            try
            {
                var savedUri = new Uri(SavedSettings.api ?? string.Empty);
                var alternateUri = new Uri(alternateSettings.api ?? string.Empty);
                
                if (Uri.Compare(savedUri, alternateUri, UriComponents.AbsoluteUri, UriFormat.UriEscaped, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    MyDebug.Verbose("Soil ====> Alternate purchasing settings are the same as saved settings. No need to update.");
                    return;
                }
            }
            catch (UriFormatException)
            {
                if (SavedSettings.api == alternateSettings.api)
                {
                    MyDebug.Verbose("Soil ====> Alternate purchasing settings are the same as saved settings. No need to update.");
                    return;
                }
            }

            try
            {
                await PurchasingSettings.Validate(alternateSettings);
            }
            catch (Exception e)
            {
                MyDebug.Info($"Soil ====> Invalid alternate purchasing settings. Error: {e.Message}");
                return;
            }

            SavedSettings = alternateSettings;
            MyDebug.Info($"Soil ====> Alternate purchasing settings updated successfully. new ApiUrl: {alternateSettings.api}");
        }

        internal static List<Item> CachedItems
        {
            get
            {
                try
                {
                    var itemsString = PlayerPrefs.GetString(PrefsPrefix + "cachedItems", "[]");
                    var items = JsonConvert.DeserializeObject<List<Item>>(itemsString);
                    return items;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Soil ====> Failed to deserialize cached items. Error: {e.Message}");
                    return new List<Item>();
                }
            }
            set
            {
                try
                {
                    PlayerPrefs.SetString(PrefsPrefix + "cachedItems", JsonConvert.SerializeObject(value));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Soil ====> Failed to serialize cached items. Error: {e.Message}");
                }
            }
        }
    }
}