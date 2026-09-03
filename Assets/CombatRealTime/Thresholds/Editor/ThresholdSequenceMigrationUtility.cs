using UnityEditor;
using UnityEngine;

/// <summary>Strict audit retained after the legacy ThresholdSequence schema was removed.</summary>
public static class ThresholdSequenceMigrationUtility
{
    [MenuItem("Lit/Combat/Validate Health Threshold Sequences")]
    public static void MigrateAllCharacterData()
    {
        string[] guids = AssetDatabase.FindAssets("t:ThresholdSequence");
        int valid = 0;
        int invalid = 0;
        for (int index = 0; index < guids.Length; index++)
        {
            ThresholdSequence sequence = AssetDatabase.LoadAssetAtPath<ThresholdSequence>(AssetDatabase.GUIDToAssetPath(guids[index]));
            string issue = null;
            if (sequence != null && sequence.TryGetStepValidationIssue(out issue))
            {
                valid++;
            }
            else
            {
                invalid++;
                Debug.LogError("[CombatThreshold] Sequence a reauthorer : '" +
                               (sequence != null ? sequence.name : "None") + "' | " + issue, sequence);
            }
        }
        Debug.Log("[CombatThreshold] Audit strict termine | valides=" + valid + " | invalides=" + invalid + ".");
    }
}
