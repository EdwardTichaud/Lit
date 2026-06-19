// Role:
// Item effect that toggles the character flame on or off.
// Usage:
// Assigned to the flame item effect asset.
// Responsibilities:
// Validate the controller and source item, then delegate the actual toggle to the character.
// Dependencies:
// Effect, SquadCharacterController, Item.
// Precautions:
// The flame state may be synchronized by inventory/network systems; keep this effect small.
using UnityEngine;

/// <summary>
/// Toggles the flame equipped state for a character.
/// </summary>
[CreateAssetMenu(fileName = "ToggleFlameEffect", menuName = "Scriptable Objects/Effects/Toggle Flame")]
public class ToggleFlameEffect : Effect
{
    /// <summary>
    /// Toggles the flame and returns true only if the equipped state changed.
    /// </summary>
    public override bool Apply(SquadCharacterController controller, Item item)
    {
        if (controller == null || item == null)
        {
            return false;
        }

        if (!controller.HasFlameItem)
        {
            return false;
        }

        bool wasEquipped = controller.IsFlameEquipped;
        controller.ToggleFlame();
        return controller.IsFlameEquipped != wasEquipped;
    }

    /// <summary>Returns the level-independent flame toggle description.</summary>
    public override string GetDescriptionForLevel(int level)
    {
        return "Allume/eteint la flamme";
    }

    /// <summary>Returns the level-independent flame toggle bonus text.</summary>
    public override string GetBonusDescriptionForLevel(int level)
    {
        return "Allume/eteint la flamme";
    }
}
