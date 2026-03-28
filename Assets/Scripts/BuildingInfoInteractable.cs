using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Interaction sur un batiment pour ouvrir le panel d'informations.
[RequireComponent(typeof(Collider))]
public class BuildingInfoInteractable : MonoBehaviour
{
    [Header("Building Data")]
    [SerializeField, Tooltip("Identifiant de construction (fallback si Item manquant).")]
    private string buildId;
    [SerializeField, Tooltip("Item building associe.")]
    private Item buildingItem;
    [SerializeField, Tooltip("Niveau actuel de cette instance.")]
    private int level = 1;
    [SerializeField, HideInInspector]
    private ulong networkBuildingId;

    [Header("Info")]
    [Tooltip("Prefab du panel d'informations local.")]
    public GameObject localInformationPanelPrefab;
    [Tooltip("Parent du panel instancie.")]
    public Transform localPanelParent;
    [Tooltip("Point d'ancrage du panel local.")]
    public Transform informationAnchor;
    [Tooltip("Offset du panel local.")]
    public Vector3 informationOffset = new Vector3(1.75f, 1.6f, 0f);
    [Tooltip("Camera utilisee pour placer le panel local en screen space.")]
    public Camera targetCamera;
    [Tooltip("Detruit le panel local a la sortie.")]
    public bool destroyPanelOnExit = false;

    [Header("Crafting Construction Panel")]
    [Tooltip("Panel de craft (optionnel).")]
    public CraftingConstructionPanel craftingPanel;
    [Tooltip("Ouvre le panel de craft quand l'interaction est faite sur un building de craft.")]
    public bool openCraftingPanelOnInteract = true;
    [Tooltip("Tag du panel de craft.")]
    public string craftingPanelTag = "CraftingConstructionPanel";

    [Header("Interaction")]
    [Tooltip("Trigger d'interaction. Laisse vide pour auto-detecter.")]
    public Collider interactionTrigger;
    [Tooltip("Ferme le panel quand le joueur quitte la zone.")]
    public bool closePanelOnExit = true;
    [Tooltip("Ouvre automatiquement le panel quand le joueur est proche.")]
    public bool openOnProximity = true;
    [Tooltip("Consomme l'input Interact meme si l'objet n'ouvre qu'une UI de proximite.")]
    public bool consumeInteractOnProximity = true;

    private readonly List<GameObject> charactersInRange = new List<GameObject>();
    private readonly Dictionary<GameObject, int> characterColliderCounts = new Dictionary<GameObject, int>();
    private GameObject currentCharacter;
    private bool useSelfTriggerEvents;
    private LocalBuildingInformationsPanelController localPanelInstance;
    private bool warnedMissingPrefab;
    private bool runtimeReferencesResolved;
    private bool lastVisibilityActive;
    private int lastLoggedDisplayedLevel = int.MinValue;
    private bool lastLoggedWorldUiBound;
    private bool lastLoggedProximityActive;
    private int lastLoggedAuthoritativeLevel = int.MinValue;
    private string lastPresentationLogSignature = string.Empty;
    private string presentationOrigin = "unknown";
    private string lastWorldUiBindingFailureReason = string.Empty;

    private static GameObject sharedLocalInformationPanelPrefab;

    private const string DefaultLocalPanelPrefabPath = "Assets/Prefabs/UI/LocalBuildingInformationsPanel.prefab";
    private const string DefaultLocalPanelResourcePath = "Prefabs/UI/LocalBuildingInformationsPanel";
    private const string DefaultLocalPanelResourceName = "LocalBuildingInformationsPanel";
    private const string DefaultLocalPanelParentName = "LocalsBuildingInformationsPanels";

    public string BuildId => buildId;
    public Item BuildingItem => buildingItem;
    public int Level => level;
    public string BuildingItemId => ResolveBuildingItemId(buildingItem, buildId);
    public bool IsHomeChest => buildingItem != null && buildingItem.isHomeChest;
    public ulong NetworkBuildingId => networkBuildingId;
    public string PresentationOrigin => presentationOrigin;

