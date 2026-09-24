using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Verification manuelle des references indispensables a l'UI inventaire.</summary>
public static class InventoryUiAudit
{
    [MenuItem("Tools/Lit/UI/Audit Inventory UI")]
    private static void Run()
    {
        int issues = 0;
        InventoryUISettings[] inventories = Object.FindObjectsByType<InventoryUISettings>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        LootUISettings[] loots = Object.FindObjectsByType<LootUISettings>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < inventories.Length; i++)
        {
            InventoryUISettings settings = inventories[i];
            if (settings.inventoryPanel == null || settings.inventoryPanel.GetComponent<CanvasGroup>() == null ||
                settings.itemsParent == null || !ValidateInventorySlotPrefab(settings.itemPrefab))
            {
                issues++;
                Debug.LogError($"[Inventory UI Audit] References invalides sur {settings.name}.", settings);
            }
        }

        for (int i = 0; i < loots.Length; i++)
        {
            LootUISettings settings = loots[i];
            if (settings.lootPanel == null || settings.lootPanel.GetComponent<CanvasGroup>() == null ||
                settings.lootItemsParent == null || !ValidateLootSlotPrefab(settings.lootItemPrefab))
            {
                issues++;
                Debug.LogError($"[Inventory UI Audit] References invalides sur {settings.name}.", settings);
            }
        }

        InventoryPanelController[] controllers = Object.FindObjectsByType<InventoryPanelController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            InventoryPanelController controller = controllers[i];
            if (controller.actionBox == null || controller.actionBox.GetComponent<CanvasGroup>() == null)
            {
                issues++;
                Debug.LogError($"[Inventory UI Audit] ActionBox invalide sur {controller.name}.", controller);
            }
        }

        QuantityBox[] quantityBoxes = Object.FindObjectsByType<QuantityBox>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < quantityBoxes.Length; i++)
        {
            if (quantityBoxes[i].GetComponent<CanvasGroup>() == null)
            {
                issues++;
                Debug.LogError($"[Inventory UI Audit] CanvasGroup manquant sur {quantityBoxes[i].name}.", quantityBoxes[i]);
            }
        }

        if (issues == 0) Debug.Log("[Inventory UI Audit] Configuration valide.");
    }

    private static bool ValidateInventorySlotPrefab(GameObject prefab) =>
        prefab != null && prefab.GetComponent<InventorySlotUI>() != null;

    private static bool ValidateLootSlotPrefab(GameObject prefab) =>
        prefab != null && prefab.GetComponent<LootSlotUI>() != null && ValidateCursor(prefab.transform);

    private static bool ValidateCursor(Transform root)
    {
        Transform cursor = root.Find("Cursor");
        if (cursor == null) return false;
        Graphic[] graphics = cursor.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null && graphics[i].raycastTarget) return false;
        }
        return true;
    }
}
