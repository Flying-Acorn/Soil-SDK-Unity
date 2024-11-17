using System;
using System.Collections.Generic;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Purchasing.Models;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Purchasing
{
    public static class PurchasingPlayerPrefs
    {
        private static string PrefsPrefix => $"{UserPlayerPrefs.GetKeysPrefix()}purchasing_";

        public static List<string> UnverifiedPurchaseIds
        {
            get
            {
                var jsonString = PlayerPrefs.GetString(PrefsPrefix + "unverifiedPurchaseIds", "[]");
                return JsonConvert.DeserializeObject<List<string>>(jsonString);
            }
            private set => PlayerPrefs.SetString(PrefsPrefix + "unverifiedPurchaseIds", JsonConvert.SerializeObject(value));
        }
        
        public static void RemoveUnverifiedPurchaseId(string purchaseID)
        {
            var unverifiedPurchaseIds = UnverifiedPurchaseIds;
            unverifiedPurchaseIds.Remove(purchaseID);
            UnverifiedPurchaseIds = unverifiedPurchaseIds;
        }

        public static void AddUnverifiedPurchaseId(string purchaseId)
        {
            var unverifiedPurchaseIds = UnverifiedPurchaseIds;
            unverifiedPurchaseIds.Add(purchaseId);
            UnverifiedPurchaseIds = unverifiedPurchaseIds;
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