using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace FlyingAcorn.Soil.Core.Data.BuildData.Editor
{
    public class FaBuildTools : UnityEditor.Editor, IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log(
                $"[FABuildTools] OnPreprocessBuild for target {report.summary.platform} at path {report.summary.outputPath}");

            #region Set Build Properties LastBuildTime - Reference: https: //answers.unity.com/questions/1425758/how-can-i-find-all-instances-of-a-scriptable-objec.html

            //FindAssets uses tags check documentation for more info
            var guids = AssetDatabase.FindAssets($"t:{typeof(Core.Data.BuildData.BuildData)}");
            if (guids.Length > 1)
                Debug.LogErrorFormat("[FABuildTools] Found more than 1 Build Properties: {0}. Using first one!",
                    guids.Length);

            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var buildSettings = AssetDatabase.LoadAssetAtPath<Core.Data.BuildData.BuildData>(path);
                buildSettings.LastBuildTime = DateTime.Now.ToString("yyyy/MM/dd-HH:mm:ss"); // case sensitive
                buildSettings.EditorRefreshScriptingBackend(report.summary.platform);
#if UNITY_IOS
                buildSettings.BuildNumber = PlayerSettings.iOS.buildNumber;
#endif
#if UNITY_ANDROID
                buildSettings.BuildNumber = PlayerSettings.Android.bundleVersionCode.ToString();
#endif
#if UNITY_CLOUD_BUILD
            buildSettings.RepositoryVersion += "-cloud";
#endif
                EditorUtility.SetDirty(buildSettings);
                Debug.LogFormat("[FABuildTools] Updated settings LastBuildDate to \"{0}\". Settings Path: {1}",
                    buildSettings.LastBuildTime, path);

                if (string.IsNullOrEmpty(buildSettings.StoreName))
                {
                    Debug.LogError("[FABuildTools] Please set store name through build_settings.asset, did you intentionally left it empty? please set it!");
                }
                else
                {
                    Debug.LogFormat("[FABuildTools] Store Name: {0}", buildSettings.StoreName);
                }
            }
            else
            {
                // TODO: AUTO-CREATE ONE!
                Debug.LogWarning("[FABuildTools] Couldn't find Build Settings, please create one!");
            }

            #endregion
        }
    }
}