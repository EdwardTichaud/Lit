using UnityEditor;
using UnityEngine;

public static class PlayerActionContinuityValidation
{
    [MenuItem("Lit/Combat/Validate Player Action Event Blends")]
    private static void Validate()
    {
        int warnings = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:SkillSO"))
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill == null || skill.AnimationClip == null) continue;
            float blend = skill.Presentation.entryBlendSeconds;
            // CrossFade uses normalized duration; also cover short clips conservatively.
            float ambiguousUntil = blend * Mathf.Max(1f, skill.AnimationClip.length);
            foreach (var evt in AnimationUtility.GetAnimationEvents(skill.AnimationClip))
            {
                if (evt.time >= ambiguousUntil) continue;
                warnings++;
                Debug.LogWarning($"[PlayerAction Authoring] {skill.name}: {evt.functionName} at {evt.time:F3}s " +
                    $"is inside the entry blend ({ambiguousUntil:F3}s). A same-state restart rejects outgoing/ambiguous events; " +
                    "place gameplay events after the blend or use separate states.", skill);
            }
        }
        Debug.Log($"[PlayerAction Authoring] Event blend validation complete: {warnings} warnings. No clips modified.");
    }
}
