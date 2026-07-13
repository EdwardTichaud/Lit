using System.Collections;
using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    private const int ObstacleTraversalHitCapacity = 8;
    private const int ObstacleTraversalOverlapCapacity = 8;
    private const float ObstacleTraversalMinOpposingNormalDot = 0.45f;
    private const float ObstacleTraversalTopNormalMinUpDot = 0.5f;
    private const float ObstacleTraversalStepHeightTolerance = 0.02f;

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
    [SerializeField, Min(0f), Tooltip("Marge ajoutee au-dessus de l'obstacle pour eviter un franchissement trop plat.")]
    private float obstacleTraversalTopClearance = 0.16f;
    [SerializeField, Range(0f, 1f), Tooltip("Ajoute une part de la hauteur de l'obstacle a l'arc de franchissement.")]
    private float obstacleTraversalHeightArcMultiplier = 0.38f;
    [SerializeField, Range(0.1f, 1f), Tooltip("Fraction du franchissement utilisee pour terminer la rotation vers la direction cible.")]
    private float obstacleTraversalRotationLead = 0.68f;
    [SerializeField, Range(0f, 1f), Tooltip("Magnitude d'input minimale avant de declencher un franchissement automatique.")]
    private float obstacleTraversalMinInputMagnitude = 0.34f;
    [SerializeField, Min(0f), Tooltip("Delai anti-redeclenchement apres un franchissement.")]
    private float obstacleTraversalCooldown = 0.28f;
    [SerializeField, Tooltip("Layers consideres comme obstacles franchissables.")]
    private LayerMask obstacleTraversalMask = ~0;
    [SerializeField, Tooltip("Trigger Animator optionnel lance au debut du franchissement.")]
    private string obstacleTraversalTriggerParam = "ObstacleTraversal";

    private readonly RaycastHit[] obstacleTraversalHits = new RaycastHit[ObstacleTraversalHitCapacity];
    private readonly Collider[] obstacleTraversalOverlaps = new Collider[ObstacleTraversalOverlapCapacity];
    private Coroutine obstacleTraversalRoutine;
    private bool obstacleTraversalOwnsScriptedLock;
    private float lastObstacleTraversalTime = -999f;

    private void ValidateObstacleTraversalSettings()
    {
        ignoredObstacleMaxHeight = Mathf.Max(0f, ignoredObstacleMaxHeight);
        traversableObstacleMaxHeight = Mathf.Max(ignoredObstacleMaxHeight, traversableObstacleMaxHeight);
        obstacleProbeDistance = Mathf.Max(0.05f, obstacleProbeDistance);
        obstacleProbeRadius = Mathf.Max(0.01f, obstacleProbeRadius);
        obstacleProbeBaseHeight = Mathf.Max(0f, obstacleProbeBaseHeight);
        obstacleTraversalMaxSurfaceUpDot = Mathf.Clamp01(obstacleTraversalMaxSurfaceUpDot);
        obstacleLandingDistance = Mathf.Max(0f, obstacleLandingDistance);
        obstacleTraversalDuration = Mathf.Max(0.01f, obstacleTraversalDuration);
        obstacleTraversalArcHeight = Mathf.Max(0f, obstacleTraversalArcHeight);
        obstacleTraversalTopClearance = Mathf.Max(0f, obstacleTraversalTopClearance);
        obstacleTraversalHeightArcMultiplier = Mathf.Clamp01(obstacleTraversalHeightArcMultiplier);
        obstacleTraversalRotationLead = Mathf.Clamp(obstacleTraversalRotationLead, 0.1f, 1f);
        obstacleTraversalMinInputMagnitude = Mathf.Clamp01(obstacleTraversalMinInputMagnitude);
        obstacleTraversalCooldown = Mathf.Max(0f, obstacleTraversalCooldown);
    }

    private bool TryStartObstacleTraversal()
    {
        if (!enableObstacleTraversal ||
            obstacleTraversalRoutine != null ||
            !IsDriving ||
            IsInputSuppressedByUcc ||
            IsFlightActive ||
            locomotion == null ||
            !locomotion.Grounded ||
            Time.time - lastObstacleTraversalTime < Mathf.Max(0f, obstacleTraversalCooldown) ||
            currentWorldMoveInput.magnitude < Mathf.Max(movementDeadZone, obstacleTraversalMinInputMagnitude))
        {
            return false;
        }

        Vector3 direction = new Vector3(currentWorldMoveInput.x, 0f, currentWorldMoveInput.y);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        direction.Normalize();
        if (!TryResolveObstacleTraversal(direction, out ObstacleTraversalSolution solution))
        {
            return false;
        }

        obstacleTraversalRoutine = StartCoroutine(ObstacleTraversalRoutine(solution));
        return true;
    }

    private bool TryResolveObstacleTraversal(Vector3 direction, out ObstacleTraversalSolution solution)
    {
        solution = new ObstacleTraversalSolution(transform.position, transform.rotation, direction, 0f, 0f);

        float footY = ResolveObstacleTraversalFootY();
        Vector3 up = transform.up;
        Vector3 origin = transform.position + up * Mathf.Max(0f, obstacleProbeBaseHeight);
        float probeDistance = Mathf.Max(0.05f, obstacleProbeDistance);
        float probeRadius = Mathf.Max(0.01f, obstacleProbeRadius);
        int traversalMask = ResolveObstacleTraversalMask();
        if (traversalMask == 0)
        {
            return false;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            probeRadius,
            direction,
            obstacleTraversalHits,
            probeDistance,
            traversalMask,
            QueryTriggerInteraction.Ignore);

        if (!TryFindClosestTraversalHit(hitCount, footY, direction, traversalMask, out RaycastHit obstacleHit, out float obstacleHeight))
        {
            return false;
        }

        float landingDistance = Mathf.Max(0f, obstacleLandingDistance);
        float travelDistance = Mathf.Max(obstacleHit.distance + probeRadius + landingDistance, landingDistance);
        Vector3 candidate = transform.position + direction * travelDistance;
        candidate.y = ResolveObstacleTraversalLandingFootY(candidate, footY, traversalMask) + (transform.position.y - footY);

        if (!HasTraversalLandingClearance(candidate, traversalMask))
        {
            return false;
        }

        if (!HasTraversalHeadClearance(origin, direction, travelDistance, obstacleHeight, traversalMask))
        {
            return false;
        }

        Quaternion targetRotation = direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction, up)
            : transform.rotation;
        solution = new ObstacleTraversalSolution(candidate, targetRotation, direction, obstacleHeight, travelDistance);
        return true;
    }

    private bool TryFindClosestTraversalHit(
        int hitCount,
        float footY,
        Vector3 direction,
        int traversalMask,
        out RaycastHit closestHit,
        out float obstacleHeight)
    {
        closestHit = default;
        obstacleHeight = 0f;
        float closestDistance = float.PositiveInfinity;
        float ignoreHeight = ResolveObstacleTraversalIgnoreHeight();
        float maxHeight = Mathf.Max(ignoreHeight, traversableObstacleMaxHeight);
        float maxSurfaceUpDot = Mathf.Clamp01(obstacleTraversalMaxSurfaceUpDot);
        Vector3 up = transform.up;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = obstacleTraversalHits[i];
            Collider hitCollider = hit.collider;
            if (!IsValidObstacleTraversalCollider(hitCollider, traversalMask))
            {
                continue;
            }

            if (Vector3.Dot(hit.normal, direction) > -ObstacleTraversalMinOpposingNormalDot)
            {
                continue;
            }

            if (Vector3.Dot(hit.normal, up) > maxSurfaceUpDot)
            {
                continue;
            }

            if (!TryResolveTraversalSurfaceHeight(hit, footY, direction, ignoreHeight, maxHeight, traversalMask, out float height))
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

    private bool TryResolveTraversalSurfaceHeight(
        RaycastHit obstacleHit,
        float footY,
        Vector3 direction,
        float ignoreHeight,
        float maxHeight,
        int traversalMask,
        out float obstacleHeight)
    {
        obstacleHeight = 0f;

        Vector3 up = transform.up;
        float forwardOffset = Mathf.Max(0.03f, obstacleProbeRadius * 0.5f);
        Vector3 topProbeOrigin = obstacleHit.point + direction * forwardOffset;
        topProbeOrigin.y = footY + maxHeight + 0.2f;
        float topProbeDistance = maxHeight + 0.35f;

        if (!Physics.Raycast(
                topProbeOrigin,
                -up,
                out RaycastHit topHit,
                topProbeDistance,
                traversalMask,
                QueryTriggerInteraction.Ignore) ||
            !IsValidObstacleTraversalCollider(topHit.collider, traversalMask) ||
            topHit.collider != obstacleHit.collider)
        {
            return false;
        }

        if (Vector3.Dot(topHit.normal, up) < ObstacleTraversalTopNormalMinUpDot)
        {
            return false;
        }

        obstacleHeight = topHit.point.y - footY;
        return obstacleHeight > ignoreHeight && obstacleHeight <= maxHeight;
    }

    private bool HasTraversalLandingClearance(Vector3 candidate, int traversalMask)
    {
        Vector3 center = candidate + transform.up * Mathf.Max(0f, obstacleProbeBaseHeight);
        int overlapCount = Physics.OverlapSphereNonAlloc(
            center,
            Mathf.Max(0.01f, obstacleProbeRadius),
            obstacleTraversalOverlaps,
            traversalMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = obstacleTraversalOverlaps[i];
            if (IsValidObstacleTraversalCollider(overlap, traversalMask, excludeInteractiveBlockers: false))
            {
                return false;
            }
        }

        return true;
    }

    private bool HasTraversalHeadClearance(Vector3 baseOrigin, Vector3 direction, float travelDistance, float obstacleHeight, int traversalMask)
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
            traversalMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = obstacleTraversalHits[i].collider;
            if (IsValidObstacleTraversalCollider(hitCollider, traversalMask, excludeInteractiveBlockers: false))
            {
                return false;
            }
        }

        return true;
    }

    private float ResolveObstacleTraversalLandingFootY(Vector3 candidate, float fallbackY, int traversalMask)
    {
        Vector3 rayOrigin = candidate + transform.up * (Mathf.Max(ignoredObstacleMaxHeight, traversableObstacleMaxHeight) + 0.5f);
        float rayDistance = Mathf.Max(0.6f, traversableObstacleMaxHeight + 1f);
        if (Physics.Raycast(
                rayOrigin,
                -transform.up,
                out RaycastHit hit,
                rayDistance,
                traversalMask,
                QueryTriggerInteraction.Ignore) &&
            IsValidObstacleTraversalCollider(hit.collider, traversalMask))
        {
            return hit.point.y;
        }

        return fallbackY;
    }

    private float ResolveObstacleTraversalFootY()
    {
        Collider[] locomotionColliders = locomotion != null ? locomotion.Colliders : null;
        int locomotionColliderCount = locomotion != null ? locomotion.ColliderCount : 0;
        float footY = float.PositiveInfinity;
        bool foundLocomotionCollider = false;
        for (int i = 0; locomotionColliders != null && i < locomotionColliderCount && i < locomotionColliders.Length; i++)
        {
            Collider collider = locomotionColliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            footY = Mathf.Min(footY, collider.bounds.min.y);
            foundLocomotionCollider = true;
        }

        if (foundLocomotionCollider)
        {
            return footY;
        }

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            return capsule.bounds.min.y;
        }

        return transform.position.y;
    }

    private int ResolveObstacleTraversalMask()
    {
        int mask = obstacleTraversalMask.value;
        if (locomotion != null)
        {
            mask &= locomotion.ColliderLayerMask.value;
        }

        return mask;
    }

    private float ResolveObstacleTraversalIgnoreHeight()
    {
        float ignoreHeight = Mathf.Max(0f, ignoredObstacleMaxHeight);
        if (locomotion == null)
        {
            return ignoreHeight;
        }

        float uccStepHeight = Mathf.Max(0f, locomotion.MaxStepHeight - ObstacleTraversalStepHeightTolerance);
        return Mathf.Min(ignoreHeight, uccStepHeight);
    }

    private bool IsValidObstacleTraversalCollider(Collider hitCollider, int traversalMask, bool excludeInteractiveBlockers = true)
    {
        if (hitCollider == null ||
            !hitCollider.enabled ||
            hitCollider.isTrigger ||
            IsOwnObstacleTraversalCollider(hitCollider))
        {
            return false;
        }

        if ((traversalMask & (1 << hitCollider.gameObject.layer)) == 0)
        {
            return false;
        }

        return !excludeInteractiveBlockers || !IsObstacleTraversalInteractiveBlocker(hitCollider);
    }

    private static bool IsObstacleTraversalInteractiveBlocker(Collider hitCollider)
    {
        return hitCollider != null && hitCollider.GetComponentInParent<Door>() != null;
    }

    private bool IsOwnObstacleTraversalCollider(Collider hitCollider)
    {
        return hitCollider != null && hitCollider.transform.IsChildOf(transform);
    }

    private IEnumerator ObstacleTraversalRoutine(ObstacleTraversalSolution solution)
    {
        obstacleTraversalOwnsScriptedLock = false;
        if (!BeginScriptedTraversal())
        {
            obstacleTraversalRoutine = null;
            yield break;
        }

        obstacleTraversalOwnsScriptedLock = true;
        SetAnimatorTrigger(obstacleTraversalTriggerParam);
        if (orientLookSourceFromMovement && lookSource != null && solution.direction.sqrMagnitude > 0.0001f)
        {
            lookSource.SetPlanarLookDirection(solution.direction);
        }

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float duration = ResolveObstacleTraversalDuration(solution.travelDistance);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float rawT = Mathf.Clamp01(elapsed / duration);
            float easedT = EaseObstacleTraversalTime(rawT);
            Vector3 position = ResolveObstacleTraversalPosition(startPosition, solution, easedT);
            Quaternion rotation = ResolveObstacleTraversalRotation(startRotation, solution.targetRotation, rawT);
            ApplyScriptedTraversalPose(position, rotation);

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        ApplyScriptedTraversalPose(solution.targetPosition, solution.targetRotation);
        EndScriptedTraversal();
        lastObstacleTraversalTime = Time.time;
        obstacleTraversalOwnsScriptedLock = false;
        obstacleTraversalRoutine = null;
    }

    private Vector3 ResolveObstacleTraversalPosition(Vector3 startPosition, ObstacleTraversalSolution solution, float t)
    {
        Vector3 position = Vector3.Lerp(startPosition, solution.targetPosition, t);
        float arcHeight = ResolveObstacleTraversalArcHeight(solution.obstacleHeight);
        position += transform.up * (Mathf.Sin(t * Mathf.PI) * arcHeight);
        return position;
    }

    private float ResolveObstacleTraversalDuration(float travelDistance)
    {
        float referenceDistance = Mathf.Max(0.1f, obstacleProbeDistance + obstacleLandingDistance);
        float distanceScale = Mathf.Clamp(travelDistance / referenceDistance, 0.85f, 1.18f);
        return Mathf.Max(0.01f, obstacleTraversalDuration * distanceScale);
    }

    private float ResolveObstacleTraversalArcHeight(float obstacleHeight)
    {
        float heightDrivenArc = Mathf.Max(0f, obstacleHeight) * Mathf.Clamp01(obstacleTraversalHeightArcMultiplier) +
                                Mathf.Max(0f, obstacleTraversalTopClearance);
        return Mathf.Max(Mathf.Max(0f, obstacleTraversalArcHeight), heightDrivenArc);
    }

    private Quaternion ResolveObstacleTraversalRotation(Quaternion startRotation, Quaternion targetRotation, float rawT)
    {
        float lead = Mathf.Clamp(obstacleTraversalRotationLead, 0.1f, 1f);
        float rotationT = EaseObstacleTraversalTime(Mathf.Clamp01(rawT / lead));
        return Quaternion.Slerp(startRotation, targetRotation, rotationT);
    }

    private static float EaseObstacleTraversalTime(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
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

    private struct ObstacleTraversalSolution
    {
        public readonly Vector3 targetPosition;
        public readonly Quaternion targetRotation;
        public readonly Vector3 direction;
        public readonly float obstacleHeight;
        public readonly float travelDistance;

        public ObstacleTraversalSolution(
            Vector3 targetPosition,
            Quaternion targetRotation,
            Vector3 direction,
            float obstacleHeight,
            float travelDistance)
        {
            this.targetPosition = targetPosition;
            this.targetRotation = targetRotation;
            this.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
            this.obstacleHeight = obstacleHeight;
            this.travelDistance = travelDistance;
        }
    }
}
