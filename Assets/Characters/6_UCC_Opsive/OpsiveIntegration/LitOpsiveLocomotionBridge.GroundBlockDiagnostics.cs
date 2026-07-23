using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
#if UNITY_EDITOR
    private const int GroundBlockDiagnosticHitCapacity = 8;
    private const int GroundBlockDiagnosticOverlapCapacity = 8;
    private const float GroundBlockDiagnosticInputAngleReset = 35f;

    [Header("Obstacle Traversal Debug")]
    [SerializeField, Tooltip("Editor-only: logs the collider in front of the grounded character when movement input is applied but UCC barely moves.")]
    private bool debugGroundBlockDiagnostics;
    [SerializeField, Min(0.1f), Tooltip("Minimum time between grounded block diagnostic samples.")]
    private float groundBlockDiagnosticInterval = 0.75f;
    [SerializeField, Min(0.001f), Tooltip("Minimum forward progress over the sample window before the character is considered unblocked.")]
    private float groundBlockDiagnosticMinProgress = 0.04f;

    private readonly RaycastHit[] groundBlockDiagnosticHits = new RaycastHit[GroundBlockDiagnosticHitCapacity];
    private readonly Collider[] groundBlockDiagnosticOverlaps = new Collider[GroundBlockDiagnosticOverlapCapacity];
    private Vector3 groundBlockDiagnosticLastPosition;
    private Vector2 groundBlockDiagnosticLastInput;
    private float groundBlockDiagnosticLastSampleTime;
    private float groundBlockDiagnosticLastLogTime;
    private bool groundBlockDiagnosticHasSample;

    /// <summary>
    /// Active ponctuellement le diagnostic de collision lors de l'arrivée dans
    /// une zone. Il n'est utilisé que dans l'éditeur et n'a aucun effet dans un build.
    /// </summary>
    public void SetGroundBlockDiagnosticsEnabled(bool enabled)
    {
        debugGroundBlockDiagnostics = enabled;
        ResetGroundBlockDiagnosticSample();
    }
#endif

    private void TickGroundBlockDiagnostics()
    {
#if UNITY_EDITOR
        if (!debugGroundBlockDiagnostics &&
            Application.isPlaying &&
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("District_1") &&
            SquadManager.Instance != null &&
            SquadManager.Instance.currentCharacter == gameObject)
        {
            // District_1 est actuellement la seule zone en diagnostic : activer
            // automatiquement la trace sur le personnage effectivement pilote.
            debugGroundBlockDiagnostics = true;
        }

        if (!debugGroundBlockDiagnostics ||
            !Application.isPlaying ||
            !IsDriving ||
            IsInputSuppressedByUcc ||
            IsFlightActive ||
            locomotion == null ||
            !locomotion.Grounded ||
            currentWorldMoveInput.sqrMagnitude <= movementDeadZone * movementDeadZone)
        {
            ResetGroundBlockDiagnosticSample();
            return;
        }

        float now = Time.unscaledTime;
        if (!groundBlockDiagnosticHasSample ||
            Vector2.Angle(groundBlockDiagnosticLastInput, currentWorldMoveInput) > GroundBlockDiagnosticInputAngleReset)
        {
            SetGroundBlockDiagnosticSample(now);
            return;
        }

        float interval = Mathf.Max(0.1f, groundBlockDiagnosticInterval);
        float elapsed = now - groundBlockDiagnosticLastSampleTime;
        if (elapsed < interval)
        {
            return;
        }

        Vector3 direction = new Vector3(currentWorldMoveInput.x, 0f, currentWorldMoveInput.y);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            ResetGroundBlockDiagnosticSample();
            return;
        }

        direction.Normalize();
        Vector3 planarDelta = Vector3.ProjectOnPlane(transform.position - groundBlockDiagnosticLastPosition, transform.up);
        float forwardProgress = Vector3.Dot(planarDelta, direction);
        if (forwardProgress < Mathf.Max(0.001f, groundBlockDiagnosticMinProgress) &&
            now - groundBlockDiagnosticLastLogTime >= interval)
        {
            groundBlockDiagnosticLastLogTime = now;
            LogGroundBlockDiagnostic(direction, forwardProgress, planarDelta.magnitude, elapsed);
        }

        SetGroundBlockDiagnosticSample(now);
