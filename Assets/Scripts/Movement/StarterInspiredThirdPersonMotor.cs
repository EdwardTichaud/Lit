using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class StarterInspiredThirdPersonMotor : MonoBehaviour
{
    public enum MovementState
    {
        Idle,
        Locomotion,
        Brake,
        JumpStart,
        Airborne,
        Landing,
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
    [SerializeField, Min(0f)] private float maxMoveSpeed = 3.25f;
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
    [SerializeField] private bool debugLandingTriggered;
    [SerializeField] private LandingSeverity debugLandingSeverity;
    [SerializeField] private float debugAirborneTime;
    [SerializeField] private float debugLastGroundedTime;
    [SerializeField] private bool debugLadderTraversalActive;

    private Vector2 moveInput;
    private Vector3 desiredWorldDirection;
    private Vector3 currentPlanarVelocity;
    private MovementState currentState;
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
    private LandingSeverity lastLandingSeverity;
    private float airborneTime;
    private float airbornePeakDownwardSpeed;
    private float lastGroundedTime;
    private float landingDampingTimer;
    private float landingDampingStrength;
    private int ladderTraversalLockCount;
    private Vector3 debugProbeOrigin;
    private float debugProbeDistance;
    private float debugProbeRadius;

    public MovementState CurrentState => currentState;
    public float InputMagnitude { get; private set; }
    public float DesiredSpeed { get; private set; }
    public float ActualSpeed => new Vector3(currentPlanarVelocity.x, 0f, currentPlanarVelocity.z).magnitude;
    public Vector3 CurrentPlanarVelocity => currentPlanarVelocity;
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
    public bool Airborne => !rawGrounded;
    public bool FreeFall => freeFall;
    public bool LandingTriggered => landingTriggeredThisFrame;
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

        lastGroundedTime = Time.time;
        RefreshGrounding(0f, CollisionFlags.None, allowSnap: false);
        UpdateState();
        UpdateDebugValues();
    }

    private void OnValidate()
    {
        inputDeadZone = Mathf.Clamp(inputDeadZone, 0f, 0.5f);
        maxMoveSpeed = Mathf.Max(0f, maxMoveSpeed);
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
        maxFallSpeed = Mathf.Max(0f, maxFallSpeed);
        groundedStickVelocity = Mathf.Max(0f, groundedStickVelocity);
        groundSnapDistance = Mathf.Max(0f, groundSnapDistance);
        jumpImpulse = Mathf.Max(0f, jumpImpulse);
        jumpInputBufferTime = Mathf.Max(0f, jumpInputBufferTime);
        jumpGroundedGraceTime = Mathf.Max(0f, jumpGroundedGraceTime);
        jumpGroundIgnoreTime = Mathf.Max(0f, jumpGroundIgnoreTime);
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

    public void RequestJump()
    {
        jumpBufferTimer = Mathf.Max(jumpInputBufferTime, Time.deltaTime);
    }

    public void Jump()
    {
        RequestJump();
    }

    public void ResetMotionState(bool clearInput = true)
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
        brakingForHardReversal = false;
        snapActive = false;
        jumpBufferTimer = 0f;
        jumpGroundIgnoreTimer = 0f;
        jumpStartedThisFrame = false;
        freeFall = false;
        landingTriggeredThisFrame = false;
        lastLandingSeverity = LandingSeverity.None;
        airborneTime = 0f;
        airbornePeakDownwardSpeed = 0f;
        landingDampingTimer = 0f;
        landingDampingStrength = 0f;
        lastGroundedTime = Time.time;
        RefreshGrounding(0f, CollisionFlags.None, allowSnap: false);
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

        ResetMotionState(clearInput);
    }

    private void Tick(float deltaTime)
    {
        if (deltaTime <= 0f || characterController == null)
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

        bool wasRawGrounded = rawGrounded;

        snapActive = false;
        jumpStartedThisFrame = false;
        landingTriggeredThisFrame = false;
        UpdateTimers(deltaTime);

        Vector2 processedInput = ProcessMoveInput(moveInput);
        InputMagnitude = processedInput.magnitude;
        desiredWorldDirection = ResolveCameraRelativeDirection(processedInput);
        DesiredSpeed = InputMagnitude * maxMoveSpeed;

        TryStartJump();
        UpdatePlanarVelocity(deltaTime);
        ApplyLandingDamping(deltaTime);
        UpdateRotation(deltaTime);
        UpdateVerticalVelocity(deltaTime);

        Vector3 displacement = ResolveGroundAdjustedPlanarDisplacement(deltaTime);
        displacement += Vector3.up * (verticalVelocity * deltaTime);

        CollisionFlags collisionFlags = characterController.Move(displacement);
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
        lastLandingSeverity = LandingSeverity.None;
        landingDampingTimer = 0f;
        landingDampingStrength = 0f;
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

        currentPlanarVelocity = Vector3.MoveTowards(
            currentPlanarVelocity,
            Vector3.zero,
            landingDampingStrength * deltaTime);
    }

    private bool ShouldHardReverse(bool hasInput, float currentSpeed)
    {
        if (!hasInput || currentSpeed < hardReverseMinSpeed || currentPlanarVelocity.sqrMagnitude <= 0.0001f)
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
        if (maxMoveSpeed <= 0f)
        {
            return lowSpeedTurnRate;
        }

        float speedRatio = Mathf.Clamp01(ActualSpeed / maxMoveSpeed);
        if (speedRatio < sprintTurnThreshold)
        {
            float t = Mathf.InverseLerp(0f, sprintTurnThreshold, speedRatio);
            return Mathf.Lerp(lowSpeedTurnRate, moveTurnRate, t);
        }

        float sprintT = Mathf.InverseLerp(sprintTurnThreshold, 1f, speedRatio);
        return Mathf.Lerp(moveTurnRate, sprintTurnRate, sprintT);
    }

    private void UpdateVerticalVelocity(float deltaTime)
    {
        if (stableGrounded && !jumpStartedThisFrame && verticalVelocity <= 0f)
        {
            verticalVelocity = -groundedStickVelocity;
            return;
        }

        verticalVelocity = Mathf.Max(verticalVelocity + gravity * deltaTime, -maxFallSpeed);
    }

    private Vector3 ResolveGroundAdjustedPlanarDisplacement(float deltaTime)
    {
        Vector3 displacement = currentPlanarVelocity * deltaTime;
        if (!stableGrounded || groundNormal == Vector3.up || displacement.sqrMagnitude <= 0.000001f)
        {
            return displacement;
        }

        return Vector3.ProjectOnPlane(displacement, groundNormal);
    }

    private void ApplyCollisionFeedback(CollisionFlags collisionFlags)
    {
        if ((collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }

        if ((collisionFlags & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
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
        bool belowCollision = (collisionFlags & CollisionFlags.Below) != 0;

        rawGrounded = hitWalkableGround || belowCollision;
        if (hitWalkableGround)
        {
            ApplyGroundHit(hit);
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

        if (probeHit.collider != null && probeHit.collider.transform.IsChildOf(transform))
        {
            return false;
        }

        float angle = Vector3.Angle(probeHit.normal, Vector3.up);
        if (angle > ResolveSlopeLimit())
        {
            groundPoint = probeHit.point;
            groundNormal = probeHit.normal;
            groundAngle = angle;
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

        lastGroundedTime = Time.time;

        if (!wasRawGrounded)
        {
            TryTriggerLanding();
        }

        airborneTime = 0f;
        airbornePeakDownwardSpeed = 0f;
        freeFall = false;
    }

    private void TryTriggerLanding()
    {
        LandingSeverity severity = ResolveLandingSeverity(airbornePeakDownwardSpeed);
        bool meaningfulLanding = severity != LandingSeverity.None && airborneTime >= landingMinAirborneTime;

        if (!meaningfulLanding)
        {
            lastLandingSeverity = LandingSeverity.None;
            return;
        }

        landingTriggeredThisFrame = true;
        lastLandingSeverity = severity;
        landingDampingTimer = landingDampingDuration;
        landingDampingStrength = ResolveLandingDamping(severity);
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
        brakingForHardReversal = false;
        snapActive = false;
        jumpBufferTimer = 0f;
        jumpGroundIgnoreTimer = 0f;
        jumpStartedThisFrame = false;
        landingTriggeredThisFrame = false;
        lastLandingSeverity = LandingSeverity.None;
        landingDampingTimer = 0f;
        landingDampingStrength = 0f;
        airborneTime = 0f;
        airbornePeakDownwardSpeed = 0f;
        freeFall = false;
        rawGrounded = false;
        stableGrounded = false;
        groundNormal = Vector3.up;
        groundAngle = 0f;
        timeSinceGrounded = groundedGraceTime + 0.001f;
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
        debugLandingTriggered = landingTriggeredThisFrame;
        debugLandingSeverity = lastLandingSeverity;
        debugAirborneTime = airborneTime;
        debugLastGroundedTime = lastGroundedTime;
        debugLadderTraversalActive = IsLadderTraversalActive;
    }

    private void ResolveMainCamera()
    {
        Camera mainCamera = Camera.main;
        cameraTransform = mainCamera != null ? mainCamera.transform : null;
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

        Gizmos.color = freeFall ? Color.red : Color.yellow;
        Vector3 verticalStart = position + Vector3.up * 0.25f;
        Gizmos.DrawLine(verticalStart, verticalStart + Vector3.up * Mathf.Clamp(verticalVelocity * 0.1f, -2f, 2f));
    }
}
