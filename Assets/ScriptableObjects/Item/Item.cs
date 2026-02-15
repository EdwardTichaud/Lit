using System.Collections.Generic;
using UnityEngine;

// Donnees d'un item utilisees par l'inventaire et le loot.
[CreateAssetMenu(fileName = "Item", menuName = "Scriptable Objects/Item")]
public class Item : ScriptableObject
{
    [System.Serializable]
    public class BreakResult
    {
        [Tooltip("Item obtenu apres casse.")]
        public Item item;
        [Tooltip("Quantite obtenue.")]
        public int quantity = 1;
    }

    [System.Serializable]
    public class BuildingRequirement
    {
        [Tooltip("Item requis pour construire/ameliorer.")]
        public Item item;
        [Tooltip("Quantite requise par niveau.")]
        public int quantity = 1;
    }

    [Header("Identity")]
    [Tooltip("Identifiant unique (optionnel).")]
    public string itemId;
    [Tooltip("Nom affiche dans l'UI.")]
    public string itemName;

    [Header("UI")]
    [Tooltip("Sprite utilise pour les apercus/illustrations.")]
    public Sprite itemSprite;
    [TextArea]
    [Tooltip("Description affichee dans l'UI.")]
    public string description;

    [Header("Usage")]
    [Tooltip("Peut etre utilise via l'ActionBox.")]
    public bool canUse;
    [Tooltip("Peut etre pose dans le monde.")]
    public bool canPlace;
    [Tooltip("Est un container (coffre, sac, etc.).")]
    public bool isContainer;
    [Tooltip("Prefab a instancier lors de la pose.")]
    public GameObject worldPrefab;
    [Tooltip("Item special torche.")]
    public bool isTorch;
    [Header("Inventory Rules")]
    [Tooltip("Autorise le drop depuis l'inventaire.")]
    public bool allowDropFromInventory = true;
    [Tooltip("Autorise le placement depuis l'inventaire.")]
    public bool allowPlaceFromInventory = true;
    [Tooltip("Autorise le drop meme sans prefab de monde.")]
    public bool allowInstantDropWithoutPrefab = true;
    [Tooltip("Effets additionnels appliques lors de l'utilisation.")]
    public List<Effect> useEffects = new List<Effect>();
    [Tooltip("Si true, tous les effets doivent reussir pour valider l'utilisation.")]
    public bool requireAllUseEffects = false;
    [Tooltip("Consomme l'item apres utilisation reussie.")]
    public bool consumeOnUse = false;
    [Min(1)]
    [Tooltip("Quantite consommee lors de l'utilisation.")]
    public int consumeQuantity = 1;
    [Tooltip("Effet passif de l'item (optionnel).")]
    public Effect itemPassiveEffect;

    [Header("Container Rules")]
    [Tooltip("Autorise le depot dans les containers.")]
    public bool allowDepositToContainers = true;
    [Tooltip("Autorise la prise depuis les containers.")]
    public bool allowTakeFromContainers = true;

    [Header("Feedback")]
    [Tooltip("Message si l'item ne peut pas etre pose.")]
    public string cannotPlaceMessage;
    [Tooltip("Message si la torche equipee empêche la pose.")]
    public string cannotPlaceWhileEquippedMessage;
    [Tooltip("Message si l'item ne peut pas etre jete.")]
    public string cannotDropMessage;
    [Tooltip("Message si l'item ne peut pas etre utilise.")]
    public string cannotUseMessage;
    [Tooltip("Message si l'utilisation echoue.")]
    public string useFailedMessage;
    [Tooltip("Message si l'item ne peut pas etre ramasse.")]
    public string cannotPickupMessage;
    [Tooltip("Message si l'item ne peut pas etre depose.")]
    public string cannotDepositMessage;
    [Tooltip("Message si l'item ne peut pas etre pris.")]
    public string cannotTakeMessage;
    [Tooltip("Message si l'item ne peut pas etre casse.")]
    public string cannotBreakMessage;
    [Tooltip("Message si la casse echoue.")]
    public string breakFailedMessage;

    [Header("Building")]
    [Tooltip("Si true, l'item est traite comme un building.")]
    public bool isBuilding = false;
    [Tooltip("Prefab instancie lors de la construction (fallback: worldPrefab).")]
    public GameObject buildingPrefab;
    [Min(1)]
    [Tooltip("Niveau maximal du building.")]
    public int buildingMaxLevel = 10;
    [Min(0)]
    [Tooltip("Niveau courant (0 = non construit).")]
    public int buildingCurrentLevel = 0;
    [Tooltip("Ressources necessaires pour construire ou ameliorer (par niveau).")]
    public List<BuildingRequirement> buildingRequirements = new List<BuildingRequirement>();
    [Tooltip("Effets appliques a chaque niveau gagne.")]
    public List<Effect> buildingEffects = new List<Effect>();
    [Tooltip("Si true, la construction est un coffre maison.")]
    public bool isHomeChest = false;

