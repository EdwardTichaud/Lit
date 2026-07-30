#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Lit.Timeline.Editor
{
    /// <summary>
    /// Keeps the Timeline system discoverable from its single Bootstrap entry
    /// point without coupling the runtime manager to editor-only APIs.
    /// </summary>
    [CustomEditor(typeof(TimelineManager))]
    public sealed class TimelineManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Timeline System", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "TimelineManager est le point d'entree unique dans Bootstrap. " +
                "Les fichiers ci-dessous restent separes car certains sont des composants de scene et d'autres des assets de configuration.",
                MessageType.Info);

            DrawScriptButton("1. Service Bootstrap", "TimelineManager");
            DrawScriptButton("2. Cibles de binding dans les scenes", "TimelineBindingTarget");
            DrawScriptButton("3. Profile de bindings (asset)", "TimelineBindingProfile");
            DrawScriptButton("4. Lecture, handle et contexte", "TimelinePlayback");
            DrawScriptButton("5. Priorite camera pendant lecture", "TimelineCameraOverride");
            DrawScriptButton("6. Deplacement joueur A vers B", "TimelinePlayerMoveTrack");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bridges de scene", EditorStyles.boldLabel);
            DrawScriptButton("Joueur local / preview", "LitTimelineLocalPlayerBinder");
            DrawScriptButton("Cinemachine", "LitTimelineCinemachineBridge");
            DrawScriptButton("Acteur de preview", "LitTimelinePreviewActor");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Outils editeur", EditorStyles.boldLabel);
            DrawScriptButton("Validation des bindings", "TimelineBindingsValidator");
            DrawScriptButton("Inspector des profiles", "TimelineBindingProfileEditor");
        }

        private static void DrawScriptButton(string label, string scriptName)
        {
            if (!GUILayout.Button(label))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets($"{scriptName} t:MonoScript");
            if (guids.Length == 0)
            {
                Debug.LogWarning($"[TimelineManager] Script '{scriptName}' introuvable.");
                return;
            }

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guids[0]));
            Selection.activeObject = script;
            EditorGUIUtility.PingObject(script);
        }
    }
}
#endif
