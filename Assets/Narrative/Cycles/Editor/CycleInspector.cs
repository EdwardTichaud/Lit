using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CycleController))]
public sealed class CycleInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var cycle = (CycleController)target;
        if (cycle.definition == null)
        {
            EditorGUILayout.HelpBox("Assigner une definition de cycle (Create > Lit > Narrative > Cycle).", MessageType.Warning);
            return;
        }
        foreach (string issue in cycle.definition.ValidateConfiguration())
            EditorGUILayout.HelpBox(issue, MessageType.Warning);
        if (cycle.definition.enemyDefeatedFlags != 0 && cycle.encounterMarker == null && cycle.encounterEnemy == null)
            EditorGUILayout.HelpBox("La mort de l'ennemi ne peut pas etre suivie : assigner le marker ou l'EnemyController de la rencontre.", MessageType.Warning);
        if (cycle.definition.playCinematicAfterDefeat && (cycle.director == null || cycle.director.playableAsset == null || cycle.bindingProfile == null))
            EditorGUILayout.HelpBox("Cinematique activee : Timeline et profil de bindings requis.", MessageType.Warning);
        if (cycle.interactions != null) foreach (var binding in cycle.interactions)
            if (binding == null || binding.cycle != cycle || cycle.definition.FindDialogue(binding.dialogueId) == null)
                EditorGUILayout.HelpBox("Interaction absente, mauvais cycle ou identifiant de dialogue inconnu.", MessageType.Warning);
        if (GUILayout.Button("Relier les interactions enfants"))
        {
            Undo.RecordObject(cycle, "Bind cycle interactions");
            cycle.interactions = cycle.GetComponentsInChildren<CycleInteraction>(true);
            foreach (var binding in cycle.interactions)
            {
                Undo.RecordObject(binding, "Bind cycle interaction");
                binding.cycle = cycle;
                EditorUtility.SetDirty(binding);
            }
            EditorUtility.SetDirty(cycle);
        }
        EditorGUILayout.HelpBox("Placer la definition dans Resources/Narrative pour retrouver les skills apres chargement. Conserver l'ID du cycle et ses bits une fois les sauvegardes publiees.", MessageType.Info);
    }
}

[CustomEditor(typeof(CycleDefinition))]
public sealed class CycleDefinitionInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var definition = (CycleDefinition)target;
        foreach (string issue in definition.ValidateConfiguration()) EditorGUILayout.HelpBox(issue, MessageType.Warning);
        if (!AssetDatabase.GetAssetPath(definition).Contains("/Resources/Narrative/"))
            EditorGUILayout.HelpBox("Placer cet asset sous Resources/Narrative pour les recompenses persistantes.", MessageType.Warning);
        if (Resources.LoadAll<CycleDefinition>("Narrative").Any(other => other != definition && other.cycleId == definition.cycleId))
            EditorGUILayout.HelpBox("Un autre cycle utilise deja cet ID de sauvegarde.", MessageType.Error);
    }
}
