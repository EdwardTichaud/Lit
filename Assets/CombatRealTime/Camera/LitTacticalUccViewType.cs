using Opsive.UltimateCharacterController.ThirdPersonController.Camera.ViewTypes;
using UnityEngine;

/// <summary>UCC calls this view; no second camera or Transform writer.</summary>
[System.Serializable]
public sealed class LitTacticalUccViewType : Adventure
{
    private LitGameplayCameraModeController owner;
    private LitTacticalCameraProfile profile;
    private Vector3 pivot, targetPivot, pivotVelocity, lastAnchor;
    private float yaw, pitch, zoom, targetZoom, zoomVelocity, groundVelocity, resolvedDistance, distanceVelocity;
    private bool following = true, initialized, recentering;
    private int lastFrame = -1, lastCollisionFrame = -1;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    public Vector3 Pivot => pivot;
    public bool Following => following;

    public void SetFreeCamera(bool free)
    {
        if (!free) { Recenter(false); return; }
        if (!initialized) Recenter(true);
        following = false;
        recentering = false;
        targetPivot = pivot;
        pivotVelocity = Vector3.zero;
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
        zoomVelocity = groundVelocity = distanceVelocity = 0;
        if (m_CharacterTransform == null) { initialized = false; return; }
        targetPivot = GetAnchorPosition();
        if (immediate) pivot = targetPivot;
        lastAnchor = GetAnchorPosition();
        resolvedDistance = zoom;
        initialized = true;
        lastFrame = -1;
        lastCollisionFrame = -1;
    }

    private void Tick()
    {
        if (lastFrame == Time.frameCount || owner == null || profile == null || m_CharacterTransform == null) return;
        lastFrame = Time.frameCount;
        Vector3 anchor = GetAnchorPosition();
        if (!initialized || (anchor - lastAnchor).sqrMagnitude > profile.teleportDistance * profile.teleportDistance) Recenter(true);
        lastFrame = Time.frameCount;
        lastAnchor = anchor;
        float dt = Mathf.Min(Time.deltaTime, .1f);
        var input = owner.FrameInput;
        yaw += input.orbit.x;
        pitch = Mathf.Clamp(pitch - input.orbit.y, profile.pitchLimits.x, profile.pitchLimits.y);
        targetZoom = Mathf.Clamp(targetZoom - input.zoom, profile.distanceLimits.x, profile.distanceLimits.y);
        Quaternion planar = Quaternion.Euler(0, yaw, 0);
        Vector3 pan = planar * new Vector3(input.pan.x, 0, input.pan.y);
        pan = LitTacticalCameraMath.SlowOutwardPan(pan, targetPivot - anchor, profile.maximumPanRadius, profile.boundarySlowZone);
        // Only the explicit L3 inspection toggle can detach character follow.
        if (!following && pan.sqrMagnitude > .000001f) targetPivot += pan;
        if (following) targetPivot = anchor;
        targetPivot = LitTacticalCameraMath.ClampPivot(targetPivot, anchor, profile.maximumPanRadius);
        if (!following) targetPivot = LitTacticalBounds.Constrain(targetPivot, m_CharacterTransform.position);
        // Probe close to the previous floor, never select an arbitrary floor above it.
        if (!following)
        {
            float anchorHeight = anchor.y - m_CharacterTransform.position.y;
            Vector3 start = new Vector3(targetPivot.x, pivot.y - anchorHeight + profile.groundProbeHeight, targetPivot.z);
            int count = Physics.RaycastNonAlloc(start, Vector3.down, groundHits, profile.groundProbeHeight * 2,
                m_CharacterLayerManager.IgnoreInvisibleCharacterWaterLayers, QueryTriggerInteraction.Ignore);
            float closestHeight = profile.maximumGroundStep;
            float groundHeight = pivot.y;
            for (int i = 0; count < groundHits.Length && i < count; i++)
            {
                if (groundHits[i].collider.transform.IsChildOf(m_CharacterTransform) || groundHits[i].normal.y < .45f) continue;
                float height = groundHits[i].point.y + anchorHeight;
                float difference = Mathf.Abs(height - pivot.y);
                if (difference > closestHeight) continue;
                closestHeight = difference;
                groundHeight = height;
            }
            targetPivot.y = Mathf.SmoothDamp(pivot.y, groundHeight, ref groundVelocity, profile.groundSmoothTime, Mathf.Infinity, dt);
        }
        Vector3 nextPivot = Vector3.SmoothDamp(pivot, targetPivot, ref pivotVelocity,
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
        zoom = Mathf.SmoothDamp(zoom, targetZoom, ref zoomVelocity, profile.zoomTime, Mathf.Infinity, dt);
    }

    public override Quaternion Rotate(float horizontalMovement, float verticalMovement, bool immediateUpdate)
    {
        Tick();
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        m_Pitch = pitch;
        m_Yaw = Mathf.DeltaAngle(0, yaw - m_BaseRotation.eulerAngles.y);
        return owner != null ? owner.BlendRotation(rotation) : rotation;
    }
    public override Quaternion LateRotate(bool immediateUpdate)
    {
        Tick();
        if (!following || owner == null || owner.IsBlending) return Rotate(0, 0, immediateUpdate);
        // Aim from the actual collision-resolved pose, not the nominal orbit.
        // The pivot may lag or retract, but the character stays in the sightline.
        Vector3 aim = GetAnchorPosition() - m_Transform.position;
        if (aim.sqrMagnitude < .0001f) return m_Transform.rotation;
        Quaternion rotation = Quaternion.LookRotation(aim, Vector3.up);
        m_Pitch = Mathf.DeltaAngle(0, rotation.eulerAngles.x);
        m_Yaw = Mathf.DeltaAngle(0, rotation.eulerAngles.y - m_BaseRotation.eulerAngles.y);
        return rotation;
    }
    public override Vector3 Move(bool immediateUpdate)
    {
        Tick();
        if (!initialized) return m_Transform.position;
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 direction = rotation * Vector3.back;
        Vector3 desired = pivot + direction * zoom;
        if (!following) desired = LitTacticalCameraMath.ClampCameraDistance(desired, GetAnchorPosition(), profile.maximumFreeCameraDistance);
        owner.PrepareVisibilityMask(desired, GetAnchorPosition(), CollisionRadius);
        desired = owner.ConstrainPose(pivot, desired, CollisionRadius);
        float clearDistance = Vector3.Distance(pivot, desired);
        direction = (desired - pivot).normalized;
        if (clearDistance < resolvedDistance) { resolvedDistance = clearDistance; distanceVelocity = 0; }
        else if (lastCollisionFrame != Time.frameCount)
            resolvedDistance = Mathf.SmoothDamp(resolvedDistance, clearDistance, ref distanceVelocity, profile.collisionReturnTime);
        lastCollisionFrame = Time.frameCount;
        desired = pivot + direction * resolvedDistance;
        return owner.BlendPosition(desired, pivot, CollisionRadius);
    }
}
