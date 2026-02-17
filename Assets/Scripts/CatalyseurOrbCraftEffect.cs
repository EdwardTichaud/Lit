using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CatalyseurOrbCraftEffect", menuName = "Scriptable Objects/Effects/Catalyseur Orb Craft")]
// Permet de convertir un item d'entree en orbe lors de l'interaction avec le batiment.
public class CatalyseurOrbCraftEffect : Effect, IBuildingInteractEffect
{
    [Header("Input")]
    [Tooltip("Item requis pour la conversion.")]
    [SerializeField] private Item inputItem;
    [Tooltip("Quantite requise pour une conversion.")]
    [SerializeField] private int inputQuantity = 5;

    [Header("Output")]
    [Tooltip("Item cree apres conversion.")]
    [SerializeField] private Item outputItem;
    [Tooltip("Quantite creee apres conversion.")]
    [SerializeField] private int outputQuantity = 1;

    public Item InputItem => inputItem;
    public int InputQuantity => inputQuantity;
    public Item OutputItem => outputItem;
    public int OutputQuantity => outputQuantity;

    public override bool Apply(SquadCharacterController controller, Item item)
    {
        return TryCraft(controller);
    }

    public bool ApplyOnInteract(SquadCharacterController controller, Item building, int currentLevel)
    {
        return TryCraft(controller);
    }

    public override string GetDescription()
    {
        if (!string.IsNullOrWhiteSpace(effectDescription))
        {
            return effectDescription;
        }

        return BuildDescription();
    }

    public override string GetDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    public override string GetBonusDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    private bool TryCraft(SquadCharacterController controller)
    {
        if (controller == null)
        {
            return false;
        }

        if (inputItem == null || outputItem == null)
        {
            return false;
        }

        int inputCount = Mathf.Max(1, inputQuantity);
        int outputCount = Mathf.Max(1, outputQuantity);
        if (!CanAddToHomeContainers(outputItem, outputCount, out List<LootContainer> containers))
        {
            return false;
        }
        if (!controller.TryRemoveItemQuantity(inputItem, inputCount))
        {
            return false;
        }

        if (!AddToHomeContainers(outputItem, outputCount, containers))
        {
            controller.AddItem(inputItem, inputCount);
            return false;
        }

        return true;
    }

    private string BuildDescription()
    {
        if (inputItem == null || outputItem == null)
        {
            return "Conversion d'orbe lumineuse";
        }

        string inputName = !string.IsNullOrWhiteSpace(inputItem.itemName) ? inputItem.itemName : inputItem.name;
        string outputName = !string.IsNullOrWhiteSpace(outputItem.itemName) ? outputItem.itemName : outputItem.name;
        int inputCount = Mathf.Max(1, inputQuantity);
        int outputCount = Mathf.Max(1, outputQuantity);
        return $"{inputCount} {inputName} -> {outputCount} {outputName}";
    }

    private bool CanAddToHomeContainers(Item item, int quantity, out List<LootContainer> containers)
    {
        containers = ResolveHomeContainers();
        if (item == null || quantity <= 0 || containers == null || containers.Count == 0)
        {
            return false;
        }

        Maison maison = ResolveMaison();
        if (maison != null)
        {
            maison.EnsureHomeContainers(containers);
        }

        int remaining = GetTotalRemainingCapacity(containers);
        return remaining >= quantity;
    }

    private bool AddToHomeContainers(Item item, int quantity, List<LootContainer> containers)
    {
        if (item == null || quantity <= 0 || containers == null || containers.Count == 0)
        {
            return false;
        }

        int remaining = quantity;
        for (int i = 0; i < containers.Count && remaining > 0; i++)
        {
            LootContainer container = containers[i];
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

            container.AddItems(item, toAdd);
            remaining -= toAdd;
        }

        return remaining <= 0;
    }

    private List<LootContainer> ResolveHomeContainers()
    {
        Maison maison = ResolveMaison();
        if (maison == null)
        {
            return null;
        }

        List<LootContainer> containers = maison.ResolveMaisonLootContainers(null);
        return containers != null && containers.Count > 0 ? containers : null;
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

            total += remaining;
        }

        return total;
    }

    private Maison ResolveMaison()
    {
        Maison maison = Maison.Instance;
        if (maison != null)
        {
            return maison;
        }

#if UNITY_2023_1_OR_NEWER
        maison = Object.FindFirstObjectByType<Maison>();
#else
        maison = Object.FindObjectOfType<Maison>();
#endif
        return maison;
    }
}
