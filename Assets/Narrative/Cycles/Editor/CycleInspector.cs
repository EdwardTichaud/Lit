using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CycleController))]
public sealed class CycleInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var cycle = (CycleController)target;
        serializedObject.Update();
        CycleInspectorLayout.Group(serializedObject, "Identite et liaisons", "Fiche du cycle et acteurs propres a cette scene.", "definition", "interactions", "encounters", "sequences");
        CycleInspectorLayout.Group(serializedObject, "Presentation", "Objets et poses visibles selon les conditions ; seuls les participants listes sont suspendus.", "activations", "poses", "cinematicParticipants");
        CycleInspectorLayout.Group(serializedObject, "Rencontre historique", "Liaisons existantes conservees pour Nina ; source encounter pour l'ennemi et cinematic pour la sequence.", "encounterMarker", "encounterEnemy", "director", "bindingProfile");
        serializedObject.ApplyModifiedProperties();
        if (cycle.definition == null)
        {
            EditorGUILayout.HelpBox("Assigner une definition de cycle (Create > Lit > Narrative > Cycle).", MessageType.Warning);
            return;
        }
        foreach (string issue in cycle.definition.ValidateConfiguration())
            EditorGUILayout.HelpBox(issue, MessageType.Warning);
        if (!string.IsNullOrWhiteSpace(cycle.definition.cycleSceneName) &&
            cycle.gameObject.scene.name != cycle.definition.cycleSceneName)
            EditorGUILayout.HelpBox("Le nom de scene du cycle doit correspondre a sa scene additive dediee pour permettre son dechargement.", MessageType.Warning);
        bool requiresEncounter = cycle.definition.playCinematicAfterDefeat ||
            cycle.definition.knowledgeOnEnemyDefeat != null && cycle.definition.knowledgeOnEnemyDefeat.Length > 0;
        if (requiresEncounter && cycle.definition.enemyDefeatedFlags != 0 && cycle.encounterMarker == null && cycle.encounterEnemy == null)
            EditorGUILayout.HelpBox("La mort de l'ennemi ne peut pas etre suivie : assigner le marker ou l'EnemyController de la rencontre.", MessageType.Warning);
        if (cycle.definition.playCinematicAfterDefeat && (cycle.director == null || cycle.director.playableAsset == null || cycle.bindingProfile == null))
            EditorGUILayout.HelpBox("Cinematique activee : Timeline et profil de bindings requis.", MessageType.Warning);
        if (cycle.definition.HasSteps)
        {
            var encounterIds = new System.Collections.Generic.HashSet<string>();
            foreach (var encounter in cycle.encounters)
                if (encounter == null || string.IsNullOrWhiteSpace(encounter.id) || !encounterIds.Add(encounter.id))
                    EditorGUILayout.HelpBox("Rencontre vide ou identifiant duplique.", MessageType.Error);
            var sequenceIds = new System.Collections.Generic.HashSet<string>();
            foreach (var sequence in cycle.sequences)
                if (sequence == null || string.IsNullOrWhiteSpace(sequence.id) || !sequenceIds.Add(sequence.id))
                    EditorGUILayout.HelpBox("Sequence vide ou identifiant duplique.", MessageType.Error);
            foreach (var step in cycle.definition.steps)
            {
                if (step == null) continue;
                if ((step.kind == CycleStepKind.Interaction || step.kind == CycleStepKind.DialogueCompleted) &&
                    !cycle.interactions.Any(item => item != null && item.cycle == cycle && item.dialogueId == step.sourceId))
                    EditorGUILayout.HelpBox("Interaction non liee : " + step.title, MessageType.Error);
                if (step.kind == CycleStepKind.EnemyDefeated && step.sourceId == "encounter" && cycle.encounterEnemy == null && cycle.encounterMarker == null)
                    EditorGUILayout.HelpBox("Rencontre principale non liee : " + step.title, MessageType.Error);
                if (step.kind == CycleStepKind.EnemyDefeated && step.sourceId != "encounter" && !encounterIds.Contains(step.sourceId))
                    EditorGUILayout.HelpBox("Ennemi non lie : " + step.title, MessageType.Error);
                if (step.kind == CycleStepKind.SequenceCompleted && step.sourceId != "cinematic" && !sequenceIds.Contains(step.sourceId))
                    EditorGUILayout.HelpBox("Sequence non liee : " + step.title, MessageType.Error);
            }
        }
        if (Application.isPlaying && CycleProgressionService.Instance != null)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Etat partage", cycle.Status.ToString());
                foreach (var step in cycle.definition.steps)
                    if (step != null) EditorGUILayout.TextField(step.title, CycleProgressionService.Instance.ExplainBlocked(cycle.definition, step));
            }
        }
        if (cycle.interactions != null) foreach (var binding in cycle.interactions)
            if (binding == null || binding.cycle != cycle || cycle.definition.FindDialogue(binding.dialogueId) == null && !cycle.definition.steps.Any(step => step != null && step.kind == CycleStepKind.Interaction && step.sourceId == binding.dialogueId))
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
        var definition = (CycleDefinition)target;
        serializedObject.Update();
        CycleInspectorLayout.Group(serializedObject, "Identite et journal", "Identite persistante et textes prepares pour le futur journal ; principale ou annexe ne change pas l'autorite.", "cycleId", "title", "description", "category");
        CycleInspectorLayout.Group(serializedObject, "Disponibilite et objectifs", "Prerequis du cycle, etapes paralleles ou ordonnees et objectifs terminaux requis pour sa fin.", "prerequisites", "steps");
        CycleInspectorLayout.Group(serializedObject, "Dialogues", "Textes, durees en secondes reelles et presentation des interlocuteurs.", "dialogueSeconds", "dialogues");
        CycleInspectorLayout.Group(serializedObject, "Fin et dechargement", "Scene additive dediee et delai maximal de presentation avant nettoyage partage.", "cycleSceneName", "completionPresentationTimeout");
        CycleInspectorLayout.Group(serializedObject, "Compatibilite des sauvegardes", "Jalons numeriques historiques : ne pas modifier ceux des cycles publies.", "completionFlags", "enemyDefeatedFlags", "knowledgeOnEnemyDefeat", "playCinematicAfterDefeat", "deathDelay", "cinematicCompletedFlags");
        serializedObject.ApplyModifiedProperties();
        foreach (string issue in definition.ValidateConfiguration()) EditorGUILayout.HelpBox(issue, MessageType.Warning);
        if (!AssetDatabase.GetAssetPath(definition).Contains("/Resources/Narrative/"))
            EditorGUILayout.HelpBox("Placer cet asset sous Resources/Narrative pour les recompenses persistantes.", MessageType.Warning);
        if (Resources.LoadAll<CycleDefinition>("Narrative").Any(other => other != definition && other.cycleId == definition.cycleId))
            EditorGUILayout.HelpBox("Un autre cycle utilise deja cet ID de sauvegarde.", MessageType.Error);
    }
}

