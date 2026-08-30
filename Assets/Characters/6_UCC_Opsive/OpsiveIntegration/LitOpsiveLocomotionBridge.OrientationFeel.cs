using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    [Header("Orientation Feel")]
    [SerializeField, Tooltip("Smooths the fallback look source so the body turns with weight instead of snapping to movement input.")]
    private bool enableCinematicOrientationFeel = true;
    [SerializeField, Range(0f, 1f)] private float orientationInputDeadZone = 0.14f;
    [SerializeField, Min(1f)] private float orientationWalkTurnRate = 360f;
    [SerializeField, Min(1f)] private float orientationSprintTurnRate = 300f;
    [SerializeField, Min(1f), Tooltip("Maximum exploration body turn speed. It intentionally matches sprint turning so a 180-degree reversal follows a visible curve instead of becoming a spin in place.")]
    private float orientationSharpTurnRate = 300f;
    [SerializeField, Range(0f, 180f)] private float orientationSharpTurnAngle = 92f;
    [SerializeField, Range(0f, 1f), Tooltip("Blends a little current planar velocity into the facing target for smoother diagonals and recoveries.")]
    private float orientationVelocityBlend = 0.1f;

    private Vector3 smoothedPlanarLookDirection;
    private bool hasSmoothedPlanarLookDirection;

    private void ValidateOrientationFeelSettings()
    {
        orientationInputDeadZone = Mathf.Clamp01(orientationInputDeadZone);
        orientationWalkTurnRate = Mathf.Max(1f, orientationWalkTurnRate);
        orientationSprintTurnRate = Mathf.Max(1f, orientationSprintTurnRate);
        orientationSharpTurnRate = Mathf.Max(1f, orientationSharpTurnRate);
        orientationSharpTurnAngle = Mathf.Clamp(orientationSharpTurnAngle, 0f, 180f);
        orientationVelocityBlend = Mathf.Clamp01(orientationVelocityBlend);
    }

    private void ResetOrientationFeelState()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            smoothedPlanarLookDirection = Vector3.forward;
            hasSmoothedPlanarLookDirection = false;
            return;
        }

        smoothedPlanarLookDirection = forward.normalized;
        hasSmoothedPlanarLookDirection = true;
    }

    private void ForceOrientationLookDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        smoothedPlanarLookDirection = direction.normalized;
        hasSmoothedPlanarLookDirection = true;
        if (orientLookSourceFromMovement && lookSource != null)
        {
            lookSource.SetPlanarLookDirection(smoothedPlanarLookDirection);
        }
    }

    private Vector3 ResolveOrientationLookDirection(Vector3 targetDirection, float inputMagnitude)
    {
        targetDirection.y = 0f;
        if (targetDirection.sqrMagnitude <= 0.0001f)
        {
            return ResolveFallbackOrientationDirection();
        }

        targetDirection.Normalize();
        if (!enableCinematicOrientationFeel ||
            IsFlightModeActive ||
            IsScriptedTraversalActive)
        {
            smoothedPlanarLookDirection = targetDirection;
            hasSmoothedPlanarLookDirection = true;
            return targetDirection;
        }

        if (inputMagnitude < orientationInputDeadZone)
        {
            return ResolveFallbackOrientationDirection();
        }

        if (!hasSmoothedPlanarLookDirection || smoothedPlanarLookDirection.sqrMagnitude <= 0.0001f)
        {
            ResetOrientationFeelState();
        }

        Vector3 assistedTarget = ResolveVelocityAssistedOrientationTarget(targetDirection);
        float deltaTime = ResolveOrientationDeltaTime();
        float turnRate = ResolveOrientationTurnRate(assistedTarget, inputMagnitude);
        Quaternion currentRotation = Quaternion.LookRotation(smoothedPlanarLookDirection, Vector3.up);
        Quaternion targetRotation = Quaternion.LookRotation(assistedTarget, Vector3.up);
        smoothedPlanarLookDirection = (Quaternion.RotateTowards(
            currentRotation,
            targetRotation,
            turnRate * deltaTime) * Vector3.forward).normalized;
        hasSmoothedPlanarLookDirection = true;
        return smoothedPlanarLookDirection;
    }

    private Vector3 ResolveVelocityAssistedOrientationTarget(Vector3 targetDirection)
    {
        float velocityBlend = Mathf.Clamp01(orientationVelocityBlend);
        if (velocityBlend <= 0f || locomotion == null)
        {
            return targetDirection;
        }

        Vector3 planarVelocity = locomotion.Velocity;
        planarVelocity.y = 0f;
        if (planarVelocity.sqrMagnitude <= 0.04f)
        {
            return targetDirection;
        }

        float speedBlend = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.01f, runPresentationSpeed));
        Vector3 assisted = Vector3.Slerp(targetDirection, planarVelocity.normalized, velocityBlend * speedBlend);
        assisted.y = 0f;
        return assisted.sqrMagnitude > 0.0001f ? assisted.normalized : targetDirection;
    }

    private float ResolveOrientationTurnRate(Vector3 targetDirection, float inputMagnitude)
    {
        float baseRate = sprintPressed ? orientationSprintTurnRate : orientationWalkTurnRate;
        float angle = Vector3.Angle(smoothedPlanarLookDirection, targetDirection);
        float sharpBlend = Mathf.InverseLerp(orientationSharpTurnAngle, 180f, angle);
        float rate = Mathf.Lerp(baseRate, Mathf.Max(baseRate, orientationSharpTurnRate), sharpBlend);
        return Mathf.Lerp(rate * 0.82f, rate, Mathf.Clamp01(inputMagnitude));
    }

    private Vector3 ResolveFallbackOrientationDirection()
    {
        if (hasSmoothedPlanarLookDirection && smoothedPlanarLookDirection.sqrMagnitude > 0.0001f)
        {
            return smoothedPlanarLookDirection;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    private float ResolveOrientationDeltaTime()
    {
        float deltaTime = Time.inFixedTimeStep ? Time.fixedDeltaTime : Time.deltaTime;
        return Mathf.Max(deltaTime, 0.0001f);
    }
}
