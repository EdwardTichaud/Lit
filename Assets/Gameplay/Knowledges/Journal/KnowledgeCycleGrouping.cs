using System;
using System.Collections.Generic;

/// <summary>Read-only journal projection. Never unlocks knowledge or advances a cycle.</summary>
public static class KnowledgeCycleGrouping
{
    public sealed class Group
    {
        public readonly CycleDefinition Cycle;
        public string Title => Cycle == null ? "Autres connaissances" :
            string.IsNullOrWhiteSpace(Cycle.title) ? Cycle.name : Cycle.title;
        private readonly List<KnowledgeSO> entries = new List<KnowledgeSO>();
        public IReadOnlyList<KnowledgeSO> Entries => entries;
        internal Group(CycleDefinition cycle) => Cycle = cycle;
        internal void Add(KnowledgeSO knowledge) { if (!entries.Contains(knowledge)) entries.Add(knowledge); }
        internal void Sort() => entries.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.title, b.title));
    }

    public static List<Group> Build(IEnumerable<KnowledgeSO> unlocked, IEnumerable<CycleDefinition> definitions)
    {
        var known = new HashSet<KnowledgeSO>();
        if (unlocked != null) foreach (var knowledge in unlocked) if (knowledge != null) known.Add(knowledge);
        var cycles = new HashSet<CycleDefinition>();
        if (definitions != null) foreach (var cycle in definitions) if (cycle != null) cycles.Add(cycle);
        foreach (var knowledge in known)
            if (knowledge.cycles != null) foreach (var cycle in knowledge.cycles) if (cycle != null) cycles.Add(cycle);

        var result = new List<Group>();
        var assigned = new HashSet<KnowledgeSO>();
        foreach (var cycle in cycles)
        {
            var related = CollectReferences(cycle);
            var group = new Group(cycle);
            foreach (var knowledge in known)
                if (related.Contains(knowledge) || knowledge.cycles != null && knowledge.cycles.Contains(cycle))
                { group.Add(knowledge); assigned.Add(knowledge); }
            if (group.Entries.Count > 0) { group.Sort(); result.Add(group); }
        }
        result.Sort((a, b) =>
        {
            int title = StringComparer.CurrentCultureIgnoreCase.Compare(a.Title, b.Title);
            return title != 0 ? title : StringComparer.Ordinal.Compare(a.Cycle.cycleId, b.Cycle.cycleId);
        });
        var general = new Group(null);
        foreach (var knowledge in known) if (!assigned.Contains(knowledge)) general.Add(knowledge);
        if (general.Entries.Count > 0) { general.Sort(); result.Add(general); }
        return result;
    }

    private static HashSet<KnowledgeSO> CollectReferences(CycleDefinition cycle)
    {
        var result = new HashSet<KnowledgeSO>();
        AddRequirements(result, cycle.prerequisites);
        foreach (var step in cycle.steps ?? Array.Empty<CycleStep>())
        {
            if (step == null) continue;
            if (step.kind == CycleStepKind.Knowledge && step.knowledge != null) result.Add(step.knowledge);
            AddRequirements(result, step.prerequisites);
            foreach (var reward in step.rewards ?? Array.Empty<CycleReward>())
                if (reward != null && reward.kind == CycleRewardKind.Knowledge && reward.knowledge != null) result.Add(reward.knowledge);
        }
        foreach (var dialogue in cycle.dialogues ?? Array.Empty<CycleDialogue>())
        {
            if (dialogue?.condition == null) continue;
            AddRequirements(result, dialogue.condition.requirements);
            foreach (var knowledge in dialogue.condition.knowledge ?? Array.Empty<KnowledgeSO>())
                if (knowledge != null) result.Add(knowledge);
        }
        return result;
    }

    private static void AddRequirements(HashSet<KnowledgeSO> result, CycleRequirements requirements)
    {
        foreach (var requirement in requirements?.conditions ?? Array.Empty<CycleRequirement>())
            if (requirement != null && requirement.kind == CycleRequirementKind.Knowledge && requirement.knowledge != null)
                result.Add(requirement.knowledge);
    }
}