internal static class CycleInspectorLayout
{
    public static void Group(SerializedObject serialized, string title, string tooltip, params string[] fields)
    {
        string key = "Lit.CycleInspector." + serialized.targetObject.GetType().Name + title;
        bool expanded = SessionState.GetBool(key, true);
        expanded = EditorGUILayout.Foldout(expanded, new GUIContent(title, tooltip), true);
        SessionState.SetBool(key, expanded);
        if (!expanded) return;
        EditorGUI.indentLevel++;
        foreach (string field in fields)
        {
            var property = serialized.FindProperty(field);
            if (property != null) EditorGUILayout.PropertyField(property, true);
        }
        EditorGUI.indentLevel--;
    }
}

[CustomPropertyDrawer(typeof(CycleRequirement))]
public sealed class CycleRequirementDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => 2 * (EditorGUIUtility.singleLineHeight + 2);
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var kind = property.FindPropertyRelative("kind");
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, kind, new GUIContent("Condition", "Etape locale, connaissance partagee ou autre cycle termine."));
        position.y += position.height + 2;
        if ((CycleRequirementKind)kind.enumValueIndex == CycleRequirementKind.Step)
        {
            var id = property.FindPropertyRelative("stepId");
            var owner = property.serializedObject.targetObject as CycleDefinition;
            if (owner == null && property.serializedObject.targetObject is CycleController controller) owner = controller.definition;
            var steps = owner != null ? owner.steps.Where(step => step != null).ToArray() : System.Array.Empty<CycleStep>();
            var choices = new[] { string.IsNullOrWhiteSpace(id.stringValue) ? "Choisir une etape" : "Reference : " + id.stringValue }.Concat(steps.Select(step => string.IsNullOrWhiteSpace(step.title) ? step.id : step.title + " (" + step.id + ")")).ToArray();
            int current = System.Array.FindIndex(steps, step => step.id == id.stringValue) + 1;
            int next = EditorGUI.Popup(position, "Etape", current, choices);
            if (next > 0) id.stringValue = steps[next - 1].id;
        }
        else EditorGUI.PropertyField(position, property.FindPropertyRelative((CycleRequirementKind)kind.enumValueIndex == CycleRequirementKind.Knowledge ? "knowledge" : "cycle"));
        EditorGUI.EndProperty();
    }
}
