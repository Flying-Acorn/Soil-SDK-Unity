using System;
using System.Collections.Generic;
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
                SavedSettings = new PurchasingSettings(Constants.ApiUrl);
                MyDebug.Verbose("Soil ====> Resetting to default settings.");
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