using System.Collections.Generic;
using FlyingAcorn.Soil.Core.User;
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
                var jsonString = PlayerPrefs.GetString(PrefsPrefix + "unverifiedPurchaseIds", "{}");
                return JsonConvert.DeserializeObject<List<string>>(jsonString);
            }
            set => PlayerPrefs.SetString(PrefsPrefix + "unverifiedPurchaseIds", JsonConvert.SerializeObject(value));
        }
    }
}