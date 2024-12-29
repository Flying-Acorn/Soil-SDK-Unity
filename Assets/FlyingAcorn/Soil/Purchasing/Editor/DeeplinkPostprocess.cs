using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.Data.BuildData.Editor;
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
            var guids = AssetDatabase.FindAssets($"t:{typeof(SDKSettings)}");
            switch (guids.Length)
            {
                case > 1:
                    Debug.LogErrorFormat("[FABuildTools] Found more than 1 Build Properties: {0}. Using first one!",
                        guids.Length);
                    break;
                case <= 0:
                    throw new UnityEditor.Build.BuildFailedException(
                        "[FABuildTools] Couldn't find Build Settings, please create one!");
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var buildSettings = AssetDatabase.LoadAssetAtPath<SDKSettings>(path);
            if (string.IsNullOrEmpty(buildSettings.PaymentDeeplink)) return;
            Debug.LogError("[FABuildTools] Deeplink is empty, please set it in Build Settings!");
            DeeplinkTools.AddIOSDeeplink(report, buildSettings.PaymentDeeplink);
#endif
        }
    }
}