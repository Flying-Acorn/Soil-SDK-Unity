#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlyingAcorn.Soil.Advertisement.Models.AdPlacements;

namespace FlyingAcorn.Soil.Advertisement.Editor
{
    [CustomEditor(typeof(AdDisplayComponent))]
    [CanEditMultipleObjects]
    public class AdDisplayComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw all serialized fields as usual
            DrawDefaultInspector();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Apply Best Options"))
                {
                    foreach (var t in targets)
                    {
                        var comp = t as AdDisplayComponent;
                        if (comp == null) continue;

                        Undo.RecordObject(comp, "Apply Best Options");
                        comp.ApplyBestOptions(true);
                        EditorUtility.SetDirty(comp);
                    }
                }
            }
        }
    }
}
#endif
