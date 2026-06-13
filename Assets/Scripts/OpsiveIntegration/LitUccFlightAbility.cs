using System;
using Opsive.Shared.Game;
using Opsive.Shared.Utility;
using Opsive.UltimateCharacterController.Character.Abilities;
using UnityEngine;

[Serializable]
public sealed class LitUccFlightAbility : Ability
{
    private Vector2 worldPlanarInput;
    private bool boostInput;
    private float verticalInput;
    private Vector3 currentVelocity;
    private bool boostActive;
    private float takeoffTimer;

    private float takeoffVerticalSpeed = 6.5f;
    private float takeoffDuration = 0.45f;
    private float takeoffDamping = 16f;
    private float cruiseSpeed = 33f;
    private float boostSpeed = 81f;
    private float acceleration = 54f;
    private float boostAcceleration = 126f;
    private float deceleration = 36f;
    private float verticalSpeed = 24f;
    private float verticalAcceleration = 66f;
    private float verticalDeceleration = 54f;
    private float verticalDeadZone = 0.05f;
    private float idleSpeedThreshold = 0.08f;
    private float turnRate = 760f;
    private float boostTurnRate = 460f;

    public LitUccFlightAbility()
    {
        m_StartType = AbilityStartType.Manual;
        m_StopType = AbilityStopType.Manual;
        m_AllowPositionalInput = false;
        m_AllowRotationalInput = false;
        m_UseGravity = AbilityBoolOverride.False;
        m_UseRootMotionPosition = AbilityBoolOverride.False;
        m_UseRootMotionRotation = AbilityBoolOverride.False;
        m_AbilityIndexParameter = -1;
    }

    public Vector3 FlightVelocity => currentVelocity;
    public bool Boosting => IsActive && boostActive;
    public override float AbilityFloatData => boostSpeed > 0f ? Mathf.Clamp01(currentVelocity.magnitude / boostSpeed) : 0f;

    public void Configure(
        float newTakeoffVerticalSpeed,
        float newTakeoffDuration,
        float newTakeoffDamping,
        float newCruiseSpeed,
        float newBoostSpeed,
        float newAcceleration,
        float newBoostAcceleration,
        float newDeceleration,
        float newVerticalSpeed,
        float newVerticalAcceleration,
        float newVerticalDeceleration,
        float newVerticalDeadZone,
        float newIdleSpeedThreshold,
        float newTurnRate,
        float newBoostTurnRate)
    {
        takeoffVerticalSpeed = Mathf.Max(0f, newTakeoffVerticalSpeed);
        takeoffDuration = Mathf.Max(0f, newTakeoffDuration);
        takeoffDamping = Mathf.Max(0f, newTakeoffDamping);
        cruiseSpeed = Mathf.Max(0f, newCruiseSpeed);
        boostSpeed = Mathf.Max(cruiseSpeed, newBoostSpeed);
        acceleration = Mathf.Max(0f, newAcceleration);
        boostAcceleration = Mathf.Max(acceleration, newBoostAcceleration);
        deceleration = Mathf.Max(0f, newDeceleration);
        verticalSpeed = Mathf.Max(0f, newVerticalSpeed);
        verticalAcceleration = Mathf.Max(0f, newVerticalAcceleration);
        verticalDeceleration = Mathf.Max(0f, newVerticalDeceleration);
        verticalDeadZone = Mathf.Clamp(newVerticalDeadZone, 0f, 0.4f);
        idleSpeedThreshold = Mathf.Max(0f, newIdleSpeedThreshold);
        turnRate = Mathf.Max(0f, newTurnRate);
        boostTurnRate = Mathf.Max(0f, newBoostTurnRate);
    }

    public void SetInput(Vector2 worldInput, bool boost, float vertical)
    {
        worldPlanarInput = Vector2.ClampMagnitude(worldInput, 1f);
        boostInput = boost;
        verticalInput = Mathf.Clamp(vertical, -1f, 1f);
    }

    public override bool ShouldStopActiveAbility(Ability activeAbility)
    {
        return activeAbility is Fall ||
               activeAbility is Jump ||
               activeAbility is HeightChange ||
               activeAbility is SpeedChange ||
               base.ShouldStopActiveAbility(activeAbility);
    }

    public override bool ShouldBlockAbilityStart(Ability startingAbility)
    {
        return startingAbility is Fall ||
               startingAbility is Jump ||
               startingAbility is HeightChange ||
               base.ShouldBlockAbilityStart(startingAbility);
    }

    protected override void AbilityStarted()
    {
        currentVelocity = m_CharacterLocomotion != null
            ? m_CharacterLocomotion.Up * Mathf.Max(Vector3.Dot(m_CharacterLocomotion.Velocity, m_CharacterLocomotion.Up), takeoffVerticalSpeed)
            : Vector3.up * takeoffVerticalSpeed;
        takeoffTimer = takeoffDuration;
        boostActive = false;

        if (m_CharacterLocomotion != null)
        {
            m_CharacterLocomotion.GravityAccumulation = 0f;
        }

        base.AbilityStarted();
        UpdateAbilityAnimatorParameters(true);
    }

