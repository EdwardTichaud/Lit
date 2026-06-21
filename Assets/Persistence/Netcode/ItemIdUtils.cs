using UnityEngine;

// Utilitaires pour resoudre les IDs d'items de maniere consistante.
public static class ItemIdUtils
{
    public static string GetItemId(Item item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(item.itemId))
        {
            return item.itemId;
        }

        if (!string.IsNullOrWhiteSpace(item.itemName))
        {
            return item.itemName;
        }

        return item.name;
    }
}
