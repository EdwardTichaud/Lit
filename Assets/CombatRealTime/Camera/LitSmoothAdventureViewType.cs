using Opsive.UltimateCharacterController.ThirdPersonController.Camera.ViewTypes;
using System.Text;
using UnityEngine;

/// <summary>
/// Exploration Adventure view. UCC remains the sole owner of camera placement,
/// collision and aim. This extension only configures the native framing slack:
/// that keeps the composition from being glued to the character without adding
/// a second, visibly delayed camera follow.
/// </summary>
[System.Serializable]
public class LitSmoothAdventureViewType : Adventure
{
    [Header("Lit Exploration Framing")]
    [SerializeField, Min(0f), Tooltip("Native UCC horizontal pivot slack. Keep this at zero: its sign-based threshold can oscillate when moving in one lateral direction.")]
    private float horizontalPivotFreedom;
    [SerializeField, Min(0f), Tooltip("Native UCC smoothing used only for the look offset.")]
    private float lookOffsetSmoothing = 0.08f;
    [SerializeField, Min(0f), Tooltip("Maximum temporary vertical framing offset while the character is airborne. UCC still resolves the final camera collision.")]
    private float airborneVerticalMaximumOffset = 0.32f;
    [SerializeField, Min(0f), Tooltip("Height gained since takeoff converted into temporary framing offset while airborne.")]
    private float airborneVerticalHeightCompression = 0.20f;
    [SerializeField, Min(0.001f)] private float airborneVerticalRiseSmoothTime = 0.14f;
    [SerializeField, Min(0.001f)] private float airborneVerticalFallSmoothTime = 0.16f;
    [SerializeField, Min(0.001f)] private float groundedVerticalRestoreSmoothTime = 0.12f;

    private bool immediatePoseRequested;
    private CameraSnapReason immediatePoseReason;
    private bool recordMotionDiagnostics;
    private const int MotionHistoryCapacity = 120;
    private readonly MotionSample[] motionHistory = new MotionSample[MotionHistoryCapacity];
    private int motionHistoryNext;
    private int motionHistoryCount;
    private Vector3 baseLookOffset;
    private Vector3 lastAppliedLookOffset;
    private bool hasBaseLookOffset;
    private float verticalFramingOffset;
    private float verticalFramingVelocity;
    private bool airborneFramingActive;
    private float airborneStartHeight;
    private int lastUnexpectedAirborneSnapFrame = -1;

    private struct MotionSample
    {
        public float Time;
        public float DeltaTime;
        public float TargetError;
        public bool ImmediateUpdate;
        public bool Snapped;
        public bool Airborne;
        public bool CollisionConstrained;
        public float HorizontalPivotFreedom;
        public float VerticalVelocity;
        public float VerticalOffsetTarget;
        public float VerticalOffsetApplied;
        public CameraSnapReason Reason;
    }

    private bool IsAirborne => m_CharacterLocomotion != null && !m_CharacterLocomotion.Grounded;

