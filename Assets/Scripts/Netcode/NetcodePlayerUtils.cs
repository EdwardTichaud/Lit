using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Utilitaires pour resoudre les objets joueurs Netcode.
public static class NetcodePlayerUtils
{
    public readonly struct CharacterControlState
    {
        public CharacterControlState(
            string characterId,
            bool hasNetworkObject,
            bool isSpawned,
            ulong ownerClientId,
            bool hasLocalClientId,
            ulong localClientId,
            bool hasAssignedClientId,
            ulong assignedClientId,
            bool isOwner,
            bool isLocalPlayerFlag,
            bool isControlledLocally,
            bool isAssignedPlayerCharacter,
            bool isPlayerControlled,
            bool playerInputEnabled,
            bool followerAgentEnabled,
            string authoritySource)
        {
            CharacterId = characterId ?? string.Empty;
            HasNetworkObject = hasNetworkObject;
            IsSpawned = isSpawned;
            OwnerClientId = ownerClientId;
            HasLocalClientId = hasLocalClientId;
            LocalClientId = localClientId;
            HasAssignedClientId = hasAssignedClientId;
            AssignedClientId = assignedClientId;
            IsOwner = isOwner;
            IsLocalPlayerFlag = isLocalPlayerFlag;
            IsControlledLocally = isControlledLocally;
            IsAssignedPlayerCharacter = isAssignedPlayerCharacter;
            IsPlayerControlled = isPlayerControlled;
            PlayerInputEnabled = playerInputEnabled;
            FollowerAgentEnabled = followerAgentEnabled;
            AuthoritySource = authoritySource ?? string.Empty;
        }

        public string CharacterId { get; }
        public bool HasNetworkObject { get; }
        public bool IsSpawned { get; }
        public ulong OwnerClientId { get; }
        public bool HasLocalClientId { get; }
        public ulong LocalClientId { get; }
        public bool HasAssignedClientId { get; }
        public ulong AssignedClientId { get; }
        public bool IsOwner { get; }
        public bool IsLocalPlayerFlag { get; }
        public bool IsControlledLocally { get; }
        public bool IsAssignedPlayerCharacter { get; }
        public bool IsPlayerControlled { get; }
        public bool PlayerInputEnabled { get; }
        public bool FollowerAgentEnabled { get; }
        public string AuthoritySource { get; }
    }

    private static readonly Dictionary<string, string> loggedControlStates = new Dictionary<string, string>();

    public static Transform GetPlayerTransform(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
        {
            return null;
        }

        WorldInteractionService service = WorldInteractionService.Instance;
        if (service != null && service.TryGetAssignedCharacterId(clientId, out string assignedId))
        {
            GameObject assigned = ResolveCharacterInstanceById(assignedId);
            if (assigned != null)
            {
                return assigned.transform;
            }
        }

        if (NetworkManager.Singleton.SpawnManager != null)
        {
            NetworkObject[] owned = NetworkManager.Singleton.SpawnManager.GetClientOwnedObjects(clientId);
            if (owned != null)
            {
                for (int i = 0; i < owned.Length; i++)
                {
                    NetworkObject obj = owned[i];
                    if (obj == null)
                    {
                        continue;
                    }

                    if (obj.GetComponent<SquadCharacterController>() != null)
                    {
                        return obj.transform;
                    }
                }
            }
        }

        if (NetworkManager.Singleton.ConnectedClients != null
            && NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
            && client != null
            && client.PlayerObject != null)
        {
            return client.PlayerObject.transform;
        }

        return null;
    }

    public static CharacterControlState ResolveCharacterControlState(GameObject character)
    {
        if (character == null)
        {
            return default;
        }

        NetworkManager manager = NetworkManager.Singleton;
        bool networked = manager != null && manager.IsListening;
        bool hasLocalClientId = networked;
        ulong localClientId = hasLocalClientId ? manager.LocalClientId : 0UL;

        NetworkObject networkObject = character.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            networkObject = character.GetComponentInParent<NetworkObject>();
        }

        bool hasNetworkObject = networkObject != null;
        bool isSpawned = hasNetworkObject && networkObject.IsSpawned;
        ulong ownerClientId = hasNetworkObject ? networkObject.OwnerClientId : 0UL;
        bool isOwner = hasNetworkObject && isSpawned && networkObject.IsOwner;

        NetcodeLocalPlayer localPlayer = character.GetComponent<NetcodeLocalPlayer>();
        bool isLocalPlayerFlag = localPlayer != null && localPlayer.IsLocalPlayer;

        Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
        bool isControlledLocally = IsSameOrRelatedTransform(character.transform, localRoot);

