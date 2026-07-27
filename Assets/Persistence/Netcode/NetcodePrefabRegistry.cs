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
    private static readonly HashSet<uint> registeredHashes = new HashSet<uint>();
    private static readonly uint worldInteractionHash = NetcodeStableHash.Hash32("service:world-interaction");

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
            if (character == null || character.model == null)
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
            sourcePrefab = character.model,
            hash = NetcodeStableHash.Hash32($"character:{key}")
        };

        characterInfos[key] = created;
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

    private static GameObject CreateWorldInteractionInstance(uint hash)
    {
        GameObject instance = new GameObject("WorldInteractionService");
        instance.AddComponent<WorldInteractionService>();
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
