using System.Collections.Generic;
using UnityEngine;

public partial class SquadCharacterController
{
    private const string TurnLeftStateName = "Turn_L90";
    private const string TurnRightStateName = "Turn_R90";
    private const string WalkStopStateName = "Walk_Stop";
    private const string JogtrotStopStateName = "Jogtrot_Stop";
    private const string RunStopStateName = "Run_Stop";
    private const string HeavyLandingStateName = "Landing_Hard";

    private enum ExpectedAnimationState
    {
        None = 0,
        GroundedIdle = 1,
        GroundedMoving = 2,
        JumpTakeoff = 3,
        Airborne = 4,
        Falling = 5,
        Landing = 6,
        LandingRoll = 7,
    }

    private enum ObservedAnimationStateCategory
    {
        Unknown = 0,
        GroundedLoop = 1,
        GroundedStart = 2,
        GroundedStop = 3,
        TurnInPlace = 4,
        JumpTakeoff = 5,
        Airborne = 6,
        Falling = 7,
        Landing = 8,
        LandingRoll = 9,
        Priority = 10,
    }

    private readonly struct AnimationGameplaySnapshot
    {
        public AnimationGameplaySnapshot(
            Vector3 velocity,
            Vector3 desiredDirection,
            Vector3 facingDirection,
            float presentationSpeed,
            float animatorSpeed,
            float locomotionTier,
            float signedTurn,
            bool isMoving,
            bool shouldTurnInPlace)
        {
            Velocity = velocity;
            DesiredDirection = desiredDirection;
            FacingDirection = facingDirection;
            PresentationSpeed = presentationSpeed;
            AnimatorSpeed = animatorSpeed;
            LocomotionTier = locomotionTier;
            SignedTurn = signedTurn;
            IsMoving = isMoving;
            ShouldTurnInPlace = shouldTurnInPlace;
        }

        public Vector3 Velocity { get; }
        public Vector3 DesiredDirection { get; }
        public Vector3 FacingDirection { get; }
        public float PresentationSpeed { get; }
        public float AnimatorSpeed { get; }
        public float LocomotionTier { get; }
        public float SignedTurn { get; }
        public bool IsMoving { get; }
        public bool ShouldTurnInPlace { get; }
    }

    private readonly struct ObservedAnimatorStateSnapshot
    {
        public ObservedAnimatorStateSnapshot(
            ObservedAnimationStateCategory currentCategory,
            ObservedAnimationStateCategory nextCategory,
            int currentStateHash,
            int nextStateHash,
            string currentStateLabel,
            string nextStateLabel,
            float currentNormalizedTime,
            float nextNormalizedTime,
            float currentLengthSeconds,
            float nextLengthSeconds,
            float currentObservedSeconds,
            float nextObservedSeconds,
            bool inTransition)
        {
            CurrentCategory = currentCategory;
            NextCategory = nextCategory;
            CurrentStateHash = currentStateHash;
            NextStateHash = nextStateHash;
            CurrentStateLabel = currentStateLabel;
            NextStateLabel = nextStateLabel;
            CurrentNormalizedTime = currentNormalizedTime;
            NextNormalizedTime = nextNormalizedTime;
            CurrentLengthSeconds = currentLengthSeconds;
            NextLengthSeconds = nextLengthSeconds;
            CurrentObservedSeconds = currentObservedSeconds;
            NextObservedSeconds = nextObservedSeconds;
            InTransition = inTransition;
        }

        public ObservedAnimationStateCategory CurrentCategory { get; }
        public ObservedAnimationStateCategory NextCategory { get; }
        public int CurrentStateHash { get; }
        public int NextStateHash { get; }
        public string CurrentStateLabel { get; }
        public string NextStateLabel { get; }
        public float CurrentNormalizedTime { get; }
        public float NextNormalizedTime { get; }
        public float CurrentLengthSeconds { get; }
        public float NextLengthSeconds { get; }
        public float CurrentObservedSeconds { get; }
        public float NextObservedSeconds { get; }
        public bool InTransition { get; }

        public bool Matches(ObservedAnimationStateCategory category)
        {
            return CurrentCategory == category ||
                   (InTransition && NextCategory == category);
        }
    }

    private static readonly int TurnLeftStateHash = Animator.StringToHash(TurnLeftStateName);
    private static readonly int TurnRightStateHash = Animator.StringToHash(TurnRightStateName);
    private static readonly int KnowledgeUnlockStateHash = Animator.StringToHash("Knowledge_Unlock");
    private static readonly int HeavyLandingStateHash = Animator.StringToHash(HeavyLandingStateName);
    private static readonly int LadderTagHash = Animator.StringToHash("Ladder");

    [Header("Animation Reconciliation")]
    [SerializeField, Tooltip("Compare l'etat Animator courant a l'etat gameplay reel et corrige les desynchronisations durables.")]
    private bool enableAnimationReconciliation = true;
    [SerializeField, Tooltip("Duree de grace avant de forcer un retour vers la locomotion correcte.")]
    private float locomotionAnimationCorrectionDelay = 0.18f;
    [SerializeField, Tooltip("Duree de grace avant de corriger un state airborne incoherent.")]
    private float airborneAnimationCorrectionDelay = 0.12f;
    [SerializeField, Tooltip("Duree de grace avant de corriger un state de landing incoherent.")]
    private float landingAnimationCorrectionDelay = 0.12f;
    [SerializeField, Tooltip("Duree de grace courte appliquee aux Start/Stop/Turn quand le gameplay a deja change.")]
    private float transientAnimationCorrectionDelay = 0.05f;
    [SerializeField, Tooltip("Cooldown applique aux corrections successives vers le meme state pour eviter les CrossFade repetes.")]
    private float repeatedAnimationCorrectionCooldown = 0.12f;
    [SerializeField, Tooltip("CrossFade utilise quand le watchdog force un retour vers le state attendu.")]
    private float animationReconciliationCrossFadeDuration = 0.05f;
    [SerializeField, Tooltip("Marge ajoutee au timeout d'un one-shot quand sa fenetre Animator n'est jamais atteinte.")]
    private float animationPhaseTimeoutPadding = 0.08f;
    [SerializeField, Tooltip("States qui bloquent la reconciliation tant qu'ils sont joues sur l'Animator.")]
    private string[] priorityAnimationStateNames = { "Knowledge_Unlock", "Attack", "Hurt", "Hit", "Stun", "Dead", "Death", "Cutscene", "Scripted" };
    [SerializeField, Tooltip("Tags Animator qui bloquent la reconciliation tant qu'ils sont actifs.")]
    private string[] priorityAnimationTags = { "Ladder", "Attack", "Hit", "Stun", "Death", "Cutscene", "Scripted" };
    [SerializeField, Tooltip("Active les logs quand le watchdog corrige automatiquement un state incoherent.")]
    private bool logAnimationReconciliation;

