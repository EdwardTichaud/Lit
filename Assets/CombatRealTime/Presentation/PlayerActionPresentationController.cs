using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed partial class PlayerActionPresentationController : MonoBehaviour
{
    private const string LocomotionState = "Base Layer.Locomotion";
    private const string CombatLocomotionState = "Base Layer.CombatLocomotion";
    private const string CombatIdleState = "Base Layer.CombatIdle";
    private const string WalkStartState = "Base Layer.Walk_Start";
    private const string RunStartState = "Base Layer.Run_Start";

    [SerializeField] private Animator animator;
    private LitOpsiveLocomotionBridge locomotionBridge;
    [SerializeField] private bool debugTransitions;

    public enum ActionEndReason { Completed, Interrupted, Failed, OwnerDisabled, Death }
    private readonly System.Collections.Generic.List<Action<ActionEndReason>> sessionCleanup =
        new System.Collections.Generic.List<Action<ActionEndReason>>();
    private UnityEngine.Object sessionOwner;
    private bool externallyDriven;
    private string activeActionName;
    public int ActionGeneration => activeToken;
    public bool IsCurrentSession(int generation) => actionActive && generation == activeToken;
    public ActionEndReason LastEndReason { get; private set; }

    public bool RegisterActionCleanup(int generation, Action cleanup)
    {
        if (!IsCurrentSession(generation) || cleanup == null) return false;
        sessionCleanup.Add(_ => cleanup());
        return true;
    }

    public bool RegisterActionTermination(int generation, Action<ActionEndReason> cleanup)
    {
        if (!IsCurrentSession(generation) || cleanup == null) return false;
        sessionCleanup.Add(cleanup);
        return true;
    }

    public int BeginExternalPresentation(UnityEngine.Object owner, bool allowMobility = false)
    {
        if (deathAnimationLocked || owner == null) return 0;
        TerminateAction(activeToken, ActionEndReason.Interrupted, false);
        activeToken++;
        actionActive = true;
        externallyDriven = true;
        activeAllowsMobilityCancel = allowMobility;
        recoveryOpen = allowMobility;
        sessionOwner = owner;
        activeActionName = owner.name;
        return activeToken;
    }

    public void EndExternalPresentation(int generation)
    {
        if (externallyDriven) TerminateAction(generation, ActionEndReason.Completed);
    }

    public bool CanPlayState(string stateName) => isActiveAndEnabled && !deathAnimationLocked &&
        animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null &&
        !string.IsNullOrWhiteSpace(stateName) && animator.HasState(0, Animator.StringToHash(stateName));

    public bool AcceptAnimationEvent(AnimationEvent source)
    {
        if (source == null || !source.isFiredByAnimator || !actionActive || deathAnimationLocked ||
            animator == null || !animator.isActiveAndEnabled) return false;
        if (externallyDriven)
        {
            if (source.functionName == "QTE")
                return sessionOwner is CombatHealthThresholdController threshold && threshold.AcceptsQteEvent(source);
            var manager = RealTimeCombatManager.Instance;
            var playback = manager != null ? manager.GetComponent<CombatCinematicPlaybackService>() : null;
            var director = playback != null && playback.ActiveRig != null ? playback.ActiveRig.Director :
                (sessionOwner as Component)?.GetComponent<UnityEngine.Playables.PlayableDirector>();
            if (!CombatCinematicPlaybackService.IsActivePlayerClip(director, animator, source.animatorClipInfo.clip)) return false;
            if (source.functionName == "ResolveLightSkillImpact")
                return sessionOwner is LightSkillCombatController light && light.IsCinematicPlaying;
            if (source.functionName == "ResolveCounterSkillImpact")
                return sessionOwner is CounterSkillCombatController counter && counter.IsCinematicPlaying;
            if (source.functionName == "ResolveCinematicSkillImpact")
                return sessionOwner is CombatSkillCinematicController skill && skill.IsPlaying;
            return sessionOwner is LightSkillCombatController lightOwner && lightOwner.IsCinematicPlaying ||
                sessionOwner is CounterSkillCombatController counterOwner && counterOwner.IsCinematicPlaying ||
                sessionOwner is CombatSkillCinematicController skillOwner && skillOwner.IsPlaying;
        }
        if (!IsActiveState(source.animatorStateInfo) || !TryGetActiveAnimatorState(out _)) return false;
        bool transitioning = animator.IsInTransition(0);
        if (transitioning && IsActiveState(animator.GetCurrentAnimatorStateInfo(0)) &&
            IsActiveState(animator.GetNextAnimatorStateInfo(0))) return false;
        var clips = transitioning ? animator.GetNextAnimatorClipInfo(0) : animator.GetCurrentAnimatorClipInfo(0);
        foreach (var clip in clips)
            if (clip.weight > 0f && clip.clip == source.animatorClipInfo.clip) return true;
        return false;
    }

    private Coroutine actionRoutine;
    private Coroutine targetLungeRoutine;
    private int targetLungeToken;
    private bool targetLungeOwnsPlanarMotion;
    private int activeStateHash;
    private int activeToken;
    private PlayerActionFacingMode activeFacingMode;
    private bool actionActive;
    private bool activeActionIsBasic;
    private bool basicSkillInterruptedByDamage;
    private bool chainWindowOpen;
    private bool mobilityCancelOpen;
    private bool recoveryOpen;
    private bool activeAllowsMobilityCancel;
    private bool activeRequestsAirborneLanding;
    private bool activeHoldsAirborne;
    private MotionHandoffProfile activeAirborneLandingHandoff;
    private bool activeLandingRequested;
    private float activeHandoffStartedAt;
    private bool hasBufferedAction;
    private int bufferedStateHash;
    private PlayerActionPresentationProfile bufferedProfile;
    private string bufferedActionName;
    private bool bufferedActionIsBasic;
    private BasicSkillsSO bufferedBasicSkill;
    private Transform actionFacingTarget;
    private bool deathAnimationLocked;
    private int deathStateHash;

    public bool IsActionActive => actionActive;
    public bool IsChainWindowOpen => chainWindowOpen;
    public bool IsMobilityCancelOpen => mobilityCancelOpen;
    public bool IsRecoveryOpen => recoveryOpen;
    public bool CanStartAction => !deathAnimationLocked && (!actionActive || recoveryOpen);
    public bool CanAcceptBasicSkillInput => !deathAnimationLocked && !hasBufferedAction;
    public string BasicSkillInputBlockReason
    {
        get
        {
            if (deathAnimationLocked) return "animation de mort verrouillee";
            if (hasBufferedAction) return "un BasicSkill est deja bufferise";
            return null;
        }
    }
    public bool CanChainBasicSkill => !deathAnimationLocked && !hasBufferedAction && (!actionActive || recoveryOpen || chainWindowOpen);
    public bool CanCancelToMobility => !deathAnimationLocked &&
                                       (!actionActive || (activeAllowsMobilityCancel && (mobilityCancelOpen || recoveryOpen)));
    public bool IsDeathAnimationLocked => deathAnimationLocked;
    public bool OwnsOnlyActionMotionLock(LitOpsiveLocomotionBridge bridge) =>
        actionActive && bridge != null && (bridge.HasOnlyScriptedMotionLock(this) ||
            bridge.HasOnlyScriptedMotionLock(bridge.GetComponent<PlayerStateMotionController>()));

    public void ClearBufferedBasicAction()
    {
        if (!bufferedActionIsBasic) return;
        hasBufferedAction = false;
        bufferedBasicSkill = null;
        bufferedProfile = null;
        bufferedStateHash = 0;
        bufferedActionName = null;
        bufferedActionIsBasic = false;
    }
    public event Action ActionEnded;

    [ContextMenu("Toggle Action Diagnostics")]
    private void ToggleDiagnostics()
    {
        debugTransitions = !debugTransitions;
    }

    public void ResolveReferences(Animator targetAnimator, LitOpsiveLocomotionBridge targetBridge)
    {
        if (targetAnimator != null) animator = targetAnimator;
        if (targetBridge != null) locomotionBridge = targetBridge;
    }

    public void SetActionFacingTarget(Transform target)
    {
        if (deathAnimationLocked)
        {
            return;
        }

        actionFacingTarget = target;
        FaceActionTarget();
    }

    public void ClearActionFacingTarget()
    {
        actionFacingTarget = null;
    }

    public bool CancelActionForMobility()
    {
        if (!CanCancelToMobility)
        {
            return false;
        }

        if (actionActive)
        {
            CancelAction();
        }

        return true;
    }

    public bool TryPlaySkill(SkillSO skill, int stateHash)
    {
        BasicSkillsSO basicSkill = skill as BasicSkillsSO;
        return skill != null && TryPlay(
            stateHash,
            skill.Presentation,
            skill.SkillName,
            basicSkill != null,
            basicSkill);
    }

    public bool TryPlayCombatState(string stateName, PlayerActionPresentationProfile profile, string debugName)
    {
        if (string.IsNullOrWhiteSpace(stateName)) return false;
        return TryPlay(Animator.StringToHash(stateName), profile, debugName, false, null);
    }

    public bool TryReplaceWithDamageReaction(string stateName, PlayerActionPresentationProfile profile)
    {
        if (!CanPlayState(stateName) || externallyDriven && !activeAllowsMobilityCancel ||
            locomotionBridge != null && (locomotionBridge.IsScriptedTraversalActive ||
            locomotionBridge.IsCinematicMotionSessionActive)) return false;
        return StartAction(Animator.StringToHash(stateName), profile ?? PlayerActionPresentationProfile.CreateDefault(),
            "Hurt", false, null);
    }

    public IEnumerator WaitForChainWindow()
    {
        int token = activeToken;
        while (actionActive && token == activeToken && !CanChainBasicSkill)
        {
            yield return null;
        }
    }

    public void InterruptBasicSkillForDamage()
    {
        if (actionActive && activeActionIsBasic)
        {
            basicSkillInterruptedByDamage = true;
            CancelAction();
            return;
        }
        if (!bufferedActionIsBasic) return;
        hasBufferedAction = false;
        bufferedBasicSkill = null;
        bufferedProfile = null;
        bufferedStateHash = 0;
        bufferedActionName = null;
        bufferedActionIsBasic = false;
    }

    public void CancelAction()
    {
        TerminateAction(activeToken, deathAnimationLocked ? ActionEndReason.Death : ActionEndReason.Interrupted);
    }

    private void TerminateAction(int generation, ActionEndReason reason, bool handoff = true)
    {
        if (generation != activeToken || (!actionActive && !hasBufferedAction)) return;
        // Invalidate before cleanup: callbacks may be reentrant or belong to the outgoing clip.
        activeToken++;
        actionActive = false;
        externallyDriven = false;
        sessionOwner = null;
        LastEndReason = reason;
        var cleanups = sessionCleanup.ToArray();
        sessionCleanup.Clear();
        if (actionRoutine != null) StopCoroutine(actionRoutine);
        actionRoutine = null;
        CancelTargetLunge();
        GetMobility()?.CancelAnimationDash(transform);
        ReleaseAirborneHold();
        locomotionBridge?.GetComponent<PlayerStateMotionController>()?.Cancel();
        chainWindowOpen = mobilityCancelOpen = recoveryOpen = false;
        activeAllowsMobilityCancel = activeActionIsBasic = false;
        hasBufferedAction = bufferedActionIsBasic = false;
        bufferedStateHash = 0;
        bufferedProfile = null;
        bufferedBasicSkill = null;
        bufferedActionName = null;
        activeRequestsAirborneLanding = activeLandingRequested = false;
        activeAirborneLandingHandoff = null;
        activeHandoffStartedAt = 0f;
        foreach (var cleanup in cleanups)
        {
            try { cleanup(reason); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
        if (handoff)
        {
            ActionEnded?.Invoke();
            if (!actionActive) RequestLocomotionHandoff();
        }
    }

    private void RecoverAction(int generation, string reason, PlayerActionPresentationProfile profile = null)
    {
        if (!IsCurrentSession(generation)) return;
        int current = animator != null && animator.isActiveAndEnabled ? animator.GetCurrentAnimatorStateInfo(0).fullPathHash : 0;
        int next = animator != null && animator.isActiveAndEnabled && animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0).fullPathHash : 0;
        bool stillOwnsPose = animator != null && animator.isActiveAndEnabled &&
            IsActiveState(animator.GetCurrentAnimatorStateInfo(0)) && next == 0;
        Debug.LogWarning($"[PlayerAction] Recovery actor={name} session={generation} action={activeActionName} " +
            $"reason={reason} current={current} next={next} resources={sessionCleanup.Count}", this);
        RealTimeCombatManager.Instance?.GetComponent<RealTimeCombatInput>()?.CancelBufferedBasicSkills();
        TerminateAction(generation, ActionEndReason.Failed);
        if (actionActive || !stillOwnsPose || deathAnimationLocked || animator == null ||
            locomotionBridge == null || !locomotionBridge.Grounded || locomotionBridge.IsInputSuppressedByUcc ||
            locomotionBridge.IsCinematicMotionSessionActive) return;
        animator.CrossFade(ResolveCurrentLocomotionDestination(), profile != null ? profile.exitBlendSeconds : 0.08f, 0);
    }

    /// <summary>Ends combat ownership and exits its animation, preserving airborne traversal and death.</summary>
    public void ReturnToExplorationAfterCombat()
    {
        bool hadAction = actionActive || hasBufferedAction;
        ClearActionFacingTarget();
        CancelAction();
        if (deathAnimationLocked || animator == null || !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null ||
            (locomotionBridge != null && (!locomotionBridge.Grounded || locomotionBridge.IsFlightActive ||
                locomotionBridge.IsInputSuppressedByUcc || locomotionBridge.IsCinematicMotionSessionActive))) return;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : default;
        if (!hadAction && !IsCombatPresentation(current) && !IsCombatPresentation(next)) return;

        int destination = Animator.StringToHash(LocomotionState);
        if (animator.HasState(0, destination))
            animator.CrossFade(destination, 0.12f, 0);
    }

    private static bool IsCombatPresentation(AnimatorStateInfo state) =>
        state.IsName(CombatIdleState) || state.IsName(CombatLocomotionState) || state.IsTag("Combat");

    /// <summary>
    /// Death is a terminal presentation state for this actor instance. It can
    /// only be cleared by rebuilding the player on revive/reload.
    /// </summary>
    public bool LockDeathAnimation(string stateName, float transitionSeconds = 0.05f)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, stateHash))
        {
            return false;
        }

        deathAnimationLocked = true;
        deathStateHash = stateHash;
        CancelAction();
        animator.CrossFade(deathStateHash, Mathf.Clamp(transitionSeconds, 0f, 0.25f), 0, 0f);
        return true;
    }

    public void ClearDeathAnimationLock()
    {
        deathAnimationLocked = false;
        deathStateHash = 0;
        CancelAction();
    }

    /// <summary>Returns Animator ownership to UCC after a Timeline that did not use an action profile.</summary>
    public void ResumeLocomotionFromCinematic(bool movementHeld, bool sprintHeld, float transitionSeconds = 0.12f)
    {
        if (deathAnimationLocked || animator == null)
        {
            return;
        }

        CancelAction();
        locomotionBridge?.GetComponent<PlayerStateMotionController>()?.Cancel();
        locomotionBridge?.RefreshLocomotionPresentation();

        string destination = ResolveLocomotionDestination(movementHeld, sprintHeld);
        int destinationHash = Animator.StringToHash(destination);
        if (animator.HasState(0, destinationHash))
        {
            animator.CrossFade(destinationHash, Mathf.Clamp(transitionSeconds, 0f, 0.25f), 0);
        }
    }

    private void Awake()
    {
        SkillEventsAwake();
        CharacterAnimationController animationContract = GetComponent<CharacterAnimationController>();
        if (animationContract != null && animationContract.ValidateContract(out _))
        {
            animator = animationContract.Animator;
        }
        if (locomotionBridge == null) locomotionBridge = GetComponentInChildren<LitOpsiveLocomotionBridge>();
    }

    private void OnDisable()
    {
        SkillEventsOnDisable();
        TerminateAction(activeToken, ActionEndReason.OwnerDisabled);
    }

    /// <summary>
    /// Starts an optional, entirely UCC-driven approach/rebound for a player
    /// Skill. It is deliberately independent from Animator root motion.
    /// </summary>
    public void BeginTargetLunge(SkillSO skill, EnemyController target)
    {
        PlayerTargetLungeProfile profile = skill != null ? skill.TargetLunge : null;
        if (profile == null || !profile.enabled || target == null || locomotionBridge == null || !locomotionBridge.IsDriving)
        {
            return;
        }

        CancelTargetLunge();
        // A target lunge is authored as an in-place animation even if a stale
        // Skill asset still carries an old root-motion presentation setting.
        locomotionBridge.GetComponent<PlayerStateMotionController>()?.Cancel();
        targetLungeRoutine = StartCoroutine(RunTargetLunge(profile, target, ++targetLungeToken, activeToken));
    }

    private void CancelTargetLunge()
    {
        targetLungeToken++;
        if (targetLungeRoutine != null)
        {
            StopCoroutine(targetLungeRoutine);
            targetLungeRoutine = null;
        }

        if (targetLungeOwnsPlanarMotion)
        {
            locomotionBridge?.DriveScriptedPlanarMotion(this, Vector3.zero);
            locomotionBridge?.EndScriptedPlanarMotion(this);
            targetLungeOwnsPlanarMotion = false;
        }
    }

    private IEnumerator RunTargetLunge(PlayerTargetLungeProfile profile, EnemyController target, int lungeToken, int actionToken)
    {
        if (!locomotionBridge.BeginScriptedPlanarMotion(this))
        {
            yield break;
        }

        targetLungeOwnsPlanarMotion = true;
        bool reachedTarget = false;
        float elapsed = 0f;
        int blockedFrames = 0;
        try
        {
            while (lungeToken == targetLungeToken && actionToken == activeToken && actionActive && target != null)
            {
                Transform targetTransform = target.LockPoint != null ? target.LockPoint : target.transform;
                Vector3 toTarget = Vector3.ProjectOnPlane(targetTransform.position - transform.position, Vector3.up);
                float distance = toTarget.magnitude;
                float arrivalDistance = profile.stoppingDistance + profile.contactTolerance;
                if (distance <= arrivalDistance)
                {
                    reachedTarget = true;
                    break;
                }

                if (elapsed >= profile.approachDurationSeconds)
                {
                    break;
                }

                Vector3 direction = toTarget / Mathf.Max(0.0001f, distance);
                locomotionBridge.SetActionFacingDirection(direction);
                float remaining = Mathf.Max(0.025f, profile.approachDurationSeconds - elapsed);
                // This is a rapid Lerp-style convergence expressed as an UCC
                // target velocity, rather than a direct Transform interpolation.
                float speed = Mathf.Min(profile.maximumApproachSpeed, distance / remaining);
                Vector3 before = transform.position;
                if (!locomotionBridge.DriveScriptedPlanarMotion(this, direction * speed))
                {
                    break;
                }

                yield return null;
                elapsed += Time.unscaledDeltaTime;
                float moved = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude;
                if (moved < 0.002f) blockedFrames++; else blockedFrames = 0;
                if (blockedFrames >= 3) break;
            }
        }
        finally
        {
            if (targetLungeOwnsPlanarMotion)
            {
                locomotionBridge.DriveScriptedPlanarMotion(this, Vector3.zero);
                locomotionBridge.EndScriptedPlanarMotion(this);
                targetLungeOwnsPlanarMotion = false;
            }
            targetLungeRoutine = null;
        }

        if (lungeToken != targetLungeToken || actionToken != activeToken || !actionActive || !reachedTarget || target == null)
        {
            yield break;
        }

        Vector3 away = Vector3.ProjectOnPlane(transform.position - target.transform.position, Vector3.up);
        if (away.sqrMagnitude <= 0.0001f) away = -transform.forward;
        away.Normalize();
        Vector3 impulse = away * profile.reboundHorizontalImpulse + Vector3.up * profile.reboundVerticalImpulse;
        locomotionBridge.AddExternalImpulseUntilGrounded(
            impulse,
            ForceMode.VelocityChange,
            profile.minimumInputLockSeconds,
            profile.maximumInputLockSeconds,
            profile.airborneInertiaSeconds,
            profile.airborneInertiaEndSpeedMultiplier,
            this);
    }

    private void LateUpdate()
    {
        if (deathAnimationLocked)
        {
            KeepDeathAnimationActive();
            return;
        }

        if (actionActive && (sessionOwner == null || sessionOwner is Behaviour owner && !owner.isActiveAndEnabled ||
            sessionOwner is GameObject ownerObject && !ownerObject.activeInHierarchy))
        {
            TerminateAction(activeToken, ActionEndReason.OwnerDisabled);
            return;
        }
        if (actionActive && !externallyDriven) FaceActionTarget();
    }

    private bool TryPlay(
        int stateHash,
        PlayerActionPresentationProfile profile,
        string debugName,
        bool allowChainInterrupt,
        BasicSkillsSO basicSkill)
    {
        if (deathAnimationLocked)
        {
            return false;
        }

        if (animator == null || !animator.isActiveAndEnabled || !animator.HasState(0, stateHash))
        {
            return false;
        }

        profile = profile ?? PlayerActionPresentationProfile.CreateDefault();
        if (actionActive && allowChainInterrupt && chainWindowOpen && !recoveryOpen)
        {
            if (hasBufferedAction)
            {
                return false;
            }

            hasBufferedAction = true;
            bufferedStateHash = stateHash;
            bufferedProfile = profile;
            bufferedActionName = debugName;
            bufferedActionIsBasic = allowChainInterrupt;
            bufferedBasicSkill = basicSkill;
            Trace("buffered", debugName, profile);
            return true;
        }

        if (actionActive && !recoveryOpen)
        {
            return false;
        }

        return StartAction(stateHash, profile, debugName, allowChainInterrupt, basicSkill);
    }

    private bool StartAction(
        int stateHash,
        PlayerActionPresentationProfile profile,
        string debugName,
        bool isBasicAction,
        BasicSkillsSO basicSkill)
    {
        if (!isActiveAndEnabled || deathAnimationLocked || animator == null ||
            !animator.isActiveAndEnabled || !animator.HasState(0, stateHash)) return false;
        TerminateAction(activeToken, ActionEndReason.Interrupted, false);
        sessionOwner = this;
        externallyDriven = false;
        activeActionName = debugName;

        activeToken++;
        activeStateHash = stateHash;
        activeFacingMode = profile.facingMode;
        actionActive = true;
        activeActionIsBasic = isBasicAction;
        if (isBasicAction) basicSkillInterruptedByDamage = false;
        chainWindowOpen = false;
        mobilityCancelOpen = false;
        recoveryOpen = false;
        activeAllowsMobilityCancel = profile.allowMobilityCancel;
        activeRequestsAirborneLanding = basicSkill != null && basicSkill.RequestsLandingDuringAnimation;
        activeHoldsAirborne = basicSkill != null && basicSkill.HoldsAirborneDuringAnimation;
        activeAirborneLandingHandoff = basicSkill != null ? basicSkill.AirborneLandingHandoff : null;
        activeLandingRequested = false;
        activeHandoffStartedAt = 0f;
        locomotionBridge?.GetComponent<PlayerStateMotionController>()?.SetActionPolicy(stateHash, profile.movementPolicy);
        if (activeHoldsAirborne)
        {
            locomotionBridge?.BeginCombatAirborneHold();
        }
        animator.CrossFade(stateHash, Mathf.Clamp(profile.entryBlendSeconds, 0f, 0.25f), 0);
        Trace("enter", debugName, profile);
        actionRoutine = StartCoroutine(TrackAction(activeToken, profile, debugName, isBasicAction));
        return true;
    }

    private IEnumerator TrackAction(int token, PlayerActionPresentationProfile profile, string debugName, bool isBasicAction)
    {
        yield return null;
        bool enteredState = false;
        float elapsed = 0f;
        const float stateEntryTimeout = 0.35f;
        CombatTimeDomain domain = GetComponent<CombatTimeDomain>();
        float chainTime = Mathf.Clamp01(profile.chainNormalizedTime);
        float recoveryTime = Mathf.Max(chainTime, Mathf.Clamp01(profile.recoveryNormalizedTime));
        float completionTime = activeHoldsAirborne ? 1f : recoveryTime;
        float chainTransitionTime = Mathf.Clamp(profile.chainTransitionNormalizedTime, chainTime, recoveryTime);
        float mobilityCancelTime = Mathf.Clamp(profile.mobilityCancelNormalizedTime, 0.05f, recoveryTime);

        while (token == activeToken && elapsed < stateEntryTimeout)
        {
            if (animator == null || !animator.isActiveAndEnabled) break;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (TryGetActiveAnimatorState(out state))
            {
                enteredState = true;
                break;
            }

            elapsed += domain != null ? domain.DeltaTime : Time.deltaTime;
            yield return null;
        }

        if (!enteredState || token != activeToken)
        {
            RecoverAction(token, "Animator entry failed", profile);
            yield break;
        }

        float previousNormalizedTime = float.NaN;
        float stalledSeconds = 0f;
        while (token == activeToken)
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                RecoverAction(token, "Animator disabled", profile);
                yield break;
            }
            FaceActionTarget();
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (!TryGetActiveAnimatorState(out state))
            {
                FinishUnexpectedActionExit(token, isBasicAction);
                yield break;
            }

            bool cinematic = RealTimeCombatManager.Instance != null && RealTimeCombatManager.Instance.IsCinematicSequenceActive;
            float actionDelta = domain != null ? domain.DeltaTime : Time.deltaTime;
            stalledSeconds = !cinematic && Mathf.Approximately(previousNormalizedTime, state.normalizedTime)
                ? stalledSeconds + Mathf.Max(0f, actionDelta) : 0f;
            previousNormalizedTime = state.normalizedTime;
            if (stalledSeconds >= 1f)
            {
                RecoverAction(token, "No local animation progress for 1s", profile);
                yield break;
            }

            RequestAirborneLandingIfDue();

            if (!chainWindowOpen && state.normalizedTime >= chainTime)
            {
                chainWindowOpen = true;
                Trace("chain-open", debugName, profile);
            }

            if (!mobilityCancelOpen && activeAllowsMobilityCancel && state.normalizedTime >= mobilityCancelTime)
            {
                mobilityCancelOpen = true;
                Trace("mobility-cancel-open", debugName, profile);
            }

            if (hasBufferedAction && state.normalizedTime >= chainTransitionTime)
            {
                Trace("chain-transition", debugName, profile);
                if (StartBufferedAction(token))
                {
                    yield break;
                }
            }

            if (state.normalizedTime >= completionTime)
            {
                recoveryOpen = true;
                Trace("recovery", debugName, profile);
                if (StartBufferedAction(token))
                {
                    yield break;
                }

                if (profile.allowMoveAfterRecovery)
                {
                    yield return WaitForMotionHandoff(token, profile);
                    ResumeLocomotion(profile, token);
                }
                else
                {
                    FinishWithoutTransition(token);
                }

                yield break;
            }

            yield return null;
        }
    }

    private bool TryGetActiveAnimatorState(out AnimatorStateInfo state)
    {
        state = animator.GetCurrentAnimatorStateInfo(0);
        if (animator.IsInTransition(0))
        {
            var next = animator.GetNextAnimatorStateInfo(0);
            // The destination owns presentation as soon as a transition starts.
            if (!IsActiveState(next)) return false;
            state = next;
        }
        return IsActiveState(state);
    }

    private bool IsActiveState(AnimatorStateInfo state)
    {
        return state.fullPathHash == activeStateHash || state.shortNameHash == activeStateHash;
    }

    private void FaceActionTarget()
    {
        if (deathAnimationLocked || actionFacingTarget == null)
        {
            return;
        }

        Vector3 direction = actionFacingTarget.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (activeFacingMode == PlayerActionFacingMode.VisualOnly && FaceVisualRig(direction))
        {
            return;
        }

        if (locomotionBridge != null)
        {
            locomotionBridge.SetActionFacingDirection(direction);
            return;
        }

        if (RealTimeCombatManager.Instance == null || !RealTimeCombatManager.Instance.IsCombatActive)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    private bool FaceVisualRig(Vector3 worldDirection)
    {
        if (animator == null || !animator.isHuman)
        {
            return false;
        }

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null)
        {
            return false;
        }

        float yaw = Vector3.SignedAngle(transform.forward, worldDirection.normalized, transform.up);
        hips.rotation = Quaternion.AngleAxis(yaw, transform.up) * hips.rotation;
        return true;
    }

    private void ResumeLocomotion(PlayerActionPresentationProfile profile, int token)
    {
        if (deathAnimationLocked || token != activeToken || animator == null) return;

        if (locomotionBridge != null)
        {
            locomotionBridge.RefreshLocomotionPresentation();
        }

        // CombatIdle/CombatLocomotion are ground states. Crossing to either
        // while the capsule is still airborne cuts the jump presentation and
        // makes aerial actions feel stuck. UCC owns that handoff instead.
        if (locomotionBridge != null && !locomotionBridge.Grounded)
        {
            FinishWithoutTransition(token);
            return;
        }

        MotionHandoffProfile handoff = profile.handoff ?? MotionHandoffProfile.CreateActionDefault();
        float blend = Mathf.Max(profile.exitBlendSeconds, handoff.locomotionBlendSeconds);
        animator.CrossFade(ResolveCurrentLocomotionDestination(), Mathf.Clamp(blend, 0f, 0.25f), 0);
        FinishWithoutTransition(token);
    }

    private IEnumerator WaitForMotionHandoff(int token, PlayerActionPresentationProfile profile)
    {
        MotionHandoffProfile handoff = profile.handoff ?? MotionHandoffProfile.CreateActionDefault();
        activeHandoffStartedAt = Time.unscaledTime;
        while (token == activeToken && locomotionBridge != null && locomotionBridge.Grounded)
        {
            locomotionBridge.ApplyPlanarHandoffDamping(handoff.planarDampingPerSecond);
            if (locomotionBridge.IsMotionHandoffSettled(handoff) ||
                Time.unscaledTime - activeHandoffStartedAt >= handoff.maximumSettleSeconds)
            {
                break;
            }

            yield return null;
        }

        activeHandoffStartedAt = 0f;
    }

    private void FinishUnexpectedActionExit(int token, bool isBasicAction)
    {
        // Another state has taken ownership (guard, hurt, traversal...). Never
        // overwrite it with Idle when releasing this action's bookkeeping.
        RecoverAction(token, "Animator state replaced");
    }

    private bool StartBufferedAction(int token)
    {
        if (token != activeToken || !hasBufferedAction)
        {
            return false;
        }

        int stateHash = bufferedStateHash;
        PlayerActionPresentationProfile profile = bufferedProfile;
        string actionName = bufferedActionName;
        bool isBasicAction = bufferedActionIsBasic;
        BasicSkillsSO basicSkill = bufferedBasicSkill;
        hasBufferedAction = false;
        bufferedStateHash = 0;
        bufferedProfile = null;
        bufferedActionName = null;
        bufferedActionIsBasic = false;
        bufferedBasicSkill = null;
        return StartAction(stateHash, profile, actionName, isBasicAction, basicSkill);
    }

    private void FinishWithoutTransition(int token)
    {
        TerminateAction(token, ActionEndReason.Completed);
    }

    private void RequestLocomotionHandoff()
    {
        if (deathAnimationLocked)
        {
            return;
        }

        locomotionBridge?.RequestRunStartResponse();
        LocalPlayerInput.RequestHeldLocomotionReconciliation("Combat action ended");
    }

    private void RequestAirborneLandingIfDue()
    {
        if (!activeRequestsAirborneLanding || activeLandingRequested || locomotionBridge == null || locomotionBridge.Grounded)
        {
            return;
        }

        MotionHandoffProfile handoff = activeAirborneLandingHandoff ?? MotionHandoffProfile.CreateActionDefault();
        if (!locomotionBridge.ShouldBeginMotionHandoff(handoff))
        {
            return;
        }

        activeLandingRequested = locomotionBridge.RequestCombatSkillLanding();
    }

    private void ReleaseAirborneHold()
    {
        if (!activeHoldsAirborne)
        {
            return;
        }

        activeHoldsAirborne = false;
        locomotionBridge?.EndCombatAirborneHold();
    }

    private string ResolveLocomotionDestination(bool movementHeld, bool sprintHeld)
    {
        if (locomotionBridge != null && locomotionBridge.IsCombatLockActive)
        {
            return movementHeld ? CombatLocomotionState : CombatIdleState;
        }

        return !movementHeld
            ? LocomotionState
            : sprintHeld ? RunStartState : WalkStartState;
    }

    private string ResolveCurrentLocomotionDestination()
    {
        return ResolveLocomotionDestination(
            locomotionBridge != null && locomotionBridge.HasLocomotionIntent,
            locomotionBridge != null && locomotionBridge.IsSprintHeld);
    }

    private void KeepDeathAnimationActive()
    {
        if (animator == null || deathStateHash == 0)
        {
            return;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : default;
        if (current.fullPathHash == deathStateHash || next.fullPathHash == deathStateHash)
        {
            return;
        }

        animator.CrossFade(deathStateHash, 0.05f, 0, 0f);
    }

    private void Trace(string phase, string actionName, PlayerActionPresentationProfile profile)
    {
        if (!debugTransitions) return;
        Vector2 input = locomotionBridge != null ? locomotionBridge.CurrentWorldMoveInput : Vector2.zero;
        AnimatorStateInfo current = animator != null
            ? animator.GetCurrentAnimatorStateInfo(0)
            : default(AnimatorStateInfo);
        AnimatorStateInfo next = animator != null && animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : default(AnimatorStateInfo);
        string rootPhase = locomotionBridge != null ? locomotionBridge.CurrentAnimationPhase : "None";
        Debug.Log(
            $"[PlayerAction] {phase} action='{actionName}' current={current.fullPathHash}/{current.normalizedTime:F2} " +
            $"next={next.fullPathHash}/{next.normalizedTime:F2} input={input:F2} movement={profile.movementPolicy} " +
            $"animationPhase={rootPhase}",
            this);
    }
}
