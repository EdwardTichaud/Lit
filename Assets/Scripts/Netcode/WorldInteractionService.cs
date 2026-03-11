using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Service reseau pour router les interactions serveur (triggers non-networked).
public class WorldInteractionService : NetworkBehaviour
{
    public static WorldInteractionService Instance { get; private set; }

    private readonly NetworkList<NetPlayerAssignment> assignments = new NetworkList<NetPlayerAssignment>();
    private readonly NetworkVariable<FixedString128Bytes> activeSceneName = new NetworkVariable<FixedString128Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private Coroutine applyReplicatedWorldStateRoutine;
    private bool localSessionSyncInProgress;
    private bool localSessionSynchronized;
    private string localSessionSynchronizedScene = string.Empty;
    private ulong nextReplicatedWorldStateSequence = 1;
    public event Action AssignmentsChanged;
    public event Action ActiveSceneChanged;
    public event Action LocalSessionSynchronizationCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        assignments.OnListChanged += OnAssignmentsChanged;
        activeSceneName.OnValueChanged += OnActiveSceneNameChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (IsServer)
        {
            UpdateActiveSceneName(SceneManager.GetActiveScene().name);
        }
        AssignmentsChanged?.Invoke();
        ActiveSceneChanged?.Invoke();
        TryRequestLocalSessionSynchronization();
    }

    public override void OnNetworkDespawn()
    {
        assignments.OnListChanged -= OnAssignmentsChanged;
        activeSceneName.OnValueChanged -= OnActiveSceneNameChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (applyReplicatedWorldStateRoutine != null)
        {
            StopCoroutine(applyReplicatedWorldStateRoutine);
            applyReplicatedWorldStateRoutine = null;
        }
        localSessionSyncInProgress = false;
        localSessionSynchronized = false;
        localSessionSynchronizedScene = string.Empty;
        nextReplicatedWorldStateSequence = 1;
        if (assignments != null)
        {
            assignments.Clear();
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public int AssignmentCount => assignments != null ? assignments.Count : 0;

    public string ActiveSceneName => activeSceneName.Value.ToString();
    public bool IsLocalSessionSynchronized => localSessionSynchronized;
    public string LocalSessionSynchronizedScene => localSessionSynchronizedScene;

    public NetPlayerAssignment GetAssignment(int index)
    {
        return assignments[index];
    }

    public bool TryGetAssignedCharacterId(ulong clientId, out string characterId)
    {
        if (assignments == null)
        {
            characterId = string.Empty;
            return false;
        }

        for (int i = 0; i < assignments.Count; i++)
        {
            NetPlayerAssignment entry = assignments[i];
            if (entry.ClientId == clientId)
            {
                characterId = entry.CharacterId.ToString();
                return true;
            }
        }

        characterId = string.Empty;
        return false;
    }

    public bool TryGetAssignedClientId(string characterId, out ulong clientId)
    {
        clientId = 0;
        if (string.IsNullOrWhiteSpace(characterId) || assignments == null)
        {
            return false;
        }

        for (int i = 0; i < assignments.Count; i++)
        {
            NetPlayerAssignment entry = assignments[i];
            if (entry.CharacterId.ToString() == characterId)
            {
                clientId = entry.ClientId;
                return true;
            }
        }

        return false;
    }

    public bool IsCharacterAssigned(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId) || assignments == null)
        {
            return false;
        }

        for (int i = 0; i < assignments.Count; i++)
        {
            NetPlayerAssignment entry = assignments[i];
            if (entry.CharacterId.ToString() == characterId)
            {
                return true;
            }
        }

        return false;
    }

    public void SetAssignment(ulong clientId, string characterId)
    {
        if (!IsServer)
        {
            return;
        }

        if (assignments == null)
        {
            return;
        }

        ClearAssignment(clientId);
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return;
        }

        assignments.Add(new NetPlayerAssignment(clientId, new FixedString64Bytes(characterId)));
    }

    public void ClearAssignment(ulong clientId)
    {
        if (!IsServer || assignments == null)
        {
            return;
        }

        for (int i = assignments.Count - 1; i >= 0; i--)
        {
            if (assignments[i].ClientId == clientId)
            {
                assignments.RemoveAt(i);
            }
        }
    }

    public void ClearAllAssignments()
    {
        if (assignments == null)
        {
            return;
        }

        if (IsSpawned && !IsServer)
        {
            return;
        }

        assignments.Clear();
    }

    private void OnAssignmentsChanged(NetworkListEvent<NetPlayerAssignment> change)
    {
        AssignmentsChanged?.Invoke();
    }

    private void OnActiveSceneNameChanged(FixedString128Bytes previous, FixedString128Bytes current)
    {
        if (IsClient && !IsServer)
        {
            StopApplyReplicatedWorldStateRoutine();
            localSessionSyncInProgress = false;
            localSessionSynchronized = false;
            if (!string.Equals(localSessionSynchronizedScene, current.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                localSessionSynchronizedScene = string.Empty;
            }
            TryRequestLocalSessionSynchronization();
        }

        ActiveSceneChanged?.Invoke();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsServer)
        {
            UpdateActiveSceneName(scene.name);
        }

        if (IsClient && !IsServer)
        {
            StopApplyReplicatedWorldStateRoutine();
            localSessionSyncInProgress = false;
            localSessionSynchronized = false;
            localSessionSynchronizedScene = string.Empty;
            TryRequestLocalSessionSynchronization();
        }
    }

    private void UpdateActiveSceneName(string sceneName)
    {
        if (!IsServer)
        {
            return;
        }

        string resolvedName = string.IsNullOrWhiteSpace(sceneName)
            ? string.Empty
            : sceneName.Trim();
        activeSceneName.Value = new FixedString128Bytes(resolvedName);
    }

    public void TryRequestLocalSessionSynchronization()
    {
        if (!IsSpawned || !IsClient || IsServer)
        {
            return;
        }

        string targetSceneName = ActiveSceneName;
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !string.Equals(activeScene.name, targetSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (localSessionSyncInProgress)
        {
            return;
        }

        if (localSessionSynchronized && string.Equals(localSessionSynchronizedScene, targetSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        localSessionSyncInProgress = true;
        localSessionSynchronized = false;
        RequestSessionSynchronizationServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSessionSynchronizationServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        NetcodePlayerSpawner spawner = NetcodePlayerSpawner.Instance;
        if (spawner != null)
        {
            spawner.ResynchronizeClientState(clientId);
        }

        RepublishSpawnedObjectsToClient(clientId);
        string snapshotJson = ReplicatedWorldStateRegistry.CaptureJson(ActiveSceneName, nextReplicatedWorldStateSequence++);
        ReceiveReplicatedWorldStateClientRpc(snapshotJson, ActiveSceneName, BuildClientRpcParams(clientId));
    }

    [ClientRpc]
    private void ReceiveReplicatedWorldStateClientRpc(string snapshotJson, string sceneName, ClientRpcParams rpcParams = default)
    {
        if (IsServer)
        {
            return;
        }

        StopApplyReplicatedWorldStateRoutine();
        applyReplicatedWorldStateRoutine = StartCoroutine(ApplyReplicatedWorldStateRoutine(snapshotJson, sceneName));
    }

    private IEnumerator ApplyReplicatedWorldStateRoutine(string snapshotJson, string sceneName)
    {
        float timeout = Time.unscaledTime + 10f;
        bool sceneMatches = false;
        bool worldApplied = true;
        string worldDiagnostic = string.Empty;

        while (Time.unscaledTime < timeout)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            sceneMatches = activeScene.IsValid()
                && !string.IsNullOrWhiteSpace(sceneName)
                && string.Equals(activeScene.name, sceneName, StringComparison.OrdinalIgnoreCase);
            if (!sceneMatches)
            {
                yield return null;
                continue;
            }

            worldApplied = ReplicatedWorldStateRegistry.TryApplyJson(snapshotJson, out worldDiagnostic);
            if (worldApplied)
            {
                break;
            }

            yield return null;
        }

        if (!string.IsNullOrWhiteSpace(snapshotJson) && !worldApplied)
        {
            Debug.LogWarning($"WorldInteractionService: synchro partielle du monde pour le client local ({worldDiagnostic}).");
        }

        localSessionSyncInProgress = false;
        localSessionSynchronized = sceneMatches && worldApplied;
        localSessionSynchronizedScene = localSessionSynchronized ? sceneName : string.Empty;
        applyReplicatedWorldStateRoutine = null;
        LocalSessionSynchronizationCompleted?.Invoke();
    }

    private void StopApplyReplicatedWorldStateRoutine()
    {
        if (applyReplicatedWorldStateRoutine == null)
        {
            return;
        }

        StopCoroutine(applyReplicatedWorldStateRoutine);
        applyReplicatedWorldStateRoutine = null;
    }

    private void RepublishSpawnedObjectsToClient(ulong clientId)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || manager.SpawnManager == null)
        {
            return;
        }

        List<NetworkObject> visibleObjects = new List<NetworkObject>();
        List<NetworkObject> allObjects = new List<NetworkObject>();
        foreach (NetworkObject networkObject in manager.SpawnManager.SpawnedObjectsList)
        {
            if (networkObject == null || !networkObject.IsSpawned)
            {
                continue;
            }

            if (networkObject == NetworkObject)
            {
                continue;
            }

            allObjects.Add(networkObject);
            if (networkObject.IsNetworkVisibleTo(clientId))
            {
                visibleObjects.Add(networkObject);
            }
        }

        for (int i = 0; i < visibleObjects.Count; i++)
        {
            visibleObjects[i].NetworkHide(clientId);
        }

        for (int i = 0; i < allObjects.Count; i++)
        {
            allObjects[i].NetworkShow(clientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestReturnHomeServerRpc(uint triggerId, ServerRpcParams rpcParams = default)
    {
        if (!NetcodeTriggerRegistry.TryGetReturnHome(triggerId, out ReturnHomeTrigger trigger))
        {
            SendReturnHomeResultClientRpc(triggerId, (int)SquadManager.SendHomeResult.InvalidCharacter, NetcodeServerRpcValidation.BuildClientRpcParams(rpcParams));
            return;
        }

        if (!TryResolvePlayerCharacter(rpcParams, out GameObject character))
        {
            SendReturnHomeResultClientRpc(triggerId, (int)SquadManager.SendHomeResult.InvalidCharacter, NetcodeServerRpcValidation.BuildClientRpcParams(rpcParams));
            return;
        }

        if (character == null || !trigger.IsServerCharacterAllowed(character))
        {
            SendReturnHomeResultClientRpc(triggerId, (int)SquadManager.SendHomeResult.InvalidCharacter, NetcodeServerRpcValidation.BuildClientRpcParams(rpcParams));
            return;
        }

        SquadManager.SendHomeResult result = trigger.ServerTrySendHome(character);
        SendReturnHomeResultClientRpc(triggerId, (int)result, NetcodeServerRpcValidation.BuildClientRpcParams(rpcParams));
    }

    [ClientRpc]
    private void SendReturnHomeResultClientRpc(uint triggerId, int resultValue, ClientRpcParams rpcParams = default)
    {
        if (!NetcodeTriggerRegistry.TryGetReturnHome(triggerId, out ReturnHomeTrigger trigger))
        {
            return;
        }

        trigger.HandleReturnHomeResult((SquadManager.SendHomeResult)resultValue);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestHubSwapServerRpc(uint triggerId, ServerRpcParams rpcParams = default)
    {
        if (!NetcodeTriggerRegistry.TryGetHubSwap(triggerId, out HubCompanionSwapTrigger trigger))
        {
            SendHubSwapResultClientRpc(triggerId, false, NetcodeServerRpcValidation.BuildClientRpcParams(rpcParams));
            return;
        }

        if (!TryResolvePlayerCharacter(rpcParams, out GameObject character))
        {
            SendHubSwapResultClientRpc(triggerId, false, NetcodeServerRpcValidation.BuildClientRpcParams(rpcParams));
            return;
        }

        bool success = trigger.ServerTrySwap(character);
        SendHubSwapResultClientRpc(triggerId, success, NetcodeServerRpcValidation.BuildClientRpcParams(rpcParams));
    }

    [ClientRpc]
    private void SendHubSwapResultClientRpc(uint triggerId, bool success, ClientRpcParams rpcParams = default)
    {
        if (!NetcodeTriggerRegistry.TryGetHubSwap(triggerId, out HubCompanionSwapTrigger trigger))
        {
            return;
        }

        trigger.HandleSwapResult(success);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestLabyrinthStartServerRpc(uint triggerId, ServerRpcParams rpcParams = default)
    {
        if (!NetcodeTriggerRegistry.TryGetLabyrinth(triggerId, out LabyrinthStartTrigger trigger))
        {
            return;
        }

        if (!TryResolvePlayerCharacter(rpcParams, out GameObject character))
        {
            return;
        }

        if (!trigger.IsServerCharacterAllowed(character))
        {
            return;
        }

        trigger.ServerStartLabyrinth();
        LabyrinthStartedClientRpc(triggerId);
    }

    [ClientRpc]
    private void LabyrinthStartedClientRpc(uint triggerId)
    {
        if (!NetcodeTriggerRegistry.TryGetLabyrinth(triggerId, out LabyrinthStartTrigger trigger))
        {
            return;
        }

        trigger.ClientHandleLabyrinthStarted();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestCharacterSwitchServerRpc(string characterId, ServerRpcParams rpcParams = default)
    {
        NetcodePlayerSpawner spawner = NetcodePlayerSpawner.Instance;
        if (spawner == null)
        {
            SendSwitchResultClientRpc(false, "Spawner manquant.", NetcodeServerRpcValidation.BuildClientRpcParams(rpcParams));
            return;
        }

        bool success = spawner.TrySwitchCharacter(rpcParams.Receive.SenderClientId, characterId, out string reason);
        SendSwitchResultClientRpc(success, reason, NetcodeServerRpcValidation.BuildClientRpcParams(rpcParams));
    }

    [ClientRpc]
    private void SendSwitchResultClientRpc(bool success, string reason, ClientRpcParams rpcParams = default)
    {
        if (success)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = "Impossible de changer de personnage.";
        }

        InfoBoxUI.TryShow(reason);
    }

    private bool TryResolvePlayerCharacter(ServerRpcParams rpcParams, out GameObject character)
    {
        if (!NetcodeServerRpcValidation.TryResolvePlayerContext(
                this,
                rpcParams,
                out NetcodeServerRpcValidation.PlayerContext context,
                out _,
                requireController: false,
                requireInventory: false))
        {
            character = null;
            return false;
        }

        character = context.PlayerObject;
        return character != null;
    }

    private static ClientRpcParams BuildClientRpcParams(ServerRpcParams rpcParams)
    {
        return NetcodeServerRpcValidation.BuildClientRpcParams(rpcParams);
    }

    private static ClientRpcParams BuildClientRpcParams(ulong clientId)
    {
        return NetcodeServerRpcValidation.BuildClientRpcParams(clientId);
    }
}
