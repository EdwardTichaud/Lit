using System.Collections.Generic;
using UnityEngine;

// Runtime partage pour la previsualisation et la validation de pose en monde.
public static class WorldPlacementUtility
{
    private const float PlacementPenetrationTolerance = 0.005f;

    public struct Settings
    {
        public float placementRadius;
        public float placementStartDistance;
        public bool placementUseCameraRelative;
        public Camera placementCamera;
        public bool placementSnapToGround;
        public LayerMask placementGroundMask;
        public float placementGroundRaycastHeight;
        public float placementGroundRaycastDistance;
        public float placementGroundOffset;
        public LayerMask placementCollisionMask;
        public LayerMask placementIgnoreMask;
        public bool placementBlockTriggers;
        public float placementBoundsPadding;
        public bool placementShowValidity;
        public Color placementValidColor;
        public Color placementInvalidColor;
        public float wallProbeHeight;
        public float wallProbeRadius;
        public float wallNormalMaxY;
        public float horizontalPlacementMaxSlopeAngle;
    }

    public sealed class PreviewCaches
    {
        internal readonly List<RigidbodyState> rigidbodyStates = new List<RigidbodyState>();
        internal Collider[] colliders;
        internal readonly List<RendererState> renderers = new List<RendererState>();
        internal MaterialPropertyBlock propertyBlock;
        internal bool lastValid;

        internal readonly struct RigidbodyState
        {
            public RigidbodyState(Rigidbody body, bool wasKinematic, bool usedGravity)
            {
                Body = body;
                WasKinematic = wasKinematic;
                UsedGravity = usedGravity;
            }

            public Rigidbody Body { get; }
            public bool WasKinematic { get; }
            public bool UsedGravity { get; }
        }

        internal readonly struct RendererState
        {
            public RendererState(Renderer renderer, string colorProperty)
            {
                Renderer = renderer;
                ColorProperty = colorProperty;
            }

            public Renderer Renderer { get; }
            public string ColorProperty { get; }
        }
    }

    public static Vector3 GetPlacementStartPosition(Transform anchor, Item item, Settings settings)
    {
        if (anchor == null)
        {
            return Vector3.zero;
        }

        Vector3 forward = ResolvePlacementForward(anchor, settings);
        Vector3 startPos = anchor.position + forward * Mathf.Max(0f, settings.placementStartDistance);
        return ClampPositionAroundAnchor(anchor, item, startPos, settings);
    }

    public static Vector3 ClampPositionAroundAnchor(Transform anchor, Item item, Vector3 position, Settings settings)
    {
        if (anchor == null || item == null || item.isBuilding)
        {
            return position;
        }

        float radius = item.GetPlacementRadius(settings.placementRadius);
        Vector3 offset = position - anchor.position;
        offset.y = 0f;
        if (offset.magnitude > radius)
        {
            offset = offset.normalized * radius;
            position = new Vector3(
                anchor.position.x + offset.x,
                position.y,
                anchor.position.z + offset.z);
        }

        return position;
    }

    public static Vector3 GetPlacementMoveDirection(Vector2 input, Settings settings)
    {
        if (input.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        Camera cam = ResolvePlacementCamera(settings);
        if (settings.placementUseCameraRelative && cam != null)
        {
            forward = cam.transform.forward;
            right = cam.transform.right;
        }

        forward.y = 0f;
        right.y = 0f;
        forward = forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
        right = right.sqrMagnitude > 0f ? right.normalized : Vector3.right;

        Vector3 move = forward * input.y + right * input.x;
        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        return move;
    }

    public static bool TryResolvePlacementPose(
        Item item,
        GameObject instance,
        Transform anchor,
        PreviewCaches previewCaches,
        Settings settings,
        Vector3 desiredPosition,
        Quaternion baseRotation,
        ref Collider groundCollider,
        out Vector3 resolvedPosition,
        out Quaternion resolvedRotation)
    {
        resolvedPosition = desiredPosition;
        resolvedRotation = baseRotation;

        if (SupportsWallPlacement(item)
            && TryResolveWallPlacementPose(
                item,
                instance,
                anchor,
                previewCaches,
                settings,
                desiredPosition,
                baseRotation,
                ref groundCollider,
                out resolvedPosition,
                out resolvedRotation))
        {
            return true;
        }

        if (RequiresPlacementSurfaceSupport(item))
        {
            return TryResolveHorizontalPlacementPose(
                item,
                instance,
                anchor,
                previewCaches,
                settings,
                desiredPosition,
                baseRotation,
                SupportsWallPlacement(item),
                ref groundCollider,
                out resolvedPosition,
                out resolvedRotation);
        }

        if (TrySnapToGround(
                instance,
                anchor,
                settings,
                desiredPosition,
                false,
                -1f,
                ref groundCollider,
                out resolvedPosition,
                out _))
        {
            return true;
        }

        groundCollider = null;
        return false;
    }

    public static bool IsPlacementValid(
        Item item,
        GameObject instance,
        Transform anchor,
        Collider groundCollider,
        PreviewCaches previewCaches,
        Settings settings)
    {
        if (instance == null)
        {
            return false;
        }

        if (RequiresPlacementSurfaceSupport(item) && groundCollider == null)
        {
            return false;
        }

        if (!IsWithinPlacementHeadHeight(instance.transform.position, anchor))
        {
            return false;
        }

        Collider[] colliders = GetPlacementColliders(instance, previewCaches);
        if (colliders == null || colliders.Length == 0)
        {
            return true;
        }

        bool hasSolidCollider = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (IsPlacementSolidCollider(col))
            {
                hasSolidCollider = true;
                break;
            }
        }

        if (!hasSolidCollider)
        {
            return true;
        }

        QueryTriggerInteraction triggerInteraction = settings.placementBlockTriggers
            ? QueryTriggerInteraction.Collide
            : QueryTriggerInteraction.Ignore;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (!IsPlacementSolidCollider(col))
            {
                continue;
            }

            Collider[] overlaps = QueryPlacementColliderOverlaps(
                col,
                Mathf.Max(0f, settings.placementBoundsPadding),
                settings.placementCollisionMask,
                triggerInteraction);
            if (overlaps == null || overlaps.Length == 0)
            {
                continue;
            }

            for (int j = 0; j < overlaps.Length; j++)
            {
                Collider hit = overlaps[j];
                if (hit == null)
                {
                    continue;
                }

                if (SupportsWallPlacement(item) && IsPlacementCharacterCollider(hit))
                {
                    return false;
                }

                if (IsIgnoredPlacementCollider(hit, instance, anchor, settings.placementIgnoreMask))
                {
                    continue;
                }

                if (HasBlockingPlacementPenetration(col, hit))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static void CachePlacementPhysics(GameObject instance, PreviewCaches previewCaches)
    {
        if (previewCaches == null)
        {
            return;
        }

        previewCaches.rigidbodyStates.Clear();
        if (instance == null)
        {
            previewCaches.colliders = null;
            return;
        }

        Rigidbody[] bodies = instance.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body == null)
            {
                continue;
            }

            previewCaches.rigidbodyStates.Add(new PreviewCaches.RigidbodyState(body, body.isKinematic, body.useGravity));
            body.isKinematic = true;
            body.useGravity = false;
        }

        previewCaches.colliders = instance.GetComponentsInChildren<Collider>(true);
    }

    public static void RestorePlacementPhysics(PreviewCaches previewCaches)
    {
        if (previewCaches == null)
        {
            return;
        }

        for (int i = 0; i < previewCaches.rigidbodyStates.Count; i++)
        {
            PreviewCaches.RigidbodyState state = previewCaches.rigidbodyStates[i];
            if (state.Body == null)
            {
                continue;
            }

            state.Body.isKinematic = state.WasKinematic;
            state.Body.useGravity = state.UsedGravity;
        }

        previewCaches.rigidbodyStates.Clear();
        previewCaches.colliders = null;
    }

    public static void CachePlacementVisuals(GameObject instance, PreviewCaches previewCaches, bool showValidity)
    {
        if (previewCaches == null)
        {
            return;
        }

        previewCaches.renderers.Clear();
        previewCaches.propertyBlock = null;
        previewCaches.lastValid = false;

        if (instance == null || !showValidity)
        {
            return;
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMaterial == null)
            {
                continue;
            }

            string property = null;
            if (renderer.sharedMaterial.HasProperty("_BaseColor"))
            {
                property = "_BaseColor";
            }
            else if (renderer.sharedMaterial.HasProperty("_Color"))
            {
                property = "_Color";
            }

            if (string.IsNullOrEmpty(property))
            {
                continue;
            }

            previewCaches.renderers.Add(new PreviewCaches.RendererState(renderer, property));
        }

        if (previewCaches.renderers.Count > 0)
        {
            previewCaches.propertyBlock = new MaterialPropertyBlock();
        }
    }

    public static void UpdatePlacementVisuals(
        PreviewCaches previewCaches,
        bool showValidity,
        bool isValid,
        Color validColor,
        Color invalidColor)
    {
        if (previewCaches == null || !showValidity || previewCaches.renderers.Count == 0)
        {
            return;
        }

        if (previewCaches.lastValid == isValid && previewCaches.propertyBlock != null)
        {
            return;
        }

        Color color = isValid ? validColor : invalidColor;
        if (previewCaches.propertyBlock == null)
        {
            previewCaches.propertyBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < previewCaches.renderers.Count; i++)
        {
            PreviewCaches.RendererState state = previewCaches.renderers[i];
            if (state.Renderer == null)
            {
                continue;
            }

            previewCaches.propertyBlock.Clear();
            previewCaches.propertyBlock.SetColor(state.ColorProperty, color);
            state.Renderer.SetPropertyBlock(previewCaches.propertyBlock);
        }

        previewCaches.lastValid = isValid;
    }

    public static void ClearPlacementVisuals(PreviewCaches previewCaches)
    {
        if (previewCaches == null || previewCaches.renderers.Count == 0)
        {
            return;
        }

        for (int i = 0; i < previewCaches.renderers.Count; i++)
        {
            PreviewCaches.RendererState state = previewCaches.renderers[i];
            if (state.Renderer == null)
            {
                continue;
            }

            state.Renderer.SetPropertyBlock(null);
        }

        previewCaches.renderers.Clear();
        previewCaches.propertyBlock = null;
        previewCaches.lastValid = false;
    }

    private static bool TryResolveWallPlacementPose(
        Item item,
        GameObject instance,
        Transform anchor,
        PreviewCaches previewCaches,
        Settings settings,
        Vector3 desiredPosition,
        Quaternion baseRotation,
        ref Collider groundCollider,
        out Vector3 resolvedPosition,
        out Quaternion resolvedRotation)
    {
        resolvedPosition = desiredPosition;
        resolvedRotation = baseRotation;

        if (anchor == null)
        {
            groundCollider = null;
            return false;
        }

        Vector3 facingHint = ResolveFacingHint(anchor, settings);
        if (TryFindWallPlacementSupport(item, instance, anchor, settings, desiredPosition, out RaycastHit wallHit))
        {
            Vector3 normal = wallHit.normal.sqrMagnitude > 0.0001f ? wallHit.normal.normalized : Vector3.forward;
            float offset = 0.02f;
            if (BeaconMarker.TryFind(instance, out BeaconMarker beacon))
            {
                offset = Mathf.Max(offset, beacon.SurfaceOffset);
            }

            Quaternion wallRotation = BuildPlacementSurfaceRotation(normal, facingHint, baseRotation);
            Vector3 wallPosition = wallHit.point + normal * offset;
            if (TryAlignPlacementToSupport(instance, previewCaches, wallPosition, wallRotation, wallHit.point, normal, offset, out Vector3 alignedWallPosition))
            {
                wallPosition = alignedWallPosition;
            }

            if (!IsWithinPlacementHeadHeight(wallPosition, anchor))
            {
                groundCollider = null;
            }
            else
            {
                resolvedPosition = wallPosition;
                resolvedRotation = wallRotation;
                groundCollider = wallHit.collider;
                return true;
            }
        }

        return TryResolveHorizontalPlacementPose(
            item,
            instance,
            anchor,
            previewCaches,
            settings,
            desiredPosition,
            baseRotation,
            true,
            ref groundCollider,
            out resolvedPosition,
            out resolvedRotation);
    }

    private static bool TryResolveHorizontalPlacementPose(
        Item item,
        GameObject instance,
        Transform anchor,
        PreviewCaches previewCaches,
        Settings settings,
        Vector3 desiredPosition,
        Quaternion baseRotation,
        bool ignoreCharacterSupport,
        ref Collider groundCollider,
        out Vector3 resolvedPosition,
        out Quaternion resolvedRotation)
    {
        resolvedPosition = desiredPosition;
        resolvedRotation = baseRotation;

        if (!TrySnapToGround(
                instance,
                anchor,
                settings,
                desiredPosition,
                ignoreCharacterSupport,
                GetPlacementMinimumUpDot(settings),
                ref groundCollider,
                out resolvedPosition,
                out RaycastHit supportHit))
        {
            return false;
        }

        resolvedRotation = BuildPlacementSurfaceRotation(Vector3.up, ResolveFacingHint(anchor, settings), baseRotation);
        if (TryAlignPlacementToSupport(
                instance,
                previewCaches,
                resolvedPosition,
                resolvedRotation,
                supportHit.point,
                Vector3.up,
                settings.placementGroundOffset,
                out Vector3 alignedPosition))
        {
            resolvedPosition = alignedPosition;
        }

        return true;
    }

    private static bool TrySnapToGround(
        GameObject instance,
        Transform anchor,
        Settings settings,
        Vector3 position,
        bool ignoreCharacterSupport,
        float minimumUpDot,
        ref Collider groundCollider,
        out Vector3 snappedPosition,
        out RaycastHit supportHit)
    {
        snappedPosition = position;
        supportHit = default;
        if (!settings.placementSnapToGround)
        {
            return false;
        }

        float height = Mathf.Max(0f, settings.placementGroundRaycastHeight);
        float distance = Mathf.Max(0f, settings.placementGroundRaycastDistance);
        Vector3 origin = position + Vector3.up * height;
        float maxDistance = height + distance;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            maxDistance,
            settings.placementGroundMask,
            QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            bool hasHit = false;
            float bestDistance = float.MaxValue;
            RaycastHit bestHit = default;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                Collider col = hit.collider;
                if (col == null || IsIgnoredPlacementCollider(col, instance, anchor, settings.placementIgnoreMask))
                {
                    continue;
                }

                if (ignoreCharacterSupport && IsPlacementCharacterCollider(col))
                {
                    continue;
                }

                if (minimumUpDot >= 0f)
                {
                    Vector3 normal = hit.normal;
                    if (normal.sqrMagnitude < 0.0001f || normal.normalized.y < minimumUpDot)
                    {
                        continue;
                    }
                }

                if (!IsWithinPlacementHeadHeight(hit.point.y + settings.placementGroundOffset, anchor))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestHit = hit;
                    hasHit = true;
                }
            }

            if (hasHit)
            {
                snappedPosition.y = bestHit.point.y + settings.placementGroundOffset;
                supportHit = bestHit;
                groundCollider = bestHit.collider;
                return true;
            }
        }

        groundCollider = null;
        return false;
    }

    private static bool TryFindWallPlacementSupport(
        Item item,
        GameObject instance,
        Transform anchor,
        Settings settings,
        Vector3 desiredPosition,
        out RaycastHit bestHit)
    {
        bestHit = default;
        if (anchor == null)
        {
            return false;
        }

        Vector3 origin = anchor.position + Vector3.up * Mathf.Max(0f, settings.wallProbeHeight);
        Vector3 target = new Vector3(desiredPosition.x, origin.y, desiredPosition.z);
        Vector3 direction = target - origin;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            Vector3 fallback = ResolveFacingHint(anchor, settings);
            fallback.y = 0f;
            float fallbackDistance = Mathf.Max(0.25f, settings.placementStartDistance);
            direction = fallback.sqrMagnitude > 0.0001f
                ? fallback.normalized * fallbackDistance
                : Vector3.forward * fallbackDistance;
        }

        float distance = Mathf.Min(item != null ? item.GetPlacementRadius(settings.placementRadius) : settings.placementRadius, direction.magnitude);
        if (distance <= 0.01f)
        {
            return false;
        }

        Vector3 castDirection = direction.normalized;
        int mask = settings.placementGroundMask.value | settings.placementCollisionMask.value;
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            Mathf.Max(0.01f, settings.wallProbeRadius),
            castDirection,
            distance,
            mask,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            Collider col = hit.collider;
            if (col == null || IsIgnoredPlacementCollider(col, instance, anchor, settings.placementIgnoreMask))
            {
                continue;
            }

            if (IsPlacementCharacterCollider(col))
            {
                continue;
            }

            Vector3 normal = hit.normal;
            if (normal.sqrMagnitude < 0.0001f || Mathf.Abs(normal.y) > settings.wallNormalMaxY)
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private static Camera ResolvePlacementCamera(Settings settings)
    {
        return settings.placementCamera != null ? settings.placementCamera : Camera.main;
    }

    private static Vector3 ResolvePlacementForward(Transform anchor, Settings settings)
    {
        Vector3 forward = anchor != null ? anchor.forward : Vector3.forward;
        Camera cam = ResolvePlacementCamera(settings);
        if (settings.placementUseCameraRelative && cam != null)
        {
            forward = cam.transform.forward;
        }

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f && anchor != null)
        {
            forward = anchor.forward;
            forward.y = 0f;
        }

        return forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
    }

    private static Vector3 ResolveFacingHint(Transform anchor, Settings settings)
    {
        Vector3 forward = anchor != null ? anchor.forward : Vector3.forward;
        Camera cam = ResolvePlacementCamera(settings);
        if (settings.placementUseCameraRelative && cam != null)
        {
            forward = cam.transform.forward;
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        return forward.normalized;
    }

    private static Quaternion BuildPlacementSurfaceRotation(Vector3 surfaceNormal, Vector3 facingHint, Quaternion baseRotation)
    {
        Vector3 up = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(facingHint, up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(Vector3.forward, up);
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.Cross(up, Vector3.right);
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.Cross(up, Vector3.forward);
        }

        Quaternion surfaceRotation = Quaternion.LookRotation(forward.normalized, up);
        return surfaceRotation * baseRotation;
    }

    private static float GetPlacementMinimumUpDot(Settings settings)
    {
        float clampedAngle = Mathf.Clamp(settings.horizontalPlacementMaxSlopeAngle, 0f, 89f);
        return Mathf.Cos(clampedAngle * Mathf.Deg2Rad);
    }

    private static bool IsWithinPlacementHeadHeight(Vector3 worldPosition, Transform anchor)
    {
        return IsWithinPlacementHeadHeight(worldPosition.y, anchor);
    }

    private static bool IsWithinPlacementHeadHeight(float worldY, Transform anchor)
    {
        return !TryGetPlacementHeadWorldY(anchor, out float headWorldY)
            || worldY <= headWorldY + 0.001f;
    }

    private static bool TryGetPlacementHeadWorldY(Transform anchor, out float headWorldY)
    {
        headWorldY = 0f;
        if (anchor == null)
        {
            return false;
        }

        SquadCharacterController squadController = anchor.GetComponent<SquadCharacterController>();
        if (squadController == null)
        {
            squadController = anchor.GetComponentInParent<SquadCharacterController>();
        }

        if (squadController != null && squadController.TryGetHeadWorldY(out headWorldY))
        {
            return true;
        }

        CapsuleCollider capsule = anchor.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            capsule = anchor.GetComponentInParent<CapsuleCollider>();
        }

        if (capsule == null || capsule.direction != 1)
        {
            return false;
        }

        Vector3 scale = capsule.transform.lossyScale;
        float maxXZ = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float absY = Mathf.Abs(scale.y);
        float radius = capsule.radius * maxXZ;
        float height = Mathf.Max(capsule.height * absY, radius * 2f);
        Vector3 center = capsule.transform.TransformPoint(capsule.center);
        headWorldY = center.y + height * 0.5f;
        return true;
    }

    private static bool SupportsWallPlacement(Item item)
    {
        return item != null && item.SupportsWallPlacement();
    }

    private static bool RequiresPlacementSurfaceSupport(Item item)
    {
        return item != null && item.RequiresPlacementSurfaceSupport();
    }

    private static bool IsIgnoredPlacementCollider(Collider col, GameObject instance, Transform anchor, LayerMask ignoreMask)
    {
        if (col == null)
        {
            return true;
        }

        if (instance != null && col.transform.IsChildOf(instance.transform))
        {
            return true;
        }

        if (anchor != null && col.transform.IsChildOf(anchor))
        {
            return true;
        }

        if ((ignoreMask.value & (1 << col.gameObject.layer)) != 0)
        {
            return true;
        }

        return false;
    }

    private static bool TryAlignPlacementToSupport(
        GameObject instance,
        PreviewCaches previewCaches,
        Vector3 desiredPosition,
        Quaternion desiredRotation,
        Vector3 supportPoint,
        Vector3 supportNormal,
        float surfaceOffset,
        out Vector3 alignedPosition)
    {
        alignedPosition = desiredPosition;
        if (instance == null)
        {
            return false;
        }

        Collider[] colliders = GetPlacementColliders(instance, previewCaches);
        if (colliders == null || colliders.Length == 0)
        {
            return false;
        }

        Vector3 normal = supportNormal.sqrMagnitude > 0.0001f ? supportNormal.normalized : Vector3.up;
        Vector3 targetPoint = supportPoint + normal * Mathf.Max(0f, surfaceOffset);
        Vector3 probePoint = targetPoint - normal * GetPlacementProbeDistance(colliders);
        float targetProjection = Vector3.Dot(targetPoint, normal);

        Transform root = instance.transform;
        Vector3 originalPosition = root.position;
        Quaternion originalRotation = root.rotation;
        bool hasSupportFace = false;
        float supportProjection = 0f;

        try
        {
            root.SetPositionAndRotation(desiredPosition, desiredRotation);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (!IsPlacementSolidCollider(col))
                {
                    continue;
                }

                Vector3 closestPoint = col.ClosestPoint(probePoint);
                float projection = Vector3.Dot(closestPoint, normal);
                if (!hasSupportFace || projection < supportProjection)
                {
                    supportProjection = projection;
                    hasSupportFace = true;
                }
            }
        }
        finally
        {
            root.SetPositionAndRotation(originalPosition, originalRotation);
        }

        if (!hasSupportFace)
        {
            return false;
        }

        alignedPosition = desiredPosition + normal * (targetProjection - supportProjection);
        return true;
    }

    private static Collider[] GetPlacementColliders(GameObject instance, PreviewCaches previewCaches)
    {
        if (previewCaches != null && previewCaches.colliders != null)
        {
            return previewCaches.colliders;
        }

        return instance != null ? instance.GetComponentsInChildren<Collider>(true) : null;
    }

    private static bool IsPlacementSolidCollider(Collider col)
    {
        return col != null
            && col.enabled
            && col.gameObject.activeInHierarchy
            && !col.isTrigger;
    }

    private static Collider[] QueryPlacementColliderOverlaps(
        Collider col,
        float padding,
        LayerMask collisionMask,
        QueryTriggerInteraction triggerInteraction)
    {
        if (col == null)
        {
            return null;
        }

        if (col is BoxCollider box)
        {
            Vector3 scale = AbsVector(box.transform.lossyScale);
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, scale) + Vector3.one * padding;
            return Physics.OverlapBox(
                box.transform.TransformPoint(box.center),
                halfExtents,
                box.transform.rotation,
                collisionMask,
                triggerInteraction);
        }

        if (col is SphereCollider sphere)
        {
            float radius = sphere.radius * MaxComponent(AbsVector(sphere.transform.lossyScale)) + padding;
            return Physics.OverlapSphere(
                sphere.transform.TransformPoint(sphere.center),
                radius,
                collisionMask,
                triggerInteraction);
        }

        if (col is CapsuleCollider capsule)
        {
            GetCapsuleWorldGeometry(capsule, out Vector3 point0, out Vector3 point1, out float radius);
            return Physics.OverlapCapsule(
                point0,
                point1,
                radius + padding,
                collisionMask,
                triggerInteraction);
        }

        Bounds bounds = col.bounds;
        return Physics.OverlapBox(
            bounds.center,
            bounds.extents + Vector3.one * padding,
            Quaternion.identity,
            collisionMask,
            triggerInteraction);
    }

    private static void GetCapsuleWorldGeometry(CapsuleCollider capsule, out Vector3 point0, out Vector3 point1, out float radius)
    {
        Vector3 scale = AbsVector(capsule.transform.lossyScale);
        Vector3 center = capsule.transform.TransformPoint(capsule.center);
        Vector3 axis;
        float heightScale;
        float radiusScale;

        switch (capsule.direction)
        {
            case 0:
                axis = capsule.transform.right;
                heightScale = scale.x;
                radiusScale = Mathf.Max(scale.y, scale.z);
                break;
            case 2:
                axis = capsule.transform.forward;
                heightScale = scale.z;
                radiusScale = Mathf.Max(scale.x, scale.y);
                break;
            default:
                axis = capsule.transform.up;
                heightScale = scale.y;
                radiusScale = Mathf.Max(scale.x, scale.z);
                break;
        }

        radius = capsule.radius * radiusScale;
        float height = Mathf.Max(capsule.height * heightScale, radius * 2f);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 offset = axis.normalized * halfSegment;
        point0 = center + offset;
        point1 = center - offset;
    }

    private static bool HasBlockingPlacementPenetration(Collider placementCollider, Collider hit)
    {
        if (placementCollider == null || hit == null)
        {
            return false;
        }

        if (TryComputePenetrationDistance(placementCollider, hit, out float penetrationDistance))
        {
            return penetrationDistance > PlacementPenetrationTolerance;
        }

        return TryGetBoundsIntersectionDepth(placementCollider.bounds, hit.bounds, out float boundsDepth)
            && boundsDepth > PlacementPenetrationTolerance;
    }

    private static bool TryComputePenetrationDistance(Collider placementCollider, Collider hit, out float distance)
    {
        distance = 0f;
        if (placementCollider == null || hit == null)
        {
            return false;
        }

        return Physics.ComputePenetration(
            placementCollider,
            placementCollider.transform.position,
            placementCollider.transform.rotation,
            hit,
            hit.transform.position,
            hit.transform.rotation,
            out _,
            out distance);
    }

    private static bool TryGetBoundsIntersectionDepth(Bounds a, Bounds b, out float depth)
    {
        depth = 0f;
        float overlapX = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
        float overlapY = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
        float overlapZ = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
        if (overlapX <= 0f || overlapY <= 0f || overlapZ <= 0f)
        {
            return false;
        }

        depth = Mathf.Min(overlapX, Mathf.Min(overlapY, overlapZ));
        return true;
    }

    private static float GetPlacementProbeDistance(Collider[] colliders)
    {
        float distance = 4f;
        if (colliders == null)
        {
            return distance;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (!IsPlacementSolidCollider(col))
            {
                continue;
            }

            distance = Mathf.Max(distance, col.bounds.extents.magnitude * 4f);
        }

        return distance;
    }

    private static Vector3 AbsVector(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static float MaxComponent(Vector3 value)
    {
        return Mathf.Max(value.x, Mathf.Max(value.y, value.z));
    }

    private static bool IsPlacementCharacterCollider(Collider col)
    {
        if (col == null)
        {
            return false;
        }

        return col.GetComponentInParent<SquadCharacterController>() != null
            || col.GetComponentInParent<Character>() != null;
    }

    private static bool IsPlacementGroundCollider(Collider col, Collider groundCollider)
    {
        if (col == null || groundCollider == null)
        {
            return false;
        }

        if (col == groundCollider)
        {
            return true;
        }

        Transform root = groundCollider.transform;
        return root != null && col.transform.IsChildOf(root);
    }
}
