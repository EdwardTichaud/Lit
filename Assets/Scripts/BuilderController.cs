using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Unity.Collections;

// Gere la liste des constructions (par instance) pour la progression et les effets.
[RequireComponent(typeof(NetworkObject))]
public class BuilderController : NetworkBehaviour
{
    [System.Serializable]
    public class BuiltBuildingEntry
    {
        [Tooltip("Identifiant reseau unique.")]
        public ulong networkId;
        [Tooltip("Instance liee a cette entree.")]
        public BuildingInfoInteractable info;
        [Tooltip("Type de batiment.")]
        public Item building;
        [Tooltip("Niveau de cette instance.")]
        public int level = 1;
        [Tooltip("Position sauvegardee de l'instance.")]
        public Vector3 position;
    }

    public struct NetBuiltBuilding : INetworkSerializable, System.IEquatable<NetBuiltBuilding>
    {
        public ulong Id;
        public FixedString128Bytes ItemId;
        public int Level;
        public Vector3 Position;
        public Quaternion Rotation;

        public NetBuiltBuilding(ulong id, string itemId, int level, Vector3 position, Quaternion rotation)
        {
            Id = id;
            ItemId = new FixedString128Bytes(itemId ?? string.Empty);
            Level = level;
            Position = position;
            Rotation = rotation;
        }

        public bool Equals(NetBuiltBuilding other)
        {
            return Id == other.Id
                && ItemId.Equals(other.ItemId)
                && Level == other.Level
                && Position == other.Position
                && Rotation == other.Rotation;
        }

