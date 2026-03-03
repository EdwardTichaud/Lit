using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using Unity.Netcode;

// Gere la squad: selection, groupes, spawn et synchronisation des personnages.
public class SquadManager : MonoBehaviour
{
    public static SquadManager Instance { get; private set; }

    public enum SendHomeResult
    {
        Success,
        InvalidCharacter,
        NotInSquad,
        StorageFull
    }

    [System.Serializable]
    private class CompanionRecord
    {
        public GameObject instance;
        public Vector3 hubPosition;
        public Quaternion hubRotation;
        public Transform hubParent;
    }

    [Header("Gestion de l'UI")]
    [Tooltip("References UI du panel de squad.")]
    public SquadUISettings squadUISettings;
    [Tooltip("Index courant dans la liste de squad.")]
    public int currentCursorIndex;
    [Tooltip("Dernier input de navigation.")]
    public Vector2 moveInput;

    [Header("Gestion de l'equipe")]
    [Tooltip("Liste des CharacterData dans la squad.")]
    public List<CharacterData> currentSquad;
    [Tooltip("Parent des instances de personnages.")]
    public Transform squadCharactersParent;
    [Tooltip("Points de spawn par index.")]
    public List<Transform> squadSpawnPoints;
    [Tooltip("Origine de spawn fallback.")]
    public Transform squadSpawnOrigin;
    [Tooltip("Instances runtime des personnages.")]
    public List<GameObject> squadCharacters;
    [Tooltip("Personnage actuellement controle.")]
    public GameObject currentCharacter;
    [Tooltip("Mode selection de personnages actif.")]
    public bool charactersSelectionOn;
    [Tooltip("Nom du spawn point Maison.")]
    public string maisonSpawnPointName = "Maison_SpawnPoint";
    [Tooltip("Reference au composant Maison (auto-resolve si null).")]
    public Maison maison;

    [Header("Runtime Clones")]
    [SerializeField, Tooltip("Clone les CharacterData a l'execution pour ne pas modifier les assets.")]
    private bool useRuntimeCharacterClones = true;