    [Header("Break")]
    [Tooltip("Peut etre casse via l'ActionBox.")]
    public bool canBreak;
    [Tooltip("Resultats de la casse.")]
    public List<BreakResult> breakResults = new List<BreakResult>();

    public bool HasBreakResults()
    {
        if (!canBreak || breakResults == null || breakResults.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < breakResults.Count; i++)
        {
            BreakResult result = breakResults[i];
            if (result != null && result.item != null && result.quantity > 0)
            {
                return true;
            }
        }

        return false;
    }

    public bool CanUse()
    {
        if (!canUse)
        {
            return false;
        }

        return useEffects != null && useEffects.Count > 0;
    }

    public bool TryUse(SquadCharacterController controller)
    {
        return TryUse(controller, out _);
    }

    public bool TryUse(SquadCharacterController controller, out string reason)
    {
        if (controller == null)
        {
            reason = ResolveMessage(cannotUseMessage, "Impossible d'utiliser cet objet.");
            return false;
        }

        if (!CanUse())
        {
            reason = ResolveMessage(cannotUseMessage, "Impossible d'utiliser cet objet.");
            return false;
        }

        bool appliedAny = false;
        bool allApplied = true;
        bool hasAnyEffect = false;

        if (useEffects != null && useEffects.Count > 0)
        {
            for (int i = 0; i < useEffects.Count; i++)
            {
                Effect effect = useEffects[i];
                if (effect == null)
                {
                    continue;
                }

                hasAnyEffect = true;
                bool applied = effect.Apply(controller, this);
                appliedAny |= applied;
                allApplied &= applied;
            }

            bool success = requireAllUseEffects ? (hasAnyEffect && allApplied) : appliedAny;
            if (success)
            {
                ConsumeAfterUse(controller);
                reason = string.Empty;
                return true;
            }
        }

        reason = ResolveMessage(useFailedMessage, "L'utilisation a echoue.");
        return false;
    }