    private void Awake()
    {
        EnsureBuildingData();

        InitializeInteractionTrigger();
        ResolveRuntimeReferences();
        RefreshPresentation("awake");
    }

    public void SetNetworkBuildingId(ulong id)
    {
        networkBuildingId = id;
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        LocalPlayerContext.LocalCharacterChanged += OnLocalCharacterChanged;
        RefreshPresentation("on_enable");
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;
        LocalPlayerContext.LocalCharacterChanged -= OnLocalCharacterChanged;

        CloseInfoPanels();
        ResetState();
    }

    private void Update()
    {
        if (!openOnProximity)
        {
            return;
        }

        ResolveRuntimeReferences();
        RefreshControlledCharacterOverlap();
        RefreshCurrentCharacter();
        EnsureBuildingData();
        EnsureLocalPanel();

        if (currentCharacter != null && HasBuildingData())
        {
            if (localPanelInstance != null)
            {
                UpdateLocalPanelAnchor();
                if (!localPanelInstance.IsOpen || localPanelInstance.CurrentBuilding != this)
                {
                    localPanelInstance.OpenPanel(this);
                }
                else
                {
                    localPanelInstance.RefreshPanel();
                }
            }
        }
        else
        {
            CloseInfoPanels();
        }

        TrackVisibilityState("update");
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
        ResolveRuntimeReferences();
        EnsureBuildingData();
        if (openOnProximity)
        {
            if (InputFocusStack.HasAnyFocus())
            {
                return;
            }

            RefreshCurrentCharacter();
            if (currentCharacter == null)
            {
                return;
            }

            if (!HasBuildingData())
            {
                Debug.LogWarning("BuildingInfoInteractable: aucune donnee de construction.", this);
                return;
            }

            if (TryOpenCraftingPanel())
            {
                LocalInputRouter.ConsumeInteract();
                return;
            }

            if (TryApplyInteractEffects())
            {
                LocalInputRouter.ConsumeInteract();
                return;
            }

            if (consumeInteractOnProximity)
            {
                LocalInputRouter.ConsumeInteract();
            }
            return;
        }

        EnsureLocalPanel();
        HandleInteract();
    }

    private void HandleInteract()
    {
        if (!CanProcessInteract())
        {
            return;
        }

        RefreshCurrentCharacter();
        if (currentCharacter == null)
        {
            return;
        }

        EnsureBuildingData();
        if (!HasBuildingData())
        {
            Debug.LogWarning("BuildingInfoInteractable: aucune donnee de construction.", this);
            return;
        }

        LocalInputRouter.ConsumeInteract();

        if (TryOpenCraftingPanel())
        {
            return;
        }

        TryApplyInteractEffects();

        EnsureLocalPanel();
        if (localPanelInstance != null)
        {
            if (localPanelInstance.IsOpen && localPanelInstance.CurrentBuilding == this)
            {
                localPanelInstance.ClosePanel();
                return;
            }

            localPanelInstance.OpenPanel(this);
            return;
        }
    }

    private bool TryApplyInteractEffects()
    {
        if (buildingItem == null || !buildingItem.isBuilding)
        {
            return false;
        }

        if (currentCharacter == null)
        {
            return false;
        }

        SquadCharacterController controller = currentCharacter.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            return false;
        }

        IReadOnlyList<Effect> effects = buildingItem.GetBuildingEffectsForLevel(level);
        if (effects == null || effects.Count == 0)
        {
            return false;
        }

        bool applied = false;
        for (int i = 0; i < effects.Count; i++)
        {
            Effect effect = effects[i];
            if (effect == null)
            {
                continue;
            }

            if (effect is IBuildingInteractEffect interactEffect)
            {
                if (interactEffect.ApplyOnInteract(controller, buildingItem, level))
                {
                    applied = true;
                }
            }
        }

        return applied;
    }

    private bool TryOpenCraftingPanel()
    {
        if (!openCraftingPanelOnInteract)
        {
            return false;
        }

        if (buildingItem == null || !buildingItem.isBuilding || !buildingItem.isCraftingBuilding)
        {
            return false;
        }

        if (currentCharacter == null)
        {
            return false;
        }

        SquadCharacterController controller = currentCharacter.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            return false;
        }

        CraftingConstructionPanel panel = craftingPanel != null ? craftingPanel : ResolveCraftingPanel();
        craftingPanel = panel;

        if (panel == null)
        {
            return false;
        }

        panel.OpenPanel(this, controller);
        return true;
    }

    private CraftingConstructionPanel ResolveCraftingPanel()
    {
        CraftingConstructionPanel panel = null;
        if (!string.IsNullOrWhiteSpace(craftingPanelTag))
        {
            try
            {
                GameObject tagged = GameObject.FindGameObjectWithTag(craftingPanelTag);
                if (tagged != null)
                {
                    panel = tagged.GetComponentInChildren<CraftingConstructionPanel>(true);
                }
            }
            catch (UnityException)
            {
                // Tag not defined, ignore.
            }
        }

        if (panel == null)
        {
#if UNITY_2023_1_OR_NEWER
            panel = FindFirstObjectByType<CraftingConstructionPanel>();
#else
            panel = FindObjectOfType<CraftingConstructionPanel>();
#endif
        }

        return panel;
    }

    private bool CanProcessInteract()
    {
        if (openOnProximity)
        {
            return false;
        }

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
        if (currentCharacter == null && closePanelOnExit)
        {
            CloseInfoPanels();
            if (destroyPanelOnExit && localPanelInstance != null)
            {
                Destroy(localPanelInstance.gameObject);
                localPanelInstance = null;
            }
        }
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

    private void RefreshControlledCharacterOverlap()
    {
        RemoveNullCharacters();

        GameObject controlled = GetControlledCharacter();
        if (controlled == null)
        {
            currentCharacter = null;
            return;
        }

        bool overlaps = IsCharacterWithinInteraction(controlled);
        bool contains = charactersInRange.Contains(controlled);
        if (overlaps && !contains)
        {
            charactersInRange.Add(controlled);
            characterColliderCounts[controlled] = 1;
            LogBuildingPresentation(
                "building_visibility_activated",
                "local client visibility logic activates via overlap rescan");
        }
        else if (!overlaps && contains)
        {
            charactersInRange.Remove(controlled);
            characterColliderCounts.Remove(controlled);
            if (currentCharacter == controlled)
            {
                currentCharacter = null;
            }

            LogBuildingPresentation(
                "building_visibility_deactivated",
                "local client visibility logic deactivates because controlled character left interaction range");
        }
    }

    private void OnLocalCharacterChanged(Transform _)
    {
        RefreshPresentation("local_character_changed");
        LogBuildingPresentation(
            "world_ui_rebound",
            "World UI is initialized / rebound after local character changed");
    }

    private static GameObject GetControlledCharacter()
    {
        return LocalPlayerUtils.GetControlledCharacter();
    }

    private void ResetState()
    {
        charactersInRange.Clear();
        characterColliderCounts.Clear();
        currentCharacter = null;
        lastVisibilityActive = false;
    }

    private void EnsureBuildingData()
    {
        if (level < 1)
        {
            level = 1;
        }

        if (string.IsNullOrWhiteSpace(buildId) && buildingItem != null)
        {
            buildId = ResolveBuildingItemId(buildingItem, string.Empty);
        }
    }

    private bool HasBuildingData()
    {
        return buildingItem != null || !string.IsNullOrWhiteSpace(buildId);
    }

    private void ResolveRuntimeReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (localPanelParent == null)
        {
            GameObject parentObject = GameObject.Find(DefaultLocalPanelParentName);
            if (parentObject != null)
            {
                localPanelParent = parentObject.transform;
            }
        }

        if (localInformationPanelPrefab == null)
        {
#if UNITY_EDITOR
            localInformationPanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultLocalPanelPrefabPath);
#endif
            if (localInformationPanelPrefab == null)
            {
                localInformationPanelPrefab = Resources.Load<GameObject>(DefaultLocalPanelResourceName);
            }

            if (localInformationPanelPrefab == null)
            {
                localInformationPanelPrefab = Resources.Load<GameObject>(DefaultLocalPanelResourcePath);
            }

            if (localInformationPanelPrefab == null)
            {
                localInformationPanelPrefab = ResolveSharedLocalPanelPrefab();
            }
        }

        if (localInformationPanelPrefab != null)
        {
            sharedLocalInformationPanelPrefab = localInformationPanelPrefab;
            warnedMissingPrefab = false;
        }

        if (craftingPanel == null)
        {
            craftingPanel = ResolveCraftingPanel();
        }

        runtimeReferencesResolved = targetCamera != null || localInformationPanelPrefab != null || localPanelParent != null || craftingPanel != null;
    }

    private void EnsureLocalPanel()
    {
        ResolveRuntimeReferences();
        if (localInformationPanelPrefab == null)
        {
            if (!warnedMissingPrefab)
            {
                Debug.LogWarning("BuildingInfoInteractable: prefab LocalBuildingInformationPanel manquant.", this);
                warnedMissingPrefab = true;
            }

            LogMissingWorldUiBinding("local world UI prefab missing after fallback resolution");
            return;
        }

        if (localPanelInstance != null)
        {
            UpdateLocalPanelAnchor();
            return;
        }

        Transform parent = localPanelParent != null ? localPanelParent : null;
        GameObject instance = Instantiate(localInformationPanelPrefab, parent);
        if (instance == null)
        {
            return;
        }

        CanvasGroup group = instance.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = instance.AddComponent<CanvasGroup>();
        }

        localPanelInstance = instance.GetComponent<LocalBuildingInformationsPanelController>();
        if (localPanelInstance == null)
        {
            localPanelInstance = instance.AddComponent<LocalBuildingInformationsPanelController>();
        }

        localPanelInstance.informationPanel = instance;
        localPanelInstance.ClosePanel();
        if (localPanelInstance.deactivatePanelOnClose && localPanelInstance.informationPanel != null)
        {
            localPanelInstance.informationPanel.SetActive(false);
        }
        lastWorldUiBindingFailureReason = string.Empty;
        LogBuildingPresentation(
            "world_ui_initialized",
            "local world UI initialized");
    }

    private void UpdateLocalPanelAnchor()
    {
        if (localPanelInstance == null)
        {
            return;
        }

        Transform anchor = informationAnchor != null ? informationAnchor : transform;
        PositionLocalPanel(localPanelInstance.transform, anchor, informationOffset);
    }

    private void PositionLocalPanel(Transform panelTransform, Transform anchor, Vector3 offset)
    {
        if (panelTransform == null || anchor == null)
        {
            return;
        }

        Vector3 worldPosition = anchor.position + offset;
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        Canvas canvas = panelTransform.GetComponentInParent<Canvas>();
        RectTransform rect = panelTransform as RectTransform;

        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
        {
            if (cam == null || rect == null)
            {
                return;
            }

            Vector3 screenPos = cam.WorldToScreenPoint(worldPosition);
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                rect.position = screenPos;
            }
            else
            {
                RectTransform canvasRect = canvas.transform as RectTransform;
                if (canvasRect == null)
                {
                    rect.position = screenPos;
                }
                else if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPos, cam, out Vector3 worldPoint))
                {
                    rect.position = worldPoint;
                }
            }

            return;
        }

        panelTransform.position = worldPosition;
        if (cam != null && localPanelInstance != null && localPanelInstance.faceCamera)
        {
            Vector3 toCamera = panelTransform.position - cam.transform.position;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                panelTransform.rotation = Quaternion.LookRotation(toCamera);
            }
        }
    }

    private void CloseInfoPanels()
    {
        if (localPanelInstance != null && localPanelInstance.IsOpen && localPanelInstance.CurrentBuilding == this)
        {
            localPanelInstance.ClosePanel();
        }
    }

    public void Initialize(Item item, int levelValue = 1)
    {
        Initialize(string.Empty, item, levelValue);
    }

    public void Initialize(string buildIdValue, Item item, int levelValue = 1)
    {
        int previousLevel = level;
        buildingItem = item;
        buildId = !string.IsNullOrWhiteSpace(buildIdValue) ? buildIdValue : ResolveBuildingItemId(item, buildId);
        level = Mathf.Max(1, levelValue);
        RefreshPresentation(previousLevel != level ? "initialize_level_changed" : "initialize");
    }

    public void SetLevel(int levelValue)
    {
        int previousLevel = level;
        level = Mathf.Max(1, levelValue);
        if (previousLevel != level)
        {
            LogBuildingPresentation(
                "building_level_changed",
                $"upgrade level changes previousLevel={previousLevel} nextLevel={level}");
        }

        RefreshPresentation(previousLevel != level ? "set_level_changed" : "set_level");
    }

    public void MarkPresentationOrigin(string source, bool overwrite = false)
    {
        string safeSource = string.IsNullOrWhiteSpace(source) ? "unknown" : source;
        if (!overwrite
            && !string.IsNullOrWhiteSpace(presentationOrigin)
            && !string.Equals(presentationOrigin, "unknown", System.StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(presentationOrigin, safeSource, System.StringComparison.Ordinal))
        {
            return;
        }

        presentationOrigin = safeSource;
        LogBuildingPresentation(
            "building_origin_marked",
            $"building presentation origin set to {safeSource}");
    }

    public void RefreshPresentation(string reason)
    {
        EnsureBuildingData();
        ResolveRuntimeReferences();
        RefreshControlledCharacterOverlap();
        RefreshCurrentCharacter();
        EnsureLocalPanel();

        if (localPanelInstance != null)
        {
            UpdateLocalPanelAnchor();
            if (localPanelInstance.CurrentBuilding == this)
            {
                localPanelInstance.RefreshPanel();
            }

            LogBuildingPresentation(
                "world_ui_rebound",
                $"World UI is initialized / rebound reason='{reason}'");
        }
        else
        {
            LogMissingWorldUiBinding($"world UI still unbound reason='{reason}'");
        }

        LogBuildingPresentation(
            "building_visual_refresh",
            $"visual refresh method ran reason='{reason}'");
        TrackVisibilityState(reason);
    }

    private static string ResolveBuildingItemId(Item item, string fallback)
    {
        if (item != null)
        {
            if (!string.IsNullOrWhiteSpace(item.itemId))
            {
                return item.itemId;
            }

            if (!string.IsNullOrWhiteSpace(item.itemName))
            {
                return item.itemName;
            }

            if (!string.IsNullOrWhiteSpace(item.name))
            {
                return item.name;
            }
        }

        return fallback ?? string.Empty;
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
            Debug.LogWarning("BuildingInfoInteractable: aucun collider trouve pour l'interaction.", this);
            useSelfTriggerEvents = false;
            return;
        }

        if (IsConcaveMeshCollider(interactionTrigger))
        {
            Collider fallback = CreateBoxTrigger(interactionTrigger);
            if (fallback != null)
            {
                interactionTrigger = fallback;
                Debug.LogWarning("BuildingInfoInteractable: MeshCollider concave detecte, ajout d'un BoxCollider Trigger pour l'interaction.", this);
            }
        }
        else if (!interactionTrigger.isTrigger)
        {
            interactionTrigger.isTrigger = true;
            Debug.LogWarning("BuildingInfoInteractable: le collider d'interaction n'etait pas en Trigger. Il a ete force en Trigger.", this);
        }

        useSelfTriggerEvents = interactionTrigger.gameObject == gameObject;
        if (!useSelfTriggerEvents)
        {
            BuildingInfoTriggerProxy proxy = interactionTrigger.GetComponent<BuildingInfoTriggerProxy>();
            if (proxy == null)
            {
                proxy = interactionTrigger.gameObject.AddComponent<BuildingInfoTriggerProxy>();
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

        GameObject controlled = GetControlledCharacter();
        if (controlled != null && IsColliderFromCharacter(other, controlled))
        {
            return controlled;
        }

        if (SquadManager.Instance == null || SquadManager.Instance.squadCharacters == null)
        {
            SquadCharacterController fallbackController = other.GetComponentInParent<SquadCharacterController>();
            return fallbackController != null ? fallbackController.gameObject : null;
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

    private bool IsCharacterWithinInteraction(GameObject character)
    {
        if (character == null || interactionTrigger == null)
        {
            return false;
        }

        Collider[] colliders = character.GetComponentsInChildren<Collider>(true);
        Bounds triggerBounds = interactionTrigger.bounds;
        bool hadCollider = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            hadCollider = true;
            if (triggerBounds.Intersects(collider.bounds) || triggerBounds.Contains(collider.bounds.center))
            {
                return true;
            }
        }

        return !hadCollider && triggerBounds.Contains(character.transform.position);
    }

    private static bool IsColliderFromCharacter(Collider other, GameObject character)
    {
        if (other == null || character == null)
        {
            return false;
        }

        Transform otherTransform = other.transform;
        Transform characterTransform = character.transform;
        return otherTransform == characterTransform
            || otherTransform.IsChildOf(characterTransform)
            || characterTransform.IsChildOf(otherTransform);
    }

    private void RemoveNullCharacters()
    {
        for (int i = charactersInRange.Count - 1; i >= 0; i--)
        {
            if (charactersInRange[i] != null)
            {
                continue;
            }

            charactersInRange.RemoveAt(i);
        }

        List<GameObject> toRemove = null;
        foreach (KeyValuePair<GameObject, int> pair in characterColliderCounts)
        {
            if (pair.Key != null)
            {
                continue;
            }

            if (toRemove == null)
            {
                toRemove = new List<GameObject>();
            }

            toRemove.Add(pair.Key);
        }

        if (toRemove == null)
        {
            return;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            characterColliderCounts.Remove(toRemove[i]);
        }
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

    private void TrackVisibilityState(string reason)
    {
        bool visibilityActive = openOnProximity && currentCharacter != null && charactersInRange.Contains(currentCharacter);
        if (visibilityActive == lastVisibilityActive)
        {
            return;
        }

        lastVisibilityActive = visibilityActive;
        LogBuildingPresentation(
            visibilityActive ? "building_visibility_activated" : "building_visibility_deactivated",
            visibilityActive
                ? $"local client visibility logic activates reason='{reason}'"
                : $"local client visibility logic deactivates reason='{reason}'");
    }

    private void LogBuildingPresentation(string eventName, string reason)
    {
        BuilderController builder = GetComponentInParent<BuilderController>();
        int authoritativeLevel = builder != null && builder.TryGetSyncedBuildingLevel(this, out int syncedLevel)
            ? syncedLevel
            : 0;
        bool worldUiBound = localPanelInstance != null;
        bool proximityActive = openOnProximity && currentCharacter != null && charactersInRange.Contains(currentCharacter);
        PersistentNetworkObject persistentObject = GetComponent<PersistentNetworkObject>();
        string persistentId = persistentObject != null ? persistentObject.PersistentId : string.Empty;
        string localCharacterPath = currentCharacter != null
            ? PersistentWorldDebug.DescribeTransform(currentCharacter.transform)
            : string.Empty;
        string signature =
            $"{eventName}|{persistentId}|{BuildId}|{BuildingItemId}|{NetworkBuildingId}|{level}|{authoritativeLevel}|{worldUiBound}|{proximityActive}|{presentationOrigin}|{localCharacterPath}|{reason}";

        bool forceLog =
            eventName == "building_reconstructed" ||
            eventName == "building_runtime_spawned" ||
            eventName == "building_level_changed" ||
            eventName == "world_ui_initialized" ||
            eventName == "world_ui_rebound" ||
            eventName == "world_ui_binding_missing" ||
            eventName == "building_visibility_activated" ||
            eventName == "building_visibility_deactivated";

        if (!forceLog
            && signature == lastPresentationLogSignature
            && lastLoggedDisplayedLevel == level
            && lastLoggedWorldUiBound == worldUiBound
            && lastLoggedProximityActive == proximityActive
            && lastLoggedAuthoritativeLevel == authoritativeLevel)
        {
            return;
        }

        lastPresentationLogSignature = signature;
        lastLoggedDisplayedLevel = level;
        lastLoggedWorldUiBound = worldUiBound;
        lastLoggedProximityActive = proximityActive;
        lastLoggedAuthoritativeLevel = authoritativeLevel;

        Debug.Log(
            $"[BuildingSync] event='{eventName}' path='{PersistentWorldDebug.DescribeTransform(transform)}' persistentId='{persistentId}' buildId='{BuildId}' itemId='{BuildingItemId}' networkId={networkBuildingId} displayedLevel={level} authoritativeSyncedLevel={authoritativeLevel} worldUiBound={worldUiBound} proximityActive={proximityActive} visibilityLogicActive={openOnProximity} localCharacterPath='{localCharacterPath}' upgradeRefreshCallbackRan={(eventName == "building_upgrade_refresh_callback")} visualRefreshRan={(eventName == "building_visual_refresh" || eventName == "world_ui_rebound" || eventName == "world_ui_initialized")} source='{presentationOrigin}' reason='{reason}'",
            this);
    }

    private void LogMissingWorldUiBinding(string reason)
    {
        if (lastWorldUiBindingFailureReason == reason)
        {
            return;
        }

        lastWorldUiBindingFailureReason = reason;
        LogBuildingPresentation("world_ui_binding_missing", reason);
    }

    private GameObject ResolveSharedLocalPanelPrefab()
    {
        if (sharedLocalInformationPanelPrefab != null)
        {
            return sharedLocalInformationPanelPrefab;
        }

        BuildingInfoInteractable[] buildings = Resources.FindObjectsOfTypeAll<BuildingInfoInteractable>();
        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingInfoInteractable building = buildings[i];
            if (building == null || building == this || building.localInformationPanelPrefab == null)
            {
                continue;
            }

            sharedLocalInformationPanelPrefab = building.localInformationPanelPrefab;
            return sharedLocalInformationPanelPrefab;
        }

        LocalBuildingInformationsPanelController[] panels = Resources.FindObjectsOfTypeAll<LocalBuildingInformationsPanelController>();
        for (int i = 0; i < panels.Length; i++)
        {
            LocalBuildingInformationsPanelController panel = panels[i];
            if (panel == null)
            {
                continue;
            }

            GameObject panelTemplate = panel.informationPanel != null ? panel.informationPanel : panel.gameObject;
            if (panelTemplate == null)
            {
                continue;
            }

            sharedLocalInformationPanelPrefab = panelTemplate;
            return sharedLocalInformationPanelPrefab;
        }

        return null;
    }
}

public class BuildingInfoTriggerProxy : MonoBehaviour
{
    public BuildingInfoInteractable Owner { get; set; }

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
