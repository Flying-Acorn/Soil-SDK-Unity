using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FlyingAcorn.Analytics.BuildData.Editor
{
    public class FaPreBuildTools : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log(
                $"[FABuildTools] OnPreprocessBuild for target {report.summary.platform} at path {report.summary.outputPath}");

            // Find the BuildData asset by type name (more robust than fully-qualified type in FindAssets)
            var guids = AssetDatabase.FindAssets($"t:{nameof(BuildData)}");
            
            // If no BuildData exists, Analytics module is optional - allow build to proceed
            if (guids.Length <= 0)
            {
                Debug.Log("[FABuildTools] No Build Properties found. Analytics module is optional; proceeding with build.");
                return;
            }
            
            if (guids.Length > 1)
            {
                Debug.LogErrorFormat("[FABuildTools] Found more than 1 Build Properties: {0}. Using first one!",
                    guids.Length);
            }
            
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var buildSettings = AssetDatabase.LoadAssetAtPath<BuildData>(path);

            if (buildSettings == null)
            {
                throw new BuildFailedException(
                    $"[FABuildTools] Found asset at '{path}' but failed to load as BuildData. Ensure a BuildData asset exists and matches the type.");
            }
            
            buildSettings.FillCurrentSettings();

            // Only enforce store selection if user has explicitly enabled it
            if (buildSettings.EnforceStoreOnBuild)
            {
                var selectedStore = ShowStoreSelectionDialog();
                if (selectedStore != Constants.Store.Unknown)
                {
                    buildSettings.StoreName = selectedStore;
                    EditorUtility.SetDirty(buildSettings);
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    Debug.LogWarning("[FABuildTools] Store name not selected. Proceeding with Unknown store. You can set it manually via AnalyticsManager.SetStore() at runtime.");
                }
            }

            switch (buildSettings.StoreName)
            {
                case Constants.Store.GooglePlay:
                case Constants.Store.CafeBazaar:
                case Constants.Store.Myket:
#if !UNITY_ANDROID
                    throw new BuildFailedException(
                        "[FABuildTools] Store name is set to Android store but the platform is not Android");
#else
                    break;
#endif
                case Constants.Store.AppStore:
#if !UNITY_IOS
                    throw new BuildFailedException("[FABuildTools] Store name is set to iOS store but the platform is not iOS");
#else
                    break;
#endif
                case Constants.Store.BetaChannel:
                case Constants.Store.Postman:
                case Constants.Store.Github:
                case Constants.Store.LandingPage:
                    break;
                case Constants.Store.Unknown:
                default:
                    break;
            }

            EditorUtility.SetDirty(buildSettings);
            Debug.LogFormat("[FABuildTools] Updated settings LastBuildDate to \"{0}\". Settings Path: {1}",
                buildSettings.LastBuildTime, path);
        }

        private Constants.Store ShowStoreSelectionDialog()
        {
            // Do not attempt to show UI in batchmode (e.g., CI/Cloud Build). Force the user to set it beforehand.
            if (Application.isBatchMode)
            {
                throw new BuildFailedException(
                    "[FABuildTools] Cannot prompt for Store selection in batchmode. Set StoreName in Build Settings or disable Enforce Store On Build.");
            }

            var window = ScriptableObject.CreateInstance<StoreSelectionWindow>();
            window.titleContent = new GUIContent("Select Store");
            // Provide a reasonable default size/position to avoid layout hiccups
            if (window.position.width <= 0 || window.position.height <= 0)
            {
                window.position = new Rect(100, 100, 400, 100);
            }

            try
            {
                // Use modal utility window which is safer for editor modal interactions
                window.ShowModalUtility();
                return window.SelectedStore;
            }
            finally
            {
                // Ensure resources are released to avoid TLS allocator warnings
                if (window != null)
                {
                    window.Close();
                    ScriptableObject.DestroyImmediate(window);
                }
                // Note: Avoid UnloadUnusedAssetsImmediate here as it may destroy assets still in use
            }
        }
    }

    internal class StoreSelectionWindow : EditorWindow
    {
        public Constants.Store SelectedStore = Constants.Store.Unknown;
        private string[] storeNames;
        private int selectedIndex = 0;

        void OnEnable()
        {
            storeNames = Enum.GetNames(typeof(Constants.Store));
            minSize = new Vector2(380, 80);
        }

        void OnGUI()
        {
            selectedIndex = EditorGUILayout.Popup("Store", selectedIndex, storeNames);
            if (GUILayout.Button("OK"))
            {
                SelectedStore = (Constants.Store)Enum.Parse(typeof(Constants.Store), storeNames[selectedIndex]);
                Close();
            }
            if (GUILayout.Button("Cancel"))
            {
                SelectedStore = Constants.Store.Unknown;
                Close();
            }
        }
    }
}
