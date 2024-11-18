using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.Data.BuildData.Editor
{
    public class FaPostBuildTools : UnityEditor.Editor, IPostprocessBuildWithReport
    {
        public int callbackOrder { get; }

        public void OnPostprocessBuild(BuildReport report)
        {
            Debug.Log(
                $"[FABuildTools] OnPostprocessBuild for target {report.summary.platform} at path {report.summary.outputPath}");

            var guids = AssetDatabase.FindAssets($"t:{typeof(BuildData)}");
            switch (guids.Length)
            {
                case > 1:
                    Debug.LogErrorFormat("[FABuildTools] Found more than 1 Build Properties: {0}. Using first one!",
                        guids.Length);
                    break;
                case <= 0:
                    throw new BuildFailedException("[FABuildTools] Couldn't find Build Settings, please create one!");
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var buildSettings = AssetDatabase.LoadAssetAtPath<BuildData>(path);
            buildSettings.StoreName = null;

            EditorUtility.SetDirty(buildSettings);
            Debug.LogFormat("[FABuildTools] Cleaned build settings \"{0}\".", path);
        }
    }
}