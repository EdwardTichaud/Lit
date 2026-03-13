using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class WorldSaveAdapter : MonoBehaviour
{
    [SerializeField] private WorldStateManager worldStateManager;
    [SerializeField] private NetworkObjectRegistry registry;
    [SerializeField] private string fileName = "WorldSnapshot.bin";
    [SerializeField] private bool useActiveSaveSession = true;
    [SerializeField] private bool validateRestoredRuntimeIdentity = true;

    private readonly SnapshotSerializer snapshotSerializer = new SnapshotSerializer();
    private string lastAppliedSnapshotPath = string.Empty;

    public event Action<WorldSnapshot> HostWorldRestoreCompleted;
    public event Action<string> HostWorldRestoreFailed;

    public bool IsHostWorldRestoreInProgress { get; private set; }

    public bool HasRestoredWorldSnapshotThisSession { get; private set; }

    public bool LastRestoreSucceeded { get; private set; }

    public bool LastRestoreIdentityValidated { get; private set; }

    public int LastRestoreIdentityIssues { get; private set; }

    public int LastRestoreSequence { get; private set; }

    public string LastRestoreReason { get; private set; } = string.Empty;

    public string LastRestoreSnapshotPath { get; private set; } = string.Empty;

    public WorldSnapshot LastLoadedSnapshot { get; private set; }

    private void Awake()
    {
        ResolveReferences();
    }

    public bool HasSavedWorldSnapshot()
    {
        string path = ResolveSavePath();
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    public void SaveWorldSnapshot()
    {
        ResolveReferences();
        if (worldStateManager == null)
        {
            return;
        }

        WorldSnapshot snapshot = worldStateManager.CaptureSnapshot("save world snapshot");
        byte[] bytes = snapshotSerializer.Serialize(snapshot);
        string path = ResolveSavePath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, bytes);
        PersistentWorldDebug.Log($"world snapshot saved path='{path}' bytes={bytes.Length}", this);
    }

    public bool TryLoadWorldSnapshot(out WorldSnapshot snapshot)
    {
        snapshot = null;
        string path = ResolveSavePath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            snapshot = snapshotSerializer.Deserialize(bytes);
            if (snapshot != null)
            {
                PersistentWorldDebug.Log(
                    $"world snapshot loaded path='{path}' scene='{snapshot.SceneName}' bytes={bytes.Length}",
                    this);
            }

            return snapshot != null;
        }
        catch (IOException ex)
        {
            PersistentWorldDebug.Error($"world snapshot load failed path='{path}' error='{ex.Message}'", this);
            return false;
        }
    }

    public bool EnsureHostWorldRestoredFromSave(string restoreReason = null)
    {
        ResolveReferences();
        string path = ResolveSavePath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        if (IsHostWorldRestoreInProgress)
        {
            PersistentWorldDebug.Warn(
                $"host world restore already in progress reason='{LastRestoreReason}' path='{LastRestoreSnapshotPath}'",
                this);
            return false;
        }

        if (HasRestoredWorldSnapshotThisSession &&
            LastRestoreSucceeded &&
            string.Equals(lastAppliedSnapshotPath, path, StringComparison.Ordinal) &&
            string.Equals(
                LastLoadedSnapshot != null ? LastLoadedSnapshot.SceneName ?? string.Empty : string.Empty,
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                StringComparison.Ordinal))
        {
            PersistentWorldDebug.Log(
                $"host world restore already applied reason='{LastRestoreReason}' path='{path}' restoreSequence={LastRestoreSequence}",
                this);
            return true;
        }

        if (!TryLoadWorldSnapshot(out WorldSnapshot snapshot) || snapshot == null)
        {
            return false;
        }

        return ApplyWorldSnapshot(snapshot, true, restoreReason);
    }

    public bool ApplyWorldSnapshot(WorldSnapshot snapshot, bool serverSideLoad = true, string restoreReason = null)
    {
        ResolveReferences();

        string resolvedReason = string.IsNullOrWhiteSpace(restoreReason)
            ? "world_snapshot_restore"
            : restoreReason;
        string path = ResolveSavePath() ?? string.Empty;

        LastRestoreReason = resolvedReason;
        LastRestoreSnapshotPath = path;
        LastLoadedSnapshot = snapshot;
        LastRestoreSucceeded = false;
        LastRestoreIdentityValidated = false;
        LastRestoreIdentityIssues = 0;
        IsHostWorldRestoreInProgress = true;

        if (worldStateManager == null || snapshot == null)
        {
            string message =
                $"host world restore aborted reason='{resolvedReason}' worldStateManagerMissing={worldStateManager == null} snapshotNull={snapshot == null}";
            PersistentWorldDebug.Error(message, this);
            HostWorldRestoreFailed?.Invoke(message);
            IsHostWorldRestoreInProgress = false;
            return false;
        }

        try
        {
            PersistentWorldDebug.Log(
                $"host world restore started reason='{resolvedReason}' scene='{snapshot.SceneName}' runtimeObjects={snapshot.RuntimeObjects?.Count ?? 0} sceneObjects={snapshot.SceneObjects?.Count ?? 0} path='{path}' serverSideLoad={serverSideLoad}",
                this);

            NetcodeSceneObjectInstaller.PrepareActiveScene();
            bool applied = worldStateManager.ApplySnapshot(snapshot, serverSideLoad);
            if (!applied)
            {
                string message =
                    $"host world restore apply failed reason='{resolvedReason}' scene='{snapshot.SceneName}' path='{path}'";
                PersistentWorldDebug.Error(message, this);
                HostWorldRestoreFailed?.Invoke(message);
                return false;
            }

            int identityIssues = 0;
            bool identityValidated = !validateRestoredRuntimeIdentity ||
                                     ValidateRestoredWorldIdentity(snapshot, out identityIssues);
            LastRestoreSucceeded = true;
            LastRestoreIdentityValidated = identityValidated;
            LastRestoreIdentityIssues = identityIssues;
            HasRestoredWorldSnapshotThisSession = true;
            lastAppliedSnapshotPath = path;
            LastRestoreSequence++;

            string summary =
                $"host world restore completed reason='{resolvedReason}' scene='{snapshot.SceneName}' runtimeObjects={snapshot.RuntimeObjects?.Count ?? 0} sceneObjects={snapshot.SceneObjects?.Count ?? 0} path='{path}' restoreSequence={LastRestoreSequence} identityValidated={identityValidated} identityIssues={identityIssues}";
            if (identityValidated)
            {
                PersistentWorldDebug.Log(summary, this);
            }
            else
            {
                PersistentWorldDebug.Error(summary, this);
            }

            HostWorldRestoreCompleted?.Invoke(snapshot);
            return true;
        }
        finally
        {
            IsHostWorldRestoreInProgress = false;
        }
    }

    public bool ValidatePostLoadLateJoinSnapshot(WorldSnapshot outgoingSnapshot, ulong clientId)
    {
        if (!HasRestoredWorldSnapshotThisSession || LastLoadedSnapshot == null)
        {
            return true;
        }

        if (outgoingSnapshot != null)
        {
            int normalizedCount = PersistentWorldSceneInstaller.NormalizeManagedSingletonSnapshots(
                outgoingSnapshot,
                this,
                $"validate post-load late-join snapshot clientId={clientId}");
            if (normalizedCount > 0)
            {
                PersistentWorldDebug.Log(
                    $"post-load late-join snapshot normalizedSingletons={normalizedCount} clientId={clientId}",
                    this);
            }
        }

        int issues = ValidateRuntimeIdentityContinuity(
            LastLoadedSnapshot.RuntimeObjects,
            outgoingSnapshot != null ? outgoingSnapshot.RuntimeObjects : null,
            $"post_load_late_join_client_{clientId}");
        bool success = issues == 0;
        string message =
            $"post-load late-join snapshot validation clientId={clientId} success={success} issues={issues} restoreSequence={LastRestoreSequence} restoredRuntimeObjects={LastLoadedSnapshot.RuntimeObjects?.Count ?? 0} outgoingRuntimeObjects={outgoingSnapshot?.RuntimeObjects?.Count ?? 0}";
        if (success)
        {
            PersistentWorldDebug.Log(message, this);
        }
        else
        {
            PersistentWorldDebug.Error(message, this);
        }

        return success;
    }

    public bool LoadAndApplyWorldSnapshot()
    {
        if (!TryLoadWorldSnapshot(out WorldSnapshot snapshot) || snapshot == null)
        {
            return false;
        }

        return ApplyWorldSnapshot(snapshot, true, "world_save_adapter_load");
    }

    private bool ValidateRestoredWorldIdentity(WorldSnapshot snapshot, out int issueCount)
    {
        ResolveReferences();
        issueCount = 0;

        if (registry == null)
        {
            issueCount = 1;
            PersistentWorldDebug.Error("host world restore identity validation failed because NetworkObjectRegistry is missing", this);
            return false;
        }

        registry.RefreshSceneObjects();
        issueCount += ValidateResolvedSnapshots(snapshot != null ? snapshot.SceneObjects : null, "scene", requireSceneObject: true);
        issueCount += ValidateResolvedSnapshots(snapshot != null ? snapshot.RuntimeObjects : null, "runtime", requireSceneObject: false);

        if (worldStateManager != null)
        {
            WorldSnapshot currentSnapshot = worldStateManager.CaptureSnapshot("host restore validation capture");
            issueCount += ValidateRuntimeIdentityContinuity(
                snapshot != null ? snapshot.RuntimeObjects : null,
                currentSnapshot != null ? currentSnapshot.RuntimeObjects : null,
                "host_restore_capture");
        }

        bool success = issueCount == 0;
        string message =
            $"host world restore identity validation success={success} issues={issueCount} sceneObjects={snapshot?.SceneObjects?.Count ?? 0} runtimeObjects={snapshot?.RuntimeObjects?.Count ?? 0}";
        if (success)
        {
            PersistentWorldDebug.Log(message, this);
        }
        else
        {
            PersistentWorldDebug.Error(message, this);
        }

        return success;
    }

    private int ValidateResolvedSnapshots(
        IReadOnlyList<PersistentObjectSnapshot> snapshots,
        string category,
        bool requireSceneObject)
    {
        if (registry == null || snapshots == null)
        {
            return 0;
        }

        int issues = 0;
        for (int i = 0; i < snapshots.Count; i++)
        {
            PersistentObjectSnapshot snapshot = snapshots[i];
            if (snapshot == null)
            {
                continue;
            }

            if (registry.TryResolveSnapshotObject(
                    snapshot,
                    out _,
                    out PersistentResolveStatus status,
                    this,
                    $"host world restore validate {category}",
                    requireSceneObject))
            {
                continue;
            }

            issues++;
            PersistentWorldDebug.Error(
                $"host world restore validation failed category='{category}' status='{status}' persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}'",
                this);
        }

        return issues;
    }

    private int ValidateRuntimeIdentityContinuity(
        IReadOnlyList<PersistentObjectSnapshot> baselineRuntime,
        IReadOnlyList<PersistentObjectSnapshot> currentRuntime,
        string validationPhase)
    {
        if (baselineRuntime == null)
        {
            return 0;
        }

        Dictionary<string, PersistentObjectSnapshot> currentLookup = new Dictionary<string, PersistentObjectSnapshot>(StringComparer.Ordinal);
        if (currentRuntime != null)
        {
            for (int i = 0; i < currentRuntime.Count; i++)
            {
                PersistentObjectSnapshot snapshot = currentRuntime[i];
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.PersistentId))
                {
                    continue;
                }

                currentLookup[snapshot.PersistentId] = snapshot;
            }
        }

        int issues = 0;
        for (int i = 0; i < baselineRuntime.Count; i++)
        {
            PersistentObjectSnapshot baseline = baselineRuntime[i];
            if (baseline == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(baseline.PersistentId))
            {
                issues++;
                PersistentWorldDebug.Error(
                    $"runtime identity continuity failed phase='{validationPhase}' reason='missing_persistent_id'",
                    this);
                continue;
            }

            if (!currentLookup.TryGetValue(baseline.PersistentId, out PersistentObjectSnapshot current))
            {
                issues++;
                PersistentWorldDebug.Error(
                    $"runtime identity continuity failed phase='{validationPhase}' persistentId='{baseline.PersistentId}' reason='missing_after_restore' expectedPrefab='{baseline.RuntimePrefabId}'",
                    this);
                continue;
            }

            if (current.ObjectKind != PersistentObjectKind.RuntimeSpawned ||
                !string.Equals(current.RuntimePrefabId ?? string.Empty, baseline.RuntimePrefabId ?? string.Empty, StringComparison.Ordinal))
            {
                issues++;
                PersistentWorldDebug.Error(
                    $"runtime identity continuity failed phase='{validationPhase}' persistentId='{baseline.PersistentId}' expectedPrefab='{baseline.RuntimePrefabId}' actualPrefab='{current.RuntimePrefabId}' actualKind='{current.ObjectKind}'",
                    this);
            }
        }

        return issues;
    }

    private void ResolveReferences()
    {
        if (worldStateManager == null)
        {
#if UNITY_2023_1_OR_NEWER
            worldStateManager = FindFirstObjectByType<WorldStateManager>();
#else
            worldStateManager = FindObjectOfType<WorldStateManager>();
#endif
        }

        if (registry == null)
        {
#if UNITY_2023_1_OR_NEWER
            registry = FindFirstObjectByType<NetworkObjectRegistry>();
#else
            registry = FindObjectOfType<NetworkObjectRegistry>();
#endif
        }
    }

    private string ResolveSavePath()
    {
        if (useActiveSaveSession && SaveSessionManager.Instance != null && SaveSessionManager.Instance.HasActiveSave)
        {
            return SaveSessionManager.Instance.GetActiveSaveFilePath(fileName);
        }

        return Path.Combine(Application.persistentDataPath, fileName);
    }
}
