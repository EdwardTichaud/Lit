// Role:
// Item effect that applies a torch color vision to one character.
// Usage:
// Assigned to ScriptableObject effect assets such as ApplyTorchVision_Blue.
// Responsibilities:
// Validate torch requirements, then ask TorchVisionSystem to set the active vision.
// Dependencies:
// Effect, SquadCharacterController, Item, TorchVisionDefinition, TorchVisionSystem.
// Precautions:
// This affects the existing color-vision layer, not the new TemporalTorch age reveal.
using UnityEngine;

/// <summary>
/// Applies a configured TorchVisionDefinition to a character.
/// </summary>
[CreateAssetMenu(fileName = "ApplyTorchVision", menuName = "Scriptable Objects/Effects/Apply Torch Vision")]
public class ApplyTorchVisionEffect : Effect
{
    [Header("Vision")]
    /// <summary>Vision applied when the effect succeeds.</summary>
    [SerializeField] private TorchVisionDefinition vision;
    /// <summary>Duration in seconds. Zero means handled as permanent by TorchVisionSystem.</summary>
    [SerializeField] private float durationSeconds = 0f;
    /// <summary>If true, the character must own the torch item.</summary>
    [SerializeField] private bool requireTorchItem = true;
    /// <summary>If true, the torch must also be equipped.</summary>
    [SerializeField] private bool requireTorchEquipped = false;

    /// <summary>
    /// Applies the vision to the controller if torch requirements are met.
    /// </summary>
    public override bool Apply(SquadCharacterController controller, Item item)
    {
        if (controller == null || vision == null)
        {
            return false;
        }

        if (requireTorchItem && !controller.HasTorchItem)
        {
            return false;
        }

        if (requireTorchEquipped && !controller.IsTorchEquipped)
        {
            return false;
        }

        TorchVisionSystem.SetVisionFor(controller, vision, durationSeconds);
        return true;
    }

    /// <summary>
    /// Returns custom text or a fallback label based on the target vision.
    /// </summary>
    public override string GetDescription()
    {
        if (!string.IsNullOrWhiteSpace(effectDescription))
        {
            return effectDescription;
        }

        string label = vision != null && !string.IsNullOrWhiteSpace(vision.displayName)
            ? vision.displayName
            : (vision != null ? vision.name : "vision");
        return $"Torche: {label}";
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
}
