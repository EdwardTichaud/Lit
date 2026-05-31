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
    public static ICharacterDetectedInteractable ResolveTarget(Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

        Transform current = collider.transform;
        while (current != null)
        {
            if (current.TryGetComponent(out IustiaIdolPrayer idol) && idol.isActiveAndEnabled && TimePeriodVisibility.IsVisibleFor(idol))
            {
                return idol;
            }

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

            if (current.TryGetComponent(out BuildingInfoInteractable buildingInfo) && buildingInfo.isActiveAndEnabled && TimePeriodVisibility.IsVisibleFor(buildingInfo))
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

            if (current.TryGetComponent(out Torch torch) && torch.isActiveAndEnabled && TimePeriodVisibility.IsVisibleFor(torch))
            {
                return torch;
            }

            if (current.TryGetComponent(out Brasero brasero) && brasero.isActiveAndEnabled && TimePeriodVisibility.IsVisibleFor(brasero))
            {
                return brasero;
            }

            if (current.TryGetComponent(out ReadableSentencePuzzle readableSentencePuzzle) && readableSentencePuzzle.isActiveAndEnabled && TimePeriodVisibility.IsVisibleFor(readableSentencePuzzle))
            {
                return readableSentencePuzzle;
            }

            if (current.TryGetComponent(out GhostController ghost) && ghost.isActiveAndEnabled && TimePeriodVisibility.IsVisibleFor(ghost))
            {
                return ghost;
            }

            current = current.parent;
        }

        return null;
    }

    public static Collider ResolveInteractionCollider(Component owner, Collider preferred)
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

        if (Application.isPlaying && owner != null && TryCreateFallbackInteractionCollider(owner, out Collider fallback))
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

    public static bool UsesTriggerInteractionZone(ICharacterDetectedInteractable target)
    {
        return target is Torch || target is Brasero;
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
