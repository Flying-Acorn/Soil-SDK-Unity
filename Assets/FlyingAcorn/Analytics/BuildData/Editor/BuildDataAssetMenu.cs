using UnityEditor;
using UnityEngine;

namespace FlyingAcorn.Analytics.BuildData.Editor
{
    internal static class BuildDataAssetMenu
    {
        private const string ResourcesFolder = "Assets/Resources";
        private const string AssetPath = ResourcesFolder + "/" + Constants.BuildSettingsName + ".asset";

        [MenuItem("FlyingAcorn/Build Settings/Open or Create", priority = 10)]
        public static void OpenOrCreate()
        {
            // Try to find existing BuildData by GUID/type
            var guids = AssetDatabase.FindAssets($"t:{nameof(BuildData)}");
            BuildData asset = null;
            string path = null;
            if (guids != null && guids.Length > 0)
            {
                path = AssetDatabase.GUIDToAssetPath(guids[0]);
                asset = AssetDatabase.LoadAssetAtPath<BuildData>(path);
            }

            // If not found, or loaded asset is null, create a new one in Resources with correct name
            if (asset == null)
            {
                EnsureResourcesFolder();
                asset = ScriptableObject.CreateInstance<BuildData>();
#if UNITY_EDITOR
                asset.FillCurrentSettings();
#endif
                asset.EnforceStoreOnBuild = true; // Enable enforcement for manually created assets
                AssetDatabase.CreateAsset(asset, AssetPath);
                AssetDatabase.SaveAssets();
                path = AssetPath;
            }
            else if (!path.StartsWith(ResourcesFolder))
            {
                // Move to Resources to ensure availability on device
                EnsureResourcesFolder();
                var error = AssetDatabase.MoveAsset(path, AssetPath);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning($"[FABuildTools] Failed to move BuildData to Resources: {error}. A copy will be created under Resources.");
                    var copy = Object.Instantiate(asset);
                    AssetDatabase.CreateAsset(copy, AssetPath);
                    AssetDatabase.SaveAssets();
                    asset = copy;
                }
                path = AssetPath;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[FABuildTools] Build Settings ready at '{path}'. Ensure values are set before building.");
        }

        private static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
        }
    }
}
