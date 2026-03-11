using System;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Spawner serveur pour attribuer un personnage a chaque client.
public class NetcodePlayerSpawner : MonoBehaviour
{
    public static NetcodePlayerSpawner Instance { get; private set; }

    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private bool spawnWorldInteractionService = true;

    private readonly Dictionary<ulong, CharacterData> assignments = new Dictionary<ulong, CharacterData>();
    private readonly HashSet<int> usedRosterIndices = new HashSet<int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnServerStarted()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        assignments.Clear();
        usedRosterIndices.Clear();

        NetcodePrefabRegistry.EnsureInitialized();
        if (spawnWorldInteractionService)
        {
            SpawnWorldInteractionService();
        }

        if (WorldInteractionService.Instance != null)
        {
            WorldInteractionService.Instance.ClearAllAssignments();
        }

        SpawnSessionCharacters();
        SpawnForClient(NetworkManager.Singleton.LocalClientId);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        SpawnForClient(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (assignments.TryGetValue(clientId, out CharacterData character))
        {
            assignments.Remove(clientId);
            ReleaseRosterIndex(character);
            ReleaseCharacterOwnership(character);
        }

        NetcodePlayerSessionRegistry.Unregister(clientId);
        UpdateAssignmentRegistry(clientId, null);
    }

    private void SpawnForClient(ulong clientId)
    {
        NetcodePrefabRegistry.EnsureInitialized();
        PruneAssignments();
        if (assignments.ContainsKey(clientId))
        {
            return;
        }

        CharacterData character = ResolveCharacterForClient(clientId);
        if (character == null || character.model == null)
        {
            Debug.LogWarning("NetcodePlayerSpawner: aucun personnage disponible pour ce client.");
            return;
        }

        if (!TryEnsureNetworkCharacter(character, out GameObject instance, out NetworkObject networkObject))
        {
            return;
        }

        SquadCharacterController controller = instance.GetComponent<SquadCharacterController>();
        if (controller != null)
        {
            controller.BindCharacterData(character, true);
            EnsureStarterInventoryIfEmpty(controller, character);
        }

        NetcodeCharacterIdentity identity = NetcodeRuntimeUtilities.GetOrAdd<NetcodeCharacterIdentity>(instance);
        if (identity != null)
        {
            identity.SetCharacter(character);
        }

        assignments[clientId] = character;
        if (!networkObject.IsSpawned)
        {
            networkObject.Spawn(true);
        }

        if (networkObject.OwnerClientId != clientId)
        {
            networkObject.ChangeOwnership(clientId);
        }

        NetworkInventory inventory = instance.GetComponent<NetworkInventory>();
        if (inventory != null && inventory.IsServer)
        {
            inventory.SyncFromController();
        }

        RegisterWithSquadManager(character, instance);
        RegisterPlayerBinding(clientId, character);
        UpdateAssignmentRegistry(clientId, character);
    }

    private static void EnsureStarterInventoryIfEmpty(SquadCharacterController controller, CharacterData character)
    {
        if (controller == null || character == null)
        {
            return;
        }

        if (character.starterItemsWithQuantity == null || character.starterItemsWithQuantity.Count == 0)
        {
            return;
        }

        IReadOnlyList<Item> items = controller.Items;
        if (items != null && items.Count > 0)
        {
            return;
        }

        if (controller.TorchSecondsRemaining > 0 || controller.IsTorchEquipped)
        {
            return;
        }

        if (HasSaveFile())
        {
            return;
        }

        controller.ApplyStarterItems(character, true);
    }

