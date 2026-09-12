using System.Collections.Generic;
using UnityEngine;

/// <summary>Read-through saved rewards, without modifying character assets or replaying notifications.</summary>
public static class CycleSharedSkills
{
    private static CycleDefinition[] definitions;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => definitions = null;
    public static void AppendTo(List<SkillSO> result)
    {
        var rules = Object.FindAnyObjectByType<WorldRulesStateManager>();
        if (rules == null) return;
        definitions ??= Resources.LoadAll<CycleDefinition>("Narrative");
        foreach (var definition in definitions)
        {
            if (definition != null && definition.steps != null)
                foreach (var step in definition.steps)
                    if (step != null && step.rewardSkill != null && rules.TryGetInt(definition.StepKey(step.id), out int completed) &&
                        completed != 0 && !result.Contains(step.rewardSkill)) result.Add(step.rewardSkill);
            if (definition != null && definition.dialogues != null && rules.TryGetInt(definition.StateKey, out int state))
                foreach (var dialogue in definition.dialogues)
                    if (dialogue != null && dialogue.rewardSkill != null && dialogue.HasReward(state) && !result.Contains(dialogue.rewardSkill))
                        result.Add(dialogue.rewardSkill);
        }
    }
}
