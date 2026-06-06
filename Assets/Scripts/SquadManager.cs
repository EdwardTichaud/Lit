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
    [Tooltip("Nom du spawn point de debut en solo.")]
    public string soloStartSpawnPointName = "OriginSpawnPoint";
    [Tooltip("Nom du spawn point Maison.")]
    public string maisonSpawnPointName = "Maison_SpawnPoint";
    [Tooltip("Reference au composant Maison (auto-resolve si null).")]
    public Maison maison;

    [Header("Runtime Clones")]
    [SerializeField, Tooltip("Clone les CharacterData a l'execution pour ne pas modifier les assets.")]
    private bool useRuntimeCharacterClones = true;

    [Header("Flight")]
    [SerializeField, Tooltip("Active le moteur de vol local pour le personnage joueur selectionne.")]
    private bool useFlightMotorForLocalPlayer;
    [SerializeField, Tooltip("Ajoute les composants de vol manquants sur le personnage selectionne quand le mode vol est active.")]
    private bool autoInstallFlightMotorForLocalPlayer = true;

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
    private bool jumpRequested;
    private bool locomotionModeRequested;
    private bool triggerMuninRequested;
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
    private string lastLocalAssignmentRefreshLog = string.Empty;
    private SquadCharacterController activeFlightMotorController;

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
        LocalInputRouter.Jump += OnJumpPerformed;
        LocalInputRouter.Interact += OnInteractPerformed;
        LocalInputRouter.TriggerMunin += OnTriggerMuninPerformed;
        LocalInputRouter.TakeAll += OnTakeAllPerformed;
        LocalInputRouter.Return += OnReturnPerformed;
        LocalInputRouter.LeftShoulder += OnLeftShoulderPerformed;
        LocalInputRouter.LocomotionMode += OnLocomotionModePerformed;
        LocalPlayerContext.LocalCharacterChanged += OnLocalCharacterChanged;
    }

    void OnDisable()
    {
        LocalInputRouter.Jump -= OnJumpPerformed;
        LocalInputRouter.Interact -= OnInteractPerformed;
        LocalInputRouter.TriggerMunin -= OnTriggerMuninPerformed;
        LocalInputRouter.TakeAll -= OnTakeAllPerformed;
        LocalInputRouter.Return -= OnReturnPerformed;
        LocalInputRouter.LeftShoulder -= OnLeftShoulderPerformed;
        LocalInputRouter.LocomotionMode -= OnLocomotionModePerformed;
        LocalPlayerContext.LocalCharacterChanged -= OnLocalCharacterChanged;

        DeactivateFlightMotorController();
        InputFocusStack.Pop(this);
    }

    public void PrepareForRuntimeReset(string reason)
    {
        if (Instance == this)
        {
            Instance = null;
        }

        StopAllCoroutines();
        DeactivateFlightMotorController();
        LocalPlayerContext.Clear($"SquadManager.Reset:{reason}", LocalPlayerContext.Authority.MultiplayerAssignment);

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
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
            RefreshNetworkCharacters();
            UpdateCurrentCharacter();
            UpdateCursorPosition();
            UpdateSquadPanelCursorVisibility();
            ApplySquadPanelVisibility(true);
            RequestCrownReposition();
            StartCoroutine(RefreshNetworkCharactersRoutine());
            yield break;
        }

        Transform soloStartSpawnPoint = ResolveSoloStartSpawnPoint();
        if (soloStartSpawnPoint != null)
        {
            squadSpawnOrigin = soloStartSpawnPoint;
        }

        for (int i = 0; i < currentSquad.Count; i++)
        {
            CharacterData character = currentSquad[i];
            Transform spawnPoint = ResolveSoloSpawnPoint(i, soloStartSpawnPoint);

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
        SquadCharacterController[] controllers = FindObjectsByType<SquadCharacterController>();
#else
        SquadCharacterController[] controllers = FindObjectsByType<SquadCharacterController>();
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

            int index = ResolveSquadIndex(controller);
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

        ApplyLocalAssignmentContext();

        GameObject local = LocalPlayerUtils.GetControlledCharacter();
        currentCharacter = local;

        int localIndex = GetCurrentCharacterIndex();
        if (localIndex >= 0)
        {
            currentCursorIndex = localIndex;
            UpdateCursorPosition();
            RequestCrownReposition();
        }

        UpdateLeaderGroupFromCurrent();
        UpdateAllGroupIndicators();
    }

    private void ApplyLocalAssignmentContext()
    {
        if (!IsMultiplayerActive())
        {
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            return;
        }

        WorldInteractionService service = WorldInteractionService.Instance;
        if (service == null)
        {
            LogLocalAssignmentRefresh("world interaction service unavailable; preserving current local assignment");
            return;
        }

        if (!service.TryGetAssignedCharacterId(NetworkManager.Singleton.LocalClientId, out string characterId))
        {
            LogLocalAssignmentRefresh("local client assignment not ready; preserving current local assignment");
            return;
        }

        GameObject instance = ResolveCharacterInstanceById(characterId);
        if (instance == null)
        {
            LogLocalAssignmentRefresh(
                $"assigned character instance unresolved characterId='{characterId}'; preserving current local assignment");
            return;
        }

        lastLocalAssignmentRefreshLog = string.Empty;
        LocalPlayerContext.SetLocalCharacter(
            instance.transform,
            "squad_manager_refresh",
            LocalPlayerContext.Authority.Default);
        NetcodePlayerUtils.LogControlDecision(
            "local_assignment_refresh",
            instance,
            followerAiEnabled: false,
            waitingPointEnabled: false,
            movementMode: null,
            reason: "squad manager refreshed local assignment from assignment registry");
    }

    private void LogLocalAssignmentRefresh(string reason)
    {
        if (lastLocalAssignmentRefreshLog == reason)
        {
            return;
        }

        lastLocalAssignmentRefreshLog = reason;
        Debug.Log(
            $"[NetcodeControl] system='local_assignment_refresh' characterId='' ownerClientId=n/a assignedClientId=n/a localClientId={(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId.ToString() : "n/a")} isOwner=False isControlledLocally={(LocalPlayerContext.LocalCharacterRoot != null)} movementMode='pending' reason='{reason}'",
            this);
    }

    private GameObject ResolveCharacterInstanceById(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return null;
        }

        if (squadCharacters != null)
        {
            for (int i = 0; i < squadCharacters.Count; i++)
            {
                GameObject character = squadCharacters[i];
                if (character == null)
                {
                    continue;
                }

                if (NetcodeCharacterIdentity.MatchesCharacterId(character, characterId))
                {
                    return character;
                }
            }
        }

