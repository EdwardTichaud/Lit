using System.Collections.Generic;
using UnityEngine;

// Contrat minimal pour une cible d'interaction resolue par le personnage local.
public interface ICharacterDetectedInteractable
{
    bool CanBeDetectedBy(SquadCharacterController controller);
    Collider GetInteractionDetectionCollider();
    Transform GetInteractionAnchor();
    float GetInteractionMaxDistance(SquadCharacterController controller);
    int GetInteractionPriority(SquadCharacterController controller);
    void SetDetectedCharacter(GameObject character);
}

public interface ILocalInteractHandler
{
    bool TryHandleLocalInteract();
}

public static class CharacterInteractionDetection
{
    private const float VisibilityRaycastSkin = 0.03f;
    private const float VisibilityViewportEpsilon = 0.001f;
    private const int VisibilitySampleCapacity = 16;
    private const int VisibilityRaycastHitCapacity = 32;

    private static readonly Plane[] visibilityFrustumPlanes = new Plane[6];
    private static readonly Vector3[] visibilitySamplePoints = new Vector3[VisibilitySampleCapacity];
    private static readonly RaycastHit[] visibilityRaycastHits = new RaycastHit[VisibilityRaycastHitCapacity];
    private static readonly List<Renderer> visibilityRenderers = new List<Renderer>(16);

    public static ICharacterDetectedInteractable ResolveTarget(Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

        Transform current = collider.transform;
        while (current != null)
        {
            if (current.TryGetComponent(out InteractableItem item) && item.isActiveAndEnabled && TimePeriodVisibility.IsVisibleFor(item))
            {
                return item;
            }

            if (current.TryGetComponent(out StabReading stabReading) && stabReading.isActiveAndEnabled && TimePeriodVisibility.IsVisibleFor(stabReading))
            {
                return stabReading;
            }

            if (current.TryGetComponent(out DestructibleObject destructible) && destructible.isActiveAndEnabled && TimePeriodVisibility.IsVisibleFor(destructible))
            {
                return destructible;
            }

            if (LegacyBuildingSystem.Enabled &&
                current.TryGetComponent(out BuildingInfoInteractable buildingInfo) &&
                buildingInfo.isActiveAndEnabled &&
                TimePeriodVisibility.IsVisibleFor(buildingInfo))
            {
                return buildingInfo;
            }

            if (current.TryGetComponent(out LadderInteractable ladder) && ladder.isActiveAndEnabled && TimePeriodVisibility.IsVisibleFor(ladder))
            {
                return ladder;
            }

            if (current.TryGetComponent(out Door door) && door.isActiveAndEnabled && TimePeriodVisibility.IsVisibleFor(door))
            {
                return door;
            }

            if (current.TryGetComponent(out Flame flame) && flame.isActiveAndEnabled && TimePeriodVisibility.IsVisibleFor(flame))
            {
                return flame;
            }

            if (current.TryGetComponent(out GhostController ghost) && ghost.isActiveAndEnabled && TimePeriodVisibility.IsVisibleFor(ghost))
            {
                return ghost;
            }

            if (TryResolveGenericInteractable(current, out ICharacterDetectedInteractable genericTarget))
            {
                return genericTarget;
            }

            current = current.parent;
        }

        return null;
    }

    private static bool TryResolveGenericInteractable(Transform current, out ICharacterDetectedInteractable target)
    {
        target = null;
        if (current == null)
        {
            return false;
        }

        MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || !behaviour.isActiveAndEnabled)
            {
                continue;
            }

