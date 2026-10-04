using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LitContrastLight))]
public sealed class LitContrastLightEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var contrastLight = (LitContrastLight)target;

        EditorGUILayout.HelpBox(
            "This prefab shapes local contrast. Its bloom intent is documentation only; bloom remains controlled by the active HDRP Volume.",
            MessageType.Info);

        DrawDefaultInspector();
        EditorGUILayout.Space();

        if (GUILayout.Button("Apply selected role preset"))
        {
            Undo.RecordObject(contrastLight, "Apply contrast light preset");
            contrastLight.ApplyRolePreset();
            EditorUtility.SetDirty(contrastLight);
        }

        if (GUILayout.Button("Apply current settings to Light"))
        {
            Undo.RecordObject(contrastLight, "Apply contrast light settings");
            contrastLight.ApplySettings();
            EditorUtility.SetDirty(contrastLight);
        }
    }
}