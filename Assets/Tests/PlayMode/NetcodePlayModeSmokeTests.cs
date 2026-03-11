#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class NetcodePlayModeSmokeTests
{
    private const float TimeoutSeconds = 10f;
    private static ushort nextPort = 14000;
    private static readonly FieldInfo GlobalHashField = typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo PrefabHashField = typeof(NetworkObject).GetField("PrefabGlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);

    private GameObject counterPrefab;
    private GameObject hostObject;
    private GameObject clientObject;
    private GameObject spawnedServerObject;
    private NetworkManager hostManager;
    private NetworkManager clientManager;
    private ushort port;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        port = ++nextPort;
        yield return TearDown();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (clientManager != null && clientManager.IsListening)
        {
            clientManager.Shutdown();
        }

        if (hostManager != null && hostManager.IsListening)
        {
            hostManager.Shutdown();
        }

        yield return null;
        yield return null;

        DestroyObject(ref spawnedServerObject);
        DestroyObject(ref clientObject);
        DestroyObject(ref hostObject);
        DestroyObject(ref counterPrefab);

        hostManager = null;
        clientManager = null;
    }

    [UnityTest]
    public IEnumerator LateJoinClient_ReceivesExistingSpawnedObject()
    {
        counterPrefab = CreateCounterPrefab(900001u);
        hostManager = CreateManager("NetcodeSmokeHost", out hostObject, isHost: true);

        hostManager.SetSingleton();
        Assert.That(hostManager.StartHost(), Is.True);
        yield return WaitForCondition(() => hostManager.IsListening, "Le host n'a pas demarre.");

        spawnedServerObject = Object.Instantiate(counterPrefab);
        NetworkObject serverNetworkObject = spawnedServerObject.GetComponent<NetworkObject>();
        TestReplicatedCounter serverCounter = spawnedServerObject.GetComponent<TestReplicatedCounter>();
        serverNetworkObject.Spawn();
        serverCounter.SetValue(77);

        clientManager = CreateManager("NetcodeSmokeClient", out clientObject, isHost: false);
        Assert.That(clientManager.StartClient(), Is.True);

        yield return WaitForCondition(
            () => clientManager.IsConnectedClient && hostManager.ConnectedClientsIds.Count >= 2,
            "Le client n'a pas rejoint le host.");

        yield return WaitForCondition(
            () => TryFindClientCounter(out TestReplicatedCounter clientCounter) && clientCounter.Counter.Value == 77,
            "Le client tardif n'a pas recu l'objet deja spawn ou sa valeur.");

        Assert.That(TryFindClientCounter(out TestReplicatedCounter replicatedCounter), Is.True);
        Assert.That(replicatedCounter.Counter.Value, Is.EqualTo(77));
    }

    [UnityTest]
    public IEnumerator ConnectedClient_ReceivesSubsequentStateUpdates()
    {
        counterPrefab = CreateCounterPrefab(900002u);
        hostManager = CreateManager("NetcodeSmokeHost", out hostObject, isHost: true);

        hostManager.SetSingleton();
        Assert.That(hostManager.StartHost(), Is.True);
        yield return WaitForCondition(() => hostManager.IsListening, "Le host n'a pas demarre.");

        clientManager = CreateManager("NetcodeSmokeClient", out clientObject, isHost: false);
        Assert.That(clientManager.StartClient(), Is.True);

        yield return WaitForCondition(
            () => clientManager.IsConnectedClient && hostManager.ConnectedClientsIds.Count >= 2,
            "Le client n'a pas rejoint le host.");

        spawnedServerObject = Object.Instantiate(counterPrefab);
        NetworkObject serverNetworkObject = spawnedServerObject.GetComponent<NetworkObject>();
        TestReplicatedCounter serverCounter = spawnedServerObject.GetComponent<TestReplicatedCounter>();
        serverNetworkObject.Spawn();

        yield return WaitForCondition(
            () => TryFindClientCounter(out _),
            "Le client n'a pas instancie l'objet reseau.");

        serverCounter.SetValue(12);
        yield return WaitForCondition(
            () => TryFindClientCounter(out TestReplicatedCounter clientCounter) && clientCounter.Counter.Value == 12,
            "Le client n'a pas recu la premiere mise a jour.");

        serverCounter.SetValue(42);
        yield return WaitForCondition(
            () => TryFindClientCounter(out TestReplicatedCounter clientCounter) && clientCounter.Counter.Value == 42,
            "Le client n'a pas recu la deuxieme mise a jour.");
    }

    private NetworkManager CreateManager(string objectName, out GameObject managerObject, bool isHost)
    {
        managerObject = new GameObject(objectName);
        NetworkManager manager = managerObject.AddComponent<NetworkManager>();
        UnityTransport transport = managerObject.AddComponent<UnityTransport>();
        if (isHost)
        {
            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
        }
        else
        {
            transport.SetConnectionData("127.0.0.1", port);
        }

        manager.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            EnableSceneManagement = false,
            ConnectionApproval = false,
            ForceSamePrefabs = false,
            TickRate = 30
        };
        manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = counterPrefab });
        return manager;
    }

    private static GameObject CreateCounterPrefab(uint hash)
    {
        GameObject prefab = new GameObject("NetcodeSmokeCounterPrefab");
        prefab.hideFlags = HideFlags.HideAndDontSave;
        NetworkObject networkObject = prefab.AddComponent<NetworkObject>();
        SetRuntimePrefabHash(networkObject, hash);
        prefab.AddComponent<TestReplicatedCounter>();
        return prefab;
    }

    private static void SetRuntimePrefabHash(NetworkObject networkObject, uint hash)
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

    private static IEnumerator WaitForCondition(Func<bool> predicate, string failureMessage)
    {
        float timeoutAt = Time.realtimeSinceStartup + TimeoutSeconds;
        while (!predicate())
        {
            if (Time.realtimeSinceStartup >= timeoutAt)
            {
                Assert.Fail(failureMessage);
            }

            yield return null;
        }
    }

    private static bool TryFindClientCounter(out TestReplicatedCounter counter)
    {
        TestReplicatedCounter[] counters = FindCounters();
        for (int i = 0; i < counters.Length; i++)
        {
            TestReplicatedCounter candidate = counters[i];
            if (candidate == null || !candidate.IsSpawned)
            {
                continue;
            }

            if (candidate.IsClient && !candidate.IsServer)
            {
                counter = candidate;
                return true;
            }
        }

        counter = null;
        return false;
    }

    private static TestReplicatedCounter[] FindCounters()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<TestReplicatedCounter>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<TestReplicatedCounter>();
#endif
    }

    private static void DestroyObject(ref GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Object.DestroyImmediate(target);
        target = null;
    }

    private sealed class TestReplicatedCounter : NetworkBehaviour
    {
        public readonly NetworkVariable<int> Counter = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void SetValue(int value)
        {
            if (!IsServer)
            {
                return;
            }

            Counter.Value = value;
        }
    }
}
#endif