#if UNITY_2023_1_OR_NEWER
        SquadCharacterController[] controllers = FindObjectsByType<SquadCharacterController>();
#else
        SquadCharacterController[] controllers = FindObjectsByType<SquadCharacterController>();
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

    private int ResolveSquadIndex(SquadCharacterController controller)
    {
        if (controller == null || currentSquad == null)
        {
            return -1;
        }

        CharacterData data = controller.CharacterData;
        if (data != null)
        {
            int index = currentSquad.IndexOf(data);
            if (index >= 0)
            {
                return index;
            }
        }

        NetcodeCharacterIdentity identity = controller.GetComponent<NetcodeCharacterIdentity>();
        string characterId = identity != null && !string.IsNullOrWhiteSpace(identity.CharacterId)
            ? identity.CharacterId
            : GetCharacterId(data);
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return -1;
        }

        for (int i = 0; i < currentSquad.Count; i++)
        {
            CharacterData candidate = currentSquad[i];
            if (candidate != null && GetCharacterId(candidate) == characterId)
            {
                return i;
            }
        }

        return -1;
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

    private Transform ResolveSoloStartSpawnPoint()
    {
        if (string.IsNullOrWhiteSpace(soloStartSpawnPointName))
        {
            return null;
        }

        GameObject found = GameObject.Find(soloStartSpawnPointName);
        return found != null ? found.transform : null;
    }

    private Transform ResolveSoloSpawnPoint(int index, Transform soloStartSpawnPoint)
    {
        if (index == 0 && soloStartSpawnPoint != null)
        {
            return soloStartSpawnPoint;
        }

        if (squadSpawnPoints != null && index >= 0 && index < squadSpawnPoints.Count)
        {
            return squadSpawnPoints[index];
        }

        return null;
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
            ApplyMuninChargeStateToCharacterData(runtimeCharacter, entry);
            bool hasSavedInventory = entry.items != null && entry.items.Count > 0;
            bool hasSavedTorchState = entry.torchSeconds > 0 || entry.torchEquipped;
            bool shouldApplyInventory = entry.itemsInitialized || hasSavedInventory || hasSavedTorchState;

            bool isVersionZeroSave = pendingLoadData != null && pendingLoadData.dataVersion <= 0;
            bool hasStarterItems = runtimeCharacter != null
                && runtimeCharacter.starterItemsWithQuantity != null
                && runtimeCharacter.starterItemsWithQuantity.Count > 0;
            if (isVersionZeroSave && hasStarterItems && !hasSavedInventory && !hasSavedTorchState)
            {
                shouldApplyInventory = false;
            }

            if (shouldApplyInventory)
            {
                List<Item> items = BuildItemsFromEntry(entry);
                runtimeCharacter.SetInventory(items, entry.torchSeconds, entry.torchEquipped, true);
            }
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
            ApplyMuninChargeStateToInstance(instance, entry);

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

    private static void ApplyMuninChargeStateToCharacterData(CharacterData character, CharacterSaveEntry entry)
    {
        if (character == null || entry == null || !entry.muninChargesInitialized)
        {
            return;
        }

        int maxCharges = Mathf.Max(0, entry.muninMaxCharges);
        character.muninMaxCharges = maxCharges;
        character.muninChargesRemaining = Mathf.Clamp(entry.muninCharges, 0, maxCharges);
        character.muninChargesInitialized = true;
    }

    private static void ApplyMuninChargeStateToInstance(GameObject instance, CharacterSaveEntry entry)
    {
        if (instance == null || entry == null || !entry.muninChargesInitialized)
        {
            return;
        }

        MuninController munin = instance.GetComponentInChildren<MuninController>(true);
        if (munin == null)
        {
            return;
        }

        if (entry.muninMaxCharges > 0)
        {
            munin.SetMaxCharges(entry.muninMaxCharges, false);
        }

        munin.SetCharges(entry.muninCharges);
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
                Debug.LogWarning(
                    $"SquadManager: item sauvegarde introuvable pour characterId='{entry.characterId}' itemId='{stack.itemId}' quantity={stack.quantity}. L'item sera ignore lors de la restauration.",
                    this);
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
            DeactivateFlightMotorController();

            if (!charactersSelectionOn)
            {
                jumpRequested = false;
                return;
            }

            if (IsInputLocked())
            {
                jumpRequested = false;
                StopAllSquadCharacters();
                return;
            }

            moveInput = LocalInputRouter.MoveValue;

            if (GetSquadUnitCount() == 0)
            {
                return;
            }

            ClampCursorIndex();
            HandleCursorNavigation();
            UpdateCursorPosition();
            return;
        }

        if (IsInputLocked())
        {
            jumpRequested = false;
            StopAllSquadCharacters();
            return;
        }

        moveInput = LocalInputRouter.MoveValue;

        if (charactersSelectionOn)
        {
            DeactivateFlightMotorController();

            jumpRequested = false;
            if (GetSquadUnitCount() == 0)
            {
                return;
            }

            ClampCursorIndex();
            HandleCursorNavigation();
            UpdateCursorPosition();
            HandleGroupingInputs();
        }
        else
        {
            UpdateLeaderGroupFromCurrent();
            HandleControlledCharacterMovement();
            HandleMuninTrigger();
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
        int nextIndex = currentCursorIndex;
        bool wrap = GetSquadUIWrapCursor();

        for (int i = 0; i < count; i++)
        {
            nextIndex = GetNextCursorIndex(nextIndex, direction, count, wrap);
            if (nextIndex == currentCursorIndex && !wrap)
            {
                break;
            }

            if (IsSelectableIndex(nextIndex))
            {
                currentCursorIndex = nextIndex;
                return;
            }
        }
    }

    private int GetNextCursorIndex(int currentIndex, int direction, int count, bool wrap)
    {
        if (count <= 0)
        {
            return currentIndex;
        }

        int nextIndex = currentIndex + direction;
        if (wrap)
        {
            return (nextIndex % count + count) % count;
        }

        return Mathf.Clamp(nextIndex, 0, count - 1);
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
        currentCursorIndex = GetNearestSelectableIndex(currentCursorIndex);
    }

    private int GetNearestSelectableIndex(int preferredIndex)
    {
        int unitCount = GetSquadUnitCount();
        if (unitCount == 0)
        {
            return 0;
        }

        if (IsSelectableIndex(preferredIndex))
        {
            return preferredIndex;
        }

        for (int i = 0; i < unitCount; i++)
        {
            if (IsSelectableIndex(i))
            {
                return i;
            }
        }

        return Mathf.Clamp(preferredIndex, 0, unitCount - 1);
    }

    private bool IsSelectableIndex(int index)
    {
        if (index < 0 || currentSquad == null || index >= currentSquad.Count)
        {
            return false;
        }

        CharacterData character = currentSquad[index];
        if (character == null)
        {
            return false;
        }

        if (!IsMultiplayerActive())
        {
            return true;
        }

        WorldInteractionService service = WorldInteractionService.Instance;
        if (service == null || !service.IsSpawned)
        {
            return true;
        }

        string characterId = GetCharacterId(character);
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        if (!service.IsCharacterAssigned(characterId))
        {
            return true;
        }

        if (NetworkManager.Singleton == null)
        {
            return false;
        }

        if (service.TryGetAssignedCharacterId(NetworkManager.Singleton.LocalClientId, out string localId))
        {
            return localId == characterId;
        }

        return false;
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
        if (IsMultiplayerActive())
        {
            currentCharacter = LocalPlayerUtils.GetControlledCharacter();
            UpdateCrownPosition();
            return;
        }

        if (squadCharacters == null || squadCharacters.Count == 0)
        {
            currentCharacter = null;
            UpdateCrownPosition();
            SyncSingleplayerLocalCharacterContext();
            return;
        }

        if (charactersSelectionOn
            && currentCharacter != null
            && squadCharacters != null
            && squadCharacters.Contains(currentCharacter))
        {
            UpdateCrownPosition();
            SyncSingleplayerLocalCharacterContext();
            return;
        }

        if (currentCursorIndex < 0 || currentCursorIndex >= squadCharacters.Count)
        {
            currentCharacter = null;
            UpdateCrownPosition();
            SyncSingleplayerLocalCharacterContext();
            return;
        }

        currentCharacter = squadCharacters[currentCursorIndex];
        UpdateCrownPosition();
        SyncSingleplayerLocalCharacterContext();
    }

    private void SyncSingleplayerLocalCharacterContext()
    {
        if (IsMultiplayerActive())
        {
            return;
        }

        Transform currentRoot = currentCharacter != null ? currentCharacter.transform : null;
        if (currentRoot != null)
        {
            LocalPlayerContext.SetLocalCharacter(
                currentRoot,
                "squad_manager_singleplayer",
                LocalPlayerContext.Authority.Default);
            return;
        }

        LocalPlayerContext.Clear(
            "squad_manager_singleplayer",
            LocalPlayerContext.Authority.Default);
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
            DeactivateFlightMotorController();
            jumpRequested = false;
            locomotionModeRequested = false;
            return;
        }

        bool inputBlocked = InputFocusStack.HasAnyFocus();
        if (!inputBlocked && locomotionModeRequested && !IsMultiplayerActive())
        {
            useFlightMotorForLocalPlayer = true;
        }

        SquadCharacterController controller = currentCharacter.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            jumpRequested = false;
            locomotionModeRequested = false;
            return;
        }

        if (controller.HasUccLocomotionBridge)
        {
            useFlightMotorForLocalPlayer = false;
        }

        SquadCharacterController flightMotorController = RefreshFlightMotorController(controller);
        if (flightMotorController != null && flightMotorController.IsFlightMotorActive)
        {
            bool shoulderPressed = !inputBlocked && LocalInputRouter.RightShoulderPressed;
            flightMotorController.ApplyFlightMotorControlInput(
                inputBlocked ? Vector2.zero : moveInput,
                shoulderPressed,
                inputBlocked ? 0f : LocalInputRouter.FlightVerticalValue,
                !inputBlocked && jumpRequested,
                !inputBlocked && locomotionModeRequested);

            jumpRequested = false;
            locomotionModeRequested = false;
            return;
        }

        if (inputBlocked || controller.IsMovementInputSuppressed)
        {
            controller.SetSprintModifier(false);
            controller.Move(Vector2.zero);
            jumpRequested = false;
            locomotionModeRequested = false;
            return;
        }

        Vector2 rawMoveInput = moveInput;
        Vector2 worldMoveInput = controller.GetWorldSpaceInput(rawMoveInput);
        bool sprintRequested = LocalInputRouter.RightShoulderPressed;

        controller.SetSprintModifier(sprintRequested);
        controller.Move(rawMoveInput);

        if (locomotionModeRequested && controller.TryToggleUccHeightChange())
        {
            locomotionModeRequested = false;
        }

        if (jumpRequested)
        {
            controller.QueueCommittedJumpInput(worldMoveInput, isWorldSpace: true);
            controller.Jump();
        }

        jumpRequested = false;
        locomotionModeRequested = false;
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

        if (controller.IsFlightMotorActive)
        {
            controller.StopFlightMotorControl();
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

            if (controller.IsFlightMotorActive)
            {
                controller.StopFlightMotorControl();
                continue;
            }

            controller.Stop();
        }
    }

    private SquadCharacterController RefreshFlightMotorController(SquadCharacterController controller)
    {
        SquadCharacterController target = null;
        if (useFlightMotorForLocalPlayer && !IsMultiplayerActive() && controller != null)
        {
            target = controller;
        }

        if (activeFlightMotorController != null && activeFlightMotorController != target)
        {
            activeFlightMotorController.SetFlightMotorActive(false);
        }

        activeFlightMotorController = target;
        if (activeFlightMotorController == null)
        {
            return null;
        }

        if (!activeFlightMotorController.SetFlightMotorActive(true, autoInstallFlightMotorForLocalPlayer))
        {
            activeFlightMotorController = null;
            return null;
        }

        return activeFlightMotorController;
    }

    private void DeactivateFlightMotorController()
    {
        if (activeFlightMotorController == null)
        {
            return;
        }

        activeFlightMotorController.SetFlightMotorActive(false);
        activeFlightMotorController = null;
    }

    private void HandleMuninTrigger()
    {
        if (currentCharacter == null)
        {
            return;
        }

        if (!triggerMuninRequested)
        {
            return;
        }
        triggerMuninRequested = false;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return;
        }

        if (!LocalInputRouter.TryConsumeTriggerMunin())
        {
            return;
        }

        SquadCharacterController controller = currentCharacter.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            return;
        }

        controller.TriggerMunin();
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

        if (triggerMuninRequested)
        {
            if (LocalInputRouter.TryConsumeTriggerMunin())
            {
                SetGrouped(currentCursorIndex, true);
            }
        }

        takeAllRequested = false;
        triggerMuninRequested = false;
    }

    private void OnTriggerMuninPerformed(InputAction.CallbackContext context)
    {
        triggerMuninRequested = true;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (IsInputLocked())
        {
            return;
        }

        jumpRequested = true;
    }

    private void OnLocomotionModePerformed(InputAction.CallbackContext context)
    {
        if (IsInputLocked())
        {
            return;
        }

        locomotionModeRequested = true;
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

        bool isPlayerControlled = IsCharacterPlayerControlled(index);
        bool grouped = !isPlayerControlled && IsGrouped(index);
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

    private bool IsCharacterPlayerControlled(int index)
    {
        if (!IsMultiplayerActive())
        {
            return false;
        }

        if (currentSquad == null || index < 0 || index >= currentSquad.Count)
        {
            return false;
        }

        CharacterData character = currentSquad[index];
        if (character == null)
        {
            return false;
        }

        string characterId = GetCharacterId(character);
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        WorldInteractionService service = WorldInteractionService.Instance;
        if (service == null || !service.IsSpawned)
        {
            return false;
        }

        return service.IsCharacterAssigned(characterId);
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

    public bool SendCharacterHome(GameObject character, InteractableItem homeLootContainer)
    {
        return TrySendCharacterHome(character, homeLootContainer) == SendHomeResult.Success;
    }

    public SendHomeResult TrySendCharacterHome(GameObject character, InteractableItem homeLootContainer)
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
                List<InteractableItem> homeContainers = maisonComponent.ResolveMaisonLootContainers(homeLootContainer);
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
                List<InteractableItem> homeContainers = maisonComponent.ResolveMaisonLootContainers(null);
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
        if (IsMultiplayerActive())
        {
            GameObject local = LocalPlayerUtils.GetControlledCharacter();
            if (local != null)
            {
                if (squadCharacters != null)
                {
                    int localIndex = squadCharacters.IndexOf(local);
                    if (localIndex >= 0)
                    {
                        return localIndex;
                    }
                }

                SquadCharacterController localController = local.GetComponent<SquadCharacterController>();
                int resolvedIndex = ResolveSquadIndex(localController);
                if (resolvedIndex >= 0)
                {
                    return resolvedIndex;
                }
            }
        }

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

        WarnIfSourceCharacterHasRuntimeInventoryState(character);

        CharacterData clone = Instantiate(character);
        clone.name = $"{character.name}_Runtime";
        clone.hideFlags = HideFlags.DontSave;
        if (clone.skills != null)
        {
            clone.skills = new List<Skill>(clone.skills);
        }

        if (clone.starterItemsWithQuantity != null)
        {
            List<CharacterData.StarterItemStack> starterStacks = new List<CharacterData.StarterItemStack>(clone.starterItemsWithQuantity.Count);
            for (int i = 0; i < clone.starterItemsWithQuantity.Count; i++)
            {
                CharacterData.StarterItemStack entry = clone.starterItemsWithQuantity[i];
                if (entry == null)
                {
                    starterStacks.Add(null);
                    continue;
                }

                starterStacks.Add(new CharacterData.StarterItemStack
                {
                    item = entry.item,
                    quantity = entry.quantity
                });
            }

            clone.starterItemsWithQuantity = starterStacks;
        }

        clone.inventoryItems = new List<Item>();
        clone.equippedInteractionItems = new List<Item>();
        clone.torchSecondsRemaining = 0;
        clone.torchEquipped = false;
        clone.inventoryInitialized = false;
        clone.muninChargesRemaining = 0;
        clone.muninMaxCharges = 0;
        clone.muninChargesInitialized = false;

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

    private void WarnIfSourceCharacterHasRuntimeInventoryState(CharacterData character)
    {
        if (character == null || runtimeCharacters.Contains(character))
        {
            return;
        }

        int inventoryCount = character.inventoryItems != null ? character.inventoryItems.Count : 0;
        int equippedCount = character.equippedInteractionItems != null ? character.equippedInteractionItems.Count : 0;
        if (inventoryCount <= 0
            && equippedCount <= 0
            && !character.inventoryInitialized
            && character.torchSecondsRemaining <= 0
            && !character.torchEquipped)
        {
            return;
        }

        Debug.LogWarning(
            $"SquadManager: source CharacterData '{character.name}' contient deja un etat runtime avant clonage. inventoryInitialized={character.inventoryInitialized} inventoryCount={inventoryCount} equippedCount={equippedCount} torchSeconds={character.torchSecondsRemaining} torchEquipped={character.torchEquipped}. Cela suggere qu'un ScriptableObject a ete modifie en runtime.",
            character);
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

    private void HandleInteractInput()
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

        LocalInputRouter.ConsumeInteract();

        if (IsMultiplayerActive())
        {
            RequestCharacterSwitchFromCursor();
            return;
        }

        CommitSingleplayerCharacterSwitchFromCursor();
    }

    private void CommitSingleplayerCharacterSwitchFromCursor()
    {
        if (squadCharacters == null || squadCharacters.Count == 0)
        {
            return;
        }

        ClampCursorIndex();
        if (currentCursorIndex < 0 || currentCursorIndex >= squadCharacters.Count)
        {
            return;
        }

        GameObject targetCharacter = squadCharacters[currentCursorIndex];
        if (targetCharacter == null)
        {
            return;
        }

        currentCharacter = targetCharacter;
        UpdateLeaderGroupFromCurrent();
        UpdateCrownPosition();
        SyncSingleplayerLocalCharacterContext();
        RequestCrownReposition();
    }

    private void RequestCharacterSwitchFromCursor()
    {
        if (currentSquad == null || currentSquad.Count == 0)
        {
            return;
        }

        int index = Mathf.Clamp(currentCursorIndex, 0, currentSquad.Count - 1);
        CharacterData target = currentSquad[index];
        if (target == null)
        {
            return;
        }

        string characterId = GetCharacterId(target);
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return;
        }

        WorldInteractionService service = WorldInteractionService.Instance;
        if (service == null || !service.IsSpawned)
        {
            InfoBoxUI.TryShow("Service reseau indisponible.");
            return;
        }

        service.RequestCharacterSwitchServerRpc(characterId);
    }

    private void OnLeftShoulderPerformed(InputAction.CallbackContext context)
    {
        if (context.started || context.performed)
        {
            HandleLeftShoulderInput();
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            HandleInteractInput();
        }
    }

    private void OnReturnPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        if (!charactersSelectionOn)
        {
            return;
        }

        if (!InputFocusStack.HasFocus(this))
        {
            return;
        }

        CloseSquadPanel();
    }

    private void OnLocalCharacterChanged(Transform _)
    {
        currentCharacter = LocalPlayerContext.LocalCharacterRoot != null
            ? LocalPlayerContext.LocalCharacterRoot.gameObject
            : null;

        UpdateLeaderGroupFromCurrent();
        UpdateAllGroupIndicators();

        int index = GetCurrentCharacterIndex();
        if (index >= 0)
        {
            currentCursorIndex = index;
            UpdateCursorPosition();
        }

        UpdateCurrentCharacter();
        RequestCrownReposition();
    }

    private void HandleLeftShoulderInput()
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

        int index = GetCurrentCharacterIndex();
        if (index >= 0)
        {
            currentCursorIndex = index;
        }

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
            ui.InitializePanel(charactersSelectionOn);
        }
    }

    private void ApplySquadPanelVisibility(bool immediate)
    {
        SquadUISettings ui = GetSquadUI();
        if (ui == null)
        {
            return;
        }

        ui.ApplyPanelVisibility(charactersSelectionOn, immediate);
        ui.SetCursorVisible(charactersSelectionOn);
        ui.SetPanelActiveScale(charactersSelectionOn);
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
            maison = FindAnyObjectByType<Maison>();
#else
            maison = FindAnyObjectByType<Maison>();
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
