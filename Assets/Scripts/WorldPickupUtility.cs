using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Utilitaires partages pour configurer un pickup simple dans le monde.
public static class WorldPickupUtility
{
    public static InteractableItem EnsurePickupInfrastructure(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        NetcodeRuntimeUtilities.GetOrAdd<NetworkObject>(root);
        InteractableItem container = NetcodeRuntimeUtilities.GetOrAdd<InteractableItem>(root);
        RuntimeOutlineUtility.EnsureOutlineTargets(root);
        Collider interactionCollider = EnsureInteractionColliderInternal(root, container, null);
        if (interactionCollider != null)
        {
            container.interactionTrigger = interactionCollider;
        }

        return container;
    }

    public static InteractableItem ConfigurePickupOnRoot(
        GameObject root,
        Item item,
        int quantity,
        bool destroyWhenEmpty,
        bool collectable = true,
        Item displayItem = null,
        Collider preferredTrigger = null)
    {
        InteractableItem container = EnsurePickupInfrastructure(root);
        ConfigureLootContainer(container, item, quantity, destroyWhenEmpty, collectable, displayItem, preferredTrigger);
        return container;
    }

    public static InteractableItem CreateOrConfigureDroppedPickup(
        GameObject instance,
        Item item,
        int quantity,
        bool destroyWhenEmpty,
        bool collectable = true,
        Item displayItem = null)
    {
        if (instance == null)
        {
            return null;
        }

        InteractableItem existing = instance.GetComponentInChildren<InteractableItem>(true);
        if (existing != null)
        {
            ConfigureLootContainer(existing, item, quantity, destroyWhenEmpty, collectable, displayItem);
            return existing;
        }

        string baseName = ResolvePickupName(item, instance.name);
        GameObject root = new GameObject($"Dropped_{baseName}");
        root.transform.SetPositionAndRotation(instance.transform.position, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        instance.transform.SetParent(root.transform, true);

        return ConfigurePickupOnRoot(root, item, quantity, destroyWhenEmpty, collectable, displayItem);
    }

    public static void ConfigureLootContainer(
        InteractableItem container,
        Item item,
        int quantity,
        bool destroyWhenEmpty,
        bool collectable = true,
        Item displayItem = null,
        Collider preferredTrigger = null)
    {
        if (container == null || item == null)
        {
            return;
        }

        int clampedQuantity = Mathf.Max(1, quantity);
        container.storedItems = new List<InteractableItem.LootItemEntry>
        {
            new InteractableItem.LootItemEntry { item = item, quantity = clampedQuantity }
        };
        container.interactableCategory = InteractableItem.InteractableCategory.RecoverableItem;
        container.representedItem = displayItem != null ? displayItem : item;
        container.destroyWhenStorageEmpty = destroyWhenEmpty;
        container.allowTake = collectable;

        Collider resolvedCollider = EnsureInteractionColliderInternal(container != null ? container.gameObject : null, container, preferredTrigger);
        if (resolvedCollider != null)
        {
            container.interactionTrigger = resolvedCollider;
        }

        container.RefreshRecoverableWorldInfo();
    }

    public static Collider EnsureInteractionCollider(GameObject root)
    {
        return EnsureInteractionColliderInternal(root, root != null ? root.GetComponentInChildren<InteractableItem>(true) : null, null);
    }

    public static BoxCollider EnsureRootInteractionBoxCollider(GameObject root)
    {
        return EnsureInteractionCollider(root) as BoxCollider;
    }

    public static bool TryCalculateBounds(GameObject instance, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        if (instance == null)
        {
            return false;
        }

        bool hasBounds = false;
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
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
            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
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
            bounds.size = Vector3.one;
        }

        return hasBounds;
    }

    private static Collider ResolveInteractionCollider(InteractableItem container, Collider preferredCollider)
    {
        if (container == null)
        {
            return null;
        }

        return CharacterInteractionDetection.ResolveInteractionCollider(container, preferredCollider != null ? preferredCollider : container.interactionTrigger);
    }

    private static Collider EnsureInteractionColliderInternal(GameObject root, InteractableItem container, Collider preferredCollider)
    {
        Collider resolved = ResolveInteractionCollider(container, preferredCollider);
        if (resolved != null)
        {
            return resolved;
        }

        BoxCollider fallback = CreateFallbackBoxCollider(root);
        if (fallback == null)
        {
            return resolved;
        }

        if (container != null)
        {
            container.interactionTrigger = fallback;
        }

        return fallback;
    }

    private static BoxCollider CreateFallbackBoxCollider(GameObject root)
    {
        if (root == null || !TryCalculateBounds(root, out Bounds bounds))
        {
            return null;
        }

        BoxCollider box = root.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = root.AddComponent<BoxCollider>();
        }

        Transform rootTransform = root.transform;
        Vector3 localCenter = rootTransform.InverseTransformPoint(bounds.center);
        Vector3 localSize = rootTransform.InverseTransformVector(bounds.size);
        box.center = localCenter;
        box.size = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(localSize.x)),
            Mathf.Max(0.01f, Mathf.Abs(localSize.y)),
            Mathf.Max(0.01f, Mathf.Abs(localSize.z)));
        box.isTrigger = false;
        return box;
    }

    private static string ResolvePickupName(Item item, string fallback)
    {
        if (item != null)
        {
            if (!string.IsNullOrWhiteSpace(item.itemName))
            {
                return item.itemName;
            }

            if (!string.IsNullOrWhiteSpace(item.name))
            {
                return item.name;
            }
        }

        return string.IsNullOrWhiteSpace(fallback) ? "Pickup" : fallback;
    }
}
