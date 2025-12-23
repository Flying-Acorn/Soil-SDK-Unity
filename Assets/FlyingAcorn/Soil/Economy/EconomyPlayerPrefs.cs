using System.Collections.Generic;
using System;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Economy.Models;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Economy
{
    /// <summary>
    /// Static class for managing economy data in local player preferences for caching and offline access.
    /// </summary>
    public static class EconomyPlayerPrefs
    {
        private static string VirtualCurrenciesKey => $"{UserPlayerPrefs.GetKeysPrefix()}economy_virtual_currencies";
        private static string InventoryItemsKey => $"{UserPlayerPrefs.GetKeysPrefix()}economy_inventory_items";

        /// <summary>
        /// Gets the cached virtual currencies.
        /// </summary>
        public static List<UserVirtualCurrency> VirtualCurrencies
        {
            get
            {
                var data = PlayerPrefs.GetString(VirtualCurrenciesKey, string.Empty);
                if (string.IsNullOrEmpty(data))
                    return new List<UserVirtualCurrency>();

                try
                {
                    return JsonConvert.DeserializeObject<List<UserVirtualCurrency>>(data);
                }
                catch (Exception e)
                {
                    MyDebug.LogWarning($"Soil ====> Failed to deserialize virtual currencies. Error: {e.Message}");
                    return new List<UserVirtualCurrency>();
                }
            }
        }

        /// <summary>
        /// Gets the cached inventory items.
        /// </summary>
        public static List<UserInventoryItem> InventoryItems
        {
            get
            {
                var data = PlayerPrefs.GetString(InventoryItemsKey, string.Empty);
                if (string.IsNullOrEmpty(data))
                    return new List<UserInventoryItem>();

                try
                {
                    return JsonConvert.DeserializeObject<List<UserInventoryItem>>(data);
                }
                catch (Exception e)
                {
                    MyDebug.LogWarning($"Soil ====> Failed to deserialize inventory items. Error: {e.Message}");
                    return new List<UserInventoryItem>();
                }
            }
        }

        internal static void SetVirtualCurrencies(List<UserVirtualCurrency> virtualCurrencies)
        {
            var data = JsonConvert.SerializeObject(virtualCurrencies);
            PlayerPrefs.SetString(VirtualCurrenciesKey, data);
            PlayerPrefs.Save();
        }

        internal static void SetInventoryItems(List<UserInventoryItem> inventoryItems)
        {
            var data = JsonConvert.SerializeObject(inventoryItems);
            PlayerPrefs.SetString(InventoryItemsKey, data);
            PlayerPrefs.Save();
        }
    }
}
