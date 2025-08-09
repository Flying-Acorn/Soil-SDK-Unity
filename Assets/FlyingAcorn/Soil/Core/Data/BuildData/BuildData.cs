using UnityEditor;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.Data.BuildData
{
    [CreateAssetMenu(fileName = Constants.BuildSettingsName + ".asset", menuName = "FlyingAcorn/Build Settings")]
    public class BuildData : ScriptableObject
    {
        public Constants.Store StoreName;
        [HideInInspector] public string BuildNumber;
        [HideInInspector] public string LastBuildTime;
        [HideInInspector] public string ScriptingBackend;

#if UNITY_EDITOR

        private void OnEnable()
        {
            EditorRefreshScriptingBackend(EditorUserBuildSettings.activeBuildTarget);
#if UNITY_IOS
            BuildNumber = PlayerSettings.iOS.buildNumber;
#endif
#if UNITY_ANDROID
            BuildNumber = PlayerSettings.Android.bundleVersionCode.ToString();
#endif
        }

        public void EditorRefreshScriptingBackend(BuildTarget buildTarget)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var group = BuildPipeline.GetBuildTargetGroup(target);

            ScriptingBackend = PlayerSettings.GetScriptingBackend(group).ToString();
        }
#endif
    }
}