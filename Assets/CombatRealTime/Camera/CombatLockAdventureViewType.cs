using Opsive.UltimateCharacterController.ThirdPersonController.Camera.ViewTypes;
using UnityEngine;

/// <summary>
/// UCC-owned third-person view used while a combat target is locked.
/// UCC invokes this view to write the camera Transform, preserving its collision solver.
/// </summary>
[System.Serializable]
public sealed class CombatLockAdventureViewType : LitSmoothAdventureViewType
{
    [SerializeField, Range(1f, 720f)] private float maximumLockRotationDegreesPerSecond = 95f;
    [SerializeField, Min(0.1f)] private float lockAxisSharpness = 7f;
    [SerializeField, Min(0f)] private float lockFollowSmoothTime = 0.08f;
    [SerializeField] private Vector3 combatLookOffset = new Vector3(0.75f, 0.45f, -5f);
    [SerializeField, Range(15f, 100f)] private float combatFieldOfView = 64f;

    private Vector3 impactLookOffset;
    private float impactFieldOfView;
    private float warningFieldOfView;
    private Vector3 smoothedPlayerToEnemyAxis;
    private bool hasSmoothedPlayerToEnemyAxis;

    protected override float EffectiveFollowSmoothTime => lockFollowSmoothTime;

    public void ConfigureLockMotion(float maximumOrbitDegreesPerSecond, float axisSharpness)
    {
        maximumLockRotationDegreesPerSecond = Mathf.Max(1f, maximumOrbitDegreesPerSecond);
        lockAxisSharpness = Mathf.Max(0.1f, axisSharpness);
    }

    public void ConfigureCombatFraming(Vector3 cameraOffset, float fieldOfView)
    {
        combatLookOffset = cameraOffset;
        combatFieldOfView = Mathf.Clamp(fieldOfView, 15f, 100f);
        ApplyCombatFraming();
    }

    public void ResetLockAxisSmoothing()
    {
        hasSmoothedPlayerToEnemyAxis = false;
    }

    /// <summary>
    /// Uses a wider, over-the-shoulder framing so the player remains in front
    /// of the camera while the lock point takes visual priority.
    /// </summary>
    public void ApplyCombatFraming()
    {
        LookOffset = combatLookOffset + impactLookOffset;
        FieldOfView = Mathf.Clamp(combatFieldOfView + impactFieldOfView + warningFieldOfView, 15f, 100f);
    }

    public void SetImpactPresentation(Vector3 lookOffsetKick, float fieldOfViewKick)
    {
        impactLookOffset = lookOffsetKick;
        impactFieldOfView = fieldOfViewKick;
        ApplyCombatFraming();
    }

    public void SetWarningPresentation(float fieldOfViewOffset)
    {
        warningFieldOfView = fieldOfViewOffset;
        ApplyCombatFraming();
    }

    public override Quaternion Rotate(float horizontalMovement, float verticalMovement, bool immediateUpdate)
    {
        CombatLockUccCameraAdapter adapter = m_GameObject.GetComponent<CombatLockUccCameraAdapter>();
        // The right stick must not compete with the target framing while locked.
        // UCC still owns the returned rotation and applies the collision-aware move.
        if (adapter != null && adapter.LockActive)
        {
            return base.Rotate(0f, 0f, immediateUpdate);
        }

        return base.Rotate(horizontalMovement, verticalMovement, immediateUpdate);
    }

    public override Quaternion LateRotate(bool immediateUpdate)
    {
        Quaternion fallbackRotation = base.LateRotate(immediateUpdate);
        CombatLockUccCameraAdapter adapter = m_GameObject.GetComponent<CombatLockUccCameraAdapter>();
        if (adapter == null || !adapter.TryGetLookPoint(out Vector3 lookPoint))
        {
            return fallbackRotation;
        }

        Vector3 direction = lookPoint - m_Transform.position;
        if (adapter.TryGetPlayerToEnemyDirection(out Vector3 playerToEnemy))
        {
            playerToEnemy = SmoothLockAxis(playerToEnemy, immediateUpdate);
            // Plan the rotation from the player-facing combat axis rather than
            // the current camera side. The following UCC Move places the camera
            // at the negative LookOffset depth, behind the player, with a small
            // shoulder offset and a stable rear margin.
            Vector3 upAxis = m_CharacterLocomotion != null ? m_CharacterLocomotion.Up : Vector3.up;
            Vector3 shoulder = Vector3.Cross(upAxis, playerToEnemy).normalized * combatLookOffset.x;
            Vector3 plannedCameraPosition = GetAnchorPosition()
                + upAxis * combatLookOffset.y
                + shoulder
                - playerToEnemy * Mathf.Abs(combatLookOffset.z);
            direction = lookPoint - plannedCameraPosition;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return fallbackRotation;
        }

        Vector3 up = m_CharacterLocomotion != null ? m_CharacterLocomotion.Up : Vector3.up;
        Quaternion targetRotation = Quaternion.LookRotation(direction, up);
        if (immediateUpdate)
        {
            SynchronizeOrbitAngles(targetRotation);
            return targetRotation;
        }

        Quaternion resolvedRotation = Quaternion.RotateTowards(
            fallbackRotation,
            targetRotation,
            maximumLockRotationDegreesPerSecond * Time.unscaledDeltaTime);
        SynchronizeOrbitAngles(resolvedRotation);
        return resolvedRotation;
    }

    private Vector3 SmoothLockAxis(Vector3 targetAxis, bool immediateUpdate)
    {
        if (!hasSmoothedPlayerToEnemyAxis || immediateUpdate)
        {
            smoothedPlayerToEnemyAxis = targetAxis;
            hasSmoothedPlayerToEnemyAxis = true;
            return smoothedPlayerToEnemyAxis;
        }

        float blend = 1f - Mathf.Exp(-lockAxisSharpness * Time.unscaledDeltaTime);
        smoothedPlayerToEnemyAxis = Vector3.Slerp(smoothedPlayerToEnemyAxis, targetAxis, blend).normalized;
        return smoothedPlayerToEnemyAxis;
    }

    private void SynchronizeOrbitAngles(Quaternion worldRotation)
    {
        // CameraController.Rotate runs before LateRotate. Persist the lock
        // correction into UCC's orbit state so the next frame does not reset
        // toward the pre-lock yaw/pitch and create a visible vibration.
        Quaternion localRotation = Quaternion.Inverse(m_BaseRotation)
            * worldRotation
            * Quaternion.Inverse(Quaternion.LookRotation(m_ForwardAxis));
        Vector3 angles = localRotation.eulerAngles;
        m_Pitch = NormalizeSignedAngle(angles.x);
        m_Yaw = NormalizeSignedAngle(angles.y);
    }

    private static float NormalizeSignedAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
