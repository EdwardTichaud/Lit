using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkObjectRegistry : MonoBehaviour
{
    public static NetworkObjectRegistry Instance { get; private set; }

    private readonly Dictionary<string, PersistentNetworkObject> objectsById = new Dictionary<string, PersistentNetworkObject>();
    private readonly Dictionary<string, PersistentNetworkObject> sceneObjectsById = new Dictionary<string, PersistentNetworkObject>();
    private readonly Dictionary<string, PersistentNetworkObject> runtimeObjectsById = new Dictionary<string, PersistentNetworkObject>();
    private readonly HashSet<string> loggedCollisions = new HashSet<string>();
    private readonly HashSet<int> loggedMissingIdInstances = new HashSet<int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RefreshSceneObjects();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Register(PersistentNetworkObject persistentObject)
    {
        if (persistentObject == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(persistentObject.PersistentId))
        {
            int instanceId = persistentObject.GetInstanceID();
            if (loggedMissingIdInstances.Add(instanceId))
            {
                PersistentWorldDebug.Warn(
                    $"persistent object missing persistent ID kind={persistentObject.ObjectKind} path='{PersistentWorldDebug.DescribeTransform(persistentObject.transform)}' instance={instanceId}",
                    persistentObject);
            }

            return;
        }

        loggedMissingIdInstances.Remove(persistentObject.GetInstanceID());

        if (objectsById.TryGetValue(persistentObject.PersistentId, out PersistentNetworkObject existing) &&
            existing != null &&
            existing != persistentObject)
        {
            LogCollision(persistentObject.PersistentId, existing, persistentObject);
            return;
        }

        objectsById[persistentObject.PersistentId] = persistentObject;
        if (persistentObject.ObjectKind == PersistentObjectKind.ScenePlaced)
        {
            sceneObjectsById[persistentObject.PersistentId] = persistentObject;
            runtimeObjectsById.Remove(persistentObject.PersistentId);
            return;
        }

        runtimeObjectsById[persistentObject.PersistentId] = persistentObject;
        sceneObjectsById.Remove(persistentObject.PersistentId);
    }

    public void Unregister(PersistentNetworkObject persistentObject)
    {
        if (persistentObject == null || string.IsNullOrWhiteSpace(persistentObject.PersistentId))
        {
            return;
        }

        if (objectsById.TryGetValue(persistentObject.PersistentId, out PersistentNetworkObject existing) && existing == persistentObject)
        {
            objectsById.Remove(persistentObject.PersistentId);
        }

        if (sceneObjectsById.TryGetValue(persistentObject.PersistentId, out existing) && existing == persistentObject)
        {
            sceneObjectsById.Remove(persistentObject.PersistentId);
        }

        if (runtimeObjectsById.TryGetValue(persistentObject.PersistentId, out existing) && existing == persistentObject)
        {
            runtimeObjectsById.Remove(persistentObject.PersistentId);
        }
    }

    public bool TryGet(string persistentId, out PersistentNetworkObject persistentObject)
    {
        PruneNullEntries();
        return objectsById.TryGetValue(persistentId, out persistentObject) && persistentObject != null;
    }

    public bool TryResolveRequired(string persistentId, out PersistentNetworkObject persistentObject, Object context, string reason)
    {
        if (TryGet(persistentId, out persistentObject))
        {
            return true;
        }

        PersistentWorldDebug.Error(
            $"unresolved persistent object id='{persistentId}' reason='{reason}'",
            context);
        return false;
    }

    public bool TryResolveSnapshotObject(
        PersistentObjectSnapshot snapshot,
        out PersistentNetworkObject persistentObject,
        out PersistentResolveStatus status,
        Object context,
        string reason,
        bool requireSceneObject = false)
    {
        persistentObject = null;
        status = PersistentResolveStatus.Success;

        if (snapshot == null)
        {
            status = PersistentResolveStatus.MissingObject;
            PersistentWorldDebug.Error($"snapshot resolve failed reason='{reason}' snapshot=<null>", context);
            return false;
        }

        if (string.IsNullOrWhiteSpace(snapshot.PersistentId))
        {
            status = PersistentResolveStatus.MissingPersistentId;
            PersistentWorldDebug.Error(
                $"snapshot resolve failed reason='{reason}' missing persistent ID kind='{snapshot.ObjectKind}' prefab='{snapshot.RuntimePrefabId}'",
                context);
            return false;
        }

        string persistentId = snapshot.PersistentId;
        if (requireSceneObject)
        {
            if (!TryGetSceneObject(persistentId, out persistentObject) || persistentObject == null)
            {
                if (TryGet(persistentId, out PersistentNetworkObject resolvedAny) &&
                    resolvedAny != null &&
                    resolvedAny.ObjectKind != PersistentObjectKind.ScenePlaced)
                {
                    status = PersistentResolveStatus.KindMismatch;
                    PersistentWorldDebug.Error(
                        $"snapshot resolve kind mismatch reason='{reason}' persistentId='{persistentId}' expectedKind='{snapshot.ObjectKind}' actualKind='{resolvedAny.ObjectKind}' expectedSceneObject=true actualPath='{PersistentWorldDebug.DescribeTransform(resolvedAny.transform)}'",
                        resolvedAny);
                    return false;
                }

                status = PersistentResolveStatus.MissingObject;
                PersistentWorldDebug.Error(
                    $"snapshot resolve failed reason='{reason}' unresolved scene object persistentId='{persistentId}' expectedKind='{snapshot.ObjectKind}'",
                    context);
                return false;
            }
        }
        else if (!TryGet(persistentId, out persistentObject) || persistentObject == null)
        {
            status = PersistentResolveStatus.MissingObject;
            PersistentWorldDebug.Error(
                $"snapshot resolve failed reason='{reason}' unresolved persistent object persistentId='{persistentId}' expectedKind='{snapshot.ObjectKind}' prefab='{snapshot.RuntimePrefabId}'",
                context);
            return false;
        }

        if (persistentObject.ObjectKind != snapshot.ObjectKind)
        {
            status = PersistentResolveStatus.KindMismatch;
            PersistentWorldDebug.Error(
                $"snapshot resolve kind mismatch reason='{reason}' persistentId='{persistentId}' expectedKind='{snapshot.ObjectKind}' actualKind='{persistentObject.ObjectKind}' actualPath='{PersistentWorldDebug.DescribeTransform(persistentObject.transform)}'",
                persistentObject);
            persistentObject = null;
            return false;
        }

        if (snapshot.ObjectKind == PersistentObjectKind.RuntimeSpawned)
        {
            if (string.IsNullOrWhiteSpace(snapshot.RuntimePrefabId))
            {
                status = PersistentResolveStatus.RuntimePrefabMissing;
                PersistentWorldDebug.Error(
                    $"snapshot resolve failed reason='{reason}' missing runtime prefab persistentId='{persistentId}'",
                    context);
                persistentObject = null;
                return false;
            }

            if (string.IsNullOrWhiteSpace(persistentObject.RuntimePrefabId) ||
                !string.Equals(persistentObject.RuntimePrefabId, snapshot.RuntimePrefabId, System.StringComparison.Ordinal))
            {
                status = PersistentResolveStatus.RuntimePrefabMismatch;
                PersistentWorldDebug.Error(
                    $"snapshot resolve prefab mismatch reason='{reason}' persistentId='{persistentId}' expectedPrefab='{snapshot.RuntimePrefabId}' actualPrefab='{persistentObject.RuntimePrefabId}' actualPath='{PersistentWorldDebug.DescribeTransform(persistentObject.transform)}'",
                    persistentObject);
                persistentObject = null;
                return false;
            }
        }

        return true;
    }

    public bool TryGetSceneObject(string persistentId, out PersistentNetworkObject persistentObject)
    {
        PruneNullEntries();
        return sceneObjectsById.TryGetValue(persistentId, out persistentObject) && persistentObject != null;
    }

    public List<PersistentNetworkObject> GetAllObjects()
    {
        PruneNullEntries();
        return new List<PersistentNetworkObject>(objectsById.Values);
    }

    public List<PersistentNetworkObject> GetRuntimeObjects()
    {
        PruneNullEntries();
        return new List<PersistentNetworkObject>(runtimeObjectsById.Values);
    }

    public void RefreshSceneObjects()
    {
        PruneNullEntries();

#if UNITY_2023_1_OR_NEWER
        PersistentNetworkObject[] found = FindObjectsByType<PersistentNetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        PersistentNetworkObject[] found = FindObjectsOfType<PersistentNetworkObject>(true);
#endif
        if (found == null)
        {
            return;
        }

        for (int i = 0; i < found.Length; i++)
        {
            Register(found[i]);
        }

        ValidateUniqueIds(found);
    }

    private void PruneNullEntries()
    {
        PruneNullEntries(objectsById);
        PruneNullEntries(sceneObjectsById);
        PruneNullEntries(runtimeObjectsById);
    }

    private static void PruneNullEntries(Dictionary<string, PersistentNetworkObject> dictionary)
    {
        if (dictionary.Count == 0)
        {
            return;
        }

        List<string> toRemove = null;
        foreach (KeyValuePair<string, PersistentNetworkObject> pair in dictionary)
        {
            if (pair.Value != null)
            {
                continue;
            }

            if (toRemove == null)
            {
                toRemove = new List<string>();
            }

            toRemove.Add(pair.Key);
        }

        if (toRemove == null)
        {
            return;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            dictionary.Remove(toRemove[i]);
        }
    }

    private void ValidateUniqueIds(PersistentNetworkObject[] found)
    {
        Dictionary<string, PersistentNetworkObject> firstById = new Dictionary<string, PersistentNetworkObject>();
        HashSet<string> collisionsThisPass = new HashSet<string>();

        if (found != null)
        {
            for (int i = 0; i < found.Length; i++)
            {
                PersistentNetworkObject persistentObject = found[i];
                if (persistentObject == null || string.IsNullOrWhiteSpace(persistentObject.PersistentId))
                {
                    continue;
                }

                if (firstById.TryGetValue(persistentObject.PersistentId, out PersistentNetworkObject existing) &&
                    existing != null &&
                    existing != persistentObject)
                {
                    collisionsThisPass.Add(persistentObject.PersistentId);
                    LogCollision(persistentObject.PersistentId, existing, persistentObject);
                    continue;
                }

                firstById[persistentObject.PersistentId] = persistentObject;
            }
        }

        loggedCollisions.RemoveWhere(id => !collisionsThisPass.Contains(id));
    }

    private void LogCollision(string persistentId, PersistentNetworkObject existing, PersistentNetworkObject incoming)
    {
        if (!loggedCollisions.Add(persistentId))
        {
            return;
        }

        string existingDescription = DescribePersistentObject(existing);
        string incomingDescription = DescribePersistentObject(incoming);
        PersistentWorldDebug.Error(
            $"persistent ID collision id='{persistentId}' existing={existingDescription} incoming={incomingDescription}",
            incoming);
    }

    private static string DescribePersistentObject(PersistentNetworkObject persistentObject)
    {
        if (persistentObject == null)
        {
            return "<null>";
        }

        StringBuilder builder = new StringBuilder(160);
        builder.Append($"kind={persistentObject.ObjectKind} id='{persistentObject.PersistentId}'");
        if (!string.IsNullOrWhiteSpace(persistentObject.RuntimePrefabId))
        {
            builder.Append($" prefab='{persistentObject.RuntimePrefabId}'");
        }

        builder.Append($" path='{PersistentWorldDebug.DescribeTransform(persistentObject.transform)}' instance={persistentObject.GetInstanceID()}");
        return builder.ToString();
    }
}
