using Opsive.UltimateCharacterController.ThirdPersonController.Camera.ViewTypes;
using UnityEngine;

/// <summary>
/// UCC Adventure view with camera-follow damping. UCC still calculates the
/// desired pose and collision response; only the safe final position is eased.
/// </summary>
[System.Serializable]
public class LitSmoothAdventureViewType : Adventure
{
    [Header("Lit Follow Damping")]
    [SerializeField, Min(0f)] private float followSmoothTime = 0.14f;
    [SerializeField, Min(0f)] private float maximumFollowSpeed = 30f;
    [SerializeField, Min(0f)] private float teleportSnapDistance = 3f;

    private Vector3 smoothedPosition;
    private Vector3 followVelocity;
    private bool hasSmoothedPosition;

    protected virtual float EffectiveFollowSmoothTime => followSmoothTime;

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
            maximumFollowSpeed = smoothSource.maximumFollowSpeed;
            teleportSnapDistance = smoothSource.teleportSnapDistance;
        }
    }

    public void ConfigureFollowDamping(float smoothTime, float maximumSpeed, float snapDistance)
    {
        followSmoothTime = Mathf.Max(0f, smoothTime);
        maximumFollowSpeed = Mathf.Max(0f, maximumSpeed);
        teleportSnapDistance = Mathf.Max(0f, snapDistance);
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
        if (immediateUpdate || EffectiveFollowSmoothTime <= 0f)
        {
            return SeedFollowPosition(targetPosition);
        }

        if (!hasSmoothedPosition)
        {
            smoothedPosition = m_Transform.position;
            followVelocity = Vector3.zero;
            hasSmoothedPosition = true;
        }

        Vector3 anchorPosition = GetAnchorPosition() + m_CollisionAnchorOffset;
        if (MustSnapToTarget(anchorPosition, targetPosition))
        {
            return SeedFollowPosition(targetPosition);
        }

        Vector3 candidate = Vector3.SmoothDamp(
            smoothedPosition,
            targetPosition,
            ref followVelocity,
            EffectiveFollowSmoothTime,
            maximumFollowSpeed,
            Time.deltaTime);

        candidate = ConstrainSmoothedPosition(anchorPosition, candidate, out bool collisionConstrained);
        if (collisionConstrained)
        {
            followVelocity = Vector3.zero;
        }

        smoothedPosition = candidate;
        return candidate;
    }

    private Vector3 SeedFollowPosition(Vector3 position)
    {
        smoothedPosition = position;
        followVelocity = Vector3.zero;
        hasSmoothedPosition = true;
        return position;
    }

    private bool MustSnapToTarget(Vector3 anchorPosition, Vector3 targetPosition)
    {
        if ((targetPosition - smoothedPosition).sqrMagnitude >= teleportSnapDistance * teleportSnapDistance)
        {
            return true;
        }

        // Never ease inward when UCC has pulled the camera towards its anchor
        // to prevent wall clipping. Easing only occurs while expanding back out.
        float currentDistance = Vector3.Distance(smoothedPosition, anchorPosition);
        float targetDistance = Vector3.Distance(targetPosition, anchorPosition);
        return targetDistance < currentDistance - 0.02f;
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
