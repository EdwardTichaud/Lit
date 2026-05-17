// Role:
// Building interaction effect that converts an input item into an output orb/item.
// Usage:
// Assigned to a Catalyseur building effect and triggered when the player interacts with it.
// Responsibilities:
// Remove required input quantity and add configured output quantity.
// Dependencies:
// Effect, IBuildingInteractEffect, SquadCharacterController inventory methods, Item.
// Precautions:
// This performs direct inventory mutation. Keep validation before removal.
using UnityEngine;

/// <summary>
/// Converts one inventory resource into another when used through a catalyseur.
/// </summary>
[CreateAssetMenu(fileName = "CatalyseurOrbCraftEffect", menuName = "Scriptable Objects/Effects/Catalyseur Orb Craft")]
public class CatalyseurOrbCraftEffect : Effect, IBuildingInteractEffect
{
    [Header("Input")]
    /// <summary>Item consumed by the conversion.</summary>
    [Tooltip("Item requis pour la conversion.")]
    [SerializeField] private Item inputItem;
    /// <summary>Quantity consumed per conversion.</summary>
    [Tooltip("Quantite requise pour une conversion.")]
    [SerializeField] private int inputQuantity = 5;

    [Header("Output")]
    /// <summary>Item granted by the conversion.</summary>
    [Tooltip("Item cree apres conversion.")]
    [SerializeField] private Item outputItem;
    /// <summary>Quantity granted per conversion.</summary>
    [Tooltip("Quantite creee apres conversion.")]
    [SerializeField] private int outputQuantity = 1;

    /// <summary>Input item configured for this conversion.</summary>
    public Item InputItem => inputItem;
    /// <summary>Input quantity configured for this conversion.</summary>
    public int InputQuantity => inputQuantity;
    /// <summary>Output item configured for this conversion.</summary>
    public Item OutputItem => outputItem;
    /// <summary>Output quantity configured for this conversion.</summary>
    public int OutputQuantity => outputQuantity;

    /// <summary>
    /// Applies the conversion as a direct item use.
    /// </summary>
    public override bool Apply(SquadCharacterController controller, Item item)
    {
        return TryCraft(controller);
    }

    /// <summary>
    /// Applies the conversion when interacting with a building.
    /// </summary>
    public bool ApplyOnInteract(SquadCharacterController controller, Item building, int currentLevel)
    {
        return TryCraft(controller);
    }

    /// <summary>Returns custom text or a generated conversion recipe.</summary>
    public override string GetDescription()
    {
        if (!string.IsNullOrWhiteSpace(effectDescription))
        {
            return effectDescription;
        }

        return BuildDescription();
    }

    /// <summary>Returns the same description because this effect does not scale by level.</summary>
    public override string GetDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    /// <summary>Returns the same bonus text because this effect does not scale by level.</summary>
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
        if (!controller.TryRemoveItemQuantity(inputItem, inputCount))
        {
            return false;
        }

        // Add output only after the input removal succeeds.
        controller.AddItem(outputItem, outputCount);
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

}
