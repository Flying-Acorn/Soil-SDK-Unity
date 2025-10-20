using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.Data.BuildData.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace FlyingAcorn.Soil.Purchasing.Editor
{
    public class DeeplinkPreprocess : IPreprocessBuildWithReport
    {
        public int callbackOrder { get; }

        public void OnPreprocessBuild(BuildReport report)
        {
#if UNITY_ANDROID
            var settings = UnityEngine.Resources.Load<SDKSettings>(nameof(SDKSettings));
            if (settings == null)
            {
                UnityEngine.Debug.LogWarning("[FABuildTools] SDKSettings not found, skipping deeplink setup.");
                return;
            }

            if (!settings.DeepLinkEnabled)
            {
                UnityEngine.Debug.Log("[FABuildTools] Deeplink is disabled in SDKSettings, skipping deeplink setup.");
                return;
            }

            var link = PurchasingPlayerPrefs.GetPurchaseDeeplink();
            if (string.IsNullOrEmpty(link))
            {
                throw new UnityEditor.Build.BuildFailedException(
                    "[FABuildTools] Purchase deeplink is empty! DeepLinkEnabled is true but failed to generate deeplink. Please check SDKSettings configuration.");
            }
            DeeplinkTools.AddAndroidDeeplink(link);
#endif
        }
    }
}