    private static bool HasSaveFile()
    {
        CharacterStateStore store = ResolveCharacterStateStore();
        if (store != null)
        {
            return store.HasSaveFile;
        }

        SaveSessionManager session = SaveSessionManager.Instance;
        if (session != null && session.HasActiveSave)
        {
            string path = session.GetActiveSaveFilePath("CharacterState.json");
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        return false;
    }

    public bool TrySwitchCharacter(ulong clientId, string targetCharacterId, out string reason)
    {
        reason = string.Empty;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            reason = "Serveur requis.";
            return false;
        }

        PruneAssignments();

        if (string.IsNullOrWhiteSpace(targetCharacterId))
        {
            reason = "Personnage invalide.";
            return false;
        }

        CharacterData target = ResolveCharacterById(targetCharacterId);
        if (target == null)
        {
            reason = "Personnage introuvable.";
            return false;
        }

        if (assignments.TryGetValue(clientId, out CharacterData current) && current == target)
        {
            reason = "Deja controle.";
            return false;
        }

        if (IsCharacterAssigned(target))
        {
            reason = "Personnage deja controle.";
            return false;
        }

        GameObject targetInstance = TryResolveExistingInstance(target);
        if (targetInstance == null)
        {
            reason = "Instance introuvable.";
            return false;
        }

        PrepareExistingInstanceForNetwork(targetInstance, target);
        NetworkObject targetNetwork = targetInstance.GetComponent<NetworkObject>();
        if (targetNetwork == null)
        {
            reason = "Objet reseau manquant.";
            return false;
        }

        if (!targetNetwork.IsSpawned)
        {
            targetNetwork.Spawn(true);
        }

        if (assignments.TryGetValue(clientId, out CharacterData previous))
        {
            GameObject previousInstance = TryResolveExistingInstance(previous);
            if (previousInstance != null)
            {
                NetworkObject previousNetwork = previousInstance.GetComponent<NetworkObject>();
                if (previousNetwork != null && previousNetwork.IsSpawned)
                {
                    previousNetwork.RemoveOwnership();
                }
            }

            ReleaseRosterIndex(previous);
        }

        assignments[clientId] = target;
        int index = GetRosterIndex(target, GetRoster());
        if (index >= 0)
        {
            usedRosterIndices.Add(index);
        }

        NetcodeCharacterIdentity targetIdentity = NetcodeRuntimeUtilities.GetOrAdd<NetcodeCharacterIdentity>(targetInstance);
        if (targetIdentity != null)
        {
            targetIdentity.SetCharacter(target);
        }

        UpdateAssignmentRegistry(clientId, target);
        targetNetwork.ChangeOwnership(clientId);
        RegisterWithSquadManager(target, targetInstance);
        RegisterPlayerBinding(clientId, target);
        return true;
    }

    private static GameObject TryResolveExistingInstance(CharacterData character)
    {
        if (character == null)
        {
            return null;
        }

        SquadManager manager = SquadManager.Instance;
        if (manager != null)
        {
            GameObject found = manager.GetCharacterInstance(character);
            if (found != null)
            {
                return found;
            }
        }

#if UNITY_2023_1_OR_NEWER
        SquadCharacterController[] controllers = FindObjectsByType<SquadCharacterController>(FindObjectsSortMode.None);
#else
        SquadCharacterController[] controllers = UnityEngine.Object.FindObjectsOfType<SquadCharacterController>();
#endif
        if (controllers == null)
        {
            return null;
        }

        string targetId = GetCharacterId(character);
        for (int i = 0; i < controllers.Length; i++)
        {
            SquadCharacterController controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            CharacterData candidate = controller.CharacterData;
            if (candidate == null)
            {
                continue;
            }

            if (candidate == character)
            {
                return controller.gameObject;
            }

            string candidateId = GetCharacterId(candidate);
            if (!string.IsNullOrWhiteSpace(targetId)
                && string.Equals(targetId, candidateId, StringComparison.Ordinal))
            {
                return controller.gameObject;
            }
        }

        return null;
    }

    private static void PrepareExistingInstanceForNetwork(GameObject instance, CharacterData character)
    {
        if (instance == null)
        {
            return;
        }

        NetcodeRuntimeUtilities.ConfigureCharacterNetworkComponents(instance);
        NetworkObject networkObject = NetcodeRuntimeUtilities.GetOrAdd<NetworkObject>(instance);
        if (networkObject.IsSpawned)
        {
            return;
        }

        uint hash = NetcodePrefabRegistry.GetCharacterPrefabHash(character);
        if (hash == 0u)
        {
            hash = NetcodeStableHash.Hash32($"character:{GetCharacterId(character)}");
        }

        // Les avatars de squad deja presents dans la scene sont quand meme des instances runtime.
        // Si NGO les considere comme des scene objects, un client tardif ne peut pas les recreer.
        networkObject.SetSceneObjectStatus(false);
        NetcodeRuntimeUtilities.EnsureNetworkObjectHash(
            networkObject,
            hash,
            $"character:{GetCharacterId(character)}:existing-instance");
    }

