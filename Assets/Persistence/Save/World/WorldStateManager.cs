using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class WorldStateManager : MonoBehaviour
{
    public static WorldStateManager Instance { get; private set; }
    [SerializeField] private NetworkObjectRegistry registry;
    [SerializeField] private SpawnManager spawnManager;
    [SerializeField] private WorldRulesStateManager worldRulesStateManager;

    public event Action<WorldSnapshot> SnapshotApplied;

    public SnapshotApplyResult LastApplyResult { get; private set; }

    public bool LastApplySucceeded => LastApplyResult != null && LastApplyResult.Succeeded;
    public bool LastCaptureSucceeded { get; private set; }

    private readonly List<PersistentObjectSnapshot> preservedLegacyBuildingSnapshots =
        new List<PersistentObjectSnapshot>();
    private readonly Dictionary<string, PersistentSceneArchive> sceneArchives =
        new Dictionary<string, PersistentSceneArchive>(StringComparer.Ordinal);

    private void Awake()
    {
        Instance = this;
        ResolveReferences();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ResetSceneArchives()
    {
        sceneArchives.Clear();
        preservedLegacyBuildingSnapshots.Clear();
        LastApplyResult = null;
        LastCaptureSucceeded = false;
    }

    /// <summary>Records a gameplay removal before the scene object is destroyed.</summary>
    public void RecordSceneObjectRemoved(PersistentNetworkObject target)
    {
        if (!HasWorldAuthority() || target == null || target.ObjectKind != PersistentObjectKind.ScenePlaced ||
            !target.HasPersistentId || !target.gameObject.scene.IsValid()) return;
        string sceneName = target.gameObject.scene.name;
        PersistentSceneArchive archive = GetOrCreateArchive(sceneName);
        if (!archive.RemovedSceneObjectIds.Contains(target.PersistentId))
            archive.RemovedSceneObjectIds.Add(target.PersistentId);
    }

    /// <summary>Capture departure state while the scene's objects still exist.</summary>
    public bool CaptureScenesBeforeUnload(IEnumerable<string> sceneNames)
    {
        if (!HasWorldAuthority() || sceneNames == null) return true;
        ResolveReferences();
        registry?.RefreshSceneObjects();
        var snapshot = new WorldSnapshot { SceneName = SceneManager.GetActiveScene().name };
        var scope = new HashSet<string>(sceneNames, StringComparer.Ordinal);
        if (!CapturePersistentObjects(snapshot, true, scope)) return false;
        foreach (string sceneName in scope)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) continue;
            UpdateSceneArchive(sceneName, snapshot);
        }
        return true;
    }

    /// <summary>Replays only returning scenes, leaving live session objects untouched.</summary>
    public bool RestoreArchivedScenes(IEnumerable<string> sceneNames)
    {
        if (!HasWorldAuthority() || sceneNames == null) return true;
        var scope = new HashSet<string>(StringComparer.Ordinal);
        var snapshot = new WorldSnapshot { SceneName = SceneManager.GetActiveScene().name };
        foreach (string name in sceneNames)
        {
            if (string.IsNullOrWhiteSpace(name) || !sceneArchives.TryGetValue(name, out PersistentSceneArchive archive) ||
                !scope.Add(name)) continue;
            Scene scene = SceneManager.GetSceneByName(name);
            if (!scene.IsValid() || !scene.isLoaded) { scope.Remove(name); continue; }
            PersistentSceneArchive copy = CloneArchive(archive);
            snapshot.SceneArchives.Add(copy);
            snapshot.SceneObjects.AddRange(copy.SceneObjects);
            snapshot.RuntimeObjects.AddRange(copy.RuntimeObjects);
        }
        if (snapshot.SceneArchives.Count == 0) return true;
        return ApplySnapshotInternal(snapshot, true, scope);
    }

    private PersistentSceneArchive GetOrCreateArchive(string sceneName)
    {
        if (!sceneArchives.TryGetValue(sceneName, out PersistentSceneArchive archive))
        {
            archive = new PersistentSceneArchive { SceneName = sceneName };
            sceneArchives.Add(sceneName, archive);
        }
        return archive;
    }

    private void UpdateSceneArchive(string sceneName, WorldSnapshot live)
    {
        PersistentSceneArchive archive = GetOrCreateArchive(sceneName);
        archive.SceneObjects.Clear();
        archive.RuntimeObjects.Clear();
        foreach (PersistentObjectSnapshot entry in live.SceneObjects)
            if (entry.SceneName == sceneName && !archive.RemovedSceneObjectIds.Contains(entry.PersistentId))
                archive.SceneObjects.Add(entry);
        foreach (PersistentObjectSnapshot entry in live.RuntimeObjects)
            if (entry.SceneName == sceneName) archive.RuntimeObjects.Add(entry);
    }

    private static PersistentSceneArchive CloneArchive(PersistentSceneArchive archive)
    {
        // Providers own their payload arrays. Never retain references to an
        // incoming late-join/disk snapshot that may later be normalized.
        return JsonUtility.FromJson<PersistentSceneArchive>(JsonUtility.ToJson(archive));
    }

    private static bool HasWorldAuthority()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager == null || !manager.IsListening || manager.IsServer;
    }

    private void ApplyRemovedSceneObjects(List<PersistentSceneArchive> archives)
    {
        if (archives == null || registry == null) return;
        foreach (PersistentSceneArchive archive in archives)
        {
            Scene scene = SceneManager.GetSceneByName(archive.SceneName);
            if (!scene.IsValid() || !scene.isLoaded) continue;
            foreach (string id in archive.RemovedSceneObjectIds)
            {
                if (!registry.TryGetSceneObject(id, out PersistentNetworkObject target) || target == null ||
                    target.gameObject.scene.name != archive.SceneName) continue;
                NetworkObject networkObject = target.GetComponent<NetworkObject>();
                if (HasWorldAuthority() && networkObject != null && networkObject.IsSpawned && target.IsServer)
                    networkObject.Despawn(false);
                target.gameObject.SetActive(false);
            }
        }
    }

    public WorldSnapshot CaptureSnapshot(string captureReason = null, bool logValidationFailuresAsErrors = true)
    {
        string resolvedCaptureReason = string.IsNullOrWhiteSpace(captureReason)
            ? "capture snapshot"
            : captureReason;

        ResolveReferences();
        int liveSingletonCount = PersistentWorldSceneInstaller.EnsureLiveManagedSingletons(this, resolvedCaptureReason);
        registry?.RefreshSceneObjects();
        worldRulesStateManager?.RebuildDerivedFlameVariables();

        WorldSnapshot snapshot = new WorldSnapshot
        {
            SceneName = SceneManager.GetActiveScene().name,
            CapturedAtTime = GetCaptureTime()
        };

        CapturePlayers(snapshot);
        snapshot.Abilities = AbilityService.Ensure().Capture();
        LastCaptureSucceeded = CapturePersistentObjects(snapshot, logValidationFailuresAsErrors);
        if (HasWorldAuthority() && LastCaptureSucceeded)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) UpdateSceneArchive(scene.name, snapshot);
            }
        }
        foreach (PersistentSceneArchive archive in sceneArchives.Values)
            snapshot.SceneArchives.Add(CloneArchive(archive));
        snapshot.SceneArchives.Sort((a, b) => string.CompareOrdinal(a.SceneName, b.SceneName));
        CaptureWorldRules(snapshot);
        LegacyBuildingPersistenceMigration.MergeLegacyWorldSnapshots(
            snapshot,
            preservedLegacyBuildingSnapshots);
        int normalizedSingletonCount = PersistentWorldSceneInstaller.NormalizeManagedSingletonSnapshots(snapshot, this, resolvedCaptureReason);
        SortSnapshot(snapshot);
        AuditSnapshot(snapshot, resolvedCaptureReason, "scene-export", "runtime-export");

        if (liveSingletonCount > 0 || normalizedSingletonCount > 0)
        {
            PersistentWorldDebug.Log(
                $"snapshot capture prepared liveSingletons={liveSingletonCount} normalizedSingletons={normalizedSingletonCount} reason='{resolvedCaptureReason}'",
                this);
        }

        return snapshot;
    }

    public bool ApplySnapshot(WorldSnapshot snapshot, bool serverSideLoad = false)
    {
        return ApplySnapshotInternal(snapshot, serverSideLoad, null);
    }

    // Validate data before sending it without loading scenes or replaying any
    // gameplay provider. Archives of absent scenes deliberately remain data.
    internal SnapshotApplyResult ValidateSnapshotStructure(WorldSnapshot snapshot)
    {
        var result = new SnapshotApplyResult();
        if (snapshot == null)
        {
            result.AddError("Snapshot is missing.");
            return result;
        }
        ValidateSnapshotIdentityGraph(snapshot, result);
        ValidateSceneArchives(snapshot.SceneArchives, result);
        return result;
    }

    private bool ApplySnapshotInternal(WorldSnapshot snapshot, bool serverSideLoad, HashSet<string> sceneScope)
    {
        SnapshotApplyResult result = new SnapshotApplyResult();
        LastApplyResult = result;

        if (snapshot == null)
        {
            string message = "snapshot apply aborted because snapshot is null";
            result.AddError(message);
            PersistentWorldDebug.Error(message, this);
            return false;
        }

        ResolveReferences();
        if (sceneScope == null) preservedLegacyBuildingSnapshots.Clear();
        if (sceneScope == null && !LegacyBuildingSystem.Enabled && LegacyBuildingSystem.PreserveLegacyWorldSnapshots)
        {
            preservedLegacyBuildingSnapshots.AddRange(
                LegacyBuildingPersistenceMigration.CaptureLegacyWorldSnapshots(snapshot));
        }

        int normalizedSingletonCount = sceneScope == null
            ? PersistentWorldSceneInstaller.NormalizeManagedSingletonSnapshots(snapshot, this, "apply snapshot") : 0;
        int ensuredSingletonCount = sceneScope == null
            ? PersistentWorldSceneInstaller.EnsureManagedSingletonsForSnapshot(snapshot, this, "apply snapshot") : 0;
        registry?.RefreshSceneObjects();

        if (normalizedSingletonCount > 0 || ensuredSingletonCount > 0)
        {
            PersistentWorldDebug.Log(
                $"singleton snapshot preparation normalized={normalizedSingletonCount} ensured={ensuredSingletonCount}",
                this);
        }

        AuditSnapshot(snapshot, "apply snapshot prepared", "scene-pending", "runtime-pending");

        PersistentStateContext context = new PersistentStateContext(
            snapshot,
            registry,
            spawnManager,
            worldRulesStateManager,
            IsServer());

        PersistentWorldDebug.Log(
            $"snapshot received scene='{snapshot.SceneName}' runtimeObjects={snapshot.RuntimeObjects?.Count ?? 0} sceneObjects={snapshot.SceneObjects?.Count ?? 0}",
            this);

        ValidateSnapshotIdentityGraph(snapshot, result);
        ValidateSceneArchives(snapshot.SceneArchives, result);
        if (!result.Succeeded) return FinalizeApply(snapshot, context, result);
        if (!string.IsNullOrWhiteSpace(snapshot.SceneName) &&
            !string.Equals(snapshot.SceneName, SceneManager.GetActiveScene().name, StringComparison.Ordinal))
        {
            string sceneMismatch =
                $"snapshot scene '{snapshot.SceneName}' does not match active scene '{SceneManager.GetActiveScene().name}'.";
            result.AddError(sceneMismatch);
            PersistentWorldDebug.Error(sceneMismatch, this);
            return FinalizeApply(snapshot, context, result);
        }

        if (sceneScope == null)
        {
            sceneArchives.Clear();
            if (snapshot.SceneArchives != null)
                foreach (PersistentSceneArchive archive in snapshot.SceneArchives)
                {
                    sceneArchives.Add(archive.SceneName, CloneArchive(archive));
                    foreach (PersistentObjectSnapshot runtime in archive.RuntimeObjects)
                        spawnManager?.RegisterIssuedPersistentId(runtime.PersistentId);
                }
        }
        ApplyRemovedSceneObjects(snapshot.SceneArchives);

        if (!ExecuteReconstructionPhase(
                "resolve scene objects",
                PersistentApplyPhase.ResolveSceneObjects,
                result,
                context,
                () => ResolveSceneObjects(snapshot, result)))
        {
            return FinalizeApply(snapshot, context, result);
        }

        if (!ExecuteReconstructionPhase(
                "resolve runtime objects",
                PersistentApplyPhase.SpawnMissingRuntimeObjects,
                result,
                context,
                () =>
                {
                    PersistentWorldDebug.Log("spawn missing runtime objects", this);
                    int spawnedCount = spawnManager != null
                        ? spawnManager.ReconstructMissingRuntimeObjects(snapshot.RuntimeObjects, registry, serverSideLoad, result)
                        : 0;
                    PersistentWorldDebug.Log($"spawn missing runtime objects result count={spawnedCount}", this);

                    PersistentWorldDebug.Log("remove invalid objects", this);
                    context.SetCurrentPhase(PersistentApplyPhase.RemoveInvalidObjects);
                    int removedCount = spawnManager != null
                        ? spawnManager.RemoveRuntimeObjectsNotInSnapshot(snapshot.RuntimeObjects, registry, serverSideLoad, sceneScope)
                        : 0;
                    PersistentWorldDebug.Log($"remove invalid objects result count={removedCount}", this);

                    if (spawnedCount > 0 || removedCount > 0)
                    {
                        registry?.RefreshSceneObjects();
                    }

                    context.SetCurrentPhase(PersistentApplyPhase.SpawnMissingRuntimeObjects);
                    ValidateRuntimeObjects(snapshot, result);
                }))
        {
            return FinalizeApply(snapshot, context, result);
        }

        if (!ExecuteReconstructionPhase(
                "apply transforms",
                PersistentApplyPhase.ApplyTransformsAndActives,
                result,
                context,
                () => ApplyTransforms(snapshot, context, result)))
        {
            return FinalizeApply(snapshot, context, result);
        }

        if (!ExecuteReconstructionPhase(
                "apply state providers",
                PersistentApplyPhase.ApplyGameplayState,
                result,
                context,
                () => ApplyGameplayState(snapshot, context, result, sceneScope == null)))
        {
            return FinalizeApply(snapshot, context, result);
        }

        if (!ExecuteReconstructionPhase(
                "finalize references",
                PersistentApplyPhase.FinalizeReferences,
                result,
                context,
                () => FinalizeReferences(snapshot, context, result)))
        {
            return FinalizeApply(snapshot, context, result);
        }

        return FinalizeApply(snapshot, context, result);
    }

    public PersistentNetworkObject ResolveControlledObject(WorldSnapshot snapshot, ulong clientId)
    {
        if (snapshot == null || snapshot.Players == null || registry == null)
        {
            return null;
        }

        for (int i = 0; i < snapshot.Players.Count; i++)
        {
            PlayerSnapshot playerSnapshot = snapshot.Players[i];
            if (playerSnapshot == null || playerSnapshot.OwnerClientId != clientId)
            {
                continue;
            }

            if (registry.TryGet(playerSnapshot.ControlledObjectId, out PersistentNetworkObject persistentObject))
            {
                return persistentObject;
            }
        }

        return null;
    }

    private void CapturePlayers(WorldSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

#if UNITY_2023_1_OR_NEWER
        NetcodeCharacterIdentity[] identities = FindObjectsByType<NetcodeCharacterIdentity>(FindObjectsInactive.Include);
#else
        NetcodeCharacterIdentity[] identities = FindObjectsByType<NetcodeCharacterIdentity>(FindObjectsInactive.Include);
#endif
        if (identities == null)
        {
            return;
        }

        for (int i = 0; i < identities.Length; i++)
        {
            NetcodeCharacterIdentity identity = identities[i];
            if (identity == null)
            {
                continue;
            }

            NetworkObject networkObject = identity.GetComponent<NetworkObject>();
            PersistentNetworkObject persistentObject = identity.GetComponent<PersistentNetworkObject>();
            if (networkObject == null || persistentObject == null || string.IsNullOrWhiteSpace(persistentObject.PersistentId))
            {
                continue;
            }

            string playerId = string.Empty;
            NetcodePlayerSessionRegistry.TryGetPlayerId(networkObject.OwnerClientId, out playerId);

            snapshot.Players.Add(new PlayerSnapshot
            {
                OwnerClientId = networkObject.OwnerClientId,
                PlayerId = playerId ?? string.Empty,
                CharacterId = identity.CharacterId,
                ControlledObjectId = persistentObject.PersistentId,
                Position = identity.transform.position,
                Rotation = identity.transform.rotation,
                CustomState = Array.Empty<byte>()
            });
        }
    }

    private bool CapturePersistentObjects(WorldSnapshot snapshot, bool logValidationFailuresAsErrors, ISet<string> sceneScope = null)
    {
        if (snapshot == null || registry == null)
        {
            return false;
        }

        List<PersistentNetworkObject> objects = registry.GetAllObjects();
        PersistentStateContext context = new PersistentStateContext(
            snapshot,
            registry,
            spawnManager,
            worldRulesStateManager,
            IsServer(),
            logValidationFailuresAsErrors);

        for (int i = 0; i < objects.Count; i++)
        {
            PersistentNetworkObject persistentObject = objects[i];
            if (persistentObject == null || string.IsNullOrWhiteSpace(persistentObject.PersistentId))
            {
                continue;
            }
            string objectSceneName = persistentObject.gameObject.scene.name ?? string.Empty;
            if (sceneScope != null && !sceneScope.Contains(objectSceneName)) continue;

            if (persistentObject.ObjectKind == PersistentObjectKind.ScenePlaced &&
                sceneArchives.TryGetValue(objectSceneName, out PersistentSceneArchive removedArchive) &&
                removedArchive.RemovedSceneObjectIds.Contains(persistentObject.PersistentId)) continue;

            PrepareObjectForCapture(persistentObject);
            PersistentObjectSnapshot objectSnapshot = persistentObject.CaptureSnapshot(context);
            if (!ValidateSnapshotForExport(objectSnapshot, persistentObject, context))
            {
                continue;
            }

            if (objectSnapshot.ObjectKind == PersistentObjectKind.ScenePlaced)
            {
                snapshot.SceneObjects.Add(objectSnapshot);
            }
            else
            {
                snapshot.RuntimeObjects.Add(objectSnapshot);
            }
        }
        return context.ValidationIssueCount == 0;
    }

    private void ValidateSceneArchives(List<PersistentSceneArchive> archives, SnapshotApplyResult result)
    {
        if (archives == null) return;
        var scenes = new HashSet<string>(StringComparer.Ordinal);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (PersistentSceneArchive archive in archives)
        {
            if (archive == null || string.IsNullOrWhiteSpace(archive.SceneName) || !scenes.Add(archive.SceneName) ||
                archive.SceneObjects == null || archive.RuntimeObjects == null || archive.RemovedSceneObjectIds == null)
            {
                result.AddError("Snapshot contains a missing or duplicate scene archive.");
                continue;
            }
            ValidateSnapshotIdentityGraph(archive.SceneObjects, PersistentObjectKind.ScenePlaced, identities, result);
            ValidateSnapshotIdentityGraph(archive.RuntimeObjects, PersistentObjectKind.RuntimeSpawned, identities, result);
            foreach (PersistentObjectSnapshot entry in archive.SceneObjects)
                if (entry == null || entry.SceneName != archive.SceneName || archive.RemovedSceneObjectIds.Contains(entry.PersistentId))
                    result.AddError("Scene archive contains a removed object or an object belonging to a different scene.");
            foreach (PersistentObjectSnapshot entry in archive.RuntimeObjects)
                if (entry == null || entry.SceneName != archive.SceneName)
                    result.AddError("Scene archive runtime object belongs to a different scene.");
            foreach (string removed in archive.RemovedSceneObjectIds)
                if (string.IsNullOrWhiteSpace(removed) || !identities.Add(removed))
                    result.AddError("Scene archive contains a missing or duplicate removed object identity.");
        }
    }

    private void CaptureWorldRules(WorldSnapshot snapshot)
    {
        if (snapshot == null || worldRulesStateManager == null)
        {
            return;
        }

        snapshot.WorldVariables = worldRulesStateManager.CaptureVariables();
    }

    private void ResolveSceneObjects(WorldSnapshot snapshot, SnapshotApplyResult result)
    {
        if (snapshot == null || snapshot.SceneObjects == null || registry == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.SceneObjects.Count; i++)
        {
            PersistentObjectSnapshot objectSnapshot = snapshot.SceneObjects[i];
            if (objectSnapshot == null || string.IsNullOrWhiteSpace(objectSnapshot.PersistentId))
            {
                if (objectSnapshot != null)
                {
                    RecordResolveFailure(objectSnapshot, PersistentResolveStatus.MissingPersistentId, "resolve scene objects", result);
                }

                continue;
            }

            if (!registry.TryResolveSnapshotObject(
                    objectSnapshot,
                    out PersistentNetworkObject resolvedObject,
                    out PersistentResolveStatus status,
                    this,
                    "resolve scene objects",
                    requireSceneObject: true))
            {
                if (status == PersistentResolveStatus.MissingObject)
                {
                    result.MissingSceneObjects++;
                }

                RecordResolveFailure(objectSnapshot, status, "resolve scene objects", result);
                PersistentWorldDebug.LogSnapshotObjectAudit(
                    "resolve scene objects",
                    "scene",
                    objectSnapshot,
                    PersistentWorldSceneInstaller.DescribeExpectedResolutionMode(objectSnapshot),
                    this,
                    null,
                    $"status='{status}' reason='{DescribeResolveFailureReason(objectSnapshot, status)}'");
                continue;
            }

            PersistentWorldDebug.LogSnapshotObjectAudit(
                "resolve scene objects",
                "scene",
                objectSnapshot,
                "scene-resolved",
                this,
                resolvedObject);
        }
    }

    private void ApplyTransforms(WorldSnapshot snapshot, PersistentStateContext context, SnapshotApplyResult result)
    {
        if (registry == null || snapshot == null)
        {
            return;
        }

        // Runtime items are created in the currently active Core by their
        // factory. Restore their additive ownership before the next departure.
        // Characters/session singletons keep their persistent parent/root.
        foreach (PersistentObjectSnapshot entry in snapshot.RuntimeObjects)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.RuntimePrefabId) ||
                entry.RuntimePrefabId.StartsWith(PersistentWorldSceneInstaller.CharacterPrefabPrefix, StringComparison.Ordinal) ||
                entry.RuntimePrefabId.StartsWith(PersistentWorldSceneInstaller.SingletonPrefabPrefix, StringComparison.Ordinal) ||
                !registry.TryGet(entry.PersistentId, out PersistentNetworkObject target) || target == null ||
                target.transform.parent != null) continue;
            Scene owner = SceneManager.GetSceneByName(entry.SceneName);
            if (owner.IsValid() && owner.isLoaded && target.gameObject.scene != owner)
                SceneManager.MoveGameObjectToScene(target.gameObject, owner);
        }

        ApplyTransforms(snapshot.SceneObjects, context, result);
        ApplyTransforms(snapshot.RuntimeObjects, context, result);
    }

    private void ApplyTransforms(List<PersistentObjectSnapshot> snapshots, PersistentStateContext context, SnapshotApplyResult result)
    {
        if (snapshots == null || registry == null)
        {
            return;
        }

        for (int i = 0; i < snapshots.Count; i++)
        {
            PersistentObjectSnapshot objectSnapshot = snapshots[i];
            if (LegacyBuildingSystem.ShouldSkipRuntimeSnapshot(objectSnapshot))
            {
                continue;
            }

            if (objectSnapshot == null || string.IsNullOrWhiteSpace(objectSnapshot.PersistentId))
            {
                if (objectSnapshot != null)
                {
                    RecordResolveFailure(objectSnapshot, PersistentResolveStatus.MissingPersistentId, "apply transforms and active states", result);
                }

                continue;
            }

            if (!registry.TryResolveSnapshotObject(
                    objectSnapshot,
                    out PersistentNetworkObject persistentObject,
                    out PersistentResolveStatus status,
                    this,
                    "apply transforms and active states"))
            {
                if (status == PersistentResolveStatus.MissingObject)
                {
                    result.MissingTransformTargets++;
                }

                RecordResolveFailure(objectSnapshot, status, "apply transforms and active states", result);
                continue;
            }

            try
            {
                persistentObject.ApplyTransformState(objectSnapshot);
            }
            catch (Exception ex)
            {
                RecordPhaseException(
                    "apply transforms",
                    objectSnapshot.PersistentId,
                    PersistentWorldDebug.DescribePersistentObjectType(persistentObject),
                    null,
                    ex,
                    result,
                    persistentObject);
                continue;
            }

            context?.MarkTransformApplied(objectSnapshot.PersistentId);
        }
    }

    private void ApplyGameplayState(WorldSnapshot snapshot, PersistentStateContext context, SnapshotApplyResult result, bool restoreGlobalState = true)
    {
        ApplyGameplayState(snapshot.SceneObjects, context, PersistentApplyPhase.ApplyGameplayState, result);
        ApplyGameplayState(snapshot.RuntimeObjects, context, PersistentApplyPhase.ApplyGameplayState, result);
        try
        {
            if (restoreGlobalState)
            {
                worldRulesStateManager?.ApplyVariables(snapshot.WorldVariables);
                if (snapshot.Abilities != null) AbilityService.Ensure().Restore(snapshot.Abilities);
            }
        }
        catch (Exception ex)
        {
            RecordPhaseException(
                "apply state providers",
                "world:variables",
                worldRulesStateManager != null ? worldRulesStateManager.GetType().Name : nameof(WorldRulesStateManager),
                "world-variables",
                ex,
                result,
                worldRulesStateManager != null ? worldRulesStateManager : this);
        }
    }

    private void FinalizeReferences(WorldSnapshot snapshot, PersistentStateContext context, SnapshotApplyResult result)
    {
        ApplyGameplayState(snapshot.SceneObjects, context, PersistentApplyPhase.FinalizeReferences, result);
        ApplyGameplayState(snapshot.RuntimeObjects, context, PersistentApplyPhase.FinalizeReferences, result);
        try
        {
            worldRulesStateManager?.RebuildDerivedFlameVariables();
        }
        catch (Exception ex)
        {
            RecordPhaseException(
                "finalize references",
                "world:variables",
                worldRulesStateManager != null ? worldRulesStateManager.GetType().Name : nameof(WorldRulesStateManager),
                "world-variables",
                ex,
                result,
                worldRulesStateManager != null ? worldRulesStateManager : this);
        }
    }

    private void ApplyGameplayState(
        List<PersistentObjectSnapshot> snapshots,
        PersistentStateContext context,
        PersistentApplyPhase phase,
        SnapshotApplyResult result)
    {
        if (snapshots == null || registry == null)
        {
            return;
        }

        for (int i = 0; i < snapshots.Count; i++)
        {
            PersistentObjectSnapshot objectSnapshot = snapshots[i];
            if (LegacyBuildingSystem.ShouldSkipRuntimeSnapshot(objectSnapshot))
            {
                continue;
            }

            if (objectSnapshot == null || string.IsNullOrWhiteSpace(objectSnapshot.PersistentId))
            {
                if (objectSnapshot != null)
                {
                    RecordResolveFailure(objectSnapshot, PersistentResolveStatus.MissingPersistentId, $"apply gameplay state phase={phase}", result);
                }

                continue;
            }

            if (phase == PersistentApplyPhase.ApplyGameplayState &&
                context != null &&
                !context.HasTransformApplied(objectSnapshot.PersistentId))
            {
                result.RestoreOrderIssues++;
                string orderMessage =
                    $"restore order issue persistentId='{objectSnapshot.PersistentId}' phase='{phase}' dependency='transform'";
                result.AddError(orderMessage);
                PersistentWorldDebug.Error(orderMessage, this);
            }

            if (phase == PersistentApplyPhase.FinalizeReferences &&
                context != null &&
                !context.HasGameplayApplied(objectSnapshot.PersistentId))
            {
                result.RestoreOrderIssues++;
                string orderMessage =
                    $"restore order issue persistentId='{objectSnapshot.PersistentId}' phase='{phase}' dependency='gameplay_state'";
                result.AddError(orderMessage);
                PersistentWorldDebug.Error(orderMessage, this);
            }

            if (!registry.TryResolveSnapshotObject(
                    objectSnapshot,
                    out PersistentNetworkObject persistentObject,
                    out PersistentResolveStatus status,
                    this,
                    $"apply gameplay state phase={phase}"))
            {
                if (status == PersistentResolveStatus.MissingObject)
                {
                    result.MissingGameplayTargets++;
                }

                RecordResolveFailure(objectSnapshot, status, $"apply gameplay state phase={phase}", result);
                continue;
            }

            if (!persistentObject.ApplyProviderStates(objectSnapshot, phase, context))
            {
                result.FailedPayloadApplications++;
                string message =
                    $"provider state application failed persistentId='{objectSnapshot.PersistentId}' componentType='{PersistentWorldDebug.DescribePersistentObjectType(persistentObject)}' phase='{phase}'";
                result.AddError(message);
                PersistentWorldDebug.Error(message, persistentObject);
                continue;
            }

            if (phase == PersistentApplyPhase.ApplyGameplayState)
            {
                context?.MarkGameplayApplied(objectSnapshot.PersistentId);
            }
        }
    }

    private void ValidateSnapshotIdentityGraph(WorldSnapshot snapshot, SnapshotApplyResult result)
    {
        HashSet<string> seenIds = new HashSet<string>();
        ValidateSnapshotIdentityGraph(snapshot.SceneObjects, PersistentObjectKind.ScenePlaced, seenIds, result);
        ValidateSnapshotIdentityGraph(snapshot.RuntimeObjects, PersistentObjectKind.RuntimeSpawned, seenIds, result);
    }

    private void ValidateSnapshotIdentityGraph(
        List<PersistentObjectSnapshot> snapshots,
        PersistentObjectKind expectedKind,
        HashSet<string> seenIds,
        SnapshotApplyResult result)
    {
        if (snapshots == null)
        {
            return;
        }

        for (int i = 0; i < snapshots.Count; i++)
        {
            PersistentObjectSnapshot snapshot = snapshots[i];
            if (snapshot == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(snapshot.PersistentId))
            {
                result.MissingPersistentIds++;
                string missingIdMessage = $"snapshot object missing persistent id expectedKind='{expectedKind}'";
                result.AddError(missingIdMessage);
                PersistentWorldDebug.Error(missingIdMessage, this);
                continue;
            }

            if (snapshot.ObjectKind != expectedKind)
            {
                string kindMessage =
                    $"snapshot object kind mismatch id='{snapshot.PersistentId}' expected='{expectedKind}' actual='{snapshot.ObjectKind}'";
                result.ObjectTypeMismatches++;
                result.AddError(kindMessage);
                PersistentWorldDebug.Error(kindMessage, this);
            }

            if (expectedKind == PersistentObjectKind.RuntimeSpawned &&
                string.IsNullOrWhiteSpace(snapshot.RuntimePrefabId))
            {
                string prefabMessage = $"runtime snapshot missing prefab id id='{snapshot.PersistentId}'";
                result.MissingRuntimePrefabMappings++;
                result.AddError(prefabMessage);
                PersistentWorldDebug.Error(prefabMessage, this);
            }

            if (seenIds.Add(snapshot.PersistentId))
            {
                continue;
            }

            result.DuplicateSnapshotIds++;
            string duplicateMessage = $"duplicate snapshot persistent id detected id='{snapshot.PersistentId}'";
            result.AddError(duplicateMessage);
            PersistentWorldDebug.Error(duplicateMessage, this);
        }
    }

    private void ValidateRuntimeObjects(WorldSnapshot snapshot, SnapshotApplyResult result)
    {
        if (snapshot == null || snapshot.RuntimeObjects == null || registry == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.RuntimeObjects.Count; i++)
        {
            PersistentObjectSnapshot objectSnapshot = snapshot.RuntimeObjects[i];
            if (LegacyBuildingSystem.ShouldSkipRuntimeSnapshot(objectSnapshot))
            {
                continue;
            }

            if (objectSnapshot == null || string.IsNullOrWhiteSpace(objectSnapshot.PersistentId))
            {
                if (objectSnapshot != null)
                {
                    RecordResolveFailure(objectSnapshot, PersistentResolveStatus.MissingPersistentId, "validate runtime objects", result);
                }

                continue;
            }

            if (registry.TryResolveSnapshotObject(
                    objectSnapshot,
                    out _,
                    out PersistentResolveStatus status,
                    this,
                    "validate runtime objects"))
            {
                continue;
            }

            if (status == PersistentResolveStatus.MissingObject)
            {
                result.MissingRuntimeObjects++;
            }

            RecordResolveFailure(objectSnapshot, status, "validate runtime objects", result);
        }
    }

    private bool FinalizeApply(WorldSnapshot snapshot, PersistentStateContext context, SnapshotApplyResult result)
    {
        if (context != null && context.ValidationIssueCount > 0)
        {
            result.ValidationIssues += context.ValidationIssueCount;
            for (int i = 0; i < context.ValidationIssues.Count; i++)
            {
                string message = context.ValidationIssues[i];
                result.AddError(message);
                PersistentWorldDebug.Error(message, this);
            }
        }

        string firstError = result.Errors.Count > 0 ? result.Errors[0] : string.Empty;
        string summary =
            $"snapshot apply summary success={result.Succeeded} duplicateIds={result.DuplicateSnapshotIds} missingIds={result.MissingPersistentIds} missingScene={result.MissingSceneObjects} missingRuntime={result.MissingRuntimeObjects} missingRuntimePrefabs={result.MissingRuntimePrefabMappings} failedRecreations={result.FailedRuntimeRecreations} missingTransforms={result.MissingTransformTargets} missingGameplay={result.MissingGameplayTargets} typeMismatches={result.ObjectTypeMismatches} failedPayloads={result.FailedPayloadApplications} restoreOrderIssues={result.RestoreOrderIssues} validationIssues={result.ValidationIssues} errorCount={result.Errors.Count} firstError='{firstError}'";
        if (result.Succeeded)
        {
            PersistentWorldDebug.Log(summary, this);
        }
        else
        {
            PersistentWorldDebug.Error(summary, this);
            if (HasZeroFailureCounters(result) && result.Errors.Count > 0)
            {
                PersistentWorldDebug.Error(
                    $"snapshot apply failed because the error list is non-empty despite zero counters errorCount={result.Errors.Count} firstError='{firstError}'",
                    this);
            }
        }

        SnapshotApplied?.Invoke(snapshot);
        return result.Succeeded;
    }

    private bool ExecuteReconstructionPhase(
        string phaseName,
        PersistentApplyPhase phase,
        SnapshotApplyResult result,
        PersistentStateContext context,
        Action applyPhase)
    {
        int errorCountBefore = result != null ? result.Errors.Count : 0;
        PersistentWorldDebug.Log(
            $"snapshot reconstruction phase start phase='{phaseName}' errorCount={errorCountBefore}",
            this);
        context?.SetCurrentPhase(phase);

        bool completed = true;
        try
        {
            applyPhase?.Invoke();
        }
        catch (Exception ex)
        {
            completed = false;
            RecordPhaseException(phaseName, string.Empty, string.Empty, string.Empty, ex, result, this);
        }

        int totalErrors = result != null ? result.Errors.Count : 0;
        int newErrors = Mathf.Max(0, totalErrors - errorCountBefore);
        string phaseSummary =
            $"snapshot reconstruction phase complete phase='{phaseName}' completed={completed} newErrors={newErrors} totalErrors={totalErrors}";
        if (completed && newErrors == 0)
        {
            PersistentWorldDebug.Log(phaseSummary, this);
        }
        else
        {
            PersistentWorldDebug.Error(phaseSummary, this);
        }

        return completed;
    }

    private void RecordPhaseException(
        string phaseName,
        string persistentId,
        string componentType,
        string providerType,
        Exception exception,
        SnapshotApplyResult result,
        UnityEngine.Object context)
    {
        if (exception == null || result == null)
        {
            return;
        }

        result.ValidationIssues++;
        string message =
            $"snapshot reconstruction exception phase='{phaseName}' persistentId='{persistentId}' componentType='{componentType}' providerType='{providerType}' error='{exception.Message}' stackTrace='{exception}'";
        result.AddError(message);
        PersistentWorldDebug.Error(message, context != null ? context : this);
    }

    private static bool HasZeroFailureCounters(SnapshotApplyResult result)
    {
        return result != null &&
               result.DuplicateSnapshotIds == 0 &&
               result.MissingPersistentIds == 0 &&
               result.MissingSceneObjects == 0 &&
               result.MissingRuntimeObjects == 0 &&
               result.MissingRuntimePrefabMappings == 0 &&
               result.FailedRuntimeRecreations == 0 &&
               result.MissingTransformTargets == 0 &&
               result.MissingGameplayTargets == 0 &&
               result.ObjectTypeMismatches == 0 &&
               result.FailedPayloadApplications == 0 &&
               result.RestoreOrderIssues == 0 &&
               result.ValidationIssues == 0;
    }

    private void RecordResolveFailure(
        PersistentObjectSnapshot snapshot,
        PersistentResolveStatus status,
        string phase,
        SnapshotApplyResult result)
    {
        if (status == PersistentResolveStatus.Success || snapshot == null || result == null)
        {
            return;
        }

        switch (status)
        {
            case PersistentResolveStatus.MissingPersistentId:
                result.MissingPersistentIds++;
                break;
            case PersistentResolveStatus.KindMismatch:
            case PersistentResolveStatus.RuntimePrefabMismatch:
                result.ObjectTypeMismatches++;
                break;
            case PersistentResolveStatus.RuntimePrefabMissing:
                result.MissingRuntimePrefabMappings++;
                break;
        }

        string expectedResolutionMode = PersistentWorldSceneInstaller.DescribeExpectedResolutionMode(snapshot);
        string failureReason = DescribeResolveFailureReason(snapshot, status);
        string message =
            $"persistent resolve failed phase='{phase}' status='{status}' expectedResolutionMode='{expectedResolutionMode}' reason='{failureReason}' persistentId='{snapshot.PersistentId}' kind='{snapshot.ObjectKind}' runtimePrefab='{snapshot.RuntimePrefabId}'";
        result.AddError(message);
        PersistentWorldDebug.LogSnapshotObjectAudit(
            phase,
            snapshot.ObjectKind == PersistentObjectKind.ScenePlaced ? "scene" : "runtime",
            snapshot,
            expectedResolutionMode,
            this,
            null,
            $"status='{status}' reason='{failureReason}'");
    }

    private static void PrepareObjectForCapture(PersistentNetworkObject persistentObject)
    {
        if (persistentObject == null)
        {
            return;
        }

        BuildingInfoInteractable building = persistentObject.GetComponent<BuildingInfoInteractable>();
        if (building != null && building.NetworkBuildingId != 0)
        {
            PersistentWorldSceneInstaller.EnsureRuntimeBuildingInstance(building, building.BuildingItem, building.NetworkBuildingId);
        }

        NetcodeCharacterIdentity characterIdentity = persistentObject.GetComponent<NetcodeCharacterIdentity>();
        if (characterIdentity != null && !string.IsNullOrWhiteSpace(characterIdentity.CharacterId))
        {
            PersistentWorldSceneInstaller.EnsureRuntimeCharacterIdentity(persistentObject.gameObject, characterIdentity.CharacterId);
        }
    }

    private bool ValidateSnapshotForExport(
        PersistentObjectSnapshot snapshot,
        PersistentNetworkObject persistentObject,
        PersistentStateContext context)
    {
        if (snapshot == null)
        {
            return false;
        }

        if (PersistentWorldSceneInstaller.TryValidatePersistentIdentity(
                snapshot.ObjectKind,
                snapshot.PersistentId,
                snapshot.RuntimePrefabId,
                out string validationReason))
        {
            return true;
        }

        string message =
            $"snapshot export rejected invalid identity persistentId='{snapshot.PersistentId}' kind='{snapshot.ObjectKind}' runtimePrefab='{snapshot.RuntimePrefabId}' reason='{validationReason}' path='{PersistentWorldDebug.DescribeTransform(persistentObject != null ? persistentObject.transform : null)}'";
        PersistentWorldDebug.Error(message, persistentObject != null ? persistentObject : this);
        context?.ReportValidationIssue(message);
        PersistentWorldDebug.LogSnapshotObjectAudit(
            "capture snapshot rejected",
            snapshot.ObjectKind == PersistentObjectKind.ScenePlaced ? "scene" : "runtime",
            snapshot,
            "export-rejected",
            this,
            persistentObject,
            $"reason='{validationReason}'");
        return false;
    }

    private static string DescribeResolveFailureReason(PersistentObjectSnapshot snapshot, PersistentResolveStatus status)
    {
        switch (status)
        {
            case PersistentResolveStatus.MissingPersistentId:
                return "snapshot entry is missing a persistent ID";
            case PersistentResolveStatus.MissingObject:
                if (snapshot != null && snapshot.ObjectKind == PersistentObjectKind.ScenePlaced)
                {
                    return "no live scene object with the snapshot persistent ID is registered";
                }

                if (snapshot != null &&
                    !string.IsNullOrWhiteSpace(snapshot.RuntimePrefabId) &&
                    snapshot.RuntimePrefabId.StartsWith(PersistentWorldSceneInstaller.CharacterPrefabPrefix, StringComparison.Ordinal))
                {
                    return "expected already spawned NGO player object was not registered with matching runtime identity";
                }

                return "no live runtime object with matching persistent/runtime identity is registered";
            case PersistentResolveStatus.KindMismatch:
                return "a live object was found, but its persistent kind does not match the snapshot";
            case PersistentResolveStatus.RuntimePrefabMissing:
                return "runtime snapshot is missing its prefab mapping";
            case PersistentResolveStatus.RuntimePrefabMismatch:
                return "a live runtime object was found, but its runtime prefab ID does not match the snapshot";
            default:
                return $"resolve status '{status}'";
        }
    }

    private void ResolveReferences()
    {
        if (registry == null)
        {
#if UNITY_2023_1_OR_NEWER
            registry = FindAnyObjectByType<NetworkObjectRegistry>();
#else
            registry = FindAnyObjectByType<NetworkObjectRegistry>();
#endif
        }

        if (spawnManager == null)
        {
#if UNITY_2023_1_OR_NEWER
            spawnManager = FindAnyObjectByType<SpawnManager>();
#else
            spawnManager = FindAnyObjectByType<SpawnManager>();
#endif
        }

        if (worldRulesStateManager == null)
        {
#if UNITY_2023_1_OR_NEWER
            worldRulesStateManager = FindAnyObjectByType<WorldRulesStateManager>();
#else
            worldRulesStateManager = FindAnyObjectByType<WorldRulesStateManager>();
#endif
        }
    }

    private static bool IsServer()
    {
        return HasWorldAuthority();
    }

    private static double GetCaptureTime()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return NetworkManager.Singleton.ServerTime.Time;
        }

        return Time.timeAsDouble;
    }

    private static void SortSnapshot(WorldSnapshot snapshot)
    {
        snapshot.SceneObjects.Sort((left, right) => string.CompareOrdinal(left.PersistentId, right.PersistentId));
        snapshot.RuntimeObjects.Sort((left, right) => string.CompareOrdinal(left.PersistentId, right.PersistentId));
        snapshot.Players.Sort((left, right) => left.OwnerClientId.CompareTo(right.OwnerClientId));
        snapshot.WorldVariables.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
    }

    private void AuditSnapshot(WorldSnapshot snapshot, string stage, string sceneResolutionMode, string runtimeResolutionMode)
    {
        if (snapshot == null)
        {
            return;
        }

        AuditSnapshotList(snapshot.SceneObjects, "scene", stage, sceneResolutionMode);
        AuditSnapshotList(snapshot.RuntimeObjects, "runtime", stage, runtimeResolutionMode);
    }

    private void AuditSnapshotList(
        List<PersistentObjectSnapshot> snapshots,
        string listKind,
        string stage,
        string defaultResolutionMode)
    {
        if (snapshots == null)
        {
            return;
        }

        for (int i = 0; i < snapshots.Count; i++)
        {
            PersistentObjectSnapshot snapshot = snapshots[i];
            if (snapshot == null)
            {
                continue;
            }

            string resolutionMode = PersistentWorldSceneInstaller.IsManagedSingletonSnapshot(snapshot)
                ? "singleton-normalized"
                : defaultResolutionMode;
            PersistentNetworkObject resolvedObject = null;
            if (registry != null && !string.IsNullOrWhiteSpace(snapshot.PersistentId))
            {
                registry.TryGet(snapshot.PersistentId, out resolvedObject);
            }

            PersistentWorldDebug.LogSnapshotObjectAudit(
                stage,
                listKind,
                snapshot,
                resolutionMode,
                this,
                resolvedObject);
        }
    }
}
