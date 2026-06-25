using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum PersistentObjectKind : byte
{
    ScenePlaced = 0,
    RuntimeSpawned = 1
}

public enum PersistentApplyPhase : byte
{
    None = 0,
    ResolveSceneObjects = 1,
    SpawnMissingRuntimeObjects = 2,
    RemoveInvalidObjects = 3,
    ApplyTransformsAndActives = 4,
    ApplyGameplayState = 5,
    FinalizeReferences = 6,
    ReleasePlayerIntoGameplay = 7
}

public enum WorldVariableValueType : byte
{
    Int = 0,
    Float = 1,
    Bool = 2,
    String = 3
}

public enum PersistentResolveStatus : byte
{
    Success = 0,
    MissingPersistentId = 1,
    MissingObject = 2,
    KindMismatch = 3,
    RuntimePrefabMissing = 4,
    RuntimePrefabMismatch = 5
}

[System.Serializable]
public struct TransformStateSnapshot
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
    public bool ActiveSelf;
}

[System.Serializable]
public sealed class StateBlobSnapshot
{
    public string ProviderId;
    public byte[] Payload;
}

[System.Serializable]
public sealed class PersistentObjectSnapshot
{
    public string PersistentId;
    public PersistentObjectKind ObjectKind;
    public string RuntimePrefabId;
    public string SceneName;
    public bool DestroyIfMissing;
    public TransformStateSnapshot Transform;
    public List<StateBlobSnapshot> StateBlobs = new List<StateBlobSnapshot>();
}

[System.Serializable]
public sealed class PlayerSnapshot
{
    public ulong OwnerClientId;
    public string PlayerId;
    public string CharacterId;
    public string ControlledObjectId;
    public Vector3 Position;
    public Quaternion Rotation;
    public byte[] CustomState;
}

[System.Serializable]
public sealed class WorldVariableSnapshot
{
    public string Key;
    public WorldVariableValueType ValueType;
    public int IntValue;
    public float FloatValue;
    public bool BoolValue;
    public string StringValue;
}

[System.Serializable]
public sealed class WorldSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion = CurrentSchemaVersion;
    public string SceneName;
    public double CapturedAtTime;
    public List<PlayerSnapshot> Players = new List<PlayerSnapshot>();
    public List<PersistentObjectSnapshot> SceneObjects = new List<PersistentObjectSnapshot>();
    public List<PersistentObjectSnapshot> RuntimeObjects = new List<PersistentObjectSnapshot>();
    public List<WorldVariableSnapshot> WorldVariables = new List<WorldVariableSnapshot>();
}

public sealed class SnapshotApplyResult
{
    public int DuplicateSnapshotIds;
    public int MissingPersistentIds;
    public int MissingSceneObjects;
    public int MissingRuntimeObjects;
    public int MissingRuntimePrefabMappings;
    public int FailedRuntimeRecreations;
    public int MissingTransformTargets;
    public int MissingGameplayTargets;
    public int ObjectTypeMismatches;
    public int FailedPayloadApplications;
    public int RestoreOrderIssues;
    public int ValidationIssues;
    public readonly List<string> Errors = new List<string>();

    public bool Succeeded =>
        DuplicateSnapshotIds <= 0 &&
        MissingPersistentIds <= 0 &&
        MissingSceneObjects <= 0 &&
        MissingRuntimeObjects <= 0 &&
        MissingRuntimePrefabMappings <= 0 &&
        FailedRuntimeRecreations <= 0 &&
        MissingTransformTargets <= 0 &&
        MissingGameplayTargets <= 0 &&
        ObjectTypeMismatches <= 0 &&
        FailedPayloadApplications <= 0 &&
        RestoreOrderIssues <= 0 &&
        ValidationIssues <= 0 &&
        Errors.Count == 0;

    public void AddError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Errors.Add(message);
    }
}

public sealed class PersistentStateContext
{
    private readonly Dictionary<string, PersistentObjectSnapshot> snapshotLookup = new Dictionary<string, PersistentObjectSnapshot>(StringComparer.Ordinal);
    private readonly List<string> validationIssues = new List<string>();
    private readonly HashSet<string> transformsApplied = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> gameplayApplied = new HashSet<string>(StringComparer.Ordinal);

    public PersistentStateContext(
        WorldSnapshot snapshot,
        NetworkObjectRegistry registry,
        SpawnManager spawnManager,
        WorldRulesStateManager worldRules,
        bool isServer)
    {
        Snapshot = snapshot;
        Registry = registry;
        SpawnManager = spawnManager;
        WorldRules = worldRules;
        IsServer = isServer;
        LocalClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : NetworkManager.ServerClientId;

        if (snapshot == null)
        {
            return;
        }

        AddToLookup(snapshot.SceneObjects);
        AddToLookup(snapshot.RuntimeObjects);
    }

    public WorldSnapshot Snapshot { get; }

    public NetworkObjectRegistry Registry { get; }

    public SpawnManager SpawnManager { get; }

    public WorldRulesStateManager WorldRules { get; }

    public bool IsServer { get; }

    public ulong LocalClientId { get; }

    public PersistentApplyPhase CurrentPhase { get; private set; }

    public int ValidationIssueCount => validationIssues.Count;

    public IReadOnlyList<string> ValidationIssues => validationIssues;

    public bool TryGetSnapshot(string persistentId, out PersistentObjectSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            snapshot = null;
            return false;
        }

        return snapshotLookup.TryGetValue(persistentId, out snapshot);
    }

    public void ReportValidationIssue(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        validationIssues.Add(message);
    }

    public void SetCurrentPhase(PersistentApplyPhase phase)
    {
        CurrentPhase = phase;
    }

    public void MarkTransformApplied(string persistentId)
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            return;
        }

        transformsApplied.Add(persistentId);
    }

    public bool HasTransformApplied(string persistentId)
    {
        return !string.IsNullOrWhiteSpace(persistentId) && transformsApplied.Contains(persistentId);
    }

    public void MarkGameplayApplied(string persistentId)
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            return;
        }

        gameplayApplied.Add(persistentId);
    }

    public bool HasGameplayApplied(string persistentId)
    {
        return !string.IsNullOrWhiteSpace(persistentId) && gameplayApplied.Contains(persistentId);
    }

    private void AddToLookup(List<PersistentObjectSnapshot> snapshots)
    {
        if (snapshots == null)
        {
            return;
        }

        for (int i = 0; i < snapshots.Count; i++)
        {
            PersistentObjectSnapshot snapshot = snapshots[i];
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.PersistentId))
            {
                continue;
            }

            snapshotLookup[snapshot.PersistentId] = snapshot;
        }
    }
}