        public override bool Equals(object obj)
        {
            return obj is NetBuiltBuilding other && Equals(other);
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(Id, ItemId.GetHashCode(), Level, Position.GetHashCode(), Rotation.GetHashCode());
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref ItemId);
            serializer.SerializeValue(ref Level);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
        }
    }

    public sealed class RequirementAvailability
    {
        public RequirementAvailability(string recipeId, bool useHomeResources)
        {
            RecipeId = recipeId ?? string.Empty;
            UseHomeResources = useHomeResources;
        }

        public string RecipeId { get; }
        public bool UseHomeResources { get; }
        public Dictionary<Item, int> RequiredCounts { get; } = new Dictionary<Item, int>();
        public Dictionary<Item, int> PlayerContribution { get; } = new Dictionary<Item, int>();
        public Dictionary<Item, int> StorageContribution { get; } = new Dictionary<Item, int>();
        public bool Craftable { get; set; }
        public string FailureReason { get; set; } = string.Empty;

        public int GetRequired(Item item)
        {
            return item != null && RequiredCounts.TryGetValue(item, out int value) ? value : 0;
        }

        public int GetPlayerContribution(Item item)
        {
            return item != null && PlayerContribution.TryGetValue(item, out int value) ? value : 0;
        }

        public int GetStorageContribution(Item item)
        {
            return item != null && StorageContribution.TryGetValue(item, out int value) ? value : 0;
        }

        public int GetCombinedContribution(Item item)
        {
            return GetPlayerContribution(item) + GetStorageContribution(item);
        }
    }

    [Header("Available Buildings")]
    [Tooltip("Tous les items building connus (pour la persistence/upgrade).")]
    public List<Item> availableBuildings = new List<Item>();

    [Header("Built")]
    [Tooltip("Instances construites (niveau par instance).")]
    public List<BuiltBuildingEntry> builtBuildings = new List<BuiltBuildingEntry>();
    [Tooltip("Applique les effets aux membres de la squad au lieu du personnage controle.")]
    public bool applyEffectsToAllSquad = false;

    [Header("Network Resources")]
    [Tooltip("Autorise l'utilisation des coffres maison pour les constructions.")]
    public bool useHomeResourcesForBuild = true;
    [Tooltip("Autorise l'utilisation des coffres maison pour le craft.")]
    public bool useHomeResourcesForCraft = true;
    [Tooltip("Distance max autorisee entre le joueur et la position de construction.")]
    public float networkBuildMaxDistance = 6f;
    [Tooltip("Distance max autorisee pour interagir/crafter sur un batiment.")]
    public float networkInteractDistance = 2.5f;

    public event System.Action BuildingsChanged;

    private readonly NetworkList<NetBuiltBuilding> netBuiltBuildings = new NetworkList<NetBuiltBuilding>(
        null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    [SerializeField, HideInInspector]
    private ulong nextNetBuildingId = 1;
    private readonly Dictionary<ulong, BuildingInfoInteractable> netBuildingLookup = new Dictionary<ulong, BuildingInfoInteractable>();

    [Header("Buildings Root")]
    [Tooltip("Parent des constructions instanciees.")]
    public Transform buildingsRoot;
    [Tooltip("Nom du GameObject racine (fallback si root non assigne).")]
    public string buildingsRootName = "Buildings";
    [Tooltip("Cherche automatiquement le root par nom si manquant.")]
    public bool autoFindBuildingsRoot = true;
    [Tooltip("Cree le root si introuvable.")]
    public bool autoCreateBuildingsRoot = false;

    [Header("Interaction - Voice Lines")]
    [Tooltip("Joue une voice line lors de l'interaction.")]
    public bool playVoiceLineOnInteract = true;
    [Tooltip("Ouvre le panel de construction lors de l'interaction.")]
    public bool openPanelOnInteract = true;
    [Tooltip("Panel de construction (optionnel, sinon auto-detecte).")]
    public BuildingPanelController buildingPanel;
    [Tooltip("Interaction disponible uniquement a proximite.")]
    public bool requireProximity = true;
    [Tooltip("Trigger d'interaction. Laisse vide pour auto-detecter.")]
    public Collider interactionTrigger;
    [Tooltip("Cooldown entre deux voice lines.")]
    public float voiceLineCooldown = 0.2f;

    [SerializeField, HideInInspector]
    private List<Item> existingBuildings = new List<Item>();
    [SerializeField, HideInInspector]
    private bool isRefreshingAvailableBuildings;

    private readonly List<GameObject> charactersInRange = new List<GameObject>();
    private readonly Dictionary<GameObject, int> characterColliderCounts = new Dictionary<GameObject, int>();
    private GameObject currentCharacter;
    private bool useSelfTriggerEvents;
    private LocalVoiceLineController voiceLineController;
    private float nextVoiceLineTime;
    private Maison cachedMaison;
    private readonly Dictionary<string, string> lastRequirementAnalysisLogs = new Dictionary<string, string>();
    private bool isNotifyingBuildingsChanged;
    private bool pendingBuildingsChangedNotification;

    private void Awake()
    {
        ResolveBuildingsRoot();
        InitializeInteractionTrigger();
        voiceLineController = GetComponent<LocalVoiceLineController>();
    }

    private void Start()
    {
        if (IsNetworked() && !IsServer)
        {
            return;
        }

        InitializeBuiltBuildingsFromList();
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;

        charactersInRange.Clear();
        characterColliderCounts.Clear();
        currentCharacter = null;
    }

    public override void OnNetworkSpawn()
    {
        netBuiltBuildings.OnListChanged += OnNetBuiltBuildingsChanged;
        if (IsServer)
        {
            SyncNetBuiltBuildingsFromLocal();
        }
        else
        {
            ApplyNetBuiltBuildings();
        }
    }

    public override void OnNetworkDespawn()
    {
        netBuiltBuildings.OnListChanged -= OnNetBuiltBuildingsChanged;
    }


    private static GameObject GetControlledCharacter()
    {
        return LocalPlayerUtils.GetControlledCharacter();
    }

    private static bool IsNetworked()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    private void OnNetBuiltBuildingsChanged(NetworkListEvent<NetBuiltBuilding> change)
    {
        if (IsServer)
        {
            return;
        }

        ApplyNetBuiltBuildings();
    }

    private void SyncNetBuiltBuildingsFromLocal(bool notifyChanges = true)
    {
        if (!IsServer)
        {
            return;
        }

        netBuiltBuildings.Clear();
        netBuildingLookup.Clear();

        ulong maxId = 0;
        if (builtBuildings != null)
        {
            for (int i = 0; i < builtBuildings.Count; i++)
            {
                BuiltBuildingEntry entry = builtBuildings[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.networkId == 0)
                {
                    entry.networkId = ++maxId;
                }
                else
                {
                    maxId = System.Math.Max(maxId, entry.networkId);
                }

                Item building = entry.building;
                if (building == null && entry.info != null)
                {
                    building = entry.info.BuildingItem;
                }

                if (building == null || !building.isBuilding)
                {
                    continue;
                }

                int level = entry.info != null ? entry.info.Level : entry.level;
                Vector3 position = entry.info != null ? entry.info.transform.position : entry.position;
                Quaternion rotation = entry.info != null ? entry.info.transform.rotation : Quaternion.identity;

                if (entry.info != null)
                {
                    entry.info.SetNetworkBuildingId(entry.networkId);
                    netBuildingLookup[entry.networkId] = entry.info;
                }

                netBuiltBuildings.Add(new NetBuiltBuilding(entry.networkId, GetBuildingItemId(building), Mathf.Max(1, level), position, rotation));
            }
        }

        if (maxId >= nextNetBuildingId)
        {
            nextNetBuildingId = maxId + 1;
        }

        if (notifyChanges)
        {
            NotifyBuildingsChanged();
        }
    }

    private void ApplyNetBuiltBuildings()
    {
        HashSet<ulong> seen = new HashSet<ulong>();
        if (netBuiltBuildings != null)
        {
            for (int i = 0; i < netBuiltBuildings.Count; i++)
            {
                NetBuiltBuilding entry = netBuiltBuildings[i];
                seen.Add(entry.Id);

                Item building = ResolveBuildingItem(entry.ItemId.ToString());
                if (building == null || !building.isBuilding)
                {
                    continue;
                }

                if (!netBuildingLookup.TryGetValue(entry.Id, out BuildingInfoInteractable info) || info == null)
                {
                    info = ResolveExistingRuntimeBuilding(entry, building);
                    if (info == null)
                    {
                        info = SpawnNetBuildingInstance(building, entry.Position, entry.Rotation, entry.Level, entry.Id);
                    }
                    else
                    {
                        netBuildingLookup[entry.Id] = info;
                        info.MarkPresentationOrigin("snapshot_reconstruction", overwrite: false);
                        info.RefreshPresentation("network_sync_existing");
                        LogBuildingSync(
                            "building_reconstructed",
                            info,
                            $"network sync reused existing runtime building entryId={entry.Id} level={entry.Level}");
                    }
                }

                if (info != null)
                {
                    UpdateNetBuildingInfo(info, building, entry);
                }
            }
        }

        if (netBuildingLookup.Count > 0)
        {
            List<ulong> toRemove = new List<ulong>();
            foreach (KeyValuePair<ulong, BuildingInfoInteractable> pair in netBuildingLookup)
            {
                if (!seen.Contains(pair.Key))
                {
                    if (pair.Value != null)
                    {
                        Destroy(pair.Value.gameObject);
                    }
                    toRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                netBuildingLookup.Remove(toRemove[i]);
            }
        }

        if (builtBuildings == null)
        {
            builtBuildings = new List<BuiltBuildingEntry>();
        }
        else
        {
            builtBuildings.Clear();
        }

        for (int i = 0; i < netBuiltBuildings.Count; i++)
        {
            NetBuiltBuilding entry = netBuiltBuildings[i];
            Item building = ResolveBuildingItem(entry.ItemId.ToString());
            if (building == null)
            {
                continue;
            }

            netBuildingLookup.TryGetValue(entry.Id, out BuildingInfoInteractable info);
            builtBuildings.Add(new BuiltBuildingEntry
            {
                networkId = entry.Id,
                info = info,
                building = building,
                level = Mathf.Max(1, entry.Level),
                position = entry.Position
            });
        }

        SyncBuildingCurrentLevelsFromBuiltList();
        NotifyBuildingsChanged();
    }

    private void NotifyBuildingsChanged()
    {
        if (isNotifyingBuildingsChanged)
        {
            pendingBuildingsChangedNotification = true;
            return;
        }

        isNotifyingBuildingsChanged = true;
        try
        {
            BuildingsChanged?.Invoke();
        }
        finally
        {
            isNotifyingBuildingsChanged = false;
        }

        if (pendingBuildingsChangedNotification)
        {
            pendingBuildingsChangedNotification = false;
            NotifyBuildingsChanged();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useSelfTriggerEvents)
        {
            return;
        }

        HandleCharacterEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!useSelfTriggerEvents)
        {
            return;
        }

        HandleCharacterExit(other);
    }

    public void NotifyTriggerEnter(Collider other)
    {
        HandleCharacterEnter(other);
    }

    public void NotifyTriggerExit(Collider other)
    {
        HandleCharacterExit(other);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        HandleInteract();
    }

    private void HandleInteract()
    {
        if (!CanProcessInteract())
        {
            return;
        }

        RefreshCurrentCharacter();
        if (requireProximity && currentCharacter == null)
        {
            GameObject controlled = GetControlledCharacter();
            if (controlled == null || interactionTrigger == null || !interactionTrigger.bounds.Contains(controlled.transform.position))
            {
                return;
            }

            currentCharacter = controlled;
        }

        if (!openPanelOnInteract && !playVoiceLineOnInteract)
        {
            return;
        }

        LocalInputRouter.ConsumeInteract();

        if (openPanelOnInteract)
        {
            OpenBuildingPanel();
        }

        if (!playVoiceLineOnInteract)
        {
            return;
        }

        if (Time.time < nextVoiceLineTime)
        {
            return;
        }

        if (voiceLineController == null)
        {
            voiceLineController = GetComponent<LocalVoiceLineController>();
        }

        if (voiceLineController != null && voiceLineController.PlayRandomVoiceLine())
        {
            nextVoiceLineTime = Time.time + Mathf.Max(0f, voiceLineCooldown);
        }
    }

    private bool CanProcessInteract()
    {
        return !InputFocusStack.HasAnyFocus();
    }

    private void OpenBuildingPanel()
    {
        BuildingPanelController panel = buildingPanel;
        if (panel == null)
        {
#if UNITY_2023_1_OR_NEWER
            panel = FindFirstObjectByType<BuildingPanelController>();
#else
            panel = FindObjectOfType<BuildingPanelController>();
#endif
        }

        if (panel == null)
        {
            Debug.LogWarning("BuilderController: BuildingPanelController introuvable.");
            return;
        }

        panel.OpenPanel(this);
    }

    private void HandleCharacterEnter(Collider other)
    {
        if (other == null || other.isTrigger)
        {
            return;
        }

        GameObject character = GetSquadCharacter(other);
        if (character == null)
        {
            return;
        }

        bool firstCollider = RegisterCharacterCollider(character);
        if (firstCollider && !charactersInRange.Contains(character))
        {
            charactersInRange.Add(character);
        }

        RefreshCurrentCharacter();
    }

    private void HandleCharacterExit(Collider other)
    {
        if (other == null || other.isTrigger)
        {
            return;
        }

        GameObject character = GetSquadCharacter(other);
        if (character == null)
        {
            return;
        }

        if (!UnregisterCharacterCollider(character))
        {
            return;
        }

        charactersInRange.Remove(character);
        if (character == currentCharacter)
        {
            currentCharacter = null;
        }

        RefreshCurrentCharacter();
    }

    private void RefreshCurrentCharacter()
    {
        GameObject controlled = GetControlledCharacter();
        if (controlled != null && charactersInRange.Contains(controlled))
        {
            currentCharacter = controlled;
            return;
        }

        currentCharacter = null;
    }

    public void EnsureAvailableBuildings()
    {
        if (availableBuildings == null)
        {
            availableBuildings = new List<Item>();
        }

        bool needsRefresh = availableBuildings.Count == 0;
        if (!needsRefresh)
        {
            for (int i = 0; i < availableBuildings.Count; i++)
            {
                if (availableBuildings[i] == null)
                {
                    needsRefresh = true;
                    break;
                }
            }
        }

        if (needsRefresh)
        {
            RefreshAvailableBuildings();
        }
    }

    public void RefreshAvailableBuildings()
    {
        if (isRefreshingAvailableBuildings)
        {
            return;
        }

        isRefreshingAvailableBuildings = true;
        try
        {
            if (availableBuildings == null)
            {
                availableBuildings = new List<Item>();
            }
            else
            {
                availableBuildings.Clear();
            }

            if (existingBuildings != null)
            {
                for (int i = 0; i < existingBuildings.Count; i++)
                {
                    AddAvailableBuilding(existingBuildings[i]);
                }
            }

#if UNITY_2023_1_OR_NEWER
            BuildingInfoInteractable[] infos = FindObjectsByType<BuildingInfoInteractable>(FindObjectsSortMode.None);
#else
            BuildingInfoInteractable[] infos = FindObjectsOfType<BuildingInfoInteractable>();
#endif
            if (infos == null)
            {
                return;
            }

            for (int i = 0; i < infos.Length; i++)
            {
                BuildingInfoInteractable info = infos[i];
                if (info == null)
                {
                    continue;
                }

                Item item = info.BuildingItem;
                if (item == null && !string.IsNullOrWhiteSpace(info.BuildingItemId))
                {
                    item = ResolveBuildingItem(info.BuildingItemId);
                }

                AddAvailableBuilding(item);
            }
        }
        finally
        {
            isRefreshingAvailableBuildings = false;
        }
    }

    public void EnsureBuiltBuildings()
    {
        EnsureBuiltBuildings(true);
    }

    public void EnsureBuiltBuildings(bool synchronizeNetworkState)
    {
        if (builtBuildings == null)
        {
            builtBuildings = new List<BuiltBuildingEntry>();
        }

        bool needsRefresh = builtBuildings.Count == 0;
        if (!needsRefresh)
        {
            for (int i = 0; i < builtBuildings.Count; i++)
            {
                if (builtBuildings[i] == null || builtBuildings[i].building == null)
                {
                    needsRefresh = true;
                    break;
                }
            }
        }

        if (needsRefresh)
        {
            RefreshBuiltBuildings();
        }
        else
        {
            SyncBuiltBuildingEntries();
        }

        SyncBuildingCurrentLevelsFromBuiltList();
        if (synchronizeNetworkState && IsNetworked() && IsServer)
        {
            SyncNetBuiltBuildingsFromLocal();
        }
    }

    public void RefreshBuiltBuildings()
    {
        if (builtBuildings == null)
        {
            builtBuildings = new List<BuiltBuildingEntry>();
        }
        else
        {
            builtBuildings.Clear();
        }

        EnsureAvailableBuildings();

#if UNITY_2023_1_OR_NEWER
        BuildingInfoInteractable[] infos = FindObjectsByType<BuildingInfoInteractable>(FindObjectsSortMode.None);
#else
        BuildingInfoInteractable[] infos = FindObjectsOfType<BuildingInfoInteractable>();
#endif
        int added = 0;
        Transform root = ResolveBuildingsRoot();
        if (infos != null)
        {
            for (int i = 0; i < infos.Length; i++)
            {
                BuildingInfoInteractable info = infos[i];
                if (info == null)
                {
                    continue;
                }

                Item item = info.BuildingItem;
                if (item == null && !string.IsNullOrWhiteSpace(info.BuildingItemId))
                {
                    item = ResolveBuildingItem(info.BuildingItemId);
                }

                if (item == null || !item.isBuilding)
                {
                    continue;
                }

                if (root != null && !info.transform.IsChildOf(root))
                {
                    EnsureBuildingParent(info.transform);
                }

                builtBuildings.Add(new BuiltBuildingEntry
                {
                    info = info,
                    building = item,
                    level = Mathf.Max(1, info.Level),
                    position = info.transform.position
                });
                UpdateBuildingCurrentLevel(item, info.Level);
                added++;
            }
        }

        SyncBuildingCurrentLevelsFromBuiltList();
    }

    public void RegisterBuiltBuilding(Item building, int levelValue = 1, BuildingInfoInteractable info = null)
    {
        if (building == null || !building.isBuilding)
        {
            return;
        }

        UpdateBuildingCurrentLevel(building, levelValue);

        if (builtBuildings == null)
        {
            builtBuildings = new List<BuiltBuildingEntry>();
        }

        if (info != null)
        {
            EnsureBuildingParent(info.transform);
            if (IsNetworked() && IsServer && info.NetworkBuildingId == 0)
            {
                info.SetNetworkBuildingId(nextNetBuildingId++);
            }
            if (info.NetworkBuildingId != 0)
            {
                PersistentWorldSceneInstaller.EnsureRuntimeBuildingInstance(info, building, info.NetworkBuildingId);
            }
            for (int i = 0; i < builtBuildings.Count; i++)
            {
                BuiltBuildingEntry entry = builtBuildings[i];
                if (entry != null && entry.info == info)
                {
                    entry.building = building;
                    entry.level = Mathf.Max(1, levelValue);
                    entry.position = info.transform.position;
                    entry.networkId = info.NetworkBuildingId;
                    AddAvailableBuilding(building);
                    UpsertNetBuiltBuilding(entry);
                    NotifyBuildingsChanged();
                    return;
                }
            }
        }

        BuiltBuildingEntry newEntry = new BuiltBuildingEntry
        {
            info = info,
            building = building,
            level = Mathf.Max(1, levelValue),
            position = info != null ? info.transform.position : Vector3.zero,
            networkId = info != null ? info.NetworkBuildingId : 0
        };
        if (IsNetworked() && IsServer && newEntry.networkId == 0)
        {
            newEntry.networkId = nextNetBuildingId++;
            if (info != null)
            {
                info.SetNetworkBuildingId(newEntry.networkId);
            }
        }

        if (info != null && newEntry.networkId != 0)
        {
            PersistentWorldSceneInstaller.EnsureRuntimeBuildingInstance(info, building, newEntry.networkId);
        }

        builtBuildings.Add(newEntry);

        AddAvailableBuilding(building);
        UpsertNetBuiltBuilding(newEntry);
        NotifyBuildingsChanged();
    }

    private BuildingInfoInteractable SpawnNetBuildingInstance(Item building, Vector3 position, Quaternion rotation, int level, ulong networkId)
    {
        if (building == null || !building.isBuilding)
        {
            return null;
        }

        GameObject prefab = building.buildingPrefab != null ? building.buildingPrefab : building.worldPrefab;
        if (prefab == null)
        {
            return null;
        }

        Transform root = ResolveBuildingsRoot();
        GameObject instance = root != null
            ? Instantiate(prefab, position, rotation, root)
            : Instantiate(prefab, position, rotation);
        if (instance == null)
        {
            return null;
        }

        BuildingInfoInteractable info = instance.GetComponent<BuildingInfoInteractable>();
        if (info == null)
        {
            info = instance.AddComponent<BuildingInfoInteractable>();
        }

        info.Initialize(GetBuildingItemId(building), building, Mathf.Max(1, level));
        info.SetNetworkBuildingId(networkId);
        info.MarkPresentationOrigin("runtime_spawn", overwrite: false);
        EnsureBuildingParent(instance.transform);
        netBuildingLookup[networkId] = info;
        PersistentWorldSceneInstaller.EnsureRuntimeBuildingInstance(info, building, networkId);
        info.RefreshPresentation("runtime_spawn");
        LogBuildingSync(
            "building_runtime_spawned",
            info,
            $"runtime building spawned level={level} networkId={networkId}");

        InteractableItem container = instance.GetComponentInChildren<InteractableItem>();
        if (container != null)
        {
            container.interactableCategory = InteractableItem.InteractableCategory.Container;
            container.representedItem = building;
        }

        if (building.isHomeChest)
        {
            TryAssignMaisonChestTag(instance);
            if (container != null)
            {
                EnsureHomeChestDefaults(container);
            }
        }

        return info;
    }

    private void UpdateNetBuildingInfo(BuildingInfoInteractable info, Item building, NetBuiltBuilding entry)
    {
        if (info == null || building == null)
        {
            return;
        }

        int previousLevel = info.Level;
        info.SetNetworkBuildingId(entry.Id);
        if (info.BuildingItem != building)
        {
            info.Initialize(GetBuildingItemId(building), building, Mathf.Max(1, entry.Level));
        }
        else
        {
            info.SetLevel(Mathf.Max(1, entry.Level));
        }

        info.transform.SetPositionAndRotation(entry.Position, entry.Rotation);
        PersistentWorldSceneInstaller.EnsureRuntimeBuildingInstance(info, building, entry.Id);
        info.RefreshPresentation(previousLevel != entry.Level ? "network_upgrade_refresh" : "network_sync_refresh");
        LogBuildingSync(
            previousLevel != entry.Level ? "building_upgrade_refresh_callback" : "building_network_sync",
            info,
            $"net update previousLevel={previousLevel} syncedLevel={entry.Level} visualRefreshRan=true");
    }

    private void UpsertNetBuiltBuilding(BuiltBuildingEntry entry)
    {
        if (!IsNetworked() || !IsServer || entry == null)
        {
            return;
        }

        if (entry.building == null || !entry.building.isBuilding)
        {
            return;
        }

        if (entry.networkId == 0)
        {
            return;
        }

        Vector3 position = entry.info != null ? entry.info.transform.position : entry.position;
        Quaternion rotation = entry.info != null ? entry.info.transform.rotation : Quaternion.identity;
        NetBuiltBuilding netEntry = new NetBuiltBuilding(entry.networkId, GetBuildingItemId(entry.building), Mathf.Max(1, entry.level), position, rotation);

        int index = FindNetBuiltIndex(entry.networkId);
        if (index >= 0)
        {
            netBuiltBuildings[index] = netEntry;
        }
        else
        {
            netBuiltBuildings.Add(netEntry);
        }
    }

    private int FindNetBuiltIndex(ulong id)
    {
        for (int i = 0; i < netBuiltBuildings.Count; i++)
        {
            if (netBuiltBuildings[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    public Transform GetBuildingsRoot()
    {
        return ResolveBuildingsRoot();
    }

    public void EnsureBuildingParent(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Transform root = ResolveBuildingsRoot();
        if (root == null || target == root)
        {
            return;
        }

        if (!target.IsChildOf(root))
        {
            NetworkObject targetNetworkObject = target.GetComponent<NetworkObject>();
            if (targetNetworkObject != null && (!IsNetworked() || !IsServer))
            {
                PersistentWorldDebug.Log(
                    $"builder parent skipped reason='{(!IsNetworked() ? "network_not_listening" : "non_server_client")}' target='{PersistentWorldDebug.DescribeTransform(target)}' root='{PersistentWorldDebug.DescribeTransform(root)}'",
                    this);
                return;
            }

            target.SetParent(root, true);
        }
    }

    public void ClearBuiltBuildings(bool destroyInstances)
    {
        if (builtBuildings == null)
        {
            builtBuildings = new List<BuiltBuildingEntry>();
        }

        if (destroyInstances)
        {
            Transform root = ResolveBuildingsRoot();
            if (root != null)
            {
                for (int i = root.childCount - 1; i >= 0; i--)
                {
                    Transform child = root.GetChild(i);
                    if (child != null && child.GetComponentInChildren<BuildingInfoInteractable>(true) != null)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
            else
            {
                for (int i = 0; i < builtBuildings.Count; i++)
                {
                    BuiltBuildingEntry entry = builtBuildings[i];
                    if (entry != null && entry.info != null)
                    {
                        Destroy(entry.info.gameObject);
                    }
                }
            }
        }

        builtBuildings.Clear();
        SyncBuildingCurrentLevelsFromBuiltList();

        if (IsNetworked() && IsServer)
        {
            netBuiltBuildings.Clear();
            netBuildingLookup.Clear();
            nextNetBuildingId = 1;
            NotifyBuildingsChanged();
        }
    }

    private void InitializeBuiltBuildingsFromList()
    {
        if (builtBuildings == null || builtBuildings.Count == 0)
        {
            return;
        }

        EnsureAvailableBuildings();
        Transform root = ResolveBuildingsRoot();

        for (int i = 0; i < builtBuildings.Count; i++)
        {
            BuiltBuildingEntry entry = builtBuildings[i];
            if (entry == null)
            {
                continue;
            }

            if (entry.info != null)
            {
                if (entry.building == null)
                {
                    entry.building = entry.info.BuildingItem;
                }

                entry.level = Mathf.Max(1, entry.info.Level);
                entry.position = entry.info.transform.position;
                EnsureBuildingParent(entry.info.transform);
                if (IsNetworked() && IsServer)
                {
                    if (entry.networkId == 0)
                    {
                        entry.networkId = nextNetBuildingId++;
                    }

                    entry.info.SetNetworkBuildingId(entry.networkId);
                    netBuildingLookup[entry.networkId] = entry.info;
                    UpsertNetBuiltBuilding(entry);
                }
                if (entry.building != null)
                {
                    UpdateBuildingCurrentLevel(entry.building, entry.level);
                    AddAvailableBuilding(entry.building);
                }
                continue;
            }

            if (entry.building == null || !entry.building.isBuilding)
            {
                continue;
            }

            GameObject prefab = entry.building.buildingPrefab != null ? entry.building.buildingPrefab : entry.building.worldPrefab;
            if (prefab == null)
            {
                continue;
            }

            Vector3 position = entry.position;
            Quaternion rotation = Quaternion.identity;
            GameObject instance = root != null
                ? Instantiate(prefab, position, rotation, root)
                : Instantiate(prefab, position, rotation);
            if (instance == null)
            {
                continue;
            }

            BuildingInfoInteractable info = instance.GetComponent<BuildingInfoInteractable>();
            if (info == null)
            {
                info = instance.AddComponent<BuildingInfoInteractable>();
            }

            info.Initialize(GetBuildingItemId(entry.building), entry.building, Mathf.Max(1, entry.level));
            entry.info = info;
            entry.position = instance.transform.position;
            entry.level = info.Level;
            if (IsNetworked() && IsServer)
            {
                if (entry.networkId == 0)
                {
                    entry.networkId = nextNetBuildingId++;
                }

                info.SetNetworkBuildingId(entry.networkId);
                netBuildingLookup[entry.networkId] = info;
                UpsertNetBuiltBuilding(entry);
            }
            UpdateBuildingCurrentLevel(entry.building, entry.level);
            AddAvailableBuilding(entry.building);

            InteractableItem container = instance.GetComponentInChildren<InteractableItem>();
            if (container != null)
            {
                container.interactableCategory = InteractableItem.InteractableCategory.Container;
                container.representedItem = entry.building;
            }
        }
    }

    private void SyncBuildingCurrentLevelsFromBuiltList()
    {
        EnsureAvailableBuildings();

        Dictionary<Item, int> builtLevels = new Dictionary<Item, int>();
        if (builtBuildings != null)
        {
            for (int i = 0; i < builtBuildings.Count; i++)
            {
                BuiltBuildingEntry entry = builtBuildings[i];
                if (entry == null)
                {
                    continue;
                }

                Item item = entry.building;
                if (item == null && entry.info != null)
                {
                    item = entry.info.BuildingItem;
                }

                if (item == null && entry.info != null && !string.IsNullOrWhiteSpace(entry.info.BuildingItemId))
                {
                    item = ResolveBuildingItem(entry.info.BuildingItemId);
                }

                if (item == null || !item.isBuilding)
                {
                    continue;
                }

                int level = entry.info != null ? entry.info.Level : entry.level;
                level = Mathf.Max(1, level);
                if (builtLevels.TryGetValue(item, out int current))
                {
                    if (level > current)
                    {
                        builtLevels[item] = level;
                    }
                }
                else
                {
                    builtLevels[item] = level;
                }
            }
        }

        HashSet<Item> candidates = new HashSet<Item>();
        if (availableBuildings != null)
        {
            for (int i = 0; i < availableBuildings.Count; i++)
            {
                Item item = availableBuildings[i];
                if (item != null && item.isBuilding)
                {
                    candidates.Add(item);
                }
            }
        }

        if (existingBuildings != null)
        {
            for (int i = 0; i < existingBuildings.Count; i++)
            {
                Item item = existingBuildings[i];
                if (item != null && item.isBuilding)
                {
                    candidates.Add(item);
                }
            }
        }

        foreach (KeyValuePair<Item, int> built in builtLevels)
        {
            if (built.Key != null && built.Key.isBuilding)
            {
                candidates.Add(built.Key);
            }
        }

        foreach (Item item in candidates)
        {
            if (item == null || !item.isBuilding)
            {
                continue;
            }

            if (builtLevels.TryGetValue(item, out int level))
            {
                BuildingRuntimeState.SetLevel(item, level, false);
            }
            else
            {
                BuildingRuntimeState.SetLevel(item, 0, false);
            }
        }
    }

    private void SyncBuiltBuildingEntries()
    {
        if (builtBuildings == null)
        {
            return;
        }

        for (int i = 0; i < builtBuildings.Count; i++)
        {
            BuiltBuildingEntry entry = builtBuildings[i];
            if (entry == null)
            {
                continue;
            }

            if (entry.info != null)
            {
                if (entry.building == null)
                {
                    entry.building = entry.info.BuildingItem;
                }

                entry.level = Mathf.Max(1, entry.info.Level);
                entry.position = entry.info.transform.position;
                EnsureBuildingParent(entry.info.transform);
            }
        }
    }

    public int GetCurrentLevel(Item building)
    {
        Vector3 origin = GetUpgradeOriginPosition();
        return GetCurrentLevel(building, origin, out _);
    }

    public int GetCurrentLevel(Item building, Vector3 origin, out BuildingInfoInteractable info)
    {
        info = null;
        if (building == null || !building.isBuilding)
        {
            return 0;
        }

        if (!TryFindNearestBuilt(building, origin, out info))
        {
            return BuildingRuntimeState.GetLevel(building);
        }

        return info != null ? Mathf.Max(1, info.Level) : 0;
    }

    public bool TryFindNearestBuilt(Item building, Vector3 origin, out BuildingInfoInteractable info)
    {
        info = null;
        if (building == null || !building.isBuilding)
        {
            return false;
        }

        EnsureBuiltBuildings(false);
        if (builtBuildings != null && builtBuildings.Count > 0)
        {
            float bestSqr = float.MaxValue;
            for (int i = 0; i < builtBuildings.Count; i++)
            {
                BuiltBuildingEntry entry = builtBuildings[i];
                if (entry == null || entry.info == null)
                {
                    continue;
                }

                BuildingInfoInteractable candidate = entry.info;
                if (!IsInfoForBuilding(candidate, building))
                {
                    continue;
                }

                float sqr = (candidate.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    info = candidate;
                }
            }

            return info != null;
        }

#if UNITY_2023_1_OR_NEWER
        BuildingInfoInteractable[] infos = FindObjectsByType<BuildingInfoInteractable>(FindObjectsSortMode.None);
#else
        BuildingInfoInteractable[] infos = FindObjectsOfType<BuildingInfoInteractable>();
#endif
        if (infos == null || infos.Length == 0)
        {
            return false;
        }

        float bestFallback = float.MaxValue;
        for (int i = 0; i < infos.Length; i++)
        {
            BuildingInfoInteractable candidate = infos[i];
            if (candidate == null)
            {
                continue;
            }

            if (!IsInfoForBuilding(candidate, building))
            {
                continue;
            }

            float sqr = (candidate.transform.position - origin).sqrMagnitude;
            if (sqr < bestFallback)
            {
                bestFallback = sqr;
                info = candidate;
            }
        }

        return info != null;
    }

    private Transform ResolveBuildingsRoot()
    {
        if (buildingsRoot != null)
        {
            return buildingsRoot;
        }

        if (autoFindBuildingsRoot && !string.IsNullOrWhiteSpace(buildingsRootName))
        {
            GameObject found = GameObject.Find(buildingsRootName);
            if (found != null)
            {
                buildingsRoot = found.transform;
                return buildingsRoot;
            }
        }

        if (autoCreateBuildingsRoot && !string.IsNullOrWhiteSpace(buildingsRootName))
        {
            GameObject root = new GameObject(buildingsRootName);
            buildingsRoot = root.transform;
            return buildingsRoot;
        }

        return null;
    }

    public bool TryUpgradeBuildingInstance(BuildingInfoInteractable info, int targetLevel)
    {
        if (info == null)
        {
            return false;
        }

        if (IsNetworked() && IsServer && info.NetworkBuildingId == 0)
        {
            info.SetNetworkBuildingId(nextNetBuildingId++);
        }

        Item item = info.BuildingItem;
        int maxLevel = item != null && item.isBuilding
            ? Mathf.Max(1, item.buildingMaxLevel)
            : int.MaxValue;
        int clampedLevel = Mathf.Clamp(targetLevel, 1, maxLevel);
        info.SetLevel(clampedLevel);
        info.RefreshPresentation("authoritative_upgrade");
        if (item != null)
        {
            UpdateBuildingCurrentLevel(item, clampedLevel);
        }

        if (builtBuildings == null)
        {
            builtBuildings = new List<BuiltBuildingEntry>();
        }

        bool updatedList = false;
        for (int i = 0; i < builtBuildings.Count; i++)
        {
            BuiltBuildingEntry entry = builtBuildings[i];
            if (entry != null && entry.info == info)
            {
                entry.building = item != null ? item : entry.building;
                entry.level = clampedLevel;
                entry.networkId = info.NetworkBuildingId;
                updatedList = true;
                UpsertNetBuiltBuilding(entry);
                break;
            }
        }

        if (!updatedList)
        {
            BuiltBuildingEntry entry = new BuiltBuildingEntry
            {
                info = info,
                building = item,
                level = clampedLevel,
                networkId = info.NetworkBuildingId
            };
            builtBuildings.Add(entry);
            UpsertNetBuiltBuilding(entry);
        }

        NotifyBuildingsChanged();
        return true;
    }

    public bool TryGetSyncedBuildingLevel(BuildingInfoInteractable info, out int level)
    {
        level = 0;
        if (info == null || builtBuildings == null)
        {
            return false;
        }

        for (int i = 0; i < builtBuildings.Count; i++)
        {
            BuiltBuildingEntry entry = builtBuildings[i];
            if (entry == null)
            {
                continue;
            }

            if (entry.info == info || (info.NetworkBuildingId != 0 && entry.networkId == info.NetworkBuildingId))
            {
                level = Mathf.Max(1, entry.level > 0 ? entry.level : (entry.info != null ? entry.info.Level : 0));
                return true;
            }
        }

        return false;
    }

    private void LogBuildingSync(string eventName, BuildingInfoInteractable info, string reason)
    {
        if (info == null)
        {
            return;
        }

        int syncedLevel = TryGetSyncedBuildingLevel(info, out int levelValue) ? levelValue : 0;
        PersistentNetworkObject persistentObject = info.GetComponent<PersistentNetworkObject>();
        string persistentId = persistentObject != null ? persistentObject.PersistentId : string.Empty;
        Debug.Log(
            $"[BuildingSync] event='{eventName}' path='{PersistentWorldDebug.DescribeTransform(info.transform)}' persistentId='{persistentId}' buildId='{info.BuildId}' itemId='{info.BuildingItemId}' networkId={info.NetworkBuildingId} displayedLevel={info.Level} authoritativeSyncedLevel={syncedLevel} source='{info.PresentationOrigin}' reason='{reason}'",
            info);
    }

    private void UpdateBuildingCurrentLevel(Item building, int levelValue)
    {
        if (building == null || !building.isBuilding)
        {
            return;
        }

        BuildingRuntimeState.SetLevel(building, levelValue, true);
    }

    public void ApplyBuildingEffects(Item building, int currentLevel, int levelDelta = 1)
    {
        if (building == null || !building.isBuilding || levelDelta <= 0)
        {
            return;
        }

        List<SquadCharacterController> targets = GetEffectTargets();
        if (targets == null || targets.Count == 0)
        {
            return;
        }

        int startLevel = Mathf.Max(0, currentLevel);
        int targetLevel = startLevel + levelDelta;
        for (int level = startLevel + 1; level <= targetLevel; level++)
        {
            ApplyBuildingEffectsForLevel(building, level, targets);
        }
    }

    private void ApplyBuildingEffectsForLevel(Item building, int level, List<SquadCharacterController> targets)
    {
        if (building == null || targets == null || targets.Count == 0)
        {
            return;
        }

        IReadOnlyList<Effect> effects = building.GetBuildingEffectsForLevel(level);
        if (effects == null || effects.Count == 0)
        {
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            Effect effect = effects[i];
            if (effect == null)
            {
                continue;
            }

            // Les effets declenches a l'interaction ne doivent pas s'appliquer a la construction/amelioration.
            if (effect is IBuildingInteractEffect)
            {
                continue;
            }

            if (effect is IBuildingLevelSquadEffect levelSquadEffect)
            {
                levelSquadEffect.ApplyToSquadForLevel(level, 1);
                continue;
            }

            if (effect is ISquadEffect squadEffect)
            {
                squadEffect.ApplyToSquad(1);
                continue;
            }

            for (int t = 0; t < targets.Count; t++)
            {
                SquadCharacterController controller = targets[t];
                if (controller == null)
                {
                    continue;
                }

                if (effect is IBuildingLevelEffect levelEffect)
                {
                    levelEffect.ApplyForBuildingLevel(controller, building, level, 1);
                }
                else
                {
                    effect.Apply(controller);
                }
            }
        }
    }

    private List<SquadCharacterController> GetEffectTargets()
    {
        List<SquadCharacterController> targets = new List<SquadCharacterController>();

        if (applyEffectsToAllSquad && SquadManager.Instance != null && SquadManager.Instance.squadCharacters != null)
        {
            List<GameObject> squad = SquadManager.Instance.squadCharacters;
            for (int i = 0; i < squad.Count; i++)
            {
                GameObject character = squad[i];
                if (character == null)
                {
                    continue;
                }

                SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
                if (controller != null)
                {
                    targets.Add(controller);
                }
            }
        }
        else
        {
            GameObject controlled = GetControlledCharacter();
            if (controlled != null)
            {
                SquadCharacterController controller = controlled.GetComponent<SquadCharacterController>();
                if (controller != null)
                {
                    targets.Add(controller);
                }
            }
        }

        return targets;
    }

    public Vector3 GetUpgradeOriginPosition()
    {
        GameObject controlled = GetControlledCharacter();
        if (controlled != null)
        {
            return controlled.transform.position;
        }

        return transform.position;
    }

    private bool IsInfoForBuilding(BuildingInfoInteractable info, Item building)
    {
        if (info == null || building == null || !building.isBuilding)
        {
            return false;
        }

        if (info.BuildingItem == building)
        {
            return true;
        }

        string buildingId = GetBuildingItemId(building);
        if (string.IsNullOrWhiteSpace(buildingId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(info.BuildingItemId) && info.BuildingItemId == buildingId)
        {
            return true;
        }

        return false;
    }

    public int CountBuilt(Item building)
    {
        if (building == null || !building.isBuilding || builtBuildings == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < builtBuildings.Count; i++)
        {
            BuiltBuildingEntry entry = builtBuildings[i];
            if (entry != null && entry.building == building)
            {
                count++;
            }
        }

        return count;
    }

    private void InitializeInteractionTrigger()
    {
        if (interactionTrigger == null)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].isTrigger && !IsConcaveMeshCollider(colliders[i]))
                {
                    interactionTrigger = colliders[i];
                    break;
                }
            }

            if (interactionTrigger == null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null && !IsConcaveMeshCollider(colliders[i]))
                    {
                        interactionTrigger = colliders[i];
                        break;
                    }
                }
            }

            if (interactionTrigger == null && colliders.Length > 0)
            {
                interactionTrigger = colliders[0];
            }
        }

        if (interactionTrigger == null)
        {
            interactionTrigger = CreateFallbackTrigger();
        }

        if (interactionTrigger == null)
        {
            useSelfTriggerEvents = false;
            return;
        }

        if (IsConcaveMeshCollider(interactionTrigger))
        {
            Collider fallback = CreateBoxTrigger(interactionTrigger);
            if (fallback != null)
            {
                interactionTrigger = fallback;
            }
        }
        else if (!interactionTrigger.isTrigger)
        {
            interactionTrigger.isTrigger = true;
        }

        useSelfTriggerEvents = interactionTrigger.gameObject == gameObject;
        if (!useSelfTriggerEvents)
        {
            BuilderInteractionTriggerProxy proxy = interactionTrigger.GetComponent<BuilderInteractionTriggerProxy>();
            if (proxy == null)
            {
                proxy = interactionTrigger.gameObject.AddComponent<BuilderInteractionTriggerProxy>();
            }
            proxy.Owner = this;
        }
    }

    private Collider CreateFallbackTrigger()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(transform.position, Vector3.one);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        if (hasBounds)
        {
            box.center = transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = transform.InverseTransformVector(bounds.size);
            box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        }

        return box;
    }

    private static bool IsConcaveMeshCollider(Collider collider)
    {
        MeshCollider meshCollider = collider as MeshCollider;
        return meshCollider != null && !meshCollider.convex;
    }

    private Collider CreateBoxTrigger(Collider reference)
    {
        if (reference == null)
        {
            return null;
        }

        BoxCollider box = reference.gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        FitBoxToCollider(box, reference);
        return box;
    }

    private void FitBoxToCollider(BoxCollider box, Collider reference)
    {
        if (box == null)
        {
            return;
        }

        if (reference == null)
        {
            box.center = Vector3.zero;
            box.size = Vector3.one;
            return;
        }

        if (reference is BoxCollider boxCollider)
        {
            box.center = boxCollider.center;
            box.size = boxCollider.size;
            return;
        }

        if (reference is SphereCollider sphereCollider)
        {
            float diameter = sphereCollider.radius * 2f;
            box.center = sphereCollider.center;
            box.size = new Vector3(diameter, diameter, diameter);
            return;
        }

        if (reference is CapsuleCollider capsuleCollider)
        {
            float diameter = capsuleCollider.radius * 2f;
            box.center = capsuleCollider.center;
            box.size = new Vector3(diameter, capsuleCollider.height, diameter);
            return;
        }

        Bounds bounds = reference.bounds;
        box.center = reference.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = reference.transform.InverseTransformVector(bounds.size);
        box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
    }

    private GameObject GetSquadCharacter(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        if (SquadManager.Instance == null || SquadManager.Instance.squadCharacters == null)
        {
            return null;
        }

        Transform current = other.transform;
        bool hasPlayerTag = false;
        GameObject squadRoot = null;
        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                hasPlayerTag = true;
            }

            if (SquadManager.Instance.squadCharacters.Contains(current.gameObject))
            {
                squadRoot = current.gameObject;
            }

            current = current.parent;
        }

        if (squadRoot == null)
        {
            Transform root = other.transform.root;
            if (root != null)
            {
                if (root.CompareTag("Player"))
                {
                    hasPlayerTag = true;
                }

                for (int i = 0; i < SquadManager.Instance.squadCharacters.Count; i++)
                {
                    GameObject candidate = SquadManager.Instance.squadCharacters[i];
                    if (candidate != null && candidate.transform.IsChildOf(root))
                    {
                        squadRoot = candidate;
                        break;
                    }
                }
            }
        }

        if (hasPlayerTag && squadRoot != null)
        {
            return squadRoot;
        }

        return null;
    }

    private bool RegisterCharacterCollider(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        if (!characterColliderCounts.TryGetValue(character, out int count))
        {
            characterColliderCounts[character] = 1;
            return true;
        }

        characterColliderCounts[character] = count + 1;
        return false;
    }

    private bool UnregisterCharacterCollider(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        if (!characterColliderCounts.TryGetValue(character, out int count))
        {
            return false;
        }

        count -= 1;
        if (count > 0)
        {
            characterColliderCounts[character] = count;
            return false;
        }

        characterColliderCounts.Remove(character);
        return true;
    }

    private void AddAvailableBuilding(Item data)
    {
        if (data == null || !data.isBuilding)
        {
            return;
        }

        if (availableBuildings == null)
        {
            availableBuildings = new List<Item>();
        }

        if (!availableBuildings.Contains(data))
        {
            availableBuildings.Add(data);
        }
    }

    private Item ResolveBuildingItem(string dataId)
    {
        if (string.IsNullOrWhiteSpace(dataId))
        {
            return null;
        }

        if (!isRefreshingAvailableBuildings)
        {
            EnsureAvailableBuildings();
        }

        Item resolved = FindBuildingById(availableBuildings, dataId);
        if (resolved != null)
        {
            return resolved;
        }

        resolved = FindBuildingById(existingBuildings, dataId);
        if (resolved != null)
        {
            return resolved;
        }

        Item[] loadedItems = Resources.FindObjectsOfTypeAll<Item>();
        if (loadedItems != null)
        {
            for (int i = 0; i < loadedItems.Length; i++)
            {
                Item candidate = loadedItems[i];
                if (candidate == null || !candidate.isBuilding)
                {
                    continue;
                }

                if (GetBuildingItemId(candidate) == dataId)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private Item FindBuildingById(List<Item> items, string dataId)
    {
        if (items == null || string.IsNullOrWhiteSpace(dataId))
        {
            return null;
        }

        for (int i = 0; i < items.Count; i++)
        {
            Item candidate = items[i];
            if (candidate == null || !candidate.isBuilding)
            {
                continue;
            }

            if (GetBuildingItemId(candidate) == dataId)
            {
                return candidate;
            }
        }

        return null;
    }

    public void RequestBuild(Item building, Vector3 position, Quaternion rotation)
    {
        if (building == null || !building.isBuilding)
        {
            return;
        }

        if (IsNetworked() && !IsServer)
        {
            RequestBuildServerRpc(GetBuildingItemId(building), position, rotation);
            return;
        }

        if (!ExecuteBuild(building, position, rotation, null, out string feedback) && !string.IsNullOrWhiteSpace(feedback))
        {
            InfoBoxUI.TryShow(feedback);
        }
    }

    public void RequestUpgrade(BuildingInfoInteractable info, int targetLevel)
    {
        if (info == null)
        {
            return;
        }

        if (IsNetworked() && !IsServer)
        {
            RequestUpgradeServerRpc(info.NetworkBuildingId, info.BuildingItemId, targetLevel);
            return;
        }

        if (!ExecuteUpgrade(info, targetLevel, null, out string feedback) && !string.IsNullOrWhiteSpace(feedback))
        {
            InfoBoxUI.TryShow(feedback);
        }
    }

    public void RequestCraft(BuildingInfoInteractable info, Item craftItem, string successMessage, string failedMessage)
    {
        if (info == null || craftItem == null)
        {
            return;
        }

        if (IsNetworked() && !IsServer)
        {
            string craftId = ItemIdUtils.GetItemId(craftItem);
            if (!string.IsNullOrWhiteSpace(craftId))
            {
                RequestCraftServerRpc(info.NetworkBuildingId, info.BuildingItemId, craftId, successMessage, failedMessage);
            }
            return;
        }

        if (!ExecuteCraft(info, craftItem, null, out string feedback))
        {
            if (!string.IsNullOrWhiteSpace(feedback))
            {
                InfoBoxUI.TryShow(feedback);
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(successMessage))
        {
            InfoBoxUI.TryShow(successMessage);
        }
    }

    public void RequestCatalyseurCraft(BuildingInfoInteractable info, int effectIndex, string successMessage, string failedMessage)
    {
        if (info == null)
        {
            return;
        }

        if (IsNetworked() && !IsServer)
        {
            RequestCatalyseurCraftServerRpc(info.NetworkBuildingId, info.BuildingItemId, effectIndex, successMessage, failedMessage);
            return;
        }

        if (!ExecuteCatalyseurCraft(info, effectIndex, null, out string feedback))
        {
            if (!string.IsNullOrWhiteSpace(feedback))
            {
                InfoBoxUI.TryShow(feedback);
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(successMessage))
        {
            InfoBoxUI.TryShow(successMessage);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestBuildServerRpc(string buildingId, Vector3 position, Quaternion rotation, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || string.IsNullOrWhiteSpace(buildingId))
        {
            return;
        }

        if (!TryResolveSender(rpcParams, out Transform playerRoot, out SquadCharacterController controller, out NetworkInventory inventory))
        {
            return;
        }

        Item building = ResolveBuildingItem(buildingId);
        if (building == null || !building.isBuilding)
        {
            SendFeedback("Construction invalide.", rpcParams);
            return;
        }

        if (!IsWithinBuildRange(playerRoot, position))
        {
            SendFeedback("Trop loin pour construire.", rpcParams);
            return;
        }

        if (!TryConsumeRequirements(building, controller, useHomeResourcesForBuild, out string reason))
        {
            SendFeedback(string.IsNullOrWhiteSpace(reason) ? "Ressources insuffisantes." : reason, rpcParams);
            return;
        }

        BuildingInfoInteractable info = SpawnNetBuildingInstance(building, position, rotation, 1, nextNetBuildingId++);
        if (info == null)
        {
            SendFeedback("Construction impossible.", rpcParams);
            return;
        }

        RegisterBuiltBuilding(building, 1, info);
        ApplyBuildingEffects(building, 0, 1);
        inventory.SyncFromController();
        SendFeedback(building.GetPlaceSuccessMessage(), rpcParams);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestUpgradeServerRpc(ulong buildingNetworkId, string buildingItemId, int targetLevel, ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
        {
            return;
        }

        if (!TryResolveSender(rpcParams, out Transform playerRoot, out SquadCharacterController controller, out NetworkInventory inventory))
        {
            return;
        }

        BuildingInfoInteractable info = ResolveBuildingInfo(buildingNetworkId, buildingItemId, playerRoot);
        if (info == null)
        {
            SendFeedback("Batiment introuvable.", rpcParams);
            return;
        }

        if (!IsWithinInteractRange(playerRoot, info))
        {
            SendFeedback("Trop loin pour ameliorer.", rpcParams);
            return;
        }

        Item building = info.BuildingItem;
        if (building == null || !building.isBuilding)
        {
            SendFeedback("Batiment invalide.", rpcParams);
            return;
        }

        int currentLevel = Mathf.Max(1, info.Level);
        int maxLevel = Mathf.Max(1, building.buildingMaxLevel);
        if (currentLevel >= maxLevel)
        {
            SendFeedback("Niveau maximal atteint.", rpcParams);
            return;
        }

        if (!TryConsumeRequirements(building, controller, useHomeResourcesForBuild, out string reason))
        {
            SendFeedback(string.IsNullOrWhiteSpace(reason) ? "Ressources insuffisantes." : reason, rpcParams);
            return;
        }

        int finalLevel = Mathf.Clamp(targetLevel, currentLevel + 1, maxLevel);
        if (!TryUpgradeBuildingInstance(info, finalLevel))
        {
            SendFeedback("Amelioration impossible.", rpcParams);
            return;
        }

        ApplyBuildingEffects(building, currentLevel, finalLevel - currentLevel);
        inventory.SyncFromController();
        SendFeedback("Amelioration terminee.", rpcParams);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCraftServerRpc(ulong buildingNetworkId, string buildingItemId, string craftItemId, string successMessage, string failedMessage, ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
        {
            return;
        }

        if (!TryResolveSender(rpcParams, out Transform playerRoot, out SquadCharacterController controller, out NetworkInventory inventory))
        {
            return;
        }

        BuildingInfoInteractable info = ResolveBuildingInfo(buildingNetworkId, buildingItemId, playerRoot);
        if (info == null)
        {
            SendFeedback(failedMessage, rpcParams);
            return;
        }

        if (!IsWithinInteractRange(playerRoot, info))
        {
            SendFeedback("Trop loin pour crafter.", rpcParams);
            return;
        }

        Item craftItem = ItemRegistry.Resolve(craftItemId);
        if (craftItem == null)
        {
            SendFeedback(failedMessage, rpcParams);
            return;
        }

        Item building = info.BuildingItem;
        if (building == null || !building.isBuilding)
        {
            SendFeedback(failedMessage, rpcParams);
            return;
        }

        List<Item> unlocked = building.GetUnlockedCraftsForLevel(info.Level);
        if (unlocked == null || !unlocked.Contains(craftItem))
        {
            SendFeedback(failedMessage, rpcParams);
            return;
        }

        if (!TryConsumeRequirements(craftItem, controller, useHomeResourcesForCraft, out string reason))
        {
            SendFeedback(string.IsNullOrWhiteSpace(failedMessage) ? reason : failedMessage, rpcParams);
            return;
        }

        controller.AddItem(craftItem, 1);
        inventory.SyncFromController();
        SendFeedback(string.IsNullOrWhiteSpace(successMessage) ? "Craft reussi." : successMessage, rpcParams);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCatalyseurCraftServerRpc(ulong buildingNetworkId, string buildingItemId, int effectIndex, string successMessage, string failedMessage, ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
        {
            return;
        }

        if (!TryResolveSender(rpcParams, out Transform playerRoot, out SquadCharacterController controller, out NetworkInventory inventory))
        {
            return;
        }

        BuildingInfoInteractable info = ResolveBuildingInfo(buildingNetworkId, buildingItemId, playerRoot);
        if (info == null)
        {
            SendFeedback(failedMessage, rpcParams);
            return;
        }

        if (!IsWithinInteractRange(playerRoot, info))
        {
            SendFeedback("Trop loin pour crafter.", rpcParams);
            return;
        }

        Item building = info.BuildingItem;
        if (building == null || building.buildingEffects == null)
        {
            SendFeedback(failedMessage, rpcParams);
            return;
        }

        if (effectIndex < 0 || effectIndex >= building.buildingEffects.Count)
        {
            SendFeedback(failedMessage, rpcParams);
            return;
        }

        CatalyseurOrbCraftEffect effect = building.buildingEffects[effectIndex] as CatalyseurOrbCraftEffect;
        if (effect == null)
        {
            SendFeedback(failedMessage, rpcParams);
            return;
        }

        bool success = effect.ApplyOnInteract(controller, building, info.Level);
        if (!success)
        {
            SendFeedback(string.IsNullOrWhiteSpace(failedMessage) ? "Craft impossible." : failedMessage, rpcParams);
            return;
        }

        inventory.SyncFromController();
        SendFeedback(string.IsNullOrWhiteSpace(successMessage) ? "Craft reussi." : successMessage, rpcParams);
    }

    private bool ExecuteBuild(Item building, Vector3 position, Quaternion rotation, SquadCharacterController controller, out string feedback)
    {
        feedback = string.Empty;
        if (building == null || !building.isBuilding)
        {
            return false;
        }

        if (controller != null && !TryConsumeRequirements(building, controller, useHomeResourcesForBuild, out feedback))
        {
            return false;
        }

        BuildingInfoInteractable info = SpawnNetBuildingInstance(building, position, rotation, 1, nextNetBuildingId++);
        if (info == null)
        {
            feedback = "Construction impossible.";
            return false;
        }

        RegisterBuiltBuilding(building, 1, info);
        ApplyBuildingEffects(building, 0, 1);
        return true;
    }

    private bool ExecuteUpgrade(BuildingInfoInteractable info, int targetLevel, SquadCharacterController controller, out string feedback)
    {
        feedback = string.Empty;
        if (info == null)
        {
            return false;
        }

        Item building = info.BuildingItem;
        if (building == null || !building.isBuilding)
        {
            feedback = "Batiment invalide.";
            return false;
        }

        int currentLevel = Mathf.Max(1, info.Level);
        int maxLevel = Mathf.Max(1, building.buildingMaxLevel);
        if (currentLevel >= maxLevel)
        {
            feedback = "Niveau maximal atteint.";
            return false;
        }

        if (controller != null && !TryConsumeRequirements(building, controller, useHomeResourcesForBuild, out feedback))
        {
            return false;
        }

        int finalLevel = Mathf.Clamp(targetLevel, currentLevel + 1, maxLevel);
        if (!TryUpgradeBuildingInstance(info, finalLevel))
        {
            feedback = "Amelioration impossible.";
            return false;
        }

        ApplyBuildingEffects(building, currentLevel, finalLevel - currentLevel);
        return true;
    }

    private bool ExecuteCraft(BuildingInfoInteractable info, Item craftItem, SquadCharacterController controller, out string feedback)
    {
        feedback = string.Empty;
        if (info == null || craftItem == null)
        {
            return false;
        }

        Item building = info.BuildingItem;
        if (building == null)
        {
            feedback = "Batiment invalide.";
            return false;
        }

        List<Item> unlocked = building.GetUnlockedCraftsForLevel(info.Level);
        if (unlocked == null || !unlocked.Contains(craftItem))
        {
            feedback = "Craft indisponible.";
            return false;
        }

        if (controller != null && !TryConsumeRequirements(craftItem, controller, useHomeResourcesForCraft, out feedback))
        {
            return false;
        }

        if (controller != null)
        {
            controller.AddItem(craftItem, 1);
        }

        return true;
    }

    private bool ExecuteCatalyseurCraft(BuildingInfoInteractable info, int effectIndex, SquadCharacterController controller, out string feedback)
    {
        feedback = string.Empty;
        if (info == null)
        {
            return false;
        }

        Item building = info.BuildingItem;
        if (building == null || building.buildingEffects == null)
        {
            feedback = "Craft indisponible.";
            return false;
        }

        if (effectIndex < 0 || effectIndex >= building.buildingEffects.Count)
        {
            feedback = "Craft indisponible.";
            return false;
        }

        CatalyseurOrbCraftEffect effect = building.buildingEffects[effectIndex] as CatalyseurOrbCraftEffect;
        if (effect == null)
        {
            feedback = "Craft indisponible.";
            return false;
        }

        if (controller == null)
        {
            feedback = "Aucun personnage.";
            return false;
        }

        if (!effect.ApplyOnInteract(controller, building, info.Level))
        {
            feedback = "Craft impossible.";
            return false;
        }

        return true;
    }

    private bool TryResolveSender(ServerRpcParams rpcParams, out Transform playerRoot, out SquadCharacterController controller, out NetworkInventory inventory)
    {
        playerRoot = NetcodePlayerUtils.GetPlayerTransform(rpcParams.Receive.SenderClientId);
        controller = null;
        inventory = null;

        if (playerRoot == null)
        {
            return false;
        }

        controller = playerRoot.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            controller = playerRoot.GetComponentInChildren<SquadCharacterController>(true);
        }

        inventory = playerRoot.GetComponent<NetworkInventory>();
        if (inventory == null)
        {
            inventory = playerRoot.GetComponentInChildren<NetworkInventory>(true);
        }

        if (inventory != null && inventory.OwnerClientId != rpcParams.Receive.SenderClientId)
        {
            inventory = null;
        }

        return controller != null && inventory != null;
    }

    private BuildingInfoInteractable ResolveBuildingInfo(ulong networkId, string buildingItemId, Transform playerRoot)
    {
        if (networkId != 0 && netBuildingLookup.TryGetValue(networkId, out BuildingInfoInteractable found) && found != null)
        {
            return found;
        }

        if (string.IsNullOrWhiteSpace(buildingItemId))
        {
            return null;
        }

        Item building = ResolveBuildingItem(buildingItemId);
        if (building == null)
        {
            return null;
        }

        Vector3 origin = playerRoot != null ? playerRoot.position : Vector3.zero;
        if (TryFindNearestBuilt(building, origin, out BuildingInfoInteractable info))
        {
            return info;
        }

        return null;
    }

    private BuildingInfoInteractable ResolveExistingRuntimeBuilding(NetBuiltBuilding entry, Item building)
    {
        if (entry.Id == 0)
        {
            return null;
        }

        string buildingItemId = GetBuildingItemId(building);
        string persistentId = PersistentWorldSceneInstaller.BuildRuntimeBuildingPersistentId(entry.Id, buildingItemId);
        if (NetworkObjectRegistry.Instance != null &&
            NetworkObjectRegistry.Instance.TryGet(persistentId, out PersistentNetworkObject persistentObject) &&
            persistentObject != null)
        {
            BuildingInfoInteractable resolvedFromRegistry = persistentObject.GetComponent<BuildingInfoInteractable>();
            if (resolvedFromRegistry != null)
            {
                return resolvedFromRegistry;
            }
        }

#if UNITY_2023_1_OR_NEWER
        BuildingInfoInteractable[] infos = FindObjectsByType<BuildingInfoInteractable>(FindObjectsSortMode.None);
#else
        BuildingInfoInteractable[] infos = FindObjectsOfType<BuildingInfoInteractable>();
#endif
        if (infos == null)
        {
            return null;
        }

        for (int i = 0; i < infos.Length; i++)
        {
            BuildingInfoInteractable info = infos[i];
            if (info != null && info.NetworkBuildingId == entry.Id)
            {
                return info;
            }
        }

        return null;
    }

    private bool IsWithinBuildRange(Transform playerRoot, Vector3 position)
    {
        if (!requireProximity)
        {
            return true;
        }

        if (playerRoot == null)
        {
            return false;
        }

        float maxDistance = Mathf.Max(0.1f, networkBuildMaxDistance);
        return (playerRoot.position - position).sqrMagnitude <= maxDistance * maxDistance;
    }

    private bool IsWithinInteractRange(Transform playerRoot, BuildingInfoInteractable info)
    {
        if (!requireProximity)
        {
            return true;
        }

        if (playerRoot == null || info == null)
        {
            return false;
        }

        float maxDistance = Mathf.Max(0.1f, networkInteractDistance);
        Collider trigger = info.interactionTrigger != null ? info.interactionTrigger : info.GetComponent<Collider>();
        if (trigger != null)
        {
            Vector3 closest = trigger.ClosestPoint(playerRoot.position);
            return (closest - playerRoot.position).sqrMagnitude <= maxDistance * maxDistance;
        }

        return (info.transform.position - playerRoot.position).sqrMagnitude <= maxDistance * maxDistance;
    }

    public RequirementAvailability EvaluateRequirements(Item targetItem, SquadCharacterController controller, bool useHomeResources)
    {
        RequirementAvailability availability = new RequirementAvailability(ResolveRecipeId(targetItem), useHomeResources);
        if (targetItem == null || controller == null)
        {
            availability.FailureReason = "Ressources insuffisantes.";
            return availability;
        }

        Dictionary<Item, int> requiredCounts = BuildRequirementCounts(targetItem);
        foreach (KeyValuePair<Item, int> requirement in requiredCounts)
        {
            availability.RequiredCounts[requirement.Key] = requirement.Value;
        }

        if (requiredCounts.Count == 0)
        {
            availability.Craftable = true;
            return availability;
        }

        Dictionary<Item, int> inventoryCounts = BuildInventoryCounts(controller);
        List<InteractableItem> homeContainers = useHomeResources ? ResolveHomeContainers() : null;
        bool craftable = true;

        foreach (KeyValuePair<Item, int> requirement in requiredCounts)
        {
            Item requiredItem = requirement.Key;
            int playerContribution = 0;
            int storageContribution = 0;

            if (requiredItem != null && inventoryCounts.TryGetValue(requiredItem, out int invCount))
            {
                playerContribution = invCount;
            }

            if (requiredItem != null && useHomeResources && homeContainers != null)
            {
                storageContribution = GetHomeItemCount(requiredItem, homeContainers);
            }

            availability.PlayerContribution[requiredItem] = playerContribution;
            availability.StorageContribution[requiredItem] = storageContribution;

            if (playerContribution + storageContribution < requirement.Value)
            {
                craftable = false;
            }
        }

        availability.Craftable = craftable;
        availability.FailureReason = craftable ? string.Empty : "Ressources insuffisantes.";
        return availability;
    }

    public void LogCraftRequirementAnalysis(
        string phase,
        Item targetItem,
        RequirementAvailability availability,
        bool previewCraftable,
        bool validationCraftable,
        string consumptionSources)
    {
        if (availability == null)
        {
            return;
        }

        string recipeId = ResolveRecipeId(targetItem);
        string message =
            $"[CraftValidation] phase='{phase}' recipeId='{recipeId}' requiredResources='{DescribeItemQuantityMap(availability.RequiredCounts)}' playerContribution='{DescribeItemQuantityMap(availability.PlayerContribution)}' storageContribution='{DescribeItemQuantityMap(availability.StorageContribution)}' combinedContribution='{DescribeCombinedContribution(availability)}' previewCraftable={previewCraftable} validationCraftable={validationCraftable} consumptionSources='{consumptionSources}'";

        string key = $"{phase}:{recipeId}";
        if (lastRequirementAnalysisLogs.TryGetValue(key, out string previous) && previous == message)
        {
            return;
        }

        lastRequirementAnalysisLogs[key] = message;
        Debug.Log(message, this);
    }

    public bool TryConsumeCraftRequirements(Item targetItem, SquadCharacterController controller, out string reason)
    {
        return TryConsumeRequirements(targetItem, controller, useHomeResourcesForCraft, out reason);
    }

    private bool TryConsumeRequirements(Item targetItem, SquadCharacterController controller, bool useHomeResources, out string reason)
    {
        RequirementAvailability availability = EvaluateRequirements(targetItem, controller, useHomeResources);
        reason = availability.FailureReason;
        LogCraftRequirementAnalysis(
            "validation",
            targetItem,
            availability,
            previewCraftable: availability.Craftable,
            validationCraftable: availability.Craftable,
            consumptionSources: availability.Craftable ? "pending" : "none");

        if (targetItem == null || controller == null)
        {
            return false;
        }

        if (!availability.Craftable)
        {
            return false;
        }

        if (availability.RequiredCounts.Count == 0)
        {
            LogCraftRequirementAnalysis(
                "consumption",
                targetItem,
                availability,
                previewCraftable: true,
                validationCraftable: true,
                consumptionSources: "none");
            return true;
        }

        Dictionary<Item, int> inventoryCounts = BuildInventoryCounts(controller);
        List<InteractableItem> homeContainers = useHomeResources ? ResolveHomeContainers() : null;
        List<string> consumptionSources = new List<string>();

        foreach (KeyValuePair<Item, int> requirement in availability.RequiredCounts)
        {
            Item requiredItem = requirement.Key;
            int remaining = requirement.Value;
            int removedFromInventory = 0;
            int removedFromStorage = 0;

            if (requiredItem != null && inventoryCounts.TryGetValue(requiredItem, out int invCount))
            {
                int fromInventory = Mathf.Min(invCount, remaining);
                if (fromInventory > 0)
                {
                    controller.TryRemoveItemQuantity(requiredItem, fromInventory);
                    remaining -= fromInventory;
                    removedFromInventory = fromInventory;
                }
            }

            if (remaining > 0 && requiredItem != null && useHomeResources && homeContainers != null)
            {
                removedFromStorage = RemoveFromHomeContainers(requiredItem, remaining, homeContainers);
                remaining -= removedFromStorage;
            }

            consumptionSources.Add(
                $"{DescribeItem(requiredItem)}:player={removedFromInventory},storage={removedFromStorage}");

            if (remaining > 0)
            {
                reason = "Ressources insuffisantes.";
                LogCraftRequirementAnalysis(
                    "consumption_failed",
                    targetItem,
                    availability,
                    previewCraftable: availability.Craftable,
                    validationCraftable: false,
                    consumptionSources: string.Join(";", consumptionSources));
                return false;
            }
        }

        LogCraftRequirementAnalysis(
            "consumption",
            targetItem,
            availability,
            previewCraftable: availability.Craftable,
            validationCraftable: true,
            consumptionSources: string.Join(";", consumptionSources));
        return true;
    }

    private Dictionary<Item, int> BuildRequirementCounts(Item targetItem)
    {
        Dictionary<Item, int> counts = new Dictionary<Item, int>();
        if (targetItem == null || targetItem.buildingRequirements == null)
        {
            return counts;
        }

        for (int i = 0; i < targetItem.buildingRequirements.Count; i++)
        {
            Item.BuildingRequirement requirement = targetItem.buildingRequirements[i];
            if (requirement == null || requirement.item == null || requirement.quantity <= 0)
            {
                continue;
            }

            if (!counts.TryGetValue(requirement.item, out int current))
            {
                counts[requirement.item] = requirement.quantity;
            }
            else
            {
                counts[requirement.item] = current + requirement.quantity;
            }
        }

        return counts;
    }

    private Dictionary<Item, int> BuildInventoryCounts(SquadCharacterController controller)
    {
        Dictionary<Item, int> counts = new Dictionary<Item, int>();
        if (controller == null)
        {
            return counts;
        }

        IReadOnlyList<Item> items = controller.Items;
        if (items == null)
        {
            return counts;
        }

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null)
            {
                continue;
            }

            if (!counts.TryGetValue(item, out int current))
            {
                counts[item] = 1;
            }
            else
            {
                counts[item] = current + 1;
            }
        }

        return counts;
    }

    private List<InteractableItem> ResolveHomeContainers()
    {
        Maison maison = GetMaison();
        if (maison == null)
        {
            return null;
        }

        return maison.ResolveMaisonLootContainers(null);
    }

    private int GetHomeItemCount(Item item, List<InteractableItem> containers)
    {
        if (item == null || containers == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < containers.Count; i++)
        {
            InteractableItem container = containers[i];
            if (container == null)
            {
                continue;
            }

            total += container.GetItemCount(item);
        }

        return total;
    }

    private int RemoveFromHomeContainers(Item item, int quantity, List<InteractableItem> containers)
    {
        if (item == null || quantity <= 0 || containers == null)
        {
            return 0;
        }

        int remaining = quantity;
        for (int i = 0; i < containers.Count && remaining > 0; i++)
        {
            InteractableItem container = containers[i];
            if (container == null)
            {
                continue;
            }

            int removed = container.RemoveItems(item, remaining);
            remaining -= removed;
        }

        return quantity - remaining;
    }

    private static string ResolveRecipeId(Item item)
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

    private static string DescribeItem(Item item)
    {
        return ResolveRecipeId(item);
    }

    private static string DescribeItemQuantityMap(Dictionary<Item, int> values)
    {
        if (values == null || values.Count == 0)
        {
            return string.Empty;
        }

        List<string> entries = new List<string>();
        foreach (KeyValuePair<Item, int> pair in values)
        {
            if (pair.Key == null)
            {
                continue;
            }

            entries.Add($"{DescribeItem(pair.Key)}:{pair.Value}");
        }

        entries.Sort(System.StringComparer.Ordinal);
        return string.Join(",", entries);
    }

    private static string DescribeCombinedContribution(RequirementAvailability availability)
    {
        if (availability == null || availability.RequiredCounts.Count == 0)
        {
            return string.Empty;
        }

        List<string> entries = new List<string>();
        foreach (KeyValuePair<Item, int> requirement in availability.RequiredCounts)
        {
            Item item = requirement.Key;
            if (item == null)
            {
                continue;
            }

            entries.Add($"{DescribeItem(item)}:{availability.GetCombinedContribution(item)}");
        }

        entries.Sort(System.StringComparer.Ordinal);
        return string.Join(",", entries);
    }

    private Maison GetMaison()
    {
        if (cachedMaison != null)
        {
            return cachedMaison;
        }

        cachedMaison = Maison.Instance;
        if (cachedMaison != null)
        {
            return cachedMaison;
        }

#if UNITY_2023_1_OR_NEWER
        cachedMaison = FindFirstObjectByType<Maison>();
#else
        cachedMaison = FindObjectOfType<Maison>();
#endif

        return cachedMaison;
    }

    private void TryAssignMaisonChestTag(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        string tag = GetMaisonChestTag();
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        try
        {
            instance.tag = tag;
        }
        catch (UnityException)
        {
            // Tag not defined.
        }
    }

    private string GetMaisonChestTag()
    {
        Maison maison = GetMaison();
        if (maison != null && !string.IsNullOrWhiteSpace(maison.maisonChestTag))
        {
            return maison.maisonChestTag;
        }

        return "MaisonChest";
    }

    private void EnsureHomeChestDefaults(InteractableItem container)
    {
        if (container == null)
        {
            return;
        }

        Maison maison = GetMaison();
        if (maison != null)
        {
            maison.EnsureHomeChestDefaults(container);
        }
    }

    [ClientRpc]
    private void ShowFeedbackClientRpc(string message, ClientRpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        InfoBoxUI.TryShow(message);
    }

    private void SendFeedback(string message, ServerRpcParams rpcParams)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ShowFeedbackClientRpc(message, BuildClientRpcParams(rpcParams));
    }

    private static ClientRpcParams BuildClientRpcParams(ServerRpcParams rpcParams)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { rpcParams.Receive.SenderClientId }
            }
        };
    }

    private static string GetBuildingItemId(Item data)
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
}

public class BuilderInteractionTriggerProxy : MonoBehaviour
{
    public BuilderController Owner { get; set; }

    private void OnTriggerEnter(Collider other)
    {
        if (Owner != null)
        {
            Owner.NotifyTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (Owner != null)
        {
            Owner.NotifyTriggerExit(other);
        }
    }
}
