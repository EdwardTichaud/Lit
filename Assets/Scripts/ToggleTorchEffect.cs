// Role:
// Item effect that toggles the character torch on or off.
// Usage:
// Assigned to the torch item effect asset.
// Responsibilities:
// Validate the controller and source item, then delegate the actual toggle to the character.
// Dependencies:
// Effect, SquadCharacterController, Item.
// Precautions:
// The torch state may be synchronized by inventory/network systems; keep this effect small.
using UnityEngine;

/// <summary>
/// Toggles the torch equipped state for a character.
/// </summary>
[CreateAssetMenu(fileName = "ToggleTorchEffect", menuName = "Scriptable Objects/Effects/Toggle Torch")]
public class ToggleTorchEffect : Effect
{
    /// <summary>
    /// Toggles the torch and returns true only if the equipped state changed.
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

        bool wasEquipped = controller.IsTorchEquipped;
        controller.ToggleTorch();
        return controller.IsTorchEquipped != wasEquipped;
    }

    /// <summary>Returns the level-independent torch toggle description.</summary>
    public override string GetDescriptionForLevel(int level)
    {
        return "Allume/eteint la torche";
    }

    /// <summary>Returns the level-independent torch toggle bonus text.</summary>
    public override string GetBonusDescriptionForLevel(int level)
    {
        return "Allume/eteint la torche";
    }
}
