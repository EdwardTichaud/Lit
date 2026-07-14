using System.Collections;
using Opsive.Shared.Events;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character.Abilities.Items;
using UnityEngine;

// Runtime bridge between Lit's gameplay facade and Opsive UCC locomotion.
[RequireComponent(typeof(UltimateCharacterLocomotion))]
[RequireComponent(typeof(UltimateCharacterLocomotionHandler))]
[RequireComponent(typeof(LitOpsivePlayerInput))]
public partial class LitOpsiveLocomotionBridge : MonoBehaviour
{
    private const string JumpInputName = "Jump";
    private const string SpeedChangeInputName = "Change Speeds";
    private const string CrouchInputName = "Crouch";

    [SerializeField] private SquadCharacterController squadController;
    [SerializeField] private UltimateCharacterLocomotion locomotion;
    [SerializeField] private UltimateCharacterLocomotionHandler locomotionHandler;
    [SerializeField] private LitOpsivePlayerInput playerInput;
    [SerializeField] private LitOpsiveLookSource lookSource;
    [SerializeField] private Animator animator;
    [SerializeField] private AnimatorMonitor animatorMonitor;

    [Header("Bridge")]
    [SerializeField, Tooltip("When enabled, SquadCharacterController forwards locomotion commands to UCC and keeps Lit simulation disabled.")]
    private bool driveFromSquadFacade = true;
    [SerializeField, Tooltip("Feed movement directly into UltimateCharacterLocomotionHandler.OverrideInput.")]
    private bool overrideOpsiveHandlerInput = true;
    [SerializeField, Tooltip("Rotate the local look source toward world-space movement. Does not modify the project camera.")]
    private bool orientLookSourceFromMovement = true;
    [SerializeField, Tooltip("Configure the Rigidbody as kinematic/no-gravity while UCC is active.")]
    private bool configureRigidbodyForOpsive = true;
    [Header("Root Motion Locomotion")]
    [SerializeField, Tooltip("Lets UCC consume Animator root motion for grounded locomotion instead of using motor-force displacement.")]
    private bool useRootMotionLocomotion = false;
    [SerializeField, Min(0f)] private float rootMotionSpeedMultiplier = 1f;
    [SerializeField, Min(0f)] private float rootMotionRotationMultiplier = 1f;
    [SerializeField, Tooltip("Lets UCC/look-source rotation drive regular root-motion locomotion so directional clips cannot lock the body facing. Pivot clips can still use authored root rotation.")]
    private bool preferLookSourceRotationForRootMotionLocomotion = true;
    [SerializeField, Tooltip("Allows authored root rotation during start/stop clips. Keep disabled when clips contain little or conflicting deltaRotation.")]
    private bool allowRootMotionRotationDuringStartStop = false;
    [SerializeField, Tooltip("Applies additional root-motion tuning based on the active grounded animation phase.")]
    private bool useRootMotionPhaseMultipliers = true;
    [SerializeField, Min(0f)] private float rootMotionLoopSpeedScale = 1f;
    [SerializeField, Min(0f)] private float rootMotionLoopRotationScale = 1f;
    [SerializeField, Min(0f)] private float rootMotionStartSpeedScale = 0.96f;
    [SerializeField, Min(0f)] private float rootMotionStartRotationScale = 1f;
    [SerializeField, Min(0f)] private float rootMotionStopSpeedScale = 0.82f;
    [SerializeField, Min(0f)] private float rootMotionStopRotationScale = 0.94f;
    [SerializeField, Min(0f)] private float rootMotionPivotSpeedScale = 0.88f;
    [SerializeField, Min(0f)] private float rootMotionPivotRotationScale = 1.12f;
    [SerializeField, Tooltip("Keeps Animator.applyRootMotion enabled while UCC reads deltaPosition/deltaRotation through AnimatorMonitor.")]
    private bool preserveAnimatorRootMotion = true;
    [SerializeField, Tooltip("Restores the previous UCC root motion settings when the bridge is disabled.")]
    private bool restoreRootMotionSettingsOnDisable = true;
    [SerializeField, Tooltip("Reapplies root motion multipliers every frame so Play Mode inspector tuning is felt immediately.")]
    private bool refreshRootMotionSettingsEveryFrame = true;
    [SerializeField, Tooltip("Feeds local X/Y movement to UCC and the Animator so root-motion strafe/diagonal clips can blend in.")]
    private bool driveDirectionalRootMotionInput = true;
    [SerializeField, Tooltip("Add Lit/UCC companion bridges at runtime so interaction, damage and follower systems can respect UCC state without prefab edits.")]
    private bool autoInstallCompanionBridges = true;
    [SerializeField, Range(0f, 0.5f)] private float movementDeadZone = 0.08f;
    [SerializeField, Min(0f), Tooltip("Keeps a rejected jump request alive briefly while UCC updates grounded/ability state.")]
    private float jumpRetryWindow = 0.15f;
    [SerializeField, Tooltip("Log a diagnostic warning when UCC keeps rejecting a jump request.")]
    private bool warnWhenJumpRejected = true;
    [SerializeField, Tooltip("Apply a direct UCC force if the migrated Jump ability is missing or refuses a grounded jump request.")]
    private bool useJumpForceFallback = true;
    [SerializeField, Min(0f), Tooltip("Velocity-style upward force used by the Lit fallback jump path.")]
    private float jumpFallbackVelocity = 7f;

    [Header("Ground Relief")]
    [SerializeField, Tooltip("Raises selected UCC ground settings at runtime so mesh floor reliefs and thresholds do not behave like hard walls.")]
    private bool relaxGroundReliefTolerance = true;
    [SerializeField, Min(0f), Tooltip("Minimum UCC step height used while this bridge drives locomotion.")]
    private float groundReliefMinStepHeight = 0.6f;
    [SerializeField, Range(0f, 89f), Tooltip("Minimum UCC traversable slope angle used while this bridge drives locomotion.")]
    private float groundReliefMinSlopeLimit = 58f;
    [SerializeField, Min(0f), Tooltip("Minimum UCC stick-to-ground distance used while this bridge drives locomotion.")]
    private float groundReliefMinStickToGroundDistance = 0.55f;
    [SerializeField, Tooltip("Blends stronger relief tolerance while root-motion locomotion is moving across uneven surfaces.")]
    private bool adaptRootMotionGroundRelief = true;
    [SerializeField, Min(0f)] private float rootMotionMovingStepHeight = 0.58f;
    [SerializeField, Range(0f, 89f)] private float rootMotionMovingSlopeLimit = 62f;
    [SerializeField, Min(0f)] private float rootMotionMovingStickToGroundDistance = 0.86f;
    [SerializeField, Min(0f)] private float rootMotionIdleStickToGroundDistance = 0.64f;
    [SerializeField, Min(0f)] private float rootMotionGroundReliefAdaptationSpeed = 7.5f;

    [Header("Flight")]
    [SerializeField, Tooltip("Restores the pre-UCC LocomotionMode flight toggle through a lightweight UCC ability.")]
    private bool enableUccFlight = true;
    [SerializeField, Min(0f)] private float flightTakeoffVerticalSpeed = 6.5f;
    [SerializeField, Min(0f)] private float flightTakeoffDuration = 0.45f;
    [SerializeField, Min(0f)] private float flightTakeoffDamping = 16f;
    [SerializeField, Min(0f)] private float flightCruiseSpeed = 33f;
    [SerializeField, Min(0f)] private float flightBoostSpeed = 81f;
    [SerializeField, Min(0f)] private float flightAcceleration = 54f;
    [SerializeField, Min(0f)] private float flightBoostAcceleration = 126f;
    [SerializeField, Min(0f)] private float flightDeceleration = 36f;
    [SerializeField, Min(0f)] private float flightVerticalSpeed = 24f;
    [SerializeField, Min(0f)] private float flightVerticalAcceleration = 66f;
    [SerializeField, Min(0f)] private float flightVerticalDeceleration = 54f;
    [SerializeField, Range(0f, 0.4f)] private float flightVerticalDeadZone = 0.05f;
    [SerializeField, Min(0f)] private float flightIdleSpeedThreshold = 0.08f;
    [SerializeField, Min(0f)] private float flightTurnRate = 760f;
    [SerializeField, Min(0f)] private float flightBoostTurnRate = 460f;
    [SerializeField, Min(0f)] private float flightLandingSpeed = 12f;
    [SerializeField, Min(0f)] private float flightLandingAcceleration = 36f;
    [SerializeField, Tooltip("Utilise un pilote autonome si la capacite de vol UCC est absente ou refuse de demarrer.")]
    private bool allowStandaloneFlightFallback = true;
    [SerializeField, Min(0f)] private float fallbackFlightCollisionSkin = 0.03f;
    [SerializeField, Min(0f)] private float fallbackFlightGroundProbeDistance = 0.2f;

    [Header("Animator Compatibility")]
    [SerializeField, Tooltip("Only enable for legacy Lit animator controllers. UCC animator controllers should be driven by AnimatorMonitor parameters.")]
    private bool driveLitLocomotionAnimatorParameters = false;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string horizontalMovementParam = "HorizontalMovement";
    [SerializeField] private string forwardMovementParam = "ForwardMovement";
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string locomotionTierParam = "LocomotionTier";
    [SerializeField] private string turnParam = "Turn";
    [SerializeField] private string flightStateParam = "FlightState";
    [SerializeField] private string flightSpeedParam = "FlightSpeed";
    [SerializeField] private string flightVerticalParam = "FlightVertical";
    [SerializeField] private string flightBoostParam = "FlightBoost";
    [SerializeField] private string flightStartTriggerParam = "FlightStartTrigger";
    [SerializeField] private float walkPresentationSpeed = 1.35f;
    [SerializeField] private float runPresentationSpeed = 3.25f;

    private Rigidbody rb;
    private bool externalDriverRegistered;
    private bool previousUseGravity;
    private bool previousIsKinematic;
    private CollisionDetectionMode previousCollisionMode;
    private bool rootMotionLocomotionApplied;
    private bool previousUseRootMotionPosition;
    private float previousRootMotionSpeedMultiplier;
    private bool previousUseRootMotionRotation;
    private float previousRootMotionRotationMultiplier;
    private bool previousAnimatorApplyRootMotion;
    private bool groundReliefToleranceApplied;
    private bool previousUccStickToGround;
    private float previousUccMaxStepHeight;
    private float previousUccSlopeLimit;
    private float previousUccStickToGroundDistance;
    private Vector2 currentWorldMoveInput;
    private bool sprintPressed;
    private bool warnedMissingJump;
    private bool warnedMissingSpeedChange;
    private bool warnedMissingHeightChange;
    private bool warnedJumpRejected;
    private bool warnedFlightStartRejected;
    private bool hasPendingJump;
    private float pendingJumpUntil;
    private Vector2 pendingJumpWorldInput;
    private bool pendingJumpHasWorldInput;
    private LitUccFlightAbility flightAbility;
    private Vector3 lastPlanarDirection = Vector3.forward;
    private Vector3 lastPosition;
    private bool hasLastPosition;
    private int scriptedTraversalLockCount;
    private bool scriptedTraversalInputDisabled;
    private Coroutine externalImpulseLockRoutine;
    private Ability[] scriptedTraversalAbilities;
    private bool[] scriptedTraversalAbilityEnabledStates;
    private ItemAbility[] scriptedTraversalItemAbilities;
    private bool[] scriptedTraversalItemAbilityEnabledStates;
    private bool previousLocomotionUseGravity;
    private bool scriptedTraversalGravityPrepared;
    private int externalLockCount;
    private bool externalLockInputDisabled;

    private enum RootMotionPhase
    {
        Locomotion,
        Start,
        Stop,
        Pivot,
        Other
    }

    public bool IsDriving => isActiveAndEnabled && driveFromSquadFacade && locomotion != null && locomotionHandler != null;
    public bool IsScriptedTraversalActive => scriptedTraversalLockCount > 0;
    public bool IsExternalLockActive => externalLockCount > 0;
    public bool IsInputSuppressedByUcc => IsScriptedTraversalActive || IsExternalLockActive;
    public bool IsFlightActive => IsFlightModeActive;
    public bool Grounded => locomotion != null && locomotion.Grounded;
    public bool ShouldPreserveAnimatorRootMotion => useRootMotionLocomotion && preserveAnimatorRootMotion;
    public Vector3 Velocity => locomotion != null ? locomotion.Velocity : Vector3.zero;
    public Vector3 PlanarVelocity => Vector3.ProjectOnPlane(Velocity, transform.up);
    public Vector3 WorldPosition => locomotion != null ? locomotion.transform.position : Vector3.zero;
    public bool CanDriveScriptedTraversal
    {
        get
        {
            ResolveReferences();
            return isActiveAndEnabled && locomotion != null;
        }
    }

    private void OnValidate()
    {
        rootMotionSpeedMultiplier = Mathf.Max(0f, rootMotionSpeedMultiplier);
        rootMotionRotationMultiplier = Mathf.Max(0f, rootMotionRotationMultiplier);
        rootMotionLoopSpeedScale = Mathf.Max(0f, rootMotionLoopSpeedScale);
        rootMotionLoopRotationScale = Mathf.Max(0f, rootMotionLoopRotationScale);
        rootMotionStartSpeedScale = Mathf.Max(0f, rootMotionStartSpeedScale);
        rootMotionStartRotationScale = Mathf.Max(0f, rootMotionStartRotationScale);
        rootMotionStopSpeedScale = Mathf.Max(0f, rootMotionStopSpeedScale);
        rootMotionStopRotationScale = Mathf.Max(0f, rootMotionStopRotationScale);
        rootMotionPivotSpeedScale = Mathf.Max(0f, rootMotionPivotSpeedScale);
        rootMotionPivotRotationScale = Mathf.Max(0f, rootMotionPivotRotationScale);
        groundReliefMinStepHeight = Mathf.Max(0f, groundReliefMinStepHeight);
        groundReliefMinSlopeLimit = Mathf.Clamp(groundReliefMinSlopeLimit, 0f, 89f);
        groundReliefMinStickToGroundDistance = Mathf.Max(0f, groundReliefMinStickToGroundDistance);
        rootMotionMovingStepHeight = Mathf.Max(0f, rootMotionMovingStepHeight);
        rootMotionMovingSlopeLimit = Mathf.Clamp(rootMotionMovingSlopeLimit, 0f, 89f);
        rootMotionMovingStickToGroundDistance = Mathf.Max(0f, rootMotionMovingStickToGroundDistance);
        rootMotionIdleStickToGroundDistance = Mathf.Max(0f, rootMotionIdleStickToGroundDistance);
        rootMotionGroundReliefAdaptationSpeed = Mathf.Max(0f, rootMotionGroundReliefAdaptationSpeed);
        ValidateOrientationFeelSettings();
        ValidateObstacleTraversalSettings();
        ValidateJumpLandingSettings();
    }

    public void SetMoveInput(Vector2 input, bool isWorldSpace)
    {
        ResolveReferences();

        if (IsInputSuppressedByUcc)
        {
            ApplyWorldMoveInput(Vector2.zero);
            return;
        }

        Vector2 worldInput = isWorldSpace || squadController == null ? input : squadController.GetWorldSpaceInput(input);
        ApplyWorldMoveInput(worldInput);
    }

    public void SetSprintModifier(bool pressed)
    {
        ResolveReferences();

        if (IsInputSuppressedByUcc)
        {
            pressed = false;
        }

        sprintPressed = pressed;
        if (playerInput != null)
        {
            playerInput.SetSprintOverride(pressed, IsDriving);
        }

        if (!IsFlightModeActive)
        {
            SyncSpeedChangeAbility();
        }
    }

    public bool Jump(Vector2 worldInput, bool hasWorldInput)
    {
        ResolveReferences();

        if (!IsDriving)
        {
            return false;
        }

        if (IsInputSuppressedByUcc)
        {
            ClearPendingJump();
            return false;
        }

        if (IsFlightActive)
        {
            return false;
        }

        if (hasWorldInput && worldInput.sqrMagnitude > movementDeadZone * movementDeadZone)
        {
            ApplyWorldMoveInput(worldInput);
        }

        if (playerInput != null)
        {
            playerInput.PulseButton(JumpInputName);
        }

        Jump jump = locomotion != null ? locomotion.GetAbility<Jump>() : null;
        if (jump == null)
        {
            if (TryApplyJumpForceFallback(worldInput, hasWorldInput))
            {
                return true;
            }

            WarnOnce(ref warnedMissingJump, "UCC Jump ability is missing on this character. Add standard movement abilities with the Lit/Opsive UCC migration tool or Opsive Character Manager.");
            ClearPendingJump();
            return false;
        }

        if (TryStartJumpAbility(jump))
        {
            return true;
        }

        if (TryApplyJumpForceFallback(worldInput, hasWorldInput))
        {
            return true;
        }

        QueuePendingJump(worldInput, hasWorldInput);
        return true;
    }

    public void StopBridgeInput()
    {
        ApplyWorldMoveInput(Vector2.zero);
        SetSprintModifier(false);
        ShutdownFlightMode();
    }

    public bool ToggleHeightChange()
    {
        ResolveReferences();

        if (!IsDriving || IsInputSuppressedByUcc)
        {
            return false;
        }

        HeightChange heightChange = locomotion != null ? locomotion.GetAbility<HeightChange>() : null;
        if (heightChange == null)
        {
            WarnOnce(ref warnedMissingHeightChange, "UCC HeightChange ability is missing on this character. LocomotionMode/Crouch input will be ignored until the ability is added.");
            return false;
        }

        if (heightChange.IsActive)
        {
            return locomotion.TryStopAbility(heightChange);
        }

        if (playerInput != null)
        {
            playerInput.PulseButton(CrouchInputName);
        }

        return locomotion.TryStartAbility(heightChange);
    }

    public bool ToggleFlightMode(float verticalInput)
    {
        bool shouldActivate = !IsFlightModeActive ||
                              flightPresentationState == FlightPresentationState.Landing;
        return SetFlightMode(shouldActivate, verticalInput);
    }

    public bool SetFlightMode(bool active, float verticalInput)
    {
        return RequestFlightMode(active, verticalInput);
    }

    public bool SetFlightInput(Vector2 worldInput, bool boost, float verticalInput)
    {
        return ApplyFlightInput(worldInput, boost, verticalInput);
    }

    public bool BeginExternalLock(bool disableGameplayInput = true, bool stopActiveAbilities = false)
    {
        ResolveReferences();
        if (!CanDriveScriptedTraversal)
        {
            return false;
        }

        externalLockCount = Mathf.Max(0, externalLockCount) + 1;
        if (externalLockCount > 1)
        {
            ForceZeroInput();
            return true;
        }

        ClearPendingJump();
        if (stopActiveAbilities && locomotion != null)
        {
            locomotion.StopAllAbilities(false);
        }

        ForceZeroInput();
        if (disableGameplayInput)
        {
            EventHandler.ExecuteEvent<bool>(gameObject, "OnEnableGameplayInput", false);
            externalLockInputDisabled = true;
        }

        return true;
    }

    public void EndExternalLock()
    {
        if (externalLockCount <= 0)
        {
            externalLockCount = 0;
            return;
        }

        externalLockCount--;
        if (externalLockCount > 0)
        {
            ForceZeroInput();
            return;
        }

        if (externalLockInputDisabled)
        {
            if (!scriptedTraversalInputDisabled)
            {
                EventHandler.ExecuteEvent<bool>(gameObject, "OnEnableGameplayInput", true);
            }

            externalLockInputDisabled = false;
        }

        ForceZeroInput();
        AttachLookSourceIfNeeded(true);
    }

    public bool BeginScriptedTraversal()
    {
        ResolveReferences();
        if (!CanDriveScriptedTraversal)
        {
            return false;
        }

        scriptedTraversalLockCount = Mathf.Max(0, scriptedTraversalLockCount) + 1;
        if (scriptedTraversalLockCount > 1)
        {
            ForceZeroInput();
            return true;
        }

        ClearPendingJump();
        if (locomotion != null)
        {
            locomotion.StopAllAbilities(false);
        }

        SuppressGravityForScriptedTraversal();
        DisableAbilitiesForScriptedTraversal();
        ForceZeroInput();
        EventHandler.ExecuteEvent<bool>(gameObject, "OnEnableGameplayInput", false);
        scriptedTraversalInputDisabled = true;
        return true;
    }

    public void EndScriptedTraversal()
    {
        if (scriptedTraversalLockCount <= 0)
        {
            scriptedTraversalLockCount = 0;
            return;
        }

        scriptedTraversalLockCount--;
        if (scriptedTraversalLockCount > 0)
        {
            ForceZeroInput();
            return;
        }

        RestoreAbilitiesAfterScriptedTraversal();
        RestoreGravityAfterScriptedTraversal();
        if (scriptedTraversalInputDisabled)
        {
            if (!externalLockInputDisabled)
            {
                EventHandler.ExecuteEvent<bool>(gameObject, "OnEnableGameplayInput", true);
            }

            scriptedTraversalInputDisabled = false;
        }

        ForceZeroInput();
        AttachLookSourceIfNeeded(true);
    }

    public void ApplyScriptedTraversalPose(Vector3 position, Quaternion rotation)
    {
        ResolveReferences();
        if (locomotion == null)
        {
            return;
        }

        locomotion.SetPositionAndRotation(position, rotation, false, false);
        lastPosition = position;
        hasLastPosition = true;
    }

    public bool SetExternalPositionAndRotation(Vector3 position, Quaternion rotation, bool stopActiveAbilities)
    {
        ResolveReferences();
        if (!IsDriving || locomotion == null)
        {
            return false;
        }

        locomotion.SetPositionAndRotation(position, rotation, false, stopActiveAbilities);
        lastPosition = position;
        hasLastPosition = true;
        ForceZeroInput();
        return true;
    }

