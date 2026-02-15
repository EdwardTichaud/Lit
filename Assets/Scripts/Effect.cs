using System.Collections.Generic;
using UnityEngine;

// Base des effets appliques par items ou batiments.
public abstract class Effect : ScriptableObject
{
    [Header("Effect")]
    [TextArea]
    [Tooltip("Texte affiche dans l'UI pour expliquer l'effet.")]
    public string effectDescription;

    public virtual bool Apply(SquadCharacterController controller)
    {
        return Apply(controller, null);
    }

    public abstract bool Apply(SquadCharacterController controller, Item item);

    public virtual string GetDescription()
    {
        if (!string.IsNullOrWhiteSpace(effectDescription))
        {
            return effectDescription;
        }

        return name;
    }

    public virtual string GetDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    public virtual string GetBonusDescriptionForLevel(int level)
    {
        return GetDescriptionForLevel(level);
    }
}

// Effet qui s'applique a toute la squad en une seule operation.
public interface ISquadEffect
{
    bool ApplyToSquad(int levelDelta);
}

// Effet qui adapte son application au niveau du building.
public interface IBuildingLevelEffect
{
    bool ApplyForBuildingLevel(SquadCharacterController controller, Item building, int currentLevel, int levelDelta);
}

// Effet de squad qui adapte son application au niveau du building.
public interface IBuildingLevelSquadEffect
{
    bool ApplyToSquadForLevel(int currentLevel, int levelDelta);
}

// Effet declenche lors de l'interaction avec un batiment.
public interface IBuildingInteractEffect
{
    bool ApplyOnInteract(SquadCharacterController controller, Item building, int currentLevel);
}

// Effet passif applique par un item en fonction de sa position.
public interface IItemPassiveEffect
{
    void Tick(ItemPassiveContext context);
}

public readonly struct ItemPassiveContext
{
    public ItemPassiveContext(Item item, Transform source, int quantity, IReadOnlyList<SquadCharacterController> characters)
    {
        Item = item;
        Source = source;
        Quantity = quantity;
        Position = source != null ? source.position : Vector3.zero;
        Characters = characters;
    }

    public Item Item { get; }
    public Transform Source { get; }
    public Vector3 Position { get; }
    public int Quantity { get; }
    public IReadOnlyList<SquadCharacterController> Characters { get; }
}
