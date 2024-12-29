using System;
using System.Collections.Generic;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using JetBrains.Annotations;
using UnityEngine;

namespace FlyingAcorn.Soil.Purchasing
{
    public static class DeeplinkHandler
    {
        private static readonly Uri PaymentDeeplink;
        [UsedImplicitly] public static Action<Dictionary<string, string>> OnPaymentDeeplinkActivated;

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
            PaymentDeeplink = new Uri(sdkSettings.PaymentDeeplink);
        }

        private static void OnDeepLinkActivated(string arg1, Dictionary<string, string> arg2)
        {
            if (arg1 != PaymentDeeplink.AbsolutePath) return;
            MyDebug.Info("Payment deeplink activated");
            OnPaymentDeeplinkActivated?.Invoke(arg2);
        }
    }
}