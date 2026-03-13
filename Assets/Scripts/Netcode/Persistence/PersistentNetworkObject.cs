using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class PersistentNetworkObject : NetworkBehaviour
{
    [SerializeField] private PersistentObjectKind objectKind = PersistentObjectKind.ScenePlaced;
    [SerializeField] private string persistentId;
    [SerializeField] private string runtimePrefabId;
    [SerializeField] private bool destroyIfMissingFromSnapshot = true;

    private readonly List<IPersistentStateProvider> providers = new List<IPersistentStateProvider>();
    private readonly NetworkVariable<FixedString512Bytes> syncedPersistentId =
        new NetworkVariable<FixedString512Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<FixedString512Bytes> syncedRuntimePrefabId =
        new NetworkVariable<FixedString512Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<byte> syncedObjectKind =
        new NetworkVariable<byte>((byte)PersistentObjectKind.ScenePlaced, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> syncedDestroyIfMissing =
        new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private bool suppressSyncedIdentityCallbacks;

    public PersistentObjectKind ObjectKind => objectKind;

    public string PersistentId => persistentId;

    public bool HasPersistentId => !string.IsNullOrWhiteSpace(persistentId);

    public string RuntimePrefabId => runtimePrefabId;

    public bool DestroyIfMissingFromSnapshot => destroyIfMissingFromSnapshot;

    public bool IsRuntimeObject => objectKind == PersistentObjectKind.RuntimeSpawned;

    private void Awake()
    {
        CacheProviders();
    }

    private void OnEnable()
    {
        CacheProviders();
        TryRegisterWithRegistry();
    }

    private void Start()
    {
        TryRegisterWithRegistry();
    }

    private void OnDisable()
    {
        if (NetworkObjectRegistry.Instance != null)
        {
            NetworkObjectRegistry.Instance.Unregister(this);
        }
    }

    public override void OnNetworkSpawn()
    {
        syncedPersistentId.OnValueChanged += OnSyncedPersistentIdChanged;
        syncedRuntimePrefabId.OnValueChanged += OnSyncedRuntimePrefabIdChanged;
        syncedObjectKind.OnValueChanged += OnSyncedObjectKindChanged;
        syncedDestroyIfMissing.OnValueChanged += OnSyncedDestroyIfMissingChanged;

        if (IsServer)
        {
            PushIdentityToNetworkVariables();
        }
        else
        {
            ApplySyncedIdentity();
        }

        TryRegisterWithRegistry();
    }

    public override void OnNetworkDespawn()
    {
        syncedPersistentId.OnValueChanged -= OnSyncedPersistentIdChanged;
        syncedRuntimePrefabId.OnValueChanged -= OnSyncedRuntimePrefabIdChanged;
        syncedObjectKind.OnValueChanged -= OnSyncedObjectKindChanged;
        syncedDestroyIfMissing.OnValueChanged -= OnSyncedDestroyIfMissingChanged;
    }

    public void AssignSceneIdentity(string assignedPersistentId)
    {
        UpdateIdentity(() =>
        {
            objectKind = PersistentObjectKind.ScenePlaced;
            persistentId = assignedPersistentId ?? string.Empty;
            runtimePrefabId = string.Empty;
        });
    }

    public void AssignRuntimeIdentity(string assignedPersistentId, string prefabId)
    {
        UpdateIdentity(() =>
        {
            objectKind = PersistentObjectKind.RuntimeSpawned;
            persistentId = assignedPersistentId ?? string.Empty;
            runtimePrefabId = prefabId ?? string.Empty;
        });
    }

    public void SetDestroyIfMissingFromSnapshot(bool destroyIfMissing)
    {
        destroyIfMissingFromSnapshot = destroyIfMissing;
        PushIdentityToNetworkVariables();
    }

    public void SetRuntimePrefabId(string prefabId)
    {
        runtimePrefabId = prefabId ?? string.Empty;
        PushIdentityToNetworkVariables();
    }

    public PersistentObjectSnapshot CaptureSnapshot(PersistentStateContext context)
    {
        CacheProviders();

        PersistentObjectSnapshot snapshot = new PersistentObjectSnapshot
        {
            PersistentId = persistentId,
            ObjectKind = objectKind,
            RuntimePrefabId = runtimePrefabId,
            SceneName = gameObject.scene.IsValid() ? gameObject.scene.name : string.Empty,
            DestroyIfMissing = destroyIfMissingFromSnapshot,
            Transform = new TransformStateSnapshot
            {
                Position = transform.position,
                Rotation = transform.rotation,
                Scale = transform.localScale,
                ActiveSelf = gameObject.activeSelf
            }
        };

        for (int i = 0; i < providers.Count; i++)
        {
            IPersistentStateProvider provider = providers[i];
            if (provider == null)
            {
                continue;
            }

            byte[] payload = Array.Empty<byte>();
            try
            {
                payload = provider.CaptureState(context);
                if (payload == null)
                {
                    payload = Array.Empty<byte>();
                }
            }
            catch (Exception ex)
            {
                string message =
                    $"capture provider failed persistentId='{persistentId}' componentType='{PersistentWorldDebug.DescribePersistentObjectType(this)}' provider='{provider.ProviderId}' providerType='{provider.GetType().Name}' error='{ex.Message}' stackTrace='{ex}'";
                PersistentWorldDebug.Error(message, this);
                context?.ReportValidationIssue(message);
            }

            snapshot.StateBlobs.Add(new StateBlobSnapshot
            {
                ProviderId = provider.ProviderId,
                Payload = payload
            });
        }

        return snapshot;
    }

    public void ApplyTransformState(PersistentObjectSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        transform.position = snapshot.Transform.Position;
        transform.rotation = snapshot.Transform.Rotation;
        transform.localScale = snapshot.Transform.Scale;
        gameObject.SetActive(snapshot.Transform.ActiveSelf);
    }

    public bool ApplyProviderStates(PersistentObjectSnapshot snapshot, PersistentApplyPhase phase, PersistentStateContext context)
    {
        if (snapshot == null)
        {
            return false;
        }

        CacheProviders();
        if (snapshot.StateBlobs == null || snapshot.StateBlobs.Count == 0)
        {
            return true;
        }

        bool success = true;
        for (int i = 0; i < snapshot.StateBlobs.Count; i++)
        {
            StateBlobSnapshot blob = snapshot.StateBlobs[i];
            if (blob == null || string.IsNullOrWhiteSpace(blob.ProviderId))
            {
                continue;
            }

            bool providerFound = false;
            for (int providerIndex = 0; providerIndex < providers.Count; providerIndex++)
            {
                IPersistentStateProvider provider = providers[providerIndex];
                if (provider == null || !string.Equals(provider.ProviderId, blob.ProviderId, StringComparison.Ordinal))
                {
                    continue;
                }

                providerFound = true;
                try
                {
                    provider.ApplyState(blob.Payload ?? Array.Empty<byte>(), phase, context);
                }
                catch (Exception ex)
                {
                    string message =
                        $"provider apply failed phase='{phase}' persistentId='{persistentId}' componentType='{PersistentWorldDebug.DescribePersistentObjectType(this)}' provider='{blob.ProviderId}' providerType='{provider.GetType().Name}' error='{ex.Message}' stackTrace='{ex}'";
                    PersistentWorldDebug.Error(message, this);
                    context?.ReportValidationIssue(message);
                    success = false;
                }

                break;
            }

            if (providerFound)
            {
                continue;
            }

            string missingProviderMessage =
                $"missing persistent state provider phase='{phase}' persistentId='{persistentId}' componentType='{PersistentWorldDebug.DescribePersistentObjectType(this)}' provider='{blob.ProviderId}' providerType='<missing>'";
            PersistentWorldDebug.Error(missingProviderMessage, this);
            context?.ReportValidationIssue(missingProviderMessage);
            success = false;
        }

        return success;
    }

    private void CacheProviders()
    {
        providers.Clear();
        HashSet<string> providerIds = new HashSet<string>();

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPersistentStateProvider provider)
            {
                string providerId = provider.ProviderId ?? string.Empty;
                if (!providerIds.Add(providerId))
                {
                    PersistentWorldDebug.Error(
                        $"duplicate provider id '{providerId}' on persistent object '{persistentId}'",
                        behaviours[i]);
                }

                providers.Add(provider);
            }
        }
    }

    private void TryRegisterWithRegistry()
    {
        if (NetworkObjectRegistry.Instance == null || string.IsNullOrWhiteSpace(persistentId))
        {
            return;
        }

        NetworkObjectRegistry.Instance.Register(this);
    }

    private void UpdateIdentity(Action applyChange, bool pushToNetwork = true)
    {
        if (applyChange == null)
        {
            return;
        }

        if (NetworkObjectRegistry.Instance != null && !string.IsNullOrWhiteSpace(persistentId))
        {
            NetworkObjectRegistry.Instance.Unregister(this);
        }

        applyChange();
        if (objectKind == PersistentObjectKind.RuntimeSpawned && !string.IsNullOrWhiteSpace(persistentId))
        {
            SpawnManager.Instance?.RegisterIssuedPersistentId(persistentId, this, "persistent_identity_update");
        }

        if (pushToNetwork)
        {
            PushIdentityToNetworkVariables();
        }

        TryRegisterWithRegistry();
    }

    private void PushIdentityToNetworkVariables()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        suppressSyncedIdentityCallbacks = true;
        try
        {
            syncedObjectKind.Value = (byte)objectKind;
            syncedPersistentId.Value = new FixedString512Bytes(persistentId ?? string.Empty);
            syncedRuntimePrefabId.Value = new FixedString512Bytes(runtimePrefabId ?? string.Empty);
            syncedDestroyIfMissing.Value = destroyIfMissingFromSnapshot;
        }
        finally
        {
            suppressSyncedIdentityCallbacks = false;
        }
    }

    private void ApplySyncedIdentity()
    {
        string syncedId = syncedPersistentId.Value.ToString();
        if (string.IsNullOrWhiteSpace(syncedId))
        {
            return;
        }

        string syncedPrefab = syncedRuntimePrefabId.Value.ToString();
        PersistentObjectKind syncedKind = (PersistentObjectKind)syncedObjectKind.Value;
        bool syncedDestroy = syncedDestroyIfMissing.Value;
        if (!PersistentWorldSceneInstaller.TryValidatePersistentIdentity(syncedKind, syncedId, syncedPrefab, out string validationReason))
        {
            PersistentWorldDebug.Warn(
                $"persistent identity sync ignored reason='{validationReason}' syncedId='{syncedId}' syncedKind='{syncedKind}' syncedPrefab='{syncedPrefab}' currentId='{persistentId}' currentKind='{objectKind}' currentPrefab='{runtimePrefabId}' path='{PersistentWorldDebug.DescribeTransform(transform)}'",
                this);
            return;
        }

        bool matches =
            string.Equals(persistentId ?? string.Empty, syncedId, StringComparison.Ordinal) &&
            string.Equals(runtimePrefabId ?? string.Empty, syncedPrefab ?? string.Empty, StringComparison.Ordinal) &&
            objectKind == syncedKind &&
            destroyIfMissingFromSnapshot == syncedDestroy;
        if (matches)
        {
            return;
        }

        UpdateIdentity(() =>
        {
            objectKind = syncedKind;
            persistentId = syncedId;
            runtimePrefabId = syncedPrefab ?? string.Empty;
            destroyIfMissingFromSnapshot = syncedDestroy;
        }, pushToNetwork: false);
    }

    private void OnSyncedPersistentIdChanged(FixedString512Bytes previous, FixedString512Bytes current)
    {
        if (IsServer || suppressSyncedIdentityCallbacks)
        {
            return;
        }

        ApplySyncedIdentity();
    }

    private void OnSyncedRuntimePrefabIdChanged(FixedString512Bytes previous, FixedString512Bytes current)
    {
        if (IsServer || suppressSyncedIdentityCallbacks)
        {
            return;
        }

        ApplySyncedIdentity();
    }

    private void OnSyncedObjectKindChanged(byte previous, byte current)
    {
        if (IsServer || suppressSyncedIdentityCallbacks)
        {
            return;
        }

        ApplySyncedIdentity();
    }

    private void OnSyncedDestroyIfMissingChanged(bool previous, bool current)
    {
        if (IsServer || suppressSyncedIdentityCallbacks)
        {
            return;
        }

        ApplySyncedIdentity();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        if (objectKind != PersistentObjectKind.ScenePlaced)
        {
            return;
        }

        string generatedId = PersistentIdUtility.GenerateSceneObjectId(gameObject);
        if (string.IsNullOrWhiteSpace(generatedId) || string.Equals(persistentId, generatedId, StringComparison.Ordinal))
        {
            return;
        }

        persistentId = generatedId;
        EditorUtility.SetDirty(this);
    }
#endif
}

internal static class PersistentIdUtility
{
#if UNITY_EDITOR
    public static string GenerateSceneObjectId(GameObject target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        if (!target.scene.IsValid())
        {
            return string.Empty;
        }

        if (EditorUtility.IsPersistent(target))
        {
            return string.Empty;
        }

        GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(target);
        return globalObjectId.ToString();
    }
#else
    public static string GenerateSceneObjectId(GameObject target)
    {
        return string.Empty;
    }
#endif
}
