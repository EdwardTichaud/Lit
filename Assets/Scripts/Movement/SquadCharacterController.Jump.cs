using UnityEngine;

public partial class SquadCharacterController
{
    private enum CommittedJumpPhase
    {
        Grounded = 0,
        Takeoff = 1,
        Airborne = 2,
        LandingRecovery = 3,
        LandingRoll = 4,
    }

    private enum JumpStartContext
    {
        Idle = 0,
        Moving = 1,
        AssistedForward = 2,
    }

    private enum CommittedLandingType
    {
        None = 0,
        IdleRecovery = 1,
        Roll = 2,
    }

    [Header("Jump Start")]
    [SerializeField, Tooltip("Active le saut a phases engagees et remplace le saut legacy.")]
    private bool enableCommittedJump = true;
    [SerializeField, Tooltip("Courte anticipation avant la poussee du saut (s).")]
    private float anticipationDuration = 0.08f;
    [SerializeField, Tooltip("Delai supplementaire avant l'impulsion si le timing n'est pas anime (s).")]
    private float impulseDelay = 0.02f;
    [SerializeField, Tooltip("Declenche l'impulsion sur le timing de l'animation de takeoff.")]
    private bool useAnimationTimedImpulse = true;
    [SerializeField, Range(0f, 1f), Tooltip("Normalized time du takeoff pour appliquer l'impulsion.")]
    private float takeoffImpulseNormalizedTime = 0.22f;
    [SerializeField, Tooltip("Vitesse verticale appliquee au moment du takeoff.")]
    private float verticalImpulse = 7f;
    [SerializeField, Tooltip("Distance horizontale minimale visee par un saut sans obstacle. 3m par defaut pour tester le saut engage.")]
    private float minimumJumpTravelDistance = 3f;
    [SerializeField, Tooltip("Marge appliquee au calcul de vitesse minimale du saut.")]
    private float minimumJumpTravelSpeedSafety = 1.15f;
    [SerializeField, Tooltip("Vitesse avant verrouillee pour un saut demarre a l'arret.")]
    private float idleLockedForwardSpeed = 0f;
    [SerializeField, Tooltip("Vitesse minimale pour considerer que le saut commence en mouvement.")]
    private float movingJumpThreshold = 0.3f;
    [SerializeField, Tooltip("Vitesse minimale preservee par un saut lance en mouvement.")]
    private float movingTakeoffMinLaunchSpeed = 1.5f;
    [SerializeField, Tooltip("Vitesse minimale au depart pour declencher un roulage a l'atterrissage.")]
    private float movingJumpMinSpeedForRoll = 1.1f;
    [SerializeField, Tooltip("Multiplicateur applique a la vitesse de depart pour verrouiller l'elan.")]
    private float movingJumpSpeedMultiplier = 1f;
    [SerializeField, Tooltip("Vitesse avant maximale conservee pendant le saut engage.")]
    private float maxLockedForwardSpeed = 4f;
    [SerializeField, Tooltip("Acceleration horizontale pendant le takeoff pour atteindre la vitesse verrouillee.")]
    private float takeoffHorizontalAcceleration = 18f;
    [SerializeField, Tooltip("Duree de freinage de l'elan avant l'impulsion du saut (s).")]
    private float takeoffMomentumBrakeDuration = 0.08f;
    [SerializeField, Tooltip("Frein horizontal applique pendant l'anticipation du saut.")]
    private float takeoffMomentumBrakeDamping = 32f;
    [SerializeField, Tooltip("Boost avant ajoute au takeoff pour un saut demarre a l'arret.")]
    private float idleTakeoffForwardImpulse = 0f;
    [SerializeField, Tooltip("Boost avant ajoute au takeoff pour un saut lance en mouvement.")]
    private float movingTakeoffForwardImpulse = 0f;
    [SerializeField, Tooltip("Boost avant ajoute au takeoff pour un saut impulse vers l'avant depuis l'arret.")]
    private float forwardAssistTakeoffForwardImpulse = 0f;
    [SerializeField, Tooltip("Magnitude minimale d'input pour transformer un saut idle en saut impulse vers l'avant.")]
    private float forwardAssistInputThreshold = 0.45f;
    [SerializeField, Range(-1f, 1f), Tooltip("Alignement minimal entre l'input et le forward du personnage pour le saut impulse vers l'avant.")]
    private float forwardAssistMinDot = 0.45f;
    [SerializeField, Tooltip("Vitesse minimale du saut impulse vers l'avant quand on saute depuis l'arret.")]
    private float forwardAssistLaunchSpeed = 1.75f;
    [SerializeField, Tooltip("Tolere un saut juste apres avoir quitte le sol (s).")]
    private float jumpCoyoteTime = 0.1f;
    [SerializeField, Tooltip("Temps minimal entre deux sauts engages (s).")]
    private float jumpCooldown = 0.1f;
    [SerializeField, Tooltip("Temps pendant lequel le sol est ignore juste apres le takeoff (s).")]
    private float jumpGroundIgnoreTime = 0.08f;

    [Header("Airborne")]
    [SerializeField, Tooltip("Multiplicateur de gravite pendant la montee.")]
    private float gravityMultiplier = 1f;
    [SerializeField, Tooltip("Multiplicateur de gravite pendant la chute.")]
    private float fallMultiplier = 1.65f;
    [SerializeField, Tooltip("Vitesse max de chute appliquee au saut engage.")]
    private float maxFallSpeed = 20f;
    [SerializeField, Tooltip("Distance de contact sol utilisee pour declencher les atterrissages engages (m).")]
    private float landingContactProbeDistance = 0.04f;
    [SerializeField, Tooltip("Multiplicateur de rayon pour confirmer un vrai contact sol a l'atterrissage.")]
    private float landingContactRadiusScale = 0.65f;
    [SerializeField, Tooltip("Vitesse verticale max autorisee pour valider le contact d'atterrissage.")]
    private float landingContactMaxUpwardVelocity = 0.75f;

    [Header("Idle Landing")]
    [SerializeField, Tooltip("Duree de stabilization apres un saut demarre a l'arret (s).")]
    private float idleLandingRecoveryDuration = 0.28f;
    [SerializeField, Tooltip("Verrou de mouvement minimal pendant l'atterrissage idle (s).")]
    private float idleLandingMovementLockDuration = 0.28f;
    [SerializeField, Tooltip("Frein applique a l'horizontal pendant la stabilisation idle.")]
    private float idleLandingStopDamping = 14f;
    [SerializeField, Range(0f, 1f), Tooltip("Fenetre mini de l'animation d'atterrissage avant rendu du controle.")]
    private float landingUnlockNormalizedTime = 0.85f;

    [Header("Moving Landing / Roll")]
    [SerializeField, Tooltip("Vitesse horizontale constante cible pendant la fin de saut avant une roulade. 0 = conserve l'elan verrouille.")]
    private float rollApproachForwardSpeed = 0f;
    [SerializeField, Tooltip("Acceleration utilisee pour converger vers la vitesse de fin de saut avant la roulade.")]
    private float rollApproachAcceleration = 18f;
    [SerializeField, Tooltip("Vitesse avant du roulage. Si rollDistance > 0, la distance pilote la vitesse.")]
    private float rollForwardSpeed = 3.2f;
    [SerializeField, Tooltip("Distance cible du roulage. 0 = utilise rollForwardSpeed.")]
    private float rollDistance = 1.5f;
    [SerializeField, Tooltip("Duree de propulsion du roulage (s).")]
    private float rollDuration = 0.38f;
    [SerializeField, Tooltip("Duree de recuperation apres le roulage (s).")]
    private float rollRecoveryDuration = 0.2f;
    [SerializeField, Tooltip("Frein applique pendant la fin du roulage.")]
    private float rollRecoveryDamping = 12f;
    [SerializeField, Range(0f, 1f), Tooltip("Fenetre mini de l'animation de roulage avant rendu du controle.")]
    private float rollEndNormalizedTime = 0.9f;
    [SerializeField, Tooltip("Pilote la fin de la roulade sur la duree reelle de l'etat Animator au lieu d'une duree fixe.")]
    private bool useRollAnimationDurationForExit = true;
    [SerializeField, Tooltip("Marge ajoutee apres la duree reelle de la roulade avant de rendre le controle (s).")]
    private float rollAnimationExitPadding = 0f;

    [Header("Animation Sync")]
    [SerializeField, Tooltip("Force un CrossFade vers les etats nommes ci-dessous quand une phase commence.")]
    private bool forceJumpStateCrossFade;
    [SerializeField, Tooltip("Layer Animator utilise pour lire les etats de saut.")]
    private int jumpAnimationLayer;
    [SerializeField, Tooltip("Duree du CrossFade de saut (s).")]
    private float jumpAnimationCrossFadeDuration = 0.08f;
    [SerializeField, Tooltip("Trigger lance au debut du takeoff.")]
    private string jumpTriggerParam = "JumpTrigger";
    [SerializeField, Tooltip("Bool actif pendant la phase airborne.")]
    private string isAirborneParam = "IsAirborne";
    [SerializeField, Tooltip("Int de type d'atterrissage: 0 none, 1 idle, 2 roll.")]
    private string landingTypeParam = "LandingType";
    [SerializeField, Tooltip("Bool vrai si le saut a commence en mouvement.")]
    private string jumpFromMovementParam = "JumpFromMovement";
    [SerializeField, Tooltip("Trigger lance a l'entree du roulage.")]
    private string rollTriggerParam = "RollTrigger";
    [SerializeField, Tooltip("Trigger optionnel lance au premier contact d'atterrissage.")]
    private string landingTriggerParam = "LandTrigger";
    [SerializeField, Tooltip("Int de phase de saut: 0 grounded, 1 takeoff, 2 airborne, 3 landingRecovery, 4 landingRoll.")]
    private string jumpPhaseParam = "JumpPhase";
    [SerializeField, Tooltip("Nom de l'etat Animator du takeoff.")]
    private string takeoffStateName = "Jump_Takeoff";
    [SerializeField, Tooltip("Nom de l'etat Animator airborne.")]
    private string airborneStateName = "Jump_Airborne";
    [SerializeField, Tooltip("Nom de l'etat Animator d'atterrissage idle.")]
    private string idleLandingStateName = "Jump_Land";
    [SerializeField, Tooltip("Nom de l'etat Animator de roulage.")]
    private string rollStateName = "Jump_Roll";
    [SerializeField, Tooltip("Nom de l'etat Animator de locomotion utilise comme repli quand le saut se termine.")]
    private string groundedRecoveryStateName = "Locomotion";

    private CommittedJumpPhase committedJumpPhase;
    private JumpStartContext committedJumpStartContext;
    private CommittedLandingType committedLandingType;
    private bool committedJumpRequested;
    private bool takeoffImpulseApplied;
    private float committedJumpPhaseStartTime = float.NegativeInfinity;
    private float lastCommittedJumpTime = float.NegativeInfinity;
    private Vector3 committedJumpDirection = Vector3.forward;
    private float committedLaunchSpeed;
    private Vector3 committedLockedHorizontalVelocity;
    private float committedRollSpeed;
    private Vector2 queuedCommittedJumpInput;
    private bool queuedCommittedJumpInputIsWorldSpace;
    private bool hasQueuedCommittedJumpInput;