    public bool AddExternalImpulse(Vector3 worldImpulse, ForceMode forceMode, float lockInputForSeconds)
    {
        ResolveReferences();
        if (!IsDriving || locomotion == null || worldImpulse.sqrMagnitude <= 0f)
        {
            return false;
        }

        bool scaleByMass = forceMode != ForceMode.VelocityChange && forceMode != ForceMode.Acceleration;
        locomotion.AddForce(worldImpulse, 1, scaleByMass);

        if (lockInputForSeconds > 0f)
        {
            BeginExternalLock(disableGameplayInput: false, stopActiveAbilities: false);
            if (externalImpulseLockRoutine != null)
            {
                StopCoroutine(externalImpulseLockRoutine);
                EndExternalLock();
            }

            externalImpulseLockRoutine = StartCoroutine(EndExternalImpulseLockAfter(lockInputForSeconds));
        }

        return true;
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureCompanionBridges();
        CacheRigidbodyState();
        lastPosition = transform.position;
        hasLastPosition = true;
        ResetOrientationFeelState();
        ResetGroundedFeelState();
        ResetJumpLandingState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureCompanionBridges();
        CacheRigidbodyState();
        ConfigureRigidbody();
        RefreshRootMotionLocomotionSettings();
        ConfigureGroundReliefTolerance();
        ConfigureGroundedFeelProfile();
        ResetOrientationFeelState();
        ResetGroundedFeelState();
        ResetJumpLandingState();
        RegisterExternalDriver();
        AttachLookSourceIfNeeded(true);
        ApplyWorldMoveInput(currentWorldMoveInput);
    }

    private void OnDisable()
    {
        CancelObstacleTraversal();
        RestoreAbilitiesAfterScriptedTraversal();
        RestoreGravityAfterScriptedTraversal();
        if (externalLockInputDisabled || scriptedTraversalInputDisabled)
        {
            EventHandler.ExecuteEvent<bool>(gameObject, "OnEnableGameplayInput", true);
            externalLockInputDisabled = false;
            scriptedTraversalInputDisabled = false;
        }

        if (externalImpulseLockRoutine != null)
        {
            StopCoroutine(externalImpulseLockRoutine);
            externalImpulseLockRoutine = null;
        }

        externalLockCount = 0;
        scriptedTraversalLockCount = 0;
        StopBridgeInput();
        SetLitAnimatorSpeedParameterOverride(false);
        UnregisterExternalDriver();
        RestoreGroundedFeelProfile();
        RestoreRootMotionLocomotion();
        ResetOrientationFeelState();
        ResetGroundedFeelState();
        ResetJumpLandingState();
        RestoreGroundReliefTolerance();
        RestoreRigidbody();
    }

    private void Update()
    {
        RefreshRootMotionLocomotionSettings();
        RefreshGroundReliefTolerance(immediate: false);

        if (!IsDriving && !IsInputSuppressedByUcc)
        {
            return;
        }

        if (IsInputSuppressedByUcc)
        {
            ForceZeroInput();
            RefreshSquadFacadeSystems();
            return;
        }

        if (TryStartObstacleTraversal())
        {
            RefreshSquadFacadeSystems();
            return;
        }

        TickGroundBlockDiagnostics();

        if (!IsFlightModeActive)
        {
            SyncSpeedChangeAbility();
        }
        AttachLookSourceIfNeeded(false);
        RetryPendingJump();
        UpdateFlightMode();
        UpdateJumpLandingAnimatorParameters();
        UpdateAnimatorParameters();
        RefreshSquadFacadeSystems();
    }

    private void RefreshSquadFacadeSystems()
    {
        if (squadController != null)
        {
            squadController.RefreshAudioListenerStateForExternalLocomotion();
            if (squadController.IsCharacterFlameSystemEnabled)
            {
                squadController.TickFlameLifetimeForExternalLocomotion(Time.deltaTime);
            }
            squadController.RefreshLocalInteractionDetectionForExternalLocomotion();
        }
    }