    private readonly HashSet<string> missingAnimatorStateWarnings = new HashSet<string>();
    private ExpectedAnimationState activeAnimationMismatchState;
    private float animationMismatchTimer;
    private int observedCurrentAnimatorStateHash;
    private float observedCurrentAnimatorStateDuration;
    private int observedNextAnimatorStateHash;
    private float observedNextAnimatorStateDuration;
    private int lastWatchdogCrossFadeStateHash;
    private int lastWatchdogCrossFadeLayer = -1;
    private float lastWatchdogCrossFadeTime = float.NegativeInfinity;

    private AnimationGameplaySnapshot CreateAnimationGameplaySnapshot()
    {
        Vector3 velocity = GetCurrentHorizontalVelocity();
        float presentationSpeed = ResolveAnimationPresentationSpeed(velocity);
        float animatorSpeed = ResolveAnimatorSpeedValue(presentationSpeed);
        Vector3 desiredDirection = ResolveAnimatorDesiredDirection(velocity);
        Vector3 facingDirection = GetFacingPlanarForward();
        float signedTurn = 0f;

        if (desiredDirection.sqrMagnitude > 0.0001f)
        {
            signedTurn = Mathf.Clamp(
                Vector3.SignedAngle(facingDirection, desiredDirection, transform.up) / 90f,
                -1f,
                1f);
        }

        bool isMovingNow = presentationSpeed >= animationMovingEnterSpeed
            ? true
            : presentationSpeed <= animationMovingExitSpeed
                ? false
                : wasMovingForAnimator;

        float locomotionTier = isMovingNow
            ? ResolveLocomotionTier(presentationSpeed)
            : lastMovingLocomotionTier;

        bool shouldTurnInPlace = !isMovingNow &&
                                 !IsJumpCommitted &&
                                 desiredDirection.sqrMagnitude > 0.0001f &&
                                 Mathf.Abs(Vector3.SignedAngle(facingDirection, desiredDirection, transform.up)) >= turnInPlaceAngleThreshold;

        return new AnimationGameplaySnapshot(
            velocity,
            desiredDirection,
            facingDirection,
            presentationSpeed,
            animatorSpeed,
            locomotionTier,
            signedTurn,
            isMovingNow,
            shouldTurnInPlace);
    }

    private bool ShouldRunAnimationReconciliationInFixedUpdate()
    {
        return animator != null && animator.updateMode == AnimatorUpdateMode.Fixed;
    }

    private bool ShouldRunAnimationReconciliationInLateUpdate()
    {
        return animator != null && animator.updateMode != AnimatorUpdateMode.Fixed;
    }

    private void ValidateAnimationReconciliationMappings()
    {
        if (!enableAnimationReconciliation ||
            animator == null ||
            animator.runtimeAnimatorController == null)
        {
            return;
        }

        ValidateAnimatorStateReference(locomotionAnimationLayer, groundedRecoveryStateName);
        ValidateAnimatorStateReference(locomotionAnimationLayer, WalkStartStateName);
        ValidateAnimatorStateReference(locomotionAnimationLayer, JogtrotStartStateName);
        ValidateAnimatorStateReference(locomotionAnimationLayer, RunStartStateName);
        ValidateAnimatorStateReference(locomotionAnimationLayer, WalkStopStateName);
        ValidateAnimatorStateReference(locomotionAnimationLayer, JogtrotStopStateName);
        ValidateAnimatorStateReference(locomotionAnimationLayer, RunStopStateName);
        ValidateAnimatorStateReference(locomotionAnimationLayer, TurnLeftStateName);
        ValidateAnimatorStateReference(locomotionAnimationLayer, TurnRightStateName);

        ValidateAnimatorStateReference(jumpAnimationLayer, takeoffStateName);
        ValidateAnimatorStateReference(jumpAnimationLayer, airborneStateName);
        ValidateAnimatorStateReference(jumpAnimationLayer, fallingStateName);
        ValidateAnimatorStateReference(jumpAnimationLayer, idleLandingStateName);
        ValidateAnimatorStateReference(jumpAnimationLayer, naturalLandingStateName);
        ValidateAnimatorStateReference(jumpAnimationLayer, rollStateName);
        ValidateAnimatorStateReference(jumpAnimationLayer, HeavyLandingStateName);
    }