    public bool IsJumpCommitted => enableCommittedJump && committedJumpPhase != CommittedJumpPhase.Grounded;

    public bool IsMovementInputSuppressed => inputLockTimer > 0f || IsJumpCommitted || IsLadderTraversalActive;

    public int CurrentCommittedJumpPhase => (int)committedJumpPhase;

    public void QueueCommittedJumpInput(Vector2 input, bool isWorldSpace)
    {
        queuedCommittedJumpInput = Vector2.ClampMagnitude(input, 1f);
        queuedCommittedJumpInputIsWorldSpace = isWorldSpace;
        hasQueuedCommittedJumpInput = queuedCommittedJumpInput.sqrMagnitude > 0.0001f;
    }

    private void ResetCommittedJumpRuntime()
    {
        committedJumpPhase = CommittedJumpPhase.Grounded;
        committedJumpStartContext = JumpStartContext.Idle;
        committedLandingType = CommittedLandingType.None;
        committedJumpRequested = false;
        takeoffImpulseApplied = false;
        committedJumpPhaseStartTime = float.NegativeInfinity;
        committedJumpDirection = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
        committedLaunchSpeed = 0f;
        committedLockedHorizontalVelocity = Vector3.zero;
        committedRollSpeed = 0f;
        ClearQueuedCommittedJumpInput();
    }

    private void ValidateCommittedJumpSettings()
    {
        anticipationDuration = Mathf.Max(0f, anticipationDuration);
        impulseDelay = Mathf.Max(0f, impulseDelay);
        takeoffImpulseNormalizedTime = Mathf.Clamp01(takeoffImpulseNormalizedTime);
        verticalImpulse = Mathf.Max(0f, verticalImpulse);
        minimumJumpTravelDistance = Mathf.Max(0f, minimumJumpTravelDistance);
        minimumJumpTravelSpeedSafety = Mathf.Max(1f, minimumJumpTravelSpeedSafety);
        idleLockedForwardSpeed = Mathf.Max(0f, idleLockedForwardSpeed);
        movingJumpThreshold = Mathf.Max(0f, movingJumpThreshold);
        movingTakeoffMinLaunchSpeed = Mathf.Max(0f, movingTakeoffMinLaunchSpeed);
        movingJumpMinSpeedForRoll = Mathf.Max(0f, movingJumpMinSpeedForRoll);
        movingJumpSpeedMultiplier = Mathf.Max(0f, movingJumpSpeedMultiplier);
        maxLockedForwardSpeed = Mathf.Max(0f, maxLockedForwardSpeed);
        takeoffHorizontalAcceleration = Mathf.Max(0f, takeoffHorizontalAcceleration);
        takeoffMomentumBrakeDuration = Mathf.Max(0f, takeoffMomentumBrakeDuration);
        takeoffMomentumBrakeDamping = Mathf.Max(0f, takeoffMomentumBrakeDamping);
        idleTakeoffForwardImpulse = Mathf.Max(0f, idleTakeoffForwardImpulse);
        movingTakeoffForwardImpulse = Mathf.Max(0f, movingTakeoffForwardImpulse);
        forwardAssistTakeoffForwardImpulse = Mathf.Max(0f, forwardAssistTakeoffForwardImpulse);
        forwardAssistInputThreshold = Mathf.Clamp01(forwardAssistInputThreshold);
        forwardAssistMinDot = Mathf.Clamp(forwardAssistMinDot, -1f, 1f);
        forwardAssistLaunchSpeed = Mathf.Max(0f, forwardAssistLaunchSpeed);
        jumpCoyoteTime = Mathf.Max(0f, jumpCoyoteTime);
        jumpCooldown = Mathf.Max(0f, jumpCooldown);
        jumpGroundIgnoreTime = Mathf.Max(0f, jumpGroundIgnoreTime);
        gravityMultiplier = Mathf.Max(0f, gravityMultiplier);
        fallMultiplier = Mathf.Max(0f, fallMultiplier);
        maxFallSpeed = Mathf.Max(0f, maxFallSpeed);
        landingContactProbeDistance = Mathf.Max(0.005f, landingContactProbeDistance);
        landingContactRadiusScale = Mathf.Clamp(landingContactRadiusScale, 0.1f, 1.25f);
        landingContactMaxUpwardVelocity = Mathf.Max(0f, landingContactMaxUpwardVelocity);
        idleLandingRecoveryDuration = Mathf.Max(0f, idleLandingRecoveryDuration);
        idleLandingMovementLockDuration = Mathf.Max(0f, idleLandingMovementLockDuration);
        idleLandingStopDamping = Mathf.Max(0f, idleLandingStopDamping);
        landingUnlockNormalizedTime = Mathf.Clamp01(landingUnlockNormalizedTime);
        rollApproachForwardSpeed = Mathf.Max(0f, rollApproachForwardSpeed);
        rollApproachAcceleration = Mathf.Max(0f, rollApproachAcceleration);
        rollForwardSpeed = Mathf.Max(0f, rollForwardSpeed);
        rollDistance = Mathf.Max(0f, rollDistance);
        rollDuration = Mathf.Max(0f, rollDuration);
        rollRecoveryDuration = Mathf.Max(0f, rollRecoveryDuration);
        rollRecoveryDamping = Mathf.Max(0f, rollRecoveryDamping);
        rollEndNormalizedTime = Mathf.Clamp01(rollEndNormalizedTime);
        rollAnimationExitPadding = Mathf.Max(0f, rollAnimationExitPadding);
        jumpAnimationLayer = Mathf.Max(0, jumpAnimationLayer);
        jumpAnimationCrossFadeDuration = Mathf.Max(0f, jumpAnimationCrossFadeDuration);
    }

