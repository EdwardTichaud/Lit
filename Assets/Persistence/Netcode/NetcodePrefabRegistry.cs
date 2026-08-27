using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Registre des handlers Netcode pour instancier des prefabs dynamiques.
public static class NetcodePrefabRegistry
{
    private class ItemSpawnInfo
    {
        public Item item;
        public GameObject sourcePrefab;
        public uint plainHash;
        public uint lootHash;
    }

    private class CharacterSpawnInfo
    {
        public CharacterData character;
        public GameObject sourcePrefab;
        public uint hash;
    }

    private class SceneMarkerCharacterSpawnInfo
    {
        public string markerId;
        public CharacterData character;
        public uint hash;
    }

    private class ItemPrefabHandler : INetworkPrefabInstanceHandler
    {
        private readonly ItemSpawnInfo info;
        private readonly bool withLoot;

        public ItemPrefabHandler(ItemSpawnInfo info, bool withLoot)
        {
            this.info = info;
            this.withLoot = withLoot;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            GameObject instance = CreateItemInstance(info, withLoot, position, rotation, null);
            return instance != null ? instance.GetComponent<NetworkObject>() : null;
        }

        public void Destroy(NetworkObject networkObject)
        {
            if (networkObject != null)
            {
                Object.Destroy(networkObject.gameObject);
            }
        }
    }

    private class CharacterPrefabHandler : INetworkPrefabInstanceHandler
    {
        private readonly CharacterSpawnInfo info;

        public CharacterPrefabHandler(CharacterSpawnInfo info)
        {
            this.info = info;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            GameObject instance = CreateCharacterInstance(info, position, rotation, null);
            return instance != null ? instance.GetComponent<NetworkObject>() : null;
        }

        public void Destroy(NetworkObject networkObject)
        {
            if (networkObject != null)
            {
                Object.Destroy(networkObject.gameObject);
            }
        }
    }

    private class SceneMarkerCharacterPrefabHandler : INetworkPrefabInstanceHandler
    {
        private readonly SceneMarkerCharacterSpawnInfo info;

        public SceneMarkerCharacterPrefabHandler(SceneMarkerCharacterSpawnInfo info)
        {
            this.info = info;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            GameObject instance = CreateSceneMarkerCharacterInstance(info, position, rotation);
            return instance != null ? instance.GetComponent<NetworkObject>() : null;
        }

        public void Destroy(NetworkObject networkObject)
        {
            if (networkObject != null)
            {
                Object.Destroy(networkObject.gameObject);
            }
        }
    }

    private class ServicePrefabHandler : INetworkPrefabInstanceHandler
    {
        private readonly uint hash;

        public ServicePrefabHandler(uint hash)
        {
            this.hash = hash;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            GameObject instance = CreateWorldInteractionInstance(hash);
            return instance != null ? instance.GetComponent<NetworkObject>() : null;
        }

        public void Destroy(NetworkObject networkObject)
        {
            if (networkObject != null)
            {
                Object.Destroy(networkObject.gameObject);
            }
        }
    }

    private static bool initialized;
    private static readonly Dictionary<string, ItemSpawnInfo> itemInfos = new Dictionary<string, ItemSpawnInfo>();
    private static readonly Dictionary<string, CharacterSpawnInfo> characterInfos = new Dictionary<string, CharacterSpawnInfo>();
    private static readonly Dictionary<string, SceneMarkerCharacterSpawnInfo> sceneMarkerCharacterInfos = new Dictionary<string, SceneMarkerCharacterSpawnInfo>();
    private static readonly HashSet<uint> registeredHashes = new HashSet<uint>();
    private static readonly uint worldInteractionHash = NetcodeStableHash.Hash32("service:world-interaction");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeCaches()
    {
        initialized = false;
        itemInfos.Clear();
        characterInfos.Clear();
        sceneMarkerCharacterInfos.Clear();
        registeredHashes.Clear();
    }

    /// <summary>Clears stale SceneMarker source references after an authoring change.</summary>
    public static void InvalidateSceneMarkerCharacterCache(string markerId = null)
    {
        if (string.IsNullOrWhiteSpace(markerId))
        {
            foreach (SceneMarkerCharacterSpawnInfo info in sceneMarkerCharacterInfos.Values)
            {
                RemoveSceneMarkerHandler(info);
            }
            sceneMarkerCharacterInfos.Clear();
            return;
        }

        if (sceneMarkerCharacterInfos.TryGetValue(markerId, out SceneMarkerCharacterSpawnInfo existing))
        {
            RemoveSceneMarkerHandler(existing);
            sceneMarkerCharacterInfos.Remove(markerId);
        }
    }

