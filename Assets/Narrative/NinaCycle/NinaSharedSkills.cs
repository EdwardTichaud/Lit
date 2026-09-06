using System.Collections.Generic;
using UnityEngine;

/// <summary>Read-through view of saved world rewards, never writes CharacterData.</summary>
public static class NinaSharedSkills
{
    private static NinaCycleDefinition[] definitions;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => definitions = null;

    public static void AppendTo(List<SkillSO> result)
    {
        var rules = Object.FindAnyObjectByType<WorldRulesStateManager>();
        if (rules == null) return;
        definitions ??= Resources.LoadAll<NinaCycleDefinition>("Narrative");
        foreach (var definition in definitions)
            if (definition != null && definition.cicatrice != null &&
                rules.TryGetInt(definition.StateKey, out int state) && (state & NinaCycleController.RewardGranted) != 0 &&
                !result.Contains(definition.cicatrice)) result.Add(definition.cicatrice);
    }
}
