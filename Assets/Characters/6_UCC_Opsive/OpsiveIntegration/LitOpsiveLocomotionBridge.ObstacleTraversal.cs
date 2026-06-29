using System.Collections;
using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    private const int ObstacleTraversalHitCapacity = 8;

    [Header("Obstacle Traversal")]
    [SerializeField, Tooltip("Active le franchissement automatique des obstacles bas pendant la locomotion UCC.")]
    private bool enableObstacleTraversal = true;
    [SerializeField, Min(0f), Tooltip("Les obstacles plus bas que cette hauteur sont ignores par le franchissement.")]
    private float ignoredObstacleMaxHeight = 0.18f;
    [SerializeField, Min(0f), Tooltip("Hauteur maximale d'un obstacle franchissable avec animation.")]
    private float traversableObstacleMaxHeight = 0.9f;
    [SerializeField, Min(0.05f), Tooltip("Distance de detection devant le personnage.")]
    private float obstacleProbeDistance = 0.75f;
    [SerializeField, Min(0.01f), Tooltip("Rayon du probe horizontal utilise pour detecter l'obstacle.")]
    private float obstacleProbeRadius = 0.22f;
    [SerializeField, Min(0f), Tooltip("Hauteur du probe horizontal au-dessus des pieds du personnage.")]
    private float obstacleProbeBaseHeight = 0.25f;
    [SerializeField, Range(0f, 1f), Tooltip("Ignore les surfaces progressives dont la normale pointe trop vers le haut. 0 accepte seulement les faces verticales, 1 accepte aussi les rampes.")]
    private float obstacleTraversalMaxSurfaceUpDot = 0.35f;
    [SerializeField, Min(0f), Tooltip("Distance horizontale ajoutee derriere l'obstacle pour poser le personnage.")]
    private float obstacleLandingDistance = 0.55f;
    [SerializeField, Min(0.01f), Tooltip("Duree du franchissement scripte.")]
    private float obstacleTraversalDuration = 0.42f;
    [SerializeField, Min(0f), Tooltip("Arc vertical ajoute pendant le franchissement.")]
    private float obstacleTraversalArcHeight = 0.25f;
    [SerializeField, Tooltip("Layers consideres comme obstacles franchissables.")]
    private LayerMask obstacleTraversalMask = ~0;
    [SerializeField, Tooltip("Trigger Animator optionnel lance au debut du franchissement.")]
    private string obstacleTraversalTriggerParam = "ObstacleTraversal";

    private readonly RaycastHit[] obstacleTraversalHits = new RaycastHit[ObstacleTraversalHitCapacity];
    private Coroutine obstacleTraversalRoutine;
    private bool obstacleTraversalOwnsScriptedLock;

    private bool TryStartObstacleTraversal()
    {
        if (!enableObstacleTraversal ||
            obstacleTraversalRoutine != null ||
            !IsDriving ||
            IsInputSuppressedByUcc ||
            IsFlightActive ||
            locomotion == null ||
            !locomotion.Grounded ||
            currentWorldMoveInput.sqrMagnitude <= movementDeadZone * movementDeadZone)
        {
            return false;
        }

        Vector3 direction = new Vector3(currentWorldMoveInput.x, 0f, currentWorldMoveInput.y);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        direction.Normalize();
        if (!TryResolveObstacleTraversal(direction, out Vector3 targetPosition, out Quaternion targetRotation))
        {
            return false;
        }

        obstacleTraversalRoutine = StartCoroutine(ObstacleTraversalRoutine(targetPosition, targetRotation));
        return true;
    }

    private bool TryResolveObstacleTraversal(Vector3 direction, out Vector3 targetPosition, out Quaternion targetRotation)
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;

        float footY = ResolveObstacleTraversalFootY();
        Vector3 up = transform.up;
        Vector3 origin = transform.position + up * Mathf.Max(0f, obstacleProbeBaseHeight);
        float probeDistance = Mathf.Max(0.05f, obstacleProbeDistance);
        float probeRadius = Mathf.Max(0.01f, obstacleProbeRadius);
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            probeRadius,
            direction,
            obstacleTraversalHits,
            probeDistance,
            obstacleTraversalMask,
            QueryTriggerInteraction.Ignore);

        if (!TryFindClosestTraversalHit(hitCount, footY, direction, out RaycastHit obstacleHit, out float obstacleHeight))
        {
            return false;
        }

        float landingDistance = Mathf.Max(0f, obstacleLandingDistance);
        float travelDistance = Mathf.Max(obstacleHit.distance + probeRadius + landingDistance, landingDistance);
        Vector3 candidate = transform.position + direction * travelDistance;
        candidate.y = ResolveObstacleTraversalLandingFootY(candidate, footY) + (transform.position.y - footY);

        if (!HasTraversalHeadClearance(origin, direction, travelDistance, obstacleHeight))
        {
            return false;
        }

        targetPosition = candidate;
        targetRotation = direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction, up)
            : transform.rotation;
        return true;
    }

    private bool TryFindClosestTraversalHit(
        int hitCount,
        float footY,
        Vector3 direction,
        out RaycastHit closestHit,
        out float obstacleHeight)
    {
        closestHit = default;
        obstacleHeight = 0f;
        float closestDistance = float.PositiveInfinity;
        float ignoreHeight = Mathf.Max(0f, ignoredObstacleMaxHeight);
        float maxHeight = Mathf.Max(ignoreHeight, traversableObstacleMaxHeight);
        float maxSurfaceUpDot = Mathf.Clamp01(obstacleTraversalMaxSurfaceUpDot);
        Vector3 up = transform.up;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = obstacleTraversalHits[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null || IsOwnObstacleTraversalCollider(hitCollider))
            {
                continue;
            }

            if (Vector3.Dot(hit.normal, direction) > -0.15f)
            {
                continue;
            }

            if (Vector3.Dot(hit.normal, up) > maxSurfaceUpDot)
            {
                continue;
            }

            float height = hitCollider.bounds.max.y - footY;
            if (height <= ignoreHeight || height > maxHeight)
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                obstacleHeight = height;
            }
        }

        return closestDistance < float.PositiveInfinity;
    }

    private bool HasTraversalHeadClearance(Vector3 baseOrigin, Vector3 direction, float travelDistance, float obstacleHeight)
    {
        float probeRadius = Mathf.Max(0.01f, obstacleProbeRadius);
        float clearanceHeight = Mathf.Max(obstacleHeight + probeRadius + 0.15f, obstacleProbeBaseHeight + probeRadius * 2f);
        Vector3 highOrigin = new Vector3(baseOrigin.x, ResolveObstacleTraversalFootY() + clearanceHeight, baseOrigin.z);
        int hitCount = Physics.SphereCastNonAlloc(
            highOrigin,
            probeRadius,
            direction,
            obstacleTraversalHits,
            Mathf.Max(0.05f, travelDistance),
            obstacleTraversalMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = obstacleTraversalHits[i].collider;
            if (hitCollider != null && !IsOwnObstacleTraversalCollider(hitCollider))
            {
                return false;
            }
        }

        return true;
    }

    private float ResolveObstacleTraversalLandingFootY(Vector3 candidate, float fallbackY)
    {
        Vector3 rayOrigin = candidate + transform.up * (Mathf.Max(ignoredObstacleMaxHeight, traversableObstacleMaxHeight) + 0.5f);
        float rayDistance = Mathf.Max(0.6f, traversableObstacleMaxHeight + 1f);
        if (Physics.Raycast(
                rayOrigin,
                -transform.up,
                out RaycastHit hit,
                rayDistance,
                obstacleTraversalMask,
                QueryTriggerInteraction.Ignore) &&
            hit.collider != null &&
            !IsOwnObstacleTraversalCollider(hit.collider))
        {
            return hit.point.y;
        }

        return fallbackY;
    }

    private float ResolveObstacleTraversalFootY()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            return capsule.bounds.min.y;
        }

        return transform.position.y;
    }

    private bool IsOwnObstacleTraversalCollider(Collider hitCollider)
    {
        return hitCollider != null && hitCollider.transform.IsChildOf(transform);
    }

    private IEnumerator ObstacleTraversalRoutine(Vector3 targetPosition, Quaternion targetRotation)
    {
        obstacleTraversalOwnsScriptedLock = false;
        if (!BeginScriptedTraversal())
        {
            obstacleTraversalRoutine = null;
            yield break;
        }

        obstacleTraversalOwnsScriptedLock = true;
        SetAnimatorTrigger(obstacleTraversalTriggerParam);

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float duration = Mathf.Max(0.01f, obstacleTraversalDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 position = Vector3.Lerp(startPosition, targetPosition, t);
            position += transform.up * (Mathf.Sin(t * Mathf.PI) * Mathf.Max(0f, obstacleTraversalArcHeight));
            ApplyScriptedTraversalPose(position, Quaternion.Slerp(startRotation, targetRotation, t));

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        ApplyScriptedTraversalPose(targetPosition, targetRotation);
        EndScriptedTraversal();
        obstacleTraversalOwnsScriptedLock = false;
        obstacleTraversalRoutine = null;
    }

    private void CancelObstacleTraversal()
    {
        if (obstacleTraversalRoutine != null)
        {
            StopCoroutine(obstacleTraversalRoutine);
            obstacleTraversalRoutine = null;
        }

        if (obstacleTraversalOwnsScriptedLock)
        {
            EndScriptedTraversal();
            obstacleTraversalOwnsScriptedLock = false;
        }
    }
}
