using UnityEngine;

[DisallowMultipleComponent]
public sealed class FallingCameraRig : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 2.4f, -6.8f);
    [SerializeField, Min(0.1f)] private float followSharpness = 10f;
    [SerializeField, Min(0.1f)] private float fieldOfViewSharpness = 12f;
    [SerializeField, Range(40f, 100f)] private float baseFieldOfView = 68f;
    [SerializeField, Range(0f, 20f)] private float accelerationFieldOfView = 10f;
    [SerializeField, Range(0f, 4f)] private float boostPullBackDistance = 1.2f;
    [SerializeField, Range(0f, 20f)] private float chargeFieldOfViewReduction = 4f;

    [Header("Charge And Boost Contrast")]
    [SerializeField, Range(1f, 3f)] private float boostPullBackMultiplier = 1.7f;
    [SerializeField, Range(1f, 3f)] private float boostFieldOfViewMultiplier = 1.6f;
    [SerializeField, Range(1f, 3f)] private float chargeFieldOfViewMultiplier = 2.1f;

    [Header("Boost Charge Shoulder")]
    [SerializeField] private Vector3 leftShoulderOffset = new Vector3(-0.55f, 1.55f, -1.15f);
    [Tooltip("Local camera movement in units per second while BoostCharge is held.")]
    [SerializeField] private Vector3 boostChargeCameraMoveSpeed = Vector3.zero;
    [SerializeField, Range(0f, 1f)] private float chargeApproachLerp = 0.1f;

    [Header("Opposing Movement")]
    [SerializeField] private Vector2 oppositeMovementAmplitude = new Vector2(0.55f, 0.35f);

    [Header("Camera Motion")]
    [SerializeField, Range(0f, 20f)] private float maxRollDegrees = 7f;
    [SerializeField, Range(0f, 12f)] private float accelerationPitchDegrees = 3f;
    [SerializeField, Range(0f, 0.25f)] private float speedBreathingAmplitude = 0.035f;
    [SerializeField, Min(0.1f)] private float speedBreathingFrequency = 8f;
    [SerializeField, Range(0f, 1f)] private float impactShakeDistance = 0.24f;
    [SerializeField, Min(0.05f)] private float impactShakeDuration = 0.22f;
    [Header("Grapple Presentation")]
    [SerializeField] private Vector3 grappleCameraOffset = new Vector3(0f, 0.25f, -1.8f);
    [SerializeField, Range(0f, 20f)] private float grappleFieldOfViewIncrease = 8f;
    [SerializeField, Min(0.05f)] private float grapplePresentationDuration = 0.55f;
    [SerializeField] private FallingPlayerController player;

    private Camera controlledCamera;
    private float impactShakeStrength;
    private float impactShakeEndsAt;
    private float chargePresentationBlend;
    private float chargePresentationBlendVelocity;
    private float fieldOfViewVelocity;
    private float grapplePresentationEndsAt;

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        if (player == null && target != null)
        {
            player = target.GetComponent<FallingPlayerController>();
        }
    }

    private void Start()
    {
        if (player != null)
        {
            player.Impacted += PlayImpactShake;
            player.GrappleTriggered += PlayGrapplePresentation;
        }
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.Impacted -= PlayImpactShake;
            player.GrappleTriggered -= PlayGrapplePresentation;
        }
    }

    private void LateUpdate()
    {
        if (target == null || controlledCamera == null)
        {
            return;
        }

        float speed01 = player != null ? player.Speed01 : 0f;
        bool isBoostCharging = player != null && player.IsBoostCharging;
        float boostResponse = Mathf.SmoothStep(0f, 1f, speed01);
        float chargeElapsedSeconds = player != null ? player.BoostChargeElapsedSeconds : 0f;
        float grappleResponse = GetGrapplePresentationResponse();
        float chargeTransitionSeconds = Mathf.Lerp(0.01f, 1.25f, chargeApproachLerp);
        chargePresentationBlend = Mathf.SmoothDamp(
            chargePresentationBlend,
            isBoostCharging ? 1f : 0f,
            ref chargePresentationBlendVelocity,
            chargeTransitionSeconds);
        Vector2 steeringVelocity = player != null ? player.SteeringVelocity : Vector2.zero;
        Vector2 steering01 = player != null ? player.Steering01 : Vector2.zero;
        Vector3 speedOffset = new Vector3(0f, 0f, -boostPullBackDistance * boostPullBackMultiplier * boostResponse);
        Vector3 oppositeMovementOffset = new Vector3(
            -steering01.x * oppositeMovementAmplitude.x,
            -steering01.y * oppositeMovementAmplitude.y,
            0f);
        float breathingWeight = (1f - boostResponse) * (1f - chargePresentationBlend);
        float breathing = Mathf.Sin(Time.unscaledTime * speedBreathingFrequency) * speedBreathingAmplitude * speed01 * breathingWeight;
        Vector3 followOffset = localOffset + speedOffset + oppositeMovementOffset + Vector3.up * breathing + grappleCameraOffset * grappleResponse;
        Vector3 chargeOffset = leftShoulderOffset + boostChargeCameraMoveSpeed * chargeElapsedSeconds;
        Vector3 targetPosition = target.TransformPoint(Vector3.Lerp(followOffset, chargeOffset, chargePresentationBlend));
        targetPosition += GetImpactShakeOffset();
        float normalInterpolation = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, normalInterpolation);
        Quaternion lookRotation = Quaternion.LookRotation(target.position + Vector3.up * 0.9f - transform.position, Vector3.up);
        float roll = -steeringVelocity.x / 14f * maxRollDegrees;
        float pitch = player != null ? -player.ForwardAcceleration01 * accelerationPitchDegrees : 0f;
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation * Quaternion.Euler(pitch, 0f, roll), normalInterpolation);

        if (player != null)
        {
            float boostFieldOfView = accelerationFieldOfView * boostFieldOfViewMultiplier * boostResponse;
            float chargeFieldOfView = chargeFieldOfViewReduction * chargeFieldOfViewMultiplier * chargePresentationBlend;
            float targetFieldOfView = Mathf.Clamp(
                baseFieldOfView + boostFieldOfView - chargeFieldOfView + grappleFieldOfViewIncrease * grappleResponse,
                40f,
                100f);
            controlledCamera.fieldOfView = Mathf.SmoothDamp(
                controlledCamera.fieldOfView,
                targetFieldOfView,
                ref fieldOfViewVelocity,
                1f / fieldOfViewSharpness);
        }
    }

    private void PlayImpactShake(float strength)
    {
        impactShakeStrength = Mathf.Clamp01(strength);
        impactShakeEndsAt = Time.unscaledTime + impactShakeDuration;
    }

    private void PlayGrapplePresentation()
    {
        grapplePresentationEndsAt = Time.unscaledTime + grapplePresentationDuration;
    }

    private float GetGrapplePresentationResponse()
    {
        if (Time.unscaledTime >= grapplePresentationEndsAt)
        {
            return 0f;
        }

        float elapsed01 = 1f - Mathf.Clamp01((grapplePresentationEndsAt - Time.unscaledTime) / grapplePresentationDuration);
        return Mathf.Sin(elapsed01 * Mathf.PI);
    }

    private Vector3 GetImpactShakeOffset()
    {
        if (Time.unscaledTime >= impactShakeEndsAt || impactShakeStrength <= 0f)
        {
            return Vector3.zero;
        }

        float remaining01 = Mathf.Clamp01((impactShakeEndsAt - Time.unscaledTime) / impactShakeDuration);
        float amplitude = impactShakeDistance * impactShakeStrength * remaining01;
        return new Vector3(
            Mathf.PerlinNoise(Time.unscaledTime * 37f, 0f) - 0.5f,
            Mathf.PerlinNoise(0f, Time.unscaledTime * 41f) - 0.5f,
            0f) * amplitude;
    }
}
