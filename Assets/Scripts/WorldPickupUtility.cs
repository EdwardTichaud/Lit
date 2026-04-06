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
        Collider trigger = EnsureRootTriggerCollider(root);
        InteractableItem container = NetcodeRuntimeUtilities.GetOrAdd<InteractableItem>(root);
        if (trigger != null)
        {
            container.interactionTrigger = trigger;
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
        container.lootItems = new List<InteractableItem.LootItemEntry>
        {
            new InteractableItem.LootItemEntry { item = item, quantity = clampedQuantity }
        };
        container.containerItem = displayItem != null ? displayItem : item;
        container.destroyWhenEmpty = destroyWhenEmpty;
        container.collectable = collectable;

        Collider resolvedTrigger = ResolveInteractionTrigger(container, preferredTrigger);
        if (resolvedTrigger != null)
        {
            container.interactionTrigger = resolvedTrigger;
        }

        container.RefreshRecoverableWorldInfo();
    }

    public static Collider EnsureTriggerCollider(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.isTrigger || IsConcaveMeshCollider(collider))
                {
                    continue;
                }

                return collider;
            }
        }

        return EnsureRootTriggerCollider(root);
    }

    public static BoxCollider EnsureRootTriggerCollider(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        if (!TryCalculateBounds(root, out Bounds bounds))
        {
            bounds = new Bounds(root.transform.position, Vector3.one);
        }

        BoxCollider reusableRootTrigger = null;
        BoxCollider[] rootBoxes = root.GetComponents<BoxCollider>();
        for (int i = 0; i < rootBoxes.Length; i++)
        {
            BoxCollider candidate = rootBoxes[i];
            if (candidate != null && candidate.isTrigger)
            {
                reusableRootTrigger = candidate;
                break;
            }
        }

        if (reusableRootTrigger == null)
        {
            reusableRootTrigger = root.AddComponent<BoxCollider>();
        }

        reusableRootTrigger.isTrigger = true;
        reusableRootTrigger.center = root.transform.InverseTransformPoint(bounds.center);
        reusableRootTrigger.size = bounds.size;
        return reusableRootTrigger;
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

    private static Collider ResolveInteractionTrigger(InteractableItem container, Collider preferredTrigger)
    {
        if (container == null)
        {
            return null;
        }

        if (IsValidTrigger(preferredTrigger))
        {
            return preferredTrigger;
        }

        if (IsValidTrigger(container.interactionTrigger))
        {
            return container.interactionTrigger;
        }

        return EnsureTriggerCollider(container.gameObject);
    }

    private static bool IsValidTrigger(Collider collider)
    {
        return collider != null && collider.isTrigger && !IsConcaveMeshCollider(collider);
    }

    private static bool IsConcaveMeshCollider(Collider collider)
    {
        MeshCollider meshCollider = collider as MeshCollider;
        return meshCollider != null && !meshCollider.convex;
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
