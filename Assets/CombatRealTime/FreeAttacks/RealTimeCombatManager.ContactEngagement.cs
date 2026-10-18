using System;
using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    public event Action<ICombatTarget> EncounterAggroStarted;

    public bool TryEngageFromEnemyPerception(Transform source, ICombatTarget victim)
    {
        if (!AllowsFreeAttacks || !CombatProtectionDamage.HasAuthority || source == null) return false;
        var world = GetComponent<CombatWorldRuntime>();
        if (world != null) return world.TryEngageActor(source.GetComponent<SquadCharacterController>(), victim);
        if (source != playerRoot || IsPlayerDead()) return false;
        return BeginContactEncounter(source, victim);
    }

    public bool TryEngageFromPlayerContact(Transform source, ICombatTarget victim, PlayerAttackContext action)
    {
        if (!AllowsFreeAttacks || !CombatProtectionDamage.HasAuthority || source != playerRoot || action == null ||
            !action.ImpactConsumed || !SkillTargetingContract.Alive(victim) || IsPlayerDead()) return false;
        bool current = playerActionPresentation != null && playerActionPresentation.AttackContext == action &&
            playerActionPresentation.IsCurrentSession(action.Generation);
        bool cinematic = combatSkillCinematicController != null && combatSkillCinematicController.Targeting == action;
        if (!current && !cinematic) return false;
        if (action.Mode != SkillTargetingMode.FreeAttack && action.Mode != SkillTargetingMode.CasterArea &&
            action.Target != null && action.TargetRoot != victim.Root) return false;
        if (!BeginContactEncounter(source, victim)) return false;
        GetComponent<CombatWorldRuntime>()?.TryEngageActor(playerController, victim);
        victim.Root.GetComponent<EnemyController>()?.RegisterThreat(playerController, 1f);
        return true;
    }

    private bool BeginContactEncounter(Transform source, ICombatTarget victim)
    {
        if (!AllowsFreeAttacks || source == null || source != playerRoot || !SkillTargetingContract.Alive(victim)) return false;
        var legacy = victim.Root.GetComponent<EnemyController>();
        var brain = victim.Root.GetComponent<LitBrainsEnemy>();
        var encounter = EnemyEncounterActors.Resolve(victim.Root);
        if (encounter != null && !EnemyEncounterActors.IsAvailable(encounter)) return false;
        bool ownedCinematic = combatSkillCinematicController?.Targeting?.TargetRoot == victim.Root;
        if (legacy != null && (!legacy.CombatEnabled || legacy.IsCinematicSuspended && !ownedCinematic || legacy.IsFlameDormant ||
            legacy.BossEncounter != null && !legacy.BossEncounter.IsBossEngaged) || brain != null && brain.IsCinematicSuspended && !ownedCinematic) return false;
        RegisterWorldThreat(victim);
        bool entering = !combatActive;
        if (entering)
        {
            playerRoot = source;
            ResolvePlayerReferences();
            StowPlayerTorchForCombat();
            combatActive = true;
            // Engagement and manual lock are independent, including when another enemy is touched.
            if (externalTarget == null && lockedEnemy == null)
            { externalTarget = victim; externalTargetLocked = false; }
            else if (externalTarget != null && !externalTargetLocked) externalTarget = victim;
            if (legacy != null) SetEngagedEnemy(legacy);
            clarity = 0;
            RefreshCombatSpiritFusion(true);
            playerController?.HideLocalInteractionPresentation();
            if (!IsCinematicSequenceActive) combatInput?.SetInputActive(true, preserveHeldActions: true);
            ClarityChanged?.Invoke(clarity, ClarityRank);
            CombatStateChanged?.Invoke(true);
        }
        if (CombatProtectionDamage.HasAuthority) brain?.NotifyPlayerContact(source);
        if (legacy != null)
        {
            SetEnemyAttackMode(legacy, true);
            if (CombatProtectionDamage.HasAuthority) legacy.BeginBattleWall();
        }
        RefreshCombatMusicOverride();
        if (entering) EncounterAggroStarted?.Invoke(victim);
        return true;
    }
}
