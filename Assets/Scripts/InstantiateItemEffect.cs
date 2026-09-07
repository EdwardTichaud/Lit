// Role:
// Item effect that instantiates the world/building prefab of an Item.
// Usage:
// Assigned to effect assets that should spawn a configured item prefab when used.
// Responsibilities:
// Resolve the correct prefab, compute position/rotation offsets, and instantiate it.
// Dependencies:
// Effect, Item prefab fields, SquadCharacterController transform.
// Precautions:
// This is a local Instantiate path. Check Netcode/building systems before using it
// for objects that must persist or synchronize.
using UnityEngine;

/// <summary>
/// Instantiates an item prefab at or near the using character.
/// </summary>
[CreateAssetMenu(fileName = "InstantiateItem", menuName = "Scriptable Objects/Effects/Instantiate Item")]
public class InstantiateItemEffect : Effect
{
    [Header("Item")]
    /// <summary>Optional item whose prefab is spawned. Falls back to the source item.</summary>
    [Tooltip("Item dont le prefab sera instancie.")]
    public Item itemToInstantiate;

    [Header("Spawn")]
    /// <summary>Position offset applied to the character position.</summary>
    [Tooltip("Offset de position.")]
    public Vector3 positionOffset = Vector3.zero;
    /// <summary>Euler rotation offset applied to the character rotation.</summary>
    [Tooltip("Offset de rotation (Euler).")]
    public Vector3 rotationOffsetEuler = Vector3.zero;
    /// <summary>If true, positionOffset is rotated by the character rotation.</summary>
    [Tooltip("Offset en espace local du personnage.")]
    public bool offsetInCharacterSpace = true;
    /// <summary>If true, parents the spawned object to the character.</summary>
    [Tooltip("Parent du prefab instancie (si false, instancie en root).")]
    public bool parentToCharacter = false;

    /// <summary>
    /// Spawns the resolved prefab relative to the controller transform.
    /// </summary>
    public override bool Apply(SquadCharacterController controller, Item item)
    {
        if (controller == null)
        {
            return false;
        }

        Item targetItem = itemToInstantiate != null ? itemToInstantiate : item;
        GameObject prefab = ResolvePrefab(targetItem);
        if (prefab == null)
        {
            return false;
        }

        // Local-space offsets let designers spawn objects in front of the character.
        Transform target = controller.transform;
        Vector3 offset = offsetInCharacterSpace
            ? target.rotation * positionOffset
            : positionOffset;
        Vector3 position = target.position + offset;
        Quaternion rotation = target.rotation * Quaternion.Euler(rotationOffsetEuler);

        Transform parent = parentToCharacter ? target : null;
        GameObject instance = Instantiate(prefab, position, rotation, parent);
        if (instance == null)
        {
            return false;
        }

        return true;
    }

    /// <summary>Returns the default description because spawning does not scale by level.</summary>
    public override string GetDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    /// <summary>Returns the default bonus text because spawning does not scale by level.</summary>
    public override string GetBonusDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    private GameObject ResolvePrefab(Item targetItem)
    {
        if (targetItem == null)
        {
            return null;
        }

        if (targetItem.isBuilding && targetItem.buildingPrefab != null)
        {
            return targetItem.buildingPrefab;
        }

        if (targetItem.worldPrefab != null)
        {
            return targetItem.worldPrefab;
        }

        if (targetItem.buildingPrefab != null)
        {
            return targetItem.buildingPrefab;
        }

        return null;
    }
}
