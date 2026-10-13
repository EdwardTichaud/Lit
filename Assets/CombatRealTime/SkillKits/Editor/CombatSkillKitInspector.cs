using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CombatSkillKitInput))]
public sealed class CombatSkillKitInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var input = (CombatSkillKitInput)target;
        EditorGUILayout.LabelField("Kit actif", input.ActiveKit.ToString());
        EditorGUILayout.LabelField("Roue ouverte", input.IsOpen ? "Oui" : "Non");
        EditorGUILayout.LabelField("Catalogue solo disponible", input.IsAvailable ? "Oui" : "Non");
        EditorGUILayout.LabelField("Mode d'input", InputModeCoordinator.CurrentMode.ToString());
        if (!input.IsSolo)
            EditorGUILayout.HelpBox("Le multijoueur conserve ses loadouts et validations existants. La repartition des kits reseau est distincte.", MessageType.Info);
    }
}
