using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    internal bool IsThresholdTargetEngaged(ICombatTarget target)
    {
        if (target?.Root == null || !combatActive) return false;
        if (AllowsFreeAttacks && GetComponent<CombatWorldRuntime>() != null)
            return worldThreats.ContainsKey(target.Root);
        return CombatTarget?.Root == target.Root;
    }

    internal void ApplyThresholdFailureKnockback(ICombatTarget source, float distance)
    {
        ResolvePlayerReferences();
        if (source?.Root == null || playerRoot == null || playerLocomotionBridge == null || distance <= 0f) return;
        StartCoroutine(ApplyThresholdFailureKnockbackRoutine(source.Root, distance));
    }

    internal void FinishThresholdKillPresentation(ICombatTarget target)
    {
        if (target?.Root == null) return;
        var legacy = target.Root.GetComponent<EnemyController>();
        if (AllowsFreeAttacks && GetComponent<CombatWorldRuntime>() != null)
        {
            worldThreats.Remove(target.Root);
            // Clear only this target's lock. Remaining threats retain their encounter.
            if (externalTarget?.Root == target.Root)
            {
                externalTargetLocked = false;
                externalTarget = null;
                RefreshExternalLockIndicator();
                RefreshLockedEnemyStrafeBinding();
            }
            if (worldThreats.Count == 0) EndCombat();
            else RefreshWorldEngagements();
            return;
        }
        if (legacy != null) { FinishThresholdKillPresentation(legacy); return; }
        if (CombatTarget?.Root == target.Root) EndCombat();
    }
}
