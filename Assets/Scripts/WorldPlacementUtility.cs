using System.Collections.Generic;
using UnityEngine;

// Runtime partage pour la previsualisation et la validation de pose en monde.
public static class WorldPlacementUtility
{
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

        Collider[] colliders = previewCaches != null ? previewCaches.colliders : null;
        if (colliders == null || colliders.Length == 0)
        {
            return true;
        }

        Collider seed = null;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col != null && !col.isTrigger)
            {
                seed = col;
                break;
            }
        }

        if (seed == null)
        {
            return true;
        }

        Bounds bounds = seed.bounds;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || col.isTrigger)
            {
                continue;
            }

            bounds.Encapsulate(col.bounds);
        }

        Vector3 extents = bounds.extents + Vector3.one * Mathf.Max(0f, settings.placementBoundsPadding);
        QueryTriggerInteraction triggerInteraction = settings.placementBlockTriggers
            ? QueryTriggerInteraction.Collide
            : QueryTriggerInteraction.Ignore;
        Collider[] overlaps = Physics.OverlapBox(
            bounds.center,
            extents,
            Quaternion.identity,
            settings.placementCollisionMask,
            triggerInteraction);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider hit = overlaps[i];
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

            if (IsPlacementGroundCollider(hit, groundCollider))
            {
                continue;
            }

            return false;
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

            Vector3 wallPosition = wallHit.point + normal * offset;
            if (!IsWithinPlacementHeadHeight(wallPosition, anchor))
            {
                groundCollider = null;
            }
            else
            {
                resolvedPosition = wallPosition;
                resolvedRotation = BuildPlacementSurfaceRotation(normal, facingHint, baseRotation);
                groundCollider = wallHit.collider;
                return true;
            }
        }

        return TryResolveHorizontalPlacementPose(
            item,
            instance,
            anchor,
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
                out _))
        {
            return false;
        }

        resolvedRotation = BuildPlacementSurfaceRotation(Vector3.up, ResolveFacingHint(anchor, settings), baseRotation);
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
