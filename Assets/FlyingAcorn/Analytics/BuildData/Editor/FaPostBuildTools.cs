using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FlyingAcorn.Analytics.BuildData.Editor
{
    public class FaPostBuildTools : IPostprocessBuildWithReport
    {
        public int callbackOrder { get; }
        private const string ResourcesFolder = "Assets/Resources";
        private static readonly string AssetPath = ResourcesFolder + "/" + Constants.BuildSettingsName + ".asset";

        public void OnPostprocessBuild(BuildReport report)
        {
            Debug.Log(
                $"[FABuildTools] OnPostprocessBuild for target {report.summary.platform} at path {report.summary.outputPath}");

            // Prefer the canonical Resources path to match pre-build behavior
            var buildSettings = AssetDatabase.LoadAssetAtPath<BuildData>(AssetPath);
            
            if (buildSettings == null)
            {
                // Fallback: search for any BuildData if canonical path doesn't exist
                var guids = AssetDatabase.FindAssets($"t:{nameof(BuildData)}");
                if (guids == null || guids.Length == 0)
                {
                    Debug.LogWarning("[FABuildTools] No Build Properties found in post-build. This is expected if Analytics module is not used.");
                    return;
                }
                
                if (guids.Length > 1)
                {
                    Debug.LogWarningFormat("[FABuildTools] Found more than 1 Build Properties: {0}. Using first one!", guids.Length);
                }
                
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                buildSettings = AssetDatabase.LoadAssetAtPath<BuildData>(path);
                
                if (buildSettings == null)
                {
                    Debug.LogError($"[FABuildTools] Found BuildData GUID but failed to load asset at '{path}'.");
                    return;
                }
            }
            
            if (buildSettings.PreserveStoreAfterBuild)
            {
                Debug.LogFormat("[FABuildTools] Preserving store setting \"{0}\".", buildSettings.StoreName);
                return;
            }
            
            buildSettings.StoreName = Constants.Store.Unknown;
            EditorUtility.SetDirty(buildSettings);
            AssetDatabase.SaveAssets();
            Debug.LogFormat("[FABuildTools] Reset store to Unknown in BuildData.");
        }
    }
}
