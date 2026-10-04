using Opsive.UltimateCharacterController.ThirdPersonController.Camera.ViewTypes;
using UnityEngine;

/// <summary>UCC calls this view; no second camera or Transform writer.</summary>
[System.Serializable]
public sealed class LitTacticalUccViewType : Adventure
{
    private LitGameplayCameraModeController owner;
    private LitTacticalCameraProfile profile;
    private Vector3 pivot, targetPivot, pivotVelocity, lastAnchor;
    private float yaw, pitch, zoom, targetZoom, zoomVelocity, groundVelocity;
    private bool following = true, initialized, recentering;
    private Vector2 pendingPan;
    private Vector3 lastResolvedPosition;
    private LitTacticalSimulationClock clock;
    private LitTacticalDistanceRecovery distanceRecovery;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    public Vector3 Pivot => pivot;
    public bool Following => following;
    public override bool UseSmoothOffset => false; // Never rotate the world orbit with combatant yaw.

    public void SetFreeCamera(bool free)
    {
        if (!free) { Recenter(false); return; }
        if (!initialized) Recenter(true);
        following = false;
        recentering = false;
        targetPivot = pivot;
        pivotVelocity = Vector3.zero;
        pendingPan = Vector2.zero;
    }

    public void Configure(LitGameplayCameraModeController controller, LitTacticalCameraProfile settings, ThirdPerson source)
    {
        owner = controller;
        profile = settings;
        CollisionRadius = source.CollisionRadius;
        CollisionAnchorOffset = source.CollisionAnchorOffset;
        FieldOfView = profile.fieldOfView;
        zoom = targetZoom = Mathf.Clamp(profile.initialDistance, profile.distanceLimits.x, profile.distanceLimits.y);
        pitch = profile.initialPitch;
    }

    public void Enter(float worldYaw)
    {
        yaw = worldYaw;
        Recenter(true);
    }
    public void Recenter(bool immediate)
    {
        if (immediate) owner?.ResetPoseTracking();
        following = true;
        recentering = !immediate;
        pivotVelocity = Vector3.zero;
        zoomVelocity = groundVelocity = 0;
        pendingPan = Vector2.zero;
        if (m_CharacterTransform == null) { initialized = false; return; }
        targetPivot = GetAnchorPosition();
        if (immediate) pivot = targetPivot;
        lastAnchor = GetAnchorPosition();
        distanceRecovery.Reset(zoom);
        initialized = true;
        clock.Reset();
    }

    private void ConsumePendingInput()
    {
        if (owner == null || profile == null) return;
        var input = owner.ConsumeTacticalInput();
        yaw += input.orbit.x;
        pitch = Mathf.Clamp(pitch - input.orbit.y, profile.pitchLimits.x, profile.pitchLimits.y);
        targetZoom = Mathf.Clamp(targetZoom - input.zoom, profile.distanceLimits.x, profile.distanceLimits.y);
        pendingPan += input.pan;
    }

    private void Tick(float dt, bool immediate)
    {
        Vector3 anchor = GetAnchorPosition();
        if (!initialized || (anchor - lastAnchor).sqrMagnitude > profile.teleportDistance * profile.teleportDistance) Recenter(true);
        lastAnchor = anchor;
        Quaternion planar = Quaternion.Euler(0, yaw, 0);
        Vector3 pan = immediate ? Vector3.zero : planar * new Vector3(pendingPan.x, 0, pendingPan.y);
        pendingPan = Vector2.zero;
        pan = LitTacticalCameraMath.SlowOutwardPan(pan, targetPivot - anchor, profile.maximumPanRadius, profile.boundarySlowZone);
        // Only the explicit L3 inspection toggle can detach character follow.
        if (!following && pan.sqrMagnitude > .000001f) targetPivot += pan;
        if (following) targetPivot = anchor;
        targetPivot = LitTacticalCameraMath.ClampPivot(targetPivot, anchor, profile.maximumPanRadius);
        if (!following) targetPivot = LitTacticalBounds.Constrain(targetPivot, m_CharacterTransform.position);
        // Probe close to the previous floor, never select an arbitrary floor above it.
        if (!following && !immediate)
        {
            float anchorHeight = anchor.y - m_CharacterTransform.position.y;
            Vector3 start = new Vector3(targetPivot.x, pivot.y - anchorHeight + profile.groundProbeHeight, targetPivot.z);
            int count = Physics.RaycastNonAlloc(start, Vector3.down, groundHits, profile.groundProbeHeight * 2,
                m_CharacterLayerManager.IgnoreInvisibleCharacterWaterLayers, QueryTriggerInteraction.Ignore);
            float closestHeight = profile.maximumGroundStep;
            float groundHeight = pivot.y;
            for (int i = 0; count < groundHits.Length && i < count; i++)
            {
                if (owner.ShouldIgnoreTacticalCollider(groundHits[i].collider) || groundHits[i].normal.y < .45f) continue;
                float height = groundHits[i].point.y + anchorHeight;
                float difference = Mathf.Abs(height - pivot.y);
                if (difference > closestHeight) continue;
                closestHeight = difference;
                groundHeight = height;
            }
            targetPivot.y = Mathf.SmoothDamp(pivot.y, groundHeight, ref groundVelocity, profile.groundSmoothTime, Mathf.Infinity, dt);
        }
        Vector3 nextPivot = immediate ? targetPivot : Vector3.SmoothDamp(pivot, targetPivot, ref pivotVelocity,
            recentering ? profile.recenterTime : following ? profile.followTime : profile.panTime, Mathf.Infinity, dt);
        if (!following)
        {
            Vector3 constrained = owner.ConstrainPan(pivot, nextPivot, CollisionRadius);
            if ((constrained - nextPivot).sqrMagnitude > .0001f) { pivotVelocity = Vector3.zero; targetPivot = constrained; }
            nextPivot = constrained;
        }
        pivot = nextPivot;
        pivot = LitTacticalCameraMath.ClampPivot(pivot, anchor, profile.maximumPanRadius);
        if (!following) pivot = LitTacticalBounds.Constrain(pivot, m_CharacterTransform.position);
        if (recentering && (pivot - anchor).sqrMagnitude < .0025f) recentering = false;
        zoom = immediate ? targetZoom : Mathf.SmoothDamp(zoom, targetZoom, ref zoomVelocity, profile.zoomTime, Mathf.Infinity, dt);
        if (immediate) pivotVelocity = Vector3.zero;
    }

