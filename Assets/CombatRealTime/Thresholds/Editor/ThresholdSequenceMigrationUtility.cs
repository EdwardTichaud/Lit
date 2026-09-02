using UnityEditor;
using UnityEngine;

public static class ThresholdSequenceMigrationUtility
{
    private const string SequenceFolder = "Assets/CombatRealTime/Thresholds/Sequences";

    [MenuItem("Lit/Combat/Migrate Health Threshold Sequences")]
    public static void MigrateAllCharacterData()
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterData");
        int migrated = 0;
        int skipped = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (data == null || !data.enableCombatHealthThresholds || data.combatHealthThresholdStages == null) continue;

                for (int stageIndex = 0; stageIndex < data.combatHealthThresholdStages.Count; stageIndex++)
                {
                    CombatHealthThresholdStage stage = data.combatHealthThresholdStages[stageIndex];
                    if (stage == null || stage.sequence != null)
                    {
                        skipped++;
                        continue;
                    }

                    ThresholdSequence sequence = CreateSequence(data, stage);
                    stage.sequence = sequence;
                    EditorUtility.SetDirty(data);
                    migrated++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[CombatThreshold] Migration ThresholdSequence terminee | migrees=" + migrated + " | deja configurees=" + skipped + ". Renseignez les deux etats Animator de Lucian dans chaque nouvelle sequence.");
    }

    private static ThresholdSequence CreateSequence(CharacterData data, CombatHealthThresholdStage stage)
    {
        if (!AssetDatabase.IsValidFolder("Assets/CombatRealTime/Thresholds"))
        {
            AssetDatabase.CreateFolder("Assets/CombatRealTime", "Thresholds");
        }
        if (!AssetDatabase.IsValidFolder(SequenceFolder))
        {
            AssetDatabase.CreateFolder("Assets/CombatRealTime/Thresholds", "Sequences");
        }

        ThresholdSequence sequence = ScriptableObject.CreateInstance<ThresholdSequence>();
        sequence.name = data.name + "_Threshold_" + stage.healthPercent;
        sequence.successResult = stage.legacySuccessResult;
        sequence.failureRetaliationSkill = stage.legacyFailureRetaliationSkill;
        sequence.failureResult = sequence.failureRetaliationSkill != null
            ? ThresholdSequenceFailureResult.EnemySkill
            : ThresholdSequenceFailureResult.ResumeCombat;

        string path = AssetDatabase.GenerateUniqueAssetPath(SequenceFolder + "/" + sequence.name + ".asset");
        AssetDatabase.CreateAsset(sequence, path);
        return sequence;
    }
}
