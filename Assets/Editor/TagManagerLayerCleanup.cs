using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TagManagerLayerCleanup
{
    private const string TagManagerPath = "ProjectSettings/TagManager.asset";

    static TagManagerLayerCleanup()
    {
        EditorApplication.delayCall -= RemoveDuplicateLayerNames;
        EditorApplication.delayCall += RemoveDuplicateLayerNames;
    }

    private static void RemoveDuplicateLayerNames()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
        if (assets == null || assets.Length == 0 || assets[0] == null)
        {
            return;
        }

        SerializedObject tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        if (layers == null || !layers.isArray)
        {
            return;
        }

        bool changed = false;
        HashSet<string> seenLayerNames = new HashSet<string>();
        for (int i = 0; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            string layerName = layer.stringValue;
            if (string.IsNullOrWhiteSpace(layerName))
            {
                continue;
            }

            if (seenLayerNames.Add(layerName))
            {
                continue;
            }

            layer.stringValue = string.Empty;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        tagManager.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }
}
