using System;
using System.Collections.Generic;

public static class CycleValidation
{
    public static void Validate(CycleDefinition cycle, List<string> issues)
    {
        if (!cycle.HasSteps) return;
        var ids = new HashSet<string>();
        bool terminal = false;
        foreach (var step in cycle.steps)
        {
            if (step == null) { issues.Add("Etape vide."); continue; }
            if (string.IsNullOrWhiteSpace(step.id) || !ids.Add(step.id)) issues.Add("Identifiant d'etape vide ou duplique : " + step.id);
            if (step.kind == CycleStepKind.Knowledge && step.knowledge == null) issues.Add("Connaissance absente : " + step.id);
            if (step.kind != CycleStepKind.Knowledge && string.IsNullOrWhiteSpace(step.sourceId)) issues.Add("Source absente : " + step.id);
            if (step.kind == CycleStepKind.DialogueCompleted && cycle.FindDialogue(step.sourceId) == null) issues.Add("Dialogue absent : " + step.sourceId);
            foreach (var reward in step.rewards ?? Array.Empty<CycleReward>())
            {
                if (reward == null) { issues.Add("Recompense vide : " + step.id); continue; }
                if (reward.kind == CycleRewardKind.Skill && reward.skill == null) issues.Add("Competence absente : " + step.id);
                if (reward.kind == CycleRewardKind.Knowledge && reward.knowledge == null) issues.Add("Connaissance de recompense absente : " + step.id);
                if (reward.kind == CycleRewardKind.Item && reward.item == null) issues.Add("Objet de recompense absent : " + step.id);
                if ((reward.kind == CycleRewardKind.WorldVariable || reward.kind == CycleRewardKind.Activation) && string.IsNullOrWhiteSpace(reward.key)) issues.Add("Cle de recompense absente : " + step.id);
            }
            terminal |= step.terminal;
            ValidateRequirements(cycle, step.prerequisites, issues);
        }
        if (!terminal) issues.Add("Fin inatteignable : aucune etape terminale.");
        ValidateRequirements(cycle, cycle.prerequisites, issues);
        if (cycle.legacyMigrationVersion > 0)
            foreach (var migration in cycle.legacyMigrations ?? Array.Empty<CycleLegacyStepMigration>())
                if (migration == null || cycle.FindStep(migration.stepId) == null || (migration.anyFlags == 0 && migration.allFlags == 0))
                    issues.Add("Migration invalide.");
        var visiting = new HashSet<string>();
        var visited = new HashSet<string>();
        foreach (var step in cycle.steps)
            if (step != null && HasLoop(cycle, step.id, visiting, visited))
            { issues.Add("Dependance circulaire : " + cycle.cycleId + "/" + step.id); break; }
    }
    private static void ValidateRequirements(CycleDefinition owner, CycleRequirements requirements, List<string> issues)
    {
        if (requirements?.conditions == null) return;
        foreach (var requirement in requirements.conditions)
        {
            if (requirement == null) { issues.Add("Prerequis vide."); continue; }
            if (requirement.kind == CycleRequirementKind.Step && owner.FindStep(requirement.stepId) == null)
                issues.Add("Etape requise introuvable : " + requirement.stepId);
            if (requirement.kind == CycleRequirementKind.Knowledge && requirement.knowledge == null) issues.Add("Connaissance requise absente.");
            if (requirement.kind == CycleRequirementKind.CycleCompleted && requirement.cycle == null) issues.Add("Cycle requis absent.");
        }
    }
    private static bool HasLoop(CycleDefinition cycle, string id, HashSet<string> visiting, HashSet<string> visited)
    {
        string key = cycle.cycleId + "/" + id;
        if (visited.Contains(key)) return false;
        if (!visiting.Add(key)) return true;
        var step = cycle.FindStep(id);
        if (step != null)
        {
            foreach (var requirements in new[] { cycle.prerequisites, step.prerequisites })
                foreach (var requirement in requirements?.conditions ?? Array.Empty<CycleRequirement>())
                {
                    if (requirement == null) continue;
                    if (requirement.kind == CycleRequirementKind.Step && HasLoop(cycle, requirement.stepId, visiting, visited)) return true;
                    if (requirement.kind == CycleRequirementKind.CycleCompleted && requirement.cycle != null)
                        foreach (var terminal in requirement.cycle.steps ?? Array.Empty<CycleStep>())
                            if (terminal != null && terminal.terminal && HasLoop(requirement.cycle, terminal.id, visiting, visited)) return true;
                }
        }
        visiting.Remove(key); visited.Add(key);
        return false;
    }
}
