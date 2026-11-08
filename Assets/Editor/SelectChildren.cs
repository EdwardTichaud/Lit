using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class SelectChildren
{
    [MenuItem("Tools/Select Direct Children")]
    private static void SelectDirectChildren()
    {
        List<GameObject> children = new List<GameObject>();

        foreach (GameObject parent in Selection.gameObjects)
        {
            foreach (Transform child in parent.transform)
            {
                if (!children.Contains(child.gameObject))
                {
                    children.Add(child.gameObject);
                }
            }
        }

        Selection.objects = children.ToArray();
    }
}