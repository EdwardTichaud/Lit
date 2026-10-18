using System.Collections.Generic;
using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    private readonly Dictionary<Transform, ICombatTarget> worldThreats = new Dictionary<Transform, ICombatTarget>();
    private ICombatTarget worldLockedTarget;
    private bool confirmedWorldExitPending;
    public int ActiveThreatCount => worldThreats.Count;

    public void ApplyConfirmedWorldThreats(System.Collections.Generic.IEnumerable<ICombatTarget> threats)
    {
        if (!AllowsFreeAttacks || playerRoot == null || threats == null) return;
        var snapshot = new Dictionary<Transform, ICombatTarget>();
        foreach (var threat in threats)
            if (SkillTargetingContract.Alive(threat)) snapshot[threat.Root] = threat;
        if (snapshot.Count == 0) { ApplyConfirmedWorldEngagement(null, false); return; }
        confirmedWorldExitPending = false;
        foreach (var threat in snapshot.Values) BeginContactEncounter(playerRoot, threat);
        worldThreats.Clear();
        foreach (var pair in snapshot) worldThreats.Add(pair.Key, pair.Value);
        if (!SkillTargetingContract.Alive(externalTarget) || !worldThreats.ContainsKey(externalTarget.Root))
            foreach (var threat in snapshot.Values) { externalTarget = threat; break; }
    }

    /// <summary>Applies presentation only to this facade's controlled actor; never rebinds a remote actor.</summary>
    public void ApplyConfirmedWorldEngagement(ICombatTarget threat, bool engaged)
    {
        if (!AllowsFreeAttacks || playerRoot == null) return;
        if (engaged)
        {
            if (SkillTargetingContract.Alive(threat))
            {
                confirmedWorldExitPending = false;
                BeginContactEncounter(playerRoot, threat);
            }
            return;
        }
        worldThreats.Clear();
        confirmedWorldExitPending = combatActive;
        TryFinishConfirmedWorldExit();
    }

    public void RegisterControlledActor(Transform actor)
    {
        if (playerRoot == actor) { ResolvePlayerReferences(); return; }
        if (combatActive || playerRoot != null) EndCombat();
        confirmedWorldExitPending = false;
        playerRoot = actor;
        playerLoadout = null; playerHealth = null; playerController = null;
        playerAnimator = null; playerActionPresentation = null; playerLocomotionBridge = null;
        if (actor != null) ResolvePlayerReferences();
    }

    private void RegisterWorldThreat(ICombatTarget target)
    {
        if (target?.Root != null)
        {
            confirmedWorldExitPending = false;
            worldThreats[target.Root] = target;
        }
    }

    private void TryFinishConfirmedWorldExit()
    {
        if (!confirmedWorldExitPending) return;
        if (!combatActive || worldThreats.Count != 0)
        {
            confirmedWorldExitPending = false;
            return;
        }
        if (IsCinematicSequenceActive || FinishingFreeDashAfterDefeat) return;
        confirmedWorldExitPending = false;
        EndCombatInternal(preserveCurrentAction: !IsPlayerDead());
    }

    public void RefreshWorldEngagements()
    {
        if (!AllowsFreeAttacks) return;
        // Client perception is disabled, but a confirmed empty snapshot still needs
        // its deferred presentation cleanup after a cinematic or a finishing dash.
        TryFinishConfirmedWorldExit();
        RefreshWorldLockLiveness();
        // Remote brains do not run perception; their owner consumes the server's threat snapshot.
        if (!CombatProtectionDamage.HasAuthority) return;
        var removed = new List<Transform>();
        foreach (var pair in worldThreats)
        {
            var target = pair.Value;
            var brain = pair.Key != null ? pair.Key.GetComponent<LitBrainsEnemy>() : null;
            var legacy = pair.Key != null ? pair.Key.GetComponent<EnemyController>() : null;
            if (!SkillTargetingContract.Alive(target) || brain != null && !brain.IsCinematicSuspended && !brain.IsThreatFor(playerRoot) ||
                legacy != null && (legacy.ShouldEndCombatForPursuit || !legacy.HasActiveBattleWall &&
                    playerRoot != null && legacy.IsPositionOutsidePursuitZone(playerRoot.position))) removed.Add(pair.Key);
        }
        foreach (var root in removed)
        {
            var enemy = root != null ? root.GetComponent<EnemyController>() : null;
            if (enemy != null) { enemy.EndBattleWall(); SetEnemyAttackMode(enemy, false); }
            worldThreats.Remove(root);
        }
        if (worldThreats.Count == 0)
        {
            if (combatActive && !IsCinematicSequenceActive && !FinishingFreeDashAfterDefeat)
            {
                if (IsPlayerDead()) PlayPlayerDeathAnimation();
                if (IsPlayerDead() || externalTarget != null && externalTarget.IsDead) CombatResolved?.Invoke(!IsPlayerDead());
                EndCombatInternal(preserveCurrentAction: !IsPlayerDead());
            }
            return;
        }
        if (!SkillTargetingContract.Alive(externalTarget) || !worldThreats.ContainsKey(externalTarget.Root))
        {
            foreach (var target in worldThreats.Values) { externalTarget = target; break; }
        }
    }

    private void RefreshWorldLockLiveness()
    {
        if (worldLockedTarget == null || SkillTargetingContract.Alive(worldLockedTarget)) return;
        worldLockedTarget = null;
        RefreshExternalLockIndicator();
        RefreshLockedEnemyStrafeBinding();
    }

    private List<ICombatTarget> FindWorldLockTargets()
    {
        var targets = new List<ICombatTarget>();
        if (playerRoot == null) return targets;
        foreach (var brain in FindObjectsByType<LitBrainsEnemy>())
            if (!brain.IsCinematicSuspended && SkillTargetingContract.Alive(brain) &&
                Vector3.Distance(playerRoot.position, brain.Root.position) <= lockRange &&
                FreeAttackLineClear(playerRoot.position, brain)) targets.Add(brain);
        foreach (var enemy in FindLockableEnemies(requireVision: true)) targets.Add(new LegacyCombatTarget(enemy));
        targets.Sort((a, b) =>
        {
            int order = (a.Root.position - playerRoot.position).sqrMagnitude.CompareTo((b.Root.position - playerRoot.position).sqrMagnitude);
            return order != 0 ? order : string.CompareOrdinal(a.Root.GetEntityId().ToString(), b.Root.GetEntityId().ToString());
        });
        return targets;
    }

    private bool ToggleWorldLock(bool cycle)
    {
        if (!AllowsFreeAttacks || playerRoot == null) return false;
        if (!cycle && HasLockedCombatTarget)
        {
            worldLockedTarget = null;
            externalTargetLocked = false;
            SetLockedEnemy(null);
            RefreshExternalLockIndicator(); RefreshLockedEnemyStrafeBinding();
            return true;
        }
        var candidates = FindWorldLockTargets();
        if (candidates.Count == 0) return false;
        int index = -1;
        if (cycle && HasLockedCombatTarget)
            index = candidates.FindIndex(candidate => candidate.Root == LockedCombatTarget.Root);
        worldLockedTarget = candidates[(index + 1) % candidates.Count];
        externalTargetLocked = false;
        RefreshExternalLockIndicator(); RefreshLockedEnemyStrafeBinding();
        return true;
    }
}