        string characterId = ResolveCharacterId(character);
        WorldInteractionService service = WorldInteractionService.Instance;
        ulong assignedClientId = 0UL;
        bool hasAssignedClientId = service != null
            && !string.IsNullOrWhiteSpace(characterId)
            && service.TryGetAssignedClientId(characterId, out assignedClientId);

        bool hasPlayerDriverComponents =
            character.GetComponent<NetcodeLocalPlayer>() != null ||
            character.GetComponent<NetworkCharacterInput>() != null ||
            character.GetComponent<NetcodeCharacterIdentity>() != null;

        bool ownerConnected = hasNetworkObject && networked && HasConnectedClient(manager, ownerClientId);
        bool isAssignedPlayerCharacter = hasAssignedClientId;
        bool isPlayerControlled = false;
        string authoritySource = "ai_or_unassigned";

        if (networked)
        {
            if (isAssignedPlayerCharacter)
            {
                isPlayerControlled = true;
                authoritySource = "assignment_registry";
            }
            else if (hasPlayerDriverComponents && isSpawned && ownerConnected && (ownerClientId != NetworkManager.ServerClientId || isControlledLocally))
            {
                isPlayerControlled = true;
                authoritySource = "network_owner_fallback";
            }
        }
        else if (isControlledLocally)
        {
            isPlayerControlled = true;
            authoritySource = "singleplayer_local_context";
        }

        bool playerInputEnabled =
            character.GetComponent<NetworkCharacterInput>() != null &&
            character.GetComponent<NetworkCharacterInput>().enabled &&
            hasNetworkObject &&
            isSpawned &&
            isOwner &&
            IsAssignedToLocalClient(characterId, manager, service);

        SquadFollowerAgent followerAgent = character.GetComponent<SquadFollowerAgent>();
        bool followerAgentEnabled = followerAgent != null && followerAgent.enabled;

        return new CharacterControlState(
            characterId,
            hasNetworkObject,
            isSpawned,
            ownerClientId,
            hasLocalClientId,
            localClientId,
            hasAssignedClientId,
            hasAssignedClientId ? assignedClientId : 0UL,
            isOwner,
            isLocalPlayerFlag,
            isControlledLocally,
            isAssignedPlayerCharacter,
            isPlayerControlled,
            playerInputEnabled,
            followerAgentEnabled,
            authoritySource);
    }

    public static bool ShouldUsePlayerControl(GameObject character, out CharacterControlState state)
    {
        state = ResolveCharacterControlState(character);
        return state.IsPlayerControlled;
    }

    public static bool IsFollowerSimulationActiveOnThisMachine()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager == null || !manager.IsListening || manager.IsServer;
    }

    public static bool IsWaitingSimulationActiveOnThisMachine()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager == null || !manager.IsListening || manager.IsServer;
    }

    public static void LogControlDecision(
        string system,
        GameObject character,
        bool followerAiEnabled,
        bool waitingPointEnabled,
        string movementMode,
        string reason)
    {
        if (character == null)
        {
            return;
        }

        CharacterControlState state = ResolveCharacterControlState(character);
        string resolvedMovementMode = string.IsNullOrWhiteSpace(movementMode)
            ? ResolveMovementMode(state, followerAiEnabled, waitingPointEnabled)
            : movementMode;
        string message =
            $"[NetcodeControl] system='{system}' path='{DescribeTransform(character.transform)}' characterId='{state.CharacterId}' ownerClientId={FormatClientId(state.HasNetworkObject, state.OwnerClientId)} localClientId={FormatClientId(state.HasLocalClientId, state.LocalClientId)} assignedClientId={FormatClientId(state.HasAssignedClientId, state.AssignedClientId)} isOwner={state.IsOwner} isLocalPlayer={state.IsLocalPlayerFlag} isControlledLocally={state.IsControlledLocally} isPlayerControlled={state.IsPlayerControlled} isAssignedPlayer={state.IsAssignedPlayerCharacter} followerAiEnabled={followerAiEnabled} waitingPointEnabled={waitingPointEnabled} followerAgentEnabled={state.FollowerAgentEnabled} playerInputEnabled={state.PlayerInputEnabled} movementMode='{resolvedMovementMode}' authoritySource='{state.AuthoritySource}' reason='{reason}'";

        string key = $"{system}:{character.GetInstanceID()}";
        if (loggedControlStates.TryGetValue(key, out string previous) && previous == message)
        {
            return;
        }

        loggedControlStates[key] = message;
        Debug.Log(message, character);
    }

    public static string GetTransformPath(Transform target)
    {
        return DescribeTransform(target);
    }

    private static GameObject ResolveCharacterInstanceById(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return null;
        }

        SquadManager manager = SquadManager.Instance;
        if (manager != null && manager.currentSquad != null)
        {
            for (int i = 0; i < manager.currentSquad.Count; i++)
            {
                CharacterData character = manager.currentSquad[i];
                if (character == null)
                {
                    continue;
                }

                if (GetCharacterId(character) == characterId)
                {
                    return manager.GetCharacterInstance(character);
                }
            }
        }

