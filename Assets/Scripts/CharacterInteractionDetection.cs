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
            if (current.TryGetComponent(out IustiaIdolPrayer idol) && idol.isActiveAndEnabled)
            {
                return idol;
            }

            if (current.TryGetComponent(out InteractableItem item) && item.isActiveAndEnabled)
            {
                return item;
            }

            if (current.TryGetComponent(out DestructibleObject destructible) && destructible.isActiveAndEnabled)
            {
                return destructible;
            }

            if (current.TryGetComponent(out BuildingInfoInteractable buildingInfo) && buildingInfo.isActiveAndEnabled)
            {
                return buildingInfo;
            }

            if (current.TryGetComponent(out LadderInteractable ladder) && ladder.isActiveAndEnabled)
            {
                return ladder;
            }

            if (current.TryGetComponent(out ReadableSentencePuzzle readableSentencePuzzle) && readableSentencePuzzle.isActiveAndEnabled)
            {
                return readableSentencePuzzle;
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

            return collider.bounds.center;
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

    private static bool IsUsableCollider(Collider collider, bool allowTrigger)
    {
        return collider != null
            && collider.enabled
            && collider.gameObject.activeInHierarchy
            && (allowTrigger || !collider.isTrigger);
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
