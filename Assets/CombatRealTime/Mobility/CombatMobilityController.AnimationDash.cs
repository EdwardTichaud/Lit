using UnityEngine;

public sealed partial class CombatMobilityController
{
    private readonly PlayerModuleConfiguration<PlayerDashSettings> dashConfiguration = new PlayerModuleConfiguration<PlayerDashSettings>();
    private PlayerDashSettings defaultDashSettings = new PlayerDashSettings();
    private PlayerDashSettings DashSettings => RealTimeCombatManager.Instance != null && RealTimeCombatManager.Instance.PlayerRoot != null
        ? dashConfiguration.Resolve(RealTimeCombatManager.Instance.PlayerRoot, data => data.dash) : defaultDashSettings;
    private float dashOvershootDistance => DashSettings.dashOvershootDistance;
    private float dashImpulsePerMeter => DashSettings.dashImpulsePerMeter;
    private float minimumDashImpulse => DashSettings.minimumDashImpulse;
    private float maximumDashImpulse => DashSettings.maximumDashImpulse;
    private float dashInputLockSeconds => DashSettings.dashInputLockSeconds;
    private float stopDashDuration => DashSettings.stopDashDuration;
    private float stopDashDeceleration => DashSettings.stopDashDeceleration;

    private Vector3 lastDashDirection;
    private Transform dashCaster;
    private Coroutine stopDashRoutine;
    public void HandleDash()
    {
        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        Transform caster = manager != null ? manager.PlayerRoot : null;
        EnemyController target = manager != null ? manager.LockedEnemy : null;
        if (caster == null || target == null)
        {
            return;
        }
        dashCaster = caster;

        Vector3 direction = target.LockPoint.position - caster.position;
        direction.y = 0f;
        float distanceToTarget = direction.magnitude;
        if (distanceToTarget <= 0.001f)
        {
            return;
        }

        lastDashDirection = direction / distanceToTarget;
        float intendedTravelDistance = distanceToTarget + dashOvershootDistance;
        float impulse = Mathf.Clamp(
            intendedTravelDistance * dashImpulsePerMeter,
            minimumDashImpulse,
            maximumDashImpulse);

        LitOpsiveLocomotionBridge bridge = caster.GetComponentInChildren<LitOpsiveLocomotionBridge>(true);
        if (bridge == null || !bridge.AddExternalImpulse(lastDashDirection * impulse, ForceMode.VelocityChange,
                dashInputLockSeconds, caster.GetComponentInChildren<PlayerActionPresentationController>(true)))
        {
            lastDashDirection = Vector3.zero;
        }

        if (stopDashRoutine != null)
        {
            StopCoroutine(stopDashRoutine);
            stopDashRoutine = null;
        }
    }

    public void HandleStopDash()
    {
        if (lastDashDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (stopDashRoutine != null)
        {
            StopCoroutine(stopDashRoutine);
        }

        stopDashRoutine = StartCoroutine(StopDashRoutine(lastDashDirection));
    }

    private System.Collections.IEnumerator StopDashRoutine(Vector3 dashDirection)
    {
        Transform caster = dashCaster;
        LitOpsiveLocomotionBridge bridge = caster != null
            ? caster.GetComponentInChildren<LitOpsiveLocomotionBridge>(true)
            : null;
        if (bridge == null)
        {
            lastDashDirection = Vector3.zero;
            stopDashRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < stopDashDuration)
        {
            Vector3 velocity = bridge.PlanarVelocity;
            float forwardSpeed = Vector3.Dot(velocity, dashDirection);
            if (forwardSpeed <= 0.01f)
            {
                break;
            }

            float brakingStep = Mathf.Min(forwardSpeed, stopDashDeceleration * Time.fixedDeltaTime);
            bridge.AddExternalImpulse(-dashDirection * brakingStep, ForceMode.VelocityChange, 0f);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        lastDashDirection = Vector3.zero;
        stopDashRoutine = null;
    }
    public void CancelAnimationDash()
    {
        if (stopDashRoutine != null) StopCoroutine(stopDashRoutine);
        stopDashRoutine = null;
        lastDashDirection = Vector3.zero;
        dashCaster = null;
    }
    public void CancelAnimationDash(Transform caster)
    {
        if (caster == dashCaster) CancelAnimationDash();
    }
}