#if UNITY_2023_1_OR_NEWER
        SquadCharacterController[] controllers = UnityEngine.Object.FindObjectsByType<SquadCharacterController>(UnityEngine.FindObjectsSortMode.None);
#else
        SquadCharacterController[] controllers = UnityEngine.Object.FindObjectsOfType<SquadCharacterController>();
#endif
        for (int i = 0; i < controllers.Length; i++)
        {
            SquadCharacterController controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            if (NetcodeCharacterIdentity.MatchesCharacterId(controller.gameObject, characterId))
            {
                return controller.gameObject;
            }
        }

        return null;
    }

    private static string GetCharacterId(CharacterData character)
    {
        return NetcodeCharacterIdentity.GetCharacterId(character);
    }

    private static string ResolveCharacterId(GameObject character)
    {
        if (character == null)
        {
            return string.Empty;
        }

        NetcodeCharacterIdentity identity = character.GetComponent<NetcodeCharacterIdentity>();
        if (identity != null && !string.IsNullOrWhiteSpace(identity.CharacterId))
        {
            return identity.CharacterId;
        }

        SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            controller = character.GetComponentInChildren<SquadCharacterController>(true);
        }

        return controller != null ? GetCharacterId(controller.CharacterData) : string.Empty;
    }

    private static bool IsAssignedToLocalClient(string characterId, NetworkManager manager, WorldInteractionService service)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        if (manager == null || !manager.IsListening)
        {
            return true;
        }

        if (service == null)
        {
            return false;
        }

        if (!service.TryGetAssignedCharacterId(manager.LocalClientId, out string localCharacterId))
        {
            return false;
        }

        return string.Equals(localCharacterId, characterId, System.StringComparison.Ordinal);
    }

    private static bool IsSameOrRelatedTransform(Transform character, Transform candidate)
    {
        if (character == null || candidate == null)
        {
            return false;
        }

        return character == candidate || character.IsChildOf(candidate) || candidate.IsChildOf(character);
    }

    private static bool HasConnectedClient(NetworkManager manager, ulong clientId)
    {
        if (manager == null)
        {
            return false;
        }

        IReadOnlyList<ulong> connectedClients = manager.ConnectedClientsIds;
        if (connectedClients == null)
        {
            return false;
        }

        for (int i = 0; i < connectedClients.Count; i++)
        {
            if (connectedClients[i] == clientId)
            {
                return true;
            }
        }

        return false;
    }

    public static string ResolveMovementMode(CharacterControlState state, bool followerAiEnabled, bool waitingPointEnabled)
    {
        if (state.PlayerInputEnabled)
        {
            return "local_player_input";
        }

        if (waitingPointEnabled)
        {
            return "waiting_point";
        }

        if (followerAiEnabled)
        {
            return "follower_ai";
        }

        if (state.IsPlayerControlled)
        {
            return state.IsControlledLocally ? "local_player_observer" : "remote_player_replica";
        }

        if (IsFollowerSimulationActiveOnThisMachine() || IsWaitingSimulationActiveOnThisMachine())
        {
            return "ai_candidate";
        }

        return "observer";
    }

    public static string ResolveAnimationDriverMode(CharacterControlState state)
    {
        if (state.PlayerInputEnabled)
        {
            return "local";
        }

        if (state.IsPlayerControlled)
        {
            return "remote";
        }

        if (IsFollowerSimulationActiveOnThisMachine() || IsWaitingSimulationActiveOnThisMachine())
        {
            return "ai";
        }

        return "observer";
    }

    private static string DescribeTransform(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }

        string sceneName = target.gameObject.scene.IsValid()
            ? target.gameObject.scene.name
            : "NoScene";
        return $"{sceneName}:{path}";
    }

    private static string FormatClientId(bool hasValue, ulong value)
    {
        return hasValue ? value.ToString() : "n/a";
    }
}
