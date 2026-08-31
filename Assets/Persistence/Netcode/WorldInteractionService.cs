using System;
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
    public event Action AssignmentsChanged;
    public event Action ActiveSceneChanged;

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
        if (IsServer)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            UpdateActiveSceneName(SceneManager.GetActiveScene().name);
        }
        AssignmentsChanged?.Invoke();
        ActiveSceneChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        assignments.OnListChanged -= OnAssignmentsChanged;
        activeSceneName.OnValueChanged -= OnActiveSceneNameChanged;
        if (IsServer)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        if (assignments != null && IsServer)
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
        ActiveSceneChanged?.Invoke();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateActiveSceneName(scene.name);
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

    [ServerRpc(RequireOwnership = false)]
    public void RequestReturnHomeServerRpc(uint triggerId, ServerRpcParams rpcParams = default)
    {
        if (!NetcodeTriggerRegistry.TryGetReturnHome(triggerId, out ReturnHomeTrigger trigger))
        {
            SendReturnHomeResultClientRpc(triggerId, (int)SquadManager.SendHomeResult.InvalidCharacter, BuildClientRpcParams(rpcParams));
            return;
        }

        GameObject character = ResolvePlayerCharacter(rpcParams);
        if (character == null || !trigger.IsServerCharacterAllowed(character))
        {
            SendReturnHomeResultClientRpc(triggerId, (int)SquadManager.SendHomeResult.InvalidCharacter, BuildClientRpcParams(rpcParams));
            return;
        }

        SquadManager.SendHomeResult result = trigger.ServerTrySendHome(character);
        SendReturnHomeResultClientRpc(triggerId, (int)result, BuildClientRpcParams(rpcParams));
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
            SendHubSwapResultClientRpc(triggerId, false, BuildClientRpcParams(rpcParams));
            return;
        }

        GameObject character = ResolvePlayerCharacter(rpcParams);
        bool success = trigger.ServerTrySwap(character);
        SendHubSwapResultClientRpc(triggerId, success, BuildClientRpcParams(rpcParams));
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

        GameObject character = ResolvePlayerCharacter(rpcParams);
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
    public void RequestPortalUseServerRpc(uint triggerId, ServerRpcParams rpcParams = default)
    {
        if (!NetcodeTriggerRegistry.TryGetPortal(triggerId, out PortalController portal))
        {
            SendPortalUseResultClientRpc(
                triggerId,
                false,
                Vector3.zero,
                Quaternion.identity,
                false,
                BuildClientRpcParams(rpcParams));
            return;
        }

        GameObject character = ResolvePlayerCharacter(rpcParams);
        Vector3 destinationPosition = Vector3.zero;
        Quaternion destinationRotation = Quaternion.identity;
        bool success = character != null &&
                       portal.ServerTryUse(character, out destinationPosition, out destinationRotation);
        SendPortalUseResultClientRpc(
            triggerId,
            success,
            destinationPosition,
            destinationRotation,
            portal.IsSceneTransition,
            BuildClientRpcParams(rpcParams));
    }

    [ClientRpc]
    private void SendPortalUseResultClientRpc(
        uint triggerId,
        bool success,
        Vector3 destinationPosition,
        Quaternion destinationRotation,
        bool sceneTransition,
        ClientRpcParams rpcParams = default)
    {
        if (!NetcodeTriggerRegistry.TryGetPortal(triggerId, out PortalController portal))
        {
            return;
        }

        portal.HandlePortalUseResult(success, destinationPosition, destinationRotation, sceneTransition);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestLadderPassageServerRpc(uint ladderId, int sourceEndpoint, ServerRpcParams rpcParams = default)
    {
        if (!NetcodeTriggerRegistry.TryGetLadder(ladderId, out LadderController ladder))
        {
            SendLadderPassageResultClientRpc(ladderId, false, sourceEndpoint, Vector3.zero, Quaternion.identity, BuildClientRpcParams(rpcParams));
            return;
        }

        GameObject character = ResolvePlayerCharacter(rpcParams);
        bool success = ladder.ServerTryBeginPassage(character, sourceEndpoint, out Vector3 destination, out Quaternion rotation);
        SendLadderPassageResultClientRpc(ladderId, success, sourceEndpoint, destination, rotation, BuildClientRpcParams(rpcParams));
    }

    [ClientRpc]
    private void SendLadderPassageResultClientRpc(uint ladderId, bool success, int sourceEndpoint, Vector3 destination, Quaternion rotation, ClientRpcParams rpcParams = default)
    {
        if (NetcodeTriggerRegistry.TryGetLadder(ladderId, out LadderController ladder))
            ladder.HandlePassageResult(success, sourceEndpoint, destination, rotation);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestCharacterSwitchServerRpc(string characterId, ServerRpcParams rpcParams = default)
    {
        NetcodePlayerSpawner spawner = NetcodePlayerSpawner.Instance;
        if (spawner == null)
        {
            SendSwitchResultClientRpc(false, "Spawner manquant.", BuildClientRpcParams(rpcParams));
            return;
        }

        bool success = spawner.TrySwitchCharacter(rpcParams.Receive.SenderClientId, characterId, out string reason);
        SendSwitchResultClientRpc(success, reason, BuildClientRpcParams(rpcParams));
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

    private static GameObject ResolvePlayerCharacter(ServerRpcParams rpcParams)
    {
        Transform playerRoot = NetcodePlayerUtils.GetPlayerTransform(rpcParams.Receive.SenderClientId);
        return playerRoot != null ? playerRoot.gameObject : null;
    }

    private static ClientRpcParams BuildClientRpcParams(ServerRpcParams rpcParams)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { rpcParams.Receive.SenderClientId }
            }
        };
    }
}
