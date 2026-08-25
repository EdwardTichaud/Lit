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

    private readonly List<RealTimeCombatEnemyBehaviour> suspendedEnemies = new List<RealTimeCombatEnemyBehaviour>();
    private SkillSO activeSkill;
    private RealTimeCombatEnemy activeEnemyCaster;
    private CombatCinematicCasterRole activeCasterRole;
    private bool active;
    private bool playbackStarted;
    private bool impactResolved;
    private bool playerLockHeld;
    private int sessionToken;
    private Coroutine watchdog;

    public bool IsPlaying => active;
    public SkillSO ActiveSkill => activeSkill;

    private void Awake() => ResolveReferences();

    private void OnDisable()
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

    public bool TryPlayEnemySkill(RealTimeCombatEnemy caster, SkillSO skill)
    {
        return TryPlay(skill, CombatCinematicCasterRole.Enemy, caster);
    }

    public void ResolveCinematicSkillImpact()
    {
        if (!active || impactResolved || activeSkill == null || combatManager == null)
        {
            return;
        }

        impactResolved = true;
        if (activeCasterRole == CombatCinematicCasterRole.Player)
        {
            int applied = combatManager.ApplySkillDamageToLockedEnemy(activeSkill);
            if (applied > 0)
            {
                CombatImpactFeedbackController.EnsureInstance()?.PlayImpact(activeSkill, combatManager.LockedEnemy);
            }
        }
        else if (activeEnemyCaster != null)
        {
            combatManager.ApplyEnemySkillDamageToPlayer(activeEnemyCaster, activeSkill);
        }
    }

    private bool TryPlay(SkillSO skill, CombatCinematicCasterRole casterRole, RealTimeCombatEnemy caster)
    {
        ResolveReferences();
        if (active || cinematicPlayback == null || cinematicPlayback.IsPlaying || combatManager == null ||
            !combatManager.IsCombatActive || skill == null || !skill.HasCombatCinematic ||
            combatManager.LockedEnemy == null || (combatManager.LockedEnemy.Health != null && combatManager.LockedEnemy.Health.IsDead))
        {
            return false;
        }

        if (casterRole == CombatCinematicCasterRole.Enemy &&
            (caster == null || caster != combatManager.LockedEnemy || caster.ActiveSkill != skill))
        {
            return false;
        }

        CombatSkillCinematicDefinition definition = skill.Cinematic;
        active = true;
        playbackStarted = false;
        impactResolved = false;
        activeSkill = skill;
        activeCasterRole = casterRole;
        activeEnemyCaster = caster;
        sessionToken++;

        combatManager.CancelPlayerActionForCinematic();
        SuspendEncounter();
        combatManager.SetCinematicSequenceActive(true);
        playerLockHeld = combatManager.TryLockPlayerForCinematic();
        InputModeCoordinator.Enter(this, InputMode.Cinematic);
        combatInput?.SetCinematicInputSuspended(true);

        CombatCinematicContext context = new CombatCinematicContext(
            combatManager,
            skill,
            ResolveCinematicSkillImpact,
            casterRole,
            caster);
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
            RealTimeCombatEnemy caster = activeEnemyCaster;
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
        combatManager?.SetCinematicSequenceActive(false);
        if (playerLockHeld)
        {
            combatManager?.UnlockPlayerAfterCinematic();
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
    }

    private void SuspendEncounter()
    {
        suspendedEnemies.Clear();
        RealTimeCombatEnemyBehaviour[] behaviours = FindObjectsByType<RealTimeCombatEnemyBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null) continue;
            behaviours[i].SetCinematicSuspended(true);
            suspendedEnemies.Add(behaviours[i]);
        }
    }

    private void RestoreEncounter()
    {
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
        ApplyState(combatManager.LockedEnemy != null ? combatManager.LockedEnemy.Animator : null, definition.PostTimelineEnemyState);
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
