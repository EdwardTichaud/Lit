// Role:
// Consumable item effect that adds remaining torch time.
// Usage:
// Assigned to an item that should be consumed to recharge the torch.
// Responsibilities:
// Validate that the character owns a torch, remove one source item, then add time.
// Dependencies:
// Effect, SquadCharacterController, Item.
// Precautions:
// Removing the item before adding time is intentional; avoid reordering unless you
// also audit inventory/network failure handling.
using UnityEngine;

/// <summary>
/// Adds torch seconds by consuming the source item.
/// </summary>
[CreateAssetMenu(fileName = "IncreaseTorchRemaining", menuName = "Scriptable Objects/Effects/Increase Torch Remaining")]
public class IncreaseTorchRemaining : Effect
{
    [Header("Settings")]
    /// <summary>Seconds added per successful use.</summary>
    [Tooltip("Secondes ajoutees par application de l'effet.")]
    [SerializeField] private int addedSeconds = 60;

    /// <summary>
    /// Consumes one item and adds torch time if the controller has a torch.
    /// </summary>
    public override bool Apply(SquadCharacterController controller, Item item)
    {
        if (controller == null || item == null)
        {
            return false;
        }

        if (!controller.HasTorchItem)
        {
            return false;
        }

        if (!controller.TryRemoveItem(item, 1))
        {
            return false;
        }

        controller.AddTorchSeconds(addedSeconds);
        return true;
    }

    /// <summary>Returns a UI description for this fixed recharge amount.</summary>
    public override string GetDescriptionForLevel(int level)
    {
        int value = Mathf.Max(1, addedSeconds);
        return $"+{value}s torche";
    }

    /// <summary>Returns a short bonus text for this fixed recharge amount.</summary>
    public override string GetBonusDescriptionForLevel(int level)
    {
        int value = Mathf.Max(1, addedSeconds);
        return $"+{value}s";
    }
}
