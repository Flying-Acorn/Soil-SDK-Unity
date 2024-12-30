using System;
using System.Collections.Generic;
using System.Linq;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using JetBrains.Annotations;
using UnityEngine;

namespace FlyingAcorn.Soil.Purchasing
{
    public static class DeeplinkHandler
    {
        private static readonly Uri PaymentDeeplink;
        [UsedImplicitly] internal static Action<Dictionary<string, string>> OnPaymentDeeplinkActivated;

        static DeeplinkHandler()
        {
            var sdkSettings = Resources.Load<SDKSettings>("SDKSettings");
            if (sdkSettings == null)
            {
                Debug.LogError("SDKSettings not found to initialize shop DeeplinkHandler");
                return;
            }

            DeepLinkHandler.OnDeepLinkActivated -= OnDeepLinkActivated;
            DeepLinkHandler.OnDeepLinkActivated += OnDeepLinkActivated;
            PaymentDeeplink = new Uri(PurchasingPlayerPrefs.GetPurchaseDeeplink());
            OnDeepLinkActivated(DeepLinkHandler.LastDeeplinkURL);
        }

        private static void OnDeepLinkActivated(Uri invokedUri)
        {
            if (invokedUri == null) return;
            MyDebug.Info(
                $"OnDeepLinkActivated {invokedUri.GetLeftPart(UriPartial.Authority)} {PaymentDeeplink.GetLeftPart(UriPartial.Authority)}");
            if (invokedUri.GetLeftPart(UriPartial.Authority) !=
                PaymentDeeplink.GetLeftPart(UriPartial.Authority)) return;
            var parameters = invokedUri.Query.Replace("?", "");
            var keyValuePairs = parameters.Split('&').Select(x => x.Split('=')).ToDictionary(x => x[0], x => x[1]);
            OnPaymentDeeplinkActivated?.Invoke(keyValuePairs);
        }
    }
}