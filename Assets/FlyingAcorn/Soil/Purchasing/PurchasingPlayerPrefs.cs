using System;
using System.Collections.Generic;
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

        public static List<string> UnverifiedPurchaseIds
        {
            get
            {
                var jsonString = PlayerPrefs.GetString(PrefsPrefix + "unverifiedPurchaseIds", "[]");
                try
                {
                    return JsonConvert.DeserializeObject<List<string>>(jsonString);
                }
                catch (Exception e)
                {
                    MyDebug.LogWarning($"Soil ====> Failed to deserialize unverified purchase ids. Error: {e.Message}");
                    PlayerPrefs.SetString(PrefsPrefix + "unverifiedPurchaseIds", "[]");
                    return new List<string>();
                }
            }
            private set =>
                PlayerPrefs.SetString(PrefsPrefix + "unverifiedPurchaseIds", JsonConvert.SerializeObject(value));
        }

        public static void RemoveUnverifiedPurchaseId(string purchaseID)
        {
            var unverifiedPurchaseIds = UnverifiedPurchaseIds;
            unverifiedPurchaseIds.RemoveAll(id => id == purchaseID);
            UnverifiedPurchaseIds = unverifiedPurchaseIds;
        }

        public static void AddUnverifiedPurchaseId(string purchaseId)
        {
            var unverifiedPurchaseIds = UnverifiedPurchaseIds;
            unverifiedPurchaseIds.Add(purchaseId);
            UnverifiedPurchaseIds = unverifiedPurchaseIds;
        }

        public static string GetPurchaseDeeplink()
        {
            var settings = Resources.Load<SDKSettings>(nameof(SDKSettings));
            if (settings == null || string.IsNullOrEmpty(settings.PaymentDeeplinkRoot))
            {
                MyDebug.LogWarning("[Soil] Payment deeplink is not set in SDKSettings. Please set it.");
                return null;
            }
            var bundleId = Application.identifier.ToLower();
            var uri = new Uri($"{bundleId}://{settings.PaymentDeeplinkRoot}");
            return uri.ToString();
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