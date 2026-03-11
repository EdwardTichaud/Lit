using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class WorldSessionSnapshot
{
    public static string CaptureJson()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        return ReplicatedWorldStateRegistry.CaptureJson(activeScene.IsValid() ? activeScene.name : string.Empty);
    }

    public static bool TryApplyJson(string json, out string diagnostic)
    {
        return ReplicatedWorldStateRegistry.TryApplyJson(json, out diagnostic);
    }
}

[Serializable]
public class BuilderControllerSessionSnapshot
{
    public List<BuilderControllerSessionSnapshotEntry> controllers = new List<BuilderControllerSessionSnapshotEntry>();

    public static string CaptureJson()
    {
        BuilderControllerSessionSnapshot snapshot = new BuilderControllerSessionSnapshot();
        List<BuilderController> controllers = WorldSessionSnapshotUtilities.FindAll<BuilderController>();
        for (int i = 0; i < controllers.Count; i++)
        {
            BuilderController controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            BuilderControllerSessionSnapshotEntry entry = new BuilderControllerSessionSnapshotEntry();
            NetworkObject networkObject = WorldSessionSnapshotUtilities.ResolveNetworkObject(controller.transform);
            if (networkObject != null && networkObject.IsSpawned)
            {
                entry.networkObjectId = networkObject.NetworkObjectId;
            }

            entry.sceneId = NetcodeSceneIdUtility.GetStableId(controller.transform);
            entry.buildings = controller.BuildSessionSnapshotEntries();
            snapshot.controllers.Add(entry);
        }

        return JsonUtility.ToJson(snapshot);
    }

    public static bool TryApplyJson(string json, out int appliedControllers, out int unresolvedControllers, out int appliedBuildings)
    {
        appliedControllers = 0;
        unresolvedControllers = 0;
        appliedBuildings = 0;

        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        BuilderControllerSessionSnapshot snapshot = JsonUtility.FromJson<BuilderControllerSessionSnapshot>(json);
        if (snapshot == null || snapshot.controllers == null || snapshot.controllers.Count == 0)
        {
            return true;
        }

        Dictionary<ulong, BuilderController> byNetworkId = new Dictionary<ulong, BuilderController>();
        Dictionary<uint, BuilderController> bySceneId = new Dictionary<uint, BuilderController>();
        List<BuilderController> controllers = WorldSessionSnapshotUtilities.FindAll<BuilderController>();
        for (int i = 0; i < controllers.Count; i++)
        {
            BuilderController controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            NetworkObject networkObject = WorldSessionSnapshotUtilities.ResolveNetworkObject(controller.transform);
            if (networkObject != null && networkObject.IsSpawned && !byNetworkId.ContainsKey(networkObject.NetworkObjectId))
            {
                byNetworkId.Add(networkObject.NetworkObjectId, controller);
            }

            uint sceneId = NetcodeSceneIdUtility.GetStableId(controller.transform);
            if (sceneId != 0u && !bySceneId.ContainsKey(sceneId))
            {
                bySceneId.Add(sceneId, controller);
            }
        }

        for (int i = 0; i < snapshot.controllers.Count; i++)
        {
            BuilderControllerSessionSnapshotEntry entry = snapshot.controllers[i];
            BuilderController controller = ResolveController(entry, byNetworkId, bySceneId);
            if (controller == null)
            {
                unresolvedControllers++;
                continue;
            }

            controller.ApplySessionSnapshotEntries(entry.buildings);
            appliedControllers++;
            appliedBuildings += entry.buildings != null ? entry.buildings.Count : 0;
        }

        return unresolvedControllers == 0;
    }

    private static BuilderController ResolveController(
        BuilderControllerSessionSnapshotEntry entry,
        Dictionary<ulong, BuilderController> byNetworkId,
        Dictionary<uint, BuilderController> bySceneId)
    {
        if (entry == null)
        {
            return null;
        }

        if (entry.networkObjectId != 0ul && byNetworkId.TryGetValue(entry.networkObjectId, out BuilderController byNetwork))
        {
            return byNetwork;
        }

        if (entry.sceneId != 0u && bySceneId.TryGetValue(entry.sceneId, out BuilderController byScene))
        {
            return byScene;
        }

        return null;
    }
}

[Serializable]
public class BuilderControllerSessionSnapshotEntry
{
    public ulong networkObjectId;
    public uint sceneId;
    public List<BuilderSessionSnapshotBuildingEntry> buildings = new List<BuilderSessionSnapshotBuildingEntry>();
}

[Serializable]
public class BuilderSessionSnapshotBuildingEntry
{
    public ulong networkId;
    public string buildingItemId;
    public int level;
    public Vector3 position;
    public Quaternion rotation;
}

[Serializable]
public class BraseroSessionSnapshot
{
    public List<BraseroSessionSnapshotEntry> braseros = new List<BraseroSessionSnapshotEntry>();

    public static string CaptureJson()
    {
        BraseroSessionSnapshot snapshot = new BraseroSessionSnapshot();
        List<Brasero> braseros = WorldSessionSnapshotUtilities.FindAll<Brasero>();
        for (int i = 0; i < braseros.Count; i++)
        {
            Brasero brasero = braseros[i];
            if (brasero == null)
            {
                continue;
            }

            BraseroSessionSnapshotEntry entry = new BraseroSessionSnapshotEntry
            {
                sceneId = NetcodeSceneIdUtility.GetStableId(brasero.transform),
                braseroId = brasero.BraseroId,
                isLit = brasero.IsLit
            };

            NetworkObject networkObject = WorldSessionSnapshotUtilities.ResolveNetworkObject(brasero.transform);
            if (networkObject != null && networkObject.IsSpawned)
            {
                entry.networkObjectId = networkObject.NetworkObjectId;
            }

            snapshot.braseros.Add(entry);
        }

        return JsonUtility.ToJson(snapshot);
    }

    public static bool TryApplyJson(string json, out int appliedCount, out int unresolvedCount)
    {
        appliedCount = 0;
        unresolvedCount = 0;

        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        BraseroSessionSnapshot snapshot = JsonUtility.FromJson<BraseroSessionSnapshot>(json);
        if (snapshot == null || snapshot.braseros == null || snapshot.braseros.Count == 0)
        {
            return true;
        }

        Dictionary<ulong, Brasero> byNetworkId = new Dictionary<ulong, Brasero>();
        Dictionary<uint, Brasero> bySceneId = new Dictionary<uint, Brasero>();
        Dictionary<string, Brasero> byBraseroId = new Dictionary<string, Brasero>();
        List<Brasero> braseros = WorldSessionSnapshotUtilities.FindAll<Brasero>();
        for (int i = 0; i < braseros.Count; i++)
        {
            Brasero brasero = braseros[i];
            if (brasero == null)
            {
                continue;
            }

            NetworkObject networkObject = WorldSessionSnapshotUtilities.ResolveNetworkObject(brasero.transform);
            if (networkObject != null && networkObject.IsSpawned && !byNetworkId.ContainsKey(networkObject.NetworkObjectId))
            {
                byNetworkId.Add(networkObject.NetworkObjectId, brasero);
            }

            uint sceneId = NetcodeSceneIdUtility.GetStableId(brasero.transform);
            if (sceneId != 0u && !bySceneId.ContainsKey(sceneId))
            {
                bySceneId.Add(sceneId, brasero);
            }

            if (!string.IsNullOrWhiteSpace(brasero.BraseroId) && !byBraseroId.ContainsKey(brasero.BraseroId))
            {
                byBraseroId.Add(brasero.BraseroId, brasero);
            }
        }

        for (int i = 0; i < snapshot.braseros.Count; i++)
        {
            BraseroSessionSnapshotEntry entry = snapshot.braseros[i];
            Brasero brasero = ResolveBrasero(entry, byNetworkId, bySceneId, byBraseroId);
            if (brasero == null)
            {
                unresolvedCount++;
                continue;
            }

            brasero.ApplySnapshotState(entry.isLit);
            appliedCount++;
        }

        return unresolvedCount == 0;
    }

    private static Brasero ResolveBrasero(
        BraseroSessionSnapshotEntry entry,
        Dictionary<ulong, Brasero> byNetworkId,
        Dictionary<uint, Brasero> bySceneId,
        Dictionary<string, Brasero> byBraseroId)
    {
        if (entry == null)
        {
            return null;
        }

        if (entry.networkObjectId != 0ul && byNetworkId.TryGetValue(entry.networkObjectId, out Brasero byNetwork))
        {
            return byNetwork;
        }

        if (entry.sceneId != 0u && bySceneId.TryGetValue(entry.sceneId, out Brasero byScene))
        {
            return byScene;
        }

        if (!string.IsNullOrWhiteSpace(entry.braseroId) && byBraseroId.TryGetValue(entry.braseroId, out Brasero byId))
        {
            return byId;
        }

        return null;
    }
}

[Serializable]
public class BraseroSessionSnapshotEntry
{
    public ulong networkObjectId;
    public uint sceneId;
    public string braseroId;
    public bool isLit;
}

[Serializable]
public class LeverSessionSnapshot
{
    public List<LeverSessionSnapshotEntry> levers = new List<LeverSessionSnapshotEntry>();

    public static string CaptureJson()
    {
        LeverSessionSnapshot snapshot = new LeverSessionSnapshot();
        List<Lever> levers = WorldSessionSnapshotUtilities.FindAll<Lever>();
        for (int i = 0; i < levers.Count; i++)
        {
            Lever lever = levers[i];
            if (lever == null)
            {
                continue;
            }

            LeverSessionSnapshotEntry entry = new LeverSessionSnapshotEntry
            {
                sceneId = NetcodeSceneIdUtility.GetStableId(lever.transform),
                isActive = lever.IsActive
            };

            NetworkObject networkObject = WorldSessionSnapshotUtilities.ResolveNetworkObject(lever.transform);
            if (networkObject != null && networkObject.IsSpawned)
            {
                entry.networkObjectId = networkObject.NetworkObjectId;
            }

            snapshot.levers.Add(entry);
        }

        return JsonUtility.ToJson(snapshot);
    }

    public static bool TryApplyJson(string json, out int appliedCount, out int unresolvedCount)
    {
        appliedCount = 0;
        unresolvedCount = 0;

        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        LeverSessionSnapshot snapshot = JsonUtility.FromJson<LeverSessionSnapshot>(json);
        if (snapshot == null || snapshot.levers == null || snapshot.levers.Count == 0)
        {
            return true;
        }

        Dictionary<ulong, Lever> byNetworkId = new Dictionary<ulong, Lever>();
        Dictionary<uint, Lever> bySceneId = new Dictionary<uint, Lever>();
        List<Lever> levers = WorldSessionSnapshotUtilities.FindAll<Lever>();
        for (int i = 0; i < levers.Count; i++)
        {
            Lever lever = levers[i];
            if (lever == null)
            {
                continue;
            }

            NetworkObject networkObject = WorldSessionSnapshotUtilities.ResolveNetworkObject(lever.transform);
            if (networkObject != null && networkObject.IsSpawned && !byNetworkId.ContainsKey(networkObject.NetworkObjectId))
            {
                byNetworkId.Add(networkObject.NetworkObjectId, lever);
            }

            uint sceneId = NetcodeSceneIdUtility.GetStableId(lever.transform);
            if (sceneId != 0u && !bySceneId.ContainsKey(sceneId))
            {
                bySceneId.Add(sceneId, lever);
            }
        }

        for (int i = 0; i < snapshot.levers.Count; i++)
        {
            LeverSessionSnapshotEntry entry = snapshot.levers[i];
            Lever lever = ResolveLever(entry, byNetworkId, bySceneId);
            if (lever == null)
            {
                unresolvedCount++;
                continue;
            }

            lever.ApplySnapshotState(entry.isActive);
            appliedCount++;
        }

        return unresolvedCount == 0;
    }

    private static Lever ResolveLever(
        LeverSessionSnapshotEntry entry,
        Dictionary<ulong, Lever> byNetworkId,
        Dictionary<uint, Lever> bySceneId)
    {
        if (entry == null)
        {
            return null;
        }

        if (entry.networkObjectId != 0ul && byNetworkId.TryGetValue(entry.networkObjectId, out Lever byNetwork))
        {
            return byNetwork;
        }

        if (entry.sceneId != 0u && bySceneId.TryGetValue(entry.sceneId, out Lever byScene))
        {
            return byScene;
        }

        return null;
    }
}

[Serializable]
public class LeverSessionSnapshotEntry
{
    public ulong networkObjectId;
    public uint sceneId;
    public bool isActive;
}

internal static class WorldSessionSnapshotUtilities
{
    public static List<T> FindAll<T>() where T : UnityEngine.Object
    {
        List<T> results = new List<T>();
#if UNITY_2023_1_OR_NEWER
        T[] found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        T[] found = UnityEngine.Object.FindObjectsOfType<T>(true);
#endif
        if (found == null)
        {
            return results;
        }

        for (int i = 0; i < found.Length; i++)
        {
            T entry = found[i];
            if (entry == null || results.Contains(entry))
            {
                continue;
            }

            results.Add(entry);
        }

        return results;
    }

    public static NetworkObject ResolveNetworkObject(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        NetworkObject direct = target.GetComponent<NetworkObject>();
        if (direct != null && direct.IsSpawned)
        {
            return direct;
        }

        Transform current = target.parent;
        while (current != null)
        {
            NetworkObject parent = current.GetComponent<NetworkObject>();
            if (parent != null)
            {
                return parent;
            }

            current = current.parent;
        }

        return direct;
    }
}
