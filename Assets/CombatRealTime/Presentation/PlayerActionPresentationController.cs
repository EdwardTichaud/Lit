using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerActionPresentationController : MonoBehaviour
{
    private const string LocomotionState = "Base Layer.Locomotion";
    private const string CombatLocomotionState = "Base Layer.CombatLocomotion";
    private const string WalkStartState = "Base Layer.Walk_Start";
    private const string RunStartState = "Base Layer.Run_Start";

    [SerializeField] private Animator animator;
    [SerializeField] private LitOpsiveLocomotionBridge locomotionBridge;
    [SerializeField] private bool debugTransitions;

    private Coroutine actionRoutine;
    private int activeStateHash;
    private int activeToken;
    private PlayerActionRootMotionMode activeRootMotionMode;
    private PlayerActionFacingMode activeFacingMode;
    private bool actionActive;
    private bool chainWindowOpen;
    private bool mobilityCancelOpen;
    private bool recoveryOpen;
    private bool activeAllowsMobilityCancel;
    private bool hasBufferedAction;
    private int bufferedStateHash;
    private PlayerActionPresentationProfile bufferedProfile;
    private string bufferedActionName;
    private bool bufferedActionIsBasic;
    private Transform actionFacingTarget;
    private bool deathAnimationLocked;
    private int deathStateHash;

    public bool IsActionActive => actionActive;
    public bool IsChainWindowOpen => chainWindowOpen;
    public bool IsMobilityCancelOpen => mobilityCancelOpen;
    public bool IsRecoveryOpen => recoveryOpen;
    public bool CanStartAction => !deathAnimationLocked && (!actionActive || recoveryOpen);
    public bool CanAcceptBasicSkillInput => !deathAnimationLocked && !hasBufferedAction;
    public bool CanChainBasicSkill => !deathAnimationLocked && !hasBufferedAction && (!actionActive || recoveryOpen || chainWindowOpen);
    public bool CanCancelToMobility => !deathAnimationLocked &&
                                       (!actionActive || (activeAllowsMobilityCancel && (mobilityCancelOpen || recoveryOpen)));
    public bool IsDeathAnimationLocked => deathAnimationLocked;
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
        return skill != null && TryPlay(
            stateHash,
            skill.Presentation,
            skill.SkillName,
            skill is BasicSkillsSO);
    }

    public bool TryPlayCombatState(string stateName, PlayerActionPresentationProfile profile, string debugName)
    {
        if (string.IsNullOrWhiteSpace(stateName)) return false;
        return TryPlay(Animator.StringToHash(stateName), profile, debugName, false);
    }

    public IEnumerator WaitForChainWindow()
    {
        int token = activeToken;
        while (actionActive && token == activeToken && !CanChainBasicSkill)
        {
            yield return null;
        }
    }

    public void CancelAction()
    {
        bool hadAction = actionActive || hasBufferedAction;
        activeToken++;
        if (actionRoutine != null)
        {
            StopCoroutine(actionRoutine);
            actionRoutine = null;
        }

        actionActive = false;
        chainWindowOpen = false;
        mobilityCancelOpen = false;
        recoveryOpen = false;
        activeAllowsMobilityCancel = false;
        hasBufferedAction = false;
        bufferedStateHash = 0;
        bufferedProfile = null;
        bufferedActionName = null;
        bufferedActionIsBasic = false;
        locomotionBridge?.ClearPlayerActionRootMotionMode();
        if (hadAction)
        {
            ActionEnded?.Invoke();
            RequestLocomotionHandoff();
        }
    }

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
    public void ResumeLocomotionFromCinematic(bool movementHeld, bool sprintHeld, float transitionSeconds = 0.08f)
    {
        if (deathAnimationLocked || animator == null)
        {
            return;
        }

        CancelAction();
        locomotionBridge?.ClearPlayerActionRootMotionMode();
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
        CombatActorAnimationRoot animationContract = GetComponent<CombatActorAnimationRoot>();
        if (animationContract != null && animationContract.ValidateContract(out _))
        {
            animator = animationContract.Animator;
        }
        if (locomotionBridge == null) locomotionBridge = GetComponentInChildren<LitOpsiveLocomotionBridge>();
    }

    private void OnDisable()
    {
        CancelAction();
    }

    private void LateUpdate()
    {
        if (deathAnimationLocked)
        {
            KeepDeathAnimationActive();
            return;
        }

        if (actionActive)
        {
            FaceActionTarget();
        }
    }

    private bool TryPlay(int stateHash, PlayerActionPresentationProfile profile, string debugName, bool allowChainInterrupt)
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
            Trace("buffered", debugName, profile);
            return true;
        }

        if (actionActive && !recoveryOpen)
        {
            return false;
        }

        return StartAction(stateHash, profile, debugName, allowChainInterrupt);
    }

    private bool StartAction(int stateHash, PlayerActionPresentationProfile profile, string debugName, bool isBasicAction)
    {
        if (actionRoutine != null)
        {
            StopCoroutine(actionRoutine);
            actionRoutine = null;
        }

        activeToken++;
        activeStateHash = stateHash;
        activeRootMotionMode = profile.rootMotionMode;
        activeFacingMode = profile.facingMode;
        actionActive = true;
        chainWindowOpen = false;
        mobilityCancelOpen = false;
        recoveryOpen = false;
        activeAllowsMobilityCancel = profile.allowMobilityCancel;
        locomotionBridge?.SetPlayerActionRootMotionMode(
            profile.rootMotionMode,
            profile.facingMode == PlayerActionFacingMode.VisualOnly);
        animator.CrossFade(stateHash, Mathf.Clamp(profile.entryBlendSeconds, 0f, 0.25f), 0);
        Trace("enter", debugName, profile);
        actionRoutine = StartCoroutine(TrackAction(activeToken, profile, debugName, isBasicAction));
        return true;
    }

    private IEnumerator TrackAction(int token, PlayerActionPresentationProfile profile, string debugName, bool isBasicAction)
    {
        bool enteredState = false;
        float elapsed = 0f;
        const float stateEntryTimeout = 0.35f;
        float chainTime = Mathf.Clamp01(profile.chainNormalizedTime);
        float recoveryTime = Mathf.Max(chainTime, Mathf.Clamp01(profile.recoveryNormalizedTime));
        float chainTransitionTime = Mathf.Clamp(profile.chainTransitionNormalizedTime, chainTime, recoveryTime);
        float mobilityCancelTime = Mathf.Clamp(profile.mobilityCancelNormalizedTime, 0.05f, recoveryTime);

        while (token == activeToken && elapsed < stateEntryTimeout)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (IsActiveState(state))
            {
                enteredState = true;
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!enteredState || token != activeToken)
        {
            FinishUnexpectedActionExit(token, isBasicAction);
            yield break;
        }

        while (token == activeToken)
        {
            FaceActionTarget();
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (!IsActiveState(state))
            {
                FinishUnexpectedActionExit(token, isBasicAction);
                yield break;
            }

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

            if (state.normalizedTime >= recoveryTime)
            {
                recoveryOpen = true;
                Trace("recovery", debugName, profile);
                if (StartBufferedAction(token))
                {
                    yield break;
                }

                if (profile.allowMoveAfterRecovery)
                {
                    yield return new WaitForEndOfFrame();
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

        animator.CrossFade(ResolveLocomotionDestination(false, false), Mathf.Clamp(profile.exitBlendSeconds, 0f, 0.25f), 0);
        FinishWithoutTransition(token);
    }

    private void FinishUnexpectedActionExit(int token, bool isBasicAction)
    {
        if (!isBasicAction || deathAnimationLocked || token != activeToken || animator == null)
        {
            FinishWithoutTransition(token);
            return;
        }

        locomotionBridge?.RefreshLocomotionPresentation();
        animator.CrossFade(ResolveLocomotionDestination(false, false), 0.08f, 0);
        FinishWithoutTransition(token);
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
        hasBufferedAction = false;
        bufferedStateHash = 0;
        bufferedProfile = null;
        bufferedActionName = null;
        bufferedActionIsBasic = false;
        return StartAction(stateHash, profile, actionName, isBasicAction);
    }

    private void FinishWithoutTransition(int token)
    {
        if (token != activeToken) return;
        actionActive = false;
        chainWindowOpen = false;
        mobilityCancelOpen = false;
        recoveryOpen = false;
        activeAllowsMobilityCancel = false;
        hasBufferedAction = false;
        bufferedStateHash = 0;
        bufferedProfile = null;
        bufferedActionName = null;
        bufferedActionIsBasic = false;
        actionRoutine = null;
        locomotionBridge?.ClearPlayerActionRootMotionMode();
        ActionEnded?.Invoke();
        RequestLocomotionHandoff();
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

    private string ResolveLocomotionDestination(bool movementHeld, bool sprintHeld)
    {
        if (locomotionBridge != null && locomotionBridge.IsCombatLockActive)
        {
            return CombatLocomotionState;
        }

        return !movementHeld
            ? LocomotionState
            : sprintHeld ? RunStartState : WalkStartState;
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
        Vector3 delta = animator != null ? animator.deltaPosition : Vector3.zero;
        string rootPhase = locomotionBridge != null ? locomotionBridge.CurrentRootMotionPhase : "None";
        Debug.Log(
            $"[PlayerAction] {phase} action='{actionName}' current={current.fullPathHash}/{current.normalizedTime:F2} " +
            $"next={next.fullPathHash}/{next.normalizedTime:F2} input={input:F2} rootMode={profile.rootMotionMode} " +
            $"rootPhase={rootPhase} delta={delta:F4}",
            this);
    }
}
