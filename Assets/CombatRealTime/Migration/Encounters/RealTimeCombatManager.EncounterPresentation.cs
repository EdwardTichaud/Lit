using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    /// <summary>Read only the controlled actor's confirmed threats, never its speculative manual lock.</summary>
    public bool HasConfirmedEncounterThreat(Transform actor, Transform enemyRoot)
    {
        return actor != null && enemyRoot != null && playerRoot == actor && combatActive && AllowsFreeAttacks &&
            worldThreats.TryGetValue(enemyRoot, out var threat) && threat?.Root == enemyRoot && !threat.IsDead;
    }
}