    private void RequestCommittedJump()
    {
        if (!enableCommittedJump)
        {
            return;
        }

        committedJumpRequested = true;
    }

    private void UpdateCommittedJump(float deltaTime)
    {
        if (!enableCommittedJump || !ShouldUseRigidbody() || rigidbodyTarget == null)
        {
            committedJumpRequested = false;
            if (committedJumpPhase != CommittedJumpPhase.Grounded)
            {
                FinishCommittedJump();
            }

            return;
        }

        if (committedJumpPhase == CommittedJumpPhase.Grounded)
        {
            if (!committedJumpRequested)
            {
                return;
            }

            committedJumpRequested = false;
            if (!CanStartCommittedJump())
            {
                ClearQueuedCommittedJumpInput();
                return;
            }

            BeginCommittedJump();
        }

        switch (committedJumpPhase)
        {
            case CommittedJumpPhase.Takeoff:
                if (!takeoffImpulseApplied && ShouldApplyTakeoffImpulse())
                {
                    ApplyTakeoffImpulse(deltaTime);
                }
                break;

            case CommittedJumpPhase.Airborne:
                if (ShouldBeginCommittedLanding())
                {
                    BeginLandingPhase();
                }
                break;

            case CommittedJumpPhase.LandingRecovery:
                if (CanExitIdleLanding())
                {
                    FinishCommittedJump();
                }
                break;

            case CommittedJumpPhase.LandingRoll:
                if (CanExitLandingRoll())
                {
                    FinishCommittedJump();
                }
                break;
        }
    }

    private bool TryApplyCommittedJumpMovement(float deltaTime)
    {
        if (!enableCommittedJump || committedJumpPhase == CommittedJumpPhase.Grounded)
        {
            return false;
        }

        if (!ShouldUseRigidbody() || rigidbodyTarget == null)
        {
            return false;
        }

        switch (committedJumpPhase)
        {
            case CommittedJumpPhase.Takeoff:
                ApplyTakeoffMovement(deltaTime);
                return true;

            case CommittedJumpPhase.Airborne:
                ApplyAirborneMovement(deltaTime);
                return true;

            case CommittedJumpPhase.LandingRecovery:
                ApplyIdleLandingMovement(deltaTime);
                return true;

            case CommittedJumpPhase.LandingRoll:
                ApplyLandingRollMovement(deltaTime);
                return true;
        }

        return false;
    }

    private void UpdateCommittedJumpAnimation()
    {
        if (animator == null)
        {
            return;
        }

        SetAnimatorBoolIfValid(isAirborneParam, committedJumpPhase == CommittedJumpPhase.Airborne);
        SetAnimatorBoolIfValid(
            jumpFromMovementParam,
            committedJumpPhase != CommittedJumpPhase.Grounded && IsMovementStyleJumpContext(committedJumpStartContext));
        SetAnimatorIntIfValid(landingTypeParam, (int)committedLandingType);
        SetAnimatorIntIfValid(jumpPhaseParam, (int)committedJumpPhase);
    }

    private bool ShouldSuppressFootIkForCommittedJump()
    {
        return committedJumpPhase != CommittedJumpPhase.Grounded;
    }

    private bool CanStartCommittedJump()
    {
        if (verticalImpulse <= 0f)
        {
            return false;
        }

        if (Time.time < lastCommittedJumpTime + jumpCooldown)
        {
            return false;
        }

        return isGrounded || Time.time <= lastGroundedTime + jumpCoyoteTime;
    }

    private void BeginCommittedJump()
    {
        Vector3 planarVelocity = GetCurrentHorizontalVelocity();
        float planarSpeed = planarVelocity.magnitude;
        Vector2 launchInput = ResolveCommittedLaunchInput();
        Vector3 inputDirection = launchInput.sqrMagnitude > 0.0001f
            ? ResolveCommittedInputDirection(launchInput)
            : Vector3.zero;
        float requestedSpeed = Mathf.Clamp01(launchInput.magnitude) * ResolveCurrentTargetMoveSpeed();
        bool hasMomentumLaunch = planarSpeed >= movingJumpThreshold;
        bool hasForwardAssistLaunch = !hasMomentumLaunch && IsForwardAssistJumpIntent(launchInput, inputDirection);

        if (hasMomentumLaunch)
        {
            committedJumpStartContext = JumpStartContext.Moving;
        }
        else if (hasForwardAssistLaunch)
        {
            committedJumpStartContext = JumpStartContext.AssistedForward;
        }
        else
        {
            committedJumpStartContext = JumpStartContext.Idle;
        }

        committedJumpDirection = ResolveCommittedJumpDirection(planarVelocity, inputDirection);
        committedLaunchSpeed = ResolveCommittedLaunchSpeed(planarSpeed, requestedSpeed);
        committedLockedHorizontalVelocity = committedJumpDirection * committedLaunchSpeed;
        takeoffImpulseApplied = false;
        committedLandingType = CommittedLandingType.None;
        ClearQueuedCommittedJumpInput();
        EnterCommittedJumpPhase(CommittedJumpPhase.Takeoff);
        SetAnimatorTriggerIfValid(jumpTriggerParam);
        CrossFadeJumpStateIfRequested(takeoffStateName);
    }

