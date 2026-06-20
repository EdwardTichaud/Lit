using UnityEngine;

public partial class SquadCharacterController
{
    private static readonly int SitDownStateHash = Animator.StringToHash("Sit_Down");
    private static readonly int SittingIdleStateHash = Animator.StringToHash("Sitting_Idle");
    private static readonly int SittingIdleFullPathHash = Animator.StringToHash("Base Layer.Sitting_Idle");
    private static readonly int StandUpStateHash = Animator.StringToHash("Stand_Up");

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
    private bool sittingMovementSuppressionActive;
    private bool waitingForStandUpCompletion;
    private bool observedStandUpState;
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
            sittingMovementActivityVersion = LocalInputRouter.CharacterMovementActivityVersion;
            SetAnimatorBoolIfValid(sittingParam, true);
            return true;
        }

        if (!IsInSittingCycle)
        {
            return true;
        }

        sittingRequested = false;
        sittingMovementActivityVersion = LocalInputRouter.CharacterMovementActivityVersion;
        SetAnimatorBoolIfValid(sittingParam, false);

        if (!sittingMovementSuppressionActive ||
            !HasAnimatorParameter(sittingParam, AnimatorControllerParameterType.Bool))
        {
            ReleaseSittingMovementSuppression();
            return true;
        }

        waitingForStandUpCompletion = true;
        observedStandUpState = false;
        standUpRequestedAt = Time.unscaledTime;
        return true;
    }

    public bool TrySetSittingImmediate(float normalizedTime = 0f)
    {
        if (sittingRequested)
        {
            if (animator != null && animator.HasState(0, SittingIdleFullPathHash))
            {
                animator.Play(SittingIdleFullPathHash, 0, Mathf.Repeat(normalizedTime, 1f));
                animator.Update(0f);
            }

            return true;
        }

        if (!CanEnterScriptedSittingImmediate())
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
        sittingMovementActivityVersion = LocalInputRouter.CharacterMovementActivityVersion;
        SetAnimatorBoolIfValid(sittingParam, true);
        animator.Play(SittingIdleFullPathHash, 0, Mathf.Repeat(normalizedTime, 1f));
        animator.Update(0f);
        return true;
    }

    private void InitializeSittingState()
    {
        sittingRequested = false;
        waitingForStandUpCompletion = false;
        observedStandUpState = false;
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
            if (LocalInputRouter.CharacterMovementActivityVersion != sittingMovementActivityVersion)
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

    private bool CanEnterScriptedSittingImmediate()
    {
        if (!isActiveAndEnabled ||
            animator == null ||
            !animator.isActiveAndEnabled ||
            currentHp <= 0 ||
            IsFlightActive ||
            !HasAnimatorParameter(sittingParam, AnimatorControllerParameterType.Bool) ||
            !animator.HasState(0, SittingIdleFullPathHash))
        {
            return false;
        }

        if (IsExternalLocomotionDriverActive && !IsUccLocomotionActive)
        {
            return false;
        }

        return !IsUccLocomotionActive || IsGrounded;
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

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : default;

        observedStandUpState |= IsState(current, StandUpStateHash) || IsState(next, StandUpStateHash);

        bool currentIsSitting = IsSittingState(current);
        bool nextIsSitting = animator.IsInTransition(0) && IsSittingState(next);
        if (observedStandUpState &&
            !animator.IsInTransition(0) &&
            !currentIsSitting &&
            !nextIsSitting)
        {
            ReleaseSittingMovementSuppression();
            return;
        }

        if (Time.unscaledTime - standUpRequestedAt >= standUpMovementUnlockFallback)
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
        SetAnimatorBoolIfValid(sittingParam, false);
        ReleaseSittingMovementSuppression();
    }

    private void ValidateSittingSettings()
    {
        idleSecondsBeforeSitting = Mathf.Max(0f, idleSecondsBeforeSitting);
        standUpMovementUnlockFallback = Mathf.Max(0.1f, standUpMovementUnlockFallback);
    }
}
