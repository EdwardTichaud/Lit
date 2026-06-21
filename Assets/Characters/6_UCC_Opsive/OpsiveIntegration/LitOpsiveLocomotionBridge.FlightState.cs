using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    private enum FlightBackend
    {
        None,
        UccAbility,
        Standalone
    }

    private enum FlightPresentationState
    {
        Grounded,
        Takeoff,
        Cruise,
        Landing
    }

    private const int StandaloneFlightHitCapacity = 16;
    private const float MinimumStandaloneLandingDuration = 0.15f;

    private readonly RaycastHit[] standaloneFlightHits = new RaycastHit[StandaloneFlightHitCapacity];
    private FlightBackend flightBackend;
    private FlightPresentationState flightPresentationState;
    private Vector2 flightWorldInput;
    private bool flightBoostInput;
    private float flightVerticalInput;
    private Vector3 standaloneFlightVelocity;
    private float standaloneTakeoffTimer;
    private float standaloneLandingTimer;
    private bool standalonePreviousLocomotionEnabled;
    private bool standalonePreviousHandlerEnabled;
    private CapsuleCollider standaloneCapsule;

    private bool IsFlightModeActive => flightBackend != FlightBackend.None;
    public bool IsUsingStandaloneFlightFallback => flightBackend == FlightBackend.Standalone;
    public int FlightState => (int)flightPresentationState;

    private bool RequestFlightMode(bool active, float verticalInput)
    {
        ResolveReferences();
        if (IsInputSuppressedByUcc)
        {
            return false;
        }

        flightVerticalInput = Mathf.Clamp(verticalInput, -1f, 1f);
        if (!active)
        {
            return BeginFlightLanding();
        }

        if (flightBackend == FlightBackend.UccAbility)
        {
            if (flightAbility != null && flightAbility.IsActive)
            {
                flightAbility.CancelLanding();
                flightAbility.SetInput(flightWorldInput, flightBoostInput, flightVerticalInput);
                SetFlightPresentationState(FlightPresentationState.Takeoff, triggerStart: true);
                return true;
            }

            CompleteFlightMode();
        }
        else if (flightBackend == FlightBackend.Standalone)
        {
            Vector3 up = ResolveFlightUp();
            Vector3 planarVelocity = Vector3.ProjectOnPlane(standaloneFlightVelocity, up);
            float verticalSpeed = Mathf.Max(
                Vector3.Dot(standaloneFlightVelocity, up),
                flightTakeoffVerticalSpeed);
            standaloneFlightVelocity = planarVelocity + up * verticalSpeed;
            standaloneTakeoffTimer = flightTakeoffDuration;
            standaloneLandingTimer = 0f;
            SetFlightPresentationState(FlightPresentationState.Takeoff, triggerStart: true);
            return true;
        }

        flightWorldInput = currentWorldMoveInput;
        flightBoostInput = sprintPressed;
        ClearPendingJump();
        if (TryStartUccFlight())
        {
            return true;
        }

        if (allowStandaloneFlightFallback && StartStandaloneFlight())
        {
            return true;
        }

        return false;
    }

    private bool ApplyFlightInput(Vector2 worldInput, bool boost, float verticalInput)
    {
        flightWorldInput = Vector2.ClampMagnitude(worldInput, 1f);
        flightBoostInput = boost;
        flightVerticalInput = Mathf.Clamp(verticalInput, -1f, 1f);

        if (flightBackend == FlightBackend.UccAbility &&
            flightAbility != null &&
            flightAbility.IsActive)
        {
            flightAbility.SetInput(flightWorldInput, flightBoostInput, flightVerticalInput);
            return true;
        }

        return flightBackend == FlightBackend.Standalone;
    }

    private bool TryStartUccFlight()
    {
        if (!enableUccFlight || !IsDriving || locomotion == null || !EnsureFlightAbility())
        {
            return false;
        }

        ConfigureFlightAbility();
        flightAbility.SetInput(flightWorldInput, flightBoostInput, flightVerticalInput);
        if (!locomotion.TryStartAbility(flightAbility, ignorePriority: true))
        {
            WarnOnce(
                ref warnedFlightStartRejected,
                $"UCC Flight was requested on '{name}' but UltimateCharacterLocomotion rejected LitUccFlightAbility. Switching to standalone flight when enabled. ActiveAbilities={ResolveActiveAbilityLabel()}.");
            return false;
        }

        warnedFlightStartRejected = false;
        flightBackend = FlightBackend.UccAbility;
        SetFlightPresentationState(FlightPresentationState.Takeoff, triggerStart: true);
        return true;
    }

    private bool StartStandaloneFlight()
    {
        if (locomotion == null ||
            locomotionHandler == null ||
            !locomotion.enabled ||
            !locomotionHandler.enabled)
        {
            return false;
        }

        standaloneCapsule = GetComponent<CapsuleCollider>();
        if (standaloneCapsule == null)
        {
            return false;
        }

        Vector3 initialVelocity = locomotion.Velocity;
        standalonePreviousLocomotionEnabled = locomotion.enabled;
        standalonePreviousHandlerEnabled = locomotionHandler.enabled;
        locomotion.StopAllAbilities(false);
        locomotion.enabled = false;
        locomotionHandler.enabled = false;

        Vector3 up = ResolveFlightUp();
        Vector3 inheritedPlanarVelocity = Vector3.ProjectOnPlane(initialVelocity, up);
        float inheritedUpSpeed = Mathf.Max(Vector3.Dot(initialVelocity, up), flightTakeoffVerticalSpeed);
        standaloneFlightVelocity = inheritedPlanarVelocity + up * inheritedUpSpeed;
        standaloneTakeoffTimer = flightTakeoffDuration;
        standaloneLandingTimer = 0f;
        flightBackend = FlightBackend.Standalone;
        SetFlightPresentationState(FlightPresentationState.Takeoff, triggerStart: true);
        return true;
    }

    private bool BeginFlightLanding()
    {
        if (flightBackend == FlightBackend.None)
        {
            return false;
        }

        flightBoostInput = false;
        if (flightBackend == FlightBackend.UccAbility)
        {
            if (flightAbility == null || !flightAbility.IsActive)
            {
                CompleteFlightMode();
                return true;
            }

            flightAbility.RequestLanding();
        }

        standaloneLandingTimer = 0f;
        SetFlightPresentationState(FlightPresentationState.Landing, triggerStart: false);
        return true;
    }

    private void UpdateFlightMode()
    {
        if (flightBackend == FlightBackend.UccAbility)
        {
            UpdateUccFlightMode();
        }

    }

    private void UpdateUccFlightMode()
    {
        if (flightAbility == null || !flightAbility.IsActive)
        {
            CompleteFlightMode();
            return;
        }

        flightAbility.SetInput(flightWorldInput, flightBoostInput, flightVerticalInput);
        switch (flightAbility.Phase)
        {
            case LitUccFlightAbility.FlightPhase.Takeoff:
                SetFlightPresentationState(FlightPresentationState.Takeoff, triggerStart: false);
                break;
            case LitUccFlightAbility.FlightPhase.Landing:
                SetFlightPresentationState(FlightPresentationState.Landing, triggerStart: false);
                if (flightAbility.LandingComplete)
                {
                    StopFlightAbilityIfActive();
                    CompleteFlightMode();
                }
                break;
            default:
                SetFlightPresentationState(FlightPresentationState.Cruise, triggerStart: false);
                break;
        }
    }

    private void FixedUpdate()
    {
        if (flightBackend == FlightBackend.Standalone && !IsInputSuppressedByUcc)
        {
            TickStandaloneFlight(Time.fixedDeltaTime);
        }
    }

    private void TickStandaloneFlight(float deltaTime)
    {
        if (deltaTime <= 0f || locomotion == null)
        {
            return;
        }

        Vector3 up = ResolveFlightUp();
        if (flightPresentationState == FlightPresentationState.Takeoff)
        {
            standaloneTakeoffTimer = Mathf.Max(0f, standaloneTakeoffTimer - deltaTime);
            standaloneFlightVelocity = Vector3.MoveTowards(
                standaloneFlightVelocity,
                Vector3.zero,
                flightTakeoffDamping * deltaTime);
            if (standaloneTakeoffTimer <= 0f)
            {
                SetFlightPresentationState(FlightPresentationState.Cruise, triggerStart: false);
            }
        }
        else if (flightPresentationState == FlightPresentationState.Landing)
        {
            standaloneLandingTimer += deltaTime;
            Vector3 planarVelocity = Vector3.ProjectOnPlane(standaloneFlightVelocity, up);
            planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, flightDeceleration * deltaTime);
            float verticalSpeed = Mathf.MoveTowards(
                Vector3.Dot(standaloneFlightVelocity, up),
                -flightLandingSpeed,
                flightLandingAcceleration * deltaTime);
            standaloneFlightVelocity = planarVelocity + up * verticalSpeed;
        }
        else
        {
            TickStandaloneCruise(deltaTime, up);
        }

        RotateStandaloneFlight(deltaTime, up);
        MoveStandaloneFlight(standaloneFlightVelocity * deltaTime);

        if (flightPresentationState == FlightPresentationState.Landing &&
            standaloneLandingTimer >= MinimumStandaloneLandingDuration &&
            IsStandaloneGrounded(up))
        {
            CompleteFlightMode();
        }
    }

    private void TickStandaloneCruise(float deltaTime, Vector3 up)
    {
        Vector3 planarInput = new Vector3(flightWorldInput.x, 0f, flightWorldInput.y);
        planarInput = Vector3.ProjectOnPlane(planarInput, up);
        float inputMagnitude = Mathf.Clamp01(planarInput.magnitude);
        bool hasPlanarInput = inputMagnitude > movementDeadZone;
        bool boosting = flightBoostInput && hasPlanarInput;

        Vector3 desiredPlanarVelocity = hasPlanarInput
            ? planarInput.normalized * ((boosting ? flightBoostSpeed : flightCruiseSpeed) * inputMagnitude)
            : Vector3.zero;
        Vector3 planarVelocity = Vector3.ProjectOnPlane(standaloneFlightVelocity, up);
        float planarRate = hasPlanarInput
            ? (boosting ? flightBoostAcceleration : flightAcceleration)
            : flightDeceleration;
        planarVelocity = Vector3.MoveTowards(planarVelocity, desiredPlanarVelocity, planarRate * deltaTime);

        float processedVerticalInput = ProcessStandaloneVerticalInput(flightVerticalInput);
        float verticalTarget = processedVerticalInput * flightVerticalSpeed;
        float verticalRate = Mathf.Abs(processedVerticalInput) > 0f
            ? flightVerticalAcceleration
            : flightVerticalDeceleration;
        float verticalVelocity = Mathf.MoveTowards(
            Vector3.Dot(standaloneFlightVelocity, up),
            verticalTarget,
            verticalRate * deltaTime);

        standaloneFlightVelocity = planarVelocity + up * verticalVelocity;
    }

    private void RotateStandaloneFlight(float deltaTime, Vector3 up)
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(standaloneFlightVelocity, up);
        if (planarVelocity.sqrMagnitude <= 0.04f)
        {
            return;
        }

        Quaternion target = Quaternion.LookRotation(planarVelocity.normalized, up);
        float rate = flightBoostInput ? flightBoostTurnRate : flightTurnRate;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rate * deltaTime);
    }

    private void MoveStandaloneFlight(Vector3 displacement)
    {
        if (displacement.sqrMagnitude <= 0f)
        {
            return;
        }

        Vector3 position = transform.position;
        Vector3 direction = displacement.normalized;
        float distance = displacement.magnitude;
        if (TryCapsuleCastStandalone(position, direction, distance, out RaycastHit hit))
        {
            float allowedDistance = Mathf.Max(0f, hit.distance - fallbackFlightCollisionSkin);
            Vector3 firstMove = direction * allowedDistance;
            Vector3 remainder = displacement - firstMove;
            displacement = firstMove + Vector3.ProjectOnPlane(remainder, hit.normal);
        }

        locomotion.SetPositionAndRotation(
            position + displacement,
            transform.rotation,
            snapAnimator: false,
            stopAllAbilities: false);
    }

    private bool IsStandaloneGrounded(Vector3 up)
    {
        float probeDistance = Mathf.Max(
            fallbackFlightGroundProbeDistance,
            fallbackFlightCollisionSkin + 0.02f);
        return TryCapsuleCastStandalone(transform.position, -up, probeDistance, out RaycastHit hit) &&
               Vector3.Dot(hit.normal, up) >= 0.35f;
    }

    private bool TryCapsuleCastStandalone(
        Vector3 position,
        Vector3 direction,
        float distance,
        out RaycastHit closestHit)
    {
        closestHit = default;
        if (standaloneCapsule == null || distance <= 0f)
        {
            return false;
        }

        GetStandaloneCapsuleWorldPoints(position, out Vector3 point1, out Vector3 point2, out float radius);
        int hitCount = Physics.CapsuleCastNonAlloc(
            point1,
            point2,
            radius,
            direction,
            standaloneFlightHits,
            distance + fallbackFlightCollisionSkin,
            locomotion.ColliderLayerMask,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = standaloneFlightHits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private void GetStandaloneCapsuleWorldPoints(
        Vector3 rootPosition,
        out Vector3 point1,
        out Vector3 point2,
        out float radius)
    {
        Vector3 scale = transform.lossyScale;
        float verticalScale = Mathf.Abs(scale.y);
        float radialScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        radius = Mathf.Max(0.01f, standaloneCapsule.radius * radialScale);
        float height = Mathf.Max(radius * 2f, standaloneCapsule.height * verticalScale);
        Vector3 centerOffset = transform.TransformVector(standaloneCapsule.center);
        Vector3 center = rootPosition + centerOffset;
        Vector3 up = ResolveFlightUp();
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        point1 = center + up * halfSegment;
        point2 = center - up * halfSegment;
    }

    private float ProcessStandaloneVerticalInput(float input)
    {
        float magnitude = Mathf.Abs(input);
        if (magnitude <= flightVerticalDeadZone)
        {
            return 0f;
        }

        return Mathf.Sign(input) *
               Mathf.InverseLerp(flightVerticalDeadZone, 1f, Mathf.Clamp01(magnitude));
    }

    private Vector3 ResolveFlightUp()
    {
        if (locomotion != null && locomotion.Up.sqrMagnitude > 0f)
        {
            return locomotion.Up.normalized;
        }

        return transform.up.sqrMagnitude > 0f ? transform.up.normalized : Vector3.up;
    }

    private void CompleteFlightMode()
    {
        if (flightBackend == FlightBackend.Standalone)
        {
            RestoreUccAfterStandaloneFlight();
        }

        flightBackend = FlightBackend.None;
        standaloneFlightVelocity = Vector3.zero;
        standaloneTakeoffTimer = 0f;
        standaloneLandingTimer = 0f;
        flightBoostInput = false;
        flightVerticalInput = 0f;
        SetFlightPresentationState(FlightPresentationState.Grounded, triggerStart: false);
    }

    private void RestoreUccAfterStandaloneFlight()
    {
        if (locomotion == null || locomotionHandler == null)
        {
            return;
        }

        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        locomotion.enabled = standalonePreviousLocomotionEnabled;
        locomotionHandler.enabled = standalonePreviousHandlerEnabled;
        locomotion.SetPositionAndRotation(position, rotation, snapAnimator: false, stopAllAbilities: true);
        locomotion.GravityAccumulation = 0f;
        ApplyWorldMoveInput(Vector2.zero);
    }

    private void ShutdownFlightMode()
    {
        if (flightBackend == FlightBackend.UccAbility)
        {
            StopFlightAbilityIfActive();
        }
        else if (flightBackend == FlightBackend.Standalone)
        {
            RestoreUccAfterStandaloneFlight();
        }

        flightBackend = FlightBackend.None;
        standaloneFlightVelocity = Vector3.zero;
        standaloneTakeoffTimer = 0f;
        standaloneLandingTimer = 0f;
        flightWorldInput = Vector2.zero;
        flightBoostInput = false;
        flightVerticalInput = 0f;
        SetFlightPresentationState(FlightPresentationState.Grounded, triggerStart: false);
    }

    private void SetFlightPresentationState(FlightPresentationState state, bool triggerStart)
    {
        if (flightPresentationState == state && !triggerStart)
        {
            return;
        }

        flightPresentationState = state;
        if (triggerStart)
        {
            SetAnimatorTrigger(flightStartTriggerParam);
        }
    }

    private void UpdateFlightAnimatorParameters()
    {
        if (animator == null)
        {
            return;
        }

        float normalizedSpeed = 0f;
        float normalizedVertical = 0f;
        bool boosting = false;
        if (flightBackend == FlightBackend.UccAbility &&
            flightAbility != null &&
            flightAbility.IsActive)
        {
            normalizedSpeed = flightAbility.NormalizedSpeed;
            normalizedVertical = flightAbility.NormalizedVerticalSpeed;
            boosting = flightAbility.Boosting;
        }
        else if (flightBackend == FlightBackend.Standalone)
        {
            Vector3 up = ResolveFlightUp();
            normalizedSpeed = flightBoostSpeed > 0f
                ? Mathf.Clamp01(Vector3.ProjectOnPlane(standaloneFlightVelocity, up).magnitude / flightBoostSpeed)
                : 0f;
            normalizedVertical = flightVerticalSpeed > 0f
                ? Mathf.Clamp(Vector3.Dot(standaloneFlightVelocity, up) / flightVerticalSpeed, -1f, 1f)
                : 0f;
            boosting = flightBoostInput && flightWorldInput.sqrMagnitude > movementDeadZone * movementDeadZone;
        }

        SetAnimatorInteger(flightStateParam, (int)flightPresentationState);
        SetAnimatorFloat(flightSpeedParam, normalizedSpeed);
        SetAnimatorFloat(flightVerticalParam, normalizedVertical);
        SetAnimatorBool(flightBoostParam, boosting);
    }
}