    private void ReconcileAnimationState(float deltaTime)
    {
        // The gameplay state stays authoritative. Transition clips may play briefly,
        // but if the Animator drifts away from the real state we realign it.
        if (!enableAnimationReconciliation ||
            animator == null ||
            !animator.isActiveAndEnabled ||
            locomotionAnimationLayer < 0 ||
            locomotionAnimationLayer >= animator.layerCount)
        {
            ResetAnimationReconciliationState();
            return;
        }

        if (currentHp <= 0 || scriptedMovementSuppressionCount > 0 || IsExternalLocomotionDriverActive)
        {
            ResetAnimationReconciliationState();
            return;
        }

        AnimationGameplaySnapshot snapshot = CreateAnimationGameplaySnapshot();
        ExpectedAnimationState expectedState = ResolveExpectedAnimationState(snapshot);
        if (expectedState == ExpectedAnimationState.None)
        {
            ResetAnimationReconciliationState();
            return;
        }

        if (IsPriorityAnimationActiveAcrossLayers())
        {
            ResetAnimationReconciliationState();
            return;
        }

        ObservedAnimatorStateSnapshot observed = ObserveCurrentAnimationState(deltaTime);
        if (IsPriorityAnimationStillAuthoritative(observed) ||
            IsAnimationStateCompatible(expectedState, snapshot, observed))
        {
            ResetAnimationReconciliationState();
            return;
        }

        float correctionDelay = ResolveAnimationCorrectionDelay(expectedState, snapshot, observed);
        if (activeAnimationMismatchState != expectedState)
        {
            activeAnimationMismatchState = expectedState;
            animationMismatchTimer = 0f;
        }

        animationMismatchTimer += Mathf.Max(0f, deltaTime);
        if (animationMismatchTimer < correctionDelay)
        {
            return;
        }

        string reason = ResolveAnimationCorrectionReason(expectedState, snapshot, observed);
        if (ApplyAnimationCorrection(expectedState, snapshot, out string targetStateName))
        {
            LogAnimationReconciliationCorrection(
                expectedState,
                observed,
                snapshot,
                reason,
                targetStateName,
                animationMismatchTimer);
        }

        ResetAnimationReconciliationState();
    }

    private ExpectedAnimationState ResolveExpectedAnimationState(AnimationGameplaySnapshot snapshot)
    {
        switch (committedJumpPhase)
        {
            case CommittedJumpPhase.Takeoff:
                return ExpectedAnimationState.JumpTakeoff;
            case CommittedJumpPhase.Airborne:
                return ExpectedAnimationState.Airborne;
            case CommittedJumpPhase.LandingRecovery:
                return ExpectedAnimationState.Landing;
            case CommittedJumpPhase.LandingRoll:
                return ExpectedAnimationState.LandingRoll;
        }

        switch (naturalFallAnimationPhase)
        {
            case NaturalFallAnimationPhase.Falling:
                return ExpectedAnimationState.Falling;
            case NaturalFallAnimationPhase.Landing:
                return ExpectedAnimationState.Landing;
        }

        return snapshot.IsMoving
            ? ExpectedAnimationState.GroundedMoving
            : ExpectedAnimationState.GroundedIdle;
    }

    private ObservedAnimatorStateSnapshot ObserveCurrentAnimationState(float deltaTime)
    {
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(locomotionAnimationLayer);
        bool inTransition = animator.IsInTransition(locomotionAnimationLayer);
        AnimatorStateInfo nextState = inTransition
            ? animator.GetNextAnimatorStateInfo(locomotionAnimationLayer)
            : default;

        ObservedAnimationStateCategory currentCategory = ClassifyAnimationState(currentState);
        ObservedAnimationStateCategory nextCategory = inTransition
            ? ClassifyAnimationState(nextState)
            : ObservedAnimationStateCategory.Unknown;

        string currentLabel = ResolveAnimatorStateLabel(currentState, currentCategory);
        string nextLabel = inTransition
            ? ResolveAnimatorStateLabel(nextState, nextCategory)
            : "None";

        int currentStateHash = ResolveAnimatorStateIdentityHash(currentState);
        int nextStateHash = inTransition
            ? ResolveAnimatorStateIdentityHash(nextState)
            : 0;

        float currentObservedSeconds = TrackObservedAnimatorStateDuration(
            ref observedCurrentAnimatorStateHash,
            ref observedCurrentAnimatorStateDuration,
            currentStateHash,
            deltaTime);

        float nextObservedSeconds = 0f;
        if (inTransition)
        {
            nextObservedSeconds = TrackObservedAnimatorStateDuration(
                ref observedNextAnimatorStateHash,
                ref observedNextAnimatorStateDuration,
                nextStateHash,
                deltaTime);
        }
        else
        {
            observedNextAnimatorStateHash = 0;
            observedNextAnimatorStateDuration = 0f;
        }

        return new ObservedAnimatorStateSnapshot(
            currentCategory,
            nextCategory,
            currentStateHash,
            nextStateHash,
            currentLabel,
            nextLabel,
            Mathf.Max(0f, currentState.normalizedTime),
            Mathf.Max(0f, nextState.normalizedTime),
            ResolveAnimatorStateLengthSeconds(currentState, currentLabel),
            ResolveAnimatorStateLengthSeconds(nextState, nextLabel),
            currentObservedSeconds,
            nextObservedSeconds,
            inTransition);
    }

    private ObservedAnimationStateCategory ClassifyAnimationState(AnimatorStateInfo stateInfo)
    {
        if (stateInfo.shortNameHash == 0 && stateInfo.fullPathHash == 0)
        {
            return ObservedAnimationStateCategory.Unknown;
        }

        if (MatchesPriorityAnimationState(stateInfo))
        {
            return ObservedAnimationStateCategory.Priority;
        }

        if (AnimatorStateMatches(stateInfo, groundedRecoveryStateName))
        {
            return ObservedAnimationStateCategory.GroundedLoop;
        }

        if (AnimatorStateMatches(stateInfo, WalkStartStateName) ||
            AnimatorStateMatches(stateInfo, JogtrotStartStateName) ||
            AnimatorStateMatches(stateInfo, RunStartStateName))
        {
            return ObservedAnimationStateCategory.GroundedStart;
        }

        if (MatchesLocomotionEndAnimationState(stateInfo))
        {
            return ObservedAnimationStateCategory.GroundedStop;
        }

        if (stateInfo.shortNameHash == TurnLeftStateHash || stateInfo.shortNameHash == TurnRightStateHash)
        {
            return ObservedAnimationStateCategory.TurnInPlace;
        }

        if (AnimatorStateMatches(stateInfo, takeoffStateName))
        {
            return ObservedAnimationStateCategory.JumpTakeoff;
        }

        if (AnimatorStateMatches(stateInfo, airborneStateName))
        {
            return ObservedAnimationStateCategory.Airborne;
        }

        if (AnimatorStateMatches(stateInfo, fallingStateName))
        {
            return ObservedAnimationStateCategory.Falling;
        }

        if (AnimatorStateMatches(stateInfo, rollStateName))
        {
            return ObservedAnimationStateCategory.LandingRoll;
        }

        if (AnimatorStateMatches(stateInfo, idleLandingStateName) ||
            AnimatorStateMatches(stateInfo, naturalLandingStateName) ||
            stateInfo.shortNameHash == HeavyLandingStateHash)
        {
            return ObservedAnimationStateCategory.Landing;
        }

        return ObservedAnimationStateCategory.Unknown;
    }

