using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class SpawnManager : MonoBehaviour
{
    private const string FailureCodeMissingPrefabMapping = "missing_prefab_mapping";
    private const string FailureCodeRecreationFailed = "recreation_failed";

    public static SpawnManager Instance { get; private set; }

    [Serializable]
    public sealed class RuntimePersistentPrefab
    {
        public string PrefabId;
        public PersistentNetworkObject Prefab;
        public bool AllowClientSideReconstruction;
    }

    [SerializeField] private List<RuntimePersistentPrefab> runtimePrefabs = new List<RuntimePersistentPrefab>();

    private readonly Dictionary<string, RuntimePersistentPrefab> prefabLookup = new Dictionary<string, RuntimePersistentPrefab>();
    private readonly Dictionary<string, int> runtimeIdCounters = new Dictionary<string, int>();
    private readonly HashSet<string> issuedPersistentIds = new HashSet<string>(StringComparer.Ordinal);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        BuildLookup();
        ReserveExistingRuntimeIds();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        BuildLookup();
    }

    public void ResetRuntimeState(string reason = null)
    {
        runtimeIdCounters.Clear();
        issuedPersistentIds.Clear();
        BuildLookup();
    }

    public PersistentNetworkObject SpawnRuntimeObject(string prefabId, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!IsServer())
        {
            Debug.LogWarning("SpawnManager: only the host/server may spawn persistent runtime objects.", this);
            return null;
        }

        if (!TryGetDefinition(prefabId, out RuntimePersistentPrefab definition) || definition.Prefab == null)
        {
            Debug.LogWarning($"SpawnManager: unknown runtime prefab '{prefabId}'.", this);
            return null;
        }

        PersistentNetworkObject instance = parent != null
            ? Instantiate(definition.Prefab, position, rotation, parent)
            : Instantiate(definition.Prefab, position, rotation);

        if (instance == null)
        {
            return null;
        }

        string persistentId = AllocatePersistentId("spawn", prefabId);
        PrepareInstance(instance, persistentId, prefabId, spawnNetworkObject: true);
        PersistentWorldDebug.Log($"spawn runtime object persistentId={persistentId} prefabId={prefabId}", instance);
        return instance;
    }

    public string AllocatePersistentId(string category, string subjectId)
    {
        string safeCategory = SanitizeIdSegment(category, "runtime");
        string safeSubject = SanitizeIdSegment(subjectId, "object");
        string prefix = $"runtime:{safeCategory}:{safeSubject}:";

        if (!runtimeIdCounters.TryGetValue(prefix, out int counter))
        {
            counter = 0;
        }

        string persistentId;
        do
        {
            counter++;
            persistentId = $"{prefix}{counter}";
        } while ((NetworkObjectRegistry.Instance != null &&
                  NetworkObjectRegistry.Instance.TryGet(persistentId, out _)) ||
                 issuedPersistentIds.Contains(persistentId));

        runtimeIdCounters[prefix] = counter;
        issuedPersistentIds.Add(persistentId);
        return persistentId;
    }

    public void RegisterIssuedPersistentId(string persistentId, UnityEngine.Object context = null, string reason = null)
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            return;
        }

        if (!issuedPersistentIds.Add(persistentId))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            PersistentWorldDebug.Log(
                $"runtime persistent ID reserved persistentId='{persistentId}' reason='{reason}'",
                context);
        }
    }

    public int ReconstructMissingRuntimeObjects(
        IReadOnlyList<PersistentObjectSnapshot> snapshots,
        NetworkObjectRegistry registry,
        bool serverSideLoad,
        SnapshotApplyResult result = null)
    {
        if (snapshots == null || registry == null)
        {
            return 0;
        }

        int spawnedCount = 0;

        for (int i = 0; i < snapshots.Count; i++)
        {
            PersistentObjectSnapshot snapshot = snapshots[i];
            if (snapshot == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(snapshot.PersistentId))
            {
                if (result != null)
                {
                    result.MissingPersistentIds++;
                }

                RecordRuntimeSpawnFailure(
                    snapshot,
                    result,
                    FailureCodeRecreationFailed,
                    "runtime reconstruction skipped because snapshot is missing a persistent ID");
                continue;
            }

            if (PersistentWorldSceneInstaller.TryResolveManagedSingletonPersistentObject(
                    snapshot.PersistentId,
                    snapshot.RuntimePrefabId,
                    out PersistentNetworkObject managedSingleton,
                    this,
                    $"spawn missing runtime objects serverSideLoad={serverSideLoad}"))
            {
                if (managedSingleton == null)
                {
                    RecordRuntimeSpawnFailure(
                        snapshot,
                        result,
                        FailureCodeRecreationFailed,
                        $"singleton runtime snapshot could not be resolved in place persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}'");
                    continue;
                }

                if (NetworkObjectRegistry.Instance != null)
                {
                    NetworkObjectRegistry.Instance.Register(managedSingleton);
                }

                PersistentWorldDebug.LogSnapshotObjectAudit(
                    "spawn missing runtime objects",
                    "runtime",
                    snapshot,
                    "resolved-in-place",
                    this,
                    managedSingleton);
                continue;
            }

            if ((!string.IsNullOrWhiteSpace(snapshot.PersistentId) &&
                 snapshot.PersistentId.StartsWith("runtime:singleton:", StringComparison.Ordinal)) ||
                (!string.IsNullOrWhiteSpace(snapshot.RuntimePrefabId) &&
                 snapshot.RuntimePrefabId.StartsWith(PersistentWorldSceneInstaller.SingletonPrefabPrefix, StringComparison.Ordinal)))
            {
                PersistentWorldDebug.Warn(
                    $"unregistered singleton runtime snapshot encountered persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}'. Register it as a managed singleton before using DontDestroyOnLoad persistence.",
                    this);
            }

            if (registry.TryGet(snapshot.PersistentId, out PersistentNetworkObject existing) && existing != null)
            {
                if (existing.ObjectKind != PersistentObjectKind.RuntimeSpawned ||
                    !string.Equals(existing.RuntimePrefabId ?? string.Empty, snapshot.RuntimePrefabId ?? string.Empty, StringComparison.Ordinal))
                {
                    string collisionMessage =
                        $"runtime reconstruction collision persistentId='{snapshot.PersistentId}' expectedPrefab='{snapshot.RuntimePrefabId}' actualPrefab='{existing.RuntimePrefabId}' actualKind='{existing.ObjectKind}' path='{PersistentWorldDebug.DescribeTransform(existing.transform)}'";
                    if (result != null)
                    {
                        result.ObjectTypeMismatches++;
                        result.AddError(collisionMessage);
                    }

                    PersistentWorldDebug.Error(collisionMessage, existing);
                    continue;
                }

                LogDuplicateReconstructionAvoided(snapshot, existing);
                PersistentWorldDebug.LogSnapshotObjectAudit(
                    "spawn missing runtime objects",
                    "runtime",
                    snapshot,
                    "runtime-live-existing",
                    this,
                    existing);
                continue;
            }

            if (string.IsNullOrWhiteSpace(snapshot.RuntimePrefabId))
            {
                RecordRuntimeSpawnFailure(
                    snapshot,
                    result,
                    FailureCodeMissingPrefabMapping,
                    $"runtime reconstruction missing prefab mapping persistentId='{snapshot.PersistentId}'");
                continue;
            }

            if (!TryInstantiateSnapshotObject(
                    snapshot,
                    serverSideLoad,
                    out PersistentNetworkObject instance,
                    out string failureCode,
                    out string failureMessage))
            {
                RecordRuntimeSpawnFailure(snapshot, result, failureCode, failureMessage);
                continue;
            }

            if (instance == null)
            {
                RecordRuntimeSpawnFailure(
                    snapshot,
                    result,
                    FailureCodeRecreationFailed,
                    $"runtime reconstruction returned null persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}'");
                continue;
            }

            if (PersistentWorldSceneInstaller.TryPrepareManagedSingletonResolvedInstance(
                    snapshot,
                    instance,
                    this,
                    "spawn missing runtime objects"))
            {
                if (NetworkObjectRegistry.Instance != null)
                {
                    NetworkObjectRegistry.Instance.Register(instance);
                }

                continue;
            }

            PrepareInstance(instance, snapshot.PersistentId, snapshot.RuntimePrefabId, serverSideLoad);
            if (!string.Equals(instance.PersistentId, snapshot.PersistentId, StringComparison.Ordinal))
            {
                string idMismatchMessage =
                    $"runtime reconstruction persistent ID mismatch expected='{snapshot.PersistentId}' actual='{instance.PersistentId}' prefab='{snapshot.RuntimePrefabId}'";
                if (result != null)
                {
                    result.ObjectTypeMismatches++;
                    result.AddError(idMismatchMessage);
                }

                PersistentWorldDebug.Error(idMismatchMessage, instance);
            }

            if (NetworkObjectRegistry.Instance == null ||
                !NetworkObjectRegistry.Instance.TryGet(snapshot.PersistentId, out PersistentNetworkObject registered) ||
                registered != instance)
            {
                RecordRuntimeSpawnFailure(
                    snapshot,
                    result,
                    FailureCodeRecreationFailed,
                    $"runtime reconstruction failed to register persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}'");
                PersistentWorldDebug.Error(
                    $"runtime reconstruction failed to register persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}'",
                    instance);
            }

            PersistentWorldDebug.LogSnapshotObjectAudit(
                "spawn missing runtime objects",
                "runtime",
                snapshot,
                "runtime-reconstructed",
                this,
                instance,
                $"serverSideLoad={serverSideLoad}");
            spawnedCount++;
        }

        return spawnedCount;
    }

    public int RemoveRuntimeObjectsNotInSnapshot(IReadOnlyList<PersistentObjectSnapshot> snapshots, NetworkObjectRegistry registry, bool serverSideLoad)
    {
        if (registry == null)
        {
            return 0;
        }

        HashSet<string> validIds = new HashSet<string>();
        if (snapshots != null)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                PersistentObjectSnapshot snapshot = snapshots[i];
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.PersistentId))
                {
                    continue;
                }

                validIds.Add(snapshot.PersistentId);
            }
        }

        List<PersistentNetworkObject> runtimeObjects = registry.GetRuntimeObjects();
        int removedCount = 0;
        for (int i = 0; i < runtimeObjects.Count; i++)
        {
            PersistentNetworkObject runtimeObject = runtimeObjects[i];
            if (runtimeObject == null || validIds.Contains(runtimeObject.PersistentId) || !runtimeObject.DestroyIfMissingFromSnapshot)
            {
                continue;
            }

            PersistentWorldDebug.Log(
                $"remove invalid objects removing persistentId='{runtimeObject.PersistentId}' prefab='{runtimeObject.RuntimePrefabId}' serverSideLoad={serverSideLoad}",
                runtimeObject);

            NetworkObject networkObject = runtimeObject.GetComponent<NetworkObject>();
            if (serverSideLoad && networkObject != null && networkObject.IsSpawned && runtimeObject.IsServer)
            {
                networkObject.Despawn(true);
                removedCount++;
                continue;
            }

            if (!serverSideLoad && networkObject != null && networkObject.IsSpawned)
            {
                runtimeObject.gameObject.SetActive(false);
                removedCount++;
                continue;
            }

            Destroy(runtimeObject.gameObject);
            removedCount++;
        }

        return removedCount;
    }

    private void PrepareInstance(PersistentNetworkObject instance, string persistentId, string prefabId, bool spawnNetworkObject)
    {
        if (instance == null)
        {
            return;
        }

        RegisterIssuedPersistentId(persistentId, instance, "prepare_instance");
        PersistentWorldSceneInstaller.PrepareRuntimeReconstructedObject(instance.gameObject, prefabId, persistentId);
        instance.AssignRuntimeIdentity(persistentId, prefabId);
        instance.SetDestroyIfMissingFromSnapshot(true);

        if (NetworkObjectRegistry.Instance != null)
        {
            NetworkObjectRegistry.Instance.Register(instance);
        }

        if (!spawnNetworkObject || !IsServer())
        {
            return;
        }

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject != null && !networkObject.IsSpawned)
        {
            networkObject.Spawn(true);
        }
    }

    private bool TryInstantiateSnapshotObject(
        PersistentObjectSnapshot snapshot,
        bool serverSideLoad,
        out PersistentNetworkObject instance,
        out string failureCode,
        out string failureMessage)
    {
        instance = null;
        failureCode = string.Empty;
        failureMessage = string.Empty;
        if (snapshot == null)
        {
            failureCode = FailureCodeRecreationFailed;
            failureMessage = "runtime reconstruction failed because snapshot is null";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.RuntimePrefabId) &&
            TryGetDefinition(snapshot.RuntimePrefabId, out RuntimePersistentPrefab definition) &&
            definition != null)
        {
            if (definition.Prefab == null)
            {
                failureCode = FailureCodeMissingPrefabMapping;
                failureMessage =
                    $"runtime reconstruction has null registered prefab persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}'";
                return false;
            }

            if (!serverSideLoad && !definition.AllowClientSideReconstruction)
            {
                failureCode = FailureCodeRecreationFailed;
                failureMessage =
                    $"runtime reconstruction skipped for host-only prefab persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}'";
                return false;
            }

            instance = Instantiate(definition.Prefab, snapshot.Transform.Position, snapshot.Transform.Rotation);
            if (instance == null)
            {
                failureCode = FailureCodeRecreationFailed;
                failureMessage =
                    $"runtime reconstruction instantiate returned null persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}'";
                return false;
            }

            return true;
        }

        return TryInstantiateFallbackSnapshotObject(snapshot, serverSideLoad, out instance, out failureCode, out failureMessage);
    }

    private bool TryInstantiateFallbackSnapshotObject(
        PersistentObjectSnapshot snapshot,
        bool serverSideLoad,
        out PersistentNetworkObject instance,
        out string failureCode,
        out string failureMessage)
    {
        instance = null;
        failureCode = string.Empty;
        failureMessage = string.Empty;
        string runtimePrefabId = snapshot != null ? snapshot.RuntimePrefabId ?? string.Empty : string.Empty;
        if (string.IsNullOrWhiteSpace(runtimePrefabId))
        {
            failureCode = FailureCodeMissingPrefabMapping;
            failureMessage = $"missing runtime prefab id for persistent object '{snapshot?.PersistentId}'.";
            return false;
        }

        if (runtimePrefabId.StartsWith(PersistentWorldSceneInstaller.BuildingPrefabPrefix, StringComparison.Ordinal))
        {
            string buildingItemId = runtimePrefabId.Substring(PersistentWorldSceneInstaller.BuildingPrefabPrefix.Length);
            Item building = ItemRegistry.Resolve(buildingItemId);
            GameObject prefab = building != null ? (building.buildingPrefab != null ? building.buildingPrefab : building.worldPrefab) : null;
            if (prefab == null)
            {
                failureCode = FailureCodeRecreationFailed;
                failureMessage =
                    $"runtime reconstruction failed to resolve building prefab persistentId='{snapshot.PersistentId}' prefab='{runtimePrefabId}'";
                return false;
            }

            Transform parent = ResolveBuildingParent();
            GameObject created = parent != null
                ? Instantiate(prefab, snapshot.Transform.Position, snapshot.Transform.Rotation, parent)
                : Instantiate(prefab, snapshot.Transform.Position, snapshot.Transform.Rotation);
            instance = created != null ? NetcodeRuntimeUtilities.GetOrAdd<PersistentNetworkObject>(created) : null;
            PersistentWorldDebug.Log(
                $"spawn missing runtime objects instantiated building persistentId='{snapshot.PersistentId}' prefab='{runtimePrefabId}'",
                created);
            return instance != null;
        }

        if (runtimePrefabId.StartsWith(PersistentWorldSceneInstaller.CharacterPrefabPrefix, StringComparison.Ordinal))
        {
            if (!serverSideLoad)
            {
                failureCode = FailureCodeRecreationFailed;
                failureMessage =
                    $"client-side character reconstruction skipped persistentId='{snapshot.PersistentId}' prefab='{runtimePrefabId}' because runtime characters must resolve from already spawned NGO player objects";
                return false;
            }

            string characterId = runtimePrefabId.Substring(PersistentWorldSceneInstaller.CharacterPrefabPrefix.Length);
            if (!NetcodeCharacterIdentity.TryResolveCharacterData(characterId, out CharacterData character))
            {
                failureCode = FailureCodeRecreationFailed;
                failureMessage =
                    $"character '{characterId}' could not be resolved for reconstruction persistentId='{snapshot.PersistentId}'";
                return false;
            }

            Transform parent = SquadManager.Instance != null ? SquadManager.Instance.squadCharactersParent : null;
            GameObject created = NetcodePrefabRegistry.SpawnCharacterInstance(character, snapshot.Transform.Position, snapshot.Transform.Rotation, parent);
            instance = created != null ? NetcodeRuntimeUtilities.GetOrAdd<PersistentNetworkObject>(created) : null;
            PersistentWorldDebug.Log(
                $"spawn missing runtime objects instantiated character persistentId='{snapshot.PersistentId}' prefab='{runtimePrefabId}'",
                created);
            return instance != null;
        }

        if (runtimePrefabId.StartsWith(PersistentWorldSceneInstaller.DroppedLootPrefabPrefix, StringComparison.Ordinal))
        {
            string itemId = runtimePrefabId.Substring(PersistentWorldSceneInstaller.DroppedLootPrefabPrefix.Length);
            Item item = ItemRegistry.Resolve(itemId);
            if (item == null)
            {
                failureCode = FailureCodeRecreationFailed;
                failureMessage =
                    $"dropped-loot item '{itemId}' could not be resolved for runtime reconstruction persistentId='{snapshot.PersistentId}'";
                return false;
            }

            GameObject created = NetcodePrefabRegistry.SpawnItemInstance(item, true, snapshot.Transform.Position, snapshot.Transform.Rotation);
            InteractableItem lootContainer = created != null
                ? (created.GetComponent<InteractableItem>() ?? created.GetComponentInChildren<InteractableItem>(true))
                : null;
            if (lootContainer != null)
            {
                lootContainer.interactableCategory = InteractableItem.InteractableCategory.RecoverableItem;
                lootContainer.representedItem = item;
            }

            instance = created != null ? NetcodeRuntimeUtilities.GetOrAdd<PersistentNetworkObject>(created) : null;
            PersistentWorldDebug.Log(
                $"spawn missing runtime objects instantiated dropped-loot persistentId='{snapshot.PersistentId}' prefab='{runtimePrefabId}'",
                created);
            return instance != null;
        }

        if (runtimePrefabId.StartsWith(PersistentWorldSceneInstaller.ItemPrefabPrefix, StringComparison.Ordinal))
        {
            string itemId = runtimePrefabId.Substring(PersistentWorldSceneInstaller.ItemPrefabPrefix.Length);
            Item item = ItemRegistry.Resolve(itemId);
            if (item == null)
            {
                failureCode = FailureCodeRecreationFailed;
                failureMessage =
                    $"item '{itemId}' could not be resolved for runtime reconstruction persistentId='{snapshot.PersistentId}'";
                return false;
            }

            GameObject created = NetcodePrefabRegistry.SpawnItemInstance(item, false, snapshot.Transform.Position, snapshot.Transform.Rotation);
            instance = created != null ? NetcodeRuntimeUtilities.GetOrAdd<PersistentNetworkObject>(created) : null;
            PersistentWorldDebug.Log(
                $"spawn missing runtime objects instantiated item persistentId='{snapshot.PersistentId}' prefab='{runtimePrefabId}'",
                created);
            return instance != null;
        }

        if (string.Equals(runtimePrefabId, PersistentWorldSceneInstaller.KnowledgeManagerPrefabId, StringComparison.Ordinal))
        {
            KnowledgeManager manager = KnowledgeManager.GetOrCreate();
            PersistentWorldSceneInstaller.EnsureRuntimeKnowledgeManager(manager);
            instance = manager != null ? NetcodeRuntimeUtilities.GetOrAdd<PersistentNetworkObject>(manager.gameObject) : null;
            PersistentWorldDebug.Log(
                $"spawn missing runtime objects instantiated singleton persistentId='{snapshot.PersistentId}' prefab='{runtimePrefabId}'",
                manager);
            return instance != null;
        }

        failureCode = FailureCodeMissingPrefabMapping;
        failureMessage =
            $"no registered or fallback runtime prefab for '{runtimePrefabId}' persistentId='{snapshot?.PersistentId}'";
        return false;
    }

    private static Transform ResolveBuildingParent()
    {
#if UNITY_2023_1_OR_NEWER
        BuilderController builderController = FindFirstObjectByType<BuilderController>();
#else
        BuilderController builderController = FindObjectOfType<BuilderController>();
#endif
        if (builderController == null)
        {
            return null;
        }

        return builderController.buildingsRoot;
    }

    private void BuildLookup()
    {
        prefabLookup.Clear();
        if (runtimePrefabs == null)
        {
            return;
        }

        for (int i = 0; i < runtimePrefabs.Count; i++)
        {
            RuntimePersistentPrefab definition = runtimePrefabs[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.PrefabId))
            {
                continue;
            }

            prefabLookup[definition.PrefabId] = definition;
        }
    }

    private void ReserveExistingRuntimeIds()
    {
        issuedPersistentIds.Clear();

        if (NetworkObjectRegistry.Instance != null)
        {
            List<PersistentNetworkObject> runtimeObjects = NetworkObjectRegistry.Instance.GetRuntimeObjects();
            for (int i = 0; i < runtimeObjects.Count; i++)
            {
                PersistentNetworkObject runtimeObject = runtimeObjects[i];
                if (runtimeObject == null || string.IsNullOrWhiteSpace(runtimeObject.PersistentId))
                {
                    continue;
                }

                issuedPersistentIds.Add(runtimeObject.PersistentId);
            }

            return;
        }

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
            PersistentNetworkObject runtimeObject = found[i];
            if (runtimeObject == null ||
                runtimeObject.ObjectKind != PersistentObjectKind.RuntimeSpawned ||
                string.IsNullOrWhiteSpace(runtimeObject.PersistentId))
            {
                continue;
            }

            issuedPersistentIds.Add(runtimeObject.PersistentId);
        }
    }

    private bool TryGetDefinition(string prefabId, out RuntimePersistentPrefab definition)
    {
        if (!prefabLookup.TryGetValue(prefabId ?? string.Empty, out definition) || definition == null)
        {
            BuildLookup();
            return prefabLookup.TryGetValue(prefabId ?? string.Empty, out definition) && definition != null;
        }

        return true;
    }

    private void RecordRuntimeSpawnFailure(
        PersistentObjectSnapshot snapshot,
        SnapshotApplyResult result,
        string failureCode,
        string failureMessage)
    {
        if (result != null)
        {
            bool missingPersistentId = snapshot != null && string.IsNullOrWhiteSpace(snapshot.PersistentId);
            if (missingPersistentId)
            {
                result.AddError(failureMessage);
            }
            else if (string.Equals(failureCode, FailureCodeMissingPrefabMapping, StringComparison.Ordinal))
            {
                result.MissingRuntimePrefabMappings++;
                result.AddError(failureMessage);
            }
            else
            {
                result.FailedRuntimeRecreations++;
                result.AddError(failureMessage);
            }
        }

        if (!string.IsNullOrWhiteSpace(failureMessage))
        {
            PersistentWorldDebug.Error(failureMessage, this);
        }

        if (snapshot != null)
        {
            string expectedResolutionMode = PersistentWorldSceneInstaller.DescribeExpectedResolutionMode(snapshot);
            PersistentWorldDebug.LogSnapshotObjectAudit(
                "spawn missing runtime objects",
                "runtime",
                snapshot,
                expectedResolutionMode,
                this,
                null,
                $"failureCode='{failureCode}' reason='{failureMessage}'");
        }
    }

    private void LogDuplicateReconstructionAvoided(PersistentObjectSnapshot snapshot, PersistentNetworkObject existing)
    {
        if (snapshot == null || existing == null)
        {
            return;
        }

        string runtimePrefabId = snapshot.RuntimePrefabId ?? string.Empty;
        if (runtimePrefabId.StartsWith(PersistentWorldSceneInstaller.DroppedLootPrefabPrefix, StringComparison.Ordinal))
        {
            PersistentWorldDebug.Warn(
                $"duplicate dropped-loot reconstruction avoided persistentId='{snapshot.PersistentId}' prefab='{runtimePrefabId}' existingPath='{PersistentWorldDebug.DescribeTransform(existing.transform)}'",
                existing);
            return;
        }

        if (runtimePrefabId.StartsWith(PersistentWorldSceneInstaller.BuildingPrefabPrefix, StringComparison.Ordinal))
        {
            PersistentWorldDebug.Warn(
                $"duplicate building reconstruction avoided persistentId='{snapshot.PersistentId}' prefab='{runtimePrefabId}' existingPath='{PersistentWorldDebug.DescribeTransform(existing.transform)}'",
                existing);
            return;
        }

        PersistentWorldDebug.Log(
            $"spawn missing runtime objects skipped existing persistentId='{snapshot.PersistentId}' prefab='{runtimePrefabId}'",
            existing);
    }

    private static bool IsServer()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }

    private static string SanitizeIdSegment(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        string sanitized = value.Trim().Replace(' ', '_').Replace(':', '_').Replace('|', '_').Replace('/', '_').Replace('\\', '_');
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }
}
