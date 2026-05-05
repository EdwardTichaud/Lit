using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class StarterInspiredThirdPersonMotor : MonoBehaviour
{
    private const float MinimumCharacterControllerStepOffset = 0.001f;

    public enum MovementState
    {
        Idle,
        Locomotion,
        Brake,
        JumpStart,
        Airborne,
        WallSlide,
        Landing,
        Flight,
        Ladder
    }

    public enum LandingSeverity
    {
        None,
        Light,
        Medium,
        Heavy
    }

    [Header("References")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private bool autoResolveMainCamera = true;

    [Header("Input")]
    [SerializeField, Range(0f, 0.5f)] private float inputDeadZone = 0.12f;

    [Header("Planar Movement")]
    [SerializeField, Min(0f)] private float walkMoveSpeed = 5f;
    [SerializeField, Min(0f)] private float maxMoveSpeed = 6.5f;
    [SerializeField, Min(0f)] private float acceleration = 11f;
    [SerializeField, Min(0f)] private float deceleration = 14f;
    [SerializeField, Min(0f)] private float idleSpeedThreshold = 0.04f;

    [Header("Hard Reversal")]
    [SerializeField, Range(90f, 180f)] private float hardReverseAngle = 135f;
    [SerializeField, Min(0f)] private float hardReverseMinSpeed = 1.2f;
    [SerializeField, Min(0f)] private float hardReverseReleaseSpeed = 0.35f;
    [SerializeField, Min(1f)] private float hardReverseBrakeMultiplier = 1.4f;

    [Header("Rotation")]
    [SerializeField, Min(0f)] private float lowSpeedTurnRate = 620f;
    [SerializeField, Min(0f)] private float moveTurnRate = 500f;
    [SerializeField, Min(0f)] private float sprintTurnRate = 360f;
    [SerializeField, Range(0.01f, 1f)] private float sprintTurnThreshold = 0.85f;

    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Range(0.1f, 1f)] private float groundProbeRadiusScale = 0.9f;
    [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.18f;
    [SerializeField, Min(0f)] private float groundProbeStartOffset = 0.08f;
    [SerializeField, Range(0f, 89f)] private float maxGroundAngle = 50f;
    [SerializeField, Min(0f)] private float groundedGraceTime = 0.1f;

    [Header("Step / Obstacle Traversal")]
    [SerializeField] private bool enableStepTraversal = true;
    [SerializeField, Min(0f)] private float maxStepRise = 0.35f;
    [SerializeField, Min(0f)] private float maxStepDrop = 0.45f;
    [SerializeField, Min(0f)] private float minStepRise = 0.03f;
    [SerializeField, Min(0f)] private float stepSearchDistance = 0.9f;
    [SerializeField, Min(0f)] private float stepSearchExtraDistance = 0.22f;
    [SerializeField, Min(0f)] private float stepSurfaceInset = 0.08f;
    [SerializeField, Min(0f)] private float stepContactOffset = 0.03f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -24f;
    [SerializeField, Min(0f)] private float maxFallSpeed = 35f;
    [SerializeField, Min(0f)] private float groundedStickVelocity = 2f;
    [SerializeField, Min(0f)] private float groundSnapDistance = 0.35f;

    [Header("Jump")]
    [SerializeField, Min(0f)] private float jumpImpulse = 7f;
    [SerializeField, Min(0f)] private float jumpInputBufferTime = 0.12f;
    [SerializeField, Min(0f)] private float jumpGroundedGraceTime = 0.08f;
    [SerializeField, Min(0f)] private float jumpGroundIgnoreTime = 0.12f;

    [Header("Flight")]
    [SerializeField] private bool enableFlight = true;
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
    [SerializeField, Min(0f)] private float flightExitDownwardVelocity = 1.5f;
    [SerializeField, Min(0f)] private float flightBoostKickSpeed = 4.5f;
    [SerializeField, Min(0f)] private float flightGroundContactLandingMinSpeed = 2.75f;
    [SerializeField, Min(0f)] private float flightGroundContactLandingMinDownwardSpeed = 0.2f;
    [SerializeField, Range(0f, 1f)] private float flightLandingPlanarVelocityRetention = 0.25f;
    [SerializeField, Range(0f, 1f)] private float flightLandingDampingMultiplier = 1f;
    [SerializeField, Min(0f)] private float flightLandingControlGraceTime = 0.08f;

    [Header("Airborne / Landing")]
    [SerializeField, Min(0f)] private float freeFallMinAirborneTime = 0.18f;
    [SerializeField, Min(0f)] private float freeFallMinDownwardSpeed = 1.5f;
    [SerializeField, Min(0f)] private float landingMinAirborneTime = 0.14f;
    [SerializeField, Min(0f)] private float landingMinDownwardSpeed = 2.5f;
    [SerializeField, Min(0f)] private float mediumLandingDownwardSpeed = 7f;
    [SerializeField, Min(0f)] private float heavyLandingDownwardSpeed = 10f;
    [SerializeField, Min(0f)] private float landingDampingDuration = 0.18f;
    [SerializeField, Min(0f)] private float lightLandingDamping = 14f;
    [SerializeField, Min(0f)] private float mediumLandingDamping = 26f;
    [SerializeField, Min(0f)] private float heavyLandingDamping = 38f;

    [Header("Airborne Wall Slide")]
    [SerializeField] private bool enableAirborneWallSlide = true;
    [SerializeField, Range(0f, 0.95f)] private float wallSlideMaxNormalY = 0.35f;
    [SerializeField, Min(0f)] private float wallSlideContactMemoryTime = 0.08f;
    [SerializeField, Min(0f)] private float wallSlideMinDownwardSpeed = 8f;
    [SerializeField, Min(1f)] private float wallSlideGravityMultiplier = 1.25f;

    [Header("Debug")]
    [SerializeField] private bool showDebugValues = true;
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private MovementState debugCurrentState;
    [SerializeField] private float debugInputMagnitude;
    [SerializeField] private float debugDesiredSpeed;
    [SerializeField] private float debugActualSpeed;
    [SerializeField] private Vector3 debugCurrentPlanarVelocity;
    [SerializeField] private bool debugRawGrounded;
    [SerializeField] private bool debugStableGrounded;
    [SerializeField] private Vector3 debugGroundNormal = Vector3.up;
    [SerializeField] private float debugGroundAngle;
    [SerializeField] private float debugVerticalVelocity;
    [SerializeField] private float debugTimeSinceGrounded;
    [SerializeField] private bool debugSnapActive;
    [SerializeField] private bool debugJumpRequested;
    [SerializeField] private bool debugJumpStarted;
    [SerializeField] private bool debugAirborne;
    [SerializeField] private bool debugFreeFall;
    [SerializeField] private bool debugFlightActive;
    [SerializeField] private bool debugFlightBoosting;
    [SerializeField] private bool debugFlightBoostStarted;
    [SerializeField] private bool debugFlightTakeoffActive;
    [SerializeField] private float debugFlightVerticalInput;
    [SerializeField] private Vector3 debugFlightVelocity;
    [SerializeField] private float debugFlightNormalizedSpeed;
    [SerializeField] private bool debugLandingTriggered;
    [SerializeField] private LandingSeverity debugLandingSeverity;
    [SerializeField] private bool debugWallSliding;
    [SerializeField] private int debugWallSlideSide;
    [SerializeField] private Vector3 debugWallSlideNormal;
    [SerializeField] private bool debugStepTraversalActive;
    [SerializeField] private float debugStepTraversalOffset;
    [SerializeField] private bool debugStepTraversalBlocked;
    [SerializeField] private float debugAirborneTime;
    [SerializeField] private float debugLastGroundedTime;
    [SerializeField] private bool debugLadderTraversalActive;

    private Vector2 moveInput;
    private Vector3 desiredWorldDirection;
    private Vector3 currentPlanarVelocity;
    private MovementState currentState;
    private bool sprintInput;
    private bool brakingForHardReversal;
    private bool rawGrounded;
    private bool stableGrounded;
    private Vector3 groundNormal = Vector3.up;
    private Vector3 groundPoint;
    private float groundAngle;
    private float verticalVelocity;
    private float timeSinceGrounded = float.PositiveInfinity;
    private bool snapActive;
    private float jumpBufferTimer;
    private float jumpGroundIgnoreTimer;
    private bool jumpStartedThisFrame;
    private bool freeFall;
    private bool landingTriggeredThisFrame;
    private bool landingFromFlightTriggeredThisFrame;
    private LandingSeverity lastLandingSeverity;
    private bool flightActive;
    private bool flightBoostInput;
    private bool flightBoostActive;
    private bool wasFlightBoostActive;
    private bool flightBoostStartedThisFrame;
    private float flightVerticalInput;
    private float flightTakeoffTimer;
    private Vector3 currentFlightVelocity;
    private float airborneTime;
    private float airbornePeakDownwardSpeed;
    private bool forceLandingAfterFlight;
    private float flightLandingControlGraceTimer;
    private float lastGroundedTime;
    private float landingDampingTimer;
    private float landingDampingStrength;
    private bool flightLandingDampingActive;
    private bool wallSliding;
    private int wallSlideSide;
    private Vector3 wallSlideNormal;
    private float wallSlideContactTimer;
    private bool wallHitThisFrame;
    private Vector3 wallHitNormal;
    private bool stepTraversalActive;
    private float stepTraversalVerticalOffset;
    private bool stepTraversalBlocked;
    private int ladderTraversalLockCount;
    private Vector3 debugProbeOrigin;
    private float debugProbeDistance;
    private float debugProbeRadius;
    private readonly RaycastHit[] traversalCastHits = new RaycastHit[8];
    private readonly Collider[] traversalOverlapHits = new Collider[8];

    public MovementState CurrentState => currentState;
    public float InputMagnitude { get; private set; }
    public float DesiredSpeed { get; private set; }
    public float ActualSpeed => flightActive ? currentFlightVelocity.magnitude : new Vector3(currentPlanarVelocity.x, 0f, currentPlanarVelocity.z).magnitude;
    public Vector3 CurrentPlanarVelocity => currentPlanarVelocity;
    public Vector3 FlightVelocity => currentFlightVelocity;
    public bool FlightActive => flightActive;
    public bool FlightBoosting => flightActive && flightBoostActive;
    public bool FlightBoostStarted => flightBoostStartedThisFrame;
    public bool FlightTakeoffActive => flightActive && flightTakeoffTimer > 0f;
    public float FlightVerticalInput => flightVerticalInput;
    public float FlightSpeed => flightActive ? currentFlightVelocity.magnitude : 0f;
    public float FlightNormalizedSpeed => flightBoostSpeed > 0f
        ? Mathf.Clamp01(currentFlightVelocity.magnitude / flightBoostSpeed)
        : 0f;
    public float FlightBoostAmount => flightBoostSpeed > flightCruiseSpeed
        ? Mathf.Max(FlightBoosting ? 0.65f : 0f, Mathf.InverseLerp(flightCruiseSpeed, flightBoostSpeed, currentFlightVelocity.magnitude))
        : FlightNormalizedSpeed;
    public bool RawGrounded => rawGrounded;
    public bool StableGrounded => stableGrounded;
    public bool IsGrounded => stableGrounded;
    public Vector3 GroundNormal => groundNormal;
    public float GroundAngle => groundAngle;
    public float VerticalVelocity => verticalVelocity;
    public float TimeSinceGrounded => timeSinceGrounded;
    public bool SnapActive => snapActive;
    public bool JumpRequested => jumpBufferTimer > 0f;
    public bool JumpStarted => jumpStartedThisFrame;
    public bool Airborne => flightActive || !rawGrounded;
    public bool FreeFall => freeFall;
    public bool WallSliding => wallSliding;
    public int WallSlideSide => wallSlideSide;
    public Vector3 WallSlideNormal => wallSlideNormal;
    public bool LandingTriggered => landingTriggeredThisFrame;
    public bool LandingFromFlightTriggered => landingFromFlightTriggeredThisFrame;
    public LandingSeverity LastLandingSeverity => lastLandingSeverity;
    public float AirborneTime => airborneTime;
    public float LastGroundedTime => lastGroundedTime;
    public bool IsLadderTraversalActive => ladderTraversalLockCount > 0;

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        ResolveMainCamera();
    }

    private void Awake()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (cameraTransform == null)
        {
            ResolveMainCamera();
        }

        ConfigureExplicitStepTraversal();
        lastGroundedTime = Time.time;
        RefreshGrounding(0f, CollisionFlags.None, allowSnap: false);
        UpdateState();
        UpdateDebugValues();
    }

    private void OnValidate()
    {
        inputDeadZone = Mathf.Clamp(inputDeadZone, 0f, 0.5f);
        walkMoveSpeed = Mathf.Max(0f, walkMoveSpeed);
        maxMoveSpeed = Mathf.Max(0f, maxMoveSpeed);
        walkMoveSpeed = Mathf.Min(walkMoveSpeed, maxMoveSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        idleSpeedThreshold = Mathf.Max(0f, idleSpeedThreshold);
        hardReverseMinSpeed = Mathf.Max(0f, hardReverseMinSpeed);
        hardReverseReleaseSpeed = Mathf.Max(0f, hardReverseReleaseSpeed);
        hardReverseBrakeMultiplier = Mathf.Max(1f, hardReverseBrakeMultiplier);
        lowSpeedTurnRate = Mathf.Max(0f, lowSpeedTurnRate);
        moveTurnRate = Mathf.Max(0f, moveTurnRate);
        sprintTurnRate = Mathf.Max(0f, sprintTurnRate);
        sprintTurnThreshold = Mathf.Clamp(sprintTurnThreshold, 0.01f, 1f);
        groundProbeRadiusScale = Mathf.Clamp(groundProbeRadiusScale, 0.1f, 1f);
        groundProbeDistance = Mathf.Max(0.01f, groundProbeDistance);
        groundProbeStartOffset = Mathf.Max(0f, groundProbeStartOffset);
        maxGroundAngle = Mathf.Clamp(maxGroundAngle, 0f, 89f);
        groundedGraceTime = Mathf.Max(0f, groundedGraceTime);
        maxStepRise = Mathf.Max(0f, maxStepRise);
        maxStepDrop = Mathf.Max(0f, maxStepDrop);
        minStepRise = Mathf.Max(0f, minStepRise);
        stepSearchDistance = Mathf.Max(0.05f, stepSearchDistance);
        stepSearchExtraDistance = Mathf.Max(0f, stepSearchExtraDistance);
        stepSurfaceInset = Mathf.Max(0.01f, stepSurfaceInset);
        stepContactOffset = Mathf.Max(0f, stepContactOffset);
        maxFallSpeed = Mathf.Max(0f, maxFallSpeed);
        groundedStickVelocity = Mathf.Max(0f, groundedStickVelocity);
        groundSnapDistance = Mathf.Max(0f, groundSnapDistance);
        jumpImpulse = Mathf.Max(0f, jumpImpulse);
        jumpInputBufferTime = Mathf.Max(0f, jumpInputBufferTime);
        jumpGroundedGraceTime = Mathf.Max(0f, jumpGroundedGraceTime);
        jumpGroundIgnoreTime = Mathf.Max(0f, jumpGroundIgnoreTime);
        flightTakeoffVerticalSpeed = Mathf.Max(0f, flightTakeoffVerticalSpeed);
        flightTakeoffDuration = Mathf.Max(0f, flightTakeoffDuration);
        flightTakeoffDamping = Mathf.Max(0f, flightTakeoffDamping);
        flightCruiseSpeed = Mathf.Max(0f, flightCruiseSpeed);
        flightBoostSpeed = Mathf.Max(flightCruiseSpeed, flightBoostSpeed);
        flightAcceleration = Mathf.Max(0f, flightAcceleration);
        flightBoostAcceleration = Mathf.Max(flightAcceleration, flightBoostAcceleration);
        flightDeceleration = Mathf.Max(0f, flightDeceleration);
        flightVerticalSpeed = Mathf.Max(0f, flightVerticalSpeed);
        flightVerticalAcceleration = Mathf.Max(0f, flightVerticalAcceleration);
        flightVerticalDeceleration = Mathf.Max(0f, flightVerticalDeceleration);
        flightVerticalDeadZone = Mathf.Clamp(flightVerticalDeadZone, 0f, 0.4f);
        flightIdleSpeedThreshold = Mathf.Max(0f, flightIdleSpeedThreshold);
        flightTurnRate = Mathf.Max(0f, flightTurnRate);
        flightBoostTurnRate = Mathf.Max(0f, flightBoostTurnRate);
        flightExitDownwardVelocity = Mathf.Max(0f, flightExitDownwardVelocity);
        flightBoostKickSpeed = Mathf.Max(0f, flightBoostKickSpeed);
        flightGroundContactLandingMinSpeed = Mathf.Max(0f, flightGroundContactLandingMinSpeed);
        flightGroundContactLandingMinDownwardSpeed = Mathf.Max(0f, flightGroundContactLandingMinDownwardSpeed);
        flightLandingPlanarVelocityRetention = Mathf.Clamp01(flightLandingPlanarVelocityRetention);
        flightLandingDampingMultiplier = Mathf.Clamp01(flightLandingDampingMultiplier);
        flightLandingControlGraceTime = Mathf.Max(0f, flightLandingControlGraceTime);
        freeFallMinAirborneTime = Mathf.Max(0f, freeFallMinAirborneTime);
        freeFallMinDownwardSpeed = Mathf.Max(0f, freeFallMinDownwardSpeed);
        landingMinAirborneTime = Mathf.Max(0f, landingMinAirborneTime);
        landingMinDownwardSpeed = Mathf.Max(0f, landingMinDownwardSpeed);
        mediumLandingDownwardSpeed = Mathf.Max(landingMinDownwardSpeed, mediumLandingDownwardSpeed);
        heavyLandingDownwardSpeed = Mathf.Max(mediumLandingDownwardSpeed, heavyLandingDownwardSpeed);
        landingDampingDuration = Mathf.Max(0f, landingDampingDuration);
        lightLandingDamping = Mathf.Max(0f, lightLandingDamping);
        mediumLandingDamping = Mathf.Max(0f, mediumLandingDamping);
        heavyLandingDamping = Mathf.Max(0f, heavyLandingDamping);
        wallSlideMaxNormalY = Mathf.Clamp(wallSlideMaxNormalY, 0f, 0.95f);
        wallSlideContactMemoryTime = Mathf.Max(0f, wallSlideContactMemoryTime);
        wallSlideMinDownwardSpeed = Mathf.Max(0f, wallSlideMinDownwardSpeed);
        wallSlideGravityMultiplier = Mathf.Max(1f, wallSlideGravityMultiplier);
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    public void SetMoveInput(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    public void Move(Vector2 input)
    {
        SetMoveInput(input);
    }

    public void Stop()
    {
        moveInput = Vector2.zero;
    }

    public void SetBoostInput(bool boost)
    {
        flightBoostInput = boost;
    }

    public void SetSprintInput(bool sprint)
    {
        sprintInput = sprint;
    }

    public void ConfigureGroundSpeedProfile(float walkSpeed, float sprintSpeed)
    {
        maxMoveSpeed = Mathf.Max(0f, sprintSpeed);
        walkMoveSpeed = Mathf.Clamp(walkSpeed, 0f, maxMoveSpeed);
    }

    public void SetFlightVerticalInput(float verticalInput)
    {
        flightVerticalInput = Mathf.Clamp(verticalInput, -1f, 1f);
    }

    public void ToggleFlightMode()
    {
        SetFlightMode(!flightActive);
    }

    public void SetFlightMode(bool enabled)
    {
        if (enabled)
        {
            EnterFlightMode();
            return;
        }

        ExitFlightMode();
    }

    public void RequestJump()
    {
        if (flightActive)
        {
            return;
        }

        jumpBufferTimer = Mathf.Max(jumpInputBufferTime, Time.deltaTime);
    }

    public void Jump()
    {
        RequestJump();
    }

    public void ResetMotionState(bool clearInput = true)
    {
        ResetMotionState(clearInput, allowGroundSnap: false);
    }

    private void ResetMotionState(bool clearInput, bool allowGroundSnap)
    {
        if (clearInput)
        {
            moveInput = Vector2.zero;
        }

        InputMagnitude = 0f;
        DesiredSpeed = 0f;
        desiredWorldDirection = Vector3.zero;
        currentPlanarVelocity = Vector3.zero;
        verticalVelocity = 0f;
        sprintInput = false;
        brakingForHardReversal = false;
        snapActive = false;
        jumpBufferTimer = 0f;
        jumpGroundIgnoreTimer = 0f;
        jumpStartedThisFrame = false;
        freeFall = false;
        landingTriggeredThisFrame = false;
        landingFromFlightTriggeredThisFrame = false;
        lastLandingSeverity = LandingSeverity.None;
        flightActive = false;
        flightBoostInput = false;
        flightBoostActive = false;
        wasFlightBoostActive = false;
        flightBoostStartedThisFrame = false;
        flightVerticalInput = 0f;
        flightTakeoffTimer = 0f;
        currentFlightVelocity = Vector3.zero;
        airborneTime = 0f;
        airbornePeakDownwardSpeed = 0f;
        forceLandingAfterFlight = false;
        flightLandingControlGraceTimer = 0f;
        landingDampingTimer = 0f;
        landingDampingStrength = 0f;
        flightLandingDampingActive = false;
        stepTraversalActive = false;
        stepTraversalVerticalOffset = 0f;
        stepTraversalBlocked = false;
        ClearWallSlideState();
        lastGroundedTime = Time.time;
        if (allowGroundSnap)
        {
            timeSinceGrounded = 0f;
        }

        RefreshGrounding(0f, CollisionFlags.None, allowSnap: allowGroundSnap);
        UpdateState();
        UpdateDebugValues();
    }

    public void BeginLadderTraversal(bool clearInput = true)
    {
        ladderTraversalLockCount++;
        ResetMotionState(clearInput);
        SetLadderTraversalState();
    }

    public void ApplyLadderPose(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (!IsLadderTraversalActive)
        {
            return;
        }

        transform.SetPositionAndRotation(targetPosition, targetRotation);
        Physics.SyncTransforms();
        SetLadderTraversalState();
    }

    public void EndLadderTraversal(bool clearInput = true)
    {
        if (ladderTraversalLockCount > 0)
        {
            ladderTraversalLockCount--;
        }

        if (IsLadderTraversalActive)
        {
            SetLadderTraversalState();
            return;
        }

        ResetMotionState(clearInput, allowGroundSnap: true);
    }

    private void Tick(float deltaTime)
    {
        if (deltaTime <= 0f || characterController == null || !characterController.enabled || !characterController.gameObject.activeInHierarchy)
        {
            UpdateDebugValues();
            return;
        }

        if (IsLadderTraversalActive)
        {
            SetLadderTraversalState();
            UpdateDebugValues();
            return;
        }

        if (flightActive)
        {
            TickFlight(deltaTime);
            UpdateDebugValues();
            return;
        }

        bool wasRawGrounded = rawGrounded;

        snapActive = false;
        stepTraversalActive = false;
        stepTraversalVerticalOffset = 0f;
        stepTraversalBlocked = false;
        jumpStartedThisFrame = false;
        landingTriggeredThisFrame = false;
        landingFromFlightTriggeredThisFrame = false;
        UpdateTimers(deltaTime);

        Vector2 processedInput = ProcessMoveInput(moveInput);
        InputMagnitude = processedInput.magnitude;
        desiredWorldDirection = ResolveCameraRelativeDirection(processedInput);
        DesiredSpeed = InputMagnitude * ResolveCurrentGroundMoveSpeed();

        TryStartJump();
        UpdatePlanarVelocity(deltaTime);
        ApplyLandingDamping(deltaTime);
        UpdateRotation(deltaTime);
        UpdateVerticalVelocity(deltaTime);

        Vector3 displacement = ResolveGroundAdjustedPlanarDisplacement(deltaTime);
        displacement = ResolveStepAdjustedPlanarDisplacement(displacement);

        float verticalDisplacement = verticalVelocity * deltaTime;
        if (stepTraversalActive && stepTraversalVerticalOffset > 0f && verticalDisplacement < 0f)
        {
            verticalDisplacement = 0f;
        }

        displacement += Vector3.up * verticalDisplacement;

        ResetWallHitFrame();
        CollisionFlags collisionFlags = characterController.Move(displacement);
        UpdateWallSlideState(deltaTime, collisionFlags);
        ApplyCollisionFeedback(collisionFlags);
        RefreshGrounding(deltaTime, collisionFlags, allowSnap: true);
        UpdateAirborneAndLanding(deltaTime, wasRawGrounded);

        if (stableGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -groundedStickVelocity;
        }

        UpdateState();
        UpdateDebugValues();
    }

    private void UpdateTimers(float deltaTime)
    {
        if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - deltaTime);
        }

        if (jumpGroundIgnoreTimer > 0f)
        {
            jumpGroundIgnoreTimer = Mathf.Max(0f, jumpGroundIgnoreTimer - deltaTime);
        }

        if (landingDampingTimer > 0f)
        {
            landingDampingTimer = Mathf.Max(0f, landingDampingTimer - deltaTime);
            if (landingDampingTimer <= 0f)
            {
                flightLandingDampingActive = false;
            }
        }

        if (flightLandingControlGraceTimer > 0f)
        {
            flightLandingControlGraceTimer = Mathf.Max(0f, flightLandingControlGraceTimer - deltaTime);
        }
    }

    private Vector2 ProcessMoveInput(Vector2 input)
    {
        float magnitude = Mathf.Clamp01(input.magnitude);
        if (magnitude <= inputDeadZone)
        {
            return Vector2.zero;
        }

        float normalizedMagnitude = Mathf.InverseLerp(inputDeadZone, 1f, magnitude);
        return input.normalized * normalizedMagnitude;
    }

    private Vector3 ResolveCameraRelativeDirection(Vector2 processedInput)
    {
        if (processedInput == Vector2.zero)
        {
            return Vector3.zero;
        }

        if (cameraTransform == null && autoResolveMainCamera)
        {
            ResolveMainCamera();
        }

        Vector3 forward;
        Vector3 right;

        if (cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;
        }
        else
        {
            forward = transform.forward;
            right = transform.right;
        }

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 direction = forward * processedInput.y + right * processedInput.x;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        return direction.normalized;
    }

    private void EnterFlightMode()
    {
        if (!enableFlight || flightActive || IsLadderTraversalActive)
        {
            return;
        }

        currentPlanarVelocity = Vector3.zero;
        currentFlightVelocity = Vector3.up * Mathf.Max(verticalVelocity, flightTakeoffVerticalSpeed);
        verticalVelocity = 0f;
        jumpBufferTimer = 0f;
        jumpGroundIgnoreTimer = 0f;
        jumpStartedThisFrame = true;
        landingTriggeredThisFrame = false;
        landingFromFlightTriggeredThisFrame = false;
        lastLandingSeverity = LandingSeverity.None;
        landingDampingTimer = 0f;
        landingDampingStrength = 0f;
        flightLandingDampingActive = false;
        brakingForHardReversal = false;
        freeFall = false;
        ClearWallSlideState();

        InputMagnitude = 0f;
        DesiredSpeed = 0f;
        desiredWorldDirection = Vector3.zero;
        rawGrounded = false;
        stableGrounded = false;
        snapActive = false;
        timeSinceGrounded = groundedGraceTime + 0.001f;
        airborneTime = 0f;
        airbornePeakDownwardSpeed = 0f;
        groundNormal = Vector3.up;
        groundAngle = 0f;
        flightBoostActive = false;
        wasFlightBoostActive = false;
        flightBoostStartedThisFrame = false;
        flightTakeoffTimer = flightTakeoffDuration;
        forceLandingAfterFlight = false;
        flightLandingControlGraceTimer = 0f;
        flightActive = true;
        currentState = MovementState.Flight;
    }

    private void ExitFlightMode()
    {
        if (!flightActive)
        {
            return;
        }

        Vector3 planarVelocity = Vector3.ProjectOnPlane(currentFlightVelocity, Vector3.up);
        float retainedSpeed = maxMoveSpeed > 0f ? maxMoveSpeed : planarVelocity.magnitude;
        currentPlanarVelocity = Vector3.ClampMagnitude(planarVelocity, retainedSpeed);
        verticalVelocity = Mathf.Min(currentFlightVelocity.y, -flightExitDownwardVelocity);

        currentFlightVelocity = Vector3.zero;
        flightBoostInput = false;
        flightBoostActive = false;
        wasFlightBoostActive = false;
        flightBoostStartedThisFrame = false;
        flightTakeoffTimer = 0f;
        flightActive = false;
        timeSinceGrounded = groundedGraceTime + 0.001f;
        airborneTime = Mathf.Max(airborneTime, landingMinAirborneTime);
        airbornePeakDownwardSpeed = Mathf.Max(flightGroundContactLandingMinSpeed, -verticalVelocity);
        forceLandingAfterFlight = true;
        freeFall = false;
        landingTriggeredThisFrame = false;
        landingFromFlightTriggeredThisFrame = false;
        lastLandingSeverity = LandingSeverity.None;
        RefreshGrounding(0f, CollisionFlags.None, allowSnap: false);
        UpdateState();
    }

    private void TickFlight(float deltaTime)
    {
        snapActive = false;
        stepTraversalActive = false;
        stepTraversalVerticalOffset = 0f;
        stepTraversalBlocked = false;
        jumpBufferTimer = 0f;
        jumpGroundIgnoreTimer = 0f;
        jumpStartedThisFrame = false;
        landingTriggeredThisFrame = false;
        landingFromFlightTriggeredThisFrame = false;
        lastLandingSeverity = LandingSeverity.None;
        landingDampingTimer = 0f;
        landingDampingStrength = 0f;
        flightLandingDampingActive = false;
        brakingForHardReversal = false;
        freeFall = false;
        flightBoostStartedThisFrame = false;
        ClearWallSlideState();

        if (FlightTakeoffActive)
        {
            TickFlightTakeoff(deltaTime);
            return;
        }

        Vector2 processedInput = ProcessMoveInput(moveInput);
        InputMagnitude = processedInput.magnitude;
        desiredWorldDirection = ResolveCameraRelativeDirection(processedInput);

        bool hasPlanarDirection = desiredWorldDirection.sqrMagnitude > 0.0001f;
        flightBoostActive = flightBoostInput && hasPlanarDirection;
        flightBoostStartedThisFrame = flightBoostActive && !wasFlightBoostActive;
        wasFlightBoostActive = flightBoostActive;

        float requestedSpeed = flightBoostActive ? flightBoostSpeed : flightCruiseSpeed;
        DesiredSpeed = hasPlanarDirection ? requestedSpeed * InputMagnitude : 0f;

        Vector3 currentPlanarFlightVelocity = Vector3.ProjectOnPlane(currentFlightVelocity, Vector3.up);
        Vector3 desiredPlanarVelocity = hasPlanarDirection ? desiredWorldDirection * DesiredSpeed : Vector3.zero;
        float planarRate = hasPlanarDirection
            ? (flightBoostActive ? flightBoostAcceleration : flightAcceleration)
            : flightDeceleration;

        currentPlanarFlightVelocity = Vector3.MoveTowards(
            currentPlanarFlightVelocity,
            desiredPlanarVelocity,
            planarRate * deltaTime);

        if (flightBoostStartedThisFrame && flightBoostKickSpeed > 0f)
        {
            currentPlanarFlightVelocity += desiredWorldDirection * flightBoostKickSpeed;
            currentPlanarFlightVelocity = Vector3.ClampMagnitude(currentPlanarFlightVelocity, flightBoostSpeed);
        }

        float processedVerticalInput = ProcessFlightVerticalInput(flightVerticalInput);
        float targetVerticalSpeed = processedVerticalInput * flightVerticalSpeed;
        float verticalRate = Mathf.Abs(processedVerticalInput) > 0f
            ? flightVerticalAcceleration
            : flightVerticalDeceleration;
        float nextVerticalVelocity = Mathf.MoveTowards(
            currentFlightVelocity.y,
            targetVerticalSpeed,
            verticalRate * deltaTime);

        currentFlightVelocity = currentPlanarFlightVelocity + Vector3.up * nextVerticalVelocity;
        bool shouldLandOnGroundContact = ShouldLandFromFlightGroundContact(processedVerticalInput, nextVerticalVelocity);
        if (!hasPlanarDirection &&
            Mathf.Abs(processedVerticalInput) <= 0.0001f &&
            currentFlightVelocity.sqrMagnitude <= flightIdleSpeedThreshold * flightIdleSpeedThreshold)
        {
            currentFlightVelocity = Vector3.zero;
        }

        UpdateFlightRotation(deltaTime);

        ResetWallHitFrame();
        CollisionFlags collisionFlags = characterController.Move(currentFlightVelocity * deltaTime);
        float impactDownwardSpeed = Mathf.Max(0f, -currentFlightVelocity.y);
        if (TryCompleteFlightGroundContact(collisionFlags, impactDownwardSpeed, shouldLandOnGroundContact))
        {
            return;
        }

        ApplyFlightCollisionFeedback(collisionFlags);

        rawGrounded = false;
        stableGrounded = false;
        groundNormal = Vector3.up;
        groundAngle = 0f;
        timeSinceGrounded = float.IsPositiveInfinity(timeSinceGrounded)
            ? deltaTime
            : timeSinceGrounded + deltaTime;
        airborneTime += deltaTime;
        airbornePeakDownwardSpeed = 0f;
        currentState = MovementState.Flight;
    }

    private void TickFlightTakeoff(float deltaTime)
    {
        InputMagnitude = 0f;
        DesiredSpeed = 0f;
        desiredWorldDirection = Vector3.zero;
        flightBoostActive = false;
        wasFlightBoostActive = false;

        flightTakeoffTimer = Mathf.Max(0f, flightTakeoffTimer - deltaTime);
        currentFlightVelocity = Vector3.MoveTowards(
            currentFlightVelocity,
            Vector3.zero,
            flightTakeoffDamping * deltaTime);

        if (flightTakeoffTimer <= 0f && currentFlightVelocity.magnitude <= Mathf.Max(flightIdleSpeedThreshold, 0.01f))
        {
            currentFlightVelocity = Vector3.zero;
        }

        ResetWallHitFrame();
        CollisionFlags collisionFlags = characterController.Move(currentFlightVelocity * deltaTime);
        ApplyFlightCollisionFeedback(collisionFlags);

        rawGrounded = false;
        stableGrounded = false;
        groundNormal = Vector3.up;
        groundAngle = 0f;
        timeSinceGrounded = float.IsPositiveInfinity(timeSinceGrounded)
            ? deltaTime
            : timeSinceGrounded + deltaTime;
        airborneTime += deltaTime;
        airbornePeakDownwardSpeed = 0f;
        currentState = MovementState.Flight;
    }

    private float ProcessFlightVerticalInput(float input)
    {
        float magnitude = Mathf.Abs(input);
        if (magnitude <= flightVerticalDeadZone)
        {
            return 0f;
        }

        float normalizedMagnitude = Mathf.InverseLerp(flightVerticalDeadZone, 1f, Mathf.Clamp01(magnitude));
        return Mathf.Sign(input) * normalizedMagnitude;
    }

    private bool ShouldLandFromFlightGroundContact(float processedVerticalInput, float verticalSpeed)
    {
        return processedVerticalInput < -0.0001f ||
               verticalSpeed <= -flightGroundContactLandingMinDownwardSpeed;
    }

    private bool TryCompleteFlightGroundContact(
        CollisionFlags collisionFlags,
        float impactDownwardSpeed,
        bool shouldLandOnGroundContact)
    {
        if ((collisionFlags & CollisionFlags.Below) == 0 || FlightTakeoffActive || !shouldLandOnGroundContact)
        {
            return false;
        }

        Vector3 planarVelocity = Vector3.ProjectOnPlane(currentFlightVelocity, Vector3.up);
        float retainedSpeed = maxMoveSpeed > 0f ? maxMoveSpeed : planarVelocity.magnitude;
        currentPlanarVelocity = Vector3.ClampMagnitude(planarVelocity, retainedSpeed);
        verticalVelocity = -groundedStickVelocity;

        currentFlightVelocity = Vector3.zero;
        flightBoostInput = false;
        flightBoostActive = false;
        wasFlightBoostActive = false;
        flightBoostStartedThisFrame = false;
        flightTakeoffTimer = 0f;
        flightActive = false;
        forceLandingAfterFlight = true;
        airborneTime = Mathf.Max(airborneTime, landingMinAirborneTime);
        airbornePeakDownwardSpeed = Mathf.Max(impactDownwardSpeed, flightGroundContactLandingMinSpeed);
        freeFall = false;

        RefreshGrounding(0f, collisionFlags, allowSnap: true);
        if (rawGrounded)
        {
            lastGroundedTime = Time.time;
            TryTriggerLanding(forceLanding: true);
        }

        UpdateState();
        return true;
    }

    private void UpdateFlightRotation(float deltaTime)
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(currentFlightVelocity, Vector3.up);
        Vector3 lookDirection = planarVelocity.sqrMagnitude > 0.04f
            ? planarVelocity.normalized
            : desiredWorldDirection;

        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        float turnRate = flightBoostActive ? flightBoostTurnRate : flightTurnRate;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnRate * deltaTime);
    }

    private void ApplyFlightCollisionFeedback(CollisionFlags collisionFlags)
    {
        if ((collisionFlags & CollisionFlags.Above) != 0 && currentFlightVelocity.y > 0f)
        {
            currentFlightVelocity.y = 0f;
        }

        if ((collisionFlags & CollisionFlags.Below) != 0 && currentFlightVelocity.y < 0f)
        {
            currentFlightVelocity.y = 0f;
        }

        if ((collisionFlags & CollisionFlags.Sides) != 0 && wallHitNormal.sqrMagnitude > 0.0001f)
        {
            currentFlightVelocity = Vector3.ProjectOnPlane(currentFlightVelocity, wallHitNormal);
        }
    }

    private void TryStartJump()
    {
        if (jumpBufferTimer <= 0f || !CanStartJump())
        {
            return;
        }

        jumpBufferTimer = 0f;
        jumpGroundIgnoreTimer = jumpGroundIgnoreTime;
        jumpStartedThisFrame = true;
        stableGrounded = false;
        rawGrounded = false;
        timeSinceGrounded = groundedGraceTime + 0.001f;
        verticalVelocity = jumpImpulse;
        airborneTime = 0f;
        airbornePeakDownwardSpeed = 0f;
        freeFall = false;
        landingTriggeredThisFrame = false;
        landingFromFlightTriggeredThisFrame = false;
        lastLandingSeverity = LandingSeverity.None;
        landingDampingTimer = 0f;
        landingDampingStrength = 0f;
        flightLandingDampingActive = false;
    }

    private bool CanStartJump()
    {
        if (jumpGroundIgnoreTimer > 0f)
        {
            return false;
        }

        return stableGrounded || timeSinceGrounded <= jumpGroundedGraceTime;
    }

    private void UpdatePlanarVelocity(float deltaTime)
    {
        Vector3 desiredVelocity = desiredWorldDirection * DesiredSpeed;
        float currentSpeed = ActualSpeed;
        bool hasInput = InputMagnitude > 0f && desiredWorldDirection != Vector3.zero;
        bool hardReversal = ShouldHardReverse(hasInput, currentSpeed);
        brakingForHardReversal = hardReversal && currentSpeed > hardReverseReleaseSpeed;

        if (brakingForHardReversal)
        {
            float brakeRate = deceleration * hardReverseBrakeMultiplier;
            currentPlanarVelocity = Vector3.MoveTowards(currentPlanarVelocity, Vector3.zero, brakeRate * deltaTime);
            return;
        }

        float rate = hasInput ? acceleration : deceleration;
        currentPlanarVelocity = Vector3.MoveTowards(currentPlanarVelocity, desiredVelocity, rate * deltaTime);

        if (!hasInput && currentPlanarVelocity.sqrMagnitude <= idleSpeedThreshold * idleSpeedThreshold)
        {
            currentPlanarVelocity = Vector3.zero;
        }
    }

    private void ApplyLandingDamping(float deltaTime)
    {
        if (landingDampingTimer <= 0f || landingDampingStrength <= 0f || currentPlanarVelocity.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (flightLandingDampingActive && desiredWorldDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 alignedVelocity = Vector3.Project(currentPlanarVelocity, desiredWorldDirection);
            Vector3 driftVelocity = currentPlanarVelocity - alignedVelocity;

            if (Vector3.Dot(alignedVelocity, desiredWorldDirection) < 0f)
            {
                alignedVelocity = Vector3.MoveTowards(
                    alignedVelocity,
                    Vector3.zero,
                    landingDampingStrength * deltaTime);
            }

            driftVelocity = Vector3.MoveTowards(
                driftVelocity,
                Vector3.zero,
                landingDampingStrength * deltaTime);
            currentPlanarVelocity = alignedVelocity + driftVelocity;
            return;
        }

        currentPlanarVelocity = Vector3.MoveTowards(
            currentPlanarVelocity,
            Vector3.zero,
            landingDampingStrength * deltaTime);
    }

    private bool ShouldHardReverse(bool hasInput, float currentSpeed)
    {
        if (flightLandingControlGraceTimer > 0f ||
            !hasInput ||
            currentSpeed < hardReverseMinSpeed ||
            currentPlanarVelocity.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        float angle = Vector3.Angle(currentPlanarVelocity, desiredWorldDirection);
        return angle >= hardReverseAngle;
    }

    private void UpdateRotation(float deltaTime)
    {
        if (desiredWorldDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(desiredWorldDirection, Vector3.up);
        float turnRate = ResolveTurnRate();
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnRate * deltaTime);
    }

    private float ResolveTurnRate()
    {
        float referenceSpeed = ResolveCurrentGroundMoveSpeed();
        if (referenceSpeed <= 0f)
        {
            return lowSpeedTurnRate;
        }

        float speedRatio = Mathf.Clamp01(ActualSpeed / referenceSpeed);
        if (speedRatio < sprintTurnThreshold)
        {
            float t = Mathf.InverseLerp(0f, sprintTurnThreshold, speedRatio);
            return Mathf.Lerp(lowSpeedTurnRate, moveTurnRate, t);
        }

        float sprintT = Mathf.InverseLerp(sprintTurnThreshold, 1f, speedRatio);
        return Mathf.Lerp(moveTurnRate, sprintTurnRate, sprintT);
    }

    private float ResolveCurrentGroundMoveSpeed()
    {
        float sprintSpeed = Mathf.Max(0f, maxMoveSpeed);
        float walkingSpeed = Mathf.Clamp(walkMoveSpeed, 0f, sprintSpeed);
        return sprintInput ? sprintSpeed : walkingSpeed;
    }

    private void UpdateVerticalVelocity(float deltaTime)
    {
        if (stableGrounded && !jumpStartedThisFrame && verticalVelocity <= 0f)
        {
            verticalVelocity = -groundedStickVelocity;
            return;
        }

        float gravityScale = wallSliding ? wallSlideGravityMultiplier : 1f;
        verticalVelocity = Mathf.Max(verticalVelocity + gravity * gravityScale * deltaTime, -maxFallSpeed);
    }

    private Vector3 ResolveGroundAdjustedPlanarDisplacement(float deltaTime)
    {
        Vector3 displacement = currentPlanarVelocity * deltaTime;
        if (!stableGrounded || groundNormal == Vector3.up || displacement.sqrMagnitude <= 0.000001f)
        {
            return displacement;
        }

        Vector3 groundAlignedDisplacement = Vector3.ProjectOnPlane(displacement, groundNormal);
        float alignedDistance = groundAlignedDisplacement.magnitude;
        if (alignedDistance <= 0.000001f)
        {
            return displacement;
        }

        // Preserve the intended travel distance when following a walkable slope.
        return groundAlignedDisplacement * (displacement.magnitude / alignedDistance);
    }

    private Vector3 ResolveStepAdjustedPlanarDisplacement(Vector3 planarDisplacement)
    {
        if (!enableStepTraversal ||
            !stableGrounded ||
            jumpGroundIgnoreTimer > 0f ||
            characterController == null ||
            maxStepRise <= 0f)
        {
            return planarDisplacement;
        }

        Vector3 horizontalDisplacement = Vector3.ProjectOnPlane(planarDisplacement, Vector3.up);
        float horizontalDistance = horizontalDisplacement.magnitude;
        if (horizontalDistance <= 0.0001f)
        {
            return planarDisplacement;
        }

        if (!TryGetControllerCapsule(out Vector3 point1, out Vector3 point2, out float radius))
        {
            return planarDisplacement;
        }

        Vector3 direction = horizontalDisplacement / horizontalDistance;
        Vector3 footPoint = point2 - Vector3.up * radius;
        if (!TrySampleTraversalGround(
                footPoint,
                groundProbeStartOffset + stepContactOffset + 0.05f,
                groundProbeDistance + stepContactOffset + 0.05f,
                out RaycastHit currentSupport))
        {
            return planarDisplacement;
        }

        float castRadius = ResolveTraversalCastRadius(radius);
        int mask = groundMask;
        if (TryGetTraversalBlockingHit(
                point1,
                point2,
                castRadius,
                direction,
                horizontalDistance + ResolveTraversalSkin(),
                mask,
                out RaycastHit blockingHit))
        {
            if (TryResolveStepUpDisplacement(
                    point1,
                    point2,
                    radius,
                    footPoint,
                    currentSupport,
                    direction,
                    horizontalDistance,
                    blockingHit.distance,
                    mask,
                    out Vector3 stepDisplacement))
            {
                return stepDisplacement;
            }

            stepTraversalBlocked = true;
            return planarDisplacement;
        }

        if (TryResolveStepDownDisplacement(
                point1,
                point2,
                radius,
                footPoint,
                currentSupport,
                horizontalDisplacement,
                mask,
                out Vector3 snappedDisplacement))
        {
            return snappedDisplacement;
        }

        return planarDisplacement;
    }

    private bool TryResolveStepUpDisplacement(
        Vector3 point1,
        Vector3 point2,
        float radius,
        Vector3 footPoint,
        RaycastHit currentSupport,
        Vector3 direction,
        float horizontalDistance,
        float blockingHitDistance,
        int mask,
        out Vector3 stepDisplacement)
    {
        stepDisplacement = Vector3.zero;

        float startDistance = Mathf.Clamp(
            blockingHitDistance + radius + ResolveTraversalSkin() + stepSurfaceInset,
            0.05f,
            stepSearchDistance);
        float scanDistance = Mathf.Min(stepSearchDistance, startDistance + stepSearchExtraDistance);
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(stepSearchExtraDistance / 0.05f) + 1);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1
                ? startDistance
                : Mathf.Lerp(startDistance, scanDistance, i / (sampleCount - 1f));
            Vector3 sampleFootPoint = footPoint + direction * t;
            if (!TrySampleTraversalGround(
                    sampleFootPoint,
                    maxStepRise + groundProbeStartOffset + stepContactOffset,
                    groundProbeDistance + stepContactOffset + 0.05f,
                    out RaycastHit stepSupport))
            {
                continue;
            }

            float rise = stepSupport.point.y - currentSupport.point.y;
            if (rise < minStepRise || rise > maxStepRise)
            {
                continue;
            }

            float verticalOffset = rise + stepContactOffset;
            float castRadius = ResolveTraversalCastRadius(radius);
            Vector3 raisedPoint1 = point1 + Vector3.up * verticalOffset;
            Vector3 raisedPoint2 = point2 + Vector3.up * verticalOffset;
            if (TryGetTraversalBlockingHit(
                    raisedPoint1,
                    raisedPoint2,
                    castRadius,
                    direction,
                    horizontalDistance + ResolveTraversalSkin(),
                    mask,
                    out _))
            {
                continue;
            }

            Vector3 horizontalDisplacement = direction * horizontalDistance;
            Vector3 finalPoint1 = point1 + horizontalDisplacement + Vector3.up * verticalOffset;
            Vector3 finalPoint2 = point2 + horizontalDisplacement + Vector3.up * verticalOffset;
            if (!IsTraversalCapsuleClear(finalPoint1, finalPoint2, castRadius, mask))
            {
                continue;
            }

            stepTraversalActive = true;
            stepTraversalVerticalOffset = verticalOffset;
            stepDisplacement = horizontalDisplacement + Vector3.up * verticalOffset;
            return true;
        }

        return false;
    }

    private bool TryResolveStepDownDisplacement(
        Vector3 point1,
        Vector3 point2,
        float radius,
        Vector3 footPoint,
        RaycastHit currentSupport,
        Vector3 horizontalDisplacement,
        int mask,
        out Vector3 snappedDisplacement)
    {
        snappedDisplacement = horizontalDisplacement;
        if (maxStepDrop <= 0f)
        {
            return false;
        }

        Vector3 targetFootPoint = footPoint + horizontalDisplacement;
        if (!TrySampleTraversalGround(
                targetFootPoint,
                groundProbeStartOffset + stepContactOffset + 0.05f,
                maxStepDrop + groundProbeStartOffset + stepContactOffset,
                out RaycastHit targetSupport))
        {
            return false;
        }

        float supportDelta = targetSupport.point.y - currentSupport.point.y;
        if (supportDelta >= -minStepRise || supportDelta < -maxStepDrop)
        {
            return false;
        }

        float verticalOffset = targetSupport.point.y + stepContactOffset - targetFootPoint.y;
        if (verticalOffset >= -minStepRise)
        {
            return false;
        }

        float castRadius = ResolveTraversalCastRadius(radius);
        Vector3 finalPoint1 = point1 + horizontalDisplacement + Vector3.up * verticalOffset;
        Vector3 finalPoint2 = point2 + horizontalDisplacement + Vector3.up * verticalOffset;
        if (!IsTraversalCapsuleClear(finalPoint1, finalPoint2, castRadius, mask))
        {
            return false;
        }

        stepTraversalActive = true;
        stepTraversalVerticalOffset = verticalOffset;
        snappedDisplacement = horizontalDisplacement + Vector3.up * verticalOffset;
        return true;
    }

    private bool TryGetControllerCapsule(out Vector3 point1, out Vector3 point2, out float radius)
    {
        point1 = Vector3.zero;
        point2 = Vector3.zero;
        radius = 0f;
        if (characterController == null)
        {
            return false;
        }

        Vector3 center = transform.TransformPoint(characterController.center);
        Vector3 scale = transform.lossyScale;
        float maxXZ = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float absY = Mathf.Abs(scale.y);
        radius = Mathf.Max(0.01f, characterController.radius * maxXZ);
        float height = Mathf.Max(characterController.height * absY, radius * 2f);
        float segmentHalf = Mathf.Max(0f, (height * 0.5f) - radius);
        point1 = center + Vector3.up * segmentHalf;
        point2 = center - Vector3.up * segmentHalf;
        return true;
    }

    private bool TrySampleTraversalGround(Vector3 footPoint, float maxUp, float maxDown, out RaycastHit hit)
    {
        hit = default;
        float upRange = Mathf.Max(0.02f, maxUp);
        float downRange = Mathf.Max(0.02f, maxDown);
        Vector3 origin = footPoint + Vector3.up * (upRange + 0.05f);
        float distance = upRange + downRange + 0.1f;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            traversalCastHits,
            distance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.PositiveInfinity;
        int bestIndex = -1;
        float walkableDot = Mathf.Cos(ResolveSlopeLimit() * Mathf.Deg2Rad);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = traversalCastHits[i];
            if (candidate.collider == null || IsSelfCollider(candidate.collider))
            {
                continue;
            }

            float heightOffset = candidate.point.y - footPoint.y;
            if (heightOffset > upRange || heightOffset < -downRange)
            {
                continue;
            }

            if (Vector3.Dot(candidate.normal, Vector3.up) < walkableDot)
            {
                continue;
            }

            if (candidate.distance < bestDistance)
            {
                bestDistance = candidate.distance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        hit = traversalCastHits[bestIndex];
        return true;
    }

    private bool TryGetTraversalBlockingHit(
        Vector3 point1,
        Vector3 point2,
        float radius,
        Vector3 direction,
        float distance,
        int mask,
        out RaycastHit hit)
    {
        hit = default;
        if (mask == 0 || distance <= 0.0001f)
        {
            return false;
        }

        int hitCount = Physics.CapsuleCastNonAlloc(
            point1,
            point2,
            radius,
            direction,
            traversalCastHits,
            distance,
            mask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.PositiveInfinity;
        int bestIndex = -1;
        float walkableDot = Mathf.Cos(ResolveSlopeLimit() * Mathf.Deg2Rad);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = traversalCastHits[i];
            if (candidate.collider == null || IsSelfCollider(candidate.collider))
            {
                continue;
            }

            if (Vector3.Dot(candidate.normal, Vector3.up) >= walkableDot)
            {
                continue;
            }

            if (candidate.distance < bestDistance)
            {
                bestDistance = candidate.distance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        hit = traversalCastHits[bestIndex];
        return true;
    }

    private bool IsTraversalCapsuleClear(Vector3 point1, Vector3 point2, float radius, int mask)
    {
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            point1,
            point2,
            radius,
            traversalOverlapHits,
            mask,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = traversalOverlapHits[i];
            if (col == null || IsSelfCollider(col))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private float ResolveTraversalSkin()
    {
        return characterController != null ? Mathf.Max(0.005f, characterController.skinWidth) : 0.03f;
    }

    private float ResolveTraversalCastRadius(float radius)
    {
        return Mathf.Max(0.01f, radius - ResolveTraversalSkin());
    }

    private void ApplyCollisionFeedback(CollisionFlags collisionFlags)
    {
        if ((collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }

        if (!wallSliding && (collisionFlags & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
        {
            verticalVelocity = -groundedStickVelocity;
        }
    }

    private void RefreshGrounding(float deltaTime, CollisionFlags collisionFlags, bool allowSnap)
    {
        if (ShouldIgnoreGroundingForJump())
        {
            rawGrounded = false;
            stableGrounded = false;
            groundNormal = Vector3.up;
            groundAngle = 0f;
            timeSinceGrounded = float.IsPositiveInfinity(timeSinceGrounded)
                ? deltaTime
                : timeSinceGrounded + deltaTime;
            return;
        }

        bool hitWalkableGround = TryProbeGround(groundProbeDistance, out RaycastHit hit);
        bool belowCollision = (collisionFlags & CollisionFlags.Below) != 0 && (!wallSliding || hitWalkableGround);

        rawGrounded = hitWalkableGround || belowCollision;
        if (hitWalkableGround)
        {
            ApplyGroundHit(hit);
        }
        else if (belowCollision)
        {
            groundNormal = Vector3.up;
            groundAngle = 0f;
        }
        else if (!belowCollision)
        {
            groundNormal = Vector3.up;
            groundAngle = 0f;
        }

        if (!rawGrounded && allowSnap && CanSnapToGround() && TrySnapToGround())
        {
            hitWalkableGround = TryProbeGround(groundProbeDistance, out hit);
            rawGrounded = hitWalkableGround;
            if (hitWalkableGround)
            {
                ApplyGroundHit(hit);
            }
        }

        if (rawGrounded && verticalVelocity <= 0.01f)
        {
            stableGrounded = true;
            timeSinceGrounded = 0f;
            return;
        }

        timeSinceGrounded = float.IsPositiveInfinity(timeSinceGrounded)
            ? deltaTime
            : timeSinceGrounded + deltaTime;

        stableGrounded = verticalVelocity <= 0f && timeSinceGrounded <= groundedGraceTime;
    }

    private bool ShouldIgnoreGroundingForJump()
    {
        return jumpGroundIgnoreTimer > 0f && verticalVelocity > 0f;
    }

    private bool CanSnapToGround()
    {
        return groundSnapDistance > 0f &&
               !wallSliding &&
               jumpGroundIgnoreTimer <= 0f &&
               verticalVelocity <= 0f &&
               timeSinceGrounded <= groundedGraceTime;
    }

    private bool TrySnapToGround()
    {
        if (!TryProbeGround(groundSnapDistance, out RaycastHit hit))
        {
            return false;
        }

        float snapDistance = Mathf.Max(0f, hit.distance - 0.01f);
        if (snapDistance <= 0.0001f)
        {
            ApplyGroundHit(hit);
            rawGrounded = true;
            stableGrounded = true;
            snapActive = true;
            timeSinceGrounded = 0f;
            return true;
        }

        characterController.Move(Vector3.down * snapDistance);
        snapActive = true;
        ApplyGroundHit(hit);
        timeSinceGrounded = 0f;
        stableGrounded = true;
        rawGrounded = true;
        return true;
    }

    private bool TryProbeGround(float probeDistance, out RaycastHit hit)
    {
        hit = default;
        if (characterController == null)
        {
            return false;
        }

        Vector3 bottomSphereCenter = ResolveBottomSphereCenter();
        float radius = Mathf.Max(0.01f, characterController.radius * groundProbeRadiusScale);
        Vector3 origin = bottomSphereCenter + Vector3.up * groundProbeStartOffset;
        float castDistance = groundProbeStartOffset + probeDistance;

        debugProbeOrigin = origin;
        debugProbeDistance = castDistance;
        debugProbeRadius = radius;

        if (!Physics.SphereCast(
                origin,
                radius,
                Vector3.down,
                out RaycastHit probeHit,
                castDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (IsSelfCollider(probeHit.collider))
        {
            return false;
        }

        float angle = Vector3.Angle(probeHit.normal, Vector3.up);
        if (angle > ResolveSlopeLimit())
        {
            return false;
        }

        hit = probeHit;
        return true;
    }

    private void ApplyGroundHit(RaycastHit hit)
    {
        groundPoint = hit.point;
        groundNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
        groundAngle = Vector3.Angle(groundNormal, Vector3.up);
    }

    private Vector3 ResolveBottomSphereCenter()
    {
        Vector3 center = transform.TransformPoint(characterController.center);
        float halfHeight = Mathf.Max(characterController.height * 0.5f, characterController.radius);
        float bottomOffset = Mathf.Max(0f, halfHeight - characterController.radius);
        return center - transform.up * bottomOffset;
    }

    private float ResolveSlopeLimit()
    {
        float controllerLimit = characterController != null ? characterController.slopeLimit : maxGroundAngle;
        return Mathf.Min(maxGroundAngle, controllerLimit);
    }

    private bool IsSelfCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        Transform colliderTransform = collider.transform;
        return colliderTransform == transform || colliderTransform.IsChildOf(transform);
    }

    private void UpdateAirborneAndLanding(float deltaTime, bool wasRawGrounded)
    {
        if (!rawGrounded)
        {
            airborneTime += deltaTime;

            if (verticalVelocity < 0f)
            {
                airbornePeakDownwardSpeed = Mathf.Max(airbornePeakDownwardSpeed, -verticalVelocity);
            }

            freeFall = airborneTime >= freeFallMinAirborneTime &&
                       verticalVelocity <= -freeFallMinDownwardSpeed;
            return;
        }

        bool landedFromWallSlide = wallSliding;
        ClearWallSlideState();
        lastGroundedTime = Time.time;

        if (!wasRawGrounded)
        {
            TryTriggerLanding(landedFromWallSlide);
        }

        airborneTime = 0f;
        airbornePeakDownwardSpeed = 0f;
        freeFall = false;
    }

    private void TryTriggerLanding(bool forceLanding = false)
    {
        bool fromFlight = forceLandingAfterFlight;
        forceLanding |= fromFlight;
        forceLandingAfterFlight = false;

        LandingSeverity severity = ResolveLandingSeverity(airbornePeakDownwardSpeed);
        if (forceLanding && severity == LandingSeverity.None)
        {
            severity = LandingSeverity.Light;
        }

        bool meaningfulLanding = severity != LandingSeverity.None &&
                                 (forceLanding || airborneTime >= landingMinAirborneTime);

        if (!meaningfulLanding)
        {
            landingFromFlightTriggeredThisFrame = false;
            lastLandingSeverity = LandingSeverity.None;
            return;
        }

        landingTriggeredThisFrame = true;
        landingFromFlightTriggeredThisFrame = fromFlight;
        lastLandingSeverity = severity;

        if (fromFlight)
        {
            StabilizePlanarVelocityAfterFlightLanding();
            landingDampingTimer = landingDampingDuration * flightLandingDampingMultiplier;
            landingDampingStrength = ResolveLandingDamping(severity) * flightLandingDampingMultiplier;
            flightLandingDampingActive = landingDampingTimer > 0f && landingDampingStrength > 0f;
            flightLandingControlGraceTimer = Mathf.Max(flightLandingControlGraceTimer, flightLandingControlGraceTime);
            brakingForHardReversal = false;
            return;
        }

        landingDampingTimer = landingDampingDuration;
        landingDampingStrength = ResolveLandingDamping(severity);
        flightLandingDampingActive = false;
    }

    private void StabilizePlanarVelocityAfterFlightLanding()
    {
        currentPlanarVelocity *= flightLandingPlanarVelocityRetention;
        if (InputMagnitude <= 0f || desiredWorldDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float retainedSpeed = currentPlanarVelocity.magnitude;
        if (retainedSpeed <= 0.0001f)
        {
            return;
        }

        float maxGroundSpeed = ResolveCurrentGroundMoveSpeed();
        float alignedSpeed = maxGroundSpeed > 0f
            ? Mathf.Min(retainedSpeed, maxGroundSpeed)
            : retainedSpeed;
        currentPlanarVelocity = desiredWorldDirection * alignedSpeed;
    }

    private LandingSeverity ResolveLandingSeverity(float downwardSpeed)
    {
        if (downwardSpeed < landingMinDownwardSpeed)
        {
            return LandingSeverity.None;
        }

        if (downwardSpeed >= heavyLandingDownwardSpeed)
        {
            return LandingSeverity.Heavy;
        }

        if (downwardSpeed >= mediumLandingDownwardSpeed)
        {
            return LandingSeverity.Medium;
        }

        return LandingSeverity.Light;
    }

    private float ResolveLandingDamping(LandingSeverity severity)
    {
        switch (severity)
        {
            case LandingSeverity.Heavy:
                return heavyLandingDamping;
            case LandingSeverity.Medium:
                return mediumLandingDamping;
            case LandingSeverity.Light:
                return lightLandingDamping;
            default:
                return 0f;
        }
    }

    private void UpdateState()
    {
        if (IsLadderTraversalActive)
        {
            currentState = MovementState.Ladder;
            return;
        }

        if (flightActive)
        {
            currentState = MovementState.Flight;
            return;
        }

        if (landingTriggeredThisFrame || landingDampingTimer > 0f)
        {
            currentState = MovementState.Landing;
            return;
        }

        if (jumpStartedThisFrame)
        {
            currentState = MovementState.JumpStart;
            return;
        }

        if (!stableGrounded)
        {
            if (wallSliding)
            {
                currentState = MovementState.WallSlide;
                return;
            }

            currentState = MovementState.Airborne;
            return;
        }

        if (brakingForHardReversal)
        {
            currentState = MovementState.Brake;
            return;
        }

        if (InputMagnitude > 0f)
        {
            currentState = MovementState.Locomotion;
            return;
        }

        currentState = currentPlanarVelocity.sqrMagnitude > idleSpeedThreshold * idleSpeedThreshold
            ? MovementState.Brake
            : MovementState.Idle;
    }

    private void SetLadderTraversalState()
    {
        InputMagnitude = 0f;
        DesiredSpeed = 0f;
        desiredWorldDirection = Vector3.zero;
        currentPlanarVelocity = Vector3.zero;
        verticalVelocity = 0f;
        sprintInput = false;
        brakingForHardReversal = false;
        snapActive = false;
        jumpBufferTimer = 0f;
        jumpGroundIgnoreTimer = 0f;
        jumpStartedThisFrame = false;
        landingTriggeredThisFrame = false;
        landingFromFlightTriggeredThisFrame = false;
        lastLandingSeverity = LandingSeverity.None;
        landingDampingTimer = 0f;
        landingDampingStrength = 0f;
        flightLandingDampingActive = false;
        stepTraversalActive = false;
        stepTraversalVerticalOffset = 0f;
        stepTraversalBlocked = false;
        ClearWallSlideState();
        flightActive = false;
        flightBoostInput = false;
        currentFlightVelocity = Vector3.zero;
        airborneTime = 0f;
        airbornePeakDownwardSpeed = 0f;
        freeFall = false;
        rawGrounded = false;
        stableGrounded = false;
        groundNormal = Vector3.up;
        groundAngle = 0f;
        timeSinceGrounded = groundedGraceTime + 0.001f;
        flightBoostActive = false;
        wasFlightBoostActive = false;
        flightBoostStartedThisFrame = false;
        flightVerticalInput = 0f;
        flightTakeoffTimer = 0f;
        forceLandingAfterFlight = false;
        flightLandingControlGraceTimer = 0f;
        currentState = MovementState.Ladder;
    }

    private void UpdateDebugValues()
    {
        if (!showDebugValues)
        {
            return;
        }

        debugCurrentState = currentState;
        debugInputMagnitude = InputMagnitude;
        debugDesiredSpeed = DesiredSpeed;
        debugActualSpeed = ActualSpeed;
        debugCurrentPlanarVelocity = currentPlanarVelocity;
        debugRawGrounded = rawGrounded;
        debugStableGrounded = stableGrounded;
        debugGroundNormal = groundNormal;
        debugGroundAngle = groundAngle;
        debugVerticalVelocity = verticalVelocity;
        debugTimeSinceGrounded = timeSinceGrounded;
        debugSnapActive = snapActive;
        debugJumpRequested = JumpRequested;
        debugJumpStarted = jumpStartedThisFrame;
        debugAirborne = !rawGrounded;
        debugFreeFall = freeFall;
        debugFlightActive = flightActive;
        debugFlightBoosting = FlightBoosting;
        debugFlightBoostStarted = flightBoostStartedThisFrame;
        debugFlightTakeoffActive = FlightTakeoffActive;
        debugFlightVerticalInput = flightVerticalInput;
        debugFlightVelocity = currentFlightVelocity;
        debugFlightNormalizedSpeed = FlightNormalizedSpeed;
        debugLandingTriggered = landingTriggeredThisFrame;
        debugLandingSeverity = lastLandingSeverity;
        debugWallSliding = wallSliding;
        debugWallSlideSide = wallSlideSide;
        debugWallSlideNormal = wallSlideNormal;
        debugStepTraversalActive = stepTraversalActive;
        debugStepTraversalOffset = stepTraversalVerticalOffset;
        debugStepTraversalBlocked = stepTraversalBlocked;
        debugAirborneTime = airborneTime;
        debugLastGroundedTime = lastGroundedTime;
        debugLadderTraversalActive = IsLadderTraversalActive;
    }

    private void ResolveMainCamera()
    {
        Camera mainCamera = Camera.main;
        cameraTransform = mainCamera != null ? mainCamera.transform : null;
    }

    private void ConfigureExplicitStepTraversal()
    {
        if (enableStepTraversal && characterController != null)
        {
            characterController.stepOffset = MinimumCharacterControllerStepOffset;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos || characterController == null)
        {
            return;
        }

        Gizmos.color = stableGrounded ? Color.green : rawGrounded ? Color.yellow : Color.red;
        Gizmos.DrawWireSphere(debugProbeOrigin, debugProbeRadius);
        Gizmos.DrawLine(debugProbeOrigin, debugProbeOrigin + Vector3.down * debugProbeDistance);

        Gizmos.color = snapActive ? Color.cyan : Color.white;
        Gizmos.DrawLine(groundPoint, groundPoint + groundNormal);

        Gizmos.color = Color.blue;
        Vector3 position = transform.position + Vector3.up * 0.1f;
        Gizmos.DrawLine(position, position + desiredWorldDirection);

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(position + Vector3.up * 0.1f, position + Vector3.up * 0.1f + currentPlanarVelocity);

        if (flightActive)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(position + Vector3.up * 0.2f, position + Vector3.up * 0.2f + currentFlightVelocity);
        }

        if (wallSliding)
        {
            Gizmos.DrawLine(position + Vector3.up * 0.35f, position + Vector3.up * 0.35f + wallSlideNormal);
        }

        Gizmos.color = freeFall ? Color.red : Color.yellow;
        Vector3 verticalStart = position + Vector3.up * 0.25f;
        Gizmos.DrawLine(verticalStart, verticalStart + Vector3.up * Mathf.Clamp(verticalVelocity * 0.1f, -2f, 2f));
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!enableAirborneWallSlide || hit.collider == null || hit.collider.transform.IsChildOf(transform))
        {
            return;
        }

        if (Mathf.Abs(hit.normal.y) > wallSlideMaxNormalY)
        {
            return;
        }

        Vector3 normal = hit.normal;
        normal.y = 0f;
        if (normal.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        wallHitThisFrame = true;
        wallHitNormal = normal.normalized;
    }

    private void ResetWallHitFrame()
    {
        wallHitThisFrame = false;
        wallHitNormal = Vector3.zero;
    }

    private void UpdateWallSlideState(float deltaTime, CollisionFlags collisionFlags)
    {
        if (!CanEvaluateWallSlide(collisionFlags))
        {
            ClearWallSlideState();
            return;
        }

        bool hasWallContact = wallHitThisFrame;
        if (wallHitThisFrame)
        {
            wallSlideContactTimer = wallSlideContactMemoryTime;
            wallSlideNormal = wallHitNormal;
        }
        else if (wallSlideContactTimer > 0f)
        {
            wallSlideContactTimer = Mathf.Max(0f, wallSlideContactTimer - deltaTime);
            hasWallContact = wallSlideContactTimer > 0f;
        }

        if (!hasWallContact || wallSlideNormal.sqrMagnitude <= 0.0001f)
        {
            ClearWallSlideState();
            return;
        }

        wallSliding = true;
        wallSlideSide = ResolveWallSlideSide(wallSlideNormal);

        Vector3 planarSlide = Vector3.ProjectOnPlane(currentPlanarVelocity, wallSlideNormal);
        planarSlide.y = 0f;
        currentPlanarVelocity = planarSlide;

        float minimumDownwardSpeed = Mathf.Min(Mathf.Max(0f, wallSlideMinDownwardSpeed), maxFallSpeed);
        if (minimumDownwardSpeed > 0f && verticalVelocity > -minimumDownwardSpeed)
        {
            verticalVelocity = -minimumDownwardSpeed;
        }
    }

    private bool CanEvaluateWallSlide(CollisionFlags collisionFlags)
    {
        return enableAirborneWallSlide &&
               !stableGrounded &&
               (collisionFlags & CollisionFlags.Sides) != 0 &&
               timeSinceGrounded > groundedGraceTime;
    }

    private int ResolveWallSlideSide(Vector3 normal)
    {
        Vector3 wallDirectionFromCharacter = -normal;
        float sideDot = Vector3.Dot(transform.right, wallDirectionFromCharacter);
        return sideDot >= 0f ? 1 : -1;
    }

    private void ClearWallSlideState()
    {
        wallSliding = false;
        wallSlideSide = 0;
        wallSlideNormal = Vector3.zero;
        wallSlideContactTimer = 0f;
        wallHitThisFrame = false;
        wallHitNormal = Vector3.zero;
    }
}
