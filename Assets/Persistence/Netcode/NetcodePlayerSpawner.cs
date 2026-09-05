using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

// Spawner serveur pour attribuer un personnage a chaque client.
public class NetcodePlayerSpawner : MonoBehaviour
{
    public static NetcodePlayerSpawner Instance { get; private set; }

    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private bool spawnWorldInteractionService = true;

    private readonly Dictionary<ulong, CharacterData> assignments = new Dictionary<ulong, CharacterData>();
    private readonly HashSet<int> usedRosterIndices = new HashSet<int>();
    private Coroutine deferredSpawnRoutine;

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
        SceneManager.sceneLoaded += OnSceneLoaded;
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
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    public void ResetRuntimeState(string reason = null)
    {
        assignments.Clear();
        usedRosterIndices.Clear();
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

        // Le host Relay demarre volontairement depuis MainMenu. Ne pas creer
        // son personnage ici : l'escouade de Maison n'existe pas encore et
        // l'objet serait detruit au changement de scene, tout en laissant une
        // attribution fantome qui bloque ensuite les controles.
        RequestSpawnWhenGameplaySceneIsReady();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        RequestSpawnWhenGameplaySceneIsReady();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (assignments.TryGetValue(clientId, out CharacterData character))
        {
            assignments.Remove(clientId);
            ReleaseRosterIndex(character);
        }

        NetcodePlayerSessionRegistry.Unregister(clientId);
        UpdateAssignmentRegistry(clientId, null);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        RequestSpawnWhenGameplaySceneIsReady();
    }

    private void RequestSpawnWhenGameplaySceneIsReady()
    {
        if (deferredSpawnRoutine == null)
        {
            deferredSpawnRoutine = StartCoroutine(SpawnConnectedClientsWhenGameplaySceneIsReady());
        }
    }

    private System.Collections.IEnumerator SpawnConnectedClientsWhenGameplaySceneIsReady()
    {
        while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            if (IsGameplayRosterReady())
            {
                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null && manager.IsServer)
                {
                    ulong[] clientIds = manager.ConnectedClientsIds.ToArray();
                    for (int i = 0; i < clientIds.Length; i++)
                    {
                        SpawnForClient(clientIds[i]);
                    }
                }

                deferredSpawnRoutine = null;
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.1f);
        }

        deferredSpawnRoutine = null;
        Debug.LogWarning("NetcodePlayerSpawner: l'escouade de Maison n'a pas ete preparee a temps; aucun personnage n'a ete attribue.");
    }

    private static bool IsGameplayRosterReady()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer)
        {
            return false;
        }

        if (GameFlowService.IsPreparingGameplayScene || GameFlowService.Instance == null || !GameFlowService.Instance.HasGameplaySession)
        {
            return false;
        }

        string gameplaySceneName = GameFlowService.InitialGameplaySceneName;
        if (string.IsNullOrWhiteSpace(gameplaySceneName) || !SceneManager.GetSceneByName(gameplaySceneName).isLoaded)
        {
            return false;
        }

        SquadManager squad = SquadManager.Instance;
        return squad != null && squad.currentSquad != null && squad.currentSquad.Any(character => character != null && character.worldPrefab != null);
    }

    private void SpawnForClient(ulong clientId)
    {
        PrivateSessionService session = PrivateSessionService.Instance;
        if (session != null && session.IsActive && !session.EnsureReservation(clientId)) return;
        NetcodePrefabRegistry.EnsureInitialized();
        PruneAssignments();
        if (assignments.ContainsKey(clientId))
        {
            return;
        }

        CharacterData character = ResolveCharacterForClient(clientId);
        if (character == null || character.worldPrefab == null)
        {
            Debug.LogWarning("NetcodePlayerSpawner: aucun personnage disponible pour ce client.");
            return;
        }

        GameObject instance = TryResolveExistingInstance(character);
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
                return;
            }
        }

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogWarning("NetcodePlayerSpawner: NetworkObject manquant sur le prefab reseau.");
            if (instance != null)
            {
                Destroy(instance);
            }
            return;
        }

        SquadCharacterController controller = instance.GetComponent<SquadCharacterController>();
        if (controller != null)
        {
            controller.BindCharacterData(character, true);
            EnsureStarterInventoryIfEmpty(controller, character);
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

        // Les NetworkVariables de l'identite ne peuvent etre ecrites qu'une
        // fois le NetworkObject spawn. Cette affectation doit donc rester
        // apres Spawn, y compris pour l'hote demarre depuis MainMenu.
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

        if (controller.FlameSecondsRemaining > 0 || controller.IsFlameEquipped)
        {
            return;
        }

        if (HasSavedCharacterEntry(character))
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

    private static bool HasSavedCharacterEntry(CharacterData character)
    {
        CharacterStateStore store = ResolveCharacterStateStore();
        if (store != null && store.TryGetLoadedCharacterEntry(character, out _))
        {
            return true;
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
        PrivateSessionService.Instance?.CharacterAssigned(clientId, target.characterId);
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
        SquadCharacterController[] controllers = FindObjectsByType<SquadCharacterController>();
#else
        SquadCharacterController[] controllers = UnityEngine.Object.FindObjectsByType<SquadCharacterController>();
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

        NetcodeRuntimeUtilities.GetOrAdd<NetworkTransform>(instance);
        NetcodeRuntimeUtilities.GetOrAdd<NetcodeCharacterIdentity>(instance);
        NetcodeRuntimeUtilities.GetOrAdd<NetcodeLocalPlayer>(instance);
        NetcodeRuntimeUtilities.GetOrAdd<NetworkCharacterInput>(instance);
        NetcodeRuntimeUtilities.GetOrAdd<NetworkInventory>(instance);

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

        NetcodeRuntimeUtilities.EnsureNetworkObjectHash(networkObject, hash);
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

        string reservedId = PrivateSessionService.Instance?.ReservedCharacter(clientId);
        CharacterData preferred = !string.IsNullOrEmpty(reservedId)
            ? roster.Find(c => c != null && c.characterId == reservedId)
            : ResolvePreferredCharacter(clientId, roster);
        if (!string.IsNullOrEmpty(reservedId) && (preferred == null || assignments.ContainsValue(preferred)))
            return null;
        if (preferred != null && !assignments.ContainsValue(preferred))
        {
            int preferredIndex = roster.IndexOf(preferred);
            if (preferredIndex >= 0)
            {
                usedRosterIndices.Add(preferredIndex);
            }

            return preferred;
        }

        int index = GetNextRosterIndex(roster.Count);
        if (index < 0 || index >= roster.Count)
        {
            return null;
        }

        CharacterData character = roster[index];
        if (character != null)
        {
            usedRosterIndices.Add(index);
        }

        return character;
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

    private int GetNextRosterIndex(int rosterCount)
    {
        if (rosterCount <= 0)
        {
            return -1;
        }

        for (int i = 0; i < rosterCount; i++)
        {
            if (!usedRosterIndices.Contains(i))
            {
                return i;
            }
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
        return FindAnyObjectByType<CharacterStateStore>();
#else
        return UnityEngine.Object.FindAnyObjectByType<CharacterStateStore>();
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
            assignments.Remove(clientId);
            UpdateAssignmentRegistry(clientId, null);
        }
    }
}