    public override Quaternion Rotate(float horizontalMovement, float verticalMovement, bool immediateUpdate)
    {
        if (!immediateUpdate) ConsumePendingInput();
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        m_Pitch = pitch;
        m_Yaw = Mathf.DeltaAngle(0, yaw - m_BaseRotation.eulerAngles.y);
        return owner != null ? owner.BlendRotation(rotation) : rotation;
    }
    public override Quaternion LateRotate(bool immediateUpdate)
    {
        if (!following || owner == null || owner.IsBlending)
            return m_CharacterLocomotion != null && m_CharacterLocomotion.Interpolate ? m_Transform.rotation :
                owner != null ? owner.BlendRotation(Quaternion.Euler(pitch, yaw, 0)) : Quaternion.Euler(pitch, yaw, 0);
        // GetRenderedAnchor uses the current camera axes for UCC's anchor offset.
        // Feeding it back into rotation makes the target move whenever this view rotates,
        // producing a visible frame-to-frame correction. The tactical pivot is the stable,
        // character-owned anchor that Move already follows.
        Vector3 displayedAnchor = GetAnchorPosition();
        Vector3 aim = displayedAnchor - m_Transform.position;
        if (aim.sqrMagnitude < .0001f) return m_Transform.rotation;
        Quaternion rotation = Quaternion.LookRotation(aim, Vector3.up);
        m_Pitch = Mathf.DeltaAngle(0, rotation.eulerAngles.x);
        m_Yaw = Mathf.DeltaAngle(0, rotation.eulerAngles.y - m_BaseRotation.eulerAngles.y);
        owner.RecordTacticalMotion(GetAnchorPosition(), displayedAnchor, lastResolvedPosition, m_Transform.position, 0, false, true);
        return rotation;
    }
    public override Vector3 Move(bool immediateUpdate)
    {
        if (owner == null || profile == null || m_CharacterTransform == null) return m_Transform.position;
        bool advance = clock.TryAdvance(Time.inFixedTimeStep, Time.fixedTimeAsDouble, Time.frameCount, immediateUpdate);
        if (!advance && !immediateUpdate && initialized) return lastResolvedPosition;
        float dt = immediateUpdate ? 0 : Mathf.Min(Time.inFixedTimeStep ? Time.fixedDeltaTime : Time.deltaTime, .1f);
        if (!immediateUpdate) ConsumePendingInput();
        Tick(dt, immediateUpdate);
        // A teleport reset inside Tick may have cleared the stamp. Seal this step again.
        if (advance) clock.TryAdvance(Time.inFixedTimeStep, Time.fixedTimeAsDouble, Time.frameCount);
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 direction = rotation * Vector3.back;
        Vector3 desired = pivot + direction * zoom;
        if (!following) desired = LitTacticalCameraMath.ClampCameraDistance(desired, GetAnchorPosition(), profile.maximumFreeCameraDistance);
        Vector3 requested = desired;
        owner.PrepareVisibilityMask(desired, GetAnchorPosition(), CollisionRadius);
        desired = owner.ConstrainPose(pivot, desired, CollisionRadius);
        float clearDistance = Vector3.Distance(pivot, desired);
        direction = (desired - pivot).normalized;
        float resolvedDistance = distanceRecovery.Resolve(clearDistance, Vector3.Distance(pivot, requested), dt,
            immediateUpdate, profile.collisionClearHoldTime, profile.collisionReturnTime);
        desired = pivot + direction * resolvedDistance;
        lastResolvedPosition = owner.BlendPosition(desired, pivot, CollisionRadius, dt, immediateUpdate);
        owner.RecordTacticalMotion(GetAnchorPosition(), GetAnchorPosition(), requested, lastResolvedPosition, dt, immediateUpdate, false);
        return lastResolvedPosition;
    }
}
