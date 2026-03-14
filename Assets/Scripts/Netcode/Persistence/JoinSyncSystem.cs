using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class JoinSyncSystem : MonoBehaviour
{
    private const string RequestSnapshotMessageName = "lit.joinsync.request_snapshot";
    private const string SnapshotStartMessageName = "lit.joinsync.snapshot_start";
    private const string SnapshotChunkMessageName = "lit.joinsync.snapshot_chunk";
    private const string SnapshotFinishMessageName = "lit.joinsync.snapshot_finish";
    private const string ClientReadyMessageName = "lit.joinsync.client_ready";

    [SerializeField] private WorldStateManager worldStateManager;
    [SerializeField] private WorldSaveAdapter worldSaveAdapter;
    [SerializeField] private PersistentWorldSyncOverlay syncOverlay;
    [SerializeField] private int maxChunkPayloadBytes = 48 * 1024;
    [SerializeField] private bool blockLocalGameplayUntilSynchronized = true;
    [SerializeField] private float snapshotRequestDelaySeconds = 0.25f;
    [SerializeField] private float forceSnapshotRequestAfterSeconds = 3f;
    [SerializeField] private float snapshotTransferTimeoutSeconds = 15f;

    private readonly SnapshotSerializer snapshotSerializer = new SnapshotSerializer();
    private readonly Dictionary<ulong, bool> readyClients = new Dictionary<ulong, bool>();
    private readonly Dictionary<ulong, ServerPendingSnapshotTransfer> pendingServerTransfers = new Dictionary<ulong, ServerPendingSnapshotTransfer>();

    private NetworkManager hookedManager;
    private WorldSaveAdapter hookedWorldSaveAdapter;
    private PendingSnapshotTransfer pendingTransfer;
    private bool handlersRegistered;
    private bool localSnapshotRequestSent;
    private bool localSyncFailed;
    private bool syncInputLockApplied;
    private float earliestSnapshotRequestTime;
    private float forceSnapshotRequestTime;
    private float lastSnapshotRequestSentTime;
    private ulong nextTransferId = 1;
    private string localSyncStatusMessage = string.Empty;

    public static JoinSyncSystem Instance { get; private set; }

    public static bool IsGameplayBlocked => Instance != null &&
                                            Instance.blockLocalGameplayUntilSynchronized &&
                                            !Instance.IsLocalWorldReady;

    public bool IsLocalWorldReady { get; private set; } = true;

    public event Action LocalWorldSyncStarted;
    public event Action<PersistentNetworkObject> LocalWorldSyncCompleted;
    public event Action<string> LocalWorldSyncFailed;
    public event Action<ulong> ClientMarkedReady;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResolveReferences();
        TryHookWorldSaveAdapter();
        TryHookNetworkManager();
        SyncLocalGameplayBlock();
        SyncVisualGate();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnregisterMessageHandlers();
        UnhookNetworkManager();
        UnhookWorldSaveAdapter();
        ReleaseLocalGameplayBlock();
        syncOverlay?.SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        TryHookNetworkManager();
        ResolveReferences();
        TryHookWorldSaveAdapter();
        SyncLocalGameplayBlock();
        SyncVisualGate();
        HandleSnapshotTimeouts();
        TryCompletePendingTransfer();

        if (ShouldRequestSnapshot())
        {
            SendSnapshotRequest();
        }
    }

    public bool IsClientReady(ulong clientId)
    {
        if (hookedManager != null && hookedManager.IsServer && clientId == hookedManager.LocalClientId)
        {
            return true;
        }

        return readyClients.TryGetValue(clientId, out bool ready) && ready;
    }

    public void BroadcastSnapshotToRemoteClients()
    {
        if (hookedManager == null || !hookedManager.IsServer)
        {
            return;
        }

        PersistentWorldDebug.Log(
            $"broadcast snapshot to pending remote clients hostWorldMode='{DescribeHostWorldMode()}' pendingClients={CountPendingRemoteClients()} connectedClients={hookedManager.ConnectedClientsIds.Count}",
            this);

        foreach (ulong clientId in hookedManager.ConnectedClientsIds)
        {
            if (clientId == hookedManager.LocalClientId)
            {
                continue;
            }

            if (IsClientReady(clientId))
            {
                continue;
            }

            SendSnapshotToClient(clientId);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (hookedManager == null || !hookedManager.IsClient || hookedManager.IsServer)
        {
            return;
        }

        ScheduleSnapshotRequest($"scene loaded '{scene.name}'");
    }

    private void OnClientConnected(ulong clientId)
    {
        if (hookedManager == null)
        {
            return;
        }

        if (hookedManager.IsServer)
        {
            readyClients[clientId] = clientId == hookedManager.LocalClientId;
            PersistentWorldDebug.Log(
                $"client connected clientId={clientId} hostSideReady={readyClients[clientId]} hostWorldMode='{DescribeHostWorldMode()}' pendingClients={CountPendingRemoteClients()}",
                this);

            if (clientId != hookedManager.LocalClientId &&
                worldSaveAdapter != null &&
                worldSaveAdapter.HasRestoredWorldSnapshotThisSession &&
                worldSaveAdapter.LastRestoreSucceeded)
            {
                PersistentWorldDebug.Log(
                    $"post-load late-join synchronization pending clientId={clientId} restoreSequence={worldSaveAdapter.LastRestoreSequence} identityValidated={worldSaveAdapter.LastRestoreIdentityValidated} identityIssues={worldSaveAdapter.LastRestoreIdentityIssues}",
                    this);
            }
        }

        if (!hookedManager.IsServer && clientId == hookedManager.LocalClientId)
        {
            PersistentWorldDebug.Log($"client connected localClientId={clientId}", this);
            ScheduleSnapshotRequest("local client connected");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        readyClients.Remove(clientId);
        RemoveServerTransfer(clientId, "client_disconnected");

        if (hookedManager == null || hookedManager.IsServer || clientId != hookedManager.LocalClientId)
        {
            return;
        }

        pendingTransfer = null;
        localSnapshotRequestSent = false;
        localSyncFailed = false;
        earliestSnapshotRequestTime = 0f;
        forceSnapshotRequestTime = 0f;
        lastSnapshotRequestSentTime = 0f;
        SetLocalSyncStatus(string.Empty);
        SetLocalWorldReady(true);
    }

    private bool ShouldRequestSnapshot()
    {
        return hookedManager != null &&
               hookedManager.IsClient &&
               !hookedManager.IsServer &&
               hookedManager.IsConnectedClient &&
               IsSnapshotRequestSceneEligible() &&
               !localSnapshotRequestSent &&
               !localSyncFailed &&
               Time.unscaledTime >= earliestSnapshotRequestTime &&
               (IsLocalPipelineReadyForSnapshotRequest() || Time.unscaledTime >= forceSnapshotRequestTime);
    }

    private void SendSnapshotRequest()
    {
        if (hookedManager == null || hookedManager.IsServer || hookedManager.CustomMessagingManager == null)
        {
            return;
        }

        using FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe(SceneManager.GetActiveScene().name);
        hookedManager.CustomMessagingManager.SendNamedMessage(
            RequestSnapshotMessageName,
            NetworkManager.ServerClientId,
            writer,
            NetworkDelivery.ReliableSequenced);

        PersistentWorldDebug.Log($"snapshot requested scene='{SceneManager.GetActiveScene().name}'", this);
        localSnapshotRequestSent = true;
        lastSnapshotRequestSentTime = Time.unscaledTime;
        SetLocalSyncStatus("Demande de l'etat du monde au host...");
        SetLocalWorldReady(false);
    }

    private void HandleSnapshotRequest(ulong senderClientId, FastBufferReader reader)
    {
        if (hookedManager == null || !hookedManager.IsServer)
        {
            return;
        }

        reader.ReadValueSafe(out string requestedSceneName);
        string activeSceneName = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrWhiteSpace(requestedSceneName) &&
            !string.Equals(requestedSceneName, activeSceneName, StringComparison.Ordinal))
        {
            PersistentWorldDebug.Warn($"snapshot requested scene mismatch clientId={senderClientId} requested='{requestedSceneName}' active='{activeSceneName}'", this);
        }

        PersistentWorldDebug.Log($"snapshot requested clientId={senderClientId} scene='{requestedSceneName}'", this);
        if (readyClients.TryGetValue(senderClientId, out bool clientWasReady) && clientWasReady)
        {
            PersistentWorldDebug.Warn(
                $"late-join snapshot requested by already-ready client clientId={senderClientId} hostWorldMode='{DescribeHostWorldMode()}'",
                this);
        }

        if (worldSaveAdapter != null && worldSaveAdapter.HasRestoredWorldSnapshotThisSession)
        {
            PersistentWorldDebug.Log(
                $"post-load late-join synchronization request clientId={senderClientId} restoreSequence={worldSaveAdapter.LastRestoreSequence} identityValidated={worldSaveAdapter.LastRestoreIdentityValidated}",
                this);
        }
        else
        {
            PersistentWorldDebug.Log(
                $"fresh-session late-join synchronization request clientId={senderClientId} pendingClients={CountPendingRemoteClients()}",
                this);
        }

        SendSnapshotToClient(senderClientId);
    }

    private void SendSnapshotToClient(ulong clientId)
    {
        if (hookedManager == null || hookedManager.CustomMessagingManager == null || worldStateManager == null)
        {
            return;
        }

        if (worldSaveAdapter != null && worldSaveAdapter.IsHostWorldRestoreInProgress)
        {
            PersistentWorldDebug.Warn(
                $"post-load late-join synchronization delayed clientId={clientId} because host world restore is still in progress reason='{worldSaveAdapter.LastRestoreReason}'",
                this);
            return;
        }

        bool sendingRestoredHostWorld =
            worldSaveAdapter != null &&
            worldSaveAdapter.HasRestoredWorldSnapshotThisSession &&
            worldSaveAdapter.LastRestoreSucceeded;
        if (sendingRestoredHostWorld)
        {
            PersistentWorldDebug.Log(
                $"post-load late-join synchronization preparing snapshot clientId={clientId} restoreSequence={worldSaveAdapter.LastRestoreSequence} identityValidated={worldSaveAdapter.LastRestoreIdentityValidated} identityIssues={worldSaveAdapter.LastRestoreIdentityIssues}",
                this);
        }
        else
        {
            PersistentWorldDebug.Log(
                $"fresh-session late-join synchronization preparing snapshot clientId={clientId} pendingClients={CountPendingRemoteClients()}",
                this);
        }

        WorldSnapshot snapshot = worldStateManager.CaptureSnapshot(
            sendingRestoredHostWorld
                ? $"late-join export clientId={clientId} hostMode=restored"
                : $"late-join export clientId={clientId} hostMode=fresh");
        if (sendingRestoredHostWorld)
        {
            worldSaveAdapter.ValidatePostLoadLateJoinSnapshot(snapshot, clientId);
        }

        if (pendingServerTransfers.TryGetValue(clientId, out ServerPendingSnapshotTransfer existingTransfer))
        {
            PersistentWorldDebug.Warn(
                $"late-join snapshot transfer replaced clientId={clientId} previousTransferId={existingTransfer.TransferId} previousBytes={existingTransfer.TotalBytes} previousChunks={existingTransfer.TotalChunks} ready={IsClientReady(clientId)}",
                this);
        }

        byte[] payload = snapshotSerializer.Serialize(snapshot);
        List<SnapshotSerializer.SnapshotChunk> chunks = snapshotSerializer.ChunkPayload(payload, maxChunkPayloadBytes);
        ulong transferId = nextTransferId++;

        readyClients[clientId] = false;
        pendingServerTransfers[clientId] = new ServerPendingSnapshotTransfer
        {
            ClientId = clientId,
            TransferId = transferId,
            TotalBytes = payload.Length,
            TotalChunks = chunks.Count,
            StartedAtUnscaledTime = Time.unscaledTime,
            HostWorldMode = DescribeHostWorldMode()
        };

        using (FastBufferWriter startWriter = new FastBufferWriter(256, Allocator.Temp))
        {
            startWriter.WriteValueSafe(transferId);
            startWriter.WriteValueSafe(chunks.Count);
            startWriter.WriteValueSafe(payload.Length);
            startWriter.WriteValueSafe(snapshot.SceneName ?? string.Empty);
            hookedManager.CustomMessagingManager.SendNamedMessage(
                SnapshotStartMessageName,
                clientId,
                startWriter,
                NetworkDelivery.ReliableSequenced);
        }

        for (int i = 0; i < chunks.Count; i++)
        {
            SnapshotSerializer.SnapshotChunk chunk = chunks[i];
            byte[] chunkPayload = chunk.Payload ?? Array.Empty<byte>();

            using FastBufferWriter chunkWriter = new FastBufferWriter(chunkPayload.Length + 128, Allocator.Temp);
            chunkWriter.WriteValueSafe(transferId);
            chunkWriter.WriteValueSafe(chunk.Index);
            chunkWriter.WriteValueSafe(chunk.TotalChunks);
            chunkWriter.WriteValueSafe(chunkPayload.Length);
            if (chunkPayload.Length > 0)
            {
                chunkWriter.WriteBytesSafe(chunkPayload, chunkPayload.Length);
            }

            hookedManager.CustomMessagingManager.SendNamedMessage(
                SnapshotChunkMessageName,
                clientId,
                chunkWriter,
                NetworkDelivery.ReliableFragmentedSequenced);
        }

        using (FastBufferWriter finishWriter = new FastBufferWriter(128, Allocator.Temp))
        {
            finishWriter.WriteValueSafe(transferId);
            finishWriter.WriteValueSafe(chunks.Count);
            hookedManager.CustomMessagingManager.SendNamedMessage(
                SnapshotFinishMessageName,
                clientId,
                finishWriter,
                NetworkDelivery.ReliableSequenced);
        }

        PersistentWorldDebug.Log($"snapshot sent clientId={clientId} transferId={transferId} chunks={chunks.Count} bytes={payload.Length}", this);
        if (sendingRestoredHostWorld)
        {
            PersistentWorldDebug.Log(
                $"post-load late-join synchronization snapshot sent clientId={clientId} restoreSequence={worldSaveAdapter.LastRestoreSequence} transferId={transferId}",
                this);
        }
        else
        {
            PersistentWorldDebug.Log(
                $"fresh-session late-join synchronization snapshot sent clientId={clientId} transferId={transferId}",
                this);
        }
    }

    private void HandleSnapshotStart(ulong senderClientId, FastBufferReader reader)
    {
        if (hookedManager == null || hookedManager.IsServer)
        {
            return;
        }

        if (!ValidateServerSender(senderClientId, "snapshot start"))
        {
            return;
        }

        PendingSnapshotTransfer transfer = new PendingSnapshotTransfer();
        reader.ReadValueSafe(out transfer.TransferId);
        reader.ReadValueSafe(out transfer.TotalChunks);
        reader.ReadValueSafe(out transfer.TotalBytes);
        reader.ReadValueSafe(out transfer.SceneName);
        transfer.SenderClientId = senderClientId;
        transfer.StartedAtUnscaledTime = Time.unscaledTime;

        if (transfer.TotalChunks <= 0 || transfer.TotalBytes < 0)
        {
            FailLocalSync(
                $"snapshot received invalid transfer metadata transferId={transfer.TransferId} chunks={transfer.TotalChunks} bytes={transfer.TotalBytes}");
            return;
        }

        if (pendingTransfer != null && pendingTransfer.TransferId != transfer.TransferId)
        {
            PersistentWorldDebug.Warn(
                $"snapshot received replacing pending transfer old={pendingTransfer.TransferId} new={transfer.TransferId}",
                this);
        }

        pendingTransfer = transfer;
        SetLocalSyncStatus($"Reception du snapshot '{transfer.SceneName}'...");
        SetLocalWorldReady(false);
        PersistentWorldDebug.Log($"snapshot received transferId={transfer.TransferId} scene='{transfer.SceneName}' chunks={transfer.TotalChunks} bytes={transfer.TotalBytes}", this);
    }

    private void HandleSnapshotChunk(ulong senderClientId, FastBufferReader reader)
    {
        if (hookedManager == null || hookedManager.IsServer || pendingTransfer == null)
        {
            return;
        }

        if (!ValidateServerSender(senderClientId, "snapshot chunk"))
        {
            return;
        }

        reader.ReadValueSafe(out ulong transferId);
        if (transferId != pendingTransfer.TransferId)
        {
            PersistentWorldDebug.Warn(
                $"snapshot received unexpected chunk transferId={transferId} expected={pendingTransfer.TransferId}",
                this);
            return;
        }

        reader.ReadValueSafe(out int chunkIndex);
        reader.ReadValueSafe(out int totalChunks);
        reader.ReadValueSafe(out int chunkLength);

        if (chunkIndex < 0 || totalChunks <= 0 || chunkIndex >= totalChunks || chunkLength < 0)
        {
            FailLocalSync(
                $"snapshot received invalid chunk metadata transferId={transferId} chunkIndex={chunkIndex} totalChunks={totalChunks} chunkLength={chunkLength}");
            return;
        }

        if (pendingTransfer.TotalChunks != totalChunks)
        {
            FailLocalSync(
                $"snapshot received inconsistent chunk count transferId={transferId} expected={pendingTransfer.TotalChunks} actual={totalChunks}");
            return;
        }

        byte[] payload = chunkLength > 0 ? new byte[chunkLength] : Array.Empty<byte>();
        if (chunkLength > 0)
        {
            reader.ReadBytesSafe(ref payload, chunkLength);
        }

        if (pendingTransfer.Chunks.ContainsKey(chunkIndex))
        {
            PersistentWorldDebug.Warn($"snapshot received duplicate chunk transferId={transferId} chunkIndex={chunkIndex}", this);
        }

        pendingTransfer.Chunks[chunkIndex] = payload;

        if (pendingTransfer.FinishReceived)
        {
            TryCompletePendingTransfer();
        }
    }

    private void HandleSnapshotFinish(ulong senderClientId, FastBufferReader reader)
    {
        if (hookedManager == null || hookedManager.IsServer || pendingTransfer == null)
        {
            return;
        }

        if (!ValidateServerSender(senderClientId, "snapshot finish"))
        {
            return;
        }

        reader.ReadValueSafe(out ulong transferId);
        reader.ReadValueSafe(out int totalChunks);

        if (transferId != pendingTransfer.TransferId)
        {
            FailLocalSync(
                $"snapshot received finish for unexpected transfer transferId={transferId} expected={pendingTransfer.TransferId}");
            return;
        }

        pendingTransfer.ExpectedFinishChunks = totalChunks;
        pendingTransfer.FinishReceived = true;
        PersistentWorldDebug.Log(
            $"snapshot received finish transferId={transferId} chunks={totalChunks} receivedChunks={pendingTransfer.Chunks.Count}",
            this);
        TryCompletePendingTransfer();
    }

    private bool SendClientReadyToServer(ulong transferId)
    {
        if (hookedManager == null || hookedManager.IsServer || hookedManager.CustomMessagingManager == null)
        {
            LogClientReadyAckNotSent(transferId, "custom messaging unavailable or instance is not a remote client");
            return false;
        }

        using FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp);
        writer.WriteValueSafe(transferId);
        hookedManager.CustomMessagingManager.SendNamedMessage(
            ClientReadyMessageName,
            NetworkManager.ServerClientId,
            writer,
            NetworkDelivery.ReliableSequenced);

        PersistentWorldDebug.Log($"client ready transferId={transferId}", this);
        return true;
    }

    private void HandleClientReady(ulong senderClientId, FastBufferReader reader)
    {
        if (hookedManager == null || !hookedManager.IsServer)
        {
            return;
        }

        reader.ReadValueSafe(out ulong transferId);
        PersistentWorldDebug.Log(
            $"client ready acknowledged clientId={senderClientId} transferId={transferId} hostWorldMode='{DescribeHostWorldMode()}'",
            this);

        if (!CompleteServerTransfer(senderClientId, transferId))
        {
            PersistentWorldDebug.Warn(
                $"client ready ignored because transfer validation failed clientId={senderClientId} transferId={transferId}",
                this);
            return;
        }

        readyClients[senderClientId] = true;
        ClientMarkedReady?.Invoke(senderClientId);
    }

    private void SetLocalWorldReady(bool ready)
    {
        if (IsLocalWorldReady == ready)
        {
            SyncLocalGameplayBlock();
            return;
        }

        IsLocalWorldReady = ready;
        if (!ready)
        {
            LocalWorldSyncStarted?.Invoke();
        }

        SyncLocalGameplayBlock();
    }

    private void ScheduleSnapshotRequest(string reason)
    {
        localSnapshotRequestSent = false;
        localSyncFailed = false;
        earliestSnapshotRequestTime = Time.unscaledTime + Mathf.Max(0f, snapshotRequestDelaySeconds);
        forceSnapshotRequestTime = Time.unscaledTime + Mathf.Max(snapshotRequestDelaySeconds, forceSnapshotRequestAfterSeconds);
        pendingTransfer = null;
        lastSnapshotRequestSentTime = 0f;
        SetLocalSyncStatus("Preparation de la synchronisation du monde...");
        PersistentWorldDebug.Log($"snapshot requested scheduled reason='{reason}' earliest={earliestSnapshotRequestTime:F2} force={forceSnapshotRequestTime:F2}", this);

        if (blockLocalGameplayUntilSynchronized)
        {
            SetLocalWorldReady(false);
        }
    }

    private bool IsLocalPipelineReadyForSnapshotRequest()
    {
        return ResolveReadyLocalPersistentCharacter() != null && CountReadyRuntimeCharacters() > 0;
    }

    private static bool IsSnapshotRequestSceneEligible()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return false;
        }

        return !string.Equals(activeScene.name, MainMenuController.DefaultMenuSceneName, StringComparison.OrdinalIgnoreCase);
    }

    private static PersistentNetworkObject ResolveReadyLocalPersistentCharacter()
    {
        Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
        if (localRoot == null)
        {
            return null;
        }

        NetworkObject networkObject = localRoot.GetComponent<NetworkObject>();
        if (networkObject == null || !networkObject.IsSpawned)
        {
            return null;
        }

        NetcodeCharacterIdentity identity = localRoot.GetComponent<NetcodeCharacterIdentity>();
        if (identity == null || string.IsNullOrWhiteSpace(identity.CharacterId))
        {
            return null;
        }

        PersistentNetworkObject persistentObject = localRoot.GetComponent<PersistentNetworkObject>();
        if (persistentObject == null ||
            persistentObject.ObjectKind != PersistentObjectKind.RuntimeSpawned ||
            string.IsNullOrWhiteSpace(persistentObject.PersistentId))
        {
            return null;
        }

        return persistentObject;
    }

    private static int CountReadyRuntimeCharacters()
    {
#if UNITY_2023_1_OR_NEWER
        NetcodeCharacterIdentity[] identities = UnityEngine.Object.FindObjectsByType<NetcodeCharacterIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        NetcodeCharacterIdentity[] identities = UnityEngine.Object.FindObjectsOfType<NetcodeCharacterIdentity>(true);
#endif
        if (identities == null)
        {
            return 0;
        }

        int readyCount = 0;
        for (int i = 0; i < identities.Length; i++)
        {
            NetcodeCharacterIdentity identity = identities[i];
            if (identity == null || string.IsNullOrWhiteSpace(identity.CharacterId))
            {
                continue;
            }

            NetworkObject networkObject = identity.GetComponent<NetworkObject>();
            PersistentNetworkObject persistentObject = identity.GetComponent<PersistentNetworkObject>();
            if (networkObject == null ||
                !networkObject.IsSpawned ||
                persistentObject == null ||
                persistentObject.ObjectKind != PersistentObjectKind.RuntimeSpawned ||
                string.IsNullOrWhiteSpace(persistentObject.PersistentId))
            {
                continue;
            }

            readyCount++;
        }

        return readyCount;
    }

    private void SyncLocalGameplayBlock()
    {
        if (!blockLocalGameplayUntilSynchronized)
        {
            ReleaseLocalGameplayBlock();
            return;
        }

        bool shouldBlock = hookedManager != null &&
                           hookedManager.IsClient &&
                           !hookedManager.IsServer &&
                           !IsLocalWorldReady;

        if (shouldBlock)
        {
            LocalInputRouter.ResetMove();
            if (!syncInputLockApplied && SquadManager.Instance != null)
            {
                SquadManager.Instance.SetInputLocked(true);
                syncInputLockApplied = true;
            }

            return;
        }

        ReleaseLocalGameplayBlock();
    }

    private void ReleaseLocalGameplayBlock()
    {
        if (!syncInputLockApplied)
        {
            return;
        }

        if (SquadManager.Instance != null)
        {
            SquadManager.Instance.SetInputLocked(false);
        }

        syncInputLockApplied = false;
    }

    private void TryHookNetworkManager()
    {
        NetworkManager currentManager = NetworkManager.Singleton;
        if (currentManager == hookedManager)
        {
            RegisterMessageHandlers();
            return;
        }

        UnregisterMessageHandlers();
        UnhookNetworkManager();

        hookedManager = currentManager;
        if (hookedManager == null)
        {
            return;
        }

        hookedManager.OnClientConnectedCallback += OnClientConnected;
        hookedManager.OnClientDisconnectCallback += OnClientDisconnected;
        RegisterMessageHandlers();

        if (hookedManager.IsClient &&
            !hookedManager.IsServer &&
            hookedManager.IsConnectedClient &&
            !localSnapshotRequestSent &&
            earliestSnapshotRequestTime <= 0f &&
            forceSnapshotRequestTime <= 0f)
        {
            ScheduleSnapshotRequest("network manager hooked on connected client");
        }
    }

    private void UnhookNetworkManager()
    {
        if (hookedManager == null)
        {
            return;
        }

        hookedManager.OnClientConnectedCallback -= OnClientConnected;
        hookedManager.OnClientDisconnectCallback -= OnClientDisconnected;
        hookedManager = null;
        pendingServerTransfers.Clear();
    }

    private void RegisterMessageHandlers()
    {
        if (handlersRegistered || hookedManager == null || hookedManager.CustomMessagingManager == null)
        {
            return;
        }

        CustomMessagingManager messaging = hookedManager.CustomMessagingManager;
        messaging.RegisterNamedMessageHandler(RequestSnapshotMessageName, HandleSnapshotRequest);
        messaging.RegisterNamedMessageHandler(SnapshotStartMessageName, HandleSnapshotStart);
        messaging.RegisterNamedMessageHandler(SnapshotChunkMessageName, HandleSnapshotChunk);
        messaging.RegisterNamedMessageHandler(SnapshotFinishMessageName, HandleSnapshotFinish);
        messaging.RegisterNamedMessageHandler(ClientReadyMessageName, HandleClientReady);

        handlersRegistered = true;
    }

    private void UnregisterMessageHandlers()
    {
        if (!handlersRegistered || hookedManager == null || hookedManager.CustomMessagingManager == null)
        {
            handlersRegistered = false;
            return;
        }

        CustomMessagingManager messaging = hookedManager.CustomMessagingManager;
        messaging.UnregisterNamedMessageHandler(RequestSnapshotMessageName);
        messaging.UnregisterNamedMessageHandler(SnapshotStartMessageName);
        messaging.UnregisterNamedMessageHandler(SnapshotChunkMessageName);
        messaging.UnregisterNamedMessageHandler(SnapshotFinishMessageName);
        messaging.UnregisterNamedMessageHandler(ClientReadyMessageName);

        handlersRegistered = false;
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

        if (worldSaveAdapter == null)
        {
#if UNITY_2023_1_OR_NEWER
            worldSaveAdapter = FindFirstObjectByType<WorldSaveAdapter>();
#else
            worldSaveAdapter = FindObjectOfType<WorldSaveAdapter>();
#endif
        }

        if (syncOverlay == null)
        {
#if UNITY_2023_1_OR_NEWER
            syncOverlay = FindFirstObjectByType<PersistentWorldSyncOverlay>();
#else
            syncOverlay = FindObjectOfType<PersistentWorldSyncOverlay>();
#endif
        }
    }

    private void OnHostWorldRestoreCompleted(WorldSnapshot snapshot)
    {
        PersistentWorldDebug.Log(
            $"host world restore ready for late join scene='{snapshot?.SceneName}' runtimeObjects={snapshot?.RuntimeObjects?.Count ?? 0} sceneObjects={snapshot?.SceneObjects?.Count ?? 0} restoreSequence={hookedWorldSaveAdapter?.LastRestoreSequence ?? 0} identityValidated={hookedWorldSaveAdapter != null && hookedWorldSaveAdapter.LastRestoreIdentityValidated}",
            this);

        if (hookedManager != null && hookedManager.IsServer && hookedManager.IsListening)
        {
            PersistentWorldDebug.Log("post-load late-join synchronization broadcasting restored host snapshot to pending clients", this);
            BroadcastSnapshotToRemoteClients();
        }
    }

    private void OnHostWorldRestoreFailed(string message)
    {
        PersistentWorldDebug.Error(
            $"host world restore failed; late-join continuity may be invalid error='{message}'",
            this);
    }

    private void TryHookWorldSaveAdapter()
    {
        if (hookedWorldSaveAdapter == worldSaveAdapter)
        {
            return;
        }

        UnhookWorldSaveAdapter();
        hookedWorldSaveAdapter = worldSaveAdapter;
        if (hookedWorldSaveAdapter == null)
        {
            return;
        }

        hookedWorldSaveAdapter.HostWorldRestoreCompleted += OnHostWorldRestoreCompleted;
        hookedWorldSaveAdapter.HostWorldRestoreFailed += OnHostWorldRestoreFailed;
    }

    private void UnhookWorldSaveAdapter()
    {
        if (hookedWorldSaveAdapter == null)
        {
            return;
        }

        hookedWorldSaveAdapter.HostWorldRestoreCompleted -= OnHostWorldRestoreCompleted;
        hookedWorldSaveAdapter.HostWorldRestoreFailed -= OnHostWorldRestoreFailed;
        hookedWorldSaveAdapter = null;
    }

    private int CountPendingRemoteClients()
    {
        if (hookedManager == null || !hookedManager.IsServer)
        {
            return 0;
        }

        int count = 0;
        foreach (ulong clientId in hookedManager.ConnectedClientsIds)
        {
            if (clientId == hookedManager.LocalClientId || IsClientReady(clientId))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private string DescribeHostWorldMode()
    {
        if (worldSaveAdapter != null &&
            worldSaveAdapter.HasRestoredWorldSnapshotThisSession &&
            worldSaveAdapter.LastRestoreSucceeded)
        {
            return $"restored_save:{worldSaveAdapter.LastRestoreSequence}";
        }

        return "fresh_session";
    }

    private bool CompleteServerTransfer(ulong clientId, ulong transferId)
    {
        if (!pendingServerTransfers.TryGetValue(clientId, out ServerPendingSnapshotTransfer transfer) || transfer == null)
        {
            PersistentWorldDebug.Warn(
                $"late-join snapshot ready ack had no tracked transfer clientId={clientId} transferId={transferId}",
                this);
            return false;
        }

        if (transfer.TransferId != transferId)
        {
            PersistentWorldDebug.Warn(
                $"late-join snapshot ready ack transfer mismatch clientId={clientId} transferId={transferId} trackedTransferId={transfer.TransferId} trackedBytes={transfer.TotalBytes} trackedChunks={transfer.TotalChunks} hostWorldMode='{transfer.HostWorldMode}'",
                this);
            return false;
        }

        float duration = Mathf.Max(0f, Time.unscaledTime - transfer.StartedAtUnscaledTime);
        PersistentWorldDebug.Log(
            $"late-join snapshot transfer completed clientId={clientId} transferId={transferId} duration={duration:F2}s bytes={transfer.TotalBytes} chunks={transfer.TotalChunks} hostWorldMode='{transfer.HostWorldMode}'",
            this);
        pendingServerTransfers.Remove(clientId);
        return true;
    }

    private void RemoveServerTransfer(ulong clientId, string reason)
    {
        if (!pendingServerTransfers.TryGetValue(clientId, out ServerPendingSnapshotTransfer transfer) || transfer == null)
        {
            return;
        }

        PersistentWorldDebug.Warn(
            $"late-join snapshot transfer removed clientId={clientId} transferId={transfer.TransferId} reason='{reason}' hostWorldMode='{transfer.HostWorldMode}'",
            this);
        pendingServerTransfers.Remove(clientId);
    }

    private string DescribeLastApplyResult()
    {
        if (worldStateManager == null || worldStateManager.LastApplyResult == null)
        {
            return "no_apply_result";
        }

        SnapshotApplyResult result = worldStateManager.LastApplyResult;
        string firstError = result.Errors.Count > 0 ? result.Errors[0] : string.Empty;
        return
            $"success={result.Succeeded} duplicateIds={result.DuplicateSnapshotIds} missingIds={result.MissingPersistentIds} missingScene={result.MissingSceneObjects} missingRuntime={result.MissingRuntimeObjects} missingRuntimePrefabs={result.MissingRuntimePrefabMappings} failedRecreations={result.FailedRuntimeRecreations} missingTransforms={result.MissingTransformTargets} missingGameplay={result.MissingGameplayTargets} typeMismatches={result.ObjectTypeMismatches} failedPayloads={result.FailedPayloadApplications} restoreOrderIssues={result.RestoreOrderIssues} validationIssues={result.ValidationIssues} errorCount={result.Errors.Count} firstError='{firstError}'";
    }

    private void TryCompletePendingTransfer()
    {
        if (pendingTransfer == null || !pendingTransfer.FinishReceived)
        {
            return;
        }

        if (!pendingTransfer.IsComplete)
        {
            PersistentWorldDebug.Warn(
                $"snapshot received incomplete transfer, waiting for chunks transferId={pendingTransfer.TransferId} expected={pendingTransfer.TotalChunks} received={pendingTransfer.Chunks.Count}",
                this);
            return;
        }

        if (pendingTransfer.ExpectedFinishChunks > 0 && pendingTransfer.ExpectedFinishChunks != pendingTransfer.TotalChunks)
        {
            FailLocalSync(
                $"snapshot finish chunk count mismatch transferId={pendingTransfer.TransferId} expected={pendingTransfer.TotalChunks} finish={pendingTransfer.ExpectedFinishChunks}");
            return;
        }

        if (pendingTransfer.Payload == null)
        {
            PersistentWorldDebug.Log(
                $"snapshot receive complete transferId={pendingTransfer.TransferId} chunks={pendingTransfer.TotalChunks} bytes={pendingTransfer.TotalBytes}",
                this);
            try
            {
                pendingTransfer.Payload = pendingTransfer.Reassemble();
            }
            catch (Exception ex)
            {
                LogClientReadyAckNotSent(pendingTransfer.TransferId, "snapshot reassembly failed");
                FailLocalSync($"snapshot reassembly failed transferId={pendingTransfer.TransferId} error='{ex.Message}'");
                return;
            }
        }

        if (pendingTransfer.Snapshot == null)
        {
            PersistentWorldDebug.Log(
                $"snapshot deserialize start transferId={pendingTransfer.TransferId} bytes={pendingTransfer.Payload.Length}",
                this);
            try
            {
                pendingTransfer.Snapshot = snapshotSerializer.Deserialize(pendingTransfer.Payload);
            }
            catch (Exception ex)
            {
                PersistentWorldDebug.Error(
                    $"snapshot deserialize exception transferId={pendingTransfer.TransferId} error='{ex.Message}' stackTrace='{ex}'",
                    this);
                LogClientReadyAckNotSent(pendingTransfer.TransferId, "snapshot deserialize exception");
                FailLocalSync($"snapshot deserialize failed transferId={pendingTransfer.TransferId} error='{ex.Message}'");
                return;
            }

            if (pendingTransfer.Snapshot == null)
            {
                LogClientReadyAckNotSent(pendingTransfer.TransferId, "snapshot deserialize returned null");
                FailLocalSync($"snapshot deserialize failed transferId={pendingTransfer.TransferId}");
                return;
            }

            PersistentWorldDebug.Log(
                $"snapshot deserialize completed transferId={pendingTransfer.TransferId} scene='{pendingTransfer.Snapshot.SceneName}' runtimeObjects={pendingTransfer.Snapshot.RuntimeObjects?.Count ?? 0} sceneObjects={pendingTransfer.Snapshot.SceneObjects?.Count ?? 0}",
                this);
        }

        WorldSnapshot snapshot = pendingTransfer.Snapshot;
        if (!CanApplySnapshotNow(snapshot, out PersistentObjectSnapshot blockingSnapshot, out string waitReason))
        {
            SetLocalSyncStatus("Attente des objets reseau du host...");
            if (!string.Equals(waitReason, pendingTransfer.WaitReason, StringComparison.Ordinal))
            {
                pendingTransfer.WaitReason = waitReason;
                PersistentWorldDebug.Warn(
                    $"snapshot apply waiting transferId={pendingTransfer.TransferId} reason='{waitReason}'",
                    this);
                if (blockingSnapshot != null)
                {
                    PersistentNetworkObject resolvedObject = null;
                    NetworkObjectRegistry registry = NetworkObjectRegistry.Instance;
                    if (registry != null)
                    {
                        registry.TryGet(blockingSnapshot.PersistentId, out resolvedObject);
                    }

                    PersistentWorldDebug.LogSnapshotObjectAudit(
                        "apply snapshot waiting",
                        blockingSnapshot.ObjectKind == PersistentObjectKind.ScenePlaced ? "scene" : "runtime",
                        blockingSnapshot,
                        PersistentWorldSceneInstaller.DescribeExpectedResolutionMode(blockingSnapshot),
                        this,
                        resolvedObject,
                        $"resolutionFailed='pending' reason='{waitReason}'");
                }
            }

            return;
        }

        pendingTransfer.WaitReason = string.Empty;
        SetLocalSyncStatus("Reconstruction du monde...");
        PersistentWorldDebug.Log(
            $"snapshot reconstruction start transferId={pendingTransfer.TransferId} scene='{snapshot.SceneName}'",
            this);
        bool applySucceeded;
        try
        {
            applySucceeded = worldStateManager != null && worldStateManager.ApplySnapshot(snapshot, false);
        }
        catch (Exception ex)
        {
            PersistentWorldDebug.Error(
                $"snapshot reconstruction exception transferId={pendingTransfer.TransferId} error='{ex.Message}' stackTrace='{ex}'",
                this);
            LogClientReadyAckNotSent(pendingTransfer.TransferId, "snapshot reconstruction exception");
            FailLocalSync($"snapshot reconstruction threw transferId={pendingTransfer.TransferId} error='{ex.Message}'");
            return;
        }

        if (!applySucceeded)
        {
            PersistentWorldDebug.Error(
                $"snapshot reconstruction failed transferId={pendingTransfer.TransferId} result='{DescribeLastApplyResult()}'",
                this);
            LogClientReadyAckNotSent(pendingTransfer.TransferId, "snapshot reconstruction failed");
            FailLocalSync(
                $"snapshot reconstruction failed transferId={pendingTransfer.TransferId} result='{DescribeLastApplyResult()}'");
            return;
        }

        PersistentWorldDebug.Log(
            $"snapshot reconstruction applied transferId={pendingTransfer.TransferId} result='{DescribeLastApplyResult()}'",
            this);

        PersistentNetworkObject controlledObject = worldStateManager.ResolveControlledObject(snapshot, hookedManager.LocalClientId);
        if (controlledObject != null)
        {
            LocalPlayerContext.SetLocalCharacter(
                controlledObject.transform,
                "join_snapshot_controlled_object",
                LocalPlayerContext.Authority.MultiplayerAssignment);
        }
        else if (HasPlayerSnapshotForClient(snapshot, hookedManager.LocalClientId))
        {
            LogClientReadyAckNotSent(pendingTransfer.TransferId, "controlled persistent object not resolved");
            FailLocalSync($"client controlled persistent object not resolved for clientId={hookedManager.LocalClientId}");
            return;
        }
        else
        {
            PersistentWorldDebug.Warn($"client controlled persistent object not resolved for clientId={hookedManager.LocalClientId}", this);
        }

        ulong transferId = pendingTransfer.TransferId;
        pendingTransfer = null;
        bool ackSent = SendClientReadyToServer(transferId);
        if (!ackSent)
        {
            PersistentWorldDebug.Warn(
                $"snapshot reconstruction completed but ready ack was not sent transferId={transferId}",
                this);
        }
        else
        {
            PersistentWorldDebug.Log(
                $"snapshot reconstruction completed transferId={transferId} readyAckSent=true",
                this);
        }
        SetLocalSyncStatus(string.Empty);
        SetLocalWorldReady(true);
        localSyncFailed = false;
        PersistentWorldDebug.Log("release gameplay", this);
        LocalWorldSyncCompleted?.Invoke(controlledObject);
    }

    private void HandleSnapshotTimeouts()
    {
        if (hookedManager != null && hookedManager.IsServer && snapshotTransferTimeoutSeconds > 0f)
        {
            HandleServerTransferTimeouts();
        }

        if (hookedManager == null ||
            !hookedManager.IsClient ||
            hookedManager.IsServer ||
            IsLocalWorldReady ||
            localSyncFailed ||
            snapshotTransferTimeoutSeconds <= 0f)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (pendingTransfer != null)
        {
            if (now - pendingTransfer.StartedAtUnscaledTime < snapshotTransferTimeoutSeconds)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(pendingTransfer.WaitReason))
            {
                PersistentWorldDebug.Error(
                    $"snapshot apply readiness timed out transferId={pendingTransfer.TransferId} expectedChunks={pendingTransfer.TotalChunks} receivedChunks={pendingTransfer.Chunks.Count} reason='{pendingTransfer.WaitReason}'",
                    this);
                ScheduleSnapshotRequest("snapshot apply readiness timeout");
                return;
            }

            PersistentWorldDebug.Error(
                $"snapshot transfer timed out transferId={pendingTransfer.TransferId} expectedChunks={pendingTransfer.TotalChunks} receivedChunks={pendingTransfer.Chunks.Count}",
                this);
            ScheduleSnapshotRequest("snapshot transfer timeout");
            return;
        }

        if (!localSnapshotRequestSent || lastSnapshotRequestSentTime <= 0f || now - lastSnapshotRequestSentTime < snapshotTransferTimeoutSeconds)
        {
            return;
        }

        PersistentWorldDebug.Error("snapshot request timed out before transfer start", this);
        ScheduleSnapshotRequest("snapshot request timeout");
    }

    private void HandleServerTransferTimeouts()
    {
        if (pendingServerTransfers.Count == 0)
        {
            return;
        }

        float now = Time.unscaledTime;
        foreach (KeyValuePair<ulong, ServerPendingSnapshotTransfer> pair in pendingServerTransfers)
        {
            ServerPendingSnapshotTransfer transfer = pair.Value;
            if (transfer == null || transfer.TimeoutLogged)
            {
                continue;
            }

            if (now - transfer.StartedAtUnscaledTime < snapshotTransferTimeoutSeconds)
            {
                continue;
            }

            transfer.TimeoutLogged = true;
            PersistentWorldDebug.Warn(
                $"late-join snapshot transfer still pending clientId={transfer.ClientId} transferId={transfer.TransferId} age={(now - transfer.StartedAtUnscaledTime):F2}s bytes={transfer.TotalBytes} chunks={transfer.TotalChunks} hostWorldMode='{transfer.HostWorldMode}'",
                this);
        }
    }

    private void SyncVisualGate()
    {
        if (syncOverlay == null)
        {
            return;
        }

        bool shouldShow =
            hookedManager != null &&
            hookedManager.IsClient &&
            !hookedManager.IsServer &&
            !IsLocalWorldReady;
        syncOverlay.SetVisible(shouldShow, localSyncStatusMessage, localSyncFailed);
    }

    private void SetLocalSyncStatus(string message)
    {
        localSyncStatusMessage = message ?? string.Empty;
        SyncVisualGate();
    }

    private void FailLocalSync(string message)
    {
        localSyncFailed = true;
        pendingTransfer = null;
        SetLocalSyncStatus(message);
        SetLocalWorldReady(false);
        PersistentWorldDebug.Error(message, this);
        LocalWorldSyncFailed?.Invoke(message);
    }

    private void LogClientReadyAckNotSent(ulong transferId, string reason)
    {
        PersistentWorldDebug.Warn(
            $"client ready not sent transferId={transferId} reason='{reason}'",
            this);
    }

    private bool ValidateServerSender(ulong senderClientId, string messageType)
    {
        if (senderClientId == NetworkManager.ServerClientId)
        {
            return true;
        }

        FailLocalSync($"snapshot received unexpected sender clientId={senderClientId} message='{messageType}'");
        return false;
    }

    private static bool HasPlayerSnapshotForClient(WorldSnapshot snapshot, ulong clientId)
    {
        if (snapshot == null || snapshot.Players == null)
        {
            return false;
        }

        for (int i = 0; i < snapshot.Players.Count; i++)
        {
            PlayerSnapshot playerSnapshot = snapshot.Players[i];
            if (playerSnapshot != null && playerSnapshot.OwnerClientId == clientId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanApplySnapshotNow(
        WorldSnapshot snapshot,
        out PersistentObjectSnapshot blockingSnapshot,
        out string waitReason)
    {
        blockingSnapshot = null;
        waitReason = string.Empty;
        if (snapshot == null || snapshot.RuntimeObjects == null)
        {
            return true;
        }

        for (int i = 0; i < snapshot.RuntimeObjects.Count; i++)
        {
            PersistentObjectSnapshot runtimeSnapshot = snapshot.RuntimeObjects[i];
            if (!IsRuntimeCharacterSnapshot(runtimeSnapshot))
            {
                continue;
            }

            if (TryResolveReadyRuntimeCharacter(runtimeSnapshot, out string failureReason))
            {
                continue;
            }

            blockingSnapshot = runtimeSnapshot;
            waitReason = failureReason;
            return false;
        }

        return true;
    }

    private static bool IsRuntimeCharacterSnapshot(PersistentObjectSnapshot snapshot)
    {
        return snapshot != null &&
               snapshot.ObjectKind == PersistentObjectKind.RuntimeSpawned &&
               !string.IsNullOrWhiteSpace(snapshot.RuntimePrefabId) &&
               snapshot.RuntimePrefabId.StartsWith(PersistentWorldSceneInstaller.CharacterPrefabPrefix, StringComparison.Ordinal);
    }

    private static bool TryResolveReadyRuntimeCharacter(PersistentObjectSnapshot snapshot, out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsRuntimeCharacterSnapshot(snapshot))
        {
            return true;
        }

        string expectedResolutionMode = PersistentWorldSceneInstaller.DescribeExpectedResolutionMode(snapshot);
        if (NetworkObjectRegistry.Instance == null)
        {
            failureReason =
                $"persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}' expectedResolutionMode='{expectedResolutionMode}' why='network object registry is not initialized yet'";
            return false;
        }

        if (!NetworkObjectRegistry.Instance.TryGet(snapshot.PersistentId, out PersistentNetworkObject persistentObject) || persistentObject == null)
        {
            failureReason =
                $"persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}' expectedResolutionMode='{expectedResolutionMode}' why='spawned NGO player object is not registered yet'";
            return false;
        }

        if (persistentObject.ObjectKind != PersistentObjectKind.RuntimeSpawned)
        {
            failureReason =
                $"persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}' expectedResolutionMode='{expectedResolutionMode}' why='registered object kind is {persistentObject.ObjectKind} instead of RuntimeSpawned'";
            return false;
        }

        if (!string.Equals(persistentObject.RuntimePrefabId ?? string.Empty, snapshot.RuntimePrefabId ?? string.Empty, StringComparison.Ordinal))
        {
            failureReason =
                $"persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}' expectedResolutionMode='{expectedResolutionMode}' why='registered object prefab is {persistentObject.RuntimePrefabId}'";
            return false;
        }

        NetworkObject networkObject = persistentObject.GetComponent<NetworkObject>();
        if (networkObject == null || !networkObject.IsSpawned)
        {
            failureReason =
                $"persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}' expectedResolutionMode='{expectedResolutionMode}' why='registered object does not have a spawned NetworkObject yet'";
            return false;
        }

        NetcodeCharacterIdentity identity = persistentObject.GetComponent<NetcodeCharacterIdentity>();
        if (identity == null || string.IsNullOrWhiteSpace(identity.CharacterId))
        {
            failureReason =
                $"persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}' expectedResolutionMode='{expectedResolutionMode}' why='NetcodeCharacterIdentity is missing or not synchronized yet'";
            return false;
        }

        string expectedCharacterId = snapshot.RuntimePrefabId.Substring(PersistentWorldSceneInstaller.CharacterPrefabPrefix.Length);
        if (!string.Equals(identity.CharacterId, expectedCharacterId, StringComparison.Ordinal))
        {
            failureReason =
                $"persistentId='{snapshot.PersistentId}' prefab='{snapshot.RuntimePrefabId}' expectedResolutionMode='{expectedResolutionMode}' why='resolved characterId is {identity.CharacterId}'";
            return false;
        }

        return true;
    }

    private sealed class PendingSnapshotTransfer
    {
        public ulong TransferId;
        public int TotalChunks;
        public int TotalBytes;
        public string SceneName;
        public ulong SenderClientId;
        public bool FinishReceived;
        public int ExpectedFinishChunks;
        public float StartedAtUnscaledTime;
        public Dictionary<int, byte[]> Chunks = new Dictionary<int, byte[]>();
        public byte[] Payload;
        public WorldSnapshot Snapshot;
        public string WaitReason;

        public bool IsComplete => TotalChunks > 0 && Chunks.Count == TotalChunks;

        public byte[] Reassemble()
        {
            byte[] payload = new byte[Mathf.Max(0, TotalBytes)];
            int offset = 0;

            for (int i = 0; i < TotalChunks; i++)
            {
                if (!Chunks.TryGetValue(i, out byte[] chunk) || chunk == null)
                {
                    throw new InvalidOperationException("JoinSyncSystem: missing snapshot chunk during reassembly.");
                }

                Buffer.BlockCopy(chunk, 0, payload, offset, chunk.Length);
                offset += chunk.Length;
            }

            return payload;
        }
    }

    private sealed class ServerPendingSnapshotTransfer
    {
        public ulong ClientId;
        public ulong TransferId;
        public int TotalChunks;
        public int TotalBytes;
        public float StartedAtUnscaledTime;
        public bool TimeoutLogged;
        public string HostWorldMode;
    }
}
