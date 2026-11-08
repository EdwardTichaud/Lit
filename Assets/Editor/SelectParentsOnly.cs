using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class SelectParentsOnly
{
    [MenuItem("Tools/Select Parents Only")]
    private static void SelectParents()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        HashSet<GameObject> selection = new HashSet<GameObject>(selectedObjects);

        List<GameObject> parents = new List<GameObject>();

        foreach (GameObject obj in selectedObjects)
        {
            bool hasSelectedParent = false;

            Transform parent = obj.transform.parent;

            while (parent != null)
            {
                if (selection.Contains(parent.gameObject))
                {
                    hasSelectedParent = true;
                    break;
                }

                parent = parent.parent;
            }

            if (!hasSelectedParent)
            {
                parents.Add(obj);
            }
        }

        Selection.objects = parents.ToArray();
    }

    [MenuItem("Tools/Select Parents Only", true)]
    private static bool ValidateSelection()
    {
        return Selection.gameObjects.Length > 0;
    }
}