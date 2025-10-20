using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FlyingAcorn.Soil.Core.Data.BuildData.Editor
{
    public class FaPreBuildTools : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log(
                $"[FABuildTools] OnPreprocessBuild for target {report.summary.platform} at path {report.summary.outputPath}");

            //FindAssets uses tags check documentation for more info
            var guids = AssetDatabase.FindAssets($"t:{typeof(BuildData)}");
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
            switch (buildSettings.StoreName)
            {
                case Constants.Store.GooglePlay:
                case Constants.Store.CafeBazaar:
                case Constants.Store.Myket:
#if !UNITY_ANDROID
                    throw new BuildFailedException(
                        "[FABuildTools] Store name is set to Android store but the platform is not Android");
#endif
                    break;
                case Constants.Store.AppStore:
#if !UNITY_IOS
                        throw new BuildFailedException("[FABuildTools] Store name is set to iOS store but the platform is not iOS");
#endif
                    break;
                case Constants.Store.BetaChannel:
                case Constants.Store.Postman:
                case Constants.Store.Github:
                case Constants.Store.LandingPage:
                    break;
                case Constants.Store.Unknown:
                default:
                    throw new BuildFailedException("[FABuildTools] Store name is not set");
            }

            EditorUtility.SetDirty(buildSettings);
            Debug.LogFormat("[FABuildTools] Updated settings LastBuildDate to \"{0}\". Settings Path: {1}",
                buildSettings.LastBuildTime, path);
        }
    }
}