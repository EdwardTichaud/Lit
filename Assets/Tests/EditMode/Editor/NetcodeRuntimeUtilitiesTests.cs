using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

public class NetcodeRuntimeUtilitiesTests
{
    private static readonly FieldInfo GlobalHashField = typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo PrefabHashField = typeof(NetworkObject).GetField("PrefabGlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo InSceneHashField = typeof(NetworkObject).GetField("InScenePlacedSourceGlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);

    [TearDown]
    public void TearDown()
    {
        NetcodeRuntimeUtilities.ResetLegacyCompatibilityWarningsForTests();
    }

    [Test]
    public void ResolvePrefabHash_UsesSerializedNetworkObjectHashWhenPresent()
    {
        GameObject prefab = new GameObject("PreparedPrefab");
        try
        {
            NetworkObject networkObject = prefab.AddComponent<NetworkObject>();
            SetHashes(networkObject, 123u, 123u, 0u);

            uint hash = NetcodeRuntimeUtilities.ResolvePrefabHash(prefab, "character:prepared", out bool usesLegacyHash);

            Assert.That(hash, Is.EqualTo(123u));
            Assert.That(usesLegacyHash, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void ResolvePrefabHash_FallsBackToLegacyHashWhenPrefabIsNotPrepared()
    {
        GameObject prefab = new GameObject("LegacyPrefab");
        try
        {
            uint hash = NetcodeRuntimeUtilities.ResolvePrefabHash(prefab, "character:legacy", out bool usesLegacyHash);

            Assert.That(hash, Is.EqualTo(NetcodeStableHash.Hash32("character:legacy")));
            Assert.That(usesLegacyHash, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void EnsureSceneObjectHash_DoesNotOverwritePreparedSceneObject()
    {
        GameObject host = new GameObject("SceneObject");
        try
        {
            NetworkObject networkObject = host.AddComponent<NetworkObject>();
            SetHashes(networkObject, 17u, 17u, 17u);

            NetcodeRuntimeUtilities.EnsureSceneObjectHash(networkObject, 88u, "test:scene");

            Assert.That(networkObject.PrefabIdHash, Is.EqualTo(17u));
            Assert.That((uint)InSceneHashField.GetValue(networkObject), Is.EqualTo(17u));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void EnsureNetworkObjectHash_AppliesLegacyHashWhenRequired()
    {
        GameObject host = new GameObject("RuntimeObject");
        try
        {
            NetworkObject networkObject = host.AddComponent<NetworkObject>();
            SetHashes(networkObject, 0u, 0u, 0u);

            NetcodeRuntimeUtilities.EnsureNetworkObjectHash(networkObject, 99u, "test:runtime");

            Assert.That(networkObject.PrefabIdHash, Is.EqualTo(99u));
            Assert.That((uint)PrefabHashField.GetValue(networkObject), Is.EqualTo(99u));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    private static void SetHashes(NetworkObject networkObject, uint globalHash, uint prefabHash, uint sceneHash)
    {
        GlobalHashField.SetValue(networkObject, globalHash);
        PrefabHashField.SetValue(networkObject, prefabHash);
        InSceneHashField.SetValue(networkObject, sceneHash);
    }
}
