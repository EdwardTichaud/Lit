using UnityEngine;

public partial class SquadCharacterController
{
    private static readonly int SitDownStateHash = Animator.StringToHash("Sit_Down");
    private static readonly int SittingIdleStateHash = Animator.StringToHash("Sitting_Idle");
    private static readonly int SittingIdleFullPathHash = Animator.StringToHash("Base Layer.Sitting_Idle");
    private static readonly int StandUpStateHash = Animator.StringToHash("Stand_Up");
    private static readonly int StandUpFullPathHash = Animator.StringToHash("Base Layer.Stand_Up");

    [Header("Idle Sitting")]
    [SerializeField, Tooltip("Assied automatiquement le personnage local apres une periode sans input.")]
    private bool enableIdleSitting = true;
    [SerializeField, Min(0f), Tooltip("Duree sans mouvement ni camera avant l'assise automatique.")]
    private float idleSecondsBeforeSitting = 10f;
    [SerializeField, Tooltip("Nom du bool qui pilote le cycle assis/debout dans l'Animator.")]
    private string sittingParam = "IsSitting";
    [SerializeField, Min(0.1f), Tooltip("Secours pour rendre le mouvement si l'etat Stand_Up ne peut pas etre observe.")]
    private float standUpMovementUnlockFallback = 1.5f;

    private bool sittingRequested;
    private bool holdSittingUntilStandUp;
    private bool sittingMovementSuppressionActive;
    private bool waitingForStandUpCompletion;
    private bool observedStandUpState;
    private bool standUpQueued;
    private float standUpRequestedAt;
    private float sittingIdleTrackingStartedAt;
    private uint sittingMovementActivityVersion;

    public bool IsSittingRequested => sittingRequested;
    public bool IsInSittingCycle => sittingRequested || sittingMovementSuppressionActive;

    public bool CanToggleSitting => IsInSittingCycle || CanEnterSitting();

    public bool TryToggleSitting()
    {
        return TrySetSitting(!IsInSittingCycle);
    }

