#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class WorldPickupAuthoringMenu
{
    [MenuItem("Lit/Items/Create Harvestable Pickup", false, 10)]
    private static void CreateHarvestablePickup(MenuCommand command)
    {
        GameObject selected = Selection.activeGameObject;
        bool canWrapSelection = selected != null && selected.scene.IsValid() && !EditorUtility.IsPersistent(selected);

        GameObject root = new GameObject(canWrapSelection ? $"Pickup_{selected.name}" : "HarvestablePickup");
        Undo.RegisterCreatedObjectUndo(root, "Create Harvestable Pickup");
        GameObjectUtility.SetParentAndAlign(root, command.context as GameObject);

        if (canWrapSelection)
        {
            Transform selectedTransform = selected.transform;
            Transform parent = selectedTransform.parent;
            int siblingIndex = selectedTransform.GetSiblingIndex();

            root.transform.SetParent(parent, false);
            root.transform.SetSiblingIndex(siblingIndex);
            root.transform.SetPositionAndRotation(selectedTransform.position, selectedTransform.rotation);
            root.transform.localScale = Vector3.one;

            Undo.SetTransformParent(selectedTransform, root.transform, "Wrap Selection In Harvestable Pickup");
        }

        WorldPickupAuthoring authoring = Undo.AddComponent<WorldPickupAuthoring>(root);
        authoring.ApplyPickupSetup();

        Selection.activeGameObject = root;
    }
}
#endif
