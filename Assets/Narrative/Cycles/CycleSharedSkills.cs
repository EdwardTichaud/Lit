using System.Collections.Generic;
using UnityEngine;

/// <summary>Read-through saved rewards, without modifying character assets or replaying notifications.</summary>
public static class CycleSharedSkills
{
    private static CycleDefinition[] definitions;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => definitions = null;
    public static bool IsUnlocked(SkillSO skill)
    {
        var service = CycleProgressionService.Instance;
        var rules = service != null ? service.GetComponent<WorldRulesStateManager>() : Object.FindAnyObjectByType<WorldRulesStateManager>();
        definitions ??= CycleCatalog.LoadDefinitions();
        return IsUnlocked(skill, rules, definitions);
    }

    // Explicit state also lets save/authority tests verify rewards without a scene singleton.
    public static bool IsUnlocked(SkillSO skill, WorldRulesStateManager rules, IEnumerable<CycleDefinition> cycles)
    {
        if (skill == null || rules == null || cycles == null) return false;
        foreach (var definition in cycles)
        {
            if (definition == null) continue;
            foreach (var step in definition.steps ?? System.Array.Empty<CycleStep>())
            {
                if (step == null) continue;
                bool grants = step.rewardSkill == skill;
                foreach (var reward in step.rewards ?? System.Array.Empty<CycleReward>())
                    grants |= reward != null && reward.kind == CycleRewardKind.Skill && reward.skill == skill;
                if (grants && rules.TryGetInt(definition.StepKey(step.id), out int completed) && completed != 0) return true;
            }
            if (rules.TryGetInt(definition.StateKey, out int state))
                foreach (var dialogue in definition.dialogues ?? System.Array.Empty<CycleDialogue>())
                    if (dialogue != null && dialogue.rewardSkill == skill && dialogue.HasReward(state)) return true;
        }
        return false;
    }
    public static void AppendTo(List<SkillSO> result)
    {
        var service = CycleProgressionService.Instance;
        var rules = service != null ? service.GetComponent<WorldRulesStateManager>() : Object.FindAnyObjectByType<WorldRulesStateManager>();
        if (rules == null) return;
        definitions ??= CycleCatalog.LoadDefinitions();
        foreach (var definition in definitions)
        {
            if (definition != null && definition.steps != null)
                foreach (var step in definition.steps)
                    if (step != null && rules.TryGetInt(definition.StepKey(step.id), out int completed) && completed != 0)
                    {
                        if (step.rewardSkill != null && !result.Contains(step.rewardSkill)) result.Add(step.rewardSkill);
                        foreach (var reward in step.rewards ?? System.Array.Empty<CycleReward>())
                            if (reward != null && reward.kind == CycleRewardKind.Skill && reward.skill != null && !result.Contains(reward.skill)) result.Add(reward.skill);
                    }
            if (definition != null && definition.dialogues != null && rules.TryGetInt(definition.StateKey, out int state))
                foreach (var dialogue in definition.dialogues)
                    if (dialogue != null && dialogue.rewardSkill != null && dialogue.HasReward(state) && !result.Contains(dialogue.rewardSkill))
                        result.Add(dialogue.rewardSkill);
        }
    }
}