    [Header("Grouping")]
    [SerializeField, Tooltip("Tous les membres sont groupes par defaut.")]
    private bool defaultGrouped = true;
    [SerializeField, Tooltip("Nom du tag UI pour afficher le groupe.")]
    private string groupTagName = "GroupTag";
    [SerializeField, Tooltip("Label affiche pour un membre groupe.")]
    private string groupedLabel = "G";
    [SerializeField, Tooltip("Label affiche pour un membre non groupe.")]
    private string ungroupedLabel = "S";
    [SerializeField, Tooltip("Couleur du label groupe.")]
    private Color groupedColor = new Color(0.95f, 0.78f, 0.25f, 1f);
    [SerializeField, Tooltip("Couleur du label non groupe.")]
    private Color ungroupedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField, Tooltip("IDs de groupes (par index de personnage).")]
    private List<int> groupIds = new List<int>();
    [SerializeField, Tooltip("Groupe du leader actuellement controle.")]
    private int leaderGroupId = 0;
    [Tooltip("Groupes existants et leurs membres.")]
    public List<SquadGroup> squadGroups = new List<SquadGroup>();

    [System.Serializable]
    public class SquadGroup
    {
        public int groupId;
        public List<CharacterData> memberCharacters = new List<CharacterData>();
    }

    [Header("Spawn VFX")]
    [Tooltip("Prefab VFX instancie au spawn.")]
    public GameObject spawnVfxPrefab;
    [Tooltip("Offset applique au VFX.")]
    public Vector3 spawnVfxOffset = Vector3.zero;
    [Tooltip("Parent des VFX.")]
    public Transform spawnVfxParent;
    [Tooltip("Duree de vie du VFX.")]
    public float spawnVfxLifetime = 2f;

    private int lastMoveDirection;
    private float nextMoveTime;
    private int inputLockCount;
    private bool toggleTorchRequested;
    private bool takeAllRequested;
    private bool warnedMissingSquadUI;
    private bool warnedMissingMaison;
    private readonly Dictionary<CharacterData, CompanionRecord> companionRegistry = new Dictionary<CharacterData, CompanionRecord>();
    private CharacterSaveData pendingLoadData;
    private Dictionary<string, CharacterData> pendingCharacterLookup;
    private Dictionary<string, Item> pendingItemLookup;
    private Dictionary<string, Skill> pendingSkillLookup;
    private readonly Dictionary<CharacterData, CharacterData> runtimeCharacterMap = new Dictionary<CharacterData, CharacterData>();
    private readonly Dictionary<string, CharacterData> runtimeCharactersById = new Dictionary<string, CharacterData>();
    private readonly Dictionary<string, CharacterData> runtimeCharacterSourceById = new Dictionary<string, CharacterData>();
    private readonly HashSet<string> runtimeCharacterIdWarnings = new HashSet<string>();
    private readonly HashSet<CharacterData> runtimeCharacters = new HashSet<CharacterData>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        EnsureRuntimeSquad();
        InitializeSquadPanel();
    }

    void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.ToggleTorch += OnToggleTorchPerformed;
        LocalInputRouter.TakeAll += OnTakeAllPerformed;
        LocalInputRouter.LeftShoulder += OnLeftShoulderPerformed;
    }

    void OnDisable()
    {
        LocalInputRouter.ToggleTorch -= OnToggleTorchPerformed;
        LocalInputRouter.TakeAll -= OnTakeAllPerformed;
        LocalInputRouter.LeftShoulder -= OnLeftShoulderPerformed;

        InputFocusStack.Pop(this);
    }

    void Start()
    {
        StartCoroutine(StartRoutine());
    }

    // Init principale (UI, spawns, groupes, panel).
    IEnumerator StartRoutine()
    {
        EnsureSpawnOrigin();
        ApplyPendingRoster();
        EnsureRuntimeSquad();
        Maison maisonComponent = GetMaison();
        if (maisonComponent != null)
        {
            maisonComponent.EnsureHomeContainers(maisonComponent.ResolveMaisonLootContainers(null));
        }
        SquadUISettings ui = GetSquadUI();
        if (ui != null)
        {
            ui.InitializePanel(charactersSelectionOn);
            ui.BuildSquadUnits(currentSquad);
        }

        if (squadCharacters == null)
        {
            squadCharacters = new List<GameObject>();
        }
        else
        {
            squadCharacters.Clear();
        }

        if (IsMultiplayerActive())
        {
            ApplyPendingCharacterStates();
            StartCoroutine(RefreshNetworkCharactersRoutine());
            yield break;
        }

        for (int i = 0; i < currentSquad.Count; i++)
        {
            CharacterData character = currentSquad[i];
            Transform spawnPoint = null;
            if (squadSpawnPoints != null && i < squadSpawnPoints.Count)
            {
                spawnPoint = squadSpawnPoints[i];
            }

            GameObject instance = GetOrCreateCharacterInstance(character, spawnPoint, i, false);
            squadCharacters.Add(instance);
            UpdateSquadUnitUI(i, character);
        }

        EnsureGroupIds();
        UpdateAllGroupIndicators();

        ClampCursorIndex();

        UpdateCurrentCharacter();
        UpdateCursorPosition();

        UpdateSquadPanelCursorVisibility();
        ApplySquadPanelVisibility(true);
        RequestCrownReposition();
        ApplyPendingCharacterStates();
        UpdateLeaderGroupFromCurrent();
        yield return null;
    }

    private IEnumerator RefreshNetworkCharactersRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(0.5f);
        while (IsMultiplayerActive())
        {
            RefreshNetworkCharacters();
            yield return wait;
        }
    }

    private void RefreshNetworkCharacters()
    {
        if (squadCharacters == null)
        {
            squadCharacters = new List<GameObject>();
        }

#if UNITY_2023_1_OR_NEWER
        SquadCharacterController[] controllers = FindObjectsByType<SquadCharacterController>(FindObjectsSortMode.None);
#else
        SquadCharacterController[] controllers = FindObjectsOfType<SquadCharacterController>();
#endif
        if (controllers == null)
        {
            return;
        }

        for (int i = 0; i < controllers.Length; i++)
        {
            SquadCharacterController controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            CharacterData data = controller.CharacterData;
            int index = currentSquad != null && data != null ? currentSquad.IndexOf(data) : -1;
            if (index >= 0)
            {
                while (squadCharacters.Count <= index)
                {
                    squadCharacters.Add(null);
                }

                squadCharacters[index] = controller.gameObject;
            }
            else if (!squadCharacters.Contains(controller.gameObject))
            {
                squadCharacters.Add(controller.gameObject);
            }
        }

        GameObject local = LocalPlayerUtils.GetControlledCharacter();
        if (local != null)
        {
            currentCharacter = local;
        }
    }

    public void RegisterNetworkCharacter(CharacterData character, GameObject instance)
    {
        if (character == null || instance == null)
        {
            return;
        }

        CharacterData runtimeCharacter = GetRuntimeCharacter(character);
        if (squadCharacters == null)
        {
            squadCharacters = new List<GameObject>();
        }

        int index = currentSquad != null ? currentSquad.IndexOf(runtimeCharacter) : -1;
        if (index < 0)
        {
            if (!squadCharacters.Contains(instance))
            {
                squadCharacters.Add(instance);
            }
        }
        else
        {
            while (squadCharacters.Count <= index)
            {
                squadCharacters.Add(null);
            }

            squadCharacters[index] = instance;
        }
    }

    public void RegisterHubCompanion(CharacterData character, GameObject instance, Vector3 hubPosition, Quaternion hubRotation, Transform hubParent)
    {
        if (character == null || instance == null)
        {
            return;
        }

        CharacterData runtimeCharacter = GetRuntimeCharacter(character);
        CompanionRecord record = GetOrCreateRecord(runtimeCharacter);
        if (record.instance != null && record.instance != instance)
        {
            Debug.LogWarning($"SquadManager: instance deja enregistree pour {character.name}.");
            return;
        }

        record.instance = instance;
        record.hubPosition = hubPosition;
        record.hubRotation = hubRotation;
        record.hubParent = hubParent;
    }

    public void SetPendingLoadData(CharacterSaveData data, Dictionary<string, CharacterData> characterLookup, Dictionary<string, Item> itemLookup, Dictionary<string, Skill> skillLookup)
    {
        pendingLoadData = data;
        pendingCharacterLookup = characterLookup;
        pendingItemLookup = itemLookup;
        pendingSkillLookup = skillLookup;
    }

    public List<CharacterData> GetKnownCharacters()
    {
        List<CharacterData> list = new List<CharacterData>();
        if (currentSquad != null)
        {
            for (int i = 0; i < currentSquad.Count; i++)
            {
                CharacterData character = currentSquad[i];
                if (character != null && !list.Contains(character))
                {
                    list.Add(character);
                }
            }
        }

        foreach (KeyValuePair<CharacterData, CompanionRecord> pair in companionRegistry)
        {
            CharacterData character = pair.Key;
            if (character != null && !list.Contains(character))
            {
                list.Add(character);
            }
        }

        return list;
    }

    public GameObject GetCharacterInstance(CharacterData character)
    {
        if (character == null)
        {
            return null;
        }

        CharacterData runtimeCharacter = GetRuntimeCharacter(character);
        if (companionRegistry.TryGetValue(runtimeCharacter, out CompanionRecord record) && record.instance != null)
        {
            return record.instance;
        }

        if (squadCharacters != null)
        {
            int index = currentSquad != null ? currentSquad.IndexOf(runtimeCharacter) : -1;
            if (index >= 0 && index < squadCharacters.Count)
            {
                return squadCharacters[index];
            }
        }

        return null;
    }

    public int GetCurrentSquadIndex()
    {
        return GetCurrentCharacterIndex();
    }

    private GameObject SpawnCharacter(CharacterData character, Transform spawnPoint, int index, bool spawnVfx = true)
    {
        if (character == null)
        {
            Debug.LogWarning("SquadManager: CharacterData manquant.");
            return null;
        }

        if (character.model == null)
        {
            Debug.LogWarning($"SquadManager: aucun modele defini pour {character.name}.");
            return null;
        }

        Vector3 position = spawnPoint != null
            ? spawnPoint.position
            : GetStarFormationPosition(index);
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        return SpawnCharacterAt(character, position, rotation, spawnVfx);
    }

    private GameObject SpawnCharacterAt(CharacterData character, Vector3 position, Quaternion rotation, bool spawnVfx = true)
    {
        if (character == null)
        {
            return null;
        }

        Transform parent = squadCharactersParent != null ? squadCharactersParent : null;
        GameObject instance = Instantiate(character.model, position, rotation, parent);
        if (spawnVfx)
        {
            SpawnVfx(position, rotation);
        }
        SquadCharacterController controller = instance.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            controller = instance.AddComponent<SquadCharacterController>();
        }

        if (controller != null)
        {
            controller.BindCharacterData(character, true);
        }

        CompanionRecord record = GetOrCreateRecord(character);
        record.instance = instance;
        return instance;
    }

    private GameObject GetOrCreateCharacterInstance(CharacterData character, Transform spawnPoint, int index, bool spawnVfx = true)
    {
        if (character == null)
        {
            return null;
        }

        if (companionRegistry.TryGetValue(character, out CompanionRecord record) && record.instance != null)
        {
            Vector3 position = spawnPoint != null
                ? spawnPoint.position
                : GetStarFormationPosition(index);
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
            PlaceCharacter(record.instance, position, rotation, squadCharactersParent);
            SquadCharacterController controller = record.instance.GetComponent<SquadCharacterController>();
            if (controller != null)
            {
                controller.BindCharacterData(character, true);
            }
            return record.instance;
        }

        return SpawnCharacter(character, spawnPoint, index, spawnVfx);
    }

    private void PlaceCharacter(GameObject instance, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (instance == null)
        {
            return;
        }

        if (parent != null)
        {
            instance.transform.SetParent(parent, true);
        }

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
    }

    private void EnsureSpawnOrigin()
    {
        if (squadSpawnOrigin != null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(maisonSpawnPointName))
        {
            return;
        }

        GameObject found = GameObject.Find(maisonSpawnPointName);
        if (found != null)
        {
            squadSpawnOrigin = found.transform;
        }
    }

    private void ApplyPendingRoster()
    {
        if (pendingLoadData == null || pendingCharacterLookup == null)
        {
            return;
        }

        List<CharacterData> newSquad = new List<CharacterData>();
        for (int i = 0; i < pendingLoadData.squadIds.Count; i++)
        {
            string id = pendingLoadData.squadIds[i];
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (pendingCharacterLookup.TryGetValue(id, out CharacterData character) && character != null)
            {
                newSquad.Add(GetRuntimeCharacter(character));
            }
        }

        if (newSquad.Count > 0)
        {
            currentSquad = newSquad;
        }
    }

    private void ApplyPendingCharacterStates()
    {
        if (pendingLoadData == null)
        {
            return;
        }

        bool networked = IsMultiplayerActive();
        for (int i = 0; i < pendingLoadData.characters.Count; i++)
        {
            CharacterSaveEntry entry = pendingLoadData.characters[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.characterId))
            {
                continue;
            }

            if (pendingCharacterLookup == null || !pendingCharacterLookup.TryGetValue(entry.characterId, out CharacterData character) || character == null)
            {
                continue;
            }

            CharacterData runtimeCharacter = GetRuntimeCharacter(character);
            List<Item> items = BuildItemsFromEntry(entry);
            runtimeCharacter.SetInventory(items, entry.torchSeconds, entry.torchEquipped, true);
            if (entry.skillsInitialized)
            {
                List<Skill> skills = BuildSkillsFromEntry(entry);
                runtimeCharacter.SetSkills(skills);
            }

            if (networked)
            {
                continue;
            }

            GameObject instance = GetCharacterInstance(runtimeCharacter);
            if (instance == null)
            {
                if (entry.inSquad)
                {
                    instance = SpawnCharacterAt(runtimeCharacter, entry.position, entry.rotation);
                    if (instance != null)
                    {
                        if (squadCharacters == null)
                        {
                            squadCharacters = new List<GameObject>();
                        }

                        int index = currentSquad != null ? currentSquad.IndexOf(runtimeCharacter) : -1;
                        if (index >= 0)
                        {
                            while (squadCharacters.Count <= index)
                            {
                                squadCharacters.Add(null);
                            }

                            squadCharacters[index] = instance;
                        }
                    }
                }

                if (instance == null)
                {
                    continue;
                }
            }

            SquadCharacterController controller = instance.GetComponent<SquadCharacterController>();
            if (controller != null)
            {
                controller.BindCharacterData(runtimeCharacter, true);
            }

            if (entry.inSquad)
            {
                PlaceCharacter(instance, entry.position, entry.rotation, squadCharactersParent);
            }
            else
            {
                SendCharacterToHub(runtimeCharacter, instance);
            }

            HubRosterManager roster = HubRosterManager.Instance;
            if (roster != null)
            {
                roster.SetInSquad(runtimeCharacter, entry.inSquad);
            }
        }

        currentCursorIndex = pendingLoadData.currentIndex;
        ClampCursorIndex();
        UpdateCurrentCharacter();
        UpdateCursorPosition();

        pendingLoadData = null;
        pendingCharacterLookup = null;
        pendingItemLookup = null;
        pendingSkillLookup = null;
    }

    private List<Item> BuildItemsFromEntry(CharacterSaveEntry entry)
    {
        List<Item> items = new List<Item>();
        if (entry == null || pendingItemLookup == null || entry.items == null)
        {
            return items;
        }

        for (int i = 0; i < entry.items.Count; i++)
        {
            ItemStackData stack = entry.items[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.itemId) || stack.quantity <= 0)
            {
                continue;
            }

            if (!pendingItemLookup.TryGetValue(stack.itemId, out Item item) || item == null)
            {
                continue;
            }

            int count = Mathf.Max(0, stack.quantity);
            for (int j = 0; j < count; j++)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private List<Skill> BuildSkillsFromEntry(CharacterSaveEntry entry)
    {
        List<Skill> skills = new List<Skill>();
        if (entry == null || pendingSkillLookup == null || entry.skillIds == null)
        {
            return skills;
        }

        for (int i = 0; i < entry.skillIds.Count; i++)
        {
            string id = entry.skillIds[i];
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (pendingSkillLookup.TryGetValue(id, out Skill skill) && skill != null)
            {
                if (!skills.Contains(skill))
                {
                    skills.Add(skill);
                }
            }
        }

        return skills;
    }

    private void SpawnVfx(Vector3 position, Quaternion rotation)
    {
        if (spawnVfxPrefab == null)
        {
            return;
        }

        Vector3 vfxPosition = position + spawnVfxOffset;
        Transform vfxParent = spawnVfxParent != null ? spawnVfxParent : null;
        GameObject vfxInstance = Instantiate(spawnVfxPrefab, vfxPosition, rotation, vfxParent);
        if (spawnVfxLifetime > 0f)
        {
            Destroy(vfxInstance, spawnVfxLifetime);
        }
    }

    private Vector3 GetStarFormationPosition(int index)
    {
        Vector3[] offsets =
        {
            new Vector3(0f, 0f, 3f),
            new Vector3(3f, 0f, 0f),
            new Vector3(0f, 0f, -3f),
            new Vector3(-3f, 0f, 0f),
        };

        if (index < 0 || index >= offsets.Length)
        {
            return Vector3.zero;
        }

        Vector3 origin = squadSpawnOrigin != null ? squadSpawnOrigin.position : Vector3.zero;
        return origin + offsets[index];
    }

    void Update()
    {
        if (IsMultiplayerActive())
        {
            return;
        }

        if (IsInputLocked())
        {
            StopAllSquadCharacters();
            return;
        }

        moveInput = LocalInputRouter.MoveValue;

        if (charactersSelectionOn)
        {
            if (GetSquadUnitCount() == 0)
            {
                return;
            }

            ClampCursorIndex();
            HandleCursorNavigation();
            UpdateCurrentCharacter();
            UpdateCursorPosition();
            HandleGroupingInputs();
        }
        else
        {
            UpdateLeaderGroupFromCurrent();
            HandleControlledCharacterMovement();
            HandleTorchToggle();
        }
    }

    private void HandleCursorNavigation()
    {
        int direction = GetMoveDirection(moveInput.y);
        if (direction == 0)
        {
            lastMoveDirection = 0;
            nextMoveTime = 0f;
            return;
        }

        float now = Time.unscaledTime;
        if (direction != lastMoveDirection)
        {
            MoveCursor(direction);
            lastMoveDirection = direction;
            nextMoveTime = now + GetSquadUIInitialRepeatDelay();
            return;
        }

        if (now >= nextMoveTime)
        {
            MoveCursor(direction);
            nextMoveTime = now + GetSquadUIRepeatInterval();
        }
    }

    private int GetMoveDirection(float yInput)
    {
        float deadzone = GetSquadUIMoveDeadzone();
        if (yInput > deadzone)
        {
            return -1;
        }

        if (yInput < -deadzone)
        {
            return 1;
        }

        return 0;
    }

    private void MoveCursor(int direction)
    {
        int unitCount = GetSquadUnitCount();
        if (unitCount == 0)
        {
            return;
        }

        int count = unitCount;
        int nextIndex = currentCursorIndex + direction;

        if (GetSquadUIWrapCursor())
        {
            nextIndex = (nextIndex % count + count) % count;
        }
        else
        {
            nextIndex = Mathf.Clamp(nextIndex, 0, count - 1);
        }

        if (nextIndex == currentCursorIndex)
        {
            return;
        }

        currentCursorIndex = nextIndex;
        UpdateCurrentCharacter();
    }

    private void ClampCursorIndex()
    {
        int unitCount = GetSquadUnitCount();
        if (unitCount == 0)
        {
            currentCursorIndex = 0;
            return;
        }

        currentCursorIndex = Mathf.Clamp(currentCursorIndex, 0, unitCount - 1);
    }

    private void UpdateCursorPosition()
    {
        SquadUISettings ui = GetSquadUI();
        if (ui == null)
        {
            return;
        }

        ui.UpdateCursorPosition(currentCursorIndex);
    }

    private void UpdateCurrentCharacter()
    {
        if (squadCharacters == null || squadCharacters.Count == 0)
        {
            currentCharacter = null;
            UpdateCrownPosition();
            return;
        }

        if (currentCursorIndex < 0 || currentCursorIndex >= squadCharacters.Count)
        {
            currentCharacter = null;
            UpdateCrownPosition();
            return;
        }

        currentCharacter = squadCharacters[currentCursorIndex];
        UpdateCrownPosition();
    }

    private void UpdateCrownPosition()
    {
        SquadUISettings ui = GetSquadUI();
        if (ui == null)
        {
            return;
        }

        ui.UpdateCrownPosition(GetCrownIndex());
    }

    private void RequestCrownReposition()
    {
        SquadUISettings ui = GetSquadUI();
        if (ui == null)
        {
            return;
        }

        ui.RequestCrownReposition(GetCrownIndex());
    }

    private int GetCrownIndex()
    {
        int index = -1;
        if (currentCharacter != null && squadCharacters != null)
        {
            index = squadCharacters.IndexOf(currentCharacter);
        }

        if (index < 0)
        {
            index = currentCursorIndex;
        }

        return index;
    }

    private void HandleControlledCharacterMovement()
    {
        if (currentCharacter == null)
        {
            return;
        }

        SquadCharacterController controller = currentCharacter.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            return;
        }

        controller.Move(moveInput);
    }

    private void StopControlledCharacter()
    {
        if (currentCharacter == null)
        {
            return;
        }

        SquadCharacterController controller = currentCharacter.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            return;
        }

        controller.Stop();
    }

    private void StopAllSquadCharacters()
    {
        if (squadCharacters == null)
        {
            return;
        }

        for (int i = 0; i < squadCharacters.Count; i++)
        {
            GameObject character = squadCharacters[i];
            if (character == null)
            {
                continue;
            }

            SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
            if (controller == null)
            {
                continue;
            }

            controller.Stop();
        }
    }

    private void HandleTorchToggle()
    {
        if (currentCharacter == null)
        {
            return;
        }

        if (!toggleTorchRequested)
        {
            return;
        }
        toggleTorchRequested = false;

        SquadCharacterController controller = currentCharacter.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            return;
        }

        controller.ToggleTorch();
    }

    private void HandleGroupingInputs()
    {
        if (!charactersSelectionOn)
        {
            return;
        }

        if (GetSquadUnitCount() == 0)
        {
            return;
        }

        if (takeAllRequested)
        {
            SetGrouped(currentCursorIndex, false);
        }

        if (toggleTorchRequested)
        {
            SetGrouped(currentCursorIndex, true);
        }

        takeAllRequested = false;
        toggleTorchRequested = false;
    }

    private void OnToggleTorchPerformed(InputAction.CallbackContext context)
    {
        toggleTorchRequested = true;
    }

    private void OnTakeAllPerformed(InputAction.CallbackContext context)
    {
        takeAllRequested = true;
    }

    private static bool IsMultiplayerActive()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    private void UpdateLeaderGroupFromCurrent()
    {
        if (currentCharacter == null)
        {
            return;
        }

        int index = GetCurrentCharacterIndex();
        if (index < 0)
        {
            return;
        }

        EnsureGroupIds();
        if (index >= groupIds.Count)
        {
            return;
        }

        leaderGroupId = groupIds[index];
    }

    private int GetNextSoloGroupId(int index)
    {
        EnsureGroupIds();

        int count = groupIds.Count;
        if (count == 0)
        {
            return 0;
        }

        HashSet<int> used = new HashSet<int>();
        for (int i = 0; i < groupIds.Count; i++)
        {
            if (i == index)
            {
                continue;
            }

            used.Add(groupIds[i]);
        }

        for (int candidate = 0; candidate < count; candidate++)
        {
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }

        return count;
    }

    private void EnsureGroupIds()
    {
        int count = squadCharacters != null ? squadCharacters.Count : 0;
        if (groupIds == null)
        {
            groupIds = new List<int>();
        }

        if (groupIds.Count > count)
        {
            groupIds.RemoveRange(count, groupIds.Count - count);
        }
        else if (groupIds.Count < count)
        {
            int missing = count - groupIds.Count;
            int startIndex = groupIds.Count;
            for (int i = 0; i < missing; i++)
            {
                int index = startIndex + i;
                groupIds.Add(defaultGrouped ? 0 : index);
            }
        }

        RebuildGroups();
    }

    public void EnsureGroups()
    {
        EnsureGroupIds();
        UpdateAllGroupIndicators();
    }

    public int GetGroupId(CharacterData character)
    {
        if (character == null)
        {
            return -1;
        }

        character = GetRuntimeCharacter(character);
        EnsureGroupIds();
        if (currentSquad == null)
        {
            return -1;
        }

        int index = currentSquad.IndexOf(character);
        if (index < 0 || index >= groupIds.Count)
        {
            return -1;
        }

        return groupIds[index];
    }

    public bool AreInSameGroup(CharacterData a, CharacterData b)
    {
        int groupA = GetGroupId(a);
        int groupB = GetGroupId(b);
        return groupA >= 0 && groupA == groupB;
    }

    public int LeaderGroupId => leaderGroupId;

    private void RebuildGroups()
    {
        if (squadGroups == null)
        {
            squadGroups = new List<SquadGroup>();
        }

        squadGroups.Clear();
        if (groupIds == null)
        {
            return;
        }

        Dictionary<int, SquadGroup> groups = new Dictionary<int, SquadGroup>();
        for (int i = 0; i < groupIds.Count; i++)
        {
            int groupId = groupIds[i];
            if (!groups.TryGetValue(groupId, out SquadGroup group))
            {
                group = new SquadGroup { groupId = groupId };
                groups[groupId] = group;
                squadGroups.Add(group);
            }

            CharacterData character = currentSquad != null && i < currentSquad.Count
                ? currentSquad[i]
                : null;
            if (character != null)
            {
                group.memberCharacters.Add(character);
            }
        }
    }

    private void UpdateAllGroupIndicators()
    {
        if (GetSquadUnitCount() == 0)
        {
            return;
        }

        int count = GetSquadUnitCount();
        for (int i = 0; i < count; i++)
        {
            UpdateGroupIndicator(i);
        }
    }

    private void UpdateGroupIndicator(int index)
    {
        if (index < 0 || index >= GetSquadUnitCount())
        {
            return;
        }

        GameObject unit = GetSquadUnitAt(index);
        if (unit == null)
        {
            return;
        }

        TMP_Text label = GetGroupLabel(unit);
        if (label == null)
        {
            return;
        }

        bool grouped = GetGroupSize(index) > 1;
        label.text = grouped ? groupedLabel : ungroupedLabel;
        label.color = grouped ? groupedColor : ungroupedColor;
        label.gameObject.SetActive(true);
    }

    private TMP_Text GetGroupLabel(GameObject unit)
    {
        if (unit == null)
        {
            return null;
        }

        Transform tag = null;
        Transform root = unit.transform;
        if (root.childCount > 3)
        {
            tag = root.GetChild(3);
        }

        if (tag == null && !string.IsNullOrWhiteSpace(groupTagName))
        {
            tag = root.Find(groupTagName);
        }

        if (tag == null)
        {
            return null;
        }

        return tag.GetComponent<TMP_Text>();
    }

    private int GetGroupSize(int index)
    {
        if (index < 0)
        {
            return 0;
        }

        EnsureGroupIds();
        if (index >= groupIds.Count)
        {
            return 0;
        }

        int groupId = groupIds[index];
        int count = 0;
        for (int i = 0; i < groupIds.Count; i++)
        {
            if (groupIds[i] == groupId)
            {
                count++;
            }
        }

        return count;
    }

    private bool IsGrouped(int index)
    {
        if (index < 0)
        {
            return false;
        }

        EnsureGroupIds();
        if (index >= groupIds.Count)
        {
            return false;
        }

        return groupIds[index] == leaderGroupId;
    }

    private void SetGrouped(int index, bool grouped)
    {
        if (index < 0)
        {
            return;
        }

        EnsureGroupIds();
        if (index >= groupIds.Count)
        {
            return;
        }

        int targetGroup = grouped ? leaderGroupId : GetNextSoloGroupId(index);
        groupIds[index] = targetGroup;
        RebuildGroups();
        UpdateAllGroupIndicators();
    }

    public bool SendCharacterHome(GameObject character)
    {
        return TrySendCharacterHome(character, null) == SendHomeResult.Success;
    }

    public bool SendCharacterHome(GameObject character, LootContainer homeLootContainer)
    {
        return TrySendCharacterHome(character, homeLootContainer) == SendHomeResult.Success;
    }

    public SendHomeResult TrySendCharacterHome(GameObject character, LootContainer homeLootContainer)
    {
        if (character == null)
        {
            return SendHomeResult.InvalidCharacter;
        }

        if (squadCharacters == null)
        {
            return SendHomeResult.NotInSquad;
        }

        int index = squadCharacters.IndexOf(character);
        if (index < 0)
        {
            return SendHomeResult.NotInSquad;
        }

        CharacterData removedData = currentSquad != null && index < currentSquad.Count
            ? currentSquad[index]
            : null;

        if (removedData != null)
        {
            Maison maisonComponent = GetMaison();
            if (maisonComponent != null)
            {
                List<LootContainer> homeContainers = maisonComponent.ResolveMaisonLootContainers(homeLootContainer);
                if (!maisonComponent.TransferNonTorchItemsToHome(character, homeContainers))
                {
                    return SendHomeResult.StorageFull;
                }
            }
        }

        squadCharacters.RemoveAt(index);

        SquadUISettings ui = GetSquadUI();
        if (ui != null)
        {
            ui.RemoveUnitAt(index);
        }

        if (currentSquad != null && index < currentSquad.Count)
        {
            currentSquad.RemoveAt(index);
        }

        if (groupIds != null && index >= 0 && index < groupIds.Count)
        {
            groupIds.RemoveAt(index);
        }

        RebuildGroups();
        UpdateAllGroupIndicators();

        if (removedData != null)
        {
            SendCharacterToHub(removedData, character);
            HubRosterManager roster = HubRosterManager.Instance;
            if (roster != null)
            {
                roster.SetInSquad(removedData, false);
            }
        }

        ClampCursorIndex();
        UpdateCurrentCharacter();
        UpdateCursorPosition();

        if (squadCharacters.Count == 0)
        {
            currentCharacter = null;
        }

        return SendHomeResult.Success;
    }

    public bool TrySwapWithHubCharacter(CharacterData hubCharacter)
    {
        if (hubCharacter == null)
        {
            return false;
        }

        hubCharacter = GetRuntimeCharacter(hubCharacter);

        if (currentSquad == null || currentSquad.Count == 0)
        {
            return false;
        }

        if (currentSquad.Contains(hubCharacter))
        {
            return false;
        }

        int index = GetCurrentCharacterIndex();
        if (index < 0 || index >= currentSquad.Count)
        {
            return false;
        }

        CharacterData removed = currentSquad[index];
        GameObject oldInstance = squadCharacters != null && index < squadCharacters.Count
            ? squadCharacters[index]
            : null;

        if (oldInstance != null)
        {
            Maison maisonComponent = GetMaison();
            if (maisonComponent != null)
            {
                List<LootContainer> homeContainers = maisonComponent.ResolveMaisonLootContainers(null);
                if (!maisonComponent.HasHomeStorageForCharacter(oldInstance, homeContainers))
                {
                    return false;
                }
            }
        }

        Vector3 position = oldInstance != null ? oldInstance.transform.position : GetStarFormationPosition(index);
        Quaternion rotation = oldInstance != null ? oldInstance.transform.rotation : transform.rotation;

        GameObject newInstance = GetOrCreateCharacterInstance(hubCharacter, null, index);
        if (newInstance == null)
        {
            return false;
        }

        PlaceCharacter(newInstance, position, rotation, squadCharactersParent);

        currentSquad[index] = hubCharacter;
        RebuildGroups();
        UpdateAllGroupIndicators();

        if (squadCharacters == null)
        {
            squadCharacters = new List<GameObject>();
        }

        while (squadCharacters.Count <= index)
        {
            squadCharacters.Add(null);
        }

        squadCharacters[index] = newInstance;
        currentCharacter = newInstance;
        UpdateSquadUnitUI(index, hubCharacter);

        if (oldInstance != null)
        {
            Maison maisonComponent = GetMaison();
            if (maisonComponent != null)
            {
                maisonComponent.TransferNonTorchItemsToHome(oldInstance, maisonComponent.ResolveMaisonLootContainers(null));
            }
            SendCharacterToHub(removed, oldInstance);
        }

        HubRosterManager roster = HubRosterManager.Instance;
        if (roster != null)
        {
            roster.SetInSquad(hubCharacter, true);
            roster.SetInSquad(removed, false);
        }

        ClampCursorIndex();
        UpdateCurrentCharacter();
        UpdateCursorPosition();

        return true;
    }

    private int GetCurrentCharacterIndex()
    {
        int index = -1;
        if (currentCharacter != null && squadCharacters != null)
        {
            index = squadCharacters.IndexOf(currentCharacter);
        }

        if (index < 0 && GetSquadUnitCount() > 0)
        {
            index = Mathf.Clamp(currentCursorIndex, 0, currentSquad.Count - 1);
        }

        if (index < 0 && currentSquad != null && currentSquad.Count > 0)
        {
            index = 0;
        }

        return index;
    }

    private void UpdateSquadUnitUI(int index, CharacterData character)
    {
        if (index < 0 || index >= GetSquadUnitCount())
        {
            return;
        }

        if (character == null)
        {
            return;
        }

        SquadUISettings ui = GetSquadUI();
        if (ui != null)
        {
            ui.SetUnitPortrait(index, character.portrait);
            ResolveCharacterHealth(index, character, out int currentHp, out int maxHp);
            ui.SetUnitHealth(index, currentHp, maxHp);
        }
    }

    public void NotifyCharacterHealthChanged(SquadCharacterController controller)
    {
        if (controller == null || squadCharacters == null || squadCharacters.Count == 0)
        {
            return;
        }

        int index = squadCharacters.IndexOf(controller.gameObject);
        if (index < 0 || index >= GetSquadUnitCount())
        {
            return;
        }

        SquadUISettings ui = GetSquadUI();
        if (ui != null)
        {
            ui.SetUnitHealth(index, controller.CurrentHp, controller.MaxHp);
        }
    }

    private void ResolveCharacterHealth(int index, CharacterData character, out int currentHp, out int maxHp)
    {
        currentHp = character != null ? character.hp : 0;
        maxHp = currentHp;

        if (squadCharacters == null || index < 0 || index >= squadCharacters.Count)
        {
            return;
        }

        GameObject instance = squadCharacters[index];
        if (instance == null)
        {
            return;
        }

        SquadCharacterController controller = instance.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            return;
        }

        currentHp = controller.CurrentHp;
        maxHp = controller.MaxHp;
    }

    private void SendCharacterToHub(CharacterData character, GameObject instance)
    {
        if (character == null || instance == null)
        {
            return;
        }

        if (companionRegistry.TryGetValue(character, out CompanionRecord record))
        {
            Vector3 position = record.hubPosition;
            Quaternion rotation = record.hubRotation;
            Transform parent = record.hubParent;
            if (parent != null)
            {
                instance.transform.SetParent(parent, true);
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            return;
        }

        if (squadSpawnOrigin != null)
        {
            instance.transform.SetPositionAndRotation(squadSpawnOrigin.position, squadSpawnOrigin.rotation);
        }
    }

    public CharacterData GetRuntimeCharacter(CharacterData character)
    {
        if (!useRuntimeCharacterClones || character == null)
        {
            return character;
        }

        if (runtimeCharacters.Contains(character))
        {
            return character;
        }

        if (runtimeCharacterMap.TryGetValue(character, out CharacterData existing))
        {
            return existing;
        }

        string id = GetCharacterId(character);
        if (!string.IsNullOrWhiteSpace(id) && runtimeCharactersById.TryGetValue(id, out CharacterData byId))
        {
            if (runtimeCharacterSourceById.TryGetValue(id, out CharacterData source) && source != null && source != character)
            {
                WarnDuplicateCharacterId(id, source, character);
            }
            else
            {
                runtimeCharacterSourceById[id] = character;
                runtimeCharacterMap[character] = byId;
                return byId;
            }
        }

        CharacterData clone = Instantiate(character);
        clone.name = $"{character.name}_Runtime";
        clone.hideFlags = HideFlags.DontSave;
        if (clone.skills != null)
        {
            clone.skills = new List<Skill>(clone.skills);
        }

        if (clone.starterItems != null)
        {
            clone.starterItems = new List<Item>(clone.starterItems);
        }

        clone.inventoryItems = new List<Item>();
        clone.torchSecondsRemaining = 0;
        clone.torchEquipped = false;
        clone.inventoryInitialized = false;

        runtimeCharacterMap[character] = clone;
        runtimeCharacters.Add(clone);
        if (!string.IsNullOrWhiteSpace(id))
        {
            if (!runtimeCharactersById.ContainsKey(id))
            {
                runtimeCharactersById[id] = clone;
                runtimeCharacterSourceById[id] = character;
            }
            else if (!runtimeCharacterSourceById.TryGetValue(id, out CharacterData source) || source == character)
            {
                runtimeCharactersById[id] = clone;
                runtimeCharacterSourceById[id] = character;
            }
            else
            {
                WarnDuplicateCharacterId(id, source, character);
            }
        }

        return clone;
    }

    private void EnsureRuntimeSquad()
    {
        if (!useRuntimeCharacterClones || currentSquad == null)
        {
            return;
        }

        for (int i = 0; i < currentSquad.Count; i++)
        {
            currentSquad[i] = GetRuntimeCharacter(currentSquad[i]);
        }
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

    private void WarnDuplicateCharacterId(string id, CharacterData source, CharacterData duplicate)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (!runtimeCharacterIdWarnings.Add(id))
        {
            return;
        }

        string sourceName = source != null ? source.name : "Inconnu";
        string duplicateName = duplicate != null ? duplicate.name : "Inconnu";
        Debug.LogWarning($"SquadManager: characterId duplique '{id}' entre {sourceName} et {duplicateName}. Definis des IDs uniques pour eviter le partage de runtime.");
    }

    private CompanionRecord GetOrCreateRecord(CharacterData character)
    {
        if (!companionRegistry.TryGetValue(character, out CompanionRecord record))
        {
            record = new CompanionRecord();
            companionRegistry[character] = record;
        }

        return record;
    }

    void OnInteract()
    {
        Debug.Log("Utilise Interact");
        if (IsInputLocked())
        {
            return;
        }

        if (!charactersSelectionOn)
        {
            return;
        }

        if (squadCharacters == null || squadCharacters.Count == 0)
        {
            return;
        }

        ClampCursorIndex();
        UpdateCurrentCharacter();
    }

    void OnSouthButton()
    {
        OnInteract();
    }

    private void OnLeftShoulderPerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnLeftShoulder();
        }
    }

    void OnLeftShoulder()
    {
        Debug.Log("Utilise LeftShoulder");
        if (IsInputLocked())
        {
            return;
        }

        ToggleSquadPanel();
    }

    public void ToggleSquadPanel()
    {
        if (charactersSelectionOn)
        {
            CloseSquadPanel();
        }
        else
        {
            OpenSquadPanel();
        }
    }

    public void OpenSquadPanel()
    {
        if (charactersSelectionOn)
        {
            InputFocusStack.Push(this);
            return;
        }

        if (InputFocusStack.HasAnyFocus())
        {
            return;
        }

        UpdateLeaderGroupFromCurrent();
        charactersSelectionOn = true;
        InputFocusStack.Push(this);

        lastMoveDirection = 0;
        nextMoveTime = 0f;

        ClampCursorIndex();
        UpdateCurrentCharacter();
        UpdateCursorPosition();
        StopControlledCharacter();
        ApplySquadPanelVisibility(false);
    }

    public void CloseSquadPanel()
    {
        if (!charactersSelectionOn)
        {
            return;
        }

        charactersSelectionOn = false;
        InputFocusStack.Pop(this);

        lastMoveDirection = 0;
        nextMoveTime = 0f;
        UpdateLeaderGroupFromCurrent();
        ApplySquadPanelVisibility(false);
    }

    public void SetInputLocked(bool locked)
    {
        if (locked)
        {
            inputLockCount = Mathf.Max(0, inputLockCount + 1);
            if (inputLockCount == 1)
            {
                StopControlledCharacter();
            }
        }
        else
        {
            if (inputLockCount <= 0)
            {
                return;
            }

            inputLockCount = Mathf.Max(0, inputLockCount - 1);
        }
    }

    public bool IsInputLocked()
    {
        return inputLockCount > 0;
    }

    private void InitializeSquadPanel()
    {
        SquadUISettings ui = GetSquadUI();
        if (ui != null)
        {
            ui.InitializePanel(true);
        }
    }

    private void ApplySquadPanelVisibility(bool immediate)
    {
        SquadUISettings ui = GetSquadUI();
        if (ui == null)
        {
            return;
        }

        ui.ApplyPanelVisibility(true, immediate);
        ui.SetCursorVisible(charactersSelectionOn);
    }

    private void UpdateSquadPanelCursorVisibility()
    {
        SquadUISettings ui = GetSquadUI();
        if (ui != null)
        {
            ui.SetCursorVisible(charactersSelectionOn);
        }
    }

    private SquadUISettings GetSquadUI()
    {
        SquadUISettings ui = squadUISettings != null ? squadUISettings : SquadUISettings.Instance;
        if (ui == null && !warnedMissingSquadUI)
        {
            Debug.LogWarning("SquadManager: SquadUISettings non assigne. Le panel de squad ne pourra pas s'afficher.");
            warnedMissingSquadUI = true;
        }

        return ui;
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

        if (maison == null && !warnedMissingMaison)
        {
            Debug.LogWarning("SquadManager: Maison non assignee. Le stockage maison ne sera pas utilise.");
            warnedMissingMaison = true;
        }

        return maison;
    }

    private int GetSquadUnitCount()
    {
        SquadUISettings ui = GetSquadUI();
        return ui != null ? ui.GetUnitCount() : 0;
    }

    private GameObject GetSquadUnitAt(int index)
    {
        SquadUISettings ui = GetSquadUI();
        return ui != null ? ui.GetUnitAt(index) : null;
    }

    private float GetSquadUIMoveDeadzone()
    {
        SquadUISettings ui = GetSquadUI();
        return ui != null ? ui.moveDeadzone : 0.5f;
    }

    private float GetSquadUIInitialRepeatDelay()
    {
        SquadUISettings ui = GetSquadUI();
        return ui != null ? ui.initialRepeatDelay : 0.35f;
    }

    private float GetSquadUIRepeatInterval()
    {
        SquadUISettings ui = GetSquadUI();
        return ui != null ? ui.repeatInterval : 0.12f;
    }

    private bool GetSquadUIWrapCursor()
    {
        SquadUISettings ui = GetSquadUI();
        return ui != null && ui.wrapCursor;
    }
}
