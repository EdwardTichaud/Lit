using System.Collections.Generic;
using UnityEngine;

// Gere le stockage maison (coffres, capacite, transfert d'items).
[DisallowMultipleComponent]
public class Maison : MonoBehaviour
{
    public static Maison Instance { get; private set; }

    [Header("Maison - Stockage")]
    [Tooltip("Coffre maison principal.")]
    public LootContainer maisonLootContainer;
    [Tooltip("Tag utilise pour trouver les coffres maison.")]
    public string maisonChestTag = "MaisonChest";
    [Tooltip("Capacite max par coffre maison.")]
    public int maisonChestCapacity = 100;
    [Tooltip("Force les coffres maison en non-collectables.")]
    public bool forceMaisonChestNonCollectable = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            return;
        }

        Instance = this;
    }

    public List<LootContainer> ResolveMaisonLootContainers(LootContainer preferred)
    {
        List<LootContainer> results = new List<LootContainer>();
        AddUnique(results, preferred);
        AddUnique(results, maisonLootContainer);

        if (!string.IsNullOrWhiteSpace(maisonChestTag))
        {
            try
            {
                GameObject[] found = GameObject.FindGameObjectsWithTag(maisonChestTag);
                if (found != null)
                {
                    for (int i = 0; i < found.Length; i++)
                    {
                        LootContainer container = found[i] != null ? found[i].GetComponent<LootContainer>() : null;
                        AddUnique(results, container);
                    }
                }
            }
            catch (UnityException)
            {
                // Tag not defined, ignore.
            }
        }

        return results;
    }

    public void EnsureHomeContainers(List<LootContainer> containers)
    {
        if (containers == null)
        {
            return;
        }

        for (int i = 0; i < containers.Count; i++)
        {
            EnsureHomeChestDefaults(containers[i]);
        }
    }

    public void EnsureHomeChestDefaults(LootContainer container)
    {
        if (container == null)
        {
            return;
        }

        if (container.maxTotalQuantity <= 0 && maisonChestCapacity > 0)
        {
            container.maxTotalQuantity = maisonChestCapacity;
        }

        if (forceMaisonChestNonCollectable)
        {
            container.collectable = false;
        }
    }

    public bool TransferNonTorchItemsToHome(GameObject character, List<LootContainer> homeLootContainers)
    {
        if (character == null)
        {
            return true;
        }

        if (homeLootContainers == null || homeLootContainers.Count == 0)
        {
            return true;
        }

        SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            return true;
        }

        if (!TryCollectNonTorchItemCounts(controller, out Dictionary<Item, int> counts, out int totalCount))
        {
            return true;
        }

        EnsureHomeContainers(homeLootContainers);

        int totalCapacity = GetTotalRemainingCapacity(homeLootContainers);
        if (totalCapacity < totalCount)
        {
            return false;
        }

        foreach (KeyValuePair<Item, int> pair in counts)
        {
            if (pair.Key == null || pair.Value <= 0)
            {
                continue;
            }

            if (!controller.TryRemoveItemQuantity(pair.Key, pair.Value))
            {
                continue;
            }

            int remaining = pair.Value;
            for (int i = 0; i < homeLootContainers.Count && remaining > 0; i++)
            {
                LootContainer container = homeLootContainers[i];
                if (container == null)
                {
                    continue;
                }

                int available = container.GetRemainingCapacity();
                if (available <= 0)
                {
                    continue;
                }

                int toAdd = available == int.MaxValue ? remaining : Mathf.Min(available, remaining);
                if (toAdd <= 0)
                {
                    continue;
                }

                container.AddItems(pair.Key, toAdd);
                remaining -= toAdd;
            }
        }

        return true;
    }

    public bool HasHomeStorageForCharacter(GameObject character, List<LootContainer> homeLootContainers)
    {
        if (character == null)
        {
            return true;
        }

        if (homeLootContainers == null || homeLootContainers.Count == 0)
        {
            return true;
        }

        SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            return true;
        }

        if (!TryCollectNonTorchItemCounts(controller, out Dictionary<Item, int> _, out int totalCount))
        {
            return true;
        }

        EnsureHomeContainers(homeLootContainers);
        int totalCapacity = GetTotalRemainingCapacity(homeLootContainers);
        return totalCapacity >= totalCount;
    }

    private bool TryCollectNonTorchItemCounts(SquadCharacterController controller, out Dictionary<Item, int> counts, out int totalCount)
    {
        counts = new Dictionary<Item, int>();
        totalCount = 0;

        if (controller == null)
        {
            return false;
        }

        IReadOnlyList<Item> items = controller.Items;
        if (items == null || items.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null || item.isTorch)
            {
                continue;
            }

            if (!counts.TryGetValue(item, out int count))
            {
                counts[item] = 1;
            }
            else
            {
                counts[item] = count + 1;
            }
        }

        foreach (KeyValuePair<Item, int> pair in counts)
        {
            if (pair.Value > 0)
            {
                totalCount += pair.Value;
            }
        }

        return totalCount > 0;
    }

    private int GetTotalRemainingCapacity(List<LootContainer> containers)
    {
        if (containers == null || containers.Count == 0)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < containers.Count; i++)
        {
            LootContainer container = containers[i];
            if (container == null)
            {
                continue;
            }

            int remaining = container.GetRemainingCapacity();
            if (remaining == int.MaxValue)
            {
                return int.MaxValue;
            }

            total = Mathf.Min(int.MaxValue, total + Mathf.Max(0, remaining));
        }

        return total;
    }

    private void AddUnique(List<LootContainer> list, LootContainer container)
    {
        if (container == null || list == null)
        {
            return;
        }

        if (list.Contains(container))
        {
            return;
        }

        list.Add(container);
    }
}
