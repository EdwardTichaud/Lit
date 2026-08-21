using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

// Gere la sauvegarde/chargement des personnages, inventaires, coffres maison et constructions.
public class CharacterStateStore : MonoBehaviour
{
    public static CharacterStateStore Instance { get; private set; }
    private const int SaveDataVersion = 7;
    [Header("References")]
    [Tooltip("Reference au SquadManager (auto-resolve si null).")]
    public SquadManager squadManager;
    [Tooltip("Reference au composant Maison (auto-resolve si null).")]
    public Maison maison;
    [Tooltip("Liste d'assets CharacterData disponibles.")]
    public List<CharacterData> allCharacters = new List<CharacterData>();
    [Tooltip("Liste d'assets Item disponibles.")]
    public List<Item> allItems = new List<Item>();
    [Tooltip("Liste d'assets StatsSO disponibles.")]
    public List<StatsSO> allSkills = new List<StatsSO>();

    [Header("Maison - Stockage")]
    [Tooltip("Coffre maison principal.")]
    public InteractableItem maisonLootContainer;
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
    [Tooltip("Autorise un fichier global hors session active. Desactive pour isoler completement les parties runtime.")]
    public bool allowGlobalFallbackWithoutActiveSave = false;
    [Tooltip("Charge automatiquement au Awake.")]
    public bool loadOnAwake = true;
    [Tooltip("Sauvegarde lors du OnDisable.")]
    public bool saveOnDisable = true;
    [Tooltip("Sauvegarde lors du quit.")]
    public bool saveOnApplicationQuit = true;
    [Tooltip("Capture un screenshot lors d'une sauvegarde.")]
    public bool captureScreenshotOnSave = true;
    [Tooltip("Nom du fichier screenshot ecrit a cote de la sauvegarde.")]
    public string screenshotFileName = "screenshot.png";
    [Tooltip("Force la capture meme si captureScreenshotOnSave est desactive.")]
    public bool forceScreenshotOnSave = true;

    private CharacterSaveData loadedData;
    private readonly Dictionary<string, string> playerBindings = new Dictionary<string, string>();
    private Coroutine screenshotRoutine;
    private bool suppressNextAutomaticSave;
    private string suppressNextAutomaticSaveReason;

    public bool HasSaveFile
    {
        get
        {
            string path = GetPath();
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }
    }

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

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
        if (!Application.isPlaying)
        {
            return;
        }

        if (IsNetworked() && !IsServer())
        {
            return;
        }

        if (saveOnDisable && !ConsumeAutomaticSaveSuppression("OnDisable"))
        {
            Save();
        }
    }

    private void OnApplicationQuit()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (IsNetworked() && !IsServer())
        {
            return;
        }

        if (saveOnApplicationQuit && !ConsumeAutomaticSaveSuppression("OnApplicationQuit"))
        {
            Save();
        }
    }

    public void SuppressNextAutomaticSave(string reason = null)
    {
        suppressNextAutomaticSave = true;
        suppressNextAutomaticSaveReason = reason ?? string.Empty;
    }

    public CharacterSaveData CaptureRuntimeState(string reason = null)
    {
        if (!Application.isPlaying || (IsNetworked() && !IsServer()))
        {
            return null;
        }

        SquadManager manager = GetSquadManager();
        if (manager == null)
        {
            return null;
        }

        CharacterSaveData data = BuildSaveData(manager);
        return CloneCharacterSaveData(data);
    }

    public bool RestoreRuntimeState(
        CharacterSaveData snapshot,
        bool worldStateAlreadyRestored = false,
        string reason = null)
    {
        if (!Application.isPlaying || snapshot == null || (IsNetworked() && !IsServer()))
        {
            return false;
        }

        loadedData = CloneCharacterSaveData(snapshot);
        return ApplyLoadedData(
            restoreWorldFromDisk: false,
            worldAlreadyRestored: worldStateAlreadyRestored,
            applySquadNow: true,
            restoreReason: reason);
    }

    private bool ConsumeAutomaticSaveSuppression(string eventName)
    {
        if (!suppressNextAutomaticSave)
        {
            return false;
        }

        suppressNextAutomaticSave = false;
        if (!string.IsNullOrWhiteSpace(suppressNextAutomaticSaveReason))
        {
            Debug.Log($"CharacterStateStore: sauvegarde automatique ignoree ({eventName}, {suppressNextAutomaticSaveReason}).");
        }

        suppressNextAutomaticSaveReason = null;
        return true;
    }

    private static CharacterSaveData CloneCharacterSaveData(CharacterSaveData data)
    {
        if (data == null)
        {
            return null;
        }

        string json = JsonUtility.ToJson(data);
        return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<CharacterSaveData>(json);
    }

    public void Save()
    {
        if (!Application.isPlaying)
        {
            return;
        }

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
            SaveWorldSnapshot();
            if (SaveSessionManager.Instance != null)
            {
                SaveSessionManager.Instance.RecordSaveMetadata(SceneManager.GetActiveScene().name);
            }
            RequestScreenshotCapture();
        }
        catch (IOException ex)
        {
            Debug.LogWarning($"CharacterStateStore: echec d'ecriture {path}. {ex.Message}");
        }
    }

    private void SaveWorldSnapshot()
    {
#if UNITY_2023_1_OR_NEWER
        WorldSaveAdapter adapter = FindAnyObjectByType<WorldSaveAdapter>();
#else
        WorldSaveAdapter adapter = FindAnyObjectByType<WorldSaveAdapter>();
#endif
        if (adapter == null)
        {
            return;
        }

        adapter.SaveWorldSnapshot();
    }

    public void Load()
    {
        if (!Application.isPlaying)
        {
            loadedData = null;
            return;
        }

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
        ApplyLoadedData(
            restoreWorldFromDisk: true,
            worldAlreadyRestored: false,
            applySquadNow: false,
            restoreReason: "character_state_store_host_load");
    }

    private bool ApplyLoadedData(
        bool restoreWorldFromDisk,
        bool worldAlreadyRestored,
        bool applySquadNow,
        string restoreReason)
    {
        if (IsNetworked() && !IsServer())
        {
            return false;
        }

        ReadableContentRuntime.RestoreSaveData(loadedData != null ? loadedData.readableGeneratedContents : null);

        if (loadedData == null)
        {
            return false;
        }

        ApplyLoadedPlayerBindings(loadedData);

        // Construit les lookups, puis applique les donnees sauvegardees.
        SquadManager manager = GetSquadManager();
        if (manager == null)
        {
            return false;
        }

        Dictionary<string, CharacterData> characterLookup = BuildCharacterLookup(manager);
        Dictionary<string, Item> itemLookup = BuildItemLookup();
        Dictionary<string, StatsSO> skillLookup = BuildSkillLookup();
        Dictionary<string, Item> buildingLookup = BuildBuildingLookup();

        manager.SetPendingLoadData(loadedData, characterLookup, itemLookup, skillLookup);

        bool appliedWorldSnapshot = worldAlreadyRestored;
        if (!appliedWorldSnapshot && restoreWorldFromDisk)
        {
            appliedWorldSnapshot = TryApplyLoadedWorldSnapshot(restoreReason);
        }

        if (!appliedWorldSnapshot)
        {
            ApplyBuiltConstructions(loadedData, itemLookup, buildingLookup);
            ApplyHomeItems(loadedData, itemLookup);
            ApplyFlameStates(loadedData);
        }
        else if (restoreWorldFromDisk)
        {
            PersistentWorldDebug.Log("host load path restored world state from snapshot; compatibility world restore skipped", this);
        }

        if (applySquadNow)
        {
            manager.ApplyPendingLoadDataNow();
            SyncNetworkInventoriesFromControllers();
        }

        return true;
    }

    private void RequestScreenshotCapture()
    {
        if ((!captureScreenshotOnSave && !forceScreenshotOnSave) || !Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        if (screenshotRoutine != null)
        {
            StopCoroutine(screenshotRoutine);
        }

        screenshotRoutine = StartCoroutine(CaptureScreenshotRoutine());
    }

    private System.Collections.IEnumerator CaptureScreenshotRoutine()
    {
        yield return new WaitForEndOfFrame();

        string screenshotPath = GetScreenshotPath();
        if (string.IsNullOrWhiteSpace(screenshotPath))
        {
            screenshotRoutine = null;
            yield break;
        }

        Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
        if (texture == null)
        {
            screenshotRoutine = null;
            yield break;
        }

        try
        {
            string directory = Path.GetDirectoryName(screenshotPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            byte[] png = texture.EncodeToPNG();
            if (png != null && png.Length > 0)
            {
                File.WriteAllBytes(screenshotPath, png);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"CharacterStateStore: echec screenshot {screenshotPath}. {ex.Message}");
        }
        finally
        {
            Destroy(texture);
            screenshotRoutine = null;
        }
    }

    private string GetScreenshotPath()
    {
        if (string.IsNullOrWhiteSpace(screenshotFileName))
        {
            return null;
        }

        SaveSessionManager session = SaveSessionManager.Instance;
        if (session != null && session.HasActiveSave)
        {
            return session.GetActiveSaveFilePath(screenshotFileName);
        }

        string basePath = GetPath();
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return null;
        }

        string directory = Path.GetDirectoryName(basePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        return Path.Combine(directory, screenshotFileName);
    }

    private bool TryApplyLoadedWorldSnapshot(string restoreReason = null)
    {
#if UNITY_2023_1_OR_NEWER
        WorldSaveAdapter adapter = FindAnyObjectByType<WorldSaveAdapter>();
#else
        WorldSaveAdapter adapter = FindAnyObjectByType<WorldSaveAdapter>();
#endif
        if (adapter == null)
        {
            return false;
        }

        if (!adapter.HasSavedWorldSnapshot())
        {
            PersistentWorldDebug.Log("host load path did not find a world snapshot; using compatibility restore", this);
            return false;
        }

        string resolvedReason = string.IsNullOrWhiteSpace(restoreReason)
            ? "character_state_store_host_load"
            : restoreReason;
        bool applied = adapter.EnsureHostWorldRestoredFromSave(resolvedReason);
        if (!applied)
        {
            PersistentWorldDebug.Error("host load path world snapshot apply failed; falling back to compatibility restore", this);
            return false;
        }

        WorldSnapshot snapshot = adapter.LastLoadedSnapshot;
        PersistentWorldDebug.Log(
            $"host load path restored world snapshot scene='{snapshot?.SceneName}' runtimeObjects={snapshot?.RuntimeObjects?.Count ?? 0} sceneObjects={snapshot?.SceneObjects?.Count ?? 0} restoreSequence={adapter.LastRestoreSequence} identityValidated={adapter.LastRestoreIdentityValidated} identityIssues={adapter.LastRestoreIdentityIssues}",
            this);
        return true;
    }

    private static void SyncNetworkInventoriesFromControllers()
    {
        if (!IsNetworked() || !IsServer())
        {
            return;
        }

#if UNITY_2023_1_OR_NEWER
        NetworkInventory[] inventories = FindObjectsByType<NetworkInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        NetworkInventory[] inventories = FindObjectsOfType<NetworkInventory>(true);
#endif
        if (inventories == null)
        {
            return;
        }

        for (int i = 0; i < inventories.Length; i++)
        {
            inventories[i]?.SyncFromController();
        }
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

    public bool TryGetLoadedCharacterEntry(CharacterData character, out CharacterSaveEntry entry)
    {
        entry = null;
        string characterId = GetCharacterId(character);
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        return TryGetLoadedCharacterEntry(characterId, out entry);
    }

    public bool TryGetLoadedCharacterEntry(string characterId, out CharacterSaveEntry entry)
    {
        entry = null;
        if (loadedData == null || loadedData.characters == null || string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        for (int i = 0; i < loadedData.characters.Count; i++)
        {
            CharacterSaveEntry candidate = loadedData.characters[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.characterId))
            {
                continue;
            }

            if (!string.Equals(candidate.characterId, characterId, StringComparison.Ordinal))
            {
                continue;
            }

            entry = candidate;
            return true;
        }

        return false;
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
        CharacterSaveData data = new CharacterSaveData
        {
            dataVersion = SaveDataVersion
        };
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
            CharacterRuntimeState runtimeState = manager.GetCharacterRuntimeState(character);
            CharacterSaveEntry entry = new CharacterSaveEntry
            {
                characterId = GetCharacterId(character),
                inSquad = manager.currentSquad != null && manager.currentSquad.Contains(character),
                position = instance != null ? instance.transform.position : Vector3.zero,
                rotation = instance != null ? instance.transform.rotation : Quaternion.identity,
                flameSeconds = 0,
                flameEquipped = false,
                muninChargesInitialized = runtimeState != null && runtimeState.muninChargesInitialized,
                muninCharges = runtimeState != null ? runtimeState.muninChargesRemaining : 0,
                muninMaxCharges = runtimeState != null ? runtimeState.muninMaxCharges : 0,
                items = new List<ItemStackData>(),
                itemsInitialized = runtimeState != null && runtimeState.inventoryInitialized
            };

            SquadCharacterController controller = instance != null ? instance.GetComponent<SquadCharacterController>() : null;
            int flameSeconds = runtimeState != null ? runtimeState.flameSecondsRemaining : 0;
            bool flameEquipped = runtimeState != null && runtimeState.flameEquipped;
            IReadOnlyList<Item> items = runtimeState != null ? runtimeState.inventoryItems : null;
            IReadOnlyList<Item> enabledCombatItems = runtimeState != null ? runtimeState.enabledCombatItems : null;
            if (controller != null)
            {
                flameSeconds = controller.FlameSecondsRemaining;
                flameEquipped = controller.IsFlameEquipped;
                items = controller.Items;
                enabledCombatItems = controller.EnabledCombatItems;
                MuninController munin = controller.GetComponentInChildren<MuninController>(true);
                if (munin != null)
                {
                    entry.muninChargesInitialized = true;
                    entry.muninCharges = munin.ChargesRemaining;
                    entry.muninMaxCharges = munin.MaxCharges;
                }
            }

            entry.flameSeconds = flameSeconds;
            entry.flameEquipped = flameEquipped;

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

            entry.enabledCombatItemIds.Clear();
            if (enabledCombatItems != null)
            {
                HashSet<string> enabledIds = new HashSet<string>();
                for (int j = 0; j < enabledCombatItems.Count; j++)
                {
                    Item item = enabledCombatItems[j];
                    if (item == null || !item.CanUseInCombatReaction())
                    {
                        continue;
                    }

                    string itemId = GetItemId(item);
                    if (string.IsNullOrWhiteSpace(itemId) || !enabledIds.Add(itemId))
                    {
                        continue;
                    }

                    entry.enabledCombatItemIds.Add(itemId);
                }
            }

            entry.combatDefenseItemHitPoints.Clear();
            IReadOnlyList<CombatDefenseItemHitPointData> combatDefenseHitPoints = controller != null
                ? controller.GetCombatDefenseItemHitPointsSnapshot()
                : runtimeState != null ? runtimeState.combatDefenseItemHitPoints : null;
            if (combatDefenseHitPoints != null)
            {
                Dictionary<string, Item> carriedItemById = new Dictionary<string, Item>();
                foreach (KeyValuePair<Item, int> pair in counts)
                {
                    string itemId = GetItemId(pair.Key);
                    if (string.IsNullOrWhiteSpace(itemId) || carriedItemById.ContainsKey(itemId))
                    {
                        continue;
                    }

                    carriedItemById[itemId] = pair.Key;
                }

                HashSet<string> savedDefenseKeys = new HashSet<string>();
                for (int j = 0; j < combatDefenseHitPoints.Count; j++)
                {
                    CombatDefenseItemHitPointData hitPointEntry = combatDefenseHitPoints[j];
                    if (hitPointEntry == null
                        || string.IsNullOrWhiteSpace(hitPointEntry.itemId)
                        || hitPointEntry.hitPoints <= 0
                        || !carriedItemById.TryGetValue(hitPointEntry.itemId, out Item item)
                        || item == null)
                    {
                        continue;
                    }

                    int maxHitPoints = item.GetCombatDefenseHitPoints();
                    int remainingHitPoints = Mathf.Clamp(hitPointEntry.hitPoints, 0, maxHitPoints);
                    string defenseKey = $"{hitPointEntry.itemId}:{remainingHitPoints}";
                    if (maxHitPoints <= 0
                        || remainingHitPoints <= 0
                        || remainingHitPoints >= maxHitPoints
                        || !savedDefenseKeys.Add(defenseKey))
                    {
                        continue;
                    }

                    entry.combatDefenseItemHitPoints.Add(new CombatDefenseItemHitPointData
                    {
                        itemId = hitPointEntry.itemId,
                        hitPoints = remainingHitPoints,
                        quantity = Mathf.Max(1, hitPointEntry.quantity)
                    });
                }
            }

            entry.skillIds.Clear();
            entry.skillsInitialized = true;
            if (character.skills != null)
            {
                HashSet<string> skillIds = new HashSet<string>();
                for (int j = 0; j < character.skills.Count; j++)
                {
                    StatsSO skill = character.skills[j];
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
        data.builtConstructions = LegacyBuildingPersistenceMigration.ResolveCharacterSaveData(
            loadedData,
            LegacyBuildingSystem.Enabled ? BuildBuiltConstructions() : null);
        data.flames = BuildFlameStates();
        data.readableGeneratedContents = ReadableContentRuntime.CaptureSaveData();
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

            AddAlternateCharacterIds(lookup, runtimeCharacter, id);
        }

        return lookup;
    }

    private void AddAlternateCharacterIds(Dictionary<string, CharacterData> lookup, CharacterData character, string primaryId)
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
        BuilderController[] builders = FindObjectsByType<BuilderController>();
#else
        BuilderController[] builders = FindObjectsByType<BuilderController>();
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

    private Dictionary<string, StatsSO> BuildSkillLookup()
    {
        Dictionary<string, StatsSO> lookup = new Dictionary<string, StatsSO>();
        if (allSkills != null)
        {
            for (int i = 0; i < allSkills.Count; i++)
            {
                StatsSO skill = allSkills[i];
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

        StatsSO[] loadedSkills = Resources.FindObjectsOfTypeAll<StatsSO>();
        for (int i = 0; i < loadedSkills.Length; i++)
        {
            StatsSO skill = loadedSkills[i];
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

    private string GetSkillId(StatsSO skill)
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
        List<InteractableItem> containers = GetHomeLootContainers();
        if (containers == null || containers.Count == 0)
        {
            return items;
        }

        Dictionary<string, int> counts = new Dictionary<string, int>();
        for (int i = 0; i < containers.Count; i++)
        {
            InteractableItem container = containers[i];
            if (container == null || container.storedItems == null)
            {
                continue;
            }

            for (int j = 0; j < container.storedItems.Count; j++)
            {
                InteractableItem.LootItemEntry entry = container.storedItems[j];
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
        List<InteractableItem> containers = GetHomeLootContainers();
        if (containers == null || containers.Count == 0)
        {
            return;
        }

        for (int i = 0; i < containers.Count; i++)
        {
            InteractableItem container = containers[i];
            if (container == null)
            {
                continue;
            }

            EnsureHomeChestDefaults(container);
            container.SetLootItems(new List<InteractableItem.LootItemEntry>(), false);
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
                InteractableItem container = containers[containerIndex];
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

    private List<FlameSaveEntry> BuildFlameStates()
    {
        List<FlameSaveEntry> results = new List<FlameSaveEntry>();
#if UNITY_2023_1_OR_NEWER
        Flame[] flames = FindObjectsByType<Flame>(FindObjectsInactive.Include);
#else
        Flame[] flames = FindObjectsByType<Flame>(FindObjectsInactive.Include);
#endif
        if (flames == null || flames.Length == 0)
        {
            return results;
        }

        HashSet<string> usedIds = new HashSet<string>();
        for (int i = 0; i < flames.Length; i++)
        {
            Flame flame = flames[i];
            if (flame == null)
            {
                continue;
            }

            string id = flame.FlameId;
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning("CharacterStateStore: flame sans ID, ignore pour la sauvegarde.", flame);
                continue;
            }

            if (!usedIds.Add(id))
            {
                Debug.LogWarning($"CharacterStateStore: flameId duplique '{id}', ignore.", flame);
                continue;
            }

            results.Add(new FlameSaveEntry
            {
                flameId = id,
                isLit = flame.IsLit
            });
        }

        return results;
    }

    private void ApplyFlameStates(CharacterSaveData data)
    {
        if (data == null || data.flames == null || data.flames.Count == 0)
        {
            return;
        }

        Dictionary<string, bool> states = new Dictionary<string, bool>();
        for (int i = 0; i < data.flames.Count; i++)
        {
            FlameSaveEntry entry = data.flames[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.flameId))
            {
                continue;
            }

            states[entry.flameId] = entry.isLit;
        }

        if (states.Count == 0)
        {
            return;
        }

#if UNITY_2023_1_OR_NEWER
        Flame[] flames = FindObjectsByType<Flame>(FindObjectsInactive.Include);
#else
        Flame[] flames = FindObjectsByType<Flame>(FindObjectsInactive.Include);
#endif
        if (flames == null || flames.Length == 0)
        {
            return;
        }

        for (int i = 0; i < flames.Length; i++)
        {
            Flame flame = flames[i];
            if (flame == null)
            {
                continue;
            }

            string id = flame.FlameId;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (states.TryGetValue(id, out bool lit))
            {
                flame.SetLit(lit);
            }
        }
    }

    private List<BuiltConstructionData> BuildBuiltConstructions()
    {
        List<BuiltConstructionData> results = new List<BuiltConstructionData>();

#if UNITY_2023_1_OR_NEWER
        BuilderController[] builders = FindObjectsByType<BuilderController>();
#else
        BuilderController[] builders = FindObjectsByType<BuilderController>();
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
                        InteractableItem container = info != null ? info.GetComponentInChildren<InteractableItem>() : null;
                        if (container != null && container.representedItem != null)
                        {
                            itemId = GetItemId(container.representedItem);
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
        BuildingInfoInteractable[] infos = FindObjectsByType<BuildingInfoInteractable>();
#else
        BuildingInfoInteractable[] infos = FindObjectsByType<BuildingInfoInteractable>();
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
                InteractableItem container = info.GetComponentInChildren<InteractableItem>();
                if (container != null && container.representedItem != null)
                {
                    itemId = GetItemId(container.representedItem);
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
        if (!LegacyBuildingSystem.Enabled)
        {
            return;
        }

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
            existingInfos = FindObjectsByType<BuildingInfoInteractable>();
#else
            existingInfos = FindObjectsByType<BuildingInfoInteractable>();
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

            RuntimeOutlineUtility.EnsureOutlineTargets(instance);
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
                BuildingRuntimeState.SetLevel(buildingItem, Mathf.Max(1, entry.level), true);
            }

            if (entry.isHomeChest)
            {
                TryAssignMaisonChestTag(instance);
            }

            InteractableItem container = instance.GetComponentInChildren<InteractableItem>();
            if (container != null)
            {
                container.interactableCategory = InteractableItem.InteractableCategory.Container;
                if (item != null)
                {
                    container.representedItem = item;
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
        return FindAnyObjectByType<BuilderController>();
#else
        return FindAnyObjectByType<BuilderController>();
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
        BuildingRuntimeState.Clear();
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

    private List<InteractableItem> GetHomeLootContainers()
    {
        List<InteractableItem> results = new List<InteractableItem>();
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
                        InteractableItem container = found[i] != null ? found[i].GetComponent<InteractableItem>() : null;
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

    private void EnsureHomeChestDefaults(InteractableItem container)
    {
        if (container == null)
        {
            return;
        }

        int capacity = GetHomeChestCapacity();
        if (capacity > 0 && container.maxStoredQuantity <= 0)
        {
            container.maxStoredQuantity = capacity;
        }

        Maison resolvedMaison = GetMaison();
        if (resolvedMaison != null && resolvedMaison.forceMaisonChestNonCollectable)
        {
            container.allowTake = false;
        }
    }

    private void AddUnique(List<InteractableItem> list, InteractableItem container)
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
            maison = FindAnyObjectByType<Maison>();
#else
            maison = FindAnyObjectByType<Maison>();
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
        squadManager = FindAnyObjectByType<SquadManager>();
#else
        squadManager = FindAnyObjectByType<SquadManager>();
#endif
        return squadManager;
    }

    private string GetPath()
    {
        if (string.IsNullOrWhiteSpace(saveFileName))
        {
            return null;
        }

        if (SaveSessionManager.Instance != null && SaveSessionManager.Instance.HasActiveSave)
        {
            string managedPath = SaveSessionManager.Instance.GetActiveSaveFilePath(saveFileName);
            if (!string.IsNullOrWhiteSpace(managedPath))
            {
                return managedPath;
            }
        }

        if (!allowGlobalFallbackWithoutActiveSave)
        {
            return null;
        }

        return Path.Combine(Application.persistentDataPath, saveFileName);
    }
}