    private Vector3 ResolveCommittedJumpDirection(Vector3 planarVelocity, Vector3 inputDirection)
    {
        Vector3 planarVelocityDirection = new Vector3(planarVelocity.x, 0f, planarVelocity.z);
        Vector3 planarInput = new Vector3(inputDirection.x, 0f, inputDirection.z);

        if (committedJumpStartContext == JumpStartContext.Moving &&
            planarVelocityDirection.sqrMagnitude > 0.0001f)
        {
            return planarVelocityDirection.normalized;
        }

        if (planarInput.sqrMagnitude > 0.0001f)
        {
            return planarInput.normalized;
        }

        if (planarVelocityDirection.sqrMagnitude > 0.0001f)
        {
            return planarVelocityDirection.normalized;
        }

        Vector3 fallbackForward = motionRoot != null ? motionRoot.forward : transform.forward;
        fallbackForward.y = 0f;
        if (fallbackForward.sqrMagnitude > 0.0001f)
        {
            return fallbackForward.normalized;
        }

        return Vector3.forward;
    }

    private float ResolveCommittedLaunchSpeed(float planarSpeed, float requestedSpeed)
    {
        if (committedJumpStartContext == JumpStartContext.Idle)
        {
            return ApplyMinimumCommittedJumpTravelSpeed(ClampCommittedLaunchSpeed(
                ScaleConfiguredLocomotionSpeed(idleLockedForwardSpeed) +
                ScaleConfiguredLocomotionSpeed(idleTakeoffForwardImpulse)));
        }

        if (committedJumpStartContext == JumpStartContext.AssistedForward)
        {
            float assistedSpeed = Mathf.Max(
                requestedSpeed * movingJumpSpeedMultiplier,
                ScaleConfiguredLocomotionSpeed(forwardAssistLaunchSpeed));
            return ApplyMinimumCommittedJumpTravelSpeed(ClampCommittedLaunchSpeed(
                assistedSpeed +
                ScaleConfiguredLocomotionSpeed(forwardAssistTakeoffForwardImpulse)));
        }

        float lockedSpeed = Mathf.Max(
            planarSpeed * movingJumpSpeedMultiplier,
            ScaleConfiguredLocomotionSpeed(movingTakeoffMinLaunchSpeed));
        lockedSpeed = ClampCommittedLaunchSpeed(
            lockedSpeed +
            ScaleConfiguredLocomotionSpeed(movingTakeoffForwardImpulse));

        return ApplyMinimumCommittedJumpTravelSpeed(Mathf.Max(planarSpeed, lockedSpeed));
    }

    private bool ShouldApplyTakeoffImpulse()
    {
        float phaseElapsed = Time.time - committedJumpPhaseStartTime;
        if (phaseElapsed < anticipationDuration)
        {
            return false;
        }

        if (!useAnimationTimedImpulse)
        {
            return phaseElapsed >= anticipationDuration + impulseDelay;
        }

        if (HasReachedAnimationWindow(takeoffStateName, takeoffImpulseNormalizedTime))
        {
            return true;
        }

        return phaseElapsed >= anticipationDuration + impulseDelay;
    }

    private void ApplyTakeoffImpulse(float deltaTime)
    {
        Vector3 launchHorizontal = committedLockedHorizontalVelocity;
        launchHorizontal = ConstrainHorizontalVelocityAgainstWalls(launchHorizontal, deltaTime);
        rigidbodyTarget.linearVelocity = new Vector3(launchHorizontal.x, verticalImpulse, launchHorizontal.z);
        currentHorizontalVelocity = launchHorizontal;
        committedLockedHorizontalVelocity = launchHorizontal;
        takeoffImpulseApplied = true;
        lastCommittedJumpTime = Time.time;
        isGrounded = false;
        lastGroundedTime = float.NegativeInfinity;
        groundIgnoreUntilTime = Time.time + jumpGroundIgnoreTime;
        EnterCommittedJumpPhase(CommittedJumpPhase.Airborne);
        CrossFadeJumpStateIfRequested(airborneStateName);
    }

    private void BeginLandingPhase()
    {
        bool shouldRoll = IsRollEligibleJumpContext(committedJumpStartContext) &&
                          committedLaunchSpeed >= movingJumpMinSpeedForRoll;

        committedLandingType = shouldRoll
            ? CommittedLandingType.Roll
            : CommittedLandingType.IdleRecovery;
        SetAnimatorTriggerIfValid(landingTriggerParam);

        if (shouldRoll)
        {
            committedRollSpeed = ResolveCommittedRollSpeed();
            EnterCommittedJumpPhase(CommittedJumpPhase.LandingRoll);
            SetAnimatorTriggerIfValid(rollTriggerParam);
            CrossFadeJumpStateIfRequested(rollStateName);
            return;
        }

        EnterCommittedJumpPhase(CommittedJumpPhase.LandingRecovery);
        CrossFadeJumpStateIfRequested(idleLandingStateName);
    }

    private float ResolveCommittedRollSpeed()
    {
        if (rollDistance > 0f && rollDuration > 0f)
        {
            return (rollDistance / rollDuration) * ResolveCurrentMoveSpeedScale();
        }

        if (rollForwardSpeed > 0f)
        {
            return ScaleConfiguredLocomotionSpeed(rollForwardSpeed);
        }

        return committedLaunchSpeed;
    }

    private void ApplyTakeoffMovement(float deltaTime)
    {
        Vector3 currentVelocity = rigidbodyTarget.linearVelocity;
        Vector3 currentHorizontal = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        bool shouldBrakeMomentum = ShouldBrakeTakeoffMomentum();
        Vector3 targetHorizontal = shouldBrakeMomentum
            ? Vector3.zero
            : committedLockedHorizontalVelocity;
        float acceleration = shouldBrakeMomentum
            ? takeoffMomentumBrakeDamping
            : takeoffHorizontalAcceleration;
        Vector3 nextHorizontal = MoveTowardsWithOptionalSnap(
            currentHorizontal,
            targetHorizontal,
            acceleration,
            deltaTime);

        nextHorizontal = ConstrainHorizontalVelocityAgainstWalls(nextHorizontal, deltaTime);
        float vertical = -groundedStickVelocity;
        rigidbodyTarget.linearVelocity = new Vector3(nextHorizontal.x, vertical, nextHorizontal.z);
        currentHorizontalVelocity = nextHorizontal;
        RotateTowardsCommittedDirection(deltaTime);
    }

