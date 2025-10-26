using UnityEditor;
using UnityEngine;

namespace FlyingAcorn.Analytics.BuildData.Editor
{
    [CustomEditor(typeof(BuildData))]
    public class BuildDataEditor : UnityEditor.Editor
    {
        private SerializedProperty _storeNameProp;
        private SerializedProperty _enforceStoreOnBuildProp;
        private SerializedProperty _preserveStoreAfterBuildProp;
        private SerializedProperty _buildNumberProp;
        private SerializedProperty _lastBuildTimeProp;
        private SerializedProperty _scriptingBackendProp;

        private void OnEnable()
        {
            _storeNameProp = serializedObject.FindProperty("StoreName");
            _enforceStoreOnBuildProp = serializedObject.FindProperty("EnforceStoreOnBuild");
            _preserveStoreAfterBuildProp = serializedObject.FindProperty("PreserveStoreAfterBuild");
            _buildNumberProp = serializedObject.FindProperty("BuildNumber");
            _lastBuildTimeProp = serializedObject.FindProperty("LastBuildTime");
            _scriptingBackendProp = serializedObject.FindProperty("ScriptingBackend");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            var buildData = (BuildData)target;

            // Header
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Flying Acorn Build Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // Store Configuration Section
            EditorGUILayout.LabelField("Store Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_storeNameProp, new GUIContent("Store Name", 
                "Target store for this build (GooglePlay, AppStore, etc.)"));
            EditorGUILayout.PropertyField(_enforceStoreOnBuildProp, new GUIContent("Enforce Store On Build", 
                "When enabled, build process will prompt for store selection if not set."));
            
            if (_enforceStoreOnBuildProp.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Build will be interrupted to prompt for store selection if StoreName is not set or is Unknown.", 
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Enforcement is disabled. Manually call AnalyticsManager.SetStore() in your code to set the store at runtime.", 
                    MessageType.Warning);
            }
            EditorGUILayout.PropertyField(_preserveStoreAfterBuildProp, new GUIContent("Preserve Store After Build", 
                "When enabled, the store setting will be preserved during the build process."));
            
            if (_preserveStoreAfterBuildProp.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Store setting will be preserved after build. Use this for projects where the target store is NOT going to change.", 
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Store setting will be reset to Unknown after successful build.", 
                    MessageType.Info);
            }
            
            EditorGUILayout.Space(10);

            // Build Information Section (Read-only)
            EditorGUILayout.LabelField("Build Information", EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(_buildNumberProp, new GUIContent("Build Number", 
                "Automatically populated from PlayerSettings during build."));
            EditorGUILayout.PropertyField(_lastBuildTimeProp, new GUIContent("Last Build Time", 
                "Timestamp of the last build that updated this asset."));
            EditorGUILayout.PropertyField(_scriptingBackendProp, new GUIContent("Scripting Backend", 
                "Scripting backend used in the last build."));
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space(10);

            // Refresh Button
            if (GUILayout.Button("Refresh Build Information"))
            {
                buildData.FillCurrentSettings();
                EditorUtility.SetDirty(buildData);
                serializedObject.Update();
            }
            
            EditorGUILayout.Space(5);
            
            // Information Box
            EditorGUILayout.HelpBox(
                "Build information is automatically updated during the build process. " +
                "This asset is optional - builds will proceed without it if Analytics module is not used.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
