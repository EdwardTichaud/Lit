using UnityEngine;

[CreateAssetMenu(fileName = "InstantiateItem", menuName = "Scriptable Objects/Effects/Instantiate Item")]
// Instancie le prefab d'un Item donne lors de l'utilisation.
public class InstantiateItemEffect : Effect
{
    [Header("Item")]
    [Tooltip("Item dont le prefab sera instancie.")]
    public Item itemToInstantiate;

    [Header("Spawn")]
    [Tooltip("Offset de position.")]
    public Vector3 positionOffset = Vector3.zero;
    [Tooltip("Offset de rotation (Euler).")]
    public Vector3 rotationOffsetEuler = Vector3.zero;
    [Tooltip("Offset en espace local du personnage.")]
    public bool offsetInCharacterSpace = true;
    [Tooltip("Parent du prefab instancie (si false, instancie en root).")]
    public bool parentToCharacter = false;

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

    public override string GetDescriptionForLevel(int level)
    {
        return GetDescription();
    }

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