    private void ApplyAirborneMovement(float deltaTime)
    {
        Vector3 velocity = rigidbodyTarget.linearVelocity;
        Vector3 horizontal = ResolveCommittedAirborneHorizontalVelocity(velocity.y, deltaTime);
        horizontal = ConstrainHorizontalVelocityAgainstWalls(horizontal, deltaTime);
        float extraGravityScale = velocity.y >= 0f ? gravityMultiplier : fallMultiplier;
        float vertical = velocity.y + (Physics.gravity.y * Mathf.Max(0f, extraGravityScale - 1f) * deltaTime);

        if (maxFallSpeed > 0f)
        {
            vertical = Mathf.Max(vertical, -maxFallSpeed);
        }

        rigidbodyTarget.linearVelocity = new Vector3(
            horizontal.x,
            vertical,
            horizontal.z);

        currentHorizontalVelocity = horizontal;
        RotateTowardsCommittedDirection(deltaTime);
    }

    private void ApplyIdleLandingMovement(float deltaTime)
    {
        Vector3 currentHorizontal = GetCurrentHorizontalVelocity();
        Vector3 nextHorizontal = MoveTowardsWithOptionalSnap(
            currentHorizontal,
            Vector3.zero,
            idleLandingStopDamping,
            deltaTime);

        nextHorizontal = ConstrainHorizontalVelocityAgainstWalls(nextHorizontal, deltaTime);
        rigidbodyTarget.linearVelocity = new Vector3(nextHorizontal.x, -groundedStickVelocity, nextHorizontal.z);
        currentHorizontalVelocity = nextHorizontal;
        RotateTowardsCommittedDirection(deltaTime);
    }

    private void ApplyLandingRollMovement(float deltaTime)
    {
        float elapsed = Time.time - committedJumpPhaseStartTime;
        Vector3 nextHorizontal;
        if (rollDuration > 0f && elapsed >= rollDuration)
        {
            float damping = rollRecoveryDamping > 0f ? rollRecoveryDamping : idleLandingStopDamping;
            nextHorizontal = MoveTowardsWithOptionalSnap(
                GetCurrentHorizontalVelocity(),
                Vector3.zero,
                damping,
                deltaTime);
        }
        else
        {
            nextHorizontal = committedJumpDirection * committedRollSpeed;
        }

        nextHorizontal = ConstrainHorizontalVelocityAgainstWalls(nextHorizontal, deltaTime);
        rigidbodyTarget.linearVelocity = new Vector3(nextHorizontal.x, -groundedStickVelocity, nextHorizontal.z);
        currentHorizontalVelocity = nextHorizontal;
        RotateTowardsCommittedDirection(deltaTime);
    }

