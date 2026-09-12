using System;
using UnityEditor;
using UnityEngine;

public static class CycleMigration
{
    [MenuItem("Lit/Narrative/Migrer Nina vers les etapes nommees")]
    public static void MigrateNina()
    {
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/NinaCycle.asset");
        if (definition == null || definition.HasSteps) return;
        Undo.RecordObject(definition, "Migrate Nina steps");
        ConfigureNina(definition);
        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssetIfDirty(definition);
    }
    public static void ConfigureNina(CycleDefinition definition)
    {
        if (definition.HasSteps) return;
        var nina = definition.FindDialogue("nina");
        var scar = definition.FindDialogue("scar");
        if (nina == null || scar == null || nina.condition.knowledge.Length < 2)
            throw new InvalidOperationException("Dialogues et connaissances Nina requis avant migration.");
        definition.title = "Le souvenir de Nina";
        definition.description = "Comprendre le destin de Nina et recueillir le souvenir de Scar.";
        definition.category = CycleCategory.Side;
        definition.completionPresentationTimeout = 15f;
        definition.steps = new[]
        {
            new CycleStep { id = "chimeras_known", title = "Decouvrir l'existence des chimeres", kind = CycleStepKind.Knowledge, knowledge = nina.condition.knowledge[0] },
            new CycleStep { id = "edouard_understood", title = "Lire la lettre d'Edouard", kind = CycleStepKind.Knowledge, knowledge = nina.condition.knowledge[1] },
            new CycleStep { id = "scientist_defeated", title = "Vaincre le scientifique", kind = CycleStepKind.EnemyDefeated, sourceId = "encounter", legacyAnyFlags = 1, legacyWriteFlags = 1 },
            new CycleStep { id = "nina_spoken", title = "Parler a Nina", kind = CycleStepKind.Interaction, sourceId = "nina", legacyAnyFlags = 20, legacyWriteFlags = 20, prerequisites = Requires("chimeras_known", "edouard_understood") },
            new CycleStep { id = "scar_reward", title = "Recevoir Cicatrice de Scar", kind = CycleStepKind.DialogueCompleted, sourceId = "scar", terminal = true,
                rewardSkill = scar.rewardSkill, legacyAnyFlags = 8, legacyWriteFlags = 8, prerequisites = Requires("nina_spoken") },
            new CycleStep { id = "aftermath_seen", title = "Voir le souvenir apres le combat (facultatif)", kind = CycleStepKind.SequenceCompleted, sourceId = "cinematic", legacyAnyFlags = 2, legacyWriteFlags = 2, prerequisites = Requires("scientist_defeated") }
        };
    }
    private static CycleRequirements Requires(params string[] ids) => new CycleRequirements
    {
        conditions = Array.ConvertAll(ids, id => new CycleRequirement { kind = CycleRequirementKind.Step, stepId = id })
    };
    [MenuItem("Assets/Create/Lit/Narrative/Modele de cycle vide")]
    public static void CreateTemplate()
    {
        var definition = ScriptableObject.CreateInstance<CycleDefinition>();
        definition.cycleId = "cycle." + Guid.NewGuid().ToString("N");
        definition.title = "Nouveau cycle";
        definition.enemyDefeatedFlags = 0;
        definition.steps = new[] { new CycleStep { id = "conclusion", title = "Interaction finale", kind = CycleStepKind.Interaction, sourceId = "conclusion", terminal = true } };
        ProjectWindowUtil.CreateAsset(definition, "NewCycle.asset");
    }
}
