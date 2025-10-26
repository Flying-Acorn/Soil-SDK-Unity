using System;
using System.IO;
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
        private const string ResourcesFolder = "Assets/Resources";
        private static readonly string AssetPath = ResourcesFolder + "/" + Constants.BuildSettingsName + ".asset";

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log(
                $"[FABuildTools] OnPreprocessBuild for target {report.summary.platform} at path {report.summary.outputPath}");

            // For iOS builds, proactively clean non-empty output folder to avoid Unity postprocessor 'Directory not empty' errors
            EnsureIOSOutputDirectoryClean(report);

            // Find the BuildData asset by type name (more robust than fully-qualified type in FindAssets)
            var guids = AssetDatabase.FindAssets($"t:{nameof(BuildData)}");

            // Ensure we end up with a BuildData inside Resources so it is available at runtime on device
            string path = null;
            BuildData buildSettings = null;

            if (guids.Length <= 0)
            {
                Debug.Log("[FABuildTools] No Build Properties found. Creating a default one under Resources for runtime availability.");
                EnsureResourcesFolder();
                buildSettings = ScriptableObject.CreateInstance<BuildData>();
                buildSettings.EnforceStoreOnBuild = false; // Disable enforcement for auto-created assets
                AssetDatabase.CreateAsset(buildSettings, AssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                // Reload to ensure we have a proper reference
                buildSettings = AssetDatabase.LoadAssetAtPath<BuildData>(AssetPath);
                if (buildSettings == null)
                {
                    throw new BuildFailedException($"[FABuildTools] Failed to create BuildData at '{AssetPath}'.");
                }
                buildSettings.FillCurrentSettings();
                EditorUtility.SetDirty(buildSettings);
                AssetDatabase.SaveAssets();
                path = AssetPath;
            }
            else
            {
                // Prefer the asset under Resources with the canonical name
                string preferredGuid = null;
                foreach (var guid in guids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.Equals(p, AssetPath, StringComparison.Ordinal))
                    {
                        preferredGuid = guid;
                        break;
                    }
                }

                if (preferredGuid != null)
                {
                    path = AssetDatabase.GUIDToAssetPath(preferredGuid);
                    buildSettings = AssetDatabase.LoadAssetAtPath<BuildData>(path);
                }
                else
                {
                    // Load the first found and ensure a copy exists at the canonical Resources path
                    var firstPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var firstAsset = AssetDatabase.LoadAssetAtPath<BuildData>(firstPath);
                    if (firstAsset == null)
                    {
                        throw new BuildFailedException($"[FABuildTools] Found asset at '{firstPath}' but failed to load as BuildData.");
                    }

                    EnsureResourcesFolder();
                    var existingAtCanonical = AssetDatabase.LoadAssetAtPath<BuildData>(AssetPath);
                    if (existingAtCanonical == null)
                    {
                        // Safer during builds: create a copy at the canonical path instead of moving the original
                        var copy = UnityEngine.Object.Instantiate(firstAsset);
                        AssetDatabase.CreateAsset(copy, AssetPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                        buildSettings = AssetDatabase.LoadAssetAtPath<BuildData>(AssetPath);
                        if (buildSettings == null)
                        {
                            throw new BuildFailedException($"[FABuildTools] Failed to ensure BuildData at canonical path '{AssetPath}'.");
                        }
                    }
                    else
                    {
                        buildSettings = existingAtCanonical;
                    }
                    path = AssetPath;
                }

                if (guids.Length > 1)
                {
                    Debug.LogWarningFormat("[FABuildTools] Found more than 1 Build Properties: {0}. Using '{1}'.", guids.Length, path);
                }
            }

            if (buildSettings == null)
            {
                throw new BuildFailedException(
                    $"[FABuildTools] Found asset at '{path}' but failed to load as BuildData. Ensure a BuildData asset exists and matches the type.");
            }
            
            buildSettings.FillCurrentSettings();
            // Ensure BuildNumber reflects the actual target platform being built (not just compile-time defines)
            try
            {
                switch (report.summary.platform)
                {
                    case BuildTarget.iOS:
                        buildSettings.BuildNumber = PlayerSettings.iOS.buildNumber;
                        break;
                    case BuildTarget.Android:
                        buildSettings.BuildNumber = PlayerSettings.Android.bundleVersionCode.ToString();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FABuildTools] Failed to update BuildNumber for target {report.summary.platform}: {ex.Message}");
            }

            // Only enforce store selection if user has explicitly enabled it
            if (buildSettings.EnforceStoreOnBuild && buildSettings.StoreName == Constants.Store.Unknown)
            {
                // Do not attempt to show UI in batchmode (e.g., CI/Cloud Build). Force the user to set it beforehand.
                if (Application.isBatchMode)
                {
                    throw new BuildFailedException(
                        "[FABuildTools] Cannot prompt for Store selection in batchmode. Set StoreName in Build Settings and disable Enforce Store On Build.");
                }

                var title = "Store not set";
                var message =
                    "StoreName is Unknown. It's recommended to set the correct target store before building.\n\n" +
                    "Choose an action:";
                // 0: Open Build Settings (recommended), 1: Proceed as Unknown, 2: Cancel Build
                var choice = EditorUtility.DisplayDialogComplex(title, message,
                    "Open Build Settings", "Proceed as Unknown", "Cancel Build");

                if (choice == 0)
                {
                    // Focus and ping the BuildData asset to let the user set it, then cancel the build cleanly
                    Selection.activeObject = buildSettings;
                    EditorGUIUtility.PingObject(buildSettings);
                    throw new BuildFailedException(
                        "[FABuildTools] Build canceled so you can set the Store in the Build Settings asset. Re-run the build after updating.");
                }
                if (choice == 2)
                {
                    throw new BuildFailedException("[FABuildTools] Build canceled by user.");
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
                    Debug.LogWarning("[FABuildTools] Proceeding with Unknown store. You can set it later via AnalyticsManager.SetStore() at runtime.");
                    break;
                default:
                    break;
            }

            EditorUtility.SetDirty(buildSettings);
            AssetDatabase.SaveAssets();
            Debug.LogFormat("[FABuildTools] Updated settings LastBuildDate to {0}. Settings Path: {1}, other details: Store={2}, ScriptingBackend={3}",
                buildSettings.LastBuildTime, path, buildSettings.StoreName, buildSettings.ScriptingBackend);
            // Note: we no longer create temporary assets; nothing to clean up here.
        }

        private static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
        }

        private static void EnsureIOSOutputDirectoryClean(BuildReport report)
        {
            if (report == null) return;
            if (report.summary.platform != BuildTarget.iOS) return;

            var outputPath = report.summary.outputPath;
            if (string.IsNullOrEmpty(outputPath)) return;

            try
            {
                if (!Directory.Exists(outputPath)) return;

                using (var enumerator = Directory.EnumerateFileSystemEntries(outputPath).GetEnumerator())
                {
                    if (!enumerator.MoveNext()) return; // Empty directory
                }

                if (Application.isBatchMode)
                {
                    Debug.Log($"[FABuildTools] Cleaning existing iOS output directory: {outputPath}");
                    FileUtil.DeleteFileOrDirectory(outputPath);
                    AssetDatabase.Refresh();
                }
                else
                {
                    var choice = EditorUtility.DisplayDialogComplex(
                        "iOS Output Folder Not Empty",
                        $"The iOS build output folder exists and is not empty:\n{outputPath}\n\nUnity may fail replacing it. Clean it now?",
                        "Clean and Continue",
                        "Continue (Risk)",
                        "Cancel Build");

                    if (choice == 0)
                    {
                        FileUtil.DeleteFileOrDirectory(outputPath);
                        AssetDatabase.Refresh();
                    }
                    else if (choice == 2)
                    {
                        throw new BuildFailedException("[FABuildTools] Build canceled by user.");
                    }
                    // choice == 1 -> continue without cleaning
                }
            }
            catch (Exception ex)
            {
                var msg = $"[FABuildTools] Failed to clean iOS output directory '{outputPath}': {ex.Message}";
                if (Application.isBatchMode)
                    throw new BuildFailedException(msg + ". Delete it manually and retry.");
                Debug.LogWarning(msg);
            }
        }

    }
}