    private string ResolveAnimatorStateLabel(
        AnimatorStateInfo stateInfo,
        ObservedAnimationStateCategory category)
    {
        if (stateInfo.shortNameHash == 0 && stateInfo.fullPathHash == 0)
        {
            return "None";
        }

        switch (category)
        {
            case ObservedAnimationStateCategory.GroundedLoop:
                return groundedRecoveryStateName;

            case ObservedAnimationStateCategory.GroundedStart:
                if (AnimatorStateMatches(stateInfo, WalkStartStateName))
                {
                    return WalkStartStateName;
                }

                if (AnimatorStateMatches(stateInfo, JogtrotStartStateName))
                {
                    return JogtrotStartStateName;
                }

                return RunStartStateName;

            case ObservedAnimationStateCategory.GroundedStop:
                if (AnimatorStateMatches(stateInfo, WalkStopStateName))
                {
                    return WalkStopStateName;
                }

                if (AnimatorStateMatches(stateInfo, JogtrotStopStateName))
                {
                    return JogtrotStopStateName;
                }

                if (AnimatorStateMatches(stateInfo, RunStopStateName))
                {
                    return RunStopStateName;
                }

                return $"LocomotionStop(hash:{ResolveAnimatorStateIdentityHash(stateInfo)})";

            case ObservedAnimationStateCategory.TurnInPlace:
                return stateInfo.shortNameHash == TurnLeftStateHash ? TurnLeftStateName : TurnRightStateName;

            case ObservedAnimationStateCategory.JumpTakeoff:
                return takeoffStateName;

            case ObservedAnimationStateCategory.Airborne:
                return airborneStateName;

            case ObservedAnimationStateCategory.Falling:
                return fallingStateName;

            case ObservedAnimationStateCategory.Landing:
                if (AnimatorStateMatches(stateInfo, idleLandingStateName))
                {
                    return idleLandingStateName;
                }

                if (AnimatorStateMatches(stateInfo, naturalLandingStateName))
                {
                    return naturalLandingStateName;
                }

                return HeavyLandingStateName;

            case ObservedAnimationStateCategory.LandingRoll:
                return rollStateName;

            case ObservedAnimationStateCategory.Priority:
                if (TryResolvePriorityStateLabel(stateInfo, out string priorityLabel))
                {
                    return priorityLabel;
                }

                break;
        }

        return $"hash:{ResolveAnimatorStateIdentityHash(stateInfo)}";
    }

    private bool TryResolvePriorityStateLabel(AnimatorStateInfo stateInfo, out string label)
    {
        if (stateInfo.shortNameHash == KnowledgeUnlockStateHash)
        {
            label = "Knowledge_Unlock";
            return true;
        }

        if (stateInfo.tagHash == LadderTagHash)
        {
            label = "tag:Ladder";
            return true;
        }

        if (priorityAnimationTags != null)
        {
            for (int i = 0; i < priorityAnimationTags.Length; i++)
            {
                string tagName = priorityAnimationTags[i];
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    continue;
                }

                if (stateInfo.tagHash == Animator.StringToHash(tagName))
                {
                    label = $"tag:{tagName}";
                    return true;
                }
            }
        }

        if (priorityAnimationStateNames != null)
        {
            for (int i = 0; i < priorityAnimationStateNames.Length; i++)
            {
                string stateName = priorityAnimationStateNames[i];
                if (string.IsNullOrWhiteSpace(stateName))
                {
                    continue;
                }

                if (AnimatorStateMatches(stateInfo, stateName))
                {
                    label = stateName;
                    return true;
                }
            }
        }

