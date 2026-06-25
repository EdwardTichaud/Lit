using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class WorldStateManager : MonoBehaviour
{
    [SerializeField] private NetworkObjectRegistry registry;
    [SerializeField] private SpawnManager spawnManager;
    [SerializeField] private WorldRulesStateManager worldRulesStateManager;

    public event Action<WorldSnapshot> SnapshotApplied;

    public SnapshotApplyResult LastApplyResult { get; private set; }

    public bool LastApplySucceeded => LastApplyResult != null && LastApplyResult.Succeeded;

    private readonly List<PersistentObjectSnapshot> preservedLegacyBuildingSnapshots =
        new List<PersistentObjectSnapshot>();

    private void Awake()
    {
        ResolveReferences();
    }

    public WorldSnapshot CaptureSnapshot(string captureReason = null)
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
        CapturePersistentObjects(snapshot);
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
        preservedLegacyBuildingSnapshots.Clear();
        if (!LegacyBuildingSystem.Enabled && LegacyBuildingSystem.PreserveLegacyWorldSnapshots)
        {
            preservedLegacyBuildingSnapshots.AddRange(
                LegacyBuildingPersistenceMigration.CaptureLegacyWorldSnapshots(snapshot));
        }

        int normalizedSingletonCount = PersistentWorldSceneInstaller.NormalizeManagedSingletonSnapshots(snapshot, this, "apply snapshot");
        int ensuredSingletonCount = PersistentWorldSceneInstaller.EnsureManagedSingletonsForSnapshot(snapshot, this, "apply snapshot");
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
        if (!string.IsNullOrWhiteSpace(snapshot.SceneName) &&
            !string.Equals(snapshot.SceneName, SceneManager.GetActiveScene().name, StringComparison.Ordinal))
        {
            string sceneMismatch =
                $"snapshot scene '{snapshot.SceneName}' does not match active scene '{SceneManager.GetActiveScene().name}'.";
            result.AddError(sceneMismatch);
            PersistentWorldDebug.Error(sceneMismatch, this);
            return FinalizeApply(snapshot, context, result);
        }

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
                        ? spawnManager.RemoveRuntimeObjectsNotInSnapshot(snapshot.RuntimeObjects, registry, serverSideLoad)
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
                () => ApplyGameplayState(snapshot, context, result)))
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

    private void CapturePersistentObjects(WorldSnapshot snapshot)
    {
        if (snapshot == null || registry == null)
        {
            return;
        }

        List<PersistentNetworkObject> objects = registry.GetAllObjects();
        PersistentStateContext context = new PersistentStateContext(snapshot, registry, spawnManager, worldRulesStateManager, IsServer());

        for (int i = 0; i < objects.Count; i++)
        {
            PersistentNetworkObject persistentObject = objects[i];
            if (persistentObject == null || string.IsNullOrWhiteSpace(persistentObject.PersistentId))
            {
                continue;
            }

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

    private void ApplyGameplayState(WorldSnapshot snapshot, PersistentStateContext context, SnapshotApplyResult result)
    {
        ApplyGameplayState(snapshot.SceneObjects, context, PersistentApplyPhase.ApplyGameplayState, result);
        ApplyGameplayState(snapshot.RuntimeObjects, context, PersistentApplyPhase.ApplyGameplayState, result);
        try
        {
            worldRulesStateManager?.ApplyVariables(snapshot.WorldVariables);
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
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
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
