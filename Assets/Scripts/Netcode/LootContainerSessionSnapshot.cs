using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public class LootContainerSessionSnapshot
{
    public List<LootContainerSessionSnapshotEntry> containers = new List<LootContainerSessionSnapshotEntry>();

    public static string CaptureJson()
    {
        LootContainerSessionSnapshot snapshot = new LootContainerSessionSnapshot();
        List<LootContainer> containers = FindAllContainers();
        for (int i = 0; i < containers.Count; i++)
        {
            LootContainer container = containers[i];
            if (container == null)
            {
                continue;
            }

            snapshot.containers.Add(BuildEntry(container));
        }

        return JsonUtility.ToJson(snapshot);
    }

    public static bool TryApplyJson(string json, out int appliedCount, out int unresolvedCount)
    {
        appliedCount = 0;
        unresolvedCount = 0;

        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        LootContainerSessionSnapshot snapshot = JsonUtility.FromJson<LootContainerSessionSnapshot>(json);
        if (snapshot == null || snapshot.containers == null || snapshot.containers.Count == 0)
        {
            return true;
        }

        Dictionary<ulong, LootContainer> byNetworkId = new Dictionary<ulong, LootContainer>();
        Dictionary<uint, LootContainer> bySceneId = new Dictionary<uint, LootContainer>();
        List<LootContainer> containers = FindAllContainers();
        for (int i = 0; i < containers.Count; i++)
        {
            LootContainer container = containers[i];
            if (container == null)
            {
                continue;
            }

            NetworkObject networkObject = ResolveNetworkObject(container);
            if (networkObject != null && networkObject.IsSpawned && !byNetworkId.ContainsKey(networkObject.NetworkObjectId))
            {
                byNetworkId.Add(networkObject.NetworkObjectId, container);
            }

            uint sceneId = NetcodeSceneIdUtility.GetStableId(container.transform);
            if (sceneId != 0u && !bySceneId.ContainsKey(sceneId))
            {
                bySceneId.Add(sceneId, container);
            }
        }

        for (int i = 0; i < snapshot.containers.Count; i++)
        {
            LootContainerSessionSnapshotEntry entry = snapshot.containers[i];
            if (entry == null)
            {
                continue;
            }

            LootContainer container = ResolveContainer(entry, byNetworkId, bySceneId);
            if (container == null)
            {
                unresolvedCount++;
                continue;
            }

            ApplyEntry(container, entry);
            appliedCount++;
        }

        return unresolvedCount == 0;
    }

    private static LootContainerSessionSnapshotEntry BuildEntry(LootContainer container)
    {
        LootContainerSessionSnapshotEntry entry = new LootContainerSessionSnapshotEntry();
        NetworkObject networkObject = ResolveNetworkObject(container);
        if (networkObject != null && networkObject.IsSpawned)
        {
            entry.networkObjectId = networkObject.NetworkObjectId;
        }

        entry.sceneId = NetcodeSceneIdUtility.GetStableId(container.transform);
        entry.containerItemId = ItemIdUtils.GetItemId(container.containerItem);
        entry.destroyWhenEmpty = container.destroyWhenEmpty;
        entry.collectable = container.collectable;
        entry.maxTotalQuantity = container.maxTotalQuantity;

        if (container.lootItems != null)
        {
            for (int i = 0; i < container.lootItems.Count; i++)
            {
                LootContainer.LootItemEntry lootEntry = container.lootItems[i];
                if (lootEntry == null || lootEntry.item == null)
                {
                    continue;
                }

                int quantity = Mathf.Max(0, lootEntry.quantity);
                if (quantity <= 0)
                {
                    continue;
                }

                string itemId = ItemIdUtils.GetItemId(lootEntry.item);
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    continue;
                }

                entry.items.Add(new ItemStackData
                {
                    itemId = itemId,
                    quantity = quantity
                });
            }
        }

        return entry;
    }

    private static void ApplyEntry(LootContainer container, LootContainerSessionSnapshotEntry entry)
    {
        if (container == null || entry == null)
        {
            return;
        }

        container.collectable = entry.collectable;
        container.destroyWhenEmpty = entry.destroyWhenEmpty;
        container.maxTotalQuantity = entry.maxTotalQuantity;

        if (!string.IsNullOrWhiteSpace(entry.containerItemId))
        {
            Item resolvedContainerItem = ItemRegistry.Resolve(entry.containerItemId);
            if (resolvedContainerItem != null)
            {
                container.containerItem = resolvedContainerItem;
            }
        }

        List<LootContainer.LootItemEntry> lootItems = new List<LootContainer.LootItemEntry>();
        if (entry.items != null)
        {
            for (int i = 0; i < entry.items.Count; i++)
            {
                ItemStackData stack = entry.items[i];
                if (stack == null || string.IsNullOrWhiteSpace(stack.itemId) || stack.quantity <= 0)
                {
                    continue;
                }

                Item item = ItemRegistry.Resolve(stack.itemId);
                if (item == null)
                {
                    continue;
                }

                lootItems.Add(new LootContainer.LootItemEntry
                {
                    item = item,
                    quantity = stack.quantity
                });
            }
        }

        container.SetLootItems(lootItems, true);
    }

    private static LootContainer ResolveContainer(
        LootContainerSessionSnapshotEntry entry,
        Dictionary<ulong, LootContainer> byNetworkId,
        Dictionary<uint, LootContainer> bySceneId)
    {
        if (entry == null)
        {
            return null;
        }

        if (entry.networkObjectId != 0ul && byNetworkId.TryGetValue(entry.networkObjectId, out LootContainer byNetwork))
        {
            return byNetwork;
        }

        if (entry.sceneId != 0u && bySceneId.TryGetValue(entry.sceneId, out LootContainer byScene))
        {
            return byScene;
        }

        return null;
    }

    private static NetworkObject ResolveNetworkObject(LootContainer container)
    {
        if (container == null)
        {
            return null;
        }

        NetworkObject direct = container.GetComponent<NetworkObject>();
        if (direct != null && direct.IsSpawned)
        {
            return direct;
        }

        Transform current = container.transform.parent;
        while (current != null)
        {
            NetworkObject parent = current.GetComponent<NetworkObject>();
            if (parent != null)
            {
                return parent;
            }

            current = current.parent;
        }

        return direct;
    }

    private static List<LootContainer> FindAllContainers()
    {
        List<LootContainer> results = new List<LootContainer>();
#if UNITY_2023_1_OR_NEWER
        LootContainer[] found = UnityEngine.Object.FindObjectsByType<LootContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        LootContainer[] found = UnityEngine.Object.FindObjectsOfType<LootContainer>(true);
#endif
        if (found == null)
        {
            return results;
        }

        for (int i = 0; i < found.Length; i++)
        {
            LootContainer container = found[i];
            if (container == null || results.Contains(container))
            {
                continue;
            }

            results.Add(container);
        }

        return results;
    }
}

[Serializable]
public class LootContainerSessionSnapshotEntry
{
    public ulong networkObjectId;
    public uint sceneId;
    public string containerItemId;
    public bool destroyWhenEmpty;
    public bool collectable;
    public int maxTotalQuantity;
    public List<ItemStackData> items = new List<ItemStackData>();
}