    public bool TrySetSitting(bool shouldSit)
    {
        if (shouldSit)
        {
            if (sittingRequested)
            {
                return true;
            }

            if (!CanEnterSitting())
            {
                return false;
            }

            Stop();
            if (!sittingMovementSuppressionActive)
            {
                PushScriptedMovementSuppression();
                sittingMovementSuppressionActive = true;
            }

            sittingRequested = true;
            waitingForStandUpCompletion = false;
            observedStandUpState = false;
            standUpQueued = false;
            sittingMovementActivityVersion = LocalInputRouter.CharacterMovementActivityVersion;
            SetAnimatorBoolIfValid(sittingParam, true);
            return true;
        }

        holdSittingUntilStandUp = false;
        if (!IsInSittingCycle)
        {
            return true;
        }

        if (waitingForStandUpCompletion)
        {
            return true;
        }

        sittingRequested = false;
        sittingMovementActivityVersion = LocalInputRouter.CharacterMovementActivityVersion;
        if (!sittingMovementSuppressionActive ||
            !HasAnimatorParameter(sittingParam, AnimatorControllerParameterType.Bool))
        {
            SetAnimatorBoolIfValid(sittingParam, false);
            ReleaseSittingMovementSuppression();
            return true;
        }

        // Keep the sitting transition running until its blend into the seated pose is complete.
        standUpQueued = animator.HasState(0, SittingIdleFullPathHash) &&
            animator.HasState(0, StandUpFullPathHash) &&
            (animator.IsInTransition(0) || !animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Sitting_Idle"));
        if (!standUpQueued)
        {
            SetAnimatorBoolIfValid(sittingParam, false);
        }

        waitingForStandUpCompletion = true;
        observedStandUpState = false;
        standUpRequestedAt = Time.time;
        return true;
    }

    public bool TrySetSittingImmediate(float normalizedTime = 0f, bool holdUntilStandUp = false)
    {
        if (sittingRequested)
        {
            holdSittingUntilStandUp |= holdUntilStandUp;
            if (animator != null && animator.HasState(0, SittingIdleFullPathHash))
            {
                animator.Play(SittingIdleFullPathHash, 0, Mathf.Repeat(normalizedTime, 1f));
                animator.Update(0f);
            }

            return true;
        }

        if (GetScriptedSittingBlockReason() != null)
        {
            return false;
        }

        Stop();
        if (!sittingMovementSuppressionActive)
        {
            PushScriptedMovementSuppression();
            sittingMovementSuppressionActive = true;
        }

        sittingRequested = true;
        holdSittingUntilStandUp = holdUntilStandUp;
        waitingForStandUpCompletion = false;
        observedStandUpState = false;
        standUpQueued = false;
        sittingMovementActivityVersion = LocalInputRouter.CharacterMovementActivityVersion;
        SetAnimatorBoolIfValid(sittingParam, true);
        animator.Play(SittingIdleFullPathHash, 0, Mathf.Repeat(normalizedTime, 1f));
        animator.Update(0f);
        return true;
    }

    private void InitializeSittingState()
    {
        sittingRequested = false;
        holdSittingUntilStandUp = false;
        waitingForStandUpCompletion = false;
        observedStandUpState = false;
        standUpQueued = false;
        sittingIdleTrackingStartedAt = Time.unscaledTime;
        sittingMovementActivityVersion = LocalInputRouter.CharacterMovementActivityVersion;
        SetAnimatorBoolIfValid(sittingParam, false);
    }

    private void ResetSittingIdleTimer()
    {
        sittingIdleTrackingStartedAt = Time.unscaledTime;
        sittingMovementActivityVersion = LocalInputRouter.CharacterMovementActivityVersion;
    }

    private void UpdateSittingState()
    {
        if (waitingForStandUpCompletion)
        {
            UpdateStandUpCompletion();
        }

        if (!IsLocalControlledCharacter())
        {
            ResetSittingIdleTimer();
            return;
        }

        if (sittingRequested)
        {
            if (!holdSittingUntilStandUp && LocalInputRouter.CharacterMovementActivityVersion != sittingMovementActivityVersion)
            {
                TrySetSitting(false);
            }

            return;
        }

        if (!enableIdleSitting ||
            sittingMovementSuppressionActive ||
            InputFocusStack.HasAnyFocus() ||
            !CanEnterSitting())
        {
            ResetSittingIdleTimer();
            return;
        }

        float lastActivity = Mathf.Max(
            sittingIdleTrackingStartedAt,
            LocalInputRouter.LastGameplayActivityTime);
        if (Time.unscaledTime - lastActivity >= idleSecondsBeforeSitting)
        {
            TrySetSitting(true);
        }
    }

    private bool CanEnterSitting()
    {
        if (!isActiveAndEnabled ||
            animator == null ||
            currentHp <= 0 ||
            IsFlightActive ||
            !HasAnimatorParameter(sittingParam, AnimatorControllerParameterType.Bool))
        {
            return false;
        }

        if (IsExternalLocomotionDriverActive && !IsUccLocomotionActive)
        {
            return false;
        }

        LitOpsiveLocomotionBridge bridge = GetUccLocomotionBridge();
        if (bridge != null && bridge.IsInputSuppressedByUcc)
        {
            return false;
        }

        if (LocalInputRouter.MoveValue.sqrMagnitude > movementInputDeadZone * movementInputDeadZone)
        {
            return false;
        }

        return !IsUccLocomotionActive || IsGrounded;
    }

    internal string GetScriptedSittingBlockReason()
    {
        if (!isActiveAndEnabled) return "personnage inactif";
        if (animator == null || !animator.isActiveAndEnabled) return "Animator inactif";
        if (currentHp <= 0 || IsFlightActive) return "personnage KO ou en vol";
        if (!HasAnimatorParameter(sittingParam, AnimatorControllerParameterType.Bool)) return "parametre IsSitting absent";
        if (!animator.HasState(0, SittingIdleFullPathHash)) return "etat Sitting_Idle absent";
        // During a story sequence the UCC input handler is disabled, but its
        // physics body still owns ground contact. Do not use IsDriving here.
        var bridge = GetUccLocomotionBridge();
        bool hasUccBody = bridge != null && bridge.HasActiveLocomotionBody;
        if (IsExternalLocomotionDriverActive && !hasUccBody)
            return "locomotion externe active";
        return hasUccBody && !bridge.Grounded ? "le contact au sol UCC manque" : null;
    }

    private void UpdateStandUpCompletion()
    {
        if (!sittingMovementSuppressionActive)
        {
            waitingForStandUpCompletion = false;
            return;
        }

        if (animator == null || !animator.isActiveAndEnabled)
        {
            ReleaseSittingMovementSuppression();
            return;
        }

        bool transitioning = animator.IsInTransition(0);
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = transitioning
            ? animator.GetNextAnimatorStateInfo(0)
            : default;

        bool currentIsSitting = IsSittingState(current);
        bool nextIsSitting = transitioning && IsSittingState(next);
        if (standUpQueued)
        {
            bool seated = !transitioning && current.fullPathHash == SittingIdleFullPathHash;
            bool failedToEnter = !currentIsSitting && !nextIsSitting &&
                Time.time - standUpRequestedAt >= standUpMovementUnlockFallback;
            if (seated || failedToEnter)
            {
                standUpQueued = false;
                SetAnimatorBoolIfValid(sittingParam, false);
                if (failedToEnter)
                {
                    ReleaseSittingMovementSuppression();
                    return;
                }
                standUpRequestedAt = Time.time;
            }
            return;
        }

        observedStandUpState |= IsState(current, StandUpStateHash) || IsState(next, StandUpStateHash);
        if (observedStandUpState &&
            !transitioning &&
            !currentIsSitting &&
            !nextIsSitting)
        {
            ReleaseSittingMovementSuppression();
            return;
        }

        if (!observedStandUpState && Time.time - standUpRequestedAt >= standUpMovementUnlockFallback)
        {
            ReleaseSittingMovementSuppression();
        }
    }

    private static bool IsSittingState(AnimatorStateInfo state)
    {
        return IsState(state, SitDownStateHash) ||
               IsState(state, SittingIdleStateHash) ||
               IsState(state, StandUpStateHash);
    }

    private static bool IsState(AnimatorStateInfo state, int shortNameHash)
    {
        return state.shortNameHash == shortNameHash;
    }

    private void ReleaseSittingMovementSuppression()
    {
        waitingForStandUpCompletion = false;
        observedStandUpState = false;
        standUpQueued = false;
        if (sittingMovementSuppressionActive)
        {
            sittingMovementSuppressionActive = false;
            PopScriptedMovementSuppression();
        }

        ResetSittingIdleTimer();
    }

    private void CancelSittingState()
    {
        sittingRequested = false;
        holdSittingUntilStandUp = false;
        SetAnimatorBoolIfValid(sittingParam, false);
        ReleaseSittingMovementSuppression();
    }

    private void ValidateSittingSettings()
    {
        idleSecondsBeforeSitting = Mathf.Max(0f, idleSecondsBeforeSitting);
        standUpMovementUnlockFallback = Mathf.Max(0.1f, standUpMovementUnlockFallback);
    }
}
