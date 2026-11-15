using System.Collections.Generic;
using UnityEngine;

// Relie le Container de Maison aux transferts et aux recompenses de cycle.
[DisallowMultipleComponent]
[RequireComponent(typeof(InteractableItem))]
public sealed class MaisonChest : MonoBehaviour
{
    private static readonly List<MaisonChest> active = new();
    public static MaisonChest Instance { get; private set; }

    [Header("Maison - Stockage")]
    [Tooltip("Coffre maison principal.")]
    public InteractableItem maisonLootContainer;
    [Tooltip("Tag utilise pour trouver les coffres maison.")]
    public string maisonChestTag = "MaisonChest";
    [Tooltip("Capacite max par coffre maison.")]
    public int maisonChestCapacity = 100;
    private void OnEnable()
    {
        if (maisonLootContainer == null) maisonLootContainer = GetComponent<InteractableItem>();
        active.Add(this);
        if (Instance == null) Instance = this;
    }

    private void OnDisable()
    {
        active.Remove(this);
        if (Instance == this) Instance = active.Count > 0 ? active[0] : null;
    }

    public List<InteractableItem> ResolveMaisonLootContainers(InteractableItem preferred)
    {
        List<InteractableItem> results = new List<InteractableItem>();
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
                        InteractableItem container = found[i] != null ? found[i].GetComponent<InteractableItem>() : null;
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

    public void EnsureHomeContainers(List<InteractableItem> containers)
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

    public void EnsureHomeChestDefaults(InteractableItem container)
    {
        if (container == null)
        {
            return;
        }

        if (container != maisonLootContainer && container.maxStoredQuantity <= 0 && maisonChestCapacity > 0)
        {
            container.maxStoredQuantity = maisonChestCapacity;
        }

    }

    public bool TransferNonFlameItemsToHome(GameObject character, List<InteractableItem> homeLootContainers)
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

        if (!TryCollectNonFlameItemCounts(controller, out Dictionary<Item, int> counts, out int totalCount))
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
                InteractableItem container = homeLootContainers[i];
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

    public bool HasHomeStorageForCharacter(GameObject character, List<InteractableItem> homeLootContainers)
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

        if (!TryCollectNonFlameItemCounts(controller, out Dictionary<Item, int> _, out int totalCount))
        {
            return true;
        }

        EnsureHomeContainers(homeLootContainers);
        int totalCapacity = GetTotalRemainingCapacity(homeLootContainers);
        return totalCapacity >= totalCount;
    }

    private bool TryCollectNonFlameItemCounts(SquadCharacterController controller, out Dictionary<Item, int> counts, out int totalCount)
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
            if (item == null || item.isFlame)
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

    private int GetTotalRemainingCapacity(List<InteractableItem> containers)
    {
        if (containers == null || containers.Count == 0)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < containers.Count; i++)
        {
            InteractableItem container = containers[i];
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

    private void AddUnique(List<InteractableItem> list, InteractableItem container)
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