    private IEnumerator EndExternalImpulseLockAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        externalImpulseLockRoutine = null;
        EndExternalLock();
    }

    private void ForceZeroInput()
    {
        ResetGroundedFeelInput();
        currentWorldMoveInput = Vector2.zero;
        sprintPressed = false;
        SetAnimatorFloat(horizontalMovementParam, 0f);
        SetAnimatorFloat(forwardMovementParam, 0f);

        if (playerInput != null)
        {
            playerInput.SetMovementOverride(Vector2.zero, IsDriving || IsInputSuppressedByUcc);
            playerInput.SetSprintOverride(false, IsDriving || IsInputSuppressedByUcc);
        }

        if (flightAbility != null)
        {
            flightAbility.SetInput(Vector2.zero, false, 0f);
        }

        if (overrideOpsiveHandlerInput && locomotionHandler != null)
        {
            locomotionHandler.OverrideInput = IsDriving || IsInputSuppressedByUcc;
            locomotionHandler.OverriddenHorizontalMovement = 0f;
            locomotionHandler.OverriddenForwardMovement = 0f;
            locomotionHandler.OverriddenLookVector = Vector2.zero;
        }

        SpeedChange speedChange = locomotion != null ? locomotion.GetAbility<SpeedChange>() : null;
        if (speedChange != null && speedChange.IsActive)
        {
            locomotion.TryStopAbility(speedChange);
        }
    }

    private void SuppressGravityForScriptedTraversal()
    {
        if (locomotion == null)
        {
            return;
        }

        previousLocomotionUseGravity = locomotion.UseGravity;
        locomotion.UseGravity = false;
        locomotion.GravityAccumulation = 0f;
        scriptedTraversalGravityPrepared = true;
    }

    private void RestoreGravityAfterScriptedTraversal()
    {
        if (!scriptedTraversalGravityPrepared)
        {
            return;
        }

        if (locomotion != null)
        {
            locomotion.UseGravity = previousLocomotionUseGravity;
            locomotion.GravityAccumulation = 0f;
        }

        scriptedTraversalGravityPrepared = false;
    }

    private void DisableAbilitiesForScriptedTraversal()
    {
        scriptedTraversalAbilities = locomotion != null ? locomotion.Abilities : null;
        scriptedTraversalAbilityEnabledStates = CaptureAndDisableAbilities(scriptedTraversalAbilities);
        scriptedTraversalItemAbilities = locomotion != null ? locomotion.ItemAbilities : null;
        scriptedTraversalItemAbilityEnabledStates = CaptureAndDisableAbilities(scriptedTraversalItemAbilities);
    }

    private void RestoreAbilitiesAfterScriptedTraversal()
    {
        RestoreAbilityEnabledStates(scriptedTraversalAbilities, scriptedTraversalAbilityEnabledStates);
        RestoreAbilityEnabledStates(scriptedTraversalItemAbilities, scriptedTraversalItemAbilityEnabledStates);
        scriptedTraversalAbilities = null;
        scriptedTraversalAbilityEnabledStates = null;
        scriptedTraversalItemAbilities = null;
        scriptedTraversalItemAbilityEnabledStates = null;
    }

    private static bool[] CaptureAndDisableAbilities(Ability[] abilities)
    {
        if (abilities == null || abilities.Length == 0)
        {
            return null;
        }

        bool[] enabledStates = new bool[abilities.Length];
        for (int i = 0; i < abilities.Length; i++)
        {
            Ability ability = abilities[i];
            if (ability == null)
            {
                continue;
            }

            enabledStates[i] = ability.Enabled;
            ability.Enabled = false;
        }

        return enabledStates;
    }

    private static void RestoreAbilityEnabledStates(Ability[] abilities, bool[] enabledStates)
    {
        if (abilities == null || enabledStates == null)
        {
            return;
        }

        int count = Mathf.Min(abilities.Length, enabledStates.Length);
        for (int i = 0; i < count; i++)
        {
            Ability ability = abilities[i];
            if (ability != null)
            {
                ability.Enabled = enabledStates[i];
            }
        }
    }

    private void ResolveReferences()
    {
        if (squadController == null)
        {
            squadController = GetComponent<SquadCharacterController>();
        }

        if (locomotion == null)
        {
            locomotion = GetComponent<UltimateCharacterLocomotion>();
        }

        if (locomotionHandler == null)
        {
            locomotionHandler = GetComponent<UltimateCharacterLocomotionHandler>();
        }

        if (playerInput == null)
        {
            playerInput = GetComponent<LitOpsivePlayerInput>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animatorMonitor == null)
        {
            animatorMonitor = GetComponent<AnimatorMonitor>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        EnsureLookSource();
    }

    private void EnsureCompanionBridges()
    {
        if (!autoInstallCompanionBridges || !Application.isPlaying)
        {
            return;
        }

        EnsureComponent<LitUccInteractionBridge>();
        EnsureComponent<LitUccDamageBridge>();
        EnsureComponent<LitUccFollowerBridge>();
    }

    private T EnsureComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private void EnsureLookSource()
    {
        if (lookSource != null)
        {
            lookSource.EventTarget = gameObject;
            if (Application.isPlaying && isActiveAndEnabled && (locomotion == null || !ReferenceEquals(locomotion.LookSource, lookSource)))
            {
                lookSource.AttachToCharacter();
            }
            return;
        }

        lookSource = GetComponentInChildren<LitOpsiveLookSource>(true);
        if (lookSource == null && Application.isPlaying)
        {
            GameObject host = new GameObject("LitUccLookSource");
            host.transform.SetParent(transform, false);
            lookSource = host.AddComponent<LitOpsiveLookSource>();
        }

        if (lookSource != null)
        {
            lookSource.EventTarget = gameObject;
            if (Application.isPlaying && isActiveAndEnabled && (locomotion == null || !ReferenceEquals(locomotion.LookSource, lookSource)))
            {
                lookSource.AttachToCharacter();
            }
        }
    }

    private void AttachLookSourceIfNeeded(bool force)
    {
        if (lookSource == null)
        {
            EnsureLookSource();
        }

        if (lookSource == null)
        {
            return;
        }

        lookSource.EventTarget = gameObject;
        if (force)
        {
            lookSource.QueueAttachRetries();
            lookSource.AttachToCharacter();
            return;
        }

        if (lookSource.IsAttachedToCharacter() == false)
        {
            lookSource.AttachToCharacter();
        }
    }

    private void ApplyWorldMoveInput(Vector2 worldInput)
    {
        Vector2 targetWorldMoveInput = Vector2.ClampMagnitude(worldInput, 1f);
        float targetMagnitude = targetWorldMoveInput.magnitude;
        if (targetMagnitude <= movementDeadZone)
        {
            targetWorldMoveInput = Vector2.zero;
            targetMagnitude = 0f;
        }

        targetWorldMoveInput = ResolveJumpLandingWorldMoveInput(targetWorldMoveInput);
        targetMagnitude = targetWorldMoveInput.magnitude;

        currentWorldMoveInput = ResolveGroundedFeelWorldMoveInput(targetWorldMoveInput, targetMagnitude);
        float magnitude = currentWorldMoveInput.magnitude;
        if (magnitude <= movementDeadZone)
        {
            currentWorldMoveInput = Vector2.zero;
            magnitude = 0f;
        }

        Vector2 opsiveInput = Vector2.zero;
        if (magnitude > 0f)
        {
            Vector3 direction = new Vector3(currentWorldMoveInput.x, 0f, currentWorldMoveInput.y);
            direction.Normalize();
            opsiveInput = ShouldUseDirectionalRootMotionInput()
                ? ResolveLocalMoveInput(direction, magnitude)
                : new Vector2(0f, magnitude);
            Vector3 lookDirection = direction;
            if (orientLookSourceFromMovement && lookSource != null)
            {
                lookDirection = ResolveOrientationLookDirection(direction, magnitude);
                lookSource.SetPlanarLookDirection(lookDirection);
            }

            lastPlanarDirection = lookDirection.sqrMagnitude > 0.0001f ? lookDirection : direction;
        }

        if (playerInput != null)
        {
            playerInput.SetMovementOverride(opsiveInput, IsDriving);
            playerInput.SetSprintOverride(sprintPressed, IsDriving);
        }

        if (overrideOpsiveHandlerInput && locomotionHandler != null)
        {
            locomotionHandler.OverrideInput = IsDriving;
            locomotionHandler.OverriddenHorizontalMovement = opsiveInput.x;
            locomotionHandler.OverriddenForwardMovement = opsiveInput.y;
            locomotionHandler.OverriddenLookVector = Vector2.zero;
        }
    }

    private bool ShouldUseDirectionalRootMotionInput()
    {
        return driveDirectionalRootMotionInput && IsRootMotionLocomotionEnabled();
    }

    private Vector2 ResolveLocalMoveInput(Vector3 worldDirection, float magnitude)
    {
        if (worldDirection.sqrMagnitude <= 0.0001f || magnitude <= 0f)
        {
            return Vector2.zero;
        }

        Vector3 localDirection = transform.InverseTransformDirection(worldDirection.normalized);
        Vector2 localInput = new Vector2(localDirection.x, localDirection.z);
        if (localInput.sqrMagnitude <= 0.0001f)
        {
            return new Vector2(0f, Mathf.Clamp01(magnitude));
        }

        return Vector2.ClampMagnitude(localInput.normalized * Mathf.Clamp01(magnitude), 1f);
    }

    private void SyncSpeedChangeAbility()
    {
        if (locomotion == null)
        {
            return;
        }

        SpeedChange speedChange = locomotion.GetAbility<SpeedChange>();
        if (speedChange == null)
        {
            if (sprintPressed)
            {
                WarnOnce(ref warnedMissingSpeedChange, "UCC SpeedChange ability is missing on this character. Sprint input will be ignored by UCC until the ability is added.");
            }

            return;
        }

        if (sprintPressed)
        {
            locomotion.TryStartAbility(speedChange);
        }
        else if (speedChange.IsActive)
        {
            locomotion.TryStopAbility(speedChange);
        }
    }

    private bool TryStartJumpAbility(Jump jump)
    {
        jump.ImmediateJump = true;
        bool started = locomotion != null && locomotion.TryStartAbility(jump);
        if (started)
        {
            ClearPendingJump();
            warnedJumpRejected = false;
            NotifyJumpStarted();
            return true;
        }

        jump.ImmediateJump = false;
        return false;
    }

    private void QueuePendingJump(Vector2 worldInput, bool hasWorldInput)
    {
        if (jumpRetryWindow <= 0f)
        {
            WarnJumpRejected();
            return;
        }

        hasPendingJump = true;
        pendingJumpUntil = Time.time + jumpRetryWindow;
        pendingJumpWorldInput = worldInput;
        pendingJumpHasWorldInput = hasWorldInput;
    }

    private void RetryPendingJump()
    {
        if (!hasPendingJump)
        {
            return;
        }

        if (Time.time > pendingJumpUntil)
        {
            if (TryApplyJumpForceFallback(pendingJumpWorldInput, pendingJumpHasWorldInput))
            {
                return;
            }

            ClearPendingJump();
            WarnJumpRejected();
            return;
        }

        if (pendingJumpHasWorldInput && pendingJumpWorldInput.sqrMagnitude > movementDeadZone * movementDeadZone)
        {
            ApplyWorldMoveInput(pendingJumpWorldInput);
        }

        Jump jump = locomotion != null ? locomotion.GetAbility<Jump>() : null;
        if (jump == null)
        {
            if (TryApplyJumpForceFallback(pendingJumpWorldInput, pendingJumpHasWorldInput))
            {
                return;
            }

            WarnOnce(ref warnedMissingJump, "UCC Jump ability is missing on this character. Add standard movement abilities with the Lit/Opsive UCC migration tool or Opsive Character Manager.");
            ClearPendingJump();
            return;
        }

        if (!TryStartJumpAbility(jump))
        {
            TryApplyJumpForceFallback(pendingJumpWorldInput, pendingJumpHasWorldInput);
        }
    }

    private bool TryApplyJumpForceFallback(Vector2 worldInput, bool hasWorldInput)
    {
        if (!useJumpForceFallback ||
            jumpFallbackVelocity <= 0f ||
            locomotion == null ||
            !locomotion.Grounded ||
            IsFlightActive)
        {
            return false;
        }

        if (hasWorldInput && worldInput.sqrMagnitude > movementDeadZone * movementDeadZone)
        {
            ApplyWorldMoveInput(worldInput);
        }

        locomotion.GravityAccumulation = 0f;
        locomotion.AddForce(transform.up * jumpFallbackVelocity, 1, false);
        warnedJumpRejected = false;
        ClearPendingJump();
        NotifyJumpStarted();
        return true;
    }

    private bool EnsureFlightAbility()
    {
        if (!enableUccFlight || locomotion == null)
        {
            return false;
        }

        flightAbility = locomotion.GetAbility<LitUccFlightAbility>();
        if (flightAbility != null)
        {
            ConfigureFlightAbility();
            return true;
        }

        Ability[] abilities = locomotion.Abilities;
        int length = abilities != null ? abilities.Length : 0;
        Ability[] nextAbilities = new Ability[length + 1];
        if (length > 0)
        {
            System.Array.Copy(abilities, nextAbilities, length);
        }

        flightAbility = new LitUccFlightAbility();
        nextAbilities[length] = flightAbility;
        locomotion.Abilities = nextAbilities;
        ConfigureFlightAbility();
        return flightAbility != null;
    }

    private void ConfigureFlightAbility()
    {
        if (flightAbility == null)
        {
            return;
        }

        flightAbility.Configure(
            flightTakeoffVerticalSpeed,
            flightTakeoffDuration,
            flightTakeoffDamping,
            flightCruiseSpeed,
            flightBoostSpeed,
            flightAcceleration,
            flightBoostAcceleration,
            flightDeceleration,
            flightVerticalSpeed,
            flightVerticalAcceleration,
            flightVerticalDeceleration,
            flightVerticalDeadZone,
            flightIdleSpeedThreshold,
            flightTurnRate,
            flightBoostTurnRate,
            flightLandingSpeed,
            flightLandingAcceleration);
    }

    private bool StopFlightAbilityIfActive()
    {
        if (flightAbility == null || !flightAbility.IsActive)
        {
            return true;
        }

        return locomotion != null && locomotion.TryStopAbility(flightAbility, true);
    }

    private void ClearPendingJump()
    {
        hasPendingJump = false;
        pendingJumpUntil = 0f;
        pendingJumpWorldInput = Vector2.zero;
        pendingJumpHasWorldInput = false;
    }

    private void WarnJumpRejected()
    {
        if (!warnWhenJumpRejected)
        {
            return;
        }

        WarnOnce(
            ref warnedJumpRejected,
            $"UCC Jump was requested on '{name}' but UltimateCharacterLocomotion rejected it. Grounded={ResolveGroundedLabel()}, ActiveAbilities={ResolveActiveAbilityLabel()}. Check UCC ground detection, slope/ceiling rules, and any ability blocking Jump.");
    }

    private string ResolveGroundedLabel()
    {
        return locomotion != null ? locomotion.Grounded.ToString() : "unknown";
    }

    private string ResolveActiveAbilityLabel()
    {
        if (locomotion == null || locomotion.ActiveAbilityCount <= 0 || locomotion.ActiveAbilities == null)
        {
            return "none";
        }

        string label = string.Empty;
        int count = Mathf.Min(locomotion.ActiveAbilityCount, locomotion.ActiveAbilities.Length);
        for (int i = 0; i < count; i++)
        {
            Ability ability = locomotion.ActiveAbilities[i];
            if (ability == null)
            {
                continue;
            }

            if (label.Length > 0)
            {
                label += ", ";
            }

            label += $"{ability.GetType().Name}(index {ability.Index})";
        }

        return label.Length > 0 ? label : "none";
    }

    private void UpdateAnimatorParameters()
    {
        UpdateFlightAnimatorParameters();

        if (!driveLitLocomotionAnimatorParameters || animator == null || IsFlightModeActive)
        {
            SetLitAnimatorSpeedParameterOverride(false);
            return;
        }

        SetLitAnimatorSpeedParameterOverride(true);
        Vector3 velocity = ResolvePlanarVelocity();
        float speed = velocity.magnitude;
        bool moving = currentWorldMoveInput.sqrMagnitude > movementDeadZone * movementDeadZone || speed > 0.05f;

        if (TryUpdateGroundedFeelAnimatorParameters(velocity, speed, moving))
        {
            return;
        }

        SetAnimatorFloat(speedParam, speed);
        SetGroundedDirectionalAnimatorParameters(speed, velocity);
        SetAnimatorBool(isMovingParam, moving);
        SetAnimatorFloat(locomotionTierParam, ResolveLocomotionTier(speed));
        SetAnimatorFloat(turnParam, ResolveSignedTurn(velocity));
    }

    private Vector3 ResolvePlanarVelocity()
    {
        Vector3 velocity = locomotion != null ? locomotion.Velocity : Vector3.zero;
        velocity.y = 0f;
        if (velocity.sqrMagnitude > 0.0001f)
        {
            lastPosition = transform.position;
            hasLastPosition = true;
            return velocity;
        }

        if (!hasLastPosition)
        {
            lastPosition = transform.position;
            hasLastPosition = true;
            return Vector3.zero;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        delta.y = 0f;
        return delta / deltaTime;
    }

    private float ResolveLocomotionTier(float speed)
    {
        if (speed <= 0.05f)
        {
            return 1f;
        }

        float jogThreshold = Mathf.Lerp(walkPresentationSpeed, runPresentationSpeed, 0.5f);
        return speed >= jogThreshold ? 3f : speed >= walkPresentationSpeed * 0.5f ? 2f : 1f;
    }

    private float ResolveSignedTurn(Vector3 velocity)
    {
        Vector3 direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : lastPlanarDirection;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        return Mathf.Clamp(Vector3.SignedAngle(transform.forward, direction, Vector3.up) / 90f, -1f, 1f);
    }

    private void SetGroundedDirectionalAnimatorParameters(float presentationSpeed, Vector3 fallbackVelocity)
    {
        Vector2 localDirection;
        float parameterSpeed;
        if (TryGetGroundedMoveTransitionLocalDirection(out Vector2 latchedLocalDirection, out float latchedParameterSpeed))
        {
            localDirection = latchedLocalDirection;
            parameterSpeed = Mathf.Max(Mathf.Max(0f, presentationSpeed), latchedParameterSpeed);
        }
        else
        {
            localDirection = ResolveGroundedLocalMoveDirection(fallbackVelocity);
            parameterSpeed = Mathf.Max(0f, presentationSpeed);
        }

        Vector2 directionalSpeed = localDirection * parameterSpeed;
        SetAnimatorFloat(horizontalMovementParam, directionalSpeed.x);
        SetAnimatorFloat(forwardMovementParam, directionalSpeed.y);
    }

    private Vector2 ResolveGroundedLocalMoveDirection(Vector3 fallbackVelocity)
    {
        Vector3 direction = Vector3.zero;
        if (currentWorldMoveInput.sqrMagnitude > movementDeadZone * movementDeadZone)
        {
            direction = new Vector3(currentWorldMoveInput.x, 0f, currentWorldMoveInput.y);
        }
        else if (fallbackVelocity.sqrMagnitude > 0.0001f)
        {
            direction = fallbackVelocity;
        }
        else
        {
            return Vector2.zero;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return Vector2.zero;
        }

        return ResolveLocalMoveInput(direction.normalized, 1f);
    }

    private void RegisterExternalDriver()
    {
        if (!driveFromSquadFacade || externalDriverRegistered || squadController == null)
        {
            return;
        }

        squadController.PushExternalLocomotionDriver();
        externalDriverRegistered = true;
    }

    private void UnregisterExternalDriver()
    {
        if (!externalDriverRegistered || squadController == null)
        {
            externalDriverRegistered = false;
            return;
        }

        squadController.PopExternalLocomotionDriver();
        externalDriverRegistered = false;
    }

    private void CacheRigidbodyState()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb == null)
        {
            return;
        }

        previousUseGravity = rb.useGravity;
        previousIsKinematic = rb.isKinematic;
        previousCollisionMode = rb.collisionDetectionMode;
    }

    private void ConfigureRigidbody()
    {
        if (!configureRigidbodyForOpsive || rb == null)
        {
            return;
        }

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void ConfigureRootMotionLocomotion()
    {
        if (!useRootMotionLocomotion || locomotion == null || rootMotionLocomotionApplied)
        {
            return;
        }

        previousUseRootMotionPosition = locomotion.UseRootMotionPosition;
        previousRootMotionSpeedMultiplier = locomotion.RootMotionSpeedMultiplier;
        previousUseRootMotionRotation = locomotion.UseRootMotionRotation;
        previousRootMotionRotationMultiplier = locomotion.RootMotionRotationMultiplier;
        previousAnimatorApplyRootMotion = animator != null && animator.applyRootMotion;

        ApplyRootMotionLocomotionSettings();
        rootMotionLocomotionApplied = true;
    }

    private void RefreshRootMotionLocomotionSettings()
    {
        if (!useRootMotionLocomotion)
        {
            if (rootMotionLocomotionApplied)
            {
                RestoreRootMotionLocomotion();
            }

            return;
        }

        if (locomotion == null)
        {
            return;
        }

        if (!rootMotionLocomotionApplied)
        {
            ConfigureRootMotionLocomotion();
            return;
        }

        if (refreshRootMotionSettingsEveryFrame)
        {
            ApplyRootMotionLocomotionSettings();
        }
    }

    private void ApplyRootMotionLocomotionSettings()
    {
        if (locomotion == null)
        {
            return;
        }

        RootMotionPhase phase = ResolveCurrentRootMotionPhase();
        bool useRootMotionRotation = ResolveUseRootMotionRotation(phase);
        locomotion.UseRootMotionPosition = true;
        locomotion.RootMotionSpeedMultiplier = ResolveEffectiveRootMotionSpeedMultiplier(phase);
        locomotion.UseRootMotionRotation = useRootMotionRotation;
        locomotion.RootMotionRotationMultiplier = useRootMotionRotation
            ? ResolveEffectiveRootMotionRotationMultiplier(phase)
            : 0f;

        if (animator != null && preserveAnimatorRootMotion)
        {
            animator.applyRootMotion = true;
        }
    }

    private bool ResolveUseRootMotionRotation(RootMotionPhase phase)
    {
        if (!preferLookSourceRotationForRootMotionLocomotion)
        {
            return true;
        }

        if (phase == RootMotionPhase.Pivot)
        {
            return true;
        }

        return allowRootMotionRotationDuringStartStop &&
               (phase == RootMotionPhase.Start || phase == RootMotionPhase.Stop);
    }

    private float ResolveEffectiveRootMotionSpeedMultiplier(RootMotionPhase phase)
    {
        return Mathf.Max(0f, rootMotionSpeedMultiplier) * ResolveRootMotionPhaseSpeedScale(phase);
    }

    private float ResolveEffectiveRootMotionRotationMultiplier(RootMotionPhase phase)
    {
        return Mathf.Max(0f, rootMotionRotationMultiplier) * ResolveRootMotionPhaseRotationScale(phase);
    }

    private float ResolveRootMotionPhaseSpeedScale(RootMotionPhase phase)
    {
        if (!useRootMotionPhaseMultipliers)
        {
            return 1f;
        }

        switch (phase)
        {
            case RootMotionPhase.Start:
                return Mathf.Max(0f, rootMotionStartSpeedScale);
            case RootMotionPhase.Stop:
                return Mathf.Max(0f, rootMotionStopSpeedScale);
            case RootMotionPhase.Pivot:
                return Mathf.Max(0f, rootMotionPivotSpeedScale);
            case RootMotionPhase.Locomotion:
                return Mathf.Max(0f, rootMotionLoopSpeedScale);
            default:
                return 1f;
        }
    }

    private float ResolveRootMotionPhaseRotationScale(RootMotionPhase phase)
    {
        if (!useRootMotionPhaseMultipliers)
        {
            return 1f;
        }

        switch (phase)
        {
            case RootMotionPhase.Start:
                return Mathf.Max(0f, rootMotionStartRotationScale);
            case RootMotionPhase.Stop:
                return Mathf.Max(0f, rootMotionStopRotationScale);
            case RootMotionPhase.Pivot:
                return Mathf.Max(0f, rootMotionPivotRotationScale);
            case RootMotionPhase.Locomotion:
                return Mathf.Max(0f, rootMotionLoopRotationScale);
            default:
                return 1f;
        }
    }

    private RootMotionPhase ResolveCurrentRootMotionPhase()
    {
        if (!useRootMotionPhaseMultipliers || animator == null)
        {
            return RootMotionPhase.Other;
        }

        const int baseLayer = 0;
        if (animator.IsInTransition(baseLayer))
        {
            RootMotionPhase nextPhase = ResolveRootMotionPhase(animator.GetNextAnimatorStateInfo(baseLayer));
            if (nextPhase == RootMotionPhase.Start ||
                nextPhase == RootMotionPhase.Stop ||
                nextPhase == RootMotionPhase.Pivot)
            {
                return nextPhase;
            }

            RootMotionPhase currentPhase = ResolveRootMotionPhase(animator.GetCurrentAnimatorStateInfo(baseLayer));
            if (currentPhase == RootMotionPhase.Start ||
                currentPhase == RootMotionPhase.Stop ||
                currentPhase == RootMotionPhase.Pivot)
            {
                return currentPhase;
            }

            if (nextPhase != RootMotionPhase.Other)
            {
                return nextPhase;
            }
        }

        return ResolveRootMotionPhase(animator.GetCurrentAnimatorStateInfo(baseLayer));
    }

    private RootMotionPhase ResolveRootMotionPhase(AnimatorStateInfo stateInfo)
    {
        if (stateInfo.IsName("Walk_Start") ||
            stateInfo.IsName("Jogtrot_Start") ||
            stateInfo.IsName("Run_Start"))
        {
            return RootMotionPhase.Start;
        }

        if (stateInfo.IsName("Walk_Stop") ||
            stateInfo.IsName("Jogtrot_Stop") ||
            stateInfo.IsName("Run_Stop"))
        {
            return RootMotionPhase.Stop;
        }

        if (stateInfo.IsName("Turn_L90") ||
            stateInfo.IsName("Turn_R90") ||
            stateInfo.IsName("Turn_L180") ||
            stateInfo.IsName("Turn_R180"))
        {
            return RootMotionPhase.Pivot;
        }

        if (stateInfo.IsName("Locomotion"))
        {
            return RootMotionPhase.Locomotion;
        }

        return RootMotionPhase.Other;
    }

    private void RestoreRootMotionLocomotion()
    {
        if (!rootMotionLocomotionApplied)
        {
            return;
        }

        if (restoreRootMotionSettingsOnDisable && locomotion != null)
        {
            locomotion.UseRootMotionPosition = previousUseRootMotionPosition;
            locomotion.RootMotionSpeedMultiplier = previousRootMotionSpeedMultiplier;
            locomotion.UseRootMotionRotation = previousUseRootMotionRotation;
            locomotion.RootMotionRotationMultiplier = previousRootMotionRotationMultiplier;
        }

        if (restoreRootMotionSettingsOnDisable && animator != null && preserveAnimatorRootMotion)
        {
            animator.applyRootMotion = previousAnimatorApplyRootMotion;
        }

        rootMotionLocomotionApplied = false;
    }

    private bool IsRootMotionLocomotionEnabled()
    {
        return useRootMotionLocomotion && locomotion != null;
    }

    private void ConfigureGroundReliefTolerance()
    {
        if (!relaxGroundReliefTolerance || locomotion == null || groundReliefToleranceApplied)
        {
            return;
        }

        previousUccStickToGround = locomotion.StickToGround;
        previousUccMaxStepHeight = locomotion.MaxStepHeight;
        previousUccSlopeLimit = locomotion.SlopeLimit;
        previousUccStickToGroundDistance = locomotion.StickToGroundDistance;

        groundReliefToleranceApplied = true;
        RefreshGroundReliefTolerance(immediate: true);
    }

    private void RefreshGroundReliefTolerance(bool immediate)
    {
        if (!groundReliefToleranceApplied || !relaxGroundReliefTolerance || locomotion == null)
        {
            return;
        }

        GroundReliefTargets targets = ResolveGroundReliefTargets();
        float deltaTime = ResolveGroundReliefDeltaTime();
        float rate = Mathf.Max(0f, rootMotionGroundReliefAdaptationSpeed);
        float step = immediate || rate <= 0f ? float.PositiveInfinity : rate * deltaTime;

        locomotion.StickToGround = true;
        locomotion.MaxStepHeight = MoveReliefValue(locomotion.MaxStepHeight, targets.stepHeight, step);
        locomotion.SlopeLimit = MoveReliefValue(locomotion.SlopeLimit, targets.slopeLimit, step);
        locomotion.StickToGroundDistance = MoveReliefValue(
            locomotion.StickToGroundDistance,
            targets.stickToGroundDistance,
            step);
    }

    private GroundReliefTargets ResolveGroundReliefTargets()
    {
        float baseStepHeight = Mathf.Max(previousUccMaxStepHeight, Mathf.Max(0f, groundReliefMinStepHeight));
        float baseSlopeLimit = Mathf.Max(previousUccSlopeLimit, Mathf.Clamp(groundReliefMinSlopeLimit, 0f, 89f));
        float baseStickDistance = Mathf.Max(
            previousUccStickToGroundDistance,
            Mathf.Max(0f, groundReliefMinStickToGroundDistance));

        if (!adaptRootMotionGroundRelief || !IsRootMotionLocomotionEnabled())
        {
            return new GroundReliefTargets(baseStepHeight, baseSlopeLimit, baseStickDistance);
        }

        float reliefBlend = ResolveRootMotionGroundReliefBlend();
        float movingStepHeight = Mathf.Max(baseStepHeight, Mathf.Max(0f, rootMotionMovingStepHeight));
        float movingSlopeLimit = Mathf.Max(baseSlopeLimit, Mathf.Clamp(rootMotionMovingSlopeLimit, 0f, 89f));
        float idleStickDistance = Mathf.Max(baseStickDistance, Mathf.Max(0f, rootMotionIdleStickToGroundDistance));
        float movingStickDistance = Mathf.Max(idleStickDistance, Mathf.Max(0f, rootMotionMovingStickToGroundDistance));

        return new GroundReliefTargets(
            Mathf.Lerp(baseStepHeight, movingStepHeight, reliefBlend),
            Mathf.Lerp(baseSlopeLimit, movingSlopeLimit, reliefBlend),
            Mathf.Lerp(idleStickDistance, movingStickDistance, reliefBlend));
    }

    private float ResolveRootMotionGroundReliefBlend()
    {
        float inputMagnitude = Mathf.Clamp01(currentWorldMoveInput.magnitude);
        float speedMagnitude = 0f;
        if (locomotion != null)
        {
            Vector3 planarVelocity = locomotion.Velocity;
            planarVelocity.y = 0f;
            speedMagnitude = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.01f, runPresentationSpeed));
        }

        return Mathf.Clamp01(Mathf.Max(inputMagnitude, speedMagnitude));
    }

    private float MoveReliefValue(float current, float target, float maxDelta)
    {
        if (float.IsPositiveInfinity(maxDelta))
        {
            return target;
        }

        return Mathf.MoveTowards(current, target, maxDelta);
    }

    private float ResolveGroundReliefDeltaTime()
    {
        float deltaTime = Time.inFixedTimeStep ? Time.fixedDeltaTime : Time.deltaTime;
        return Mathf.Max(deltaTime, 0.0001f);
    }

    private void RestoreGroundReliefTolerance()
    {
        if (!groundReliefToleranceApplied || locomotion == null)
        {
            groundReliefToleranceApplied = false;
            return;
        }

        locomotion.StickToGround = previousUccStickToGround;
        locomotion.MaxStepHeight = previousUccMaxStepHeight;
        locomotion.SlopeLimit = previousUccSlopeLimit;
        locomotion.StickToGroundDistance = previousUccStickToGroundDistance;
        groundReliefToleranceApplied = false;
    }

    private struct GroundReliefTargets
    {
        public readonly float stepHeight;
        public readonly float slopeLimit;
        public readonly float stickToGroundDistance;

        public GroundReliefTargets(float stepHeight, float slopeLimit, float stickToGroundDistance)
        {
            this.stepHeight = stepHeight;
            this.slopeLimit = slopeLimit;
            this.stickToGroundDistance = stickToGroundDistance;
        }
    }

    private void RestoreRigidbody()
    {
        if (!configureRigidbodyForOpsive || rb == null)
        {
            return;
        }

        rb.useGravity = previousUseGravity;
        rb.isKinematic = previousIsKinematic;
        rb.collisionDetectionMode = previousCollisionMode;
    }

    private void SetAnimatorFloat(string parameter, float value)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(parameter, value);
        }
    }

    private void SetAnimatorBool(string parameter, bool value)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(parameter, value);
        }
    }

    private void SetAnimatorInteger(string parameter, int value)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Int))
        {
            animator.SetInteger(parameter, value);
        }
    }

    private void SetAnimatorTrigger(string parameter)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(parameter);
        }
    }

    private bool HasAnimatorParameter(string parameter, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameter))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == type && parameters[i].name == parameter)
            {
                return true;
            }
        }

        return false;
    }

    private void WarnOnce(ref bool flag, string message)
    {
        if (flag)
        {
            return;
        }

        flag = true;
        Debug.LogWarning(message, this);
    }
}