    public override void Update()
    {
        base.Update();
        SetAbilityFloatDataParameter(AbilityFloatData, TimeUtility.DeltaTime);
    }

    public override void UpdateRotation()
    {
        if (m_CharacterLocomotion == null || m_Transform == null)
        {
            return;
        }

        Vector3 up = m_CharacterLocomotion.Up.sqrMagnitude > 0f ? m_CharacterLocomotion.Up.normalized : Vector3.up;
        Vector3 planarVelocity = Vector3.ProjectOnPlane(currentVelocity, up);
        Vector3 inputDirection = new Vector3(worldPlanarInput.x, 0f, worldPlanarInput.y);
        Vector3 lookDirection = planarVelocity.sqrMagnitude > 0.04f ? planarVelocity : inputDirection;
        lookDirection = Vector3.ProjectOnPlane(lookDirection, up);
        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, up);
        Quaternion deltaRotation = Quaternion.Inverse(m_Transform.rotation) * targetRotation;
        float rate = boostActive ? boostTurnRate : turnRate;
        m_CharacterLocomotion.DesiredRotation = Quaternion.RotateTowards(
            Quaternion.identity,
            deltaRotation,
            rate * TimeUtility.DeltaTime);
    }

    public override void UpdateDesiredMovement()
    {
        base.UpdateDesiredMovement();

        if (m_CharacterLocomotion == null || m_Transform == null)
        {
            return;
        }

        TickFlight(TimeUtility.DeltaTime);
        m_CharacterLocomotion.GravityAccumulation = 0f;
        m_CharacterLocomotion.LocalDesiredMovement = m_Transform.InverseTransformDirection(currentVelocity * TimeUtility.DeltaTime);
    }

    protected override void AbilityStopped(bool force)
    {
        currentVelocity = Vector3.zero;
        worldPlanarInput = Vector2.zero;
        boostInput = false;
        verticalInput = 0f;
        boostActive = false;
        takeoffTimer = 0f;

        if (m_CharacterLocomotion != null)
        {
            m_CharacterLocomotion.GravityAccumulation = 0f;
        }

        base.AbilityStopped(force);
    }

    private void TickFlight(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        Vector3 up = m_CharacterLocomotion.Up.sqrMagnitude > 0f ? m_CharacterLocomotion.Up.normalized : Vector3.up;
        if (takeoffTimer > 0f)
        {
            takeoffTimer = Mathf.Max(0f, takeoffTimer - deltaTime);
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, takeoffDamping * deltaTime);
            return;
        }

        Vector3 planarInput = new Vector3(worldPlanarInput.x, 0f, worldPlanarInput.y);
        planarInput = Vector3.ProjectOnPlane(planarInput, up);
        float inputMagnitude = Mathf.Clamp01(planarInput.magnitude);
        bool hasPlanarInput = inputMagnitude > 0.0001f;
        Vector3 desiredDirection = hasPlanarInput ? planarInput.normalized : Vector3.zero;

        boostActive = boostInput && hasPlanarInput;
        float targetSpeed = boostActive ? boostSpeed : cruiseSpeed;
        Vector3 desiredPlanarVelocity = hasPlanarInput ? desiredDirection * (targetSpeed * inputMagnitude) : Vector3.zero;
        Vector3 currentPlanarVelocity = Vector3.ProjectOnPlane(currentVelocity, up);
        float planarRate = hasPlanarInput ? (boostActive ? boostAcceleration : acceleration) : deceleration;
        currentPlanarVelocity = Vector3.MoveTowards(currentPlanarVelocity, desiredPlanarVelocity, planarRate * deltaTime);

        float processedVerticalInput = ProcessVerticalInput(verticalInput);
        float currentVerticalSpeed = Vector3.Dot(currentVelocity, up);
        float targetVerticalSpeed = processedVerticalInput * verticalSpeed;
        float verticalRate = Mathf.Abs(processedVerticalInput) > 0f ? verticalAcceleration : verticalDeceleration;
        currentVerticalSpeed = Mathf.MoveTowards(currentVerticalSpeed, targetVerticalSpeed, verticalRate * deltaTime);

        currentVelocity = currentPlanarVelocity + up * currentVerticalSpeed;
        if (!hasPlanarInput &&
            Mathf.Abs(processedVerticalInput) <= 0.0001f &&
            currentVelocity.sqrMagnitude <= idleSpeedThreshold * idleSpeedThreshold)
        {
            currentVelocity = Vector3.zero;
        }
    }

    private float ProcessVerticalInput(float input)
    {
        float magnitude = Mathf.Abs(input);
        if (magnitude <= verticalDeadZone)
        {
            return 0f;
        }

        float normalizedMagnitude = Mathf.InverseLerp(verticalDeadZone, 1f, Mathf.Clamp01(magnitude));
        return Mathf.Sign(input) * normalizedMagnitude;
    }
}
