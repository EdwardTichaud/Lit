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

        return IsUsableCollider(preferred, true) ? preferred : null;
    }

    public static Vector3 GetInteractionPoint(Collider collider, Transform anchor, Vector3 origin)
    {
        if (collider != null)
        {
            Vector3 point = collider.ClosestPoint(origin);
            if ((point - origin).sqrMagnitude > 0.000001f)
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
