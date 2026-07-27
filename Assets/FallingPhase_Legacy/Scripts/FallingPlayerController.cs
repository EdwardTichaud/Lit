using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class FallingPlayerController : MonoBehaviour
{
    private const float MaximumBoostChargeDurationSeconds = 2f;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Falling";

    [Header("Motion")]
    [SerializeField, Min(1f)] private float baseForwardSpeed = 24f;
    [SerializeField, FormerlySerializedAs("acceleratedForwardSpeed"), Min(1f)] private float boostPeakForwardSpeed = 54f;
    [SerializeField, Min(1f)] private float lateralSpeed = 14f;
    [SerializeField, Min(1f)] private float verticalSpeed = 11f;
    [SerializeField, FormerlySerializedAs("horizontalBounds"), Min(0f)]
    private Vector2 movementBounds = new Vector2(13f, 8f);
    [SerializeField, Min(0.05f)] private float accelerationResponseSeconds = 0.08f;
    [SerializeField, Min(0.05f)] private float decelerationResponseSeconds = 0.78f;
    [SerializeField, Min(1f)] private float steeringAcceleration = 32f;
    [SerializeField, Min(1f)] private float steeringDeceleration = 18f;

    [Header("Visual Momentum")]
    [SerializeField] private Transform visualTransform;
    [SerializeField, Range(0f, 50f)] private float maxPitchDegrees = 22f;
    [SerializeField, Range(0f, 50f)] private float maxRollDegrees = 28f;
    [SerializeField, Min(0.05f)] private float visualTiltResponseSeconds = 0.16f;

    [Header("Boost")]
    [SerializeField, Range(0.05f, MaximumBoostChargeDurationSeconds)] private float boostChargeDurationSeconds = MaximumBoostChargeDurationSeconds;
    [SerializeField, Range(0.05f, 1f)] private float fullyChargedSpeedMultiplier = 0.36f;
    [SerializeField, Range(0f, 2f)] private float maxChargeRecoilDistance = 0.55f;
    [SerializeField, Range(0.05f, 1f)] private float minimumReleasedBoostStrength = 0.45f;
    [SerializeField, Min(0.1f)] private float boostDurationSeconds = 0.82f;
    [SerializeField, Min(0f)] private float boostCooldownSeconds = 0.28f;
    [SerializeField] private AnimationCurve boostStrengthCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 12f),
        new Keyframe(0.1f, 1f, 0f, 0f),
        new Keyframe(0.55f, 0.82f, -0.4f, -0.8f),
        new Keyframe(1f, 0f, -1.2f, 0f));

    [Header("Grapple Impulse")]
    [SerializeField, Min(1f)] private float grapplePeakForwardSpeed = 82f;
    [SerializeField, Min(0.1f)] private float grappleDurationSeconds = 1.05f;
    [SerializeField, Min(0f)] private float grappleCooldownSeconds = 0.5f;
    [SerializeField] private AnimationCurve grappleStrengthCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 14f),
        new Keyframe(0.08f, 1f, 0f, 0f),
        new Keyframe(0.68f, 0.86f, -0.35f, -1.1f),
        new Keyframe(1f, 0f, -1.4f, 0f));

    [Header("Impact")]
    [SerializeField, Min(1f)] private float hardImpactSpeed = 34f;
    [SerializeField, Range(0.1f, 0.95f)] private float strongestSlowMultiplier = 0.28f;
    [SerializeField, Min(0.1f)] private float impactSlowDuration = 1.4f;
    [SerializeField, Min(0f)] private float impactCooldownSeconds = 0.25f;
    [SerializeField, Min(0f)] private float impactHitboxDisableDelaySeconds = 0.5f;
    [SerializeField, Min(0.05f)] private float impactHitboxDisableSeconds = 0.5f;
    [SerializeField] private Collider playerHitbox;
    [SerializeField] private Animator animator;

    public float CurrentForwardSpeed => currentForwardSpeed;
    public float Speed01 => Mathf.InverseLerp(baseForwardSpeed, boostPeakForwardSpeed, currentForwardSpeed);
    public bool IsBoostCharging => isBoostCharging;
    public float BoostChargeElapsedSeconds => isBoostCharging
        ? Mathf.Min(Time.time - boostChargeStartedAt, ChargeDurationSeconds)
        : 0f;
    public float BoostCharge01 => isBoostCharging
        ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - boostChargeStartedAt) / ChargeDurationSeconds))
        : 0f;
    public float ForwardAcceleration01 => forwardAcceleration01;
    public bool IsGrappleImpulsing => GetGrappleStrength() > 0f;
    public float GrappleDurationSeconds => grappleDurationSeconds;

    public string GetGrappleBindingDisplayString()
    {
        return grappleAction != null ? grappleAction.GetBindingDisplayString() : "Grapple";
    }
    public Vector2 SteeringVelocity => steeringVelocity;
    public Vector2 Steering01 => new Vector2(
        lateralSpeed > 0f ? Mathf.Clamp(steeringVelocity.x / lateralSpeed, -1f, 1f) : 0f,
        verticalSpeed > 0f ? Mathf.Clamp(steeringVelocity.y / verticalSpeed, -1f, 1f) : 0f);
    public float DistanceTravelled => Mathf.Max(0f, body.position.z - startPosition.z);
    public int ImpactCount { get; private set; }
    public float ImpactPenalty { get; private set; }
    public event Action<float> Impacted;
    public event Action BoostTriggered;
    public event Action GrappleRequested;
    public event Action GrappleTriggered;

    private Rigidbody body;
    private InputActionMap actionMap;
    private InputAction moveAction;
    private InputAction accelerateAction;
    private InputAction grappleAction;
    private Vector3 startPosition;
    private float currentForwardSpeed;
    private float speedVelocity;
    private float forwardAcceleration01;
    private Vector2 steeringVelocity;
    private Vector2 steeringVelocitySmooth;
    private Quaternion visualBaseLocalRotation;
    private Vector3 visualBaseLocalPosition;
    private bool hasBoostChargeParameter;
    private bool hasGrappleParameter;
    private Vector2 currentVisualTilt;
    private Vector2 visualTiltVelocity;
    private float slowMultiplier = 1f;
    private float slowEndsAt;
    private float nextImpactAt;
    private float hitboxDisableAt;
    private float hitboxReenableAt;
    private bool hitboxDisablePending;
    private bool hitboxTemporarilyDisabled;
    private float boostStartedAt = float.NegativeInfinity;
    private float nextBoostAvailableAt;
    private float boostIntensity = 1f;
    private float boostChargeStartedAt;
    private bool isBoostCharging;
    private float grappleStartedAt = float.NegativeInfinity;
    private float nextGrappleAvailableAt;

    private float ChargeDurationSeconds => Mathf.Clamp(boostChargeDurationSeconds, 0.05f, MaximumBoostChargeDurationSeconds);

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        startPosition = body.position;
        currentForwardSpeed = baseForwardSpeed;

        if (playerHitbox == null)
        {
            playerHitbox = GetComponent<Collider>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (visualTransform == null && animator != null)
        {
            visualTransform = animator.transform;
        }

        if (visualTransform != null)
        {
            visualBaseLocalRotation = visualTransform.localRotation;
            visualBaseLocalPosition = visualTransform.localPosition;
        }

        hasBoostChargeParameter = HasAnimatorParameter("BoostCharge", AnimatorControllerParameterType.Bool);
        hasGrappleParameter = HasAnimatorParameter("FallingGrapple", AnimatorControllerParameterType.Trigger);
    }

    private void OnEnable()
    {
        if (inputActions == null)
        {
            Debug.LogWarning("FallingPlayerController: InputActionAsset is missing.", this);
            return;
        }

        actionMap = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
        moveAction = actionMap != null ? actionMap.FindAction("Move", throwIfNotFound: false) : null;
        accelerateAction = actionMap != null ? actionMap.FindAction("Accelerate", throwIfNotFound: false) : null;
        grappleAction = actionMap != null ? actionMap.FindAction("Grapple", throwIfNotFound: false) : null;
        if (accelerateAction != null)
        {
            accelerateAction.started += OnAccelerateStarted;
            accelerateAction.canceled += OnAccelerateCanceled;
        }

        if (grappleAction != null)
        {
            grappleAction.performed += OnGrapplePerformed;
        }

        EnableActionMapExclusively();
    }

    private void OnDisable()
    {
        if (accelerateAction != null)
        {
            accelerateAction.started -= OnAccelerateStarted;
            accelerateAction.canceled -= OnAccelerateCanceled;
        }

        if (grappleAction != null)
        {
            grappleAction.performed -= OnGrapplePerformed;
        }

        isBoostCharging = false;
        RestorePlayerHitbox();
        actionMap?.Disable();
    }

    private void EnableActionMapExclusively()
    {
        if (actionMap == null)
        {
            return;
        }

        foreach (InputActionMap map in inputActions.actionMaps)
        {
            if (map != actionMap)
            {
                map.Disable();
            }
        }

        actionMap.Enable();
    }

    private void FixedUpdate()
    {
        UpdatePlayerHitboxState();

        if (isBoostCharging && Time.time >= boostChargeStartedAt + ChargeDurationSeconds)
        {
            ReleaseBoostCharge();
        }

        Vector2 move = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        float boostStrength = GetBoostStrength();
        float grappleStrength = GetGrappleStrength();
        float targetSpeed = isBoostCharging
            ? baseForwardSpeed * Mathf.Lerp(1f, fullyChargedSpeedMultiplier, BoostCharge01)
            : grappleStrength > 0f
                ? Mathf.Lerp(baseForwardSpeed, grapplePeakForwardSpeed, grappleStrength)
                : Mathf.Lerp(baseForwardSpeed, boostPeakForwardSpeed, boostStrength);
        if (Time.time < slowEndsAt)
        {
            targetSpeed *= slowMultiplier;
        }
        else
        {
            slowMultiplier = 1f;
        }

        float previousSpeed = currentForwardSpeed;
        float responseSeconds = targetSpeed > currentForwardSpeed
            ? accelerationResponseSeconds
            : decelerationResponseSeconds;
        currentForwardSpeed = Mathf.SmoothDamp(
            currentForwardSpeed,
            targetSpeed,
            ref speedVelocity,
            responseSeconds,
            Mathf.Infinity,
            Time.fixedDeltaTime);

        float maxAcceleration = Mathf.Max(0.01f, boostPeakForwardSpeed - baseForwardSpeed) / accelerationResponseSeconds;
        forwardAcceleration01 = Mathf.Clamp01((currentForwardSpeed - previousSpeed) / Time.fixedDeltaTime / maxAcceleration);

        Vector2 targetSteeringVelocity = new Vector2(move.x * lateralSpeed, move.y * verticalSpeed);
        float steeringRate = move.sqrMagnitude > 0.001f ? steeringAcceleration : steeringDeceleration;
        steeringVelocity = Vector2.SmoothDamp(
            steeringVelocity,
            targetSteeringVelocity,
            ref steeringVelocitySmooth,
            1f / steeringRate,
            Mathf.Infinity,
            Time.fixedDeltaTime);

        Vector3 velocity = new Vector3(
            steeringVelocity.x,
            steeringVelocity.y,
            currentForwardSpeed);
        body.linearVelocity = velocity;

        ClampToMovementBounds();

        if (animator != null)
        {
            if (hasBoostChargeParameter)
            {
                animator.SetBool("BoostCharge", isBoostCharging && Time.time >= slowEndsAt);
            }

            animator.SetBool("FallingBoost", boostStrength > 0.12f && Time.time >= slowEndsAt);
        }
    }

    public bool TriggerBoost()
    {
        return StartBoost(1f);
    }

    public bool BeginBoostCharge()
    {
        if (IsGrappleImpulsing || Time.time < nextBoostAvailableAt || Time.time < slowEndsAt)
        {
            return false;
        }

        isBoostCharging = true;
        boostChargeStartedAt = Time.time;
        return true;
    }

    public bool ReleaseBoostCharge()
    {
        if (!isBoostCharging)
        {
            return false;
        }

        float intensity = Mathf.Lerp(minimumReleasedBoostStrength, 1f, BoostCharge01);
        isBoostCharging = false;
        return StartBoost(intensity);
    }

    public bool TriggerGrappleImpulse()
    {
        if (Time.time < nextGrappleAvailableAt || Time.time < slowEndsAt)
        {
            return false;
        }

        isBoostCharging = false;
        boostStartedAt = float.NegativeInfinity;
        grappleStartedAt = Time.time;
        nextGrappleAvailableAt = Time.time + grappleDurationSeconds + grappleCooldownSeconds;
        if (animator != null && hasGrappleParameter)
        {
            animator.SetTrigger("FallingGrapple");
        }

        GrappleTriggered?.Invoke();
        return true;
    }

    private void LateUpdate()
    {
        if (visualTransform == null)
        {
            return;
        }

        float targetPitch = -steeringVelocity.y / verticalSpeed * maxPitchDegrees - forwardAcceleration01 * maxPitchDegrees * 0.35f;
        float targetRoll = -steeringVelocity.x / lateralSpeed * maxRollDegrees;
        currentVisualTilt = Vector2.SmoothDamp(
            currentVisualTilt,
            new Vector2(targetPitch, targetRoll),
            ref visualTiltVelocity,
            visualTiltResponseSeconds);
        visualTransform.localRotation = visualBaseLocalRotation * Quaternion.Euler(currentVisualTilt.x, 0f, currentVisualTilt.y);
        visualTransform.localPosition = visualBaseLocalPosition + Vector3.back * (BoostCharge01 * maxChargeRecoilDistance);
    }

    private void ClampToMovementBounds()
    {
        Vector3 localPosition = body.position - startPosition;
        float clampedX = Mathf.Clamp(localPosition.x, -movementBounds.x, movementBounds.x);
        float clampedY = Mathf.Clamp(localPosition.y, -movementBounds.y, movementBounds.y);
        if (Mathf.Approximately(localPosition.x, clampedX) && Mathf.Approximately(localPosition.y, clampedY))
        {
            return;
        }

        if (!Mathf.Approximately(localPosition.x, clampedX))
        {
            steeringVelocity.x = 0f;
        }

        if (!Mathf.Approximately(localPosition.y, clampedY))
        {
            steeringVelocity.y = 0f;
        }

        localPosition.x = clampedX;
        localPosition.y = clampedY;
        body.position = startPosition + localPosition;
    }

    private void OnCollisionEnter(Collision collision)
    {
        FallingObstacle obstacle = collision.collider.GetComponentInParent<FallingObstacle>();
        if (Time.time < nextImpactAt || obstacle == null)
        {
            return;
        }

        float impactSpeed = collision.relativeVelocity.magnitude;
        float normalizedImpact = Mathf.Clamp01(impactSpeed * obstacle.ImpactMultiplier / hardImpactSpeed);
        ApplyImpact(normalizedImpact);
        SchedulePlayerHitboxDisable();
    }

    public void ApplyImpact(float normalizedImpact)
    {
        float strength = Mathf.Clamp01(normalizedImpact);
        slowMultiplier = Mathf.Min(slowMultiplier, Mathf.Lerp(1f, strongestSlowMultiplier, strength));
        slowEndsAt = Mathf.Max(slowEndsAt, Time.time + Mathf.Lerp(0.35f, impactSlowDuration, strength));
        nextImpactAt = Time.time + impactCooldownSeconds;
        ImpactCount++;
        ImpactPenalty += Mathf.Lerp(12f, 95f, strength);

        if (animator != null)
        {
            animator.SetTrigger("FallingImpact");
        }

        Impacted?.Invoke(strength);
    }

    private bool StartBoost(float intensity)
    {
        if (Time.time < nextBoostAvailableAt || Time.time < slowEndsAt)
        {
            return false;
        }

        boostIntensity = Mathf.Clamp01(intensity);
        boostStartedAt = Time.time;
        nextBoostAvailableAt = Time.time + boostDurationSeconds + boostCooldownSeconds;
        BoostTriggered?.Invoke();
        return true;
    }

    private void SchedulePlayerHitboxDisable()
    {
        if (playerHitbox == null || hitboxDisablePending || hitboxTemporarilyDisabled)
        {
            return;
        }

        hitboxDisablePending = true;
        hitboxDisableAt = Time.time + impactHitboxDisableDelaySeconds;
    }

    private void DisablePlayerHitbox()
    {
        if (playerHitbox == null)
        {
            return;
        }

        playerHitbox.enabled = false;
        hitboxTemporarilyDisabled = true;
        hitboxReenableAt = Time.time + impactHitboxDisableSeconds;
    }

    private void UpdatePlayerHitboxState()
    {
        if (hitboxDisablePending && Time.time >= hitboxDisableAt)
        {
            hitboxDisablePending = false;
            DisablePlayerHitbox();
        }

        if (hitboxTemporarilyDisabled && Time.time >= hitboxReenableAt)
        {
            RestorePlayerHitbox();
        }
    }

    private void RestorePlayerHitbox()
    {
        if (playerHitbox != null)
        {
            playerHitbox.enabled = true;
        }

        hitboxDisablePending = false;
        hitboxTemporarilyDisabled = false;
    }

    private void OnAccelerateStarted(InputAction.CallbackContext _)
    {
        BeginBoostCharge();
    }

    private void OnAccelerateCanceled(InputAction.CallbackContext _)
    {
        ReleaseBoostCharge();
    }

    private void OnGrapplePerformed(InputAction.CallbackContext _)
    {
        GrappleRequested?.Invoke();
    }

    private float GetBoostStrength()
    {
        float elapsed = Time.time - boostStartedAt;
        if (elapsed < 0f || elapsed >= boostDurationSeconds)
        {
            return 0f;
        }

        return Mathf.Clamp01(boostStrengthCurve.Evaluate(elapsed / boostDurationSeconds)) * boostIntensity;
    }

    private float GetGrappleStrength()
    {
        float elapsed = Time.time - grappleStartedAt;
        if (elapsed < 0f || elapsed >= grappleDurationSeconds)
        {
            return 0f;
        }

        return Mathf.Clamp01(grappleStrengthCurve.Evaluate(elapsed / grappleDurationSeconds));
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName && parameters[i].type == parameterType)
            {
                return true;
            }
        }

        return false;
    }
}