#endif
    }

#if UNITY_EDITOR
    private void SetGroundBlockDiagnosticSample(float now)
    {
        groundBlockDiagnosticLastPosition = transform.position;
        groundBlockDiagnosticLastInput = currentWorldMoveInput;
        groundBlockDiagnosticLastSampleTime = now;
        groundBlockDiagnosticHasSample = true;
    }

    private void ResetGroundBlockDiagnosticSample()
    {
        groundBlockDiagnosticHasSample = false;
    }

    private void LogGroundBlockDiagnostic(Vector3 direction, float forwardProgress, float planarDistance, float elapsed)
    {
        string blocker = TryFindGroundBlockDiagnosticHit(direction, out RaycastHit hit)
            ? FormatGroundBlockDiagnosticHit(hit)
            : TryFindGroundBlockDiagnosticOverlap(out Collider overlap)
                ? FormatGroundBlockDiagnosticCollider(overlap, "overlap")
                : "none";

        string activeAbilities = ResolveActiveAbilityLabel();
        Debug.LogWarning(
            $"[Lit/UCC GroundBlock] character='{name}' grounded={locomotion.Grounded} " +
            $"input=({currentWorldMoveInput.x:F2},{currentWorldMoveInput.y:F2}) " +
            $"forwardProgress={forwardProgress:F3}m planarDistance={planarDistance:F3}m sample={elapsed:F2}s " +
            $"velocity={locomotion.Velocity.magnitude:F2} activeAbilities='{activeAbilities}' blocker={blocker}",
            this);
    }

    private bool TryFindGroundBlockDiagnosticHit(Vector3 direction, out RaycastHit closestHit)
    {
        closestHit = default;
        if (!TryResolveGroundBlockDiagnosticCapsule(out Vector3 point1, out Vector3 point2, out float radius))
        {
            return false;
        }

        int mask = ResolveObstacleTraversalMask();
        if (mask == 0)
        {
            return false;
        }

        float distance = Mathf.Max(0.3f, obstacleProbeDistance + obstacleProbeRadius);
        int hitCount = Physics.CapsuleCastNonAlloc(
            point1,
            point2,
            radius,
            direction,
            groundBlockDiagnosticHits,
            distance,
            mask,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundBlockDiagnosticHits[i];
            if (!IsGroundBlockDiagnosticCollider(hit.collider, mask))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
            }
        }

        return closestDistance < float.PositiveInfinity;
    }

    private bool TryFindGroundBlockDiagnosticOverlap(out Collider overlap)
    {
        overlap = null;
        if (!TryResolveGroundBlockDiagnosticCapsule(out Vector3 point1, out Vector3 point2, out float radius))
        {
            return false;
        }

        int mask = ResolveObstacleTraversalMask();
        if (mask == 0)
        {
            return false;
        }

        int overlapCount = Physics.OverlapCapsuleNonAlloc(
            point1,
            point2,
            radius,
            groundBlockDiagnosticOverlaps,
            mask,
            QueryTriggerInteraction.Ignore);

        float closestSqrDistance = float.PositiveInfinity;
        Vector3 position = transform.position;
        for (int i = 0; i < overlapCount; i++)
        {
            Collider candidate = groundBlockDiagnosticOverlaps[i];
            if (!IsGroundBlockDiagnosticCollider(candidate, mask))
            {
                continue;
            }

            float sqrDistance = (candidate.bounds.center - position).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                overlap = candidate;
            }
        }

        return overlap != null;
    }

    private bool TryResolveGroundBlockDiagnosticCapsule(out Vector3 point1, out Vector3 point2, out float radius)
    {
        CapsuleCollider capsule = ResolveGroundBlockDiagnosticCapsule();
        if (capsule == null)
        {
            point1 = transform.position + transform.up * obstacleProbeBaseHeight;
            point2 = point1;
            radius = Mathf.Max(0.1f, obstacleProbeRadius);
            return true;
        }

        Vector3 scale = capsule.transform.lossyScale;
        float verticalScale = Mathf.Abs(scale.y);
        float radialScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        radius = Mathf.Max(0.01f, capsule.radius * radialScale);
        float height = Mathf.Max(radius * 2f, capsule.height * verticalScale);
        Vector3 center = capsule.transform.TransformPoint(capsule.center);
        Vector3 up = capsule.transform.up.sqrMagnitude > 0f ? capsule.transform.up.normalized : transform.up;
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        point1 = center + up * halfSegment;
        point2 = center - up * halfSegment;
        return true;
    }

    private CapsuleCollider ResolveGroundBlockDiagnosticCapsule()
    {
        Collider[] locomotionColliders = locomotion != null ? locomotion.Colliders : null;
        int locomotionColliderCount = locomotion != null ? locomotion.ColliderCount : 0;
        for (int i = 0; locomotionColliders != null && i < locomotionColliderCount && i < locomotionColliders.Length; i++)
        {
            if (locomotionColliders[i] is CapsuleCollider capsule &&
                capsule.enabled &&
                !capsule.isTrigger &&
                capsule.gameObject.activeInHierarchy)
            {
                return capsule;
            }
        }

        CapsuleCollider fallback = GetComponent<CapsuleCollider>();
        if (fallback != null && fallback.enabled && !fallback.isTrigger && fallback.gameObject.activeInHierarchy)
        {
            return fallback;
        }

        return GetComponentInChildren<CapsuleCollider>();
    }

    private bool IsGroundBlockDiagnosticCollider(Collider candidate, int mask)
    {
        return candidate != null &&
               candidate.enabled &&
               !candidate.isTrigger &&
               !IsOwnObstacleTraversalCollider(candidate) &&
               (mask & (1 << candidate.gameObject.layer)) != 0;
    }

    private string FormatGroundBlockDiagnosticHit(RaycastHit hit)
    {
        string colliderInfo = FormatGroundBlockDiagnosticCollider(hit.collider, "cast");
        return $"{colliderInfo} hitDistance={hit.distance:F3} hitPoint={FormatVector(hit.point)} normal={FormatVector(hit.normal)}";
    }

    private static string FormatGroundBlockDiagnosticCollider(Collider collider, string mode)
    {
        if (collider == null)
        {
            return $"{mode}:null";
        }

        string layerName = LayerMask.LayerToName(collider.gameObject.layer);
        if (string.IsNullOrEmpty(layerName))
        {
            layerName = collider.gameObject.layer.ToString();
        }

        Door door = collider.GetComponentInParent<Door>();
        string doorInfo = door != null ? $" doorOpen={door.IsOpen} doorLocked={door.IsLocked}" : string.Empty;
        ICharacterDetectedInteractable interactable = CharacterInteractionDetection.ResolveTarget(collider);
        string interactableInfo = interactable != null ? $" interactable={interactable.GetType().Name}" : string.Empty;
        Bounds bounds = collider.bounds;
        return $"{mode}: collider='{collider.name}' type={collider.GetType().Name} layer='{layerName}' " +
               $"path='{ResolveGroundBlockDiagnosticPath(collider.transform)}' boundsCenter={FormatVector(bounds.center)} " +
               $"boundsSize={FormatVector(bounds.size)}{doorInfo}{interactableInfo}";
    }

    private static string ResolveGroundBlockDiagnosticPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:F2},{value.y:F2},{value.z:F2})";
    }
#endif
}
