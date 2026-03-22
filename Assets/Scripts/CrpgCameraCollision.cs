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
    [SerializeField] private bool ignoreTargetColliders = true;

    [Header("Relief")]
    [SerializeField] private float groundProbeUp = 8f;
    [SerializeField] private float groundProbeDown = 24f;
    [SerializeField] private float anchorGroundClearance = 0.5f;

    private readonly RaycastHit[] obstructionHits = new RaycastHit[16];

    public LayerMask CollisionMask => collisionMask;

    public void Validate()
    {
        cameraRadius = Mathf.Max(0.01f, cameraRadius);
        collisionBuffer = Mathf.Max(0f, collisionBuffer);
        minCollisionDistance = Mathf.Max(0.1f, minCollisionDistance);
        groundProbeUp = Mathf.Max(0.1f, groundProbeUp);
        groundProbeDown = Mathf.Max(0.1f, groundProbeDown);
        anchorGroundClearance = Mathf.Max(0f, anchorGroundClearance);
    }

    public SolveResult Solve(Vector3 desiredAnchorPosition, Quaternion rigRotation, float desiredDistance, Transform ignoredTarget)
    {
        Vector3 anchor = ResolveAnchorHeight(desiredAnchorPosition, ignoredTarget);
        Vector3 toCameraDirection = rigRotation * Vector3.back;
        float allowedDistance = Mathf.Max(minCollisionDistance, desiredDistance);

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
                if (hit.collider == null)
                {
                    continue;
                }

                if (ignoreTargetColliders && ignoredTarget != null && hit.collider.transform.IsChildOf(ignoredTarget))
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
                allowedDistance = Mathf.Max(minCollisionDistance, closest - collisionBuffer);
            }
        }

        return new SolveResult
        {
            anchorPosition = anchor,
            allowedDistance = allowedDistance,
            obstructed = allowedDistance + 0.001f < desiredDistance
        };
    }

    private Vector3 ResolveAnchorHeight(Vector3 anchorPosition, Transform ignoredTarget)
    {
        Vector3 origin = anchorPosition + Vector3.up * groundProbeUp;
        float maxDistance = groundProbeUp + groundProbeDown;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            return anchorPosition;
        }

        if (ignoreTargetColliders && ignoredTarget != null && hit.collider != null && hit.collider.transform.IsChildOf(ignoredTarget))
        {
            return anchorPosition;
        }

        float minimumY = hit.point.y + anchorGroundClearance;
        if (anchorPosition.y < minimumY)
        {
            anchorPosition.y = minimumY;
        }

        return anchorPosition;
    }
}
