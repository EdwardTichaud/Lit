using System.Reflection;
using Unity.Netcode;
using UnityEngine;

// Utilitaires runtime pour Netcode (hashes, composants).
public static class NetcodeRuntimeUtilities
{
    private static readonly FieldInfo GlobalHashField = typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo PrefabHashField = typeof(NetworkObject).GetField("PrefabGlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo InSceneHashField = typeof(NetworkObject).GetField("InScenePlacedSourceGlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void EnsureNetworkObjectHash(NetworkObject networkObject, uint hash)
    {
        if (networkObject == null)
        {
            return;
        }

        if (hash == 0u)
        {
            hash = 1u;
        }

        GlobalHashField?.SetValue(networkObject, hash);
        PrefabHashField?.SetValue(networkObject, hash);
    }

    public static void EnsureSceneObjectHash(NetworkObject networkObject, uint hash)
    {
        if (networkObject == null)
        {
            return;
        }

        if (hash == 0u)
        {
            hash = 1u;
        }

        GlobalHashField?.SetValue(networkObject, hash);
        InSceneHashField?.SetValue(networkObject, hash);
        PrefabHashField?.SetValue(networkObject, hash);
    }

    public static T GetOrAdd<T>(GameObject target) where T : Component
    {
        if (target == null)
        {
            return null;
        }

        T existing = target.GetComponent<T>();
        if (existing != null)
        {
            return existing;
        }

        return target.AddComponent<T>();
    }
}
