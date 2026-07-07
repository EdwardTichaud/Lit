using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ScreenWaveController))]
[CanEditMultipleObjects]
public sealed class ScreenWaveControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("PlayScreenWave"))
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is ScreenWaveController controller)
                {
                    controller.PlayScreenWave();
                }
            }
        }
    }
}