    /// <summary>Copies only UCC gameplay-view presentation, never follow damping.</summary>
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
            horizontalPivotFreedom = smoothSource.horizontalPivotFreedom;
            lookOffsetSmoothing = smoothSource.lookOffsetSmoothing;
            airborneVerticalMaximumOffset = smoothSource.airborneVerticalMaximumOffset;
            airborneVerticalHeightCompression = smoothSource.airborneVerticalHeightCompression;
            airborneVerticalRiseSmoothTime = smoothSource.airborneVerticalRiseSmoothTime;
            airborneVerticalFallSmoothTime = smoothSource.airborneVerticalFallSmoothTime;
            groundedVerticalRestoreSmoothTime = smoothSource.groundedVerticalRestoreSmoothTime;
        }

        CaptureBaseLookOffset(source.LookOffset);
    }

    public void ConfigureExplorationFraming(
        float pivotFreedom,
        float offsetSmoothing,
        float verticalMaximumOffset,
        float verticalHeightCompression,
        float verticalRiseSmoothTime,
        float verticalFallSmoothTime,
        float groundedRestoreSmoothTime)
    {
        horizontalPivotFreedom = Mathf.Max(0f, pivotFreedom);
        lookOffsetSmoothing = Mathf.Max(0f, offsetSmoothing);
        airborneVerticalMaximumOffset = Mathf.Max(0f, verticalMaximumOffset);
        airborneVerticalHeightCompression = Mathf.Max(0f, verticalHeightCompression);
        airborneVerticalRiseSmoothTime = Mathf.Max(0.001f, verticalRiseSmoothTime);
        airborneVerticalFallSmoothTime = Mathf.Max(0.001f, verticalFallSmoothTime);
        groundedVerticalRestoreSmoothTime = Mathf.Max(0.001f, groundedRestoreSmoothTime);
        HorizontalPivotFreedom = horizontalPivotFreedom;
        LookOffsetSmoothing = lookOffsetSmoothing;
        if (!hasBaseLookOffset)
        {
            CaptureBaseLookOffset(LookOffset);
        }
    }

    public void RequestImmediatePose(CameraSnapReason reason)
    {
        immediatePoseRequested = true;
        immediatePoseReason = reason;
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
                .Append(" airborne=").Append(sample.Airborne)
                .Append(" constrained=").Append(sample.CollisionConstrained)
                .Append(" pivot=").Append(sample.HorizontalPivotFreedom.ToString("F3"))
                .Append(" verticalSpeed=").Append(sample.VerticalVelocity.ToString("F3"))
                .Append(" verticalTarget=").Append(sample.VerticalOffsetTarget.ToString("F3"))
                .Append(" verticalApplied=").Append(sample.VerticalOffsetApplied.ToString("F3"))
                .Append(" reason=").Append(sample.Reason);
        }

        return report.ToString();
    }

    public void SetMotionDiagnosticsEnabled(bool enabled)
    {
        recordMotionDiagnostics = enabled;
        if (!enabled)
        {
            motionHistoryNext = 0;
            motionHistoryCount = 0;
        }
    }

    /// <summary>Clears the presentation inertia after an intentional snap or view change.</summary>
    public virtual void ResetFollowSmoothing()
    {
        if (hasBaseLookOffset)
        {
            LookOffset = baseLookOffset;
            lastAppliedLookOffset = baseLookOffset;
        }
        verticalFramingOffset = 0f;
        verticalFramingVelocity = 0f;
        airborneFramingActive = false;
        hasBaseLookOffset = false;
    }

    /// <summary>
    /// Gives specialized views access to UCC's resolved camera pose without
    /// applying an additional presentation filter.
    /// </summary>
    protected Vector3 GetUccResolvedPosition(bool immediateUpdate) => base.Move(immediateUpdate);

    protected bool ConsumeImmediatePoseRequest(out CameraSnapReason reason)
    {
        reason = immediatePoseReason;
        bool requested = immediatePoseRequested;
        immediatePoseRequested = false;
        return requested;
    }

    protected void RecordMotion(Vector3 targetPosition, Vector3 resolvedPosition, bool immediateUpdate, bool snapped, CameraSnapReason reason)
    {
        RecordMotion(targetPosition, resolvedPosition, immediateUpdate, snapped, reason, false);
    }

    protected void RecordMotion(Vector3 targetPosition, Vector3 resolvedPosition, bool immediateUpdate, bool snapped, CameraSnapReason reason, bool collisionConstrained)
    {
        if (!recordMotionDiagnostics)
        {
            return;
        }

        motionHistory[motionHistoryNext] = new MotionSample
        {
            Time = Time.unscaledTime,
            DeltaTime = Time.unscaledDeltaTime,
            TargetError = (targetPosition - resolvedPosition).magnitude,
            ImmediateUpdate = immediateUpdate,
            Snapped = snapped,
            Airborne = IsAirborne,
            CollisionConstrained = collisionConstrained,
            HorizontalPivotFreedom = HorizontalPivotFreedom,
            VerticalVelocity = ResolveVerticalVelocity(),
            VerticalOffsetTarget = ResolveVerticalFramingTarget(),
            VerticalOffsetApplied = verticalFramingOffset,
            Reason = reason
        };
        motionHistoryNext = (motionHistoryNext + 1) % MotionHistoryCapacity;
        motionHistoryCount = Mathf.Min(motionHistoryCount + 1, MotionHistoryCapacity);
    }

    public override Vector3 Move(bool immediateUpdate)
    {
        bool requestedSnap = ConsumeImmediatePoseRequest(out CameraSnapReason reason);
        if (requestedSnap)
        {
            ResetVerticalFraming();
        }

        // The vertical framing changes the UCC look offset before Adventure
        // calculates its final position and collision. Never post-process the
        // returned world position: that would detach the camera and cause a
        // visible catch-up after direction changes.
        if (!requestedSnap)
        {
            UpdateVerticalFraming();
        }
        Vector3 targetPosition = GetUccResolvedPosition(immediateUpdate);
        ReportUnexpectedAirborneSnap(immediateUpdate, requestedSnap);
        RecordMotion(targetPosition, targetPosition, immediateUpdate, requestedSnap, requestedSnap ? reason : CameraSnapReason.InitialBind);
        return targetPosition;
    }

    private void UpdateVerticalFraming()
    {
        SynchronizeBaseLookOffset();
        float previousOffset = verticalFramingOffset;
        float targetOffset = ResolveVerticalFramingTarget();
        float smoothTime = !IsAirborne
            ? groundedVerticalRestoreSmoothTime
            : targetOffset < previousOffset ? airborneVerticalRiseSmoothTime : airborneVerticalFallSmoothTime;
        verticalFramingOffset = Mathf.SmoothDamp(
            verticalFramingOffset,
            targetOffset,
            ref verticalFramingVelocity,
            smoothTime,
            Mathf.Infinity,
            Time.fixedDeltaTime);

        Vector3 offset = baseLookOffset;
        offset.y += verticalFramingOffset;
        LookOffset = offset;
        lastAppliedLookOffset = offset;
    }

    private void SynchronizeBaseLookOffset()
    {
        if (!hasBaseLookOffset || (LookOffset - lastAppliedLookOffset).sqrMagnitude > 0.000001f)
        {
            CaptureBaseLookOffset(LookOffset);
        }
    }

    private void CaptureBaseLookOffset(Vector3 offset)
    {
        baseLookOffset = offset;
        lastAppliedLookOffset = offset;
        hasBaseLookOffset = true;
    }

    private float ResolveVerticalVelocity()
    {
        if (m_CharacterLocomotion == null)
        {
            return 0f;
        }

        return Vector3.Dot(m_CharacterLocomotion.Velocity, m_CharacterLocomotion.Up);
    }

    private float ResolveVerticalFramingTarget()
    {
        if (!IsAirborne || airborneVerticalMaximumOffset <= 0f)
        {
            airborneFramingActive = false;
            return 0f;
        }

        float currentHeight = Vector3.Dot(CharacterPosition, m_CharacterLocomotion.Up);
        if (!airborneFramingActive)
        {
            airborneFramingActive = true;
            airborneStartHeight = currentHeight;
        }

        // This is intentionally based on height gained since takeoff rather
        // than the instantaneous vertical velocity. The target never flips
        // sign at the apex, eliminating the direction-dependent tremble.
        float gainedHeight = Mathf.Max(0f, currentHeight - airborneStartHeight);
        return -Mathf.Min(airborneVerticalMaximumOffset, gainedHeight * airborneVerticalHeightCompression);
    }

    private void ResetVerticalFraming()
    {
        verticalFramingOffset = 0f;
        verticalFramingVelocity = 0f;
        airborneFramingActive = false;
        if (hasBaseLookOffset)
        {
            LookOffset = baseLookOffset;
            lastAppliedLookOffset = baseLookOffset;
        }
    }

    private void ReportUnexpectedAirborneSnap(bool immediateUpdate, bool requestedSnap)
    {
        if (!recordMotionDiagnostics || !IsAirborne || !immediateUpdate || requestedSnap || lastUnexpectedAirborneSnapFrame == Time.frameCount)
        {
            return;
        }

        lastUnexpectedAirborneSnapFrame = Time.frameCount;
        Debug.LogWarning("[UccCameraMotion] Snap caméra non demandé détecté pendant un saut.", m_GameObject);
    }
}