    private bool TryEnsureNetworkCharacter(CharacterData character, out GameObject instance, out NetworkObject networkObject)
    {
        instance = TryResolveExistingInstance(character);
        if (instance != null)
        {
            PrepareExistingInstanceForNetwork(instance, character);
        }
        else
        {
            Transform parent = SquadManager.Instance != null ? SquadManager.Instance.squadCharactersParent : null;
            Vector3 position = ResolveSpawnPosition(character);
            Quaternion rotation = ResolveSpawnRotation(character);

            instance = NetcodePrefabRegistry.SpawnCharacterInstance(character, position, rotation, parent);
            if (instance == null)
            {
                Debug.LogWarning($"NetcodePlayerSpawner: prefab reseau manquant pour {character.name}.");
                networkObject = null;
                return false;
            }
        }

        networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogWarning("NetcodePlayerSpawner: NetworkObject manquant sur le prefab reseau.");
            Destroy(instance);
            return false;
        }

        if (!networkObject.IsSpawned)
        {
            networkObject.Spawn(true);
        }

        return true;
    }

    private void SpawnWorldInteractionService()
    {
        if (WorldInteractionService.Instance != null && WorldInteractionService.Instance.IsSpawned)
        {
            return;
        }

        GameObject instance = NetcodePrefabRegistry.SpawnWorldInteractionServiceInstance();
        if (instance == null)
        {
            return;
        }

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject != null && !networkObject.IsSpawned)
        {
            networkObject.Spawn(true);
        }
    }

    private CharacterData ResolveCharacterForClient(ulong clientId)
    {
        if (assignments.Count >= Mathf.Max(1, maxPlayers))
        {
            return null;
        }

        List<CharacterData> roster = GetRoster();
        if (roster == null || roster.Count == 0)
        {
            return null;
        }

        CharacterData preferred = ResolvePreferredCharacter(clientId, roster);
        if (preferred != null && !assignments.ContainsValue(preferred))
        {
            int preferredIndex = roster.IndexOf(preferred);
            if (preferredIndex >= 0)
            {
                usedRosterIndices.Add(preferredIndex);
            }

            return preferred;
        }

        int index = GetRandomAvailableRosterIndex(roster.Count);
        if (index < 0 || index >= roster.Count)
        {
            index = 0;
        }

        CharacterData character = roster[index];
        if (character != null)
        {
            usedRosterIndices.Add(index);
        }

        return character;
    }

    public void ResynchronizeClientState(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        SpawnSessionCharacters();
        PruneAssignments();
        if (!assignments.TryGetValue(clientId, out CharacterData character) || character == null)
        {
            SpawnForClient(clientId);
            return;
        }

        if (!TryEnsureNetworkCharacter(character, out GameObject instance, out NetworkObject networkObject))
        {
            return;
        }

        if (networkObject.OwnerClientId != clientId)
        {
            networkObject.ChangeOwnership(clientId);
        }

        NetcodeCharacterIdentity identity = NetcodeRuntimeUtilities.GetOrAdd<NetcodeCharacterIdentity>(instance);
        if (identity != null)
        {
            identity.SetCharacter(character);
        }

        NetworkInventory inventory = instance.GetComponent<NetworkInventory>();
        if (inventory != null && inventory.IsServer)
        {
            inventory.SyncFromController();
        }

        RegisterWithSquadManager(character, instance);
        RegisterPlayerBinding(clientId, character);
        UpdateAssignmentRegistry(clientId, character);
    }

    private void SpawnSessionCharacters()
    {
        SquadManager manager = SquadManager.Instance;
        if (manager == null || manager.currentSquad == null || manager.currentSquad.Count == 0)
        {
            return;
        }

        for (int i = 0; i < manager.currentSquad.Count; i++)
        {
            CharacterData character = manager.currentSquad[i];
            if (character == null)
            {
                continue;
            }

            if (!TryEnsureNetworkCharacter(character, out GameObject instance, out NetworkObject networkObject))
            {
                continue;
            }

            RegisterWithSquadManager(character, instance);

            SquadCharacterController controller = instance.GetComponent<SquadCharacterController>();
            if (controller != null)
            {
                controller.BindCharacterData(character, true);
                EnsureStarterInventoryIfEmpty(controller, character);
            }

            NetcodeCharacterIdentity identity = NetcodeRuntimeUtilities.GetOrAdd<NetcodeCharacterIdentity>(instance);
            if (identity != null)
            {
                identity.SetCharacter(character);
            }

            if (!assignments.ContainsValue(character) && networkObject.OwnerClientId != NetworkManager.ServerClientId)
            {
                networkObject.RemoveOwnership();
            }

            NetworkInventory inventory = instance.GetComponent<NetworkInventory>();
            if (inventory != null && inventory.IsServer)
            {
                inventory.SyncFromController();
            }
        }
    }

    private CharacterData ResolvePreferredCharacter(ulong clientId, List<CharacterData> roster)
    {
        if (roster == null || roster.Count == 0)
        {
            return null;
        }

        if (!NetcodePlayerSessionRegistry.TryGetPlayerId(clientId, out string playerId))
        {
            return null;
        }

        CharacterStateStore store = ResolveCharacterStateStore();
        if (store == null || !store.TryGetBoundCharacterId(playerId, out string characterId))
        {
            return null;
        }

        for (int i = 0; i < roster.Count; i++)
        {
            CharacterData candidate = roster[i];
            if (candidate == null)
            {
                continue;
            }

            if (GetCharacterId(candidate) == characterId)
            {
                return candidate;
            }
        }

        return null;
    }

    private void RegisterPlayerBinding(ulong clientId, CharacterData character)
    {
        if (character == null)
        {
            return;
        }

        if (!NetcodePlayerSessionRegistry.TryGetPlayerId(clientId, out string playerId))
        {
            return;
        }

        CharacterStateStore store = ResolveCharacterStateStore();
        if (store == null)
        {
            return;
        }

        string characterId = GetCharacterId(character);
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return;
        }

        store.SetPlayerBinding(playerId, characterId);
    }

    private int GetRandomAvailableRosterIndex(int rosterCount)
    {
        if (rosterCount <= 0)
        {
            return -1;
        }

        List<int> available = new List<int>();
        for (int i = 0; i < rosterCount; i++)
        {
            if (!usedRosterIndices.Contains(i))
            {
                available.Add(i);
            }
        }

        if (available.Count > 0)
        {
            return available[UnityEngine.Random.Range(0, available.Count)];
        }

        return 0;
    }

    private void ReleaseRosterIndex(CharacterData character)
    {
        if (character == null)
        {
            return;
        }

        List<CharacterData> roster = GetRoster();
        if (roster == null)
        {
            return;
        }

        int index = roster.IndexOf(character);
        if (index >= 0)
        {
            usedRosterIndices.Remove(index);
        }
    }

    private List<CharacterData> GetRoster()
    {
        SquadManager manager = SquadManager.Instance;
        if (manager != null && manager.currentSquad != null && manager.currentSquad.Count > 0)
        {
            return manager.currentSquad;
        }

        return new List<CharacterData>(Resources.FindObjectsOfTypeAll<CharacterData>());
    }

    private Vector3 ResolveSpawnPosition(CharacterData character)
    {
        SquadManager manager = SquadManager.Instance;
        if (manager != null && manager.squadSpawnPoints != null)
        {
            int index = GetRosterIndex(character, manager.currentSquad);
            if (index >= 0 && index < manager.squadSpawnPoints.Count)
            {
                Transform point = manager.squadSpawnPoints[index];
                if (point != null)
                {
                    return point.position;
                }
            }
        }

        Vector3 origin = manager != null && manager.squadSpawnOrigin != null ? manager.squadSpawnOrigin.position : Vector3.zero;
        int fallbackIndex = GetRosterIndex(character, manager != null ? manager.currentSquad : null);
        return origin + GetFallbackOffset(fallbackIndex);
    }

    private Quaternion ResolveSpawnRotation(CharacterData character)
    {
        SquadManager manager = SquadManager.Instance;
        if (manager != null && manager.squadSpawnPoints != null)
        {
            int index = GetRosterIndex(character, manager.currentSquad);
            if (index >= 0 && index < manager.squadSpawnPoints.Count)
            {
                Transform point = manager.squadSpawnPoints[index];
                if (point != null)
                {
                    return point.rotation;
                }
            }
        }

        return manager != null ? manager.transform.rotation : Quaternion.identity;
    }

    private static int GetRosterIndex(CharacterData character, List<CharacterData> roster)
    {
        if (character == null || roster == null)
        {
            return -1;
        }

        return roster.IndexOf(character);
    }

    private static Vector3 GetFallbackOffset(int index)
    {
        Vector3[] offsets =
        {
            Vector3.zero,
            new Vector3(2f, 0f, 0f),
            new Vector3(-2f, 0f, 0f),
            new Vector3(0f, 0f, 2f)
        };

        if (index < 0)
        {
            index = 0;
        }

        return offsets[index % offsets.Length];
    }

    private static void RegisterWithSquadManager(CharacterData character, GameObject instance)
    {
        SquadManager manager = SquadManager.Instance;
        if (manager == null || character == null || instance == null)
        {
            return;
        }

        manager.RegisterNetworkCharacter(character, instance);
    }

    private static void UpdateAssignmentRegistry(ulong clientId, CharacterData character)
    {
        if (WorldInteractionService.Instance == null || !WorldInteractionService.Instance.IsServer)
        {
            return;
        }

        if (character == null)
        {
            WorldInteractionService.Instance.ClearAssignment(clientId);
        }
        else
        {
            WorldInteractionService.Instance.SetAssignment(clientId, GetCharacterId(character));
        }
    }

    private static CharacterStateStore ResolveCharacterStateStore()
    {
        if (CharacterStateStore.Instance != null)
        {
            return CharacterStateStore.Instance;
        }

#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<CharacterStateStore>();
#else
        return UnityEngine.Object.FindObjectOfType<CharacterStateStore>();
#endif
    }

    private static string GetCharacterId(CharacterData character)
    {
        if (character == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(character.UniqueId))
        {
            return character.UniqueId;
        }

        if (!string.IsNullOrWhiteSpace(character.characterId))
        {
            return character.characterId;
        }

        if (!string.IsNullOrWhiteSpace(character.characterName))
        {
            return character.characterName;
        }

        return character.name;
    }

    private CharacterData ResolveCharacterById(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return null;
        }

        List<CharacterData> roster = GetRoster();
        if (roster == null)
        {
            return null;
        }

        for (int i = 0; i < roster.Count; i++)
        {
            CharacterData candidate = roster[i];
            if (candidate == null)
            {
                continue;
            }

            if (GetCharacterId(candidate) == characterId)
            {
                return candidate;
            }
        }

        return null;
    }

    private bool IsCharacterAssigned(CharacterData character)
    {
        if (character == null)
        {
            return false;
        }

        string targetId = GetCharacterId(character);
        if (!string.IsNullOrWhiteSpace(targetId))
        {
            WorldInteractionService service = WorldInteractionService.Instance;
            if (service != null && service.IsSpawned && service.TryGetAssignedClientId(targetId, out ulong assignedClientId))
            {
                if (NetworkManager.Singleton != null)
                {
                    IReadOnlyList<ulong> connected = NetworkManager.Singleton.ConnectedClientsIds;
                    if (connected != null)
                    {
                        for (int i = 0; i < connected.Count; i++)
                        {
                            if (connected[i] == assignedClientId)
                            {
                                return true;
                            }
                        }
                    }
                }

                // Assignment stale: clear it on server and locally.
                service.ClearAssignment(assignedClientId);
                if (assignments.TryGetValue(assignedClientId, out CharacterData assignedCharacter))
                {
                    ReleaseRosterIndex(assignedCharacter);
                    assignments.Remove(assignedClientId);
                }
                UpdateAssignmentRegistry(assignedClientId, null);
            }
        }

        foreach (KeyValuePair<ulong, CharacterData> pair in assignments)
        {
            if (pair.Value == null)
            {
                continue;
            }

            if (GetCharacterId(pair.Value) == targetId)
            {
                return true;
            }
        }

        return false;
    }

    private void PruneAssignments()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        IReadOnlyList<ulong> connected = NetworkManager.Singleton.ConnectedClientsIds;
        if (connected == null || connected.Count == 0)
        {
            return;
        }

        HashSet<ulong> connectedSet = new HashSet<ulong>(connected);
        List<ulong> toRemove = null;

        foreach (KeyValuePair<ulong, CharacterData> pair in assignments)
        {
            if (connectedSet.Contains(pair.Key))
            {
                continue;
            }

            if (toRemove == null)
            {
                toRemove = new List<ulong>();
            }

            toRemove.Add(pair.Key);
            ReleaseRosterIndex(pair.Value);
        }

        if (toRemove == null)
        {
            return;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            ulong clientId = toRemove[i];
            if (assignments.TryGetValue(clientId, out CharacterData character))
            {
                ReleaseCharacterOwnership(character);
            }
            assignments.Remove(clientId);
            UpdateAssignmentRegistry(clientId, null);
        }
    }

    private static void ReleaseCharacterOwnership(CharacterData character)
    {
        GameObject instance = TryResolveExistingInstance(character);
        if (instance == null)
        {
            return;
        }

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject == null || !networkObject.IsSpawned)
        {
            return;
        }

        if (networkObject.OwnerClientId != NetworkManager.ServerClientId)
        {
            networkObject.RemoveOwnership();
        }
    }
}