    public static void EnsureInitialized()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        if (initialized)
        {
            Refresh();
            return;
        }

        initialized = true;
        Refresh();
    }

    public static void Refresh()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        List<Item> items = CollectItems();
        EnsureItemRegistry(items);
        RegisterItemHandlers(items);
        RegisterCharacterHandlers(CollectCharacters());
        RegisterSceneMarkerCharacterHandlers();
        RegisterServiceHandler();
    }

    public static GameObject SpawnItemInstance(Item item, bool withLootContainer, Vector3 position, Quaternion rotation)
    {
        if (item == null)
        {
            return null;
        }

        EnsureInitialized();
        ItemSpawnInfo info = GetItemInfo(item);
        if (info == null)
        {
            uint hash = NetcodeStableHash.Hash32("item:unknown:fallback");
            return CreateFallbackItemInstance(position, rotation, withLootContainer, hash);
        }

        return CreateItemInstance(info, withLootContainer, position, rotation, null);
    }

    public static GameObject SpawnCharacterInstance(CharacterData character, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (character == null)
        {
            return null;
        }

        EnsureInitialized();
        CharacterSpawnInfo info = GetCharacterInfo(character);
        if (info == null)
        {
            return null;
        }

        return CreateCharacterInstance(info, position, rotation, parent);
    }

    /// <summary>
    /// Creates the world prefab associated with a scene marker. This is intentionally
    /// separate from squad spawning, although both use CharacterData.worldPrefab.
    /// </summary>
    public static GameObject SpawnSceneMarkerCharacterInstance(string markerId, CharacterData character, Vector3 position, Quaternion rotation)
    {
        if (string.IsNullOrWhiteSpace(markerId) || character == null || character.worldPrefab == null)
        {
            return null;
        }

        EnsureInitialized();
        SceneMarkerCharacterSpawnInfo info = GetSceneMarkerCharacterInfo(markerId, character);
        if (info == null)
        {
            return null;
        }

        RegisterHandler(info.hash, new SceneMarkerCharacterPrefabHandler(info));
        return CreateSceneMarkerCharacterInstance(info, position, rotation);
    }

    public static void RegisterSceneMarker(SceneMarker marker)
    {
        if (marker == null || string.IsNullOrWhiteSpace(marker.MarkerId) || marker.CharacterData == null || marker.CharacterData.worldPrefab == null)
        {
            return;
        }

        SceneMarkerCharacterSpawnInfo info = GetSceneMarkerCharacterInfo(marker.MarkerId, marker.CharacterData);
        if (info != null)
        {
            RegisterHandler(info.hash, new SceneMarkerCharacterPrefabHandler(info));
        }
    }

    public static void UnregisterSceneMarker(SceneMarker marker)
    {
        if (marker == null || string.IsNullOrWhiteSpace(marker.MarkerId))
        {
            return;
        }

        InvalidateSceneMarkerCharacterCache(marker.MarkerId);
    }

    public static uint GetCharacterPrefabHash(CharacterData character)
    {
        CharacterSpawnInfo info = GetCharacterInfo(character);
        return info != null ? info.hash : 0u;
    }

    public static GameObject SpawnWorldInteractionServiceInstance()
    {
        EnsureInitialized();
        return CreateWorldInteractionInstance(worldInteractionHash);
    }

    private static void RegisterItemHandlers(List<Item> items)
    {
        if (items == null)
        {
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null)
            {
                continue;
            }

            ItemSpawnInfo info = GetItemInfo(item);
            if (info == null)
            {
                continue;
            }

            RegisterHandler(info.plainHash, new ItemPrefabHandler(info, false));
            RegisterHandler(info.lootHash, new ItemPrefabHandler(info, true));
        }
    }

    private static void RegisterCharacterHandlers(List<CharacterData> characters)
    {
        if (characters == null)
        {
            return;
        }

        for (int i = 0; i < characters.Count; i++)
        {
            CharacterData character = characters[i];
            if (character == null || character.worldPrefab == null)
            {
                continue;
            }

            CharacterSpawnInfo info = GetCharacterInfo(character);
            if (info == null)
            {
                continue;
            }

            RegisterHandler(info.hash, new CharacterPrefabHandler(info));
        }
    }

    private static void RegisterSceneMarkerCharacterHandlers()
    {
#if UNITY_2023_1_OR_NEWER
        SceneMarker[] markers = Object.FindObjectsByType<SceneMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        SceneMarker[] markers = Object.FindObjectsOfType<SceneMarker>(true);
#endif
        for (int i = 0; i < markers.Length; i++)
        {
            RegisterSceneMarker(markers[i]);
        }
    }

    private static void RegisterServiceHandler()
    {
        RegisterHandler(worldInteractionHash, new ServicePrefabHandler(worldInteractionHash));
    }

    private static void RegisterHandler(uint hash, INetworkPrefabInstanceHandler handler)
    {
        if (hash == 0u || handler == null || NetworkManager.Singleton == null)
        {
            return;
        }

        if (registeredHashes.Contains(hash))
        {
            return;
        }

        NetworkPrefabHandler prefabHandler = NetworkManager.Singleton.PrefabHandler;
        if (prefabHandler == null)
        {
            return;
        }

        prefabHandler.AddHandler(hash, handler);
        registeredHashes.Add(hash);
    }

    private static ItemSpawnInfo GetItemInfo(Item item)
    {
        if (item == null)
        {
            return null;
        }

        string key = GetItemKey(item);
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (itemInfos.TryGetValue(key, out ItemSpawnInfo info))
        {
            return info;
        }

        GameObject basePrefab = item.ResolveWorldPrefab();
        ItemSpawnInfo created = new ItemSpawnInfo
        {
            item = item,
            sourcePrefab = basePrefab,
            plainHash = NetcodeStableHash.Hash32($"item:{key}:plain"),
            lootHash = NetcodeStableHash.Hash32($"item:{key}:loot")
        };

        itemInfos[key] = created;
        return created;
    }

    private static CharacterSpawnInfo GetCharacterInfo(CharacterData character)
    {
        if (character == null)
        {
            return null;
        }

        string key = GetCharacterKey(character);
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (characterInfos.TryGetValue(key, out CharacterSpawnInfo info))
        {
            return info;
        }

        CharacterSpawnInfo created = new CharacterSpawnInfo
        {
            character = character,
            sourcePrefab = character.worldPrefab,
            hash = NetcodeStableHash.Hash32($"character:{key}")
        };

        characterInfos[key] = created;
        return created;
    }

    private static SceneMarkerCharacterSpawnInfo GetSceneMarkerCharacterInfo(string markerId, CharacterData character)
    {
        if (string.IsNullOrWhiteSpace(markerId) || character == null || character.worldPrefab == null)
        {
            return null;
        }

        if (sceneMarkerCharacterInfos.TryGetValue(markerId, out SceneMarkerCharacterSpawnInfo existing))
        {
            existing.character = character;
            return existing;
        }

        SceneMarkerCharacterSpawnInfo created = new SceneMarkerCharacterSpawnInfo
        {
            markerId = markerId,
            character = character,
            hash = NetcodeStableHash.Hash32($"scene-marker-character:{markerId}")
        };
        sceneMarkerCharacterInfos[markerId] = created;
        return created;
    }

    private static GameObject CreateItemInstance(ItemSpawnInfo info, bool withLootContainer, Vector3 position, Quaternion rotation, Transform parent)
    {
        GameObject instance = null;
        if (info != null && info.sourcePrefab != null)
        {
            instance = parent != null
                ? Object.Instantiate(info.sourcePrefab, position, rotation, parent)
                : Object.Instantiate(info.sourcePrefab, position, rotation);
        }
        else
        {
            uint fallbackHash = withLootContainer ? info.lootHash : info.plainHash;
            instance = CreateFallbackItemInstance(position, rotation, withLootContainer, fallbackHash);
        }

        if (instance == null)
        {
            return null;
        }

        RuntimeOutlineUtility.EnsureOutlineTargets(instance);
        if (withLootContainer)
        {
            WorldPickupUtility.EnsurePickupInfrastructure(instance);
        }

        uint hash = withLootContainer ? info.lootHash : info.plainHash;
        NetworkObject networkObject = NetcodeRuntimeUtilities.GetOrAdd<NetworkObject>(instance);
        NetcodeRuntimeUtilities.GetOrAdd<PersistentNetworkObject>(instance);
        if (BeaconMarker.TryFind(instance, out _))
        {
            NetcodeRuntimeUtilities.GetOrAdd<PersistentBeaconState>(instance);
        }
        NetcodeRuntimeUtilities.EnsureNetworkObjectHash(networkObject, hash);
        return instance;
    }

    private static GameObject CreateCharacterInstance(CharacterSpawnInfo info, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (info == null || info.sourcePrefab == null)
        {
            return null;
        }

        GameObject instance = parent != null
            ? Object.Instantiate(info.sourcePrefab, position, rotation, parent)
            : Object.Instantiate(info.sourcePrefab, position, rotation);

        NetcodeRuntimeUtilities.GetOrAdd<NetworkTransform>(instance);
        NetcodeRuntimeUtilities.GetOrAdd<NetcodeCharacterIdentity>(instance);
        NetcodeRuntimeUtilities.GetOrAdd<NetcodeLocalPlayer>(instance);
        NetcodeRuntimeUtilities.GetOrAdd<NetworkCharacterInput>(instance);
        NetcodeRuntimeUtilities.GetOrAdd<NetworkInventory>(instance);

        NetworkObject networkObject = NetcodeRuntimeUtilities.GetOrAdd<NetworkObject>(instance);
        NetcodeRuntimeUtilities.GetOrAdd<PersistentNetworkObject>(instance);
        NetcodeRuntimeUtilities.EnsureNetworkObjectHash(networkObject, info.hash);
        return instance;
    }

    private static GameObject CreateSceneMarkerCharacterInstance(SceneMarkerCharacterSpawnInfo info, Vector3 position, Quaternion rotation)
    {
        GameObject sourcePrefab = info != null && info.character != null ? info.character.ResolveWorldPrefab() : null;
        if (info == null || sourcePrefab == null)
        {
            return null;
        }

        ValidateEnemyRuntimeSource(info, sourcePrefab, "source");

        // Offline marker actors use a normal Unity parent. In a listening
        // session the server applies the same parent after Spawn through NGO,
        // otherwise NetworkTransform can interpret the marker-relative pose as
        // a world pose during its initial synchronization.
        NetworkManager manager = NetworkManager.Singleton;
        bool isNetworkSession = manager != null && manager.IsListening;
        Transform parent = null;
        if (SceneMarker.TryGetRegisteredMarker(info.markerId, out SceneMarker marker))
        {
            parent = isNetworkSession ? null : marker.transform;
        }

        GameObject instance;
        if (parent != null)
        {
            // A scene marker is the authored spatial reference for offline
            // enemies. Do not inherit a serialized root offset from the prefab.
            instance = Object.Instantiate(sourcePrefab, parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
        }
        else
        {
            instance = Object.Instantiate(sourcePrefab);
            instance.transform.SetPositionAndRotation(position, rotation);
        }
        Physics.SyncTransforms();

        ValidateEnemyRuntimeSource(info, instance, "clone");
        SceneMarker.ConfigureSpawnedCharacter(instance, info.character, info.markerId, sourcePrefab);
        instance.GetComponent<CombatEnemyPhysicsMotor>()?.AuditPose("SceneMarker:spawn configure");
        if (isNetworkSession)
        {
            NetworkObject networkObject = NetcodeRuntimeUtilities.GetOrAdd<NetworkObject>(instance);
            NetworkTransform networkTransform = NetcodeRuntimeUtilities.GetOrAdd<NetworkTransform>(instance);
            networkTransform.SwitchTransformSpaceWhenParented = true;
            networkTransform.InLocalSpace = false;
            NetcodeRuntimeUtilities.EnsureNetworkObjectHash(networkObject, info.hash);
        }
        else
        {
            // A WorldPrefab can still carry this component from an old test.
            // It must never author an offline SceneMarker actor pose.
            NetworkTransform networkTransform = instance.GetComponent<NetworkTransform>();
            if (networkTransform != null)
            {
                networkTransform.enabled = false;
            }
        }
        return instance;
    }

    private static bool ValidateEnemyRuntimeSource(SceneMarkerCharacterSpawnInfo info, GameObject actor, string stage)
    {
        if (info.character == null || !info.character.isEnemy)
        {
            return true;
        }

        bool valid = CombatEnemyRuntimeContract.HasRequiredComponents(actor);
        string report = CombatEnemyRuntimeContract.DescribeRequiredComponents(actor);
        if (!valid)
        {
            Debug.LogError("[SceneMarker] Contrat ennemi invalide (" + stage + ") | CharacterData='" +
                           info.character.name + "' | prefab='" + actor.name + "' | identity={" +
                           DescribePrefabIdentity(actor) + "} | " + report + ".", actor);
        }

        // The clone still spawns so SceneMarker can provide the definitive
        // runtime report and disable only its combat systems. This keeps an
        // authoring mistake visible without silently deleting a scene actor.
        return true;
    }

    private static string DescribePrefabIdentity(GameObject actor)
    {
        if (actor == null)
        {
            return "absent";
        }

#if UNITY_EDITOR
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(actor);
        string guid = string.IsNullOrWhiteSpace(assetPath)
            ? "runtime"
            : UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
        return "name=" + actor.name + ", path=" +
               (string.IsNullOrWhiteSpace(assetPath) ? "runtime-clone" : assetPath) +
               ", guid=" + guid + ", fileId=" +
               UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(actor).targetObjectId;
#else
        return "name=" + actor.name + ", runtime-clone";
#endif
    }

    private static void RemoveSceneMarkerHandler(SceneMarkerCharacterSpawnInfo info)
    {
        if (info == null || NetworkManager.Singleton == null || NetworkManager.Singleton.PrefabHandler == null)
        {
            return;
        }

        NetworkManager.Singleton.PrefabHandler.RemoveHandler(info.hash);
        registeredHashes.Remove(info.hash);
    }

    private static GameObject CreateWorldInteractionInstance(uint hash)
    {
        GameObject instance = new GameObject("WorldInteractionService");
        instance.AddComponent<WorldInteractionService>();
        instance.AddComponent<KnowledgeSynchronizationService>();
        NetworkObject networkObject = instance.AddComponent<NetworkObject>();
        NetcodeRuntimeUtilities.EnsureNetworkObjectHash(networkObject, hash);
        return instance;
    }

    private static GameObject CreateFallbackItemInstance(Vector3 position, Quaternion rotation, bool withLootContainer, uint hash)
    {
        GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = Vector3.one * 0.4f;

        if (withLootContainer)
        {
            WorldPickupUtility.EnsurePickupInfrastructure(instance);
        }

        NetworkObject networkObject = NetcodeRuntimeUtilities.GetOrAdd<NetworkObject>(instance);
        if (BeaconMarker.TryFind(instance, out _))
        {
            NetcodeRuntimeUtilities.GetOrAdd<PersistentBeaconState>(instance);
        }
        NetcodeRuntimeUtilities.EnsureNetworkObjectHash(networkObject, hash);
        return instance;
    }

    private static List<Item> CollectItems()
    {
        Item[] found = Resources.FindObjectsOfTypeAll<Item>();
        List<Item> items = new List<Item>();
        if (found != null)
        {
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && !items.Contains(found[i]))
                {
                    items.Add(found[i]);
                }
            }
        }

        return items;
    }

    private static List<CharacterData> CollectCharacters()
    {
        CharacterData[] found = Resources.FindObjectsOfTypeAll<CharacterData>();
        List<CharacterData> characters = new List<CharacterData>();
        if (found != null)
        {
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && !characters.Contains(found[i]))
                {
                    characters.Add(found[i]);
                }
            }
        }

        return characters;
    }

    private static string GetItemKey(Item item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        string id = ItemIdUtils.GetItemId(item);
        if (string.IsNullOrWhiteSpace(id))
        {
            id = item.name;
        }

        return id;
    }

    private static string GetCharacterKey(CharacterData character)
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

        return character.name;
    }

    private static void EnsureItemRegistry(List<Item> items)
    {
        if (ItemRegistry.Instance == null)
        {
            GameObject host = new GameObject("ItemRegistry");
            Object.DontDestroyOnLoad(host);
            ItemRegistry registry = host.AddComponent<ItemRegistry>();
            if (items != null)
            {
                registry.items = new List<Item>(items);
            }
            registry.BuildLookup();
            return;
        }

        if (items != null && (ItemRegistry.Instance.items == null || ItemRegistry.Instance.items.Count == 0))
        {
            ItemRegistry.Instance.items = new List<Item>(items);
            ItemRegistry.Instance.BuildLookup();
        }
    }

}
