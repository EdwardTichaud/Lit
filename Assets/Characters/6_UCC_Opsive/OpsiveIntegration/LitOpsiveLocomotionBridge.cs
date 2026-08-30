using System.Collections;
using Opsive.Shared.Events;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character.Abilities.Items;
using UnityEngine;

public enum LocomotionPresentationState
{
    Idle,
    Starting,
    Moving,
    Stopping,
    Pivoting
}

// Runtime bridge between Lit's gameplay facade and Opsive UCC locomotion.
[RequireComponent(typeof(UltimateCharacterLocomotion))]
[RequireComponent(typeof(UltimateCharacterLocomotionHandler))]
[RequireComponent(typeof(LitOpsivePlayerInput))]
public partial class LitOpsiveLocomotionBridge : MonoBehaviour
{
    private const string SpeedChangeInputName = "Change Speeds";
    private const string CrouchInputName = "Crouch";

    [SerializeField] private SquadCharacterController squadController;
    [SerializeField] private UltimateCharacterLocomotion locomotion;
    [SerializeField] private UltimateCharacterLocomotionHandler locomotionHandler;
    [SerializeField] private LitOpsivePlayerInput playerInput;
    [SerializeField] private LitOpsiveLookSource lookSource;
    [SerializeField] private Animator animator;
    [SerializeField] private AnimatorMonitor animatorMonitor;
    [SerializeField] private PlayerScriptedJumpController scriptedJumpController;

    [Header("Ladder Traversal Diagnostics")]
    [SerializeField, Tooltip("Logs the requested and observed UCC pose while a scripted traversal is active. Development aid only.")]
    private bool logScriptedTraversalDiagnostics;
    [SerializeField, Min(1), Tooltip("Number of physics ticks between two traversal diagnostic samples.")]
    private int scriptedTraversalDiagnosticTickInterval = 8;
    [SerializeField, Min(0f), Tooltip("Warn when another system has moved the actor farther than this from the last requested traversal pose.")]
    private float scriptedTraversalExternalCorrectionDistance = 0.02f;
    [SerializeField, Range(0f, 45f), Tooltip("Warn when another system has rotated the actor farther than this from the last requested traversal pose.")]
    private float scriptedTraversalExternalCorrectionDegrees = 1f;

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
    [SerializeField, Min(0f)] private float rootMotionSpeedMultiplier = 1.04f;
    [SerializeField, Min(0f)] private float rootMotionRotationMultiplier = 1f;
    [SerializeField, Tooltip("Lets UCC/look-source rotation drive regular root-motion locomotion so directional clips cannot lock the body facing. Pivot clips can still use authored root rotation.")]
    private bool preferLookSourceRotationForRootMotionLocomotion = true;
    [SerializeField, Tooltip("Allows authored root rotation during start/stop clips. Keep disabled when clips contain little or conflicting deltaRotation.")]
    private bool allowRootMotionRotationDuringStartStop = false;
    [SerializeField, Tooltip("Zeros tiny idle root-motion displacement so authored idle drift cannot vibrate the capsule at rest.")]
    private bool suppressIdleRootMotionPosition = true;
    [SerializeField, Min(0f), Tooltip("Maximum planar speed still considered idle for root-motion drift suppression.")]
    private float idleRootMotionVelocityThreshold = 0.06f;
    [SerializeField, Tooltip("Applies additional root-motion tuning based on the active grounded animation phase.")]
    private bool useRootMotionPhaseMultipliers = true;
    [SerializeField, Min(0f)] private float rootMotionLoopSpeedScale = 1.02f;
    [SerializeField, Min(0f)] private float rootMotionLoopRotationScale = 1f;
    [SerializeField, Min(0f)] private float rootMotionStartSpeedScale = 1.18f;
    [SerializeField, Min(0f)] private float rootMotionStartRotationScale = 1f;
    [SerializeField, Min(0f)] private float rootMotionStopSpeedScale = 0.7f;
    [SerializeField, Min(0f)] private float rootMotionStopRotationScale = 0.94f;
    [SerializeField, Min(0f)] private float rootMotionPivotSpeedScale = 0.88f;
    [SerializeField, Min(0f)] private float rootMotionPivotRotationScale = 1.12f;
    [SerializeField, Tooltip("Keeps Animator.applyRootMotion enabled while UCC reads deltaPosition/deltaRotation through AnimatorMonitor.")]
    private bool preserveAnimatorRootMotion = true;
    [SerializeField, Tooltip("Restores the previous UCC root motion settings when the bridge is disabled.")]
    private bool restoreRootMotionSettingsOnDisable = true;
    [SerializeField, Tooltip("Reapplies root motion multipliers every frame so Play Mode inspector tuning is felt immediately.")]
    private bool refreshRootMotionSettingsEveryFrame = true;
    [SerializeField, Tooltip("Legacy directional root-motion input. Keep disabled for forward-only grounded locomotion.")]
    private bool driveDirectionalRootMotionInput = false;
    [SerializeField, Tooltip("When the look source already points toward movement, feeds UCC forward input to avoid double-rotating movement space.")]
    private bool useLookSourceForwardInputForRootMotion = true;

    [Header("Run Start Response")]
    [SerializeField, Tooltip("Applies a short, collision-respecting UCC impulse when a held sprint starts or resumes after an action.")]
    private bool enableRunStartResponse = true;
    [SerializeField, Min(0f), Tooltip("Maximum planar velocity added by the first run step.")]
    private float runStartVelocityBonus = 0.55f;
    [SerializeField, Min(0f), Tooltip("Prevents repeated input reconciliation from stacking several run-start impulses.")]
    private float runStartResponseCooldown = 0.25f;
    [SerializeField, Min(0f), Tooltip("No run-start impulse is applied once this planar speed is already reached.")]
    private float runStartResponseMaximumPlanarSpeed = 4.25f;
    [SerializeField, Tooltip("Logs run-start and external-lock handoffs when troubleshooting locomotion.")]
    private bool logLocomotionResponseDiagnostics;

    [SerializeField, Tooltip("Add Lit/UCC companion bridges at runtime so interaction, damage and follower systems can respect UCC state without prefab edits.")]
    private bool autoInstallCompanionBridges = true;
    [SerializeField, Range(0f, 0.5f)] private float movementDeadZone = 0.08f;

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
    [SerializeField, Min(0f), Tooltip("Vitesse descendante minimale appliquee lorsqu'un BasicSkill aerien demande un atterrissage.")]
    private float combatSkillLandingSpeed = 14f;
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
    [SerializeField] private string combatMoveMagnitudeParam = "CombatMoveMagnitude";
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
    // Kept independently from facing. A locked target owns body yaw, but a
    // landing roll must still honor the last direction the player actually held.
    private Vector2 lastExplicitWorldMoveInput;
    private float lastExplicitWorldMoveInputTime = float.NegativeInfinity;
    private bool sprintPressed;
    private bool warnedMissingSpeedChange;
    private bool warnedMissingHeightChange;
    private bool warnedFlightStartRejected;
    private LitUccFlightAbility flightAbility;
    private Vector3 lastPlanarDirection = Vector3.forward;
    private Vector3 lastPosition;
    private bool hasLastPosition;
    private int scriptedTraversalLockCount;
    private bool scriptedTraversalInputDisabled;
    // The traversal coroutine resumes after UCC's fixed simulation. It submits
    // the complete authored pose here; no Update or LateUpdate may compete for
    // this transform while the lock is active.
    private bool scriptedTraversalPoseActive;
    private Vector3 scriptedTraversalPosition;
    private Quaternion scriptedTraversalRotation;
    private int scriptedTraversalPoseTick;
    private Coroutine scriptedTraversalReleaseRoutine;
    private Coroutine externalImpulseLockRoutine;
    private Ability[] scriptedTraversalAbilities;
    private bool[] scriptedTraversalAbilityEnabledStates;
    private ItemAbility[] scriptedTraversalItemAbilities;
    private bool[] scriptedTraversalItemAbilityEnabledStates;
    private bool previousLocomotionUseGravity;
    private bool scriptedTraversalGravityPrepared;
    private bool scriptedTraversalGroundingPrepared;
    private bool previousScriptedTraversalStickToGround;
    private bool previousScriptedTraversalForceStickToGround;
    private int combatAirborneHoldCount;
    private bool combatAirborneHoldPreviousUseGravity;
    private int externalLockCount;
    private bool externalLockInputDisabled;
    private bool progressiveExternalStopActive;
    private bool hasPlayerActionRootMotionMode;
    private PlayerActionRootMotionMode playerActionRootMotionMode;
    private bool suppressPlayerActionRootMotionRotation;
    private bool allowPlayerActionAirborneRootMotion;
    private readonly RaycastHit[] motionHandoffProbeHits = new RaycastHit[8];
    private bool runStartResponseArmed;
    private bool wasSprintMoving;
    private float nextRunStartResponseTime;
    private bool combatLockActive;
    private Transform combatLockTarget;
    private Vector2 combatLockLocalInput;
    private float combatOrbitRadius = -1f;
    private bool combatDirectionalEvasionFacing;
    [Header("Combat Lock Motion")]
    [SerializeField, Min(1f), Tooltip("Vitesse maximale du face-a-face. Les actions et evasions restent immediates.")]
    private float combatFacingSpeedDegreesPerSecond = 900f;
    [SerializeField, Min(0f), Tooltip("Correction radiale appliquee pendant un strafe lateral pour conserver le rayon d'orbite initial.")]
    private float combatOrbitRadiusCorrection = 0.7f;
    [SerializeField, Min(0f), Tooltip("Correction radiale maximale ajoutee a l'intention de strafe.")]
    private float combatOrbitMaximumCorrection = 0.35f;
    [SerializeField, Range(0f, 1f), Tooltip("Valeur verticale maximale pour considerer l'intention comme un strafe lateral pur.")]
    private float combatOrbitLateralVerticalThreshold = 0.2f;
    [SerializeField, Tooltip("Active les traces de repere lock pour diagnostiquer une animation ou une direction incorrecte.")]
    private bool logCombatLockMotionDiagnostics;
    private Vector3 smoothedCombatFacingDirection;
    private bool hasSmoothedCombatFacingDirection;
    private bool combatIdlePresentationActive;
    private static readonly int CombatLocomotionStateHash = Animator.StringToHash("Base Layer.CombatLocomotion");
    private static readonly int CombatIdleStateHash = Animator.StringToHash("Base Layer.TwinSword_Idle_Root");

    private enum RootMotionPhase
    {
        Locomotion,
        Start,
        Stop,
        Pivot,
        Jump,
        Combat,
        Other
    }

    public bool IsDriving => isActiveAndEnabled &&
                             driveFromSquadFacade &&
                             locomotion != null && locomotion.isActiveAndEnabled &&
                             locomotionHandler != null && locomotionHandler.isActiveAndEnabled;
    public bool IsScriptedTraversalActive => scriptedTraversalLockCount > 0 || scriptedTraversalReleaseRoutine != null;
    public bool IsExternalLockActive => externalLockCount > 0;
    public bool IsInputSuppressedByUcc => IsScriptedTraversalActive || IsExternalLockActive;
    public bool IsFlightActive => IsFlightModeActive;
    public bool Grounded => locomotion != null && locomotion.Grounded;
    public bool ShouldPreserveAnimatorRootMotion => useRootMotionLocomotion && preserveAnimatorRootMotion;
    public Vector3 Velocity => locomotion != null ? locomotion.Velocity : Vector3.zero;
    public Vector3 PlanarVelocity => Vector3.ProjectOnPlane(Velocity, transform.up);
    public float VerticalVelocity => Vector3.Dot(Velocity, transform.up);
    public Vector3 WorldPosition => locomotion != null ? locomotion.transform.position : Vector3.zero;
    public Vector2 CurrentWorldMoveInput => currentWorldMoveInput;
    public Vector2 LastExplicitWorldMoveInput => lastExplicitWorldMoveInput;
    public bool HasRecentExplicitWorldMoveInput(float memorySeconds)
    {
        return lastExplicitWorldMoveInput.sqrMagnitude > movementDeadZone * movementDeadZone &&
               Time.unscaledTime - lastExplicitWorldMoveInputTime <= Mathf.Max(0f, memorySeconds);
    }
    public bool IsCombatLockActive => combatLockActive;
    public bool IsCombatDirectionalEvasionFacing => combatDirectionalEvasionFacing;
    public Vector2 CombatLockLocalInput => combatLockLocalInput;
    private bool UseForwardOnlyGroundedLocomotion => useForwardOnlyGroundedLocomotion && !combatLockActive;
    public string CurrentRootMotionPhase => ResolveCurrentRootMotionPhase().ToString();
    public LocomotionPresentationState CurrentLocomotionPresentationState => groundedPresentationState;
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
        runStartVelocityBonus = Mathf.Max(0f, runStartVelocityBonus);
        runStartResponseCooldown = Mathf.Max(0f, runStartResponseCooldown);
        runStartResponseMaximumPlanarSpeed = Mathf.Max(0f, runStartResponseMaximumPlanarSpeed);
        idleRootMotionVelocityThreshold = Mathf.Max(0f, idleRootMotionVelocityThreshold);
        combatFacingSpeedDegreesPerSecond = Mathf.Max(1f, combatFacingSpeedDegreesPerSecond);
        combatOrbitRadiusCorrection = Mathf.Max(0f, combatOrbitRadiusCorrection);
        combatOrbitMaximumCorrection = Mathf.Max(0f, combatOrbitMaximumCorrection);
        combatOrbitLateralVerticalThreshold = Mathf.Clamp01(combatOrbitLateralVerticalThreshold);
        groundReliefMinStepHeight = Mathf.Max(0f, groundReliefMinStepHeight);
        groundReliefMinSlopeLimit = Mathf.Clamp(groundReliefMinSlopeLimit, 0f, 89f);
        groundReliefMinStickToGroundDistance = Mathf.Max(0f, groundReliefMinStickToGroundDistance);
        rootMotionMovingStepHeight = Mathf.Max(0f, rootMotionMovingStepHeight);
        rootMotionMovingSlopeLimit = Mathf.Clamp(rootMotionMovingSlopeLimit, 0f, 89f);
        rootMotionMovingStickToGroundDistance = Mathf.Max(0f, rootMotionMovingStickToGroundDistance);
        rootMotionIdleStickToGroundDistance = Mathf.Max(0f, rootMotionIdleStickToGroundDistance);
        rootMotionGroundReliefAdaptationSpeed = Mathf.Max(0f, rootMotionGroundReliefAdaptationSpeed);
        scriptedTraversalDiagnosticTickInterval = Mathf.Max(1, scriptedTraversalDiagnosticTickInterval);
        scriptedTraversalExternalCorrectionDistance = Mathf.Max(0f, scriptedTraversalExternalCorrectionDistance);
        scriptedTraversalExternalCorrectionDegrees = Mathf.Clamp(scriptedTraversalExternalCorrectionDegrees, 0f, 45f);
        ValidateOrientationFeelSettings();
        ValidateObstacleTraversalSettings();
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

    public void SetPlayerActionRootMotionMode(
        PlayerActionRootMotionMode mode,
        bool suppressRootRotation = false,
        bool allowAirborneRootMotion = false)
    {
        hasPlayerActionRootMotionMode = true;
        playerActionRootMotionMode = mode;
        suppressPlayerActionRootMotionRotation = suppressRootRotation;
        allowPlayerActionAirborneRootMotion = allowAirborneRootMotion;
        RefreshRootMotionLocomotionSettings();
    }

    public void ClearPlayerActionRootMotionMode()
    {
        if (!hasPlayerActionRootMotionMode)
        {
            return;
        }

        hasPlayerActionRootMotionMode = false;
        suppressPlayerActionRootMotionRotation = false;
        allowPlayerActionAirborneRootMotion = false;
        RefreshRootMotionLocomotionSettings();
    }

    public void RefreshLocomotionPresentation()
    {
        if (IsDriving && !IsInputSuppressedByUcc)
        {
            UpdateAnimatorParameters();
        }
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

        // The movement input may have arrived just before sprint in this frame.
        // Refresh the authored locomotion parameters now so MoveStart chooses
        // Run_Start instead of spending one frame in Walk_Start.
        if (pressed && currentWorldMoveInput.sqrMagnitude > movementDeadZone * movementDeadZone)
        {
            UpdateAnimatorParameters();
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

        return scriptedJumpController != null && scriptedJumpController.TryStartJump(worldInput, hasWorldInput);
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

    /// <summary>Coupe les nouvelles entrees tout en laissant le filtre de deplacement freiner naturellement.</summary>
    public bool BeginExternalLockWithProgressiveStop(bool disableGameplayInput = true, bool stopActiveAbilities = false)
    {
        ResolveReferences();
        if (!CanDriveScriptedTraversal)
        {
            return false;
        }

        externalLockCount = Mathf.Max(0, externalLockCount) + 1;
        TraceLocomotionResponse("External lock acquired | count=" + externalLockCount + ".");
        if (externalLockCount > 1)
        {
            return true;
        }

        if (stopActiveAbilities && locomotion != null)
        {
            locomotion.StopAllAbilities(false);
        }

        if (disableGameplayInput)
        {
            EventHandler.ExecuteEvent<bool>(gameObject, "OnEnableGameplayInput", false);
            externalLockInputDisabled = true;
        }

        progressiveExternalStopActive = true;
        sprintPressed = false;
        ApplyWorldMoveInput(Vector2.zero);
        return true;
    }

    /// <summary>
    /// Starts a controlled descent without changing the actor pose. The ground
    /// contact remains fully owned by UCC, so an aerial BasicSkill can land
    /// safely from any height.
    /// </summary>
    public bool RequestCombatSkillLanding()
    {
        ResolveReferences();
        if (!IsDriving || locomotion == null || locomotion.Grounded)
        {
            return false;
        }

        float currentVerticalSpeed = Vector3.Dot(locomotion.Velocity, transform.up);
        float requestedVerticalSpeed = -Mathf.Max(0f, combatSkillLandingSpeed);
        if (currentVerticalSpeed > requestedVerticalSpeed)
        {
            locomotion.AddForce(transform.up * (requestedVerticalSpeed - currentVerticalSpeed), 1, false);
        }

        return true;
    }

    /// <summary>
    /// Applies a small, collision-safe planar deceleration while a presentation
    /// is finishing. It never changes vertical velocity, position or rotation.
    /// </summary>
    public void ApplyPlanarHandoffDamping(float decelerationPerSecond)
    {
        if (locomotion == null || decelerationPerSecond <= 0f || !locomotion.Grounded)
        {
            return;
        }

        Vector3 planarVelocity = PlanarVelocity;
        float speed = planarVelocity.magnitude;
        if (speed <= 0.001f)
        {
            return;
        }

        float deltaSpeed = Mathf.Min(speed, decelerationPerSecond * Time.deltaTime);
        locomotion.AddForce(-planarVelocity.normalized * deltaSpeed, 1, false);
    }

    public bool IsMotionHandoffSettled(MotionHandoffProfile profile)
    {
        if (profile == null || locomotion == null || !locomotion.Grounded)
        {
            return false;
        }

        return PlanarVelocity.sqrMagnitude <= profile.planarSettledSpeed * profile.planarSettledSpeed &&
               Mathf.Abs(VerticalVelocity) <= profile.verticalSettledSpeed;
    }

    /// <summary>Returns true when a descending UCC capsule is close enough to the ground to enter its landing approach.</summary>
    public bool ShouldBeginMotionHandoff(MotionHandoffProfile profile)
    {
        if (profile == null || locomotion == null || locomotion.Grounded || VerticalVelocity >= -0.01f)
        {
            return false;
        }

        Vector3 up = locomotion.Up.sqrMagnitude > 0f ? locomotion.Up.normalized : transform.up;
        Vector3 origin = transform.position + up * 0.08f;
        float probeDistance = Mathf.Max(0.01f, profile.preLandingProbeDistance + 0.08f);
        int hitCount = Physics.RaycastNonAlloc(origin, -up, motionHandoffProbeHits, probeDistance, ~0, QueryTriggerInteraction.Ignore);
        float nearestGroundDistance = float.PositiveInfinity;
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit hit = motionHandoffProbeHits[index];
            if (hit.collider != null && !hit.collider.transform.IsChildOf(transform))
            {
                nearestGroundDistance = Mathf.Min(nearestGroundDistance, hit.distance);
            }
        }

        float leadDistance = Mathf.Max(0.08f, -VerticalVelocity * profile.preLandingLeadSeconds);
        return nearestGroundDistance <= Mathf.Min(profile.preLandingProbeDistance, leadDistance);
    }

    /// <summary>
    /// Suspends UCC gravity for an aerial combat action. Horizontal movement
    /// remains owned by UCC; only the vertical component is neutralized.
    /// Calls are reference counted so a queued replacement action cannot
    /// restore gravity while it still owns the hold.
    /// </summary>
    public bool BeginCombatAirborneHold()
    {
        ResolveReferences();
        if (!IsDriving || locomotion == null || locomotion.Grounded)
        {
            return false;
        }

        if (combatAirborneHoldCount == 0)
        {
            combatAirborneHoldPreviousUseGravity = locomotion.UseGravity;

        }

        combatAirborneHoldCount++;
        MaintainCombatAirborneHold();
        return true;
    }

    /// <summary>Releases one aerial combat hold and lets UCC resume gravity.</summary>
    public void EndCombatAirborneHold()
    {
        if (combatAirborneHoldCount <= 0)
        {
            combatAirborneHoldCount = 0;
            return;
        }

        combatAirborneHoldCount--;
        if (combatAirborneHoldCount > 0 || locomotion == null)
        {
            return;
        }

        if (!scriptedTraversalGravityPrepared)
        {
            locomotion.UseGravity = combatAirborneHoldPreviousUseGravity;
        }

        // Gravity resumes from the last authored air pose rather than from a
        // downward force accumulated during the suspended frames.
        locomotion.GravityAccumulation = 0f;
    }

    private void MaintainCombatAirborneHold()
    {
        if (combatAirborneHoldCount <= 0 || locomotion == null)
        {
            return;
        }

        locomotion.UseGravity = false;
        locomotion.GravityAccumulation = 0f;

        // A jump ability can still carry an upward or downward velocity for a
        // frame after the skill starts. Remove only that vertical component;
        // planar UCC movement remains untouched.
        Vector3 up = locomotion.Up.sqrMagnitude > 0f ? locomotion.Up.normalized : transform.up;
        float verticalSpeed = Vector3.Dot(locomotion.Velocity, up);
        if (Mathf.Abs(verticalSpeed) > 0.001f)
        {
            locomotion.AddForce(-up * verticalSpeed, 1, false);
        }
    }

    private void ClearCombatAirborneHolds()
    {
        while (combatAirborneHoldCount > 0)
        {
            EndCombatAirborneHold();
        }
    }

    public bool IsProgressiveStopComplete(float velocityThreshold)
    {
        float threshold = Mathf.Max(0f, velocityThreshold);
        return !progressiveExternalStopActive ||
               (currentWorldMoveInput.sqrMagnitude <= movementDeadZone * movementDeadZone &&
                PlanarVelocity.sqrMagnitude <= threshold * threshold);
    }

    public void CompleteProgressiveStop()
    {
        progressiveExternalStopActive = false;
        ForceZeroInput();
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

        progressiveExternalStopActive = false;

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
        return TryBeginScriptedTraversal(out _);
    }

    /// <summary>
    /// Starts a traversal lock while exposing why UCC rejected it.
    /// </summary>
    public bool TryBeginScriptedTraversal(out string failureReason)
    {
        ResolveReferences();
        if (!CanDriveScriptedTraversal)
        {
            failureReason = !isActiveAndEnabled
                ? "Le bridge UCC est desactive."
                : "UltimateCharacterLocomotion est introuvable sur le personnage.";
            return false;
        }

        // A new traversal may start from the final fixed tick of a previous
        // one. Keep its suspended UCC state instead of restoring it briefly.
        if (scriptedTraversalReleaseRoutine != null)
        {
            StopCoroutine(scriptedTraversalReleaseRoutine);
            scriptedTraversalReleaseRoutine = null;
        }

        scriptedTraversalLockCount = Mathf.Max(0, scriptedTraversalLockCount) + 1;
        if (scriptedTraversalLockCount > 1)
        {
            ForceZeroInput();
            failureReason = null;
            return true;
        }

        if (locomotion != null)
        {
            locomotion.StopAllAbilities(false);
        }

        SuppressGravityForScriptedTraversal();
        SuppressGroundingForScriptedTraversal();
        DisableAbilitiesForScriptedTraversal();
        ForceZeroInput();
        EventHandler.ExecuteEvent<bool>(gameObject, "OnEnableGameplayInput", false);
        scriptedTraversalInputDisabled = true;
        failureReason = null;
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

        // Keep the last authored pose through the following physics tick. This
        // prevents UCC from correcting the actor while Ladder_End is still
        // presenting its final contact pose.
        if (scriptedTraversalReleaseRoutine == null && isActiveAndEnabled)
        {
            scriptedTraversalReleaseRoutine = StartCoroutine(ReleaseScriptedTraversalAfterFixedStep());
        }
    }

    private IEnumerator ReleaseScriptedTraversalAfterFixedStep()
    {
        yield return new WaitForFixedUpdate();

        // The coroutine resumes after UCC's fixed pass. Reapply the final
        // route pose once, then hand normal simulation back on the next frame.
        ApplyStoredScriptedTraversalPose();
        scriptedTraversalReleaseRoutine = null;
        CompleteScriptedTraversalRelease();
    }

    private void CompleteScriptedTraversalRelease()
    {
        scriptedTraversalPoseActive = false;

        RestoreAbilitiesAfterScriptedTraversal();
        RestoreGravityAfterScriptedTraversal();
        RestoreGroundingAfterScriptedTraversal();
        RefreshGroundReliefTolerance(immediate: true);
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

    /// <summary>
    /// Termine les verrous transitoires quand la scene qui les a crees vient
    /// d'etre dechargee. Une echelle ou une impulsion ne peut pas survivre a
    /// un changement de zone et ne doit jamais garder l'input/camera bloque.
    /// </summary>
    public void ClearTransientLocksForSceneTransition()
    {
        ResolveReferences();

        // Une scene dechargee peut avoir desactive le moteur UCC ou son handler
        // (cinematique, interaction, transition). Le personnage persiste entre
        // les scenes : remettre explicitement sa pile de locomotion en marche
        // avant de lui rendre le controle.
        if (locomotion != null)
        {
            locomotion.enabled = true;
        }

        if (locomotionHandler != null)
        {
            locomotionHandler.enabled = true;
        }

        if (animator != null)
        {
            animator.enabled = true;
            // UCC recoit son delta de root motion via AnimatorMonitor.
            // Pendant l'overlay de transition il n'y a parfois pas de camera
            // de jeu active : le culling ne doit jamais interrompre ce flux.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        if (animatorMonitor != null)
        {
            animatorMonitor.enabled = true;
        }

        CancelObstacleTraversal();

        if (externalImpulseLockRoutine != null)
        {
            StopCoroutine(externalImpulseLockRoutine);
            externalImpulseLockRoutine = null;
        }

        RestoreAbilitiesAfterScriptedTraversal();
        RestoreGravityAfterScriptedTraversal();
        RestoreGroundingAfterScriptedTraversal();
        if (scriptedTraversalReleaseRoutine != null)
        {
            StopCoroutine(scriptedTraversalReleaseRoutine);
            scriptedTraversalReleaseRoutine = null;
        }
        externalLockCount = 0;
        scriptedTraversalLockCount = 0;
        scriptedTraversalPoseActive = false;
        // Le portail peut avoir decharge l'objet qui possedait le verrou avant
        // qu'il ait pu le liberer. Rejouer explicitement l'evenement est
        // idempotent et garantit que UCC accepte a nouveau l'input local.
        EventHandler.ExecuteEvent<bool>(gameObject, "OnEnableGameplayInput", true);

        externalLockInputDisabled = false;
        scriptedTraversalInputDisabled = false;
        ConfigureRigidbody();
        RefreshRootMotionLocomotionSettings();
        ForceZeroInput();
        AttachLookSourceIfNeeded(true);
        RequestRunStartResponse();
        TraceLocomotionResponse("External lock released; waiting for held input reconciliation.");
    }

    /// <summary>
    /// Arms one small sprint-start response for the next valid held movement.
    /// Used by action/cinematic handoffs; it never bypasses UCC collision.
    /// </summary>
    public void RequestRunStartResponse()
    {
        runStartResponseArmed = true;
        wasSprintMoving = false;
    }

    public void ApplyScriptedTraversalPose(Vector3 position, Quaternion rotation)
    {
        ResolveReferences();
        if (locomotion == null || !IsScriptedTraversalActive)
        {
            return;
        }

        TraceExternalTraversalCorrection();
        scriptedTraversalPosition = position;
        scriptedTraversalRotation = rotation;
        scriptedTraversalPoseActive = true;
        ApplyStoredScriptedTraversalPose();
    }

    private void ApplyStoredScriptedTraversalPose()
    {
        if (!scriptedTraversalPoseActive || locomotion == null)
        {
            return;
        }

        locomotion.SetPositionAndRotation(scriptedTraversalPosition, scriptedTraversalRotation, false, false);
        lastPosition = scriptedTraversalPosition;
        hasLastPosition = true;
        TraceAppliedTraversalPose();
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

    /// <summary>
    /// Pose un personnage pour une Timeline pendant qu'un verrou externe coupe
    /// volontairement l'input UCC. Cette operation ne libere jamais le verrou.
    /// </summary>
    public bool SetCinematicPositionAndRotation(
        Vector3 position,
        Quaternion rotation,
        bool stopActiveAbilities,
        bool logDiagnostics = true)
    {
        ResolveReferences();
        if (logDiagnostics)
        {
            Debug.Log("[LightSkill Debug] UCC placement | character='" + name + "' | avant=" + transform.position +
                      " | cible=" + position + " | isDriving=" + IsDriving + " | externalLock=" + IsExternalLockActive +
                      " | locomotion=" + (locomotion != null) + ".", this);
        }
        if (locomotion == null)
        {
            if (logDiagnostics)
            {
                Debug.LogWarning("[LightSkill Debug] UCC placement refuse : locomotion absente.", this);
            }
            return false;
        }

        locomotion.SetPositionAndRotation(position, rotation, false, stopActiveAbilities);
        lastPosition = position;
        hasLastPosition = true;
        ForceZeroInput();
        if (logDiagnostics)
        {
            Debug.Log("[LightSkill Debug] UCC placement termine | apres=" + transform.position +
                      " | externalLock=" + IsExternalLockActive + ".", this);
        }
        return true;
    }

    /// <summary>
    /// Applies one relative Timeline root-motion sample while the cinematic
    /// owns UCC. The current root is deliberately used as the base so a pooled
    /// cinematic can never reuse an old world-space origin.
    /// </summary>
    public bool ApplyCinematicRootMotion(Vector3 worldDeltaPosition, Quaternion deltaRotation)
    {
        ResolveReferences();
        if (locomotion == null)
        {
            return false;
        }

        Vector3 nextPosition = locomotion.transform.position + worldDeltaPosition;
        Quaternion nextRotation = deltaRotation * locomotion.transform.rotation;
        locomotion.SetPositionAndRotation(nextPosition, nextRotation, false, false);
        lastPosition = nextPosition;
        hasLastPosition = true;
        ForceZeroInput();
        return true;
    }

    /// <summary>Requests combat-facing through the single lock-motion authority.</summary>
    public bool SetActionFacingDirection(Vector3 worldDirection)
    {
        return SetCombatFacingDirection(worldDirection);
    }

    /// <summary>
    /// Updates the UCC look source without writing the actor Transform. Under
    /// lock, root-motion rotation is disabled and this is the sole yaw intent.
    /// </summary>
    public bool SetCombatFacingDirection(Vector3 worldDirection)
    {
        ResolveReferences();
        worldDirection.y = 0f;
        if (!IsDriving || locomotion == null || worldDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 direction = worldDirection.normalized;
        smoothedCombatFacingDirection = direction;
        hasSmoothedCombatFacingDirection = true;
        ForceOrientationLookDirection(direction);
        return true;
    }

    /// <summary>
    /// Teleporte le personnage lors d'un changement de zone. Contrairement a
    /// un simple correctif de position, cette operation avertit UCC que
    /// l'Animator, les capacites et la camera doivent etre synchronises
    /// immediatement avec la nouvelle pose.
    /// </summary>
    public bool TeleportForSceneTransition(Vector3 position, Quaternion rotation)
    {
        ResolveReferences();
        if (locomotion == null)
        {
            return false;
        }

        // La scene precedente peut avoir desactive provisoirement le handler.
        // La remise en etat doit preceder le test et le snap UCC.
        ClearTransientLocksForSceneTransition();
        if (!IsDriving)
        {
            return false;
        }

        locomotion.SetPositionAndRotation(position, rotation, snapAnimator: true, stopAllAbilities: true);
        Physics.SyncTransforms();
        lastPosition = position;
        hasLastPosition = true;
        ForceZeroInput();
        AttachLookSourceIfNeeded(true);
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

    /// <summary>
    /// Impulsion auteurisee qui bloque les entrees jusqu'au prochain atterrissage.
    /// La borne maximale garantit qu'un sol manquant ne verrouille jamais Lucian.
    /// </summary>
    public bool AddExternalImpulseUntilGrounded(
        Vector3 worldImpulse,
        ForceMode forceMode,
        float minimumInputLockSeconds,
        float maximumInputLockSeconds,
        float airborneInertiaSeconds = 0f,
        float airborneInertiaEndSpeedMultiplier = 0.3f)
    {
        ResolveReferences();
        if (!IsDriving || locomotion == null || worldImpulse.sqrMagnitude <= 0f)
        {
            return false;
        }

        bool scaleByMass = forceMode != ForceMode.VelocityChange && forceMode != ForceMode.Acceleration;
        locomotion.AddForce(worldImpulse, 1, scaleByMass);

        if (externalImpulseLockRoutine != null)
        {
            StopCoroutine(externalImpulseLockRoutine);
            externalImpulseLockRoutine = null;
            EndExternalLock();
        }

        if (!BeginExternalLock(disableGameplayInput: true, stopActiveAbilities: false))
        {
            return true;
        }

        externalImpulseLockRoutine = StartCoroutine(EndExternalImpulseLockWhenGrounded(
            Mathf.Max(0f, minimumInputLockSeconds),
            Mathf.Max(0.1f, maximumInputLockSeconds),
            Vector3.ProjectOnPlane(worldImpulse, transform.up),
            Mathf.Max(0f, airborneInertiaSeconds),
            Mathf.Clamp01(airborneInertiaEndSpeedMultiplier)));
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
        RegisterExternalDriver();
        AttachLookSourceIfNeeded(true);
        ApplyWorldMoveInput(currentWorldMoveInput);
    }

    private void OnDisable()
    {
        CancelObstacleTraversal();
        ClearCombatAirborneHolds();
        if (scriptedTraversalReleaseRoutine != null)
        {
            StopCoroutine(scriptedTraversalReleaseRoutine);
            scriptedTraversalReleaseRoutine = null;
        }
        RestoreAbilitiesAfterScriptedTraversal();
        RestoreGravityAfterScriptedTraversal();
        RestoreGroundingAfterScriptedTraversal();
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
        scriptedTraversalPoseActive = false;
        StopBridgeInput();
        SetLitAnimatorSpeedParameterOverride(false);
        UnregisterExternalDriver();
        RestoreGroundedFeelProfile();
        RestoreRootMotionLocomotion();
        ResetOrientationFeelState();
        ResetGroundedFeelState();
        RestoreGroundReliefTolerance();
        RestoreRigidbody();
    }

    private void Update()
    {
        RefreshRootMotionLocomotionSettings();
        RefreshGroundReliefTolerance(immediate: false);
        TickLocomotionDiagnostics();
        MaintainCombatAirborneHold();

        if (!IsDriving && !IsInputSuppressedByUcc)
        {
            return;
        }

        if (IsInputSuppressedByUcc)
        {
            if (progressiveExternalStopActive)
            {
                ApplyWorldMoveInput(Vector2.zero);
                UpdateAnimatorParameters();
                RefreshSquadFacadeSystems();
                return;
            }

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
        MaintainCombatLockFacing();
        UpdateFlightMode();
        UpdateAnimatorParameters();
        RefreshSquadFacadeSystems();
    }

    private void LateUpdate()
    {
        // UCC abilities can update after this bridge depending on Script
        // Execution Order. Repeat the vertical neutralization after their
        // frame work so StayAirborne remains visually stable.
        MaintainCombatAirborneHold();
    }

    /// <summary>Starts target-relative locomotion for a manual combat lock.</summary>
    public void SetCombatLockTarget(Transform target)
    {
        bool wasCombatLockActive = combatLockActive;
        bool targetChanged = combatLockTarget != target;
        combatLockActive = target != null;
        combatLockTarget = target;
        if (targetChanged || wasCombatLockActive != combatLockActive)
        {
            // A new target invalidates the temporary direction selected by an
            // evasion. Reapplying the same target every frame must not.
            combatDirectionalEvasionFacing = false;
            hasSmoothedCombatFacingDirection = false;
            combatIdlePresentationActive = false;
            ResetCombatOrbit();
        }

        SetAnimatorBool("CombatStrafeActive", combatLockActive);
        if (combatLockActive)
        {
            MaintainCombatLockFacing();
        }
        else
        {
            SetCombatAnimatorInput(Vector2.zero);
        }
    }

    /// <summary>Clears target-relative locomotion without discarding the current gameplay input.</summary>
    public void ClearCombatLockTarget()
    {
        if (!combatLockActive && combatLockTarget == null)
        {
            return;
        }

        combatLockActive = false;
        combatLockTarget = null;
        combatDirectionalEvasionFacing = false;
        hasSmoothedCombatFacingDirection = false;
        combatIdlePresentationActive = false;
        ResetCombatOrbit();
        SetAnimatorBool("CombatStrafeActive", false);
        SetCombatAnimatorInput(Vector2.zero);
    }

    /// <summary>
    /// Converts the raw movement axes into the locked target's local combat
    /// frame. This intentionally bypasses camera-relative free locomotion.
    /// </summary>
    public bool TryResolveCombatLockMove(Vector2 rawInput, out Vector2 worldInput)
    {
        worldInput = Vector2.zero;
        if (!combatLockActive || combatLockTarget == null)
        {
            return false;
        }

        Vector2 clampedInput = Vector2.ClampMagnitude(rawInput, 1f);
        if (clampedInput.sqrMagnitude <= movementDeadZone * movementDeadZone)
        {
            combatLockLocalInput = Vector2.zero;
            ResetCombatOrbit();
            SetCombatAnimatorInput(Vector2.zero);
            return true;
        }

        ExitCombatIdleForMovement();

        Vector3 radialOut = transform.position - combatLockTarget.position;
        radialOut.y = 0f;
        if (radialOut.sqrMagnitude <= 0.0001f)
        {
            radialOut = -transform.forward;
            radialOut.y = 0f;
        }

        if (radialOut.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        float currentRadius = radialOut.magnitude;
        radialOut /= currentRadius;
        Vector3 towardTarget = -radialOut;
        Vector3 rightAroundTarget = Vector3.Cross(radialOut, Vector3.up).normalized;
        bool lateralOnly = Mathf.Abs(clampedInput.x) > movementDeadZone &&
                           Mathf.Abs(clampedInput.y) <= combatOrbitLateralVerticalThreshold;
        float radialCorrection = 0f;
        if (lateralOnly)
        {
            if (combatOrbitRadius < 0f)
            {
                combatOrbitRadius = currentRadius;
            }

            float radiusError = currentRadius - combatOrbitRadius;
            radialCorrection = Mathf.Clamp(
                radiusError * combatOrbitRadiusCorrection,
                -combatOrbitMaximumCorrection,
                combatOrbitMaximumCorrection);
        }
        else
        {
            ResetCombatOrbit();
        }

        Vector3 relativeDirection = rightAroundTarget * clampedInput.x +
                                    towardTarget * (clampedInput.y + radialCorrection);
        if (relativeDirection.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        relativeDirection.Normalize();
        combatLockLocalInput = clampedInput;
        worldInput = new Vector2(relativeDirection.x, relativeDirection.z) * clampedInput.magnitude;
        SetCombatAnimatorInput(combatLockLocalInput);
        MaintainCombatLockFacing();

        if (logCombatLockMotionDiagnostics)
        {
            Debug.Log("[CombatLockMotion] raw=" + rawInput.ToString("F2") +
                      " local=" + combatLockLocalInput.ToString("F2") +
                      " radius=" + currentRadius.ToString("F2") +
                      " anchored=" + combatOrbitRadius.ToString("F2") +
                      " correction=" + radialCorrection.ToString("F2"), this);
        }

        return true;
    }

    /// <summary>Allows an authored dodge or jump to own facing until it completes.</summary>
    public void BeginDirectionalEvasionFacing(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        if (!combatLockActive || worldDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        combatDirectionalEvasionFacing = true;
        ResetCombatOrbit();
        SetCombatFacingDirection(worldDirection);
        RefreshRootMotionLocomotionSettings();
    }

    /// <summary>Returns yaw authority to the currently locked enemy.</summary>
    public void EndDirectionalEvasionFacing()
    {
        if (!combatDirectionalEvasionFacing)
        {
            return;
        }

        combatDirectionalEvasionFacing = false;
        MaintainCombatLockFacing();
        RefreshRootMotionLocomotionSettings();
    }

    private void MaintainCombatLockFacing()
    {
        if (IsScriptedTraversalActive || !combatLockActive || combatDirectionalEvasionFacing || combatLockTarget == null)
        {
            return;
        }

        Vector3 targetDirection = combatLockTarget.position - transform.position;
        targetDirection.y = 0f;
        if (targetDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        targetDirection.Normalize();
        if (!hasSmoothedCombatFacingDirection)
        {
            smoothedCombatFacingDirection = targetDirection;
            hasSmoothedCombatFacingDirection = true;
        }
        else
        {
            float maximumRadians = combatFacingSpeedDegreesPerSecond * Mathf.Deg2Rad * Time.unscaledDeltaTime;
            smoothedCombatFacingDirection = Vector3.RotateTowards(
                smoothedCombatFacingDirection,
                targetDirection,
                maximumRadians,
                0f);
        }

        ForceOrientationLookDirection(smoothedCombatFacingDirection);
    }

    private void ResetCombatOrbit()
    {
        combatOrbitRadius = -1f;
    }

    private void SetCombatAnimatorInput(Vector2 localInput)
    {
        SetAnimatorFloat(horizontalMovementParam, localInput.x);
        SetAnimatorFloat(forwardMovementParam, localInput.y);
        SetAnimatorFloat(combatMoveMagnitudeParam, localInput.magnitude);
    }

    private void EnterCombatIdleFromLocomotion()
    {
        if (combatIdlePresentationActive || animator == null ||
            !animator.HasState(0, CombatLocomotionStateHash) ||
            !animator.HasState(0, CombatIdleStateHash))
        {
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != CombatLocomotionStateHash)
        {
            return;
        }

        animator.CrossFade(CombatIdleStateHash, 0.04f, 0);
        combatIdlePresentationActive = true;
    }

    private void ExitCombatIdleForMovement()
    {
        if (!combatIdlePresentationActive || animator == null ||
            !animator.HasState(0, CombatLocomotionStateHash))
        {
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool isTransitioningToCombatIdle = animator.IsInTransition(0) &&
                                           animator.GetNextAnimatorStateInfo(0).fullPathHash == CombatIdleStateHash;
        if (state.fullPathHash != CombatIdleStateHash && !isTransitioningToCombatIdle)
        {
            // A skill, guard or evasion took control since the visual idle.
            // Its own presentation controller is now the authority.
            combatIdlePresentationActive = false;
            return;
        }

        animator.CrossFade(CombatLocomotionStateHash, 0.03f, 0);
        combatIdlePresentationActive = false;
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

    private IEnumerator EndExternalImpulseLockWhenGrounded(
        float minimumDelay,
        float maximumDelay,
        Vector3 planarImpulse,
        float airborneInertiaSeconds,
        float airborneInertiaEndSpeedMultiplier)
    {
        float elapsed = 0f;
        bool leftGround = !Grounded;
        float initialPlanarSpeed = planarImpulse.magnitude;
        Vector3 inertiaDirection = initialPlanarSpeed > 0.0001f
            ? planarImpulse / initialPlanarSpeed
            : Vector3.zero;
        while (elapsed < maximumDelay)
        {
            elapsed += Time.unscaledDeltaTime;
            leftGround |= !Grounded;

            MaintainAirborneImpulseInertia(
                elapsed,
                inertiaDirection,
                initialPlanarSpeed,
                airborneInertiaSeconds,
                airborneInertiaEndSpeedMultiplier);

            // Une petite impulsion peut rester rapportee Grounded par UCC. Dans ce
            // cas, ce sol deja detecte est une restitution legitime plutot qu'un
            // verrouillage jusqu'a la borne de secours.
            if (elapsed >= minimumDelay && Grounded && (leftGround || elapsed >= minimumDelay + 0.2f))
            {
                break;
            }

            yield return null;
        }

        externalImpulseLockRoutine = null;
        EndExternalLock();
    }

    private void MaintainAirborneImpulseInertia(
        float elapsed,
        Vector3 direction,
        float initialSpeed,
        float inertiaSeconds,
        float endSpeedMultiplier)
    {
        if (locomotion == null || Grounded || inertiaSeconds <= 0f ||
            direction.sqrMagnitude <= 0.0001f || initialSpeed <= 0f)
        {
            return;
        }

        float deceleration = Mathf.Clamp01(elapsed / inertiaSeconds);
        float desiredSpeed = Mathf.Lerp(initialSpeed, initialSpeed * endSpeedMultiplier, deceleration);
        float currentForwardSpeed = Vector3.Dot(PlanarVelocity, direction);
        float missingSpeed = desiredSpeed - currentForwardSpeed;
        if (missingSpeed > 0.01f)
        {
            // UCC neutralise l'input pendant un recul. Cette compensation
            // conserve l'elan auteurise sans accelerer au-dela de sa vitesse cible.
            locomotion.AddForce(direction * missingSpeed, 1, false);
        }
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
        if (locomotion == null || scriptedTraversalGravityPrepared)
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

    private void SuppressGroundingForScriptedTraversal()
    {
        if (locomotion == null || scriptedTraversalGroundingPrepared)
        {
            return;
        }

        // An inclined root makes UCC's normal world-up ground adhesion invalid.
        // Collision remains enabled, but the ladder owns the pose until release.
        previousScriptedTraversalStickToGround = locomotion.StickToGround;
        previousScriptedTraversalForceStickToGround = locomotion.ForceStickToGround;
        locomotion.StickToGround = false;
        locomotion.ForceStickToGround = false;
        locomotion.Grounded = false;
        scriptedTraversalGroundingPrepared = true;
    }

    private void RestoreGroundingAfterScriptedTraversal()
    {
        if (!scriptedTraversalGroundingPrepared)
        {
            return;
        }

        if (locomotion != null)
        {
            locomotion.StickToGround = previousScriptedTraversalStickToGround;
            locomotion.ForceStickToGround = previousScriptedTraversalForceStickToGround;
        }

        scriptedTraversalGroundingPrepared = false;
    }

    private void TraceExternalTraversalCorrection()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!logScriptedTraversalDiagnostics || !scriptedTraversalPoseActive || locomotion == null)
        {
            return;
        }

        float distance = Vector3.Distance(locomotion.transform.position, scriptedTraversalPosition);
        float angle = Quaternion.Angle(locomotion.transform.rotation, scriptedTraversalRotation);
        if (distance <= scriptedTraversalExternalCorrectionDistance && angle <= scriptedTraversalExternalCorrectionDegrees)
        {
            return;
        }

        Debug.LogWarning("[LadderTraversal] correction externe detectee before apply: distance=" +
                         distance.ToString("F3") + "m angle=" + angle.ToString("F1") +
                         "° grounded=" + locomotion.Grounded + " velocity=" + locomotion.Velocity.ToString("F3"), this);
#endif
    }

    private void TraceAppliedTraversalPose()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!logScriptedTraversalDiagnostics)
        {
            return;
        }

        scriptedTraversalPoseTick++;
        if (scriptedTraversalPoseTick % Mathf.Max(1, scriptedTraversalDiagnosticTickInterval) != 0 || locomotion == null)
        {
            return;
        }

        float distance = Vector3.Distance(locomotion.transform.position, scriptedTraversalPosition);
        float angle = Quaternion.Angle(locomotion.transform.rotation, scriptedTraversalRotation);
        Debug.Log("[LadderTraversal] requested=" + scriptedTraversalPosition.ToString("F3") +
                  " applied=" + locomotion.transform.position.ToString("F3") +
                  " poseError=" + distance.ToString("F3") + "m/" + angle.ToString("F1") +
                  "° grounded=" + locomotion.Grounded + " velocity=" + locomotion.Velocity.ToString("F3"), this);
#endif
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
            CombatActorAnimationRoot animationContract = GetComponent<CombatActorAnimationRoot>();
            animator = animationContract != null && animationContract.ValidateContract(out _)
                ? animationContract.Animator
                : GetComponent<Animator>();
        }

        if (animatorMonitor == null)
        {
            animatorMonitor = GetComponent<AnimatorMonitor>();
        }

        if (scriptedJumpController == null)
        {
            scriptedJumpController = GetComponent<PlayerScriptedJumpController>();
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

        if (targetMagnitude > movementDeadZone)
        {
            lastExplicitWorldMoveInput = targetWorldMoveInput.normalized;
            lastExplicitWorldMoveInputTime = Time.unscaledTime;
        }

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
            Vector3 lookDirection = direction;
            if (!combatLockActive && orientLookSourceFromMovement && lookSource != null)
            {
                lookDirection = ResolveOrientationLookDirection(direction, magnitude);
                lookSource.SetPlanarLookDirection(lookDirection);
            }

            opsiveInput = ResolveOpsiveMoveInput(direction, magnitude);
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

        // SpeedChange can reject a sprint started while idle. Retry only after
        // UCC has received this frame's movement, so running starts directly.
        if (!IsFlightModeActive && sprintPressed && magnitude > movementDeadZone)
        {
            SyncSpeedChangeAbility();
        }

        TryApplyRunStartResponse(magnitude);
    }

    private void TryApplyRunStartResponse(float movementMagnitude)
    {
        bool sprintMoving = sprintPressed && movementMagnitude > movementDeadZone &&
                            !IsInputSuppressedByUcc && !IsFlightActive && Grounded;
        if (!sprintMoving)
        {
            wasSprintMoving = false;
            return;
        }

        bool beganSprint = !wasSprintMoving;
        wasSprintMoving = true;
        if (!enableRunStartResponse || (!beganSprint && !runStartResponseArmed) ||
            Time.unscaledTime < nextRunStartResponseTime || locomotion == null)
        {
            return;
        }

        Vector3 direction = new Vector3(currentWorldMoveInput.x, 0f, currentWorldMoveInput.y);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        direction.Normalize();
        float forwardSpeed = Mathf.Max(0f, Vector3.Dot(PlanarVelocity, direction));
        float availableBonus = Mathf.Min(
            runStartVelocityBonus,
            Mathf.Max(0f, runStartResponseMaximumPlanarSpeed - forwardSpeed));
        runStartResponseArmed = false;
        nextRunStartResponseTime = Time.unscaledTime + runStartResponseCooldown;
        if (availableBonus <= 0.0001f)
        {
            TraceLocomotionResponse("Run-start response skipped: planar speed already capped.");
            return;
        }

        locomotion.AddForce(direction * availableBonus, 1, false);
        TraceLocomotionResponse("Run-start response applied | bonus=" + availableBonus.ToString("F2") +
                                " | forwardSpeed=" + forwardSpeed.ToString("F2") + ".");
    }

    private void TraceLocomotionResponse(string message)
    {
        if (logLocomotionResponseDiagnostics)
        {
            Debug.Log("[Locomotion Response] " + message, this);
        }
    }

    private bool ShouldUseDirectionalRootMotionInput()
    {
        return driveDirectionalRootMotionInput && IsRootMotionLocomotionEnabled();
    }

    private Vector2 ResolveOpsiveMoveInput(Vector3 worldDirection, float magnitude)
    {
        float clampedMagnitude = Mathf.Clamp01(magnitude);
        if (clampedMagnitude <= 0f)
        {
            return Vector2.zero;
        }

        if (UseForwardOnlyGroundedLocomotion)
        {
            return new Vector2(0f, clampedMagnitude);
        }

        if (!ShouldUseDirectionalRootMotionInput())
        {
            return new Vector2(0f, clampedMagnitude);
        }

        if (useLookSourceForwardInputForRootMotion && orientLookSourceFromMovement && lookSource != null)
        {
            return new Vector2(0f, clampedMagnitude);
        }

        return ResolveLocalMoveInput(worldDirection, clampedMagnitude);
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
        // Correspond exactement aux seuils du blend tree combat Root.
        // Evite une interpolation permanente marche/course lorsque le joueur
        // maintient la course sous lock.
        return sprintPressed ? 3.25f : 1.1f;
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

        if (UseForwardOnlyGroundedLocomotion)
        {
            SetAnimatorFloat(horizontalMovementParam, 0f);
            SetAnimatorFloat(forwardMovementParam, parameterSpeed);
            return;
        }

        Vector2 directionalSpeed = localDirection * parameterSpeed;
        SetAnimatorFloat(horizontalMovementParam, directionalSpeed.x);
        SetAnimatorFloat(forwardMovementParam, directionalSpeed.y);
    }

    private Vector2 ResolveGroundedLocalMoveDirection(Vector3 fallbackVelocity)
    {
        if (combatLockActive && combatLockLocalInput.sqrMagnitude > movementDeadZone * movementDeadZone)
        {
            return Vector2.ClampMagnitude(combatLockLocalInput, 1f);
        }

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
        // Jump presentation is always visual. UCC owns gravity, vertical
        // position and inherited planar inertia, so no active aerial action
        // may leak an authored clip delta into Jump_Loop or Falling.
        bool isJumpPresentation = phase == RootMotionPhase.Jump;
        bool suppressIdlePosition = ShouldSuppressIdleRootMotionPosition(phase);
        bool useRootMotionRotation = ResolveUseRootMotionRotation(phase);
        bool useAuthoredActionRootMotion = !hasPlayerActionRootMotionMode ||
            playerActionRootMotionMode == PlayerActionRootMotionMode.AuthoredRootMotion;
        // CombatLocomotion uses Root clips, but a 2D blend tree has no neutral
        // root-motion sample at (0, 0). Letting it run without an actual stick
        // intent creates a slow autonomous drift after a combat action.
        // Explicit root-motion actions keep their authored movement even when
        // the player is not holding Move.
        bool hasCombatMoveIntent = combatLockActive
            ? combatLockLocalInput.sqrMagnitude > movementDeadZone * movementDeadZone
            : currentWorldMoveInput.sqrMagnitude > movementDeadZone * movementDeadZone ||
              desiredGroundedWorldMoveInput.sqrMagnitude > movementDeadZone * movementDeadZone;
        bool allowCombatRootMotion = phase != RootMotionPhase.Combat ||
                                     hasPlayerActionRootMotionMode ||
                                     hasCombatMoveIntent;
        // The physical grounded state, rather than an Animator state name,
        // decides whether an airborne action may move the capsule. This keeps
        // regular aerial BasicSkills visual by default and makes authored air
        // movement an explicit per-skill choice.
        bool allowAirborneRootMotion = locomotion.Grounded ||
                                        (hasPlayerActionRootMotionMode && allowPlayerActionAirborneRootMotion);
        locomotion.UseRootMotionPosition = !isJumpPresentation && useAuthoredActionRootMotion && allowAirborneRootMotion && allowCombatRootMotion;
        locomotion.RootMotionSpeedMultiplier = isJumpPresentation || !useAuthoredActionRootMotion || !allowAirborneRootMotion ||
                                                 !allowCombatRootMotion || suppressIdlePosition
            ? 0f
            : ResolveEffectiveRootMotionSpeedMultiplier(phase);
        useRootMotionRotation &= !isJumpPresentation && useAuthoredActionRootMotion && allowAirborneRootMotion && !suppressPlayerActionRootMotionRotation;
        if (combatLockActive && !combatDirectionalEvasionFacing && phase == RootMotionPhase.Combat)
        {
            // The locked target owns yaw. Root clips remain free to provide
            // translation, but can never pull Lucian away from face-to-face.
            useRootMotionRotation = false;
        }
        locomotion.UseRootMotionRotation = useRootMotionRotation;
        locomotion.RootMotionRotationMultiplier = useRootMotionRotation
            ? ResolveEffectiveRootMotionRotationMultiplier(phase)
            : 0f;

        if (animator != null && preserveAnimatorRootMotion)
        {
            animator.applyRootMotion = true;
            // Le joueur persiste pendant les chargements additifs. Le root
            // motion doit continuer a etre produit meme lorsqu'aucune camera
            // ne le rend pendant quelques images.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
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
            return !commitRootRotationDuringPivot;
        }

        return phase == RootMotionPhase.Combat ||
               (allowRootMotionRotationDuringStartStop &&
                (phase == RootMotionPhase.Start || phase == RootMotionPhase.Stop));
    }

    private bool ShouldSuppressIdleRootMotionPosition(RootMotionPhase phase)
    {
        if (!suppressIdleRootMotionPosition ||
            locomotion == null ||
            !locomotion.Grounded ||
            phase == RootMotionPhase.Start ||
            phase == RootMotionPhase.Stop ||
            phase == RootMotionPhase.Pivot ||
            phase == RootMotionPhase.Combat)
        {
            return false;
        }

        float deadZoneSqr = movementDeadZone * movementDeadZone;
        if (currentWorldMoveInput.sqrMagnitude > deadZoneSqr ||
            desiredGroundedWorldMoveInput.sqrMagnitude > deadZoneSqr)
        {
            return false;
        }

        Vector3 planarVelocity = locomotion.Velocity;
        planarVelocity.y = 0f;
        float threshold = Mathf.Max(0f, idleRootMotionVelocityThreshold);
        return planarVelocity.sqrMagnitude <= threshold * threshold;
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
        if (stateInfo.IsName("Walk_Start") || stateInfo.IsName("Run_Start"))
        {
            return RootMotionPhase.Start;
        }

        if (stateInfo.IsName("Walk_Stop") || stateInfo.IsName("Run_Stop"))
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

        if (stateInfo.IsName("Jump_Start") ||
            stateInfo.IsName("Jump_Loop") ||
            stateInfo.IsName("Falling") ||
            stateInfo.IsName("Landing") ||
            stateInfo.IsName("Landing_Hard") ||
            stateInfo.IsName("Jump_End"))
        {
            return RootMotionPhase.Jump;
        }

        if (stateInfo.IsTag("RealTimeCombatRootMotion"))
        {
            return RootMotionPhase.Combat;
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
        if (!groundReliefToleranceApplied || !relaxGroundReliefTolerance || locomotion == null || IsScriptedTraversalActive)
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
