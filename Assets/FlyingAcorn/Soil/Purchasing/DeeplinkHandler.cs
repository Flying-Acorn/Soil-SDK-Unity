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
            if (invokedUri.GetLeftPart(UriPartial.Authority) !=
                PaymentDeeplink.GetLeftPart(UriPartial.Authority)) return;
            MyDebug.Info(
                $"OnDeepLinkActivated {invokedUri.GetLeftPart(UriPartial.Authority)} {PaymentDeeplink.GetLeftPart(UriPartial.Authority)}");
            var parameters = invokedUri.Query.TrimStart('?').Split('&');
            var keyValuePairs = parameters.Select(parameter => parameter.Split('=')).Where(pair => pair.Length == 2)
                .ToDictionary(pair => pair[0], pair => pair[1]);
            OnPaymentDeeplinkActivated?.Invoke(keyValuePairs);
        }
    }
}