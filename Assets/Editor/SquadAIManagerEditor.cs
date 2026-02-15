#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SquadAIManager))]
public class SquadAIManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SquadAIManager manager = (SquadAIManager)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

        if (GUILayout.Button("Rebuild NavMesh Now"))
        {
            manager.DebugRebuildNavMesh();
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(manager);
            }
        }
    }
}
#endif
