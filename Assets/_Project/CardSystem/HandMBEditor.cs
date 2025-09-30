#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Project.CardSystem
{
    //Debug Editor Crash almaması için düzenledim
    [CustomEditor(typeof(HandMB))]
    public sealed class HandMBEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Title("Area & Prefab");
            Prop("bottomArea",  "Bottom Area");
            Prop("cardPrefab",  "Card Prefab");

            Space();
            Title("Card");
            Prop("cardSize",    "Card Size");

            Space();
            Title("Arc Layout");
            Prop("radius",        "Radius");
            Prop("maxFanAngle",   "Max Fan Angle (°)");
            Prop("overlap",       "Overlap");
            Prop("tiltScale",     "Tilt Scale");
            Prop("yOffset",       "Y Offset");
            Prop("centerOffset",  "Center Offset");
            Prop("arcDownwards",  "Arc Downwards");
            Prop("invertTilt",    "Invert Tilt");

            Space();
            Title("Neighbor Reveal");
            Prop("revealNeighbors", "Enable");
            Prop("neighborPush",    "Neighbor Push");
            Prop("neighborRange",   "Neighbor Range");
            Prop("neighborFalloff", "Neighbor Falloff");

            Space();
            Title("Generation");
            Prop("generateCount", "Generate Count");

            serializedObject.ApplyModifiedProperties();

            Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Set")) Run(t => t.Cmd_GenerateSet());
                if (GUILayout.Button("Regenerate"))   Run(t => t.Cmd_Regenerate());
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Random"))    Run(t => t.Cmd_AddRandom());
                if (GUILayout.Button("Remove Random")) Run(t => t.Cmd_RemoveRandom());
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("Play modunda çalışır. Referansları atamayı unutma. (Alan eksikse üstte uyarı görünür.)", MessageType.Info);
        }

        void Prop(string name, string label, bool includeChildren = true)
        {
            var p = serializedObject.FindProperty(name);
            if (p != null)
                EditorGUILayout.PropertyField(p, new GUIContent(label), includeChildren);
            else
                EditorGUILayout.HelpBox($"Necessary field: {name})", MessageType.Warning);
        }

        void Run(System.Action<HandMB> action)
        {
            var t = (HandMB)target;
            Undo.RegisterFullObjectHierarchyUndo(t.gameObject, "HandMB Action");
            action(t);
            EditorUtility.SetDirty(t);
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Repaint();
        }

        void Title(string s) => EditorGUILayout.LabelField(s, EditorStyles.boldLabel);
        void Space(float px = 6f) => EditorGUILayout.Space(px);
    }
}
#endif
