using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace FlyingAcorn.Soil.Core.Data.BuildData.Editor
{
    public class FaPreBuildTools : UnityEditor.Editor, IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log(
                $"[FABuildTools] OnPreprocessBuild for target {report.summary.platform} at path {report.summary.outputPath}");

            #region Set Build Properties LastBuildTime - Reference: https: //answers.unity.com/questions/1425758/how-can-i-find-all-instances-of-a-scriptable-objec.html

            //FindAssets uses tags check documentation for more info
            var guids = AssetDatabase.FindAssets($"t:{typeof(BuildData)}");
            switch (guids.Length)
            {
                case > 1:
                    Debug.LogErrorFormat("[FABuildTools] Found more than 1 Build Properties: {0}. Using first one!",
                        guids.Length);
                    break;
                case <= 0:
                    throw new UnityEditor.Build.BuildFailedException("[FABuildTools] Couldn't find Build Settings, please create one!");
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var buildSettings = AssetDatabase.LoadAssetAtPath<BuildData>(path);
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
            if (string.IsNullOrWhiteSpace(buildSettings.StoreName))
                throw new UnityEditor.Build.BuildFailedException("[FABuildTools] Store Name is empty, please fill it in!");

            EditorUtility.SetDirty(buildSettings);
            Debug.LogFormat("[FABuildTools] Updated settings LastBuildDate to \"{0}\". Settings Path: {1}",
                buildSettings.LastBuildTime, path);

            #endregion
        }
    }
}