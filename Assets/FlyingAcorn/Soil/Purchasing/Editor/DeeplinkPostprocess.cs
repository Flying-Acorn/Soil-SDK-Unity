using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Analytics.BuildData.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FlyingAcorn.Soil.Purchasing.Editor
{
    public class DeeplinkPostprocess : IPostprocessBuildWithReport
    {
        public int callbackOrder { get; }

        public void OnPostprocessBuild(BuildReport report)
        {
#if UNITY_IOS
            var settings = Resources.Load<SDKSettings>(nameof(SDKSettings));
            if (settings == null)
            {
                Debug.LogWarning("[FABuildTools] SDKSettings not found, skipping deeplink setup.");
                return;
            }

            if (!settings.DeepLinkEnabled)
            {
                Debug.Log("[FABuildTools] Deeplink is disabled in SDKSettings, skipping deeplink setup.");
                return;
            }

            var link = PurchasingPlayerPrefs.GetPurchaseDeeplink();
            if (string.IsNullOrEmpty(link))
            {
                throw new UnityEditor.Build.BuildFailedException(
                    "[FABuildTools] Purchase deeplink is empty! DeepLinkEnabled is true but failed to generate deeplink. Please check SDKSettings configuration.");
            }
            DeeplinkTools.AddIOSDeeplink(report, link);
#endif
        }
    }
}
