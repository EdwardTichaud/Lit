using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Spawner serveur pour attribuer un personnage a chaque client.
public class NetcodePlayerSpawner : MonoBehaviour
{
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private bool spawnWorldInteractionService = true;

    private readonly Dictionary<ulong, CharacterData> assignments = new Dictionary<ulong, CharacterData>();
    private readonly HashSet<int> usedRosterIndices = new HashSet<int>();

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

        NetcodePrefabRegistry.EnsureInitialized();
        if (spawnWorldInteractionService)
        {
            SpawnWorldInteractionService();
        }

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
        }

        NetcodePlayerSessionRegistry.Unregister(clientId);
    }

    private void SpawnForClient(ulong clientId)
    {
        NetcodePrefabRegistry.EnsureInitialized();
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

        Transform parent = SquadManager.Instance != null ? SquadManager.Instance.squadCharactersParent : null;
        Vector3 position = ResolveSpawnPosition(character);
        Quaternion rotation = ResolveSpawnRotation(character);

        GameObject instance = NetcodePrefabRegistry.SpawnCharacterInstance(character, position, rotation, parent);
        if (instance == null)
        {
            Debug.LogWarning($"NetcodePlayerSpawner: prefab reseau manquant pour {character.name}.");
            return;
        }

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogWarning("NetcodePlayerSpawner: NetworkObject manquant sur le prefab reseau.");
            Destroy(instance);
            return;
        }

        SquadCharacterController controller = instance.GetComponent<SquadCharacterController>();
        if (controller != null)
        {
            controller.BindCharacterData(character, true);
        }

        assignments[clientId] = character;
        networkObject.SpawnAsPlayerObject(clientId, true);

        NetworkInventory inventory = instance.GetComponent<NetworkInventory>();
        if (inventory != null && inventory.IsServer)
        {
            inventory.SyncFromController();
        }

        RegisterWithSquadManager(character, instance);
        RegisterPlayerBinding(clientId, character);
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

        int index = GetNextRosterIndex(roster.Count);
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

    private static CharacterStateStore ResolveCharacterStateStore()
    {
        if (CharacterStateStore.Instance != null)
        {
            return CharacterStateStore.Instance;
        }

#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<CharacterStateStore>();
#else
        return Object.FindObjectOfType<CharacterStateStore>();
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
}