    public bool CanBreak(out string reason)
    {
        if (!canBreak || !HasBreakResults())
        {
            reason = ResolveMessage(cannotBreakMessage, "Impossible de casser cet objet.");
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryBreak(SquadCharacterController controller, out string reason)
    {
        if (controller == null)
        {
            reason = ResolveMessage(breakFailedMessage, "Impossible de casser cet objet.");
            return false;
        }

        if (!CanBreak(out reason))
        {
            return false;
        }

        if (controller.TryBreakItem(this))
        {
            reason = string.Empty;
            return true;
        }

        reason = ResolveMessage(breakFailedMessage, "Impossible de casser cet objet.");
        return false;
    }

    public bool CanDepositToContainer(LootContainer container)
    {
        return CanDepositToContainer(container, out _);
    }

    public bool CanDepositToContainer(LootContainer container, out string reason)
    {
        if (!allowDepositToContainers)
        {
            reason = ResolveMessage(cannotDepositMessage, "Impossible de deposer cet objet.");
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool CanTakeFromContainer(LootContainer container)
    {
        return CanTakeFromContainer(container, out _);
    }

    public bool CanTakeFromContainer(LootContainer container, out string reason)
    {
        if (!allowTakeFromContainers)
        {
            string fallback = "Impossible de ramasser cet objet.";
            string custom = !string.IsNullOrWhiteSpace(cannotTakeMessage) ? cannotTakeMessage : cannotPickupMessage;
            reason = ResolveMessage(custom, fallback);
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool CanPlaceFromInventory(SquadCharacterController controller)
    {
        return CanPlaceFromInventory(controller, out _);
    }

    public bool CanPlaceFromInventory(SquadCharacterController controller, out string reason)
    {
        if (!canPlace || !allowPlaceFromInventory)
        {
            reason = ResolveMessage(cannotPlaceMessage, "Impossible de poser cet objet.");
            return false;
        }

        if (isTorch && controller != null && controller.IsTorchEquipped)
        {
            reason = ResolveMessage(cannotPlaceWhileEquippedMessage, "Impossible de poser la torche equipee.");
            return false;
        }

        if (ResolveWorldPrefab() == null)
        {
            reason = ResolveMessage(cannotPlaceMessage, "Impossible de poser cet objet.");
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool CanInstantDropFromInventory(SquadCharacterController controller, bool allowDropWithoutPrefab)
    {
        return CanInstantDropFromInventory(controller, allowDropWithoutPrefab, out _);
    }

    public bool CanInstantDropFromInventory(SquadCharacterController controller, bool allowDropWithoutPrefab, out string reason)
    {
        if (!allowDropFromInventory)
        {
            reason = ResolveMessage(cannotDropMessage, "Impossible de jeter cet objet.");
            return false;
        }

        if (isTorch)
        {
            reason = ResolveMessage(cannotDropMessage, "Impossible de jeter cet objet.");
            return false;
        }

        if (isBuilding)
        {
            reason = ResolveMessage(cannotDropMessage, "Impossible de jeter cet objet.");
            return false;
        }

        if (ResolveWorldPrefab() != null)
        {
            reason = string.Empty;
            return true;
        }

        if (allowDropWithoutPrefab && allowInstantDropWithoutPrefab)
        {
            reason = string.Empty;
            return true;
        }

        reason = ResolveMessage(cannotDropMessage, "Impossible de jeter cet objet.");
        return false;
    }

    public bool ShouldInstantDropInsteadOfPlacement(SquadCharacterController controller, bool allowDropWithoutPrefab)
    {
        if (!CanInstantDropFromInventory(controller, allowDropWithoutPrefab))
        {
            return false;
        }

        return !CanPlaceFromInventory(controller);
    }

    public GameObject ResolveWorldPrefab()
    {
        if (isBuilding && buildingPrefab != null)
        {
            return buildingPrefab;
        }

        if (worldPrefab != null)
        {
            return worldPrefab;
        }

        if (buildingPrefab != null)
        {
            return buildingPrefab;
        }

        return null;
    }

    public GameObject CreateWorldInstance(Vector3 position, Quaternion rotation)
    {
        GameObject prefab = ResolveWorldPrefab();
        if (prefab != null)
        {
            return Object.Instantiate(prefab, position, rotation);
        }

        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallback.transform.SetPositionAndRotation(position, rotation);
        fallback.transform.localScale = Vector3.one * 0.4f;
        return fallback;
    }

    public LootContainer CreateDroppedLootContainer(GameObject instance, int quantity, bool destroyWhenEmpty, bool collectable = true)
    {
        if (instance == null)
        {
            return null;
        }

        int clampedQuantity = Mathf.Max(1, quantity);
        LootContainer existing = instance.GetComponentInChildren<LootContainer>();
        if (existing != null)
        {
            existing.lootItems = new List<LootContainer.LootItemEntry>
            {
                new LootContainer.LootItemEntry { item = this, quantity = clampedQuantity }
            };
            existing.containerItem = this;
            existing.destroyWhenEmpty = destroyWhenEmpty;
            existing.collectable = collectable;
            return existing;
        }

        string baseName = !string.IsNullOrWhiteSpace(itemName) ? itemName : name;
        GameObject root = new GameObject($"Dropped_{baseName}");
        root.transform.SetPositionAndRotation(instance.transform.position, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        instance.transform.SetParent(root.transform, true);

        if (!TryCalculateBounds(instance, out Bounds bounds))
        {
            bounds = new Bounds(root.transform.position, Vector3.one);
        }

        BoxCollider trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = root.transform.InverseTransformPoint(bounds.center);
        trigger.size = bounds.size;

        LootContainer loot = root.AddComponent<LootContainer>();
        loot.lootItems = new List<LootContainer.LootItemEntry>
        {
            new LootContainer.LootItemEntry { item = this, quantity = clampedQuantity }
        };
        loot.containerItem = this;
        loot.interactionTrigger = trigger;
        loot.destroyWhenEmpty = destroyWhenEmpty;
        loot.collectable = collectable;
        return loot;
    }

    private static bool TryCalculateBounds(GameObject instance, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        if (instance == null)
        {
            return false;
        }

        bool hasBounds = false;
        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = col.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        if (hasBounds)
        {
            if (bounds.size == Vector3.zero)
            {
                bounds.size = Vector3.one;
            }
            return true;
        }

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
            bounds = new Bounds(instance.transform.position, Vector3.one);
            return false;
        }

        if (bounds.size == Vector3.zero)
        {
            bounds.size = Vector3.one;
        }

        return true;
    }

    private string ResolveMessage(string custom, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(custom))
        {
            return custom;
        }

        string display = !string.IsNullOrWhiteSpace(itemName) ? itemName : name;
        if (!string.IsNullOrWhiteSpace(display))
        {
            return $"{display} : {fallback}";
        }

        return fallback;
    }

    private void ConsumeAfterUse(SquadCharacterController controller)
    {
        if (!consumeOnUse || controller == null)
        {
            return;
        }

        int quantity = Mathf.Max(1, consumeQuantity);
        controller.TryRemoveItemQuantity(this, quantity);
    }
}
