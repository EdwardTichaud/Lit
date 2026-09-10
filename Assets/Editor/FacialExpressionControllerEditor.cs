using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FacialExpressionController))]
public sealed class FacialExpressionControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var data = PlayerSettingsAuthoring.ResolveData((FacialExpressionController)target);
        if (data != null && GUILayout.Button("Ouvrir la configuration du visage")) Selection.activeObject = data;
        EditorGUILayout.LabelField(new GUIContent("Tester les expressions", "Commandes de diagnostic du visage; aucun composant runtime supplementaire."), EditorStyles.boldLabel);
        var face = (FacialExpressionController)target;
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            foreach (FacialEmotion emotion in System.Enum.GetValues(typeof(FacialEmotion)))
                if (GUILayout.Button("Tester : " + emotion)) face.PlayEmotion(emotion);
            if (GUILayout.Button("Revenir au repos")) face.ReturnToIdle();
        }
        if (GUILayout.Button("Verifier les presets")) face.ValidatePresets();
        if (GUILayout.Button("Lister les BlendShapes")) face.PrintAvailableBlendShapes();
    }
}
