using System;
using UnityEditor;
using Debug = UnityEngine.Debug;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using FlyingAcorn.Tools;

public class PlotBuildTools : Editor, IPreprocessBuildWithReport
{
    public int callbackOrder
    {
        get { return 0; }
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        Debug.Log(
            $"[PlotBuildTools] OnPreprocessBuild for target {report.summary.platform} at path {report.summary.outputPath}");

        #region Set Build Properties LastBuildTime - Reference: https: //answers.unity.com/questions/1425758/how-can-i-find-all-instances-of-a-scriptable-objec.html
        //FindAssets uses tags check documentation for more info
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(BuildData)}");
        if (guids.Length > 1)
            Debug.LogErrorFormat("[PlotBuildTools] Found more than 1 Build Properties: {0}. Using first one!",
                guids.Length);

        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            BuildData buildSettings = AssetDatabase.LoadAssetAtPath<BuildData>(path);
            buildSettings.LastBuildTime = DateTime.Now.ToString("yyyy/MM/dd-HH:mm:ss"); // case sensitive
            buildSettings.EditorRefreshScriptingBackend(report.summary.platform);
            buildSettings.RepositoryVersion = BuildData.GetHgVersion();
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
            Debug.LogFormat("[PlotBuildTools] Updated settings LastBuildDate to \"{0}\". Settings Path: {1}",
                buildSettings.LastBuildTime, path);
        }
        else
        {
            // TODO: AUTO-CREATE ONE!
            Debug.LogWarning("[PlotBuildTools] Couldn't find Build Settings, please create one!");
        }
        #endregion
    }
}