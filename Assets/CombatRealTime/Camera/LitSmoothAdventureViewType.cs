using Opsive.UltimateCharacterController.ThirdPersonController.Camera.ViewTypes;
using UnityEngine;
using System.Text;

/// <summary>
/// UCC Adventure view with camera-follow damping. UCC still calculates the
/// desired pose and collision response; only the safe final position is eased.
/// </summary>
[System.Serializable]
public class LitSmoothAdventureViewType : Adventure
{
    [Header("Lit Follow Damping")]
    [SerializeField, Min(0f)] private float followSmoothTime = 0.16f;
    [SerializeField, Min(0f)] private float maximumFollowSpeed = 30f;
    [SerializeField, Min(0f), Tooltip("Additional position damping while the character is airborne. Aim rotation remains fully responsive.")]
    private float airborneFollowSmoothTime = 0.24f;
    [SerializeField, Min(0f), Tooltip("Maximum camera follow speed while airborne, preventing a sharp vertical catch-up on takeoff.")]
    private float airborneMaximumFollowSpeed = 18f;
    [SerializeField, Min(0f)] private float teleportSnapDistance = 3f;
    [SerializeField, Min(0f), Tooltip("Only snap the camera inward when UCC had to shorten its distance by at least this amount. Smaller obstacle corrections are eased so narrow corridors remain readable.")]
    private float hardCollisionSnapDistance = 1.25f;
    [SerializeField, Tooltip("Optional second collision cast for the smoothed camera position. Leave disabled to favor stable framing around small obstacles; UCC's native collision solver remains active.")]
    private bool useSupplementalCollisionConstraint;

    private Vector3 smoothedPosition;
    private Vector3 followVelocity;
    private bool hasSmoothedPosition;
    private bool immediatePoseRequested;
    private CameraSnapReason immediatePoseReason;
    private bool recordMotionDiagnostics;
    private const int MotionHistoryCapacity = 120;
    private readonly MotionSample[] motionHistory = new MotionSample[MotionHistoryCapacity];
    private int motionHistoryNext;
    private int motionHistoryCount;

    private struct MotionSample
    {
        public float Time;
        public float DeltaTime;
        public float TargetError;
        public bool ImmediateUpdate;
        public bool Snapped;
        public CameraSnapReason Reason;
    }

    protected virtual float EffectiveFollowSmoothTime => IsAirborneFollowActive
        ? Mathf.Max(followSmoothTime, airborneFollowSmoothTime)
        : followSmoothTime;
    protected virtual float EffectiveMaximumFollowSpeed => IsAirborneFollowActive && airborneMaximumFollowSpeed > 0f
        ? Mathf.Min(maximumFollowSpeed, airborneMaximumFollowSpeed)
        : maximumFollowSpeed;
    private bool IsAirborneFollowActive => m_CharacterLocomotion != null && !m_CharacterLocomotion.Grounded;

    /// <summary>Copies the gameplay ViewType presentation without taking ownership from UCC.</summary>
    public void CopyGameplaySettingsFrom(ThirdPerson source)
    {
        if (source == null)
        {
            return;
        }

        FieldOfView = source.FieldOfView;
        FieldOfViewDamping = source.FieldOfViewDamping;
        ForwardAxis = source.ForwardAxis;
        LookOffset = source.LookOffset;
        LookOffsetSmoothing = source.LookOffsetSmoothing;
        CollisionRadius = source.CollisionRadius;
        CollisionAnchorOffset = source.CollisionAnchorOffset;
        RotationSpeed = source.RotationSpeed;
        SecondaryRotationSpeed = source.SecondaryRotationSpeed;
        HorizontalPivotFreedom = source.HorizontalPivotFreedom;
        PitchLimit = source.PitchLimit;
        if (source is Adventure adventure)
        {
            YawLimit = adventure.YawLimit;
            YawLimitLerpSpeed = adventure.YawLimitLerpSpeed;
            RotateWithCharacter = adventure.RotateWithCharacter;
        }

        if (source is LitSmoothAdventureViewType smoothSource)
        {
            followSmoothTime = smoothSource.followSmoothTime;
            maximumFollowSpeed = smoothSource.maximumFollowSpeed;
            airborneFollowSmoothTime = smoothSource.airborneFollowSmoothTime;
            airborneMaximumFollowSpeed = smoothSource.airborneMaximumFollowSpeed;
            teleportSnapDistance = smoothSource.teleportSnapDistance;
        }
    }

    public void ConfigureFollowDamping(
        float smoothTime,
        float maximumSpeed,
        float airborneSmoothTime,
        float airborneMaximumSpeed,
        float snapDistance,
        float hardSnapDistance,
        bool supplementalCollisionConstraint,
        bool diagnosticsEnabled)
    {
        followSmoothTime = Mathf.Max(0f, smoothTime);
        maximumFollowSpeed = Mathf.Max(0f, maximumSpeed);
        airborneFollowSmoothTime = Mathf.Max(0f, airborneSmoothTime);
        airborneMaximumFollowSpeed = Mathf.Max(0f, airborneMaximumSpeed);
        teleportSnapDistance = Mathf.Max(0f, snapDistance);
        hardCollisionSnapDistance = Mathf.Max(0f, hardSnapDistance);
        useSupplementalCollisionConstraint = supplementalCollisionConstraint;
        recordMotionDiagnostics = diagnosticsEnabled;
    }

    public void RequestImmediatePose(CameraSnapReason reason)
    {
        immediatePoseRequested = true;
        immediatePoseReason = reason;
        ResetFollowSmoothing();
    }

    public string BuildMotionDiagnosticsReport()
    {
        StringBuilder report = new StringBuilder("[UccCameraMotion] samples=").Append(motionHistoryCount);
        for (int i = 0; i < motionHistoryCount; i++)
        {
            int index = (motionHistoryNext - motionHistoryCount + i + MotionHistoryCapacity) % MotionHistoryCapacity;
            MotionSample sample = motionHistory[index];
            report.Append('\n').Append(sample.Time.ToString("F3"))
                .Append(" dt=").Append(sample.DeltaTime.ToString("F3"))
                .Append(" error=").Append(sample.TargetError.ToString("F3"))
                .Append(" immediate=").Append(sample.ImmediateUpdate)
                .Append(" snap=").Append(sample.Snapped)
                .Append(" reason=").Append(sample.Reason);
        }

        return report.ToString();
    }

    /// <summary>Clears follow inertia; the following UCC move seeds from the current camera pose.</summary>
    public void ResetFollowSmoothing()
    {
        followVelocity = Vector3.zero;
        hasSmoothedPosition = false;
    }

    public override void Awake()
    {
        base.Awake();
        ResetFollowSmoothing();
    }

    public override void ChangeViewType(bool activate, float pitch, float yaw, Quaternion baseCharacterRotation)
    {
        base.ChangeViewType(activate, pitch, yaw, baseCharacterRotation);
        ResetFollowSmoothing();
    }

    public override void Reset(Quaternion characterRotation)
    {
        base.Reset(characterRotation);
        ResetFollowSmoothing();
    }

    public override Vector3 Move(bool immediateUpdate)
    {
        // Keep UCC's native anchor, obstacle and character-clipping solver as
        // the source of truth before applying any presentation smoothing.
        Vector3 targetPosition = base.Move(immediateUpdate);
        bool explicitSnap = immediatePoseRequested;
        CameraSnapReason snapReason = immediatePoseReason;
        immediatePoseRequested = false;
        if (explicitSnap || EffectiveFollowSmoothTime <= 0f)
        {
            return SeedFollowPositionAndRecord(targetPosition, immediateUpdate, true, explicitSnap ? snapReason : CameraSnapReason.SceneLoad);
        }

        if (!hasSmoothedPosition)
        {
            return SeedFollowPositionAndRecord(targetPosition, immediateUpdate, true, CameraSnapReason.InitialBind);
        }

        Vector3 anchorPosition = GetAnchorPosition() + m_CollisionAnchorOffset;
        if (MustSnapToTarget(anchorPosition, targetPosition))
        {
            return SeedFollowPositionAndRecord(targetPosition, immediateUpdate, true, CameraSnapReason.Collision);
        }

        Vector3 candidate = Vector3.SmoothDamp(
            smoothedPosition,
            targetPosition,
            ref followVelocity,
            EffectiveFollowSmoothTime,
            EffectiveMaximumFollowSpeed,
            Time.deltaTime);

        if (useSupplementalCollisionConstraint)
        {
            candidate = ConstrainSmoothedPosition(anchorPosition, candidate, out bool collisionConstrained);
            if (collisionConstrained)
            {
                followVelocity = Vector3.zero;
            }
        }

        smoothedPosition = candidate;
        RecordMotion(targetPosition, immediateUpdate, false, CameraSnapReason.InitialBind);
        return candidate;
    }

    private Vector3 SeedFollowPositionAndRecord(Vector3 position, bool immediateUpdate, bool snapped, CameraSnapReason reason)
    {
        Vector3 seeded = SeedFollowPosition(position);
        RecordMotion(position, immediateUpdate, snapped, reason);
        return seeded;
    }

    private Vector3 SeedFollowPosition(Vector3 position)
    {
        smoothedPosition = position;
        followVelocity = Vector3.zero;
        hasSmoothedPosition = true;
        return position;
    }

    private void RecordMotion(Vector3 targetPosition, bool immediateUpdate, bool snapped, CameraSnapReason reason)
    {
        if (!recordMotionDiagnostics)
        {
            return;
        }

        motionHistory[motionHistoryNext] = new MotionSample
        {
            Time = Time.unscaledTime,
            DeltaTime = Time.unscaledDeltaTime,
            TargetError = (targetPosition - smoothedPosition).magnitude,
            ImmediateUpdate = immediateUpdate,
            Snapped = snapped,
            Reason = reason
        };
        motionHistoryNext = (motionHistoryNext + 1) % MotionHistoryCapacity;
        motionHistoryCount = Mathf.Min(motionHistoryCount + 1, MotionHistoryCapacity);
    }

    private bool MustSnapToTarget(Vector3 anchorPosition, Vector3 targetPosition)
    {
        if ((targetPosition - smoothedPosition).sqrMagnitude >= teleportSnapDistance * teleportSnapDistance)
        {
            return true;
        }

        // UCC already resolves its collision position. Small inward corrections
        // are intentionally eased: in a narrow corridor this favors framing
        // continuity over constantly bouncing around minor geometry. A large
        // correction still snaps before the camera can spend visible time in a wall.
        float currentDistance = Vector3.Distance(smoothedPosition, anchorPosition);
        float targetDistance = Vector3.Distance(targetPosition, anchorPosition);
        return targetDistance < currentDistance - hardCollisionSnapDistance;
    }

    private Vector3 ConstrainSmoothedPosition(Vector3 collisionOrigin, Vector3 candidate, out bool constrained)
    {
        constrained = false;
        Vector3 direction = candidate - collisionOrigin;
        float distance = direction.magnitude;
        if (distance <= 0.0001f || m_CharacterLocomotion == null)
        {
            return candidate;
        }

        direction /= distance;
        bool collisionEnabled = m_CharacterLocomotion.CollisionLayerEnabled;
        m_CharacterLocomotion.EnableColliderCollisionLayer(false);
        bool hit = Physics.SphereCast(
            collisionOrigin - direction * m_CollisionRadius,
            m_CollisionRadius,
            direction,
            out RaycastHit raycastHit,
            distance,
            m_CharacterLayerManager.IgnoreInvisibleCharacterWaterLayers,
            QueryTriggerInteraction.Ignore);
        m_CharacterLocomotion.EnableColliderCollisionLayer(collisionEnabled);

        if (!hit)
        {
            return candidate;
        }

        constrained = true;
        return raycastHit.point + raycastHit.normal * m_CollisionRadius;
    }
}