        label = $"hash:{ResolveAnimatorStateIdentityHash(stateInfo)}";
        return false;
    }

    private bool MatchesPriorityAnimationState(AnimatorStateInfo stateInfo)
    {
        if (stateInfo.tagHash == LadderTagHash || stateInfo.shortNameHash == KnowledgeUnlockStateHash)
        {
            return true;
        }

        if (priorityAnimationTags != null)
        {
            for (int i = 0; i < priorityAnimationTags.Length; i++)
            {
                string tagName = priorityAnimationTags[i];
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    continue;
                }

                if (stateInfo.tagHash == Animator.StringToHash(tagName))
                {
                    return true;
                }
            }
        }

        if (priorityAnimationStateNames != null)
        {
            for (int i = 0; i < priorityAnimationStateNames.Length; i++)
            {
                string stateName = priorityAnimationStateNames[i];
                if (string.IsNullOrWhiteSpace(stateName))
                {
                    continue;
                }

                if (AnimatorStateMatches(stateInfo, stateName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsPriorityAnimationStillAuthoritative(ObservedAnimatorStateSnapshot observed)
    {
        return observed.CurrentCategory == ObservedAnimationStateCategory.Priority ||
               (observed.InTransition && observed.NextCategory == ObservedAnimationStateCategory.Priority);
    }

    private bool IsPriorityAnimationActiveAcrossLayers()
    {
        return IsPriorityAnimationActiveOnLayer(locomotionAnimationLayer) ||
               IsPriorityAnimationActiveOnLayer(jumpAnimationLayer);
    }

    private bool IsPriorityAnimationActiveOnLayer(int layerIndex)
    {
        if (animator == null || layerIndex < 0 || layerIndex >= animator.layerCount)
        {
            return false;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (MatchesPriorityAnimationState(currentState))
        {
            return true;
        }

        return animator.IsInTransition(layerIndex) &&
               MatchesPriorityAnimationState(animator.GetNextAnimatorStateInfo(layerIndex));
    }

    private bool IsAnimationStateCompatible(
        ExpectedAnimationState expectedState,
        AnimationGameplaySnapshot snapshot,
        ObservedAnimatorStateSnapshot observed)
    {
        switch (expectedState)
        {
            case ExpectedAnimationState.GroundedIdle:
                return observed.Matches(ObservedAnimationStateCategory.GroundedLoop) ||
                       IsTransientStateFresh(observed, ObservedAnimationStateCategory.GroundedStop) ||
                       (snapshot.ShouldTurnInPlace &&
                        IsTransientStateFresh(observed, ObservedAnimationStateCategory.TurnInPlace));

            case ExpectedAnimationState.GroundedMoving:
                return observed.Matches(ObservedAnimationStateCategory.GroundedLoop) ||
                       IsTransientStateFresh(observed, ObservedAnimationStateCategory.GroundedStart);

            case ExpectedAnimationState.JumpTakeoff:
                return observed.Matches(ObservedAnimationStateCategory.JumpTakeoff) ||
                       observed.Matches(ObservedAnimationStateCategory.Airborne);

            case ExpectedAnimationState.Airborne:
                return observed.Matches(ObservedAnimationStateCategory.Airborne) ||
                       observed.Matches(ObservedAnimationStateCategory.Falling) ||
                       observed.Matches(ObservedAnimationStateCategory.JumpTakeoff);

            case ExpectedAnimationState.Falling:
                return observed.Matches(ObservedAnimationStateCategory.Falling) ||
                       observed.Matches(ObservedAnimationStateCategory.Airborne);

            case ExpectedAnimationState.Landing:
                return observed.Matches(ObservedAnimationStateCategory.Landing);

            case ExpectedAnimationState.LandingRoll:
                return observed.Matches(ObservedAnimationStateCategory.LandingRoll);

            default:
                return false;
        }
    }

    private bool IsTransientStateFresh(
        ObservedAnimatorStateSnapshot observed,
        ObservedAnimationStateCategory category)
    {
        if (!TryGetObservedStateTiming(
                observed,
                category,
                out float normalizedTime,
                out float observedSeconds,
                out float stateLengthSeconds,
                out _))
        {
            return false;
        }

        if (normalizedTime >= 1f)
        {
            return false;
        }

        return observedSeconds <= ResolveObservedStateDurationBudget(
            stateLengthSeconds,
            transientAnimationCorrectionDelay);
    }

    private bool IsObservedCategoryExpired(
        ObservedAnimatorStateSnapshot observed,
        ObservedAnimationStateCategory category,
        float fallbackDuration)
    {
        if (!TryGetObservedStateTiming(
                observed,
                category,
                out _,
                out float observedSeconds,
                out float stateLengthSeconds,
                out _))
        {
            return false;
        }

        return observedSeconds > ResolveObservedStateDurationBudget(
            stateLengthSeconds,
            fallbackDuration);
    }

    private bool TryGetObservedStateTiming(
        ObservedAnimatorStateSnapshot observed,
        ObservedAnimationStateCategory category,
        out float normalizedTime,
        out float observedSeconds,
        out float stateLengthSeconds,
        out string stateLabel)
    {
        if (observed.CurrentCategory == category)
        {
            normalizedTime = observed.CurrentNormalizedTime;
            observedSeconds = observed.CurrentObservedSeconds;
            stateLengthSeconds = observed.CurrentLengthSeconds;
            stateLabel = observed.CurrentStateLabel;
            return true;
        }

        if (observed.InTransition && observed.NextCategory == category)
        {
            normalizedTime = observed.NextNormalizedTime;
            observedSeconds = observed.NextObservedSeconds;
            stateLengthSeconds = observed.NextLengthSeconds;
            stateLabel = observed.NextStateLabel;
            return true;
        }

        normalizedTime = 0f;
        observedSeconds = 0f;
        stateLengthSeconds = 0f;
        stateLabel = string.Empty;
        return false;
    }

    private float ResolveObservedStateDurationBudget(float stateLengthSeconds, float fallbackDuration)
    {
        float clipBudget = stateLengthSeconds > 0f
            ? stateLengthSeconds + Mathf.Max(0f, animationPhaseTimeoutPadding)
            : 0f;
        return Mathf.Max(Mathf.Max(0f, fallbackDuration), clipBudget);
    }

    private float ResolveAnimationCorrectionDelay(
        ExpectedAnimationState expectedState,
        AnimationGameplaySnapshot snapshot,
        ObservedAnimatorStateSnapshot observed)
    {
        switch (observed.CurrentCategory)
        {
            case ObservedAnimationStateCategory.GroundedStart:
                if (!snapshot.IsMoving ||
                    IsObservedCategoryExpired(observed, ObservedAnimationStateCategory.GroundedStart, transientAnimationCorrectionDelay))
                {
                    return transientAnimationCorrectionDelay;
                }

                break;

            case ObservedAnimationStateCategory.GroundedStop:
                if (snapshot.IsMoving ||
                    IsObservedCategoryExpired(observed, ObservedAnimationStateCategory.GroundedStop, transientAnimationCorrectionDelay))
                {
                    return transientAnimationCorrectionDelay;
                }

                break;

            case ObservedAnimationStateCategory.TurnInPlace:
                if (!snapshot.ShouldTurnInPlace ||
                    IsObservedCategoryExpired(observed, ObservedAnimationStateCategory.TurnInPlace, transientAnimationCorrectionDelay))
                {
                    return transientAnimationCorrectionDelay;
                }

                break;

            case ObservedAnimationStateCategory.Landing:
                if (IsObservedCategoryExpired(observed, ObservedAnimationStateCategory.Landing, landingAnimationCorrectionDelay))
                {
                    return transientAnimationCorrectionDelay;
                }

                break;

            case ObservedAnimationStateCategory.LandingRoll:
                if (IsObservedCategoryExpired(observed, ObservedAnimationStateCategory.LandingRoll, landingAnimationCorrectionDelay))
                {
                    return transientAnimationCorrectionDelay;
                }

                break;
        }

        switch (expectedState)
        {
            case ExpectedAnimationState.GroundedIdle:
            case ExpectedAnimationState.GroundedMoving:
                return locomotionAnimationCorrectionDelay;

            case ExpectedAnimationState.JumpTakeoff:
            case ExpectedAnimationState.Airborne:
            case ExpectedAnimationState.Falling:
                return airborneAnimationCorrectionDelay;

            case ExpectedAnimationState.Landing:
            case ExpectedAnimationState.LandingRoll:
                return landingAnimationCorrectionDelay;

            default:
                return locomotionAnimationCorrectionDelay;
        }
    }

    private string ResolveAnimationCorrectionReason(
        ExpectedAnimationState expectedState,
        AnimationGameplaySnapshot snapshot,
        ObservedAnimatorStateSnapshot observed)
    {
        switch (observed.CurrentCategory)
        {
            case ObservedAnimationStateCategory.GroundedStart:
                if (!snapshot.IsMoving)
                {
                    return $"start state '{observed.CurrentStateLabel}' remained active after movement stopped";
                }

                if (IsObservedCategoryExpired(observed, ObservedAnimationStateCategory.GroundedStart, transientAnimationCorrectionDelay))
                {
                    return $"start state '{observed.CurrentStateLabel}' exceeded its clip budget";
                }

                break;

            case ObservedAnimationStateCategory.GroundedStop:
                if (snapshot.IsMoving)
                {
                    return $"stop state '{observed.CurrentStateLabel}' remained active after movement resumed";
                }

                if (IsObservedCategoryExpired(observed, ObservedAnimationStateCategory.GroundedStop, transientAnimationCorrectionDelay))
                {
                    return $"stop state '{observed.CurrentStateLabel}' exceeded its clip budget";
                }

                break;

            case ObservedAnimationStateCategory.TurnInPlace:
                if (!snapshot.ShouldTurnInPlace)
                {
                    return $"turn-in-place state '{observed.CurrentStateLabel}' remained active after turn request cleared";
                }

                if (IsObservedCategoryExpired(observed, ObservedAnimationStateCategory.TurnInPlace, transientAnimationCorrectionDelay))
                {
                    return $"turn-in-place state '{observed.CurrentStateLabel}' exceeded its clip budget";
                }

                break;

            case ObservedAnimationStateCategory.Landing:
                if (IsObservedCategoryExpired(observed, ObservedAnimationStateCategory.Landing, landingAnimationCorrectionDelay))
                {
                    return $"landing state '{observed.CurrentStateLabel}' exceeded its clip budget";
                }

                break;

            case ObservedAnimationStateCategory.LandingRoll:
                if (IsObservedCategoryExpired(observed, ObservedAnimationStateCategory.LandingRoll, landingAnimationCorrectionDelay))
                {
                    return $"landing-roll state '{observed.CurrentStateLabel}' exceeded its clip budget";
                }

                break;

            case ObservedAnimationStateCategory.Unknown:
                return $"state '{observed.CurrentStateLabel}' is not mapped to expected '{expectedState}'";
        }

        return $"expected '{expectedState}' but observed '{observed.CurrentStateLabel}'";
    }

    private bool ApplyAnimationCorrection(
        ExpectedAnimationState expectedState,
        AnimationGameplaySnapshot snapshot,
        out string targetStateName)
    {
        targetStateName = ResolveExpectedAnimationStateLabel(expectedState);
        SanitizeAnimatorParametersForExpectedState(expectedState, snapshot);

        switch (expectedState)
        {
            case ExpectedAnimationState.GroundedIdle:
            case ExpectedAnimationState.GroundedMoving:
                targetStateName = groundedRecoveryStateName;
                return TryCrossFadeAnimatorStateFromWatchdog(
                    locomotionAnimationLayer,
                    groundedRecoveryStateName,
                    animationReconciliationCrossFadeDuration,
                    out _);

            case ExpectedAnimationState.JumpTakeoff:
                targetStateName = takeoffStateName;
                return TryCrossFadeAnimatorStateFromWatchdog(
                    jumpAnimationLayer,
                    takeoffStateName,
                    animationReconciliationCrossFadeDuration,
                    out _);

            case ExpectedAnimationState.Airborne:
                targetStateName = airborneStateName;
                return TryCrossFadeAnimatorStateFromWatchdog(
                           jumpAnimationLayer,
                           airborneStateName,
                           animationReconciliationCrossFadeDuration,
                           out _) ||
                       TryCrossFadeAnimatorStateFromWatchdog(
                           jumpAnimationLayer,
                           fallingStateName,
                           animationReconciliationCrossFadeDuration,
                           out targetStateName);

            case ExpectedAnimationState.Falling:
                targetStateName = fallingStateName;
                return TryCrossFadeAnimatorStateFromWatchdog(
                           jumpAnimationLayer,
                           fallingStateName,
                           animationReconciliationCrossFadeDuration,
                           out _) ||
                       TryCrossFadeAnimatorStateFromWatchdog(
                           jumpAnimationLayer,
                           airborneStateName,
                           animationReconciliationCrossFadeDuration,
                           out targetStateName);

            case ExpectedAnimationState.Landing:
                return TryCrossFadeExpectedLandingStateFromWatchdog(out targetStateName);

            case ExpectedAnimationState.LandingRoll:
                targetStateName = rollStateName;
                return TryCrossFadeAnimatorStateFromWatchdog(
                    jumpAnimationLayer,
                    rollStateName,
                    animationReconciliationCrossFadeDuration,
                    out _);
        }

        return false;
    }

    private void SanitizeAnimatorParametersForExpectedState(
        ExpectedAnimationState expectedState,
        AnimationGameplaySnapshot snapshot)
    {
        // The watchdog first restores a coherent parameter set, then forces a state only if
        // the Animator still disagrees. This keeps the gameplay state authoritative.
        ResetAnimatorTriggerIfValid(moveStartTriggerParam);
        ResetAnimatorTriggerIfValid(moveStopTriggerParam);
        ResetAnimatorTriggerIfValid(jumpTriggerParam);
        ResetAnimatorTriggerIfValid(landingTriggerParam);
        ResetAnimatorTriggerIfValid(rollTriggerParam);

        if (!string.IsNullOrWhiteSpace(speedParam))
        {
            SetSpeed(snapshot.AnimatorSpeed);
        }

        smoothedTurnAmount = snapshot.SignedTurn;
        SetAnimatorFloatIfValid(locomotionTierParam, snapshot.LocomotionTier);
        SetAnimatorFloatIfValid(turnParam, smoothedTurnAmount);
        SetAnimatorBoolIfValid(isMovingParam, snapshot.IsMoving);
        SetAnimatorBoolIfValid(
            turnInPlaceParam,
            expectedState == ExpectedAnimationState.GroundedIdle && snapshot.ShouldTurnInPlace);

        UpdateCommittedJumpAnimation();
    }

    private bool TryCrossFadeExpectedLandingStateFromWatchdog(out string targetStateName)
    {
        string primaryStateName = naturalFallAnimationPhase == NaturalFallAnimationPhase.Landing
            ? naturalLandingStateName
            : idleLandingStateName;
        string fallbackStateName = primaryStateName == naturalLandingStateName
            ? idleLandingStateName
            : naturalLandingStateName;

        targetStateName = primaryStateName;
        return TryCrossFadeAnimatorStateFromWatchdog(
                   jumpAnimationLayer,
                   primaryStateName,
                   animationReconciliationCrossFadeDuration,
                   out _) ||
               TryCrossFadeAnimatorStateFromWatchdog(
                   jumpAnimationLayer,
                   fallbackStateName,
                   animationReconciliationCrossFadeDuration,
                   out targetStateName);
    }

    private bool TryCrossFadeAnimatorStateFromWatchdog(
        int layerIndex,
        string stateName,
        float transitionDuration,
        out string resolvedStateName)
    {
        resolvedStateName = stateName;
        if (!TryResolveAnimatorStateHash(layerIndex, stateName, out int stateHash))
        {
            return false;
        }

        if (IsAnimatorStateCurrentOrNext(layerIndex, stateHash))
        {
            return false;
        }

        if (lastWatchdogCrossFadeLayer == layerIndex &&
            lastWatchdogCrossFadeStateHash == stateHash &&
            Time.time < lastWatchdogCrossFadeTime + repeatedAnimationCorrectionCooldown)
        {
            return false;
        }

        animator.CrossFadeInFixedTime(
            stateHash,
            Mathf.Max(0f, transitionDuration),
            layerIndex,
            0f);

        lastWatchdogCrossFadeLayer = layerIndex;
        lastWatchdogCrossFadeStateHash = stateHash;
        lastWatchdogCrossFadeTime = Time.time;
        return true;
    }

    private bool TryCrossFadeAnimatorState(int layerIndex, string stateName, float transitionDuration)
    {
        if (!TryResolveAnimatorStateHash(layerIndex, stateName, out int stateHash))
        {
            return false;
        }

        if (IsAnimatorStateCurrentOrNext(layerIndex, stateHash))
        {
            return false;
        }

        animator.CrossFadeInFixedTime(
            stateHash,
            Mathf.Max(0f, transitionDuration),
            layerIndex,
            0f);
        return true;
    }

    private bool TryResolveAnimatorStateHash(int layerIndex, string stateName, out int stateHash)
    {
        stateHash = 0;
        if (animator == null ||
            string.IsNullOrWhiteSpace(stateName) ||
            layerIndex < 0 ||
            layerIndex >= animator.layerCount)
        {
            return false;
        }

        string layerName = animator.GetLayerName(layerIndex);
        int fullPathHash = string.IsNullOrWhiteSpace(layerName)
            ? Animator.StringToHash(stateName)
            : Animator.StringToHash(layerName + "." + stateName);
        if (animator.HasState(layerIndex, fullPathHash))
        {
            stateHash = fullPathHash;
            return true;
        }

        int shortNameHash = Animator.StringToHash(stateName);
        if (animator.HasState(layerIndex, shortNameHash))
        {
            stateHash = shortNameHash;
            return true;
        }

        WarnMissingAnimatorState(layerIndex, stateName);
        return false;
    }

    private void ValidateAnimatorStateReference(int layerIndex, string stateName)
    {
        TryResolveAnimatorStateHash(layerIndex, stateName, out _);
    }

    private void WarnMissingAnimatorState(int layerIndex, string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        string key = $"{layerIndex}:{stateName}";
        if (!missingAnimatorStateWarnings.Add(key))
        {
            return;
        }

        string layerName = layerIndex >= 0 && layerIndex < animator.layerCount
            ? animator.GetLayerName(layerIndex)
            : $"Layer#{layerIndex}";

        Debug.LogWarning(
            $"[AnimationReconcile] character='{name}' missing Animator state '{stateName}' on layer '{layerName}'. CrossFade correction for this mapping is disabled until the controller matches the config.",
            this);
    }

    private bool IsAnimatorStateCurrentOrNext(int layerIndex, int stateHash)
    {
        if (animator == null ||
            stateHash == 0 ||
            layerIndex < 0 ||
            layerIndex >= animator.layerCount)
        {
            return false;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (AnimatorStateMatchesHash(currentState, stateHash))
        {
            return true;
        }

        if (!animator.IsInTransition(layerIndex))
        {
            return false;
        }

        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(layerIndex);
        return AnimatorStateMatchesHash(nextState, stateHash);
    }

    private static bool AnimatorStateMatchesHash(AnimatorStateInfo stateInfo, int stateHash)
    {
        return stateHash != 0 &&
               (stateInfo.fullPathHash == stateHash || stateInfo.shortNameHash == stateHash);
    }

    private static int ResolveAnimatorStateIdentityHash(AnimatorStateInfo stateInfo)
    {
        if (stateInfo.fullPathHash != 0)
        {
            return stateInfo.fullPathHash;
        }

        return stateInfo.shortNameHash;
    }

    private float ResolveAnimatorStateLengthSeconds(AnimatorStateInfo stateInfo, string fallbackStateName)
    {
        if (stateInfo.length > 0f &&
            !float.IsNaN(stateInfo.length) &&
            !float.IsInfinity(stateInfo.length))
        {
            return stateInfo.length;
        }

        if (TryResolveAnimationClipDuration(fallbackStateName, out float clipDuration))
        {
            return clipDuration;
        }

        return 0f;
    }

    private float TrackObservedAnimatorStateDuration(
        ref int trackedStateHash,
        ref float trackedDuration,
        int observedStateHash,
        float deltaTime)
    {
        if (observedStateHash == 0)
        {
            trackedStateHash = 0;
            trackedDuration = 0f;
            return 0f;
        }

        if (trackedStateHash == observedStateHash)
        {
            trackedDuration += Mathf.Max(0f, deltaTime);
        }
        else
        {
            trackedStateHash = observedStateHash;
            trackedDuration = 0f;
        }

        return trackedDuration;
    }

    private static string ResolveExpectedAnimationStateLabel(ExpectedAnimationState expectedState)
    {
        switch (expectedState)
        {
            case ExpectedAnimationState.GroundedIdle:
                return "GroundedIdle";
            case ExpectedAnimationState.GroundedMoving:
                return "GroundedMoving";
            case ExpectedAnimationState.JumpTakeoff:
                return "JumpTakeoff";
            case ExpectedAnimationState.Airborne:
                return "Airborne";
            case ExpectedAnimationState.Falling:
                return "Falling";
            case ExpectedAnimationState.Landing:
                return "Landing";
            case ExpectedAnimationState.LandingRoll:
                return "LandingRoll";
            default:
                return "None";
        }
    }

    private void ResetAnimationReconciliationState()
    {
        activeAnimationMismatchState = ExpectedAnimationState.None;
        animationMismatchTimer = 0f;
    }

    private void LogAnimationReconciliationCorrection(
        ExpectedAnimationState expectedState,
        ObservedAnimatorStateSnapshot observed,
        AnimationGameplaySnapshot snapshot,
        string reason,
        string targetStateName,
        float mismatchDuration)
    {
        if (!logAnimationReconciliation)
        {
            return;
        }

        Debug.Log(
            $"[AnimationReconcile] character='{name}' current='{observed.CurrentStateLabel}' currentHash={observed.CurrentStateHash} next='{observed.NextStateLabel}' nextHash={observed.NextStateHash} expected='{expectedState}' target='{targetStateName}' reason='{reason}' mismatch={mismatchDuration:0.###}s moving={snapshot.IsMoving} grounded={isGrounded} jumpPhase='{committedJumpPhase}' naturalFall='{naturalFallAnimationPhase}' speed={snapshot.PresentationSpeed:0.###}",
            this);
    }

    private void LogAnimationPhaseTimeout(string phaseName, string stateName, float elapsed, float timeout)
    {
        if (!logAnimationReconciliation)
        {
            return;
        }

        Debug.Log(
            $"[AnimationReconcile] character='{name}' phase='{phaseName}' state='{stateName}' timed out after {elapsed:0.###}s (timeout={timeout:0.###}s), forcing gameplay recovery.",
            this);
    }

    private float ResolveAnimationPhaseTimeout(
        string stateName,
        float minimumDuration,
        float fallbackDuration,
        float timeoutPadding)
    {
        float resolvedDuration = Mathf.Max(minimumDuration, fallbackDuration);
        if (TryResolveAnimationClipDuration(stateName, out float clipDuration))
        {
            resolvedDuration = Mathf.Max(resolvedDuration, clipDuration);
        }

        return Mathf.Max(0f, resolvedDuration + Mathf.Max(0f, timeoutPadding));
    }

    private bool TryResolveAnimationClipDuration(string stateName, out float duration)
    {
        duration = 0f;
        if (animator == null ||
            animator.runtimeAnimatorController == null ||
            string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null)
        {
            return false;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null || !string.Equals(clip.name, stateName, System.StringComparison.Ordinal))
            {
                continue;
            }

            duration = Mathf.Max(0f, clip.length);
            return duration > 0f;
        }

        return false;
    }

    private bool HasReachedAnimationGateOrTimedOut(
        string stateName,
        float normalizedTimeThreshold,
        float phaseStartTime,
        float minimumDuration,
        float fallbackDuration,
        float timeoutPadding,
        string phaseName)
    {
        if (Time.time < phaseStartTime + minimumDuration)
        {
            return false;
        }

        if (HasReachedAnimationWindow(stateName, normalizedTimeThreshold))
        {
            return true;
        }

        // A blocked Animator must not keep the gameplay phase alive forever.
        float timeout = ResolveAnimationPhaseTimeout(
            stateName,
            minimumDuration,
            fallbackDuration,
            timeoutPadding);
        float elapsed = Time.time - phaseStartTime;
        if (elapsed < timeout)
        {
            return false;
        }

        LogAnimationPhaseTimeout(phaseName, stateName, elapsed, timeout);
        return true;
    }

    private void UpdateLocomotionAnimatorSignals(AnimationGameplaySnapshot snapshot, float deltaTime)
    {
        if (animationTurnResponsiveness > 0f)
        {
            float t = 1f - Mathf.Exp(-animationTurnResponsiveness * deltaTime);
            smoothedTurnAmount = Mathf.Lerp(smoothedTurnAmount, snapshot.SignedTurn, t);
        }
        else
        {
            smoothedTurnAmount = snapshot.SignedTurn;
        }

        SetAnimatorFloatIfValid(turnParam, smoothedTurnAmount);

        if (snapshot.IsMoving)
        {
            lastMovingLocomotionTier = snapshot.LocomotionTier;
        }

        SetAnimatorFloatIfValid(locomotionTierParam, snapshot.LocomotionTier);

        if (snapshot.IsMoving != wasMovingForAnimator)
        {
            if (snapshot.IsMoving)
            {
                SetAnimatorTriggerIfValid(moveStartTriggerParam);
            }
            else
            {
                SetAnimatorTriggerIfValid(moveStopTriggerParam);
            }
        }

        SetAnimatorBoolIfValid(isMovingParam, snapshot.IsMoving);
        SetAnimatorBoolIfValid(turnInPlaceParam, snapshot.ShouldTurnInPlace);

        wasMovingForAnimator = snapshot.IsMoving;
    }
}
