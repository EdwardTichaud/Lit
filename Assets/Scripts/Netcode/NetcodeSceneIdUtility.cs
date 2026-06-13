using System.Text;
using UnityEngine;

// Utilitaire pour produire un ID stable par objet de scene.
public static class NetcodeSceneIdUtility
{
    public static uint GetStableId(Transform target)
    {
        if (target == null)
        {
            return 0u;
        }

        string sceneName = target.gameObject.scene.IsValid() ? target.gameObject.scene.name : "NoScene";
        string path = GetHierarchyPath(target);
        return NetcodeStableHash.Hash32($"scene:{sceneName}|path:{path}");
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(GetHierarchySegment(target));
        Transform current = target.parent;
        while (current != null)
        {
            builder.Insert(0, "/");
            builder.Insert(0, GetHierarchySegment(current));
            current = current.parent;
        }

        return builder.ToString();
    }

    private static string GetHierarchySegment(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        Transform parent = target.parent;
        if (parent == null)
        {
            return target.name;
        }

        int sameNameCount = 0;
        int sameNameIndex = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform sibling = parent.GetChild(i);
            if (sibling == null || sibling.name != target.name)
            {
                continue;
            }

            if (sibling == target)
            {
                sameNameIndex = sameNameCount;
            }

            sameNameCount++;
        }

        return sameNameCount > 1 ? $"{target.name}#{sameNameIndex}" : target.name;
    }
}
