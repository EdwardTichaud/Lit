using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Utilitaires runtime pour Netcode (hashes, composants).
public static class NetcodeRuntimeUtilities
{
    private static readonly HashSet<string> legacyCompatibilityWarnings = new HashSet<string>();
    private static readonly FieldInfo GlobalHashField = typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo PrefabHashField = typeof(NetworkObject).GetField("PrefabGlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo InSceneHashField = typeof(NetworkObject).GetField("InScenePlacedSourceGlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);

    public static bool TryGetRegisteredPrefabHash(GameObject prefabAsset, out uint hash)
    {
        hash = 0u;
        if (prefabAsset == null)
        {
            return false;
        }

        NetworkObject networkObject = prefabAsset.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            return false;
        }

        hash = networkObject.PrefabIdHash;
        return hash != 0u;
    }

    public static uint ResolvePrefabHash(GameObject prefabAsset, string legacyKey, out bool usesLegacyHash)
    {
        if (TryGetRegisteredPrefabHash(prefabAsset, out uint prefabHash))
        {
            usesLegacyHash = false;
            return prefabHash;
        }

        usesLegacyHash = true;
        return NetcodeStableHash.Hash32(legacyKey);
    }

    public static void EnsureNetworkObjectHash(NetworkObject networkObject, uint hash, string context = null)
    {
        if (networkObject == null)
        {
            return;
        }

        if (hash == 0u)
        {
            hash = 1u;
        }

        if (networkObject.PrefabIdHash == hash)
        {
            return;
        }

        ReportLegacyCompatibilityUsage(context, false);
        GlobalHashField?.SetValue(networkObject, hash);
        PrefabHashField?.SetValue(networkObject, hash);
    }

    public static void EnsureSceneObjectHash(NetworkObject networkObject, uint hash, string context = null)
    {
        if (networkObject == null)
        {
            return;
        }

        if (hash == 0u)
        {
            hash = 1u;
        }

        if (networkObject.PrefabIdHash != 0u)
        {
            return;
        }

        ReportLegacyCompatibilityUsage(context, true);
        GlobalHashField?.SetValue(networkObject, hash);
        InSceneHashField?.SetValue(networkObject, hash);
        PrefabHashField?.SetValue(networkObject, hash);
    }

    public static void ResetLegacyCompatibilityWarningsForTests()
    {
        legacyCompatibilityWarnings.Clear();
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

    public static void ConfigureCharacterNetworkComponents(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        NetworkTransform networkTransform = GetOrAdd<NetworkTransform>(target);
        if (networkTransform != null)
        {
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
        }

        GetOrAdd<NetcodeCharacterIdentity>(target);
        GetOrAdd<NetcodeLocalPlayer>(target);
        GetOrAdd<NetworkCharacterInput>(target);
        GetOrAdd<NetworkInventory>(target);

#if COM_UNITY_MODULES_PHYSICS
        if (target.GetComponent<Rigidbody>() != null)
        {
            GetOrAdd<NetworkRigidbody>(target);
        }
#endif
    }

    private static void ReportLegacyCompatibilityUsage(string context, bool sceneObject)
    {
        string resolvedContext = string.IsNullOrWhiteSpace(context)
            ? (sceneObject ? "scene-object" : "runtime-object")
            : context.Trim();
        string messageKey = $"{(sceneObject ? "scene" : "runtime")}:{resolvedContext}";
        if (!legacyCompatibilityWarnings.Add(messageKey))
        {
            return;
        }

        Debug.LogWarning(
            $"NetcodeRuntimeUtilities: fallback de compatibilite NGO utilise pour {resolvedContext}. " +
            "Prepare les prefabs/objets de scene avec un NetworkObject serialize pour supprimer ce patching runtime.");
    }
}
