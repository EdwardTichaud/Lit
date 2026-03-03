using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Netcode;

// Gere la sauvegarde/chargement des personnages, inventaires, coffres maison et constructions.
public class CharacterStateStore : MonoBehaviour
{
    public static CharacterStateStore Instance { get; private set; }
    [Header("References")]
    [Tooltip("Reference au SquadManager (auto-resolve si null).")]
    public SquadManager squadManager;
    [Tooltip("Reference au composant Maison (auto-resolve si null).")]
    public Maison maison;
    [Tooltip("Liste d'assets CharacterData disponibles.")]
    public List<CharacterData> allCharacters = new List<CharacterData>();
    [Tooltip("Liste d'assets Item disponibles.")]
    public List<Item> allItems = new List<Item>();
    [Tooltip("Liste d'assets Skill disponibles.")]
    public List<Skill> allSkills = new List<Skill>();

    [Header("Maison - Stockage")]
    [Tooltip("Coffre maison principal.")]
    public LootContainer maisonLootContainer;
    [Tooltip("Tag utilise pour trouver les coffres maison.")]
    public string maisonChestTag = "MaisonChest";
    [Tooltip("Capacite max par coffre maison.")]
    public int maisonChestCapacity = 100;

    [Header("Constructions")]
    [Tooltip("Parent des constructions instanciees.")]
    public Transform builtParent;

    [Header("Persistence")]
    [Tooltip("Nom du fichier de sauvegarde.")]
    public string saveFileName = "CharacterState.json";
    [Tooltip("Charge automatiquement au Awake.")]
    public bool loadOnAwake = true;
    [Tooltip("Sauvegarde lors du OnDisable.")]
    public bool saveOnDisable = true;
    [Tooltip("Sauvegarde lors du quit.")]
    public bool saveOnApplicationQuit = true;

    private CharacterSaveData loadedData;
    private readonly Dictionary<string, string> playerBindings = new Dictionary<string, string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            return;
        }

        Instance = this;

        if (IsNetworked() && !IsServer())
        {
            return;
        }

        if (loadOnAwake)
        {
            Load();
            ApplyLoadedData();
        }
    }

    private void OnDisable()
    {
        if (IsNetworked() && !IsServer())
        {
            return;
        }

        if (saveOnDisable)
        {
            Save();
        }
    }

    private void OnApplicationQuit()
    {
        if (IsNetworked() && !IsServer())
        {
            return;
        }

        if (saveOnApplicationQuit)
        {
            Save();
        }
    }

    public void Save()
    {
        if (IsNetworked() && !IsServer())
        {
            return;
        }

        SquadManager manager = GetSquadManager();
        if (manager == null)
        {
            return;
        }

        CharacterSaveData data = BuildSaveData(manager);
        string path = GetPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string json = JsonUtility.ToJson(data);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        try
        {
            File.WriteAllText(path, json);
        }
        catch (IOException ex)
        {
            Debug.LogWarning($"CharacterStateStore: echec d'ecriture {path}. {ex.Message}");
        }
    }

    public void Load()
    {
        if (IsNetworked() && !IsServer())
        {
            return;
        }

        string path = GetPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            loadedData = null;
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            loadedData = JsonUtility.FromJson<CharacterSaveData>(json);
        }
        catch (IOException ex)
        {
            Debug.LogWarning($"CharacterStateStore: echec de lecture {path}. {ex.Message}");
            loadedData = null;
        }
    }

    private void ApplyLoadedData()
    {
        if (IsNetworked() && !IsServer())
        {
            return;
        }

        if (loadedData == null)
        {
            return;
        }

        ApplyLoadedPlayerBindings(loadedData);

        // Construit les lookups, puis applique les donnees sauvegardees.
        SquadManager manager = GetSquadManager();
        if (manager == null)
        {
            return;
        }

        Dictionary<string, CharacterData> characterLookup = BuildCharacterLookup(manager);
        Dictionary<string, Item> itemLookup = BuildItemLookup();
        Dictionary<string, Skill> skillLookup = BuildSkillLookup();
        Dictionary<string, Item> buildingLookup = BuildBuildingLookup();

        manager.SetPendingLoadData(loadedData, characterLookup, itemLookup, skillLookup);
        ApplyBuiltConstructions(loadedData, itemLookup, buildingLookup);
        ApplyHomeItems(loadedData, itemLookup);
    }

    private void ApplyLoadedPlayerBindings(CharacterSaveData data)
    {
        playerBindings.Clear();
        if (data == null || data.playerBindings == null)
        {
            return;
        }

        for (int i = 0; i < data.playerBindings.Count; i++)
        {
            PlayerCharacterBinding binding = data.playerBindings[i];
            if (binding == null || string.IsNullOrWhiteSpace(binding.playerId) || string.IsNullOrWhiteSpace(binding.characterId))
            {
                continue;
            }

            playerBindings[binding.playerId] = binding.characterId;
        }
    }

    public bool TryGetBoundCharacterId(string playerId, out string characterId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            characterId = string.Empty;
            return false;
        }

        return playerBindings.TryGetValue(playerId, out characterId);
    }

    public void SetPlayerBinding(string playerId, string characterId)
    {
        if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(characterId))
        {
            return;
        }

        playerBindings[playerId] = characterId;
    }

    private static bool IsNetworked()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    private static bool IsServer()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }

    private CharacterSaveData BuildSaveData(SquadManager manager)
    {
        CharacterSaveData data = new CharacterSaveData();
        List<CharacterData> knownCharacters = BuildKnownCharacters(manager);

        // Construit la liste de squad + l'etat de chaque personnage.
        if (manager.currentSquad != null)
        {
            for (int i = 0; i < manager.currentSquad.Count; i++)
            {
                CharacterData character = manager.currentSquad[i];
                string id = GetCharacterId(character);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    data.squadIds.Add(id);
                }
            }
        }

        data.currentIndex = manager.GetCurrentSquadIndex();

        for (int i = 0; i < knownCharacters.Count; i++)
        {
            CharacterData character = knownCharacters[i];
            if (character == null)
            {
                continue;
            }

            GameObject instance = manager.GetCharacterInstance(character);
            CharacterSaveEntry entry = new CharacterSaveEntry
            {
                characterId = GetCharacterId(character),
                inSquad = manager.currentSquad != null && manager.currentSquad.Contains(character),
                position = instance != null ? instance.transform.position : Vector3.zero,
                rotation = instance != null ? instance.transform.rotation : Quaternion.identity,
                torchSeconds = 0,
                torchEquipped = false,
                items = new List<ItemStackData>()
            };

            SquadCharacterController controller = instance != null ? instance.GetComponent<SquadCharacterController>() : null;
            int torchSeconds = character != null ? character.torchSecondsRemaining : 0;
            bool torchEquipped = character != null && character.torchEquipped;
            IReadOnlyList<Item> items = character != null ? character.InventoryItems : null;
            if (controller != null)
            {
                torchSeconds = controller.TorchSecondsRemaining;
                torchEquipped = controller.IsTorchEquipped;
                items = controller.Items;
            }

            entry.torchSeconds = torchSeconds;
            entry.torchEquipped = torchEquipped;

            Dictionary<Item, int> counts = new Dictionary<Item, int>();
            if (items != null)
            {
                for (int j = 0; j < items.Count; j++)
                {
                    Item item = items[j];
                    if (item == null)
                    {
                        continue;
                    }

                    if (!counts.TryGetValue(item, out int count))
                    {
                        counts[item] = 1;
                    }
                    else
                    {
                        counts[item] = count + 1;
                    }
                }
            }

            foreach (KeyValuePair<Item, int> pair in counts)
            {
                string itemId = GetItemId(pair.Key);
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    continue;
                }

                entry.items.Add(new ItemStackData
                {
                    itemId = itemId,
                    quantity = pair.Value
                });
            }

            entry.skillIds.Clear();
            entry.skillsInitialized = true;
            if (character.skills != null)
            {
                HashSet<string> skillIds = new HashSet<string>();
                for (int j = 0; j < character.skills.Count; j++)
                {
                    Skill skill = character.skills[j];
                    string skillId = GetSkillId(skill);
                    if (string.IsNullOrWhiteSpace(skillId))
                    {
                        continue;
                    }

                    if (skillIds.Add(skillId))
                    {
                        entry.skillIds.Add(skillId);
                    }
                }
            }

            data.characters.Add(entry);
        }

        data.homeItems = BuildHomeItems();
        data.builtConstructions = BuildBuiltConstructions();
        AppendPlayerBindings(data);
        return data;
    }

    private void AppendPlayerBindings(CharacterSaveData data)
    {
        if (data == null)
        {
            return;
        }

        data.playerBindings = new List<PlayerCharacterBinding>();
        foreach (KeyValuePair<string, string> entry in playerBindings)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Value))
            {
                continue;
            }

            data.playerBindings.Add(new PlayerCharacterBinding
            {
                playerId = entry.Key,
                characterId = entry.Value
            });
        }
    }

    private List<CharacterData> BuildKnownCharacters(SquadManager manager)
    {
        List<CharacterData> list = new List<CharacterData>();
        if (allCharacters != null)
        {
            for (int i = 0; i < allCharacters.Count; i++)
            {
                CharacterData character = allCharacters[i];
                if (character == null)
                {
                    continue;
                }

                CharacterData runtimeCharacter = manager != null
                    ? manager.GetRuntimeCharacter(character)
                    : character;

                if (runtimeCharacter != null && !list.Contains(runtimeCharacter))
                {
                    list.Add(runtimeCharacter);
                }
            }
        }

        if (manager != null)
        {
            List<CharacterData> extra = manager.GetKnownCharacters();
            for (int i = 0; i < extra.Count; i++)
            {
                CharacterData character = extra[i];
                if (character != null && !list.Contains(character))
                {
                    list.Add(character);
                }
            }
        }

        return list;
    }

    private Dictionary<string, CharacterData> BuildCharacterLookup(SquadManager manager)
    {
        Dictionary<string, CharacterData> lookup = new Dictionary<string, CharacterData>();
        List<CharacterData> list = BuildKnownCharacters(manager);
        for (int i = 0; i < list.Count; i++)
        {
            CharacterData character = list[i];
            if (character == null)
            {
                continue;
            }

            CharacterData runtimeCharacter = manager != null
                ? manager.GetRuntimeCharacter(character)
                : character;

            if (runtimeCharacter == null)
            {
                continue;
            }

            string id = GetCharacterId(runtimeCharacter);
            if (!string.IsNullOrWhiteSpace(id))
            {
                lookup[id] = runtimeCharacter;
            }

            AddLegacyCharacterIds(lookup, runtimeCharacter, id);
        }

        return lookup;
    }

    private void AddLegacyCharacterIds(Dictionary<string, CharacterData> lookup, CharacterData character, string primaryId)
    {
        if (lookup == null || character == null)
        {
            return;
        }

        TryAddCharacterId(lookup, character.characterId, primaryId, character);
        TryAddCharacterId(lookup, character.characterName, primaryId, character);
        TryAddCharacterId(lookup, character.name, primaryId, character);
    }

    private void TryAddCharacterId(Dictionary<string, CharacterData> lookup, string id, string primaryId, CharacterData character)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(primaryId) && id == primaryId)
        {
            return;
        }

        if (lookup.ContainsKey(id))
        {
            return;
        }

        lookup[id] = character;
    }

    private Dictionary<string, Item> BuildItemLookup()
    {
        Dictionary<string, Item> lookup = new Dictionary<string, Item>();
        if (allItems != null)
        {
            for (int i = 0; i < allItems.Count; i++)
            {
                Item item = allItems[i];
                if (item == null)
                {
                    continue;
                }

                string id = GetItemId(item);
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                lookup[id] = item;
            }
        }

        Item[] loadedItems = Resources.FindObjectsOfTypeAll<Item>();
        for (int i = 0; i < loadedItems.Length; i++)
        {
            Item item = loadedItems[i];
            if (item == null)
            {
                continue;
            }

            string id = GetItemId(item);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!lookup.ContainsKey(id))
            {
                lookup[id] = item;
            }
        }

        return lookup;
    }

    private Dictionary<string, Item> BuildBuildingLookup()
    {
        Dictionary<string, Item> lookup = new Dictionary<string, Item>();

#if UNITY_2023_1_OR_NEWER
        BuilderController[] builders = FindObjectsByType<BuilderController>(FindObjectsSortMode.None);
#else
        BuilderController[] builders = FindObjectsOfType<BuilderController>();
#endif
        if (builders != null)
        {
            for (int i = 0; i < builders.Length; i++)
            {
                BuilderController builder = builders[i];
                if (builder == null)
                {
                    continue;
                }

                builder.EnsureAvailableBuildings();
                if (builder.availableBuildings == null)
                {
                    continue;
                }

                for (int j = 0; j < builder.availableBuildings.Count; j++)
                {
                    Item item = builder.availableBuildings[j];
                    AddBuildingToLookup(lookup, item);
                }
            }
        }

        Item[] loadedItems = Resources.FindObjectsOfTypeAll<Item>();
        for (int i = 0; i < loadedItems.Length; i++)
        {
            AddBuildingToLookup(lookup, loadedItems[i]);
        }

        return lookup;
    }

    private void AddBuildingToLookup(Dictionary<string, Item> lookup, Item data)
    {
        if (lookup == null || data == null || !data.isBuilding)
        {
            return;
        }

        string id = GetBuildingItemId(data);
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        lookup[id] = data;
    }

    private Dictionary<string, Skill> BuildSkillLookup()
    {
        Dictionary<string, Skill> lookup = new Dictionary<string, Skill>();
        if (allSkills != null)
        {
            for (int i = 0; i < allSkills.Count; i++)
            {
                Skill skill = allSkills[i];
                if (skill == null)
                {
                    continue;
                }

                string id = GetSkillId(skill);
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                lookup[id] = skill;
            }
        }

        Skill[] loadedSkills = Resources.FindObjectsOfTypeAll<Skill>();
        for (int i = 0; i < loadedSkills.Length; i++)
        {
            Skill skill = loadedSkills[i];
            if (skill == null)
            {
                continue;
            }

            string id = GetSkillId(skill);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!lookup.ContainsKey(id))
            {
                lookup[id] = skill;
            }
        }

        return lookup;
    }

    private string GetCharacterId(CharacterData character)
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

    private string GetItemId(Item item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(item.itemId))
        {
            return item.itemId;
        }

        if (!string.IsNullOrWhiteSpace(item.itemName))
        {
            return item.itemName;
        }

        return item.name;
    }

    private string GetSkillId(Skill skill)
    {
        if (skill == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(skill.skillName))
        {
            return skill.skillName;
        }

        return skill.name;
    }

    private string GetBuildingItemId(Item data)
    {
        if (data == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(data.itemId))
        {
            return data.itemId;
        }

        if (!string.IsNullOrWhiteSpace(data.itemName))
        {
            return data.itemName;
        }

        return data.name;
    }

    private List<ItemStackData> BuildHomeItems()
    {
        List<ItemStackData> items = new List<ItemStackData>();
        List<LootContainer> containers = GetHomeLootContainers();
        if (containers == null || containers.Count == 0)
        {
            return items;
        }

        Dictionary<string, int> counts = new Dictionary<string, int>();
        for (int i = 0; i < containers.Count; i++)
        {
            LootContainer container = containers[i];
            if (container == null || container.lootItems == null)
            {
                continue;
            }

            for (int j = 0; j < container.lootItems.Count; j++)
            {
                LootContainer.LootItemEntry entry = container.lootItems[j];
                if (entry == null || entry.item == null)
                {
                    continue;
                }

                int quantity = Mathf.Max(0, entry.quantity);
                if (quantity <= 0)
                {
                    continue;
                }

                string itemId = GetItemId(entry.item);
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    continue;
                }

                if (!counts.TryGetValue(itemId, out int count))
                {
                    counts[itemId] = quantity;
                }
                else
                {
                    counts[itemId] = count + quantity;
                }
            }
        }

        foreach (KeyValuePair<string, int> pair in counts)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
            {
                continue;
            }

            items.Add(new ItemStackData
            {
                itemId = pair.Key,
                quantity = pair.Value
            });
        }

        return items;
    }

    private void ApplyHomeItems(CharacterSaveData data, Dictionary<string, Item> itemLookup)
    {
        if (data == null || data.homeItems == null || itemLookup == null)
        {
            return;
        }

        // Re-injecte les items de la maison dans les coffres.
        List<LootContainer> containers = GetHomeLootContainers();
        if (containers == null || containers.Count == 0)
        {
            return;
        }

        for (int i = 0; i < containers.Count; i++)
        {
            LootContainer container = containers[i];
            if (container == null)
            {
                continue;
            }

            EnsureHomeChestDefaults(container);
            container.SetLootItems(new List<LootContainer.LootItemEntry>(), false);
        }

        int containerIndex = 0;
        for (int i = 0; i < data.homeItems.Count; i++)
        {
            ItemStackData stack = data.homeItems[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.itemId) || stack.quantity <= 0)
            {
                continue;
            }

            if (!itemLookup.TryGetValue(stack.itemId, out Item item) || item == null)
            {
                continue;
            }

            int remaining = stack.quantity;
            while (remaining > 0 && containerIndex < containers.Count)
            {
                LootContainer container = containers[containerIndex];
                if (container == null)
                {
                    containerIndex++;
                    continue;
                }

                int capacity = container.GetRemainingCapacity();
                if (capacity <= 0)
                {
                    containerIndex++;
                    continue;
                }

                int toAdd = capacity == int.MaxValue ? remaining : Mathf.Min(capacity, remaining);
                if (toAdd <= 0)
                {
                    containerIndex++;
                    continue;
                }

                container.AddItems(item, toAdd);
                remaining -= toAdd;

                if (capacity != int.MaxValue && toAdd >= capacity)
                {
                    containerIndex++;
                }
            }

            if (remaining > 0)
            {
                Debug.LogWarning($"CharacterStateStore: pas assez de place dans les coffres maison pour {stack.itemId} (reste {remaining}).");
            }
        }
    }

    private List<BuiltConstructionData> BuildBuiltConstructions()
    {
        List<BuiltConstructionData> results = new List<BuiltConstructionData>();

#if UNITY_2023_1_OR_NEWER
        BuilderController[] builders = FindObjectsByType<BuilderController>(FindObjectsSortMode.None);
#else
        BuilderController[] builders = FindObjectsOfType<BuilderController>();
#endif
        bool usedBuilder = false;
        if (builders != null && builders.Length > 0)
        {
            for (int i = 0; i < builders.Length; i++)
            {
                BuilderController builder = builders[i];
                if (builder == null)
                {
                    continue;
                }

                usedBuilder = true;
                builder.EnsureBuiltBuildings();
                List<BuilderController.BuiltBuildingEntry> entries = builder.builtBuildings;
                if (entries == null || entries.Count == 0)
                {
                    continue;
                }

                for (int j = 0; j < entries.Count; j++)
                {
                    BuilderController.BuiltBuildingEntry entry = entries[j];
                    if (entry == null || entry.info == null)
                    {
                        if (entry == null || entry.building == null)
                        {
                            continue;
                        }
                    }

                    BuildingInfoInteractable info = entry.info;
                    Item buildingItem = info != null && info.BuildingItem != null ? info.BuildingItem : entry.building;
                    if (buildingItem == null)
                    {
                        continue;
                    }

                    string buildId = info != null ? info.BuildId : GetBuildingItemId(buildingItem);
                    string buildingItemId = GetBuildingItemId(buildingItem);
                    string itemId = buildingItemId;
                    if (string.IsNullOrWhiteSpace(itemId))
                    {
                        LootContainer container = info != null ? info.GetComponentInChildren<LootContainer>() : null;
                        if (container != null && container.containerItem != null)
                        {
                            itemId = GetItemId(container.containerItem);
                        }
                    }

                    if (string.IsNullOrWhiteSpace(buildId))
                    {
                        buildId = itemId;
                    }

                    if (string.IsNullOrWhiteSpace(buildingItemId))
                    {
                        buildingItemId = buildId;
                    }

                    if (string.IsNullOrWhiteSpace(buildId) && string.IsNullOrWhiteSpace(itemId) && string.IsNullOrWhiteSpace(buildingItemId))
                    {
                        continue;
                    }

                    bool isHomeChest = buildingItem != null && buildingItem.isHomeChest;
                    string maisonTag = GetMaisonChestTag();
                    if (info != null && !isHomeChest && !string.IsNullOrWhiteSpace(maisonTag))
                    {
                        try
                        {
                            if (info.gameObject.CompareTag(maisonTag))
                            {
                                isHomeChest = true;
                            }
                        }
                        catch (UnityException)
                        {
                            // Tag not defined, ignore.
                        }
                    }

                    Transform t = info != null ? info.transform : null;
                    Vector3 position = t != null ? t.position : entry.position;
                    Quaternion rotation = t != null ? t.rotation : Quaternion.identity;
                    Vector3 scale = t != null ? t.localScale : Vector3.one;
                    if (info != null)
                    {
                        entry.position = position;
                        entry.level = Mathf.Max(1, info.Level);
                    }

                    BuiltConstructionData data = new BuiltConstructionData
                    {
                        buildId = buildId,
                        itemId = itemId,
                        buildingDataId = buildingItemId,
                        level = Mathf.Max(1, info != null ? info.Level : entry.level),
                        isHomeChest = isHomeChest,
                        position = position,
                        rotation = rotation,
                        scale = scale
                    };

                    results.Add(data);
                }
            }
        }

        if (usedBuilder)
        {
            return results;
        }

#if UNITY_2023_1_OR_NEWER
        BuildingInfoInteractable[] infos = FindObjectsByType<BuildingInfoInteractable>(FindObjectsSortMode.None);
#else
        BuildingInfoInteractable[] infos = FindObjectsOfType<BuildingInfoInteractable>();
#endif
        if (infos == null || infos.Length == 0)
        {
            return results;
        }

        for (int i = 0; i < infos.Length; i++)
        {
            BuildingInfoInteractable info = infos[i];
            if (info == null)
            {
                continue;
            }

            string buildId = info.BuildId;
            Item buildingItem = info.BuildingItem;
            string buildingItemId = GetBuildingItemId(buildingItem);
            string itemId = buildingItemId;
            if (string.IsNullOrWhiteSpace(itemId))
            {
                LootContainer container = info.GetComponentInChildren<LootContainer>();
                if (container != null && container.containerItem != null)
                {
                    itemId = GetItemId(container.containerItem);
                }
            }

            if (string.IsNullOrWhiteSpace(buildId))
            {
                buildId = itemId;
            }

            if (string.IsNullOrWhiteSpace(buildingItemId))
            {
                buildingItemId = buildId;
            }

            if (string.IsNullOrWhiteSpace(buildId) && string.IsNullOrWhiteSpace(itemId) && string.IsNullOrWhiteSpace(buildingItemId))
            {
                continue;
            }

            bool isHomeChest = buildingItem != null && buildingItem.isHomeChest;
            string maisonTag = GetMaisonChestTag();
            if (!isHomeChest && !string.IsNullOrWhiteSpace(maisonTag))
            {
                try
                {
                    if (info.gameObject.CompareTag(maisonTag))
                    {
                        isHomeChest = true;
                    }
                }
                catch (UnityException)
                {
                    // Tag not defined, ignore.
                }
            }

            Transform t = info.transform;
            BuiltConstructionData data = new BuiltConstructionData
            {
                buildId = buildId,
                itemId = itemId,
                buildingDataId = buildingItemId,
                level = Mathf.Max(1, info.Level),
                isHomeChest = isHomeChest,
                position = t.position,
                rotation = t.rotation,
                scale = t.localScale
            };

            results.Add(data);
        }

        return results;
    }

    private void ApplyBuiltConstructions(CharacterSaveData data, Dictionary<string, Item> itemLookup, Dictionary<string, Item> buildingLookup)
    {
        if (data == null || data.builtConstructions == null || itemLookup == null)
        {
            return;
        }

        BuilderController builder = GetBuilderController();
        if (builder != null)
        {
            builder.ClearBuiltBuildings(true);
        }

        ResetBuildingLevels(buildingLookup);

        // Instancie les constructions sauvegardees si elles n'existent pas deja.
        BuildingInfoInteractable[] existingInfos = null;
        if (builder == null)
        {
#if UNITY_2023_1_OR_NEWER
            existingInfos = FindObjectsByType<BuildingInfoInteractable>(FindObjectsSortMode.None);
#else
            existingInfos = FindObjectsOfType<BuildingInfoInteractable>();
#endif
        }

        Transform parent = ResolveBuiltParent(builder);

        for (int i = 0; i < data.builtConstructions.Count; i++)
        {
            BuiltConstructionData entry = data.builtConstructions[i];
            if (entry == null)
            {
                continue;
            }

            if (builder == null && IsAlreadyBuilt(entry, existingInfos))
            {
                continue;
            }

            Item buildingItem = null;
            if (buildingLookup != null && !string.IsNullOrWhiteSpace(entry.buildingDataId))
            {
                buildingLookup.TryGetValue(entry.buildingDataId, out buildingItem);
            }

            Item item = null;
            if (!string.IsNullOrWhiteSpace(entry.itemId))
            {
                itemLookup.TryGetValue(entry.itemId, out item);
            }

            if (item == null && !string.IsNullOrWhiteSpace(entry.buildId))
            {
                itemLookup.TryGetValue(entry.buildId, out item);
            }

            if (buildingItem == null)
            {
                buildingItem = item;
            }

            if (buildingItem != null && !buildingItem.isBuilding)
            {
                buildingItem = null;
            }

            GameObject prefab = null;
            if (buildingItem != null && buildingItem.isBuilding && buildingItem.buildingPrefab != null)
            {
                prefab = buildingItem.buildingPrefab;
            }
            else if (item != null)
            {
                prefab = item.worldPrefab;
            }
            if (prefab == null)
            {
                Debug.LogWarning($"CharacterStateStore: prefab introuvable pour construction {entry.buildId}.");
                continue;
            }

            GameObject instance = parent != null
                ? Instantiate(prefab, entry.position, entry.rotation, parent)
                : Instantiate(prefab, entry.position, entry.rotation);
            if (instance == null)
            {
                continue;
            }

            instance.transform.localScale = entry.scale;

            BuildingInfoInteractable info = instance.GetComponent<BuildingInfoInteractable>();
            if (info == null)
            {
                info = instance.AddComponent<BuildingInfoInteractable>();
            }

            string buildId = !string.IsNullOrWhiteSpace(entry.buildId)
                ? entry.buildId
                : (!string.IsNullOrWhiteSpace(entry.buildingDataId) ? entry.buildingDataId : entry.itemId);

            info.Initialize(buildId, buildingItem, Mathf.Max(1, entry.level));
            if (builder != null && buildingItem != null && buildingItem.isBuilding)
            {
                builder.RegisterBuiltBuilding(buildingItem, Mathf.Max(1, entry.level), info);
            }
            else if (buildingItem != null && buildingItem.isBuilding)
            {
                buildingItem.buildingCurrentLevel = Mathf.Max(buildingItem.buildingCurrentLevel, Mathf.Max(1, entry.level));
            }

            if (entry.isHomeChest)
            {
                TryAssignMaisonChestTag(instance);
            }

            LootContainer container = instance.GetComponentInChildren<LootContainer>();
            if (container != null)
            {
            if (item != null)
            {
                container.containerItem = item;
            }

                if (entry.isHomeChest)
                {
                    EnsureHomeChestDefaults(container);
                }
            }
        }
    }

    private bool IsAlreadyBuilt(BuiltConstructionData entry, BuildingInfoInteractable[] existing)
    {
        if (entry == null || existing == null)
        {
            return false;
        }

        for (int i = 0; i < existing.Length; i++)
        {
            BuildingInfoInteractable info = existing[i];
            if (info == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.buildingDataId))
            {
                string infoDataId = info.BuildingItemId;
                if (!string.IsNullOrWhiteSpace(infoDataId) && infoDataId != entry.buildingDataId)
                {
                    continue;
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.buildId)
                && !string.IsNullOrWhiteSpace(info.BuildId)
                && info.BuildId != entry.buildId)
            {
                continue;
            }

            float distance = Vector3.Distance(info.transform.position, entry.position);
            if (distance <= 0.05f)
            {
                return true;
            }
        }

        return false;
    }

    private BuilderController GetBuilderController()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<BuilderController>();
#else
        return FindObjectOfType<BuilderController>();
#endif
    }

    private Transform ResolveBuiltParent(BuilderController builder)
    {
        if (builder != null)
        {
            Transform root = builder.GetBuildingsRoot();
            if (root != null)
            {
                return root;
            }
        }

        if (builtParent != null)
        {
            return builtParent;
        }

        GameObject found = GameObject.Find("Buildings");
        if (found != null)
        {
            return found.transform;
        }

        return builtParent;
    }

    private void ResetBuildingLevels(Dictionary<string, Item> buildingLookup)
    {
        if (buildingLookup == null)
        {
            return;
        }

        foreach (KeyValuePair<string, Item> entry in buildingLookup)
        {
            Item item = entry.Value;
            if (item != null && item.isBuilding)
            {
                item.buildingCurrentLevel = 0;
            }
        }
    }

    private void TryAssignMaisonChestTag(GameObject instance)
    {
        string tag = GetMaisonChestTag();
        if (instance == null || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        try
        {
            instance.tag = tag;
        }
        catch (UnityException)
        {
            // Tag not defined, ignore.
        }
    }

    private List<LootContainer> GetHomeLootContainers()
    {
        List<LootContainer> results = new List<LootContainer>();
        AddUnique(results, maisonLootContainer);
        Maison resolvedMaison = GetMaison();
        if (resolvedMaison != null)
        {
            AddUnique(results, resolvedMaison.maisonLootContainer);
        }

        string tag = GetMaisonChestTag();

        if (!string.IsNullOrWhiteSpace(tag))
        {
            try
            {
                GameObject[] found = GameObject.FindGameObjectsWithTag(tag);
                if (found != null)
                {
                    for (int i = 0; i < found.Length; i++)
                    {
                        LootContainer container = found[i] != null ? found[i].GetComponent<LootContainer>() : null;
                        AddUnique(results, container);
                    }
                }
            }
            catch (UnityException)
            {
                // Tag not defined, ignore.
            }
        }

        return results;
    }

    private int GetHomeChestCapacity()
    {
        Maison resolvedMaison = GetMaison();
        if (resolvedMaison != null && resolvedMaison.maisonChestCapacity > 0)
        {
            return resolvedMaison.maisonChestCapacity;
        }

        return Mathf.Max(0, maisonChestCapacity);
    }

    private void EnsureHomeChestDefaults(LootContainer container)
    {
        if (container == null)
        {
            return;
        }

        int capacity = GetHomeChestCapacity();
        if (capacity > 0 && container.maxTotalQuantity <= 0)
        {
            container.maxTotalQuantity = capacity;
        }

        Maison resolvedMaison = GetMaison();
        if (resolvedMaison != null && resolvedMaison.forceMaisonChestNonCollectable)
        {
            container.collectable = false;
        }
    }

    private void AddUnique(List<LootContainer> list, LootContainer container)
    {
        if (list == null || container == null)
        {
            return;
        }

        if (list.Contains(container))
        {
            return;
        }

        list.Add(container);
    }

    private Maison GetMaison()
    {
        if (maison != null)
        {
            return maison;
        }

        maison = Maison.Instance;
        if (maison == null)
        {
#if UNITY_2023_1_OR_NEWER
            maison = FindFirstObjectByType<Maison>();
#else
            maison = FindObjectOfType<Maison>();
#endif
        }

        return maison;
    }

    private string GetMaisonChestTag()
    {
        if (!string.IsNullOrWhiteSpace(maisonChestTag))
        {
            return maisonChestTag;
        }

        Maison resolvedMaison = GetMaison();
        if (resolvedMaison != null && !string.IsNullOrWhiteSpace(resolvedMaison.maisonChestTag))
        {
            return resolvedMaison.maisonChestTag;
        }

        return null;
    }

    private SquadManager GetSquadManager()
    {
        if (squadManager != null)
        {
            return squadManager;
        }

#if UNITY_2023_1_OR_NEWER
        squadManager = FindFirstObjectByType<SquadManager>();
#else
        squadManager = FindObjectOfType<SquadManager>();
#endif
        return squadManager;
    }

    private string GetPath()
    {
        if (string.IsNullOrWhiteSpace(saveFileName))
        {
            return null;
        }

        return Path.Combine(Application.persistentDataPath, saveFileName);
    }
}
