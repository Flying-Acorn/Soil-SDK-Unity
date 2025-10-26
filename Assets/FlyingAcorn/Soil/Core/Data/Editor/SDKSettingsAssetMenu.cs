using UnityEditor;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.Data.Editor
{
    internal static class SDKSettingsAssetMenu
    {
        private const string ResourcesFolder = "Assets/Resources";
        private static readonly string AssetPath = ResourcesFolder + "/" + nameof(SDKSettings) + ".asset";

        [MenuItem("FlyingAcorn/Soil/SDK Settings/Open or Create", priority = 10)]
        public static void OpenOrCreate()
        {
            // Try to find existing SDKSettings by GUID/type
            var guids = AssetDatabase.FindAssets($"t:{nameof(SDKSettings)}");
            SDKSettings asset = null;
            string path = null;
            if (guids != null && guids.Length > 0)
            {
                path = AssetDatabase.GUIDToAssetPath(guids[0]);
                asset = AssetDatabase.LoadAssetAtPath<SDKSettings>(path);
            }

            // If not found, or loaded asset is null, create a new one in Resources with correct name
            if (asset == null)
            {
                EnsureResourcesFolder();
                asset = ScriptableObject.CreateInstance<SDKSettings>();
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
                    Debug.LogWarning($"[FASDK] Failed to move SDKSettings to Resources: {error}. A copy will be created under Resources.");
                    var copy = Object.Instantiate(asset);
                    AssetDatabase.CreateAsset(copy, AssetPath);
                    AssetDatabase.SaveAssets();
                    asset = copy;
                }
                path = AssetPath;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[FASDK] SDK Settings ready at '{path}'. Ensure values are set before building.");
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
