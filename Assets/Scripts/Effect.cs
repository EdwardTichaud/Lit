// Role:
// Defines the base contract for item/building effects used by inventory, crafting,
// buildings, passive items, and squad upgrades.
// Usage:
// Create ScriptableObject assets from concrete Effect subclasses and assign them
// to Item assets or building data.
// Responsibilities:
// Provide shared descriptions and common interfaces for different effect families.
// Dependencies:
// SquadCharacterController, Item, Transform, and optional UI systems that display descriptions.
// Precautions:
// Do not change method signatures here without updating every Effect subclass and item asset.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base ScriptableObject for effects applied by items or buildings.
/// </summary>
public abstract class Effect : ScriptableObject
{
    [Header("Effect")]
    /// <summary>Description displayed by UI when no subclass-specific text is used.</summary>
    [TextArea]
    [Tooltip("Texte affiche dans l'UI pour expliquer l'effet.")]
    public string effectDescription;

    /// <summary>
    /// Applies this effect without an item context.
    /// </summary>
    public virtual bool Apply(SquadCharacterController controller)
    {
        return Apply(controller, null);
    }

    /// <summary>
    /// Applies this effect to a controller, optionally using the source item.
    /// </summary>
    public abstract bool Apply(SquadCharacterController controller, Item item);

    /// <summary>
    /// Returns the default description shown by UI.
    /// </summary>
    public virtual string GetDescription()
    {
        if (!string.IsNullOrWhiteSpace(effectDescription))
        {
            return effectDescription;
        }

        return name;
    }

    /// <summary>
    /// Returns the description for a building or effect level.
    /// </summary>
    public virtual string GetDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    /// <summary>
    /// Returns the short bonus text for a building or effect level.
    /// </summary>
    public virtual string GetBonusDescriptionForLevel(int level)
    {
        return GetDescriptionForLevel(level);
    }
}

/// <summary>
/// Effect that applies to the whole squad in one operation.
/// </summary>
public interface ISquadEffect
{
    /// <summary>Applies the effect for a positive level delta.</summary>
    bool ApplyToSquad(int levelDelta);
}

/// <summary>
/// Effect that adapts its application to a building level.
/// </summary>
public interface IBuildingLevelEffect
{
    /// <summary>Applies the effect using the current building level and level delta.</summary>
    bool ApplyForBuildingLevel(SquadCharacterController controller, Item building, int currentLevel, int levelDelta);
}

/// <summary>
/// Squad effect that adapts its application to a building level.
/// </summary>
public interface IBuildingLevelSquadEffect
{
    /// <summary>Applies the squad effect using the current building level and level delta.</summary>
    bool ApplyToSquadForLevel(int currentLevel, int levelDelta);
}

/// <summary>
/// Effect triggered when the player interacts with a building.
/// </summary>
public interface IBuildingInteractEffect
{
    /// <summary>Applies an interaction effect for a building and current level.</summary>
    bool ApplyOnInteract(SquadCharacterController controller, Item building, int currentLevel);
}

/// <summary>
/// Passive effect evaluated from an item position over time.
/// </summary>
public interface IItemPassiveEffect
{
    /// <summary>Evaluates one passive-effect tick.</summary>
    void Tick(ItemPassiveContext context);
}

/// <summary>
/// Runtime context passed to passive item effects.
/// </summary>
public readonly struct ItemPassiveContext
{
    /// <summary>
    /// Creates a passive effect context from an item, source transform, quantity, and known characters.
    /// </summary>
    public ItemPassiveContext(Item item, Transform source, int quantity, IReadOnlyList<SquadCharacterController> characters)
    {
        Item = item;
        Source = source;
        Quantity = quantity;
        Position = source != null ? source.position : Vector3.zero;
        Characters = characters;
    }

    /// <summary>Item that owns or triggered the passive effect.</summary>
    public Item Item { get; }
    /// <summary>Transform used as the passive effect source.</summary>
    public Transform Source { get; }
    /// <summary>Cached world position of the source.</summary>
    public Vector3 Position { get; }
    /// <summary>Item quantity represented by the source.</summary>
    public int Quantity { get; }
    /// <summary>Characters available for range or squad checks.</summary>
    public IReadOnlyList<SquadCharacterController> Characters { get; }
}
