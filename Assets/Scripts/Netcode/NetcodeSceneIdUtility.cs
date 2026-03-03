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

        StringBuilder builder = new StringBuilder(target.name);
        Transform current = target.parent;
        while (current != null)
        {
            builder.Insert(0, "/");
            builder.Insert(0, current.name);
            current = current.parent;
        }

        return builder.ToString();
    }
}