            if (behaviour is ICharacterDetectedInteractable interactable &&
                TimePeriodVisibility.IsVisibleFor(behaviour))
            {
                target = interactable;
                return true;
            }
        }

        return false;
    }

    public static Collider ResolveInteractionCollider(
        Component owner,
        Collider preferred,
        bool allowRuntimeFallback = true)
    {
        if (IsUsableCollider(preferred, false))
        {
            return preferred;
        }

        if (owner != null)
        {
            Collider[] colliders = owner.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (IsUsableCollider(colliders[i], false))
                {
                    return colliders[i];
                }
            }

            if (IsUsableCollider(preferred, true))
            {
                return preferred;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (IsUsableCollider(colliders[i], true))
                {
                    return colliders[i];
                }
            }
        }

        if (IsUsableCollider(preferred, true))
        {
            return preferred;
        }

        if (allowRuntimeFallback &&
            Application.isPlaying &&
            owner != null &&
            TryCreateFallbackInteractionCollider(owner, out Collider fallback))
        {
            return fallback;
        }

        return null;
    }

    public static Vector3 GetInteractionPoint(Collider collider, Transform anchor, Vector3 origin)
    {
        if (collider != null)
        {
            if (TryGetClosestPoint(collider, origin, out Vector3 point) &&
                (point - origin).sqrMagnitude > 0.000001f)
            {
                return point;
            }

            // Certaines portes de scene utilisent des MeshColliders non convexes,
            // sans closest point precis. Le centre des bounds les rendait trop
            // faciles a rejeter alors que le joueur etait contre la surface.
            return collider.bounds.ClosestPoint(origin);
        }

        return anchor != null ? anchor.position : origin;
    }

    public static bool IsCharacterWithinRange(Transform characterRoot, Collider collider, Transform anchor, float maxDistance)
    {
        if (characterRoot == null)
        {
            return false;
        }

        Vector3 origin = GetCharacterOrigin(characterRoot);
        Vector3 point = GetInteractionPoint(collider, anchor, origin);
        float distance = Mathf.Max(0f, maxDistance);
        return (point - origin).sqrMagnitude <= distance * distance;
    }

    public static Camera ResolveInteractionCamera()
    {
        Camera mainCamera = Camera.main;
        return mainCamera != null && mainCamera.isActiveAndEnabled ? mainCamera : null;
    }

    public static bool IsInteractionTargetVisibleFromCamera(
        ICharacterDetectedInteractable target,
        Collider collider,
        Transform anchor,
        Camera camera,
        Transform characterRoot)
    {
        if (!(target is Component targetComponent) || targetComponent == null || camera == null || !camera.isActiveAndEnabled)
        {
            return false;
        }

        if (!TryResolveVisibilityBounds(targetComponent, collider, anchor, camera, out Bounds bounds))
        {
            return false;
        }

        GeometryUtility.CalculateFrustumPlanes(camera, visibilityFrustumPlanes);
        if (!GeometryUtility.TestPlanesAABB(visibilityFrustumPlanes, bounds))
        {
            return false;
        }

        Transform targetRoot = targetComponent.transform;
        Vector3 cameraPosition = camera.transform.position;
        int sampleCount = FillVisibilitySamplePoints(bounds, cameraPosition, visibilitySamplePoints);
        for (int i = 0; i < sampleCount; i++)
        {
            Vector3 samplePoint = visibilitySamplePoints[i];
            if (!IsPointInsideCameraViewport(camera, samplePoint))
            {
                continue;
            }

            if (HasUnblockedCameraRay(camera, samplePoint, targetRoot, characterRoot))
            {
                return true;
            }
        }

        return false;
    }

    public static bool UsesTriggerInteractionZone(ICharacterDetectedInteractable target)
    {
        return target is Flame;
    }

    public static bool IsCharacterInsideInteractionCollider(Transform characterRoot, Collider interactionCollider)
    {
        if (characterRoot == null || !IsUsableCollider(interactionCollider, true))
        {
            return false;
        }

        Vector3 origin = GetCharacterOrigin(characterRoot);
        if (IsPointInsideCollider(interactionCollider, origin) ||
            IsPointInsideCollider(interactionCollider, characterRoot.position))
        {
            return true;
        }

        Collider[] characterColliders = characterRoot.GetComponentsInChildren<Collider>(false);
        for (int i = 0; i < characterColliders.Length; i++)
        {
            Collider characterCollider = characterColliders[i];
            if (!IsUsableCharacterCollider(characterCollider))
            {
                continue;
            }

            if (CollidersOverlap(interactionCollider, characterCollider))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsPointInsideCollider(Collider collider, Vector3 point)
    {
        if (!IsUsableCollider(collider, true))
        {
            return false;
        }

        if (!SupportsClosestPoint(collider))
        {
            return collider.bounds.Contains(point);
        }

        Vector3 closest = collider.ClosestPoint(point);
        return (closest - point).sqrMagnitude <= 0.0001f;
    }

    private static bool IsUsableCollider(Collider collider, bool allowTrigger)
    {
        return collider != null
            && collider.enabled
            && collider.gameObject.activeInHierarchy
            && (allowTrigger || !collider.isTrigger);
    }

    private static bool IsUsableCharacterCollider(Collider collider)
    {
        return collider != null
            && collider.enabled
            && collider.gameObject.activeInHierarchy
            && !collider.isTrigger;
    }

    private static bool CollidersOverlap(Collider interactionCollider, Collider characterCollider)
    {
        if (interactionCollider == null || characterCollider == null)
        {
            return false;
        }

        bool penetrates = Physics.ComputePenetration(
            interactionCollider,
            interactionCollider.transform.position,
            interactionCollider.transform.rotation,
            characterCollider,
            characterCollider.transform.position,
            characterCollider.transform.rotation,
            out _,
            out _);
        if (penetrates)
        {
            return true;
        }

        Vector3 characterClosestPoint = characterCollider.ClosestPoint(interactionCollider.bounds.center);
        if (IsPointInsideCollider(interactionCollider, characterClosestPoint))
        {
            return true;
        }

        Vector3 interactionClosestPoint = interactionCollider.ClosestPoint(characterCollider.bounds.center);
        return SupportsClosestPoint(characterCollider) && IsPointInsideCollider(characterCollider, interactionClosestPoint);
    }

    private static bool TryResolveVisibilityBounds(
        Component target,
        Collider collider,
        Transform anchor,
        Camera camera,
        out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;
        bool hasRenderer = false;

        visibilityRenderers.Clear();
        target.GetComponentsInChildren<Renderer>(false, visibilityRenderers);
        for (int i = 0; i < visibilityRenderers.Count; i++)
        {
            Renderer renderer = visibilityRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            hasRenderer = true;
            if (!renderer.enabled ||
                renderer.forceRenderingOff ||
                !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!IsLayerVisibleToCamera(camera, renderer.gameObject.layer))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        visibilityRenderers.Clear();

        if (!hasBounds && hasRenderer)
        {
            return false;
        }

        if (!hasBounds && IsUsableCollider(collider, true))
        {
            bounds = collider.bounds;
            hasBounds = true;
        }

        if (!hasBounds && anchor != null)
        {
            bounds = new Bounds(anchor.position, Vector3.one * 0.1f);
            hasBounds = true;
        }

        if (hasBounds && bounds.size == Vector3.zero)
        {
            bounds.size = Vector3.one * 0.1f;
        }

        return hasBounds;
    }

    private static int FillVisibilitySamplePoints(Bounds bounds, Vector3 cameraPosition, Vector3[] points)
    {
        if (points == null || points.Length < VisibilitySampleCapacity)
        {
            return 0;
        }

        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        int index = 0;
        points[index++] = center;
        points[index++] = bounds.ClosestPoint(cameraPosition);

        points[index++] = new Vector3(min.x, min.y, min.z);
        points[index++] = new Vector3(max.x, min.y, min.z);
        points[index++] = new Vector3(min.x, max.y, min.z);
        points[index++] = new Vector3(max.x, max.y, min.z);
        points[index++] = new Vector3(min.x, min.y, max.z);
        points[index++] = new Vector3(max.x, min.y, max.z);
        points[index++] = new Vector3(min.x, max.y, max.z);
        points[index++] = new Vector3(max.x, max.y, max.z);

        points[index++] = center + new Vector3(extents.x, 0f, 0f);
        points[index++] = center - new Vector3(extents.x, 0f, 0f);
        points[index++] = center + new Vector3(0f, extents.y, 0f);
        points[index++] = center - new Vector3(0f, extents.y, 0f);
        points[index++] = center + new Vector3(0f, 0f, extents.z);
        points[index++] = center - new Vector3(0f, 0f, extents.z);

        return index;
    }

    private static bool IsPointInsideCameraViewport(Camera camera, Vector3 point)
    {
        Vector3 viewportPoint = camera.WorldToViewportPoint(point);
        return viewportPoint.z >= camera.nearClipPlane
            && viewportPoint.z <= camera.farClipPlane
            && viewportPoint.x >= -VisibilityViewportEpsilon
            && viewportPoint.x <= 1f + VisibilityViewportEpsilon
            && viewportPoint.y >= -VisibilityViewportEpsilon
            && viewportPoint.y <= 1f + VisibilityViewportEpsilon;
    }

    private static bool HasUnblockedCameraRay(
        Camera camera,
        Vector3 samplePoint,
        Transform targetRoot,
        Transform characterRoot)
    {
        Vector3 cameraPosition = camera.transform.position;
        Vector3 toSample = samplePoint - cameraPosition;
        float distance = toSample.magnitude;
        if (distance <= VisibilityRaycastSkin)
        {
            return true;
        }

        int occlusionMask = camera.cullingMask & Physics.DefaultRaycastLayers;
        if (occlusionMask == 0)
        {
            occlusionMask = Physics.DefaultRaycastLayers;
        }

        int hitCount = Physics.RaycastNonAlloc(
            cameraPosition,
            toSample / distance,
            visibilityRaycastHits,
            distance - VisibilityRaycastSkin,
            occlusionMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = visibilityRaycastHits[i].collider;
            if (hitCollider == null ||
                IsColliderUnderRoot(hitCollider, targetRoot) ||
                IsColliderUnderRoot(hitCollider, characterRoot))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsColliderUnderRoot(Collider collider, Transform root)
    {
        return collider != null
            && root != null
            && collider.transform != null
            && (collider.transform == root || collider.transform.IsChildOf(root));
    }

    private static bool IsLayerVisibleToCamera(Camera camera, int layer)
    {
        return camera != null && (camera.cullingMask & (1 << layer)) != 0;
    }

    private static bool TryGetClosestPoint(Collider collider, Vector3 origin, out Vector3 point)
    {
        point = Vector3.zero;
        if (collider == null || !SupportsClosestPoint(collider))
        {
            return false;
        }

        point = collider.ClosestPoint(origin);
        return true;
    }

    private static bool SupportsClosestPoint(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider)
        {
            return true;
        }

        MeshCollider meshCollider = collider as MeshCollider;
        return meshCollider != null && meshCollider.convex;
    }

    private static bool TryCreateFallbackInteractionCollider(Component owner, out Collider fallback)
    {
        fallback = null;
        GameObject target = owner != null ? owner.gameObject : null;
        if (target == null || !TryCalculateBounds(target, out Bounds bounds))
        {
            return false;
        }

        BoxCollider box = target.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = target.AddComponent<BoxCollider>();
        }

        FitBoxColliderToWorldBounds(box, bounds);
        box.isTrigger = false;
        box.enabled = true;
        fallback = box;
        return true;
    }

    private static bool TryCalculateBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        if (root == null)
        {
            return false;
        }

        bool hasBounds = false;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
        }

        if (hasBounds && bounds.size == Vector3.zero)
        {
            bounds.size = Vector3.one * 0.1f;
        }

        return hasBounds;
    }

    private static void FitBoxColliderToWorldBounds(BoxCollider box, Bounds worldBounds)
    {
        if (box == null)
        {
            return;
        }

        Transform target = box.transform;
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z)
        };

        Bounds localBounds = new Bounds(target.InverseTransformPoint(corners[0]), Vector3.zero);
        for (int i = 1; i < corners.Length; i++)
        {
            localBounds.Encapsulate(target.InverseTransformPoint(corners[i]));
        }

        box.center = localBounds.center;
        box.size = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(localBounds.size.x)),
            Mathf.Max(0.01f, Mathf.Abs(localBounds.size.y)),
            Mathf.Max(0.01f, Mathf.Abs(localBounds.size.z)));
    }

    private static Vector3 GetCharacterOrigin(Transform characterRoot)
    {
        if (characterRoot == null)
        {
            return Vector3.zero;
        }

        SquadCharacterController controller = characterRoot.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            controller = characterRoot.GetComponentInChildren<SquadCharacterController>(true);
        }

        if (controller != null)
        {
            return controller.GetInteractionOriginWorldPosition();
        }

        return characterRoot.position;
    }
}
