using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ThresholdSequence))]
public sealed class ThresholdSequenceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ThresholdSequence sequence = (ThresholdSequence)target;
        EditorGUILayout.Space();
        if (!sequence.TryGetStepValidationIssue(out string issue))
        {
            EditorGUILayout.HelpBox("Sequence invalide : " + issue, MessageType.Error);
            return;
        }

        EditorGUILayout.HelpBox(
            "Sequence valide : chaque step contient un QTE unique, son succes et sa politique d'echec. " +
            "Les succes intermediaires enchainent automatiquement le step suivant ; seul le Success Result du dernier step resout le palier.",
            MessageType.Info);
    }
}
