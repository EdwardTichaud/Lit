using UnityEngine;

[System.Serializable]
public class CrpgCameraCollision
{
    public struct SolveResult
    {
        public Vector3 anchorPosition;
        public float allowedDistance;
        public bool obstructed;
    }

    [Header("Obstacles")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private float cameraRadius = 0.35f;
    [SerializeField] private float collisionBuffer = 0.15f;
    [SerializeField] private float minCollisionDistance = 1.25f;
    [SerializeField] private float tightSpaceMinDistance = 0.15f;
    [SerializeField] private bool ignoreTargetColliders = true;

    [Header("Relief")]
    [SerializeField] private float groundProbeUp = 8f;
    [SerializeField] private float groundProbeDown = 24f;
    [SerializeField] private float anchorGroundClearance = 0.5f;
    [SerializeField] private float ceilingProbeHeight = 3f;
    [SerializeField] private float ceilingClearance = 0.15f;
    [SerializeField, Range(0.1f, 2f)] private float ceilingProbeRadiusScale = 0.9f;

    private readonly RaycastHit[] obstructionHits = new RaycastHit[16];

    public LayerMask CollisionMask => collisionMask;

    public void Validate()
    {
        cameraRadius = Mathf.Max(0.01f, cameraRadius);
        collisionBuffer = Mathf.Max(0f, collisionBuffer);
        minCollisionDistance = Mathf.Max(0.1f, minCollisionDistance);
        tightSpaceMinDistance = Mathf.Max(0.01f, tightSpaceMinDistance);
        groundProbeUp = Mathf.Max(0.1f, groundProbeUp);
        groundProbeDown = Mathf.Max(0.1f, groundProbeDown);
        anchorGroundClearance = Mathf.Max(0f, anchorGroundClearance);
        ceilingProbeHeight = Mathf.Max(0f, ceilingProbeHeight);
        ceilingClearance = Mathf.Max(0f, ceilingClearance);
        ceilingProbeRadiusScale = Mathf.Clamp(ceilingProbeRadiusScale, 0.1f, 2f);
    }

    public SolveResult Solve(Vector3 desiredAnchorPosition, Quaternion rigRotation, float desiredDistance, Transform ignoredTarget)
    {
        Vector3 anchor = ResolveAnchorHeight(desiredAnchorPosition, ignoredTarget);
        Vector3 toCameraDirection = rigRotation * Vector3.back;
        float allowedDistance = desiredDistance;
        bool obstructed = false;

        if (desiredDistance > 0.01f)
        {
            int hitCount = Physics.SphereCastNonAlloc(
                anchor,
                cameraRadius,
                toCameraDirection,
                obstructionHits,
                desiredDistance,
                collisionMask,
                QueryTriggerInteraction.Ignore);

            float closest = desiredDistance;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = obstructionHits[i];
                if (ShouldIgnoreCollider(hit.collider, ignoredTarget))
                {
                    continue;
                }

                if (hit.distance < closest)
                {
                    closest = hit.distance;
                }
            }

            if (closest < desiredDistance)
            {
                obstructed = true;
                float availableDistance = Mathf.Max(0f, closest - collisionBuffer);
                float hardMinDistance = Mathf.Min(
                    desiredDistance,
                    Mathf.Max(0.01f, Mathf.Min(minCollisionDistance, tightSpaceMinDistance)));
                allowedDistance = Mathf.Clamp(availableDistance, hardMinDistance, desiredDistance);
            }
        }

        return new SolveResult
        {
            anchorPosition = anchor,
            allowedDistance = allowedDistance,
            obstructed = obstructed
        };
    }

    private Vector3 ResolveAnchorHeight(Vector3 anchorPosition, Transform ignoredTarget)
    {
        float minimumY = float.NegativeInfinity;
        Vector3 origin = anchorPosition + Vector3.up * groundProbeUp;
        float maxDistance = groundProbeUp + groundProbeDown;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            obstructionHits,
            maxDistance,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        float closestGroundDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = obstructionHits[i];
            if (ShouldIgnoreCollider(hit.collider, ignoredTarget))
            {
                continue;
            }

            if (hit.distance < closestGroundDistance)
            {
                closestGroundDistance = hit.distance;
                minimumY = hit.point.y + anchorGroundClearance;
            }
        }

        if (!float.IsNegativeInfinity(minimumY) && anchorPosition.y < minimumY)
        {
            anchorPosition.y = minimumY;
        }

        if (ceilingProbeHeight > 0f)
        {
            float probeRadius = Mathf.Max(0.01f, cameraRadius * ceilingProbeRadiusScale);
            int ceilingHitCount = Physics.SphereCastNonAlloc(
                anchorPosition,
                probeRadius,
                Vector3.up,
                obstructionHits,
                ceilingProbeHeight,
                collisionMask,
                QueryTriggerInteraction.Ignore);

            float closestCeilingDistance = float.PositiveInfinity;
            for (int i = 0; i < ceilingHitCount; i++)
            {
                RaycastHit hit = obstructionHits[i];
                if (ShouldIgnoreCollider(hit.collider, ignoredTarget))
                {
                    continue;
                }

                if (hit.distance < closestCeilingDistance)
                {
                    closestCeilingDistance = hit.distance;
                }
            }

            if (!float.IsPositiveInfinity(closestCeilingDistance))
            {
                float maximumY = anchorPosition.y + closestCeilingDistance - ceilingClearance;
                anchorPosition.y = Mathf.Min(anchorPosition.y, maximumY);

                if (!float.IsNegativeInfinity(minimumY))
                {
                    anchorPosition.y = Mathf.Max(anchorPosition.y, minimumY);
                }
            }
        }

        return anchorPosition;
    }

    private bool ShouldIgnoreCollider(Collider collider, Transform ignoredTarget)
    {
        if (collider == null)
        {
            return true;
        }

        return ignoreTargetColliders
            && ignoredTarget != null
            && collider.transform.IsChildOf(ignoredTarget);
    }
}
