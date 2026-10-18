using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays the optional in-place Timeline attached to a regular SkillSO. It owns
/// the combat-wide lock for the session but delegates playback, pooling and
/// camera binding to CombatCinematicPlaybackService.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatSkillCinematicController : MonoBehaviour
{
    [SerializeField] private RealTimeCombatManager combatManager;
    [SerializeField] private RealTimeCombatInput combatInput;
    [SerializeField] private CombatCinematicPlaybackService cinematicPlayback;
    [SerializeField, Min(0.25f)] private float completionGraceSeconds = 1.5f;

    private readonly List<EnemyController> suspendedEnemies = new List<EnemyController>();
    private SkillSO activeSkill;
    private EnemyController activeEnemyCaster;
    private CombatCinematicCasterRole activeCasterRole;
    private bool active;
    private bool playbackStarted;
    private bool impactResolved;
    private bool playerLockHeld;
    private LitOpsiveLocomotionBridge.ExternalLockHandle playerLock;
    private int sessionToken;
    private Coroutine watchdog;
    private PlayerAttackContext targeting;
    private readonly List<ICombatTarget> ownedTargetSuspensions = new List<ICombatTarget>();
    public PlayerAttackContext Targeting => active ? targeting : null;

    public bool IsPlaying => active;
    public SkillSO ActiveSkill => activeSkill;

    private void Awake() => ResolveReferences();

    private void OnDisable()
    {
        AbortForActionTermination();
    }

    public void AbortForActionTermination()
    {
        if (active && cinematicPlayback != null && cinematicPlayback.IsPlaying)
        {
            cinematicPlayback.StopActive();
        }
        EndSession(CombatCinematicEndReason.Interrupted);
    }

    public bool TryPlayPlayerSkill(SkillSO skill)
    {
        return TryPlay(skill, CombatCinematicCasterRole.Player, null);
    }

    public bool TryPlayPlayerSkill(SkillSO skill, PlayerAttackContext resolved, out string error)
    {
        error = null;
        var network = Unity.Netcode.NetworkManager.Singleton;
        if (network != null && network.IsListening)
        {
            ResolveReferences();
            var actor = combatManager?.PlayerRoot?.GetComponent<Lit.Combat.Migration.Network.CombatNetworkActorRuntime>();
            var identity = resolved?.Target?.Root != null ? resolved.Target.Root.GetComponentInParent<Unity.Netcode.NetworkObject>() : null;
            if (actor != null && resolved != null && resolved.Skill == skill && actor.TryRequestSkill(skill, identity, resolved.Direction)) return true;
            error = "Commande cinematique ciblee refusee ou acteur reseau absent.";
            return false;
        }
        if (!CombatLocalCinematicPolicy.AllowsLocalExecution)
        { error = CombatLocalCinematicPolicy.NetworkExecutionUnavailable; return false; }
        ResolveReferences();
        if (combatManager == null || !combatManager.AllowsFreeAttacks || resolved == null || resolved.Skill != skill ||
            !resolved.TargetAlive || !combatManager.ValidateFreeAttackContext(resolved) || !skill.HasCombatCinematic)
        { error = "Contexte cible/skill cinematique invalide."; return false; }
        var rig = skill.Cinematic.CombatCinematicRigPrefab;
        var candidate = new CombatCinematicContext(combatManager, skill, explicitTarget: resolved.Target);
        if (!rig.ValidateSkillPlayback(candidate, skill.Cinematic.PlayerAnimatorTrackName, skill.Cinematic.EnemyAnimatorTrackName, out error)) return false;
        if (resolved.Target.IsAttackCommitted) { error = "La victime execute une attaque protegee."; return false; }
        if (!TryPlay(skill, CombatCinematicCasterRole.Player, null, resolved))
        { error = "Lecture cinematique indisponible ou refusee."; return false; }
        return true;
    }

    public bool TryPlayEnemySkill(EnemyController caster, SkillSO skill)
    {
        return TryPlay(skill, CombatCinematicCasterRole.Enemy, caster);
    }

    public void ResolveCinematicSkillImpact()
    {
        if (!CombatLocalCinematicPolicy.AllowsLocalExecution) { AbortForActionTermination(); return; }
        if (!active || impactResolved || activeSkill == null || combatManager == null)
        {
            return;
        }

        impactResolved = true;
        if (activeCasterRole == CombatCinematicCasterRole.Player)
        {
            int applied = targeting != null ? combatManager.ApplyTargetedCinematicSkillImpact(activeSkill, targeting) : combatManager.ApplySkillDamageToLockedEnemy(activeSkill);
            if (applied > 0)
            {
                CombatImpactFeedbackController.EnsureInstance()?.PlayImpact(activeSkill, targeting != null ? targeting.TargetPoint : combatManager.LockedTargetPoint, combatManager.PlayerRoot);
            }
        }
        else if (activeEnemyCaster != null)
        {
            combatManager.ApplyEnemySkillDamageToPlayer(activeEnemyCaster, activeSkill);
        }
    }

    private bool TryPlay(SkillSO skill, CombatCinematicCasterRole casterRole, EnemyController caster, PlayerAttackContext resolved = null)
    {
        if (!CombatLocalCinematicPolicy.AllowsLocalExecution) return false;
        ResolveReferences();
        if (active || cinematicPlayback == null || cinematicPlayback.IsPlaying || combatManager == null ||
            (!combatManager.IsCombatActive && resolved == null) || skill == null || !skill.HasCombatCinematic ||
            (casterRole == CombatCinematicCasterRole.Player && resolved == null &&
             (!combatManager.HasLockedCombatTarget || combatManager.LockedCombatTarget.IsDead)) ||
            (casterRole == CombatCinematicCasterRole.Enemy &&
             (combatManager.EngagedEnemy == null || (combatManager.EngagedEnemy.Health != null && combatManager.EngagedEnemy.Health.IsDead))))
        {
            return false;
        }

        if (casterRole == CombatCinematicCasterRole.Enemy &&
            (caster == null || caster != combatManager.EngagedEnemy || caster.ActiveSkill != skill))
        {
            return false;
        }

        CombatSkillCinematicDefinition definition = skill.Cinematic;
        if (casterRole == CombatCinematicCasterRole.Player && (resolved != null ? resolved.Target.IsAttackCommitted : combatManager.LockedCombatTarget.IsAttackCommitted))
        {
            Debug.Log("[CombatCinematic] Skill refuse pendant l'attaque ennemie : seuls LightSkill, QTE reussi ou mort interrompent le coup.", this);
            return false;
        }
        if (!combatManager.TryBeginCinematicTarget(this, resolved?.Target ?? combatManager.LockedCombatTarget, AbortForActionTermination)) return false;
        active = true;
        playbackStarted = false;
        impactResolved = false;
        activeSkill = skill;
        activeCasterRole = casterRole;
        activeEnemyCaster = caster;
        sessionToken++;
        targeting = resolved?.WithGeneration(sessionToken);

        combatManager.CancelPlayerActionForCinematic();
        SuspendEncounter();
        combatManager.SetCinematicSequenceActive(true);
        playerLockHeld = combatManager.TryLockPlayerForCinematic(this, out playerLock);
        if (resolved != null && !playerLockHeld) { EndSession(CombatCinematicEndReason.Failed); return false; }
        InputModeCoordinator.Enter(this, InputMode.Cinematic);
        combatInput?.SetCinematicInputSuspended(true);

        CombatCinematicContext context = new CombatCinematicContext(
            combatManager,
            skill,
            ResolveCinematicSkillImpact,
            casterRole,
            caster,
            allowGameplayCameraFallback: resolved != null && !definition.CombatCinematicRigPrefab.HasAuthoredCamera,
            allowMissingTimelineBindings: resolved != null,
            explicitTarget: targeting?.Target);
        if (!cinematicPlayback.TryPlay(
                definition.CombatCinematicRigPrefab,
                context,
                // The rig owns its baked Timeline. The source Timeline stored
                // on SkillSO is authoring data and must never be evaluated in
                // gameplay.
                null,
                definition.PlayerAnimatorTrackName,
                definition.EnemyAnimatorTrackName,
                null,
                OnRuntimeRigCompleted,
                out string error))
        {
            Debug.LogWarning("[CombatSkillCinematic] Lancement refuse pour '" + skill.SkillName + "': " + error, this);
            EndSession(CombatCinematicEndReason.Failed);
            return false;
        }

        StartWatchdog(sessionToken, definition.Timeline);
        playbackStarted = true;
        return true;
    }

    private void OnRuntimeRigCompleted(CombatCinematicRig rig)
    {
        EndSession(rig != null ? rig.LastEndReason : CombatCinematicEndReason.Interrupted);
    }

    private void LateUpdate()
    {
        if (active && !CombatLocalCinematicPolicy.AllowsLocalExecution) { AbortForActionTermination(); return; }
        if (active && targeting != null && !targeting.TargetAlive) AbortForActionTermination();
    }

    private void EndSession(CombatCinematicEndReason reason)
    {
        if (!active)
        {
            return;
        }

        bool completed = reason == CombatCinematicEndReason.Completed;
        sessionToken++;
        StopWatchdog();
        if (completed)
        {
            ApplyPostTimelineStates();
        }

        if (playbackStarted && activeCasterRole == CombatCinematicCasterRole.Enemy && activeEnemyCaster != null)
        {
            EnemyController caster = activeEnemyCaster;
            bool keepConfiguredEnemyExitState = completed && activeSkill != null && activeSkill.Cinematic != null &&
                                                activeSkill.Cinematic.PostTimelineEnemyState != null &&
                                                activeSkill.Cinematic.PostTimelineEnemyState.IsConfigured;
            caster.CompleteEnemyAttackWhenGrounded(() =>
            {
                combatManager?.CompleteEnemyAttack(caster);
                if (!keepConfiguredEnemyExitState)
                {
                    caster.ReturnToIdleAnimation();
                }
            });
        }

        RestoreEncounter();
        combatManager?.EndCinematicTarget(this);
        combatManager?.SetCinematicSequenceActive(false);
        if (playerLockHeld)
        {
            playerLock?.Dispose();
            playerLockHeld = false;
        }

        InputModeCoordinator.Exit(this);
        bool combatStillActive = combatManager != null && combatManager.IsCombatActive;
        if (combatStillActive)
        {
            combatInput?.SetCinematicInputSuspended(false);
        }
        else
        {
            combatInput?.SetInputActive(false);
        }
        if (completed && (activeSkill == null || activeSkill.Cinematic.PostTimelinePlayerState == null ||
                          !activeSkill.Cinematic.PostTimelinePlayerState.IsConfigured))
        {
            // The subsequent held-input reconciliation reads the actual stick
            // and sprint button after the InputMap handoff has settled.
            combatManager?.ResumePlayerLocomotionAfterCinematic(false, false);
        }

        LocalPlayerInput.RequestHeldLocomotionReconciliation("Combat Skill cinematic completed");
        combatManager?.ResolveDeferredCombatOutcome();
        active = false;
        playbackStarted = false;
        impactResolved = false;
        activeSkill = null;
        activeEnemyCaster = null;
        targeting = null;
    }

    private void SuspendEncounter()
    {
        if (targeting != null)
        {
            ownedTargetSuspensions.Clear();
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                bool suspended = targeting.TargetRoot.GetComponent<LitBrainsEnemy>()?.IsCinematicSuspended == true ||
                    targeting.TargetRoot.GetComponent<EnemyController>()?.IsCinematicSuspended == true;
                if (!suspended)
                {
                    targeting.Target.SetCinematicSuspended(true);
                    ownedTargetSuspensions.Add(targeting.Target);
                }
                return;
            }
            foreach (var enemy in FindObjectsByType<LitBrainsEnemy>(FindObjectsInactive.Exclude))
                if (!enemy.IsCinematicSuspended && !enemy.IsDead) { enemy.SetCinematicSuspended(true); ownedTargetSuspensions.Add(enemy); }
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude))
                if (!enemy.IsCinematicSuspended && enemy.Health?.IsDead != true) { enemy.SetCinematicSuspended(true); ownedTargetSuspensions.Add(new LegacyCombatTarget(enemy)); }
            return;
        }
        combatManager?.SuspendCombatTarget(true);
        suspendedEnemies.Clear();
        EnemyController[] behaviours = FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null) continue;
            behaviours[i].SetCinematicSuspended(true);
            suspendedEnemies.Add(behaviours[i]);
        }
    }

    private void RestoreEncounter()
    {
        if (targeting != null)
        {
            foreach (var enemy in ownedTargetSuspensions)
                if (SkillTargetingContract.Alive(enemy)) enemy.SetCinematicSuspended(false);
            ownedTargetSuspensions.Clear();
            if (combatManager.IsCombatActive && targeting.TargetAlive)
                targeting.TargetRoot.GetComponent<LitBrainsEnemy>()?.NotifyPlayerContact(combatManager.PlayerRoot);
            return;
        }
        combatManager?.SuspendCombatTarget(false);
        for (int i = 0; i < suspendedEnemies.Count; i++)
        {
            if (suspendedEnemies[i] != null)
            {
                suspendedEnemies[i].SetCinematicSuspended(false);
            }
        }
        suspendedEnemies.Clear();
    }

    private void ApplyPostTimelineStates()
    {
        CombatSkillCinematicDefinition definition = activeSkill != null ? activeSkill.Cinematic : null;
        if (definition == null || combatManager == null) return;

        ApplyState(combatManager.PlayerAnimator, definition.PostTimelinePlayerState);
        ApplyState(targeting != null ? targeting.TargetAlive ? targeting.Target.Animator : null : combatManager.EngagedEnemy != null ? combatManager.EngagedEnemy.Animator : null, definition.PostTimelineEnemyState);
    }

    private static void ApplyState(Animator animator, CombatCinematicPostTimelineState state)
    {
        if (animator == null || state == null || !state.IsConfigured) return;
        int stateHash = Animator.StringToHash(state.AnimatorStateName);
        if (!animator.HasState(0, stateHash))
        {
            Debug.LogWarning("[CombatSkillCinematic] State de sortie introuvable : '" + state.AnimatorStateName + "'.", animator);
            return;
        }

        animator.CrossFade(stateHash, state.TransitionSeconds, 0, state.NormalizedStartTime);
    }

    internal static void ApplyParticipantExit(SkillSO skill, Animator player, ICombatTarget target)
    {
        if (skill == null || !skill.HasCombatCinematic) return;
        ApplyState(player, skill.Cinematic.PostTimelinePlayerState);
        if (target != null && !target.IsDead) ApplyState(target.Animator, skill.Cinematic.PostTimelineEnemyState);
    }

    private void StartWatchdog(int token, UnityEngine.Playables.PlayableAsset timeline)
    {
        if (watchdog != null)
        {
            StopCoroutine(watchdog);
            watchdog = null;
        }
        float timeout = Mathf.Max(1f, timeline != null ? (float)timeline.duration : 0f) + completionGraceSeconds;
        watchdog = StartCoroutine(WatchForCompletion(token, timeout));
    }

    private void StopWatchdog()
    {
        if (watchdog != null)
        {
            StopCoroutine(watchdog);
            watchdog = null;
        }
    }

    private System.Collections.IEnumerator WatchForCompletion(int token, float timeout)
    {
        yield return new WaitForSecondsRealtime(timeout);
        if (token != sessionToken || !active) yield break;
        cinematicPlayback?.StopActive();
        yield return null;
        if (token == sessionToken && active)
        {
            EndSession(CombatCinematicEndReason.Interrupted);
        }
    }

    private void ResolveReferences()
    {
        if (combatManager == null) combatManager = GetComponent<RealTimeCombatManager>();
        if (combatInput == null) combatInput = GetComponent<RealTimeCombatInput>();
        if (cinematicPlayback == null) cinematicPlayback = GetComponent<CombatCinematicPlaybackService>();
    }
}
