#if UNITY_EDITOR
using FlyingAcorn.Soil.Core.Data;
using UnityEditor;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.Editor
{
    [InitializeOnLoad]
    internal static class SDKSettingsValidator
    {
        static SDKSettingsValidator()
        {
            // Delay call to avoid spamming on domain reload, run once after load.
            EditorApplication.delayCall += Validate;
        }

        private static void Validate()
        {
            var settings = Resources.Load<SDKSettings>(nameof(SDKSettings));
            if (settings == null)
            {
                Debug.LogWarning("[Soil] SDKSettings asset not found in Resources/. Some features (deeplink, etc.) may be skipped. Create it via FlyingAcorn > Soil > SDK Settings > Open or Create.");
                return;
            }

            // Optional: warn if deeplink is enabled but PaymentDeeplinkRoot is empty.
            if (settings.DeepLinkEnabled && string.IsNullOrEmpty(settings.PaymentDeeplinkRoot))
            {
                Debug.Log("[Soil] Deeplink is enabled but PaymentDeeplinkRoot is empty. The app will register a scheme-only URL (bundleId://). If providers send hostful links, handler will still match by scheme only as of this version.");
            }
        }
    }
}
#endif