    private void RotateTowardsCommittedDirection(float deltaTime)
    {
        if (!rotateToInput || committedJumpDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(committedJumpDirection, transform.up);
        if (rigidbodyTarget != null)
        {
            rigidbodyTarget.MoveRotation(
                Quaternion.Slerp(rigidbodyTarget.rotation, targetRotation, rotationSpeed * deltaTime));
            return;
        }

        Transform target = motionRoot != null ? motionRoot : transform;
        target.rotation = Quaternion.Slerp(target.rotation, targetRotation, rotationSpeed * deltaTime);
    }

    private bool CanExitIdleLanding()
    {
        float minimumDuration = Mathf.Max(idleLandingRecoveryDuration, idleLandingMovementLockDuration);
        if (Time.time < committedJumpPhaseStartTime + minimumDuration)
        {
            return false;
        }

        return HasReachedAnimationWindow(idleLandingStateName, landingUnlockNormalizedTime);
    }

    private bool CanExitLandingRoll()
    {
        if (useRollAnimationDurationForExit &&
            TryGetJumpAnimationElapsedAndDuration(rollStateName, out float elapsedSeconds, out float durationSeconds))
        {
            return elapsedSeconds >= durationSeconds + rollAnimationExitPadding;
        }

        float totalDuration = rollDuration + rollRecoveryDuration;
        if (Time.time < committedJumpPhaseStartTime + totalDuration)
        {
            return false;
        }

        return HasReachedAnimationWindow(rollStateName, rollEndNormalizedTime);
    }

    private bool ShouldBeginCommittedLanding()
    {
        if (Time.time < groundIgnoreUntilTime || !isGrounded || rigidbodyTarget == null)
        {
            return false;
        }

        if (rigidbodyTarget.linearVelocity.y > landingContactMaxUpwardVelocity)
        {
            return false;
        }

        return HasCommittedLandingContact();
    }

    private void FinishCommittedJump()
    {
        committedJumpRequested = false;
        takeoffImpulseApplied = false;
        committedLandingType = CommittedLandingType.None;
        committedRollSpeed = 0f;
        committedLaunchSpeed = 0f;
        committedLockedHorizontalVelocity = Vector3.zero;
        ClearQueuedCommittedJumpInput();
        EnterCommittedJumpPhase(CommittedJumpPhase.Grounded);
        CrossFadeToGroundedRecoveryStateIfNeeded();
    }

    private void EnterCommittedJumpPhase(CommittedJumpPhase newPhase)
    {
        committedJumpPhase = newPhase;
        committedJumpPhaseStartTime = Time.time;
    }

    private Vector3 MoveTowardsWithOptionalSnap(Vector3 current, Vector3 target, float speed, float deltaTime)
    {
        if (speed <= 0f)
        {
            return target;
        }

        return Vector3.MoveTowards(current, target, speed * deltaTime);
    }

    private float ClampCommittedLaunchSpeed(float speed)
    {
        float clampedSpeed = Mathf.Max(0f, speed);
        float maxLaunchSpeed = GetCommittedMaxLaunchSpeed();
        if (!float.IsPositiveInfinity(maxLaunchSpeed))
        {
            clampedSpeed = Mathf.Min(clampedSpeed, maxLaunchSpeed);
        }

        return clampedSpeed;
    }

    private float ApplyMinimumCommittedJumpTravelSpeed(float speed)
    {
        float minimumSpeed = ResolveMinimumCommittedJumpTravelSpeed();
        if (minimumSpeed <= 0f)
        {
            return Mathf.Max(0f, speed);
        }

        float maxLaunchSpeed = GetCommittedMaxLaunchSpeed();
        if (!float.IsPositiveInfinity(maxLaunchSpeed))
        {
            minimumSpeed = Mathf.Min(minimumSpeed, maxLaunchSpeed);
        }

        return Mathf.Max(Mathf.Max(0f, speed), minimumSpeed);
    }

    private float ResolveMinimumCommittedJumpTravelSpeed()
    {
        if (minimumJumpTravelDistance <= 0f || verticalImpulse <= 0f)
        {
            return 0f;
        }

        float predictedAirTime = EstimateCommittedJumpAirTime();
        if (predictedAirTime <= 0.05f)
        {
            return 0f;
        }

        return (minimumJumpTravelDistance / predictedAirTime) * minimumJumpTravelSpeedSafety;
    }

    private float EstimateCommittedJumpAirTime()
    {
        float gravityMagnitude = Mathf.Abs(Physics.gravity.y);
        if (gravityMagnitude <= 0.0001f)
        {
            return 0f;
        }

        float upwardGravity = gravityMagnitude * Mathf.Max(0.0001f, gravityMultiplier);
        float downwardGravity = gravityMagnitude * Mathf.Max(0.0001f, fallMultiplier);
        float timeUp = verticalImpulse / upwardGravity;
        float apexHeight = (verticalImpulse * verticalImpulse) / (2f * upwardGravity);
        float timeDown = Mathf.Sqrt(Mathf.Max(0f, (2f * apexHeight) / downwardGravity));
        return timeUp + timeDown;
    }

    private float GetCommittedMaxLaunchSpeed()
    {
        float scaledMaxLockedForwardSpeed = ScaleConfiguredLocomotionSpeed(maxLockedForwardSpeed);
        return scaledMaxLockedForwardSpeed > 0f
            ? scaledMaxLockedForwardSpeed
            : float.PositiveInfinity;
    }

    private bool ShouldBrakeTakeoffMomentum()
    {
        return takeoffMomentumBrakeDuration > 0f &&
               takeoffMomentumBrakeDamping > 0f &&
               Time.time < committedJumpPhaseStartTime + takeoffMomentumBrakeDuration;
    }

    private Vector3 ResolveCommittedAirborneHorizontalVelocity(float verticalVelocity, float deltaTime)
    {
        if (!ShouldUseRollApproachSpeed() || committedJumpDirection.sqrMagnitude < 0.0001f || verticalVelocity > 0f)
        {
            return committedLockedHorizontalVelocity;
        }

        Vector3 targetHorizontal = committedJumpDirection * ClampCommittedLaunchSpeed(
            ScaleConfiguredLocomotionSpeed(rollApproachForwardSpeed));
        if (rollApproachAcceleration <= 0f)
        {
            committedLockedHorizontalVelocity = targetHorizontal;
            return committedLockedHorizontalVelocity;
        }

        committedLockedHorizontalVelocity = Vector3.MoveTowards(
            committedLockedHorizontalVelocity,
            targetHorizontal,
            rollApproachAcceleration * deltaTime);
        return committedLockedHorizontalVelocity;
    }

    private bool HasReachedAnimationWindow(string stateName, float normalizedTimeThreshold)
    {
        if (normalizedTimeThreshold <= 0f ||
            string.IsNullOrWhiteSpace(stateName) ||
            animator == null)
        {
            return true;
        }

        if (TryGetJumpAnimationNormalizedTime(stateName, out float normalizedTime))
        {
            return normalizedTime >= normalizedTimeThreshold;
        }

        return true;
    }

    private bool TryGetJumpAnimationElapsedAndDuration(string stateName, out float elapsedSeconds, out float durationSeconds)
    {
        elapsedSeconds = 0f;
        durationSeconds = 0f;
        if (!TryGetJumpAnimationStateInfo(stateName, out AnimatorStateInfo stateInfo))
        {
            return false;
        }

        if (stateInfo.length <= 0f || float.IsNaN(stateInfo.length) || float.IsInfinity(stateInfo.length))
        {
            return false;
        }

        durationSeconds = stateInfo.length;
        elapsedSeconds = Mathf.Max(0f, stateInfo.normalizedTime) * durationSeconds;
        return true;
    }

    private bool TryGetJumpAnimationNormalizedTime(string stateName, out float normalizedTime)
    {
        normalizedTime = 0f;
        if (!TryGetJumpAnimationStateInfo(stateName, out AnimatorStateInfo stateInfo))
        {
            return false;
        }

        normalizedTime = Mathf.Max(0f, stateInfo.normalizedTime);
        return true;
    }

    private bool TryGetJumpAnimationStateInfo(string stateName, out AnimatorStateInfo stateInfo)
    {
        stateInfo = default;
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(jumpAnimationLayer);
        if (AnimatorStateMatches(currentState, stateName))
        {
            stateInfo = currentState;
            return true;
        }

        if (!animator.IsInTransition(jumpAnimationLayer))
        {
            return false;
        }

        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(jumpAnimationLayer);
        if (!AnimatorStateMatches(nextState, stateName))
        {
            return false;
        }

        stateInfo = nextState;
        return true;
    }

    private static bool AnimatorStateMatches(AnimatorStateInfo stateInfo, string stateName)
    {
        int stateHash = Animator.StringToHash(stateName);
        return stateInfo.shortNameHash == stateHash ||
               stateInfo.fullPathHash == stateHash ||
               stateInfo.IsName(stateName);
    }

    private Vector2 ResolveCommittedLaunchInput()
    {
        if (hasQueuedCommittedJumpInput)
        {
            return queuedCommittedJumpInput;
        }

        if (ResolveMovementInputMagnitude(smoothedInput) > 0f)
        {
            return smoothedInput;
        }

        return ResolveMovementInputMagnitude(moveInput) > 0f
            ? moveInput
            : Vector2.zero;
    }

    private Vector3 ResolveCommittedInputDirection(Vector2 launchInput)
    {
        if (!hasQueuedCommittedJumpInput)
        {
            return GetMoveDirection(launchInput);
        }

        if (queuedCommittedJumpInputIsWorldSpace)
        {
            return new Vector3(launchInput.x, 0f, launchInput.y);
        }

        return GetMoveDirection(launchInput);
    }

    private void ClearQueuedCommittedJumpInput()
    {
        queuedCommittedJumpInput = Vector2.zero;
        queuedCommittedJumpInputIsWorldSpace = false;
        hasQueuedCommittedJumpInput = false;
    }

    private bool HasCommittedLandingContact()
    {
        if (TryGetLocomotionCapsule(out _, out float radius, out _))
        {
            float probeDistance = Mathf.Max(0.005f, landingContactProbeDistance);
            float probeRadius = Mathf.Max(0.02f, radius * landingContactRadiusScale * 0.25f);
            return TryProbeGroundedSupport(probeDistance, probeRadius, out _, out _);
        }

        return isGrounded;
    }

    private bool IsForwardAssistJumpIntent(Vector2 launchInput, Vector3 inputDirection)
    {
        if (launchInput.sqrMagnitude < forwardAssistInputThreshold * forwardAssistInputThreshold)
        {
            return false;
        }

        Vector3 planarInput = new Vector3(inputDirection.x, 0f, inputDirection.z);
        if (planarInput.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        planarInput.Normalize();

        Vector3 forward = motionRoot != null ? motionRoot.forward : transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        forward.Normalize();
        return Vector3.Dot(planarInput, forward) >= forwardAssistMinDot;
    }

    private static bool IsMovementStyleJumpContext(JumpStartContext context)
    {
        return context != JumpStartContext.Idle;
    }

    private static bool IsRollEligibleJumpContext(JumpStartContext context)
    {
        return context == JumpStartContext.Moving;
    }

    private bool ShouldUseRollApproachSpeed()
    {
        return IsRollEligibleJumpContext(committedJumpStartContext) &&
               committedLaunchSpeed >= movingJumpMinSpeedForRoll &&
               ScaleConfiguredLocomotionSpeed(rollApproachForwardSpeed) > 0f;
    }

    private void CrossFadeJumpStateIfRequested(string stateName)
    {
        if (!forceJumpStateCrossFade || animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        animator.CrossFadeInFixedTime(stateName, jumpAnimationCrossFadeDuration, jumpAnimationLayer, 0f);
    }

    private void CrossFadeToGroundedRecoveryStateIfNeeded()
    {
        if (animator == null || string.IsNullOrWhiteSpace(groundedRecoveryStateName))
        {
            return;
        }

        int shortStateHash = Animator.StringToHash(groundedRecoveryStateName);
        string layerName = animator.GetLayerName(jumpAnimationLayer);
        int fullPathStateHash = string.IsNullOrWhiteSpace(layerName)
            ? shortStateHash
            : Animator.StringToHash(layerName + "." + groundedRecoveryStateName);
        if (!animator.HasState(jumpAnimationLayer, shortStateHash) &&
            !animator.HasState(jumpAnimationLayer, fullPathStateHash))
        {
            return;
        }

        if (!TryGetJumpAnimationStateInfo(takeoffStateName, out _) &&
            !TryGetJumpAnimationStateInfo(airborneStateName, out _) &&
            !TryGetJumpAnimationStateInfo(idleLandingStateName, out _) &&
            !TryGetJumpAnimationStateInfo(rollStateName, out _))
        {
            return;
        }

        animator.CrossFadeInFixedTime(groundedRecoveryStateName, jumpAnimationCrossFadeDuration, jumpAnimationLayer, 0f);
    }

    private void SetAnimatorTriggerIfValid(string parameterName)
    {
        if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger))
        {
            return;
        }

        animator.SetTrigger(parameterName);
    }

    private void SetAnimatorBoolIfValid(string parameterName, bool value)
    {
        if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
        {
            return;
        }

        animator.SetBool(parameterName, value);
    }

    private void SetAnimatorIntIfValid(string parameterName, int value)
    {
        if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Int))
        {
            return;
        }

        animator.SetInteger(parameterName, value);
    }

    private void SetAnimatorFloatIfValid(string parameterName, float value)
    {
        if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Float))
        {
            return;
        }

        animator.SetFloat(parameterName, value);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType expectedType)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter == null)
            {
                continue;
            }

            if (!string.Equals(parameter.name, parameterName, System.StringComparison.Ordinal))
            {
                continue;
            }

            return parameter.type == expectedType;
        }

        return false;
    }
}
