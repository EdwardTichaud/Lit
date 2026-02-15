using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Gere la liste des constructions (par instance) pour la progression et les effets.
public class BuilderController : MonoBehaviour
{
    [System.Serializable]
    public class BuiltBuildingEntry
    {
        [Tooltip("Instance liee a cette entree.")]
        public BuildingInfoInteractable info;
        [Tooltip("Type de batiment.")]
        public Item building;
        [Tooltip("Niveau de cette instance.")]
        public int level = 1;
    }

    [Header("Available Buildings")]
    [Tooltip("Tous les items building connus (pour la persistence/upgrade).")]
    public List<Item> availableBuildings = new List<Item>();

    [Header("Built")]
    [Tooltip("Instances construites (niveau par instance).")]
    public List<BuiltBuildingEntry> builtBuildings = new List<BuiltBuildingEntry>();
    [Tooltip("Applique les effets aux membres de la squad au lieu du personnage controle.")]
    public bool applyEffectsToAllSquad = false;

    [Header("Interaction - Voice Lines")]
    [Tooltip("Joue une voice line lors de l'interaction.")]
    public bool playVoiceLineOnInteract = true;
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
    private PlayerInputs playerInputs;
    private LocalVoiceLineController voiceLineController;
    private float nextVoiceLineTime;

    private void Awake()
    {
        InitializeInteractionTrigger();
        playerInputs = new PlayerInputs();
        voiceLineController = GetComponent<LocalVoiceLineController>();
    }

    private void OnEnable()
    {
        if (playerInputs == null)
        {
            playerInputs = new PlayerInputs();
        }

        playerInputs.Enable();
        playerInputs.Player.Interact.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        if (playerInputs != null)
        {
            playerInputs.Player.Interact.performed -= OnInteractPerformed;
            playerInputs.Disable();
        }

        charactersInRange.Clear();
        characterColliderCounts.Clear();
        currentCharacter = null;
    }


    private static GameObject GetControlledCharacter()
    {
        return SquadManager.Instance != null ? SquadManager.Instance.currentCharacter : null;
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
        if (!playVoiceLineOnInteract || !CanProcessInteract())
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

                builtBuildings.Add(new BuiltBuildingEntry
                {
                    info = info,
                    building = item,
                    level = Mathf.Max(1, info.Level)
                });
                UpdateBuildingCurrentLevel(item, info.Level);
                added++;
            }
        }

        if (added == 0 && existingBuildings != null)
        {
            for (int i = 0; i < existingBuildings.Count; i++)
            {
                Item item = existingBuildings[i];
                if (item == null || !item.isBuilding)
                {
                    continue;
                }

                builtBuildings.Add(new BuiltBuildingEntry
                {
                    info = null,
                    building = item,
                    level = 1
                });
                UpdateBuildingCurrentLevel(item, 1);
            }
        }
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
            for (int i = 0; i < builtBuildings.Count; i++)
            {
                BuiltBuildingEntry entry = builtBuildings[i];
                if (entry != null && entry.info == info)
                {
                    entry.building = building;
                    entry.level = Mathf.Max(1, levelValue);
                    AddAvailableBuilding(building);
                    return;
                }
            }
        }

        builtBuildings.Add(new BuiltBuildingEntry
        {
            info = info,
            building = building,
            level = Mathf.Max(1, levelValue)
        });

        AddAvailableBuilding(building);
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
            return Mathf.Max(0, building.buildingCurrentLevel);
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

#if UNITY_2023_1_OR_NEWER
        BuildingInfoInteractable[] infos = FindObjectsByType<BuildingInfoInteractable>(FindObjectsSortMode.None);
#else
        BuildingInfoInteractable[] infos = FindObjectsOfType<BuildingInfoInteractable>();
#endif
        if (infos == null || infos.Length == 0)
        {
            return false;
        }

        float bestSqr = float.MaxValue;
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
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                info = candidate;
            }
        }

        return info != null;
    }

    public bool TryUpgradeBuildingInstance(BuildingInfoInteractable info, int targetLevel)
    {
        if (info == null)
        {
            return false;
        }

        Item item = info.BuildingItem;
        int maxLevel = item != null && item.isBuilding
            ? Mathf.Max(1, item.buildingMaxLevel)
            : int.MaxValue;
        int clampedLevel = Mathf.Clamp(targetLevel, 1, maxLevel);
        info.SetLevel(clampedLevel);
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
                updatedList = true;
                break;
            }
        }

        if (!updatedList)
        {
            builtBuildings.Add(new BuiltBuildingEntry
            {
                info = info,
                building = item,
                level = clampedLevel
            });
        }

        return true;
    }

    private void UpdateBuildingCurrentLevel(Item building, int levelValue)
    {
        if (building == null || !building.isBuilding)
        {
            return;
        }

        int clampedLevel = Mathf.Clamp(levelValue, 0, Mathf.Max(1, building.buildingMaxLevel));
        if (clampedLevel > building.buildingCurrentLevel)
        {
            building.buildingCurrentLevel = clampedLevel;
        }
    }

    public void ApplyBuildingEffects(Item building, int levelDelta = 1)
    {
        if (building == null || !building.isBuilding || building.buildingEffects == null || levelDelta <= 0)
        {
            return;
        }

        List<SquadCharacterController> targets = GetEffectTargets();
        if (targets == null || targets.Count == 0)
        {
            return;
        }

        for (int i = 0; i < building.buildingEffects.Count; i++)
        {
            Effect effect = building.buildingEffects[i];
            if (effect == null)
            {
                continue;
            }

            if (effect is ISquadEffect squadEffect)
            {
                squadEffect.ApplyToSquad(levelDelta);
                continue;
            }

            for (int stack = 0; stack < levelDelta; stack++)
            {
                for (int t = 0; t < targets.Count; t++)
                {
                    SquadCharacterController controller = targets[t];
                    if (controller != null)
                    {
                        effect.Apply(controller);
                    }
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
