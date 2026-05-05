using UnityEngine;

public partial class SquadCharacterController
{
    public int GetStatValue(StatType stat)
    {
        return characterData != null ? characterData.GetStatValue(stat) : 10;
    }

    public int GetDexterityValue()
    {
        return GetStatValue(StatType.Dexterity);
    }

    public int GetDexterityModifier()
    {
        return global::CharacterData.GetStatModifier(GetDexterityValue());
    }

    public int CountItemById(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        EnsureInventoryList();
        MarkInventoryInitialized();

        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null)
            {
                continue;
            }

            if (string.Equals(ItemIdUtils.GetItemId(item), itemId, System.StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    public bool HasItemById(string itemId, int quantity = 1)
    {
        return CountItemById(itemId) >= Mathf.Max(1, quantity);
    }

    public int CountItem(Item item)
    {
        if (item == null)
        {
            return 0;
        }

        EnsureInventoryList();
        MarkInventoryInitialized();

        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == item)
            {
                count++;
            }
        }

        return count;
    }

    public bool TryFindInventoryItemById(string itemId, out Item item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        EnsureInventoryList();
        MarkInventoryInitialized();

        for (int i = 0; i < items.Count; i++)
        {
            Item candidate = items[i];
            if (candidate == null)
            {
                continue;
            }

            if (!string.Equals(ItemIdUtils.GetItemId(candidate), itemId, System.StringComparison.Ordinal))
            {
                continue;
            }

            item = candidate;
            return true;
        }

        return false;
    }

    public bool TryConsumeItemById(string itemId, int quantity, out Item consumedItem)
    {
        consumedItem = null;
        int remaining = Mathf.Max(0, quantity);
        if (string.IsNullOrWhiteSpace(itemId) || remaining <= 0)
        {
            return false;
        }

        if (CountItemById(itemId) < remaining)
        {
            return false;
        }

        while (remaining > 0)
        {
            if (!TryFindInventoryItemById(itemId, out Item itemToConsume))
            {
                return false;
            }

            if (!TryRemoveItemQuantity(itemToConsume, 1))
            {
                return false;
            }

            consumedItem = itemToConsume;
            remaining--;
        }

        return consumedItem != null;
    }

}
