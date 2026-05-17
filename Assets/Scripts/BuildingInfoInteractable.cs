using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Interaction sur un batiment pour ouvrir le panel d'informations.
[RequireComponent(typeof(Collider))]
public class BuildingInfoInteractable : MonoBehaviour, ICharacterDetectedInteractable
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
    [SerializeField, Tooltip("Force le panel local a rester du cote camera par rapport a l'objet qui l'affiche.")]
    private bool keepLocalPanelBetweenAnchorAndCamera = true;
    [SerializeField, Min(0f), Tooltip("Distance minimale vers la camera pour eviter que le panel reste dans ou derriere l'objet.")]
    private float localPanelCameraSideDistance = 0.45f;
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
    [Tooltip("Collider de reference pour la detection et la validation d'interaction. Laisse vide pour auto-detecter.")]
    public Collider interactionTrigger;
    [Tooltip("Distance maximale a laquelle le personnage peut cibler cette interaction.")]
    public float interactionMaxDistance = 2f;
    [Tooltip("Ferme le panel quand le joueur quitte la zone.")]
    public bool closePanelOnExit = true;
    [Tooltip("Ouvre automatiquement le panel quand le joueur est proche.")]
    public bool openOnProximity = true;
    [Tooltip("Consomme l'input Interact meme si l'objet n'ouvre qu'une UI de proximite.")]
    public bool consumeInteractOnProximity = true;
    [SerializeField, Min(0.02f), Tooltip("Frequence max de rafraichissement du panel deja ouvert.")]
    private float openPanelRefreshInterval = 0.15f;

    [Header("Debug")]
    [SerializeField, Tooltip("Log les evenements BuildingSync. Desactive par defaut pour eviter les spikes de logs en Play Mode.")]
    private bool logBuildingSyncEvents;

    private readonly List<GameObject> charactersInRange = new List<GameObject>();
    private readonly List<Collider> localPanelBoundsColliders = new List<Collider>();
    private readonly List<Renderer> localPanelBoundsRenderers = new List<Renderer>();
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
    private TorchVisionSensitive visibilityGate;
    private bool targetCameraLookupCompleted;
    private bool visibilityGateLookupCompleted;
    private bool attemptedAutoLocalPanelResolution;
    private float nextOpenPanelRefreshTime;
    private float nextVisibilityGateLookupTime;

    private bool localPanelPrefabResolvedAutomatically;
    private bool resolvedAutoLocalPanelForItem;

    private static GameObject sharedBuildingLocalInformationPanelPrefab;
    private static GameObject sharedItemLocalInformationPanelPrefab;
    private static Transform sharedLocalPanelParent;
    private static bool sharedLocalPanelParentLookupCompleted;

    private const string DefaultBuildingLocalPanelPrefabPath = "Assets/Prefabs/UI/LocalBuildingInformationsPanel.prefab";
    private const string DefaultBuildingLocalPanelResourcePath = "Prefabs/UI/LocalBuildingInformationsPanel";
    private const string DefaultBuildingLocalPanelResourceName = "LocalBuildingInformationsPanel";
    private const string DefaultItemLocalPanelPrefabPath = "Assets/Prefabs/UI/LocalItemInformationsPanel.prefab";
    private const string DefaultItemLocalPanelResourcePath = "Prefabs/UI/LocalItemInformationsPanel";
    private const string DefaultItemLocalPanelResourceName = "LocalItemInformationsPanel";
    private const string DefaultLocalPanelParentName = "LocalsInformationsPanels";
    private const float VisibilityGateRetryInterval = 0.5f;

    public string BuildId => buildId;
    public Item BuildingItem => buildingItem;
    public int Level => level;
    public string BuildingItemId => ResolveBuildingItemId(buildingItem, buildId);
    public bool IsHomeChest => buildingItem != null && buildingItem.isHomeChest;
    public ulong NetworkBuildingId => networkBuildingId;
    public string PresentationOrigin => presentationOrigin;

    private void Awake()
    {
        RuntimeOutlineUtility.EnsureOutlineTargets(gameObject);
        EnsureBuildingData();

        InitializeInteractionTrigger();
        ResolveRuntimeReferences();
        RefreshPresentation("awake");
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return controller != null && isActiveAndEnabled && HasBuildingData() && CanDisplayWorldUi();
    }

    public Collider GetInteractionDetectionCollider()
    {
        return ResolveInteractionColliderReference();
    }

    public Transform GetInteractionAnchor()
    {
        return informationAnchor != null ? informationAnchor : transform;
    }

    public float GetInteractionMaxDistance(SquadCharacterController controller)
    {
        return Mathf.Max(0.1f, interactionMaxDistance);
    }

    public int GetInteractionPriority(SquadCharacterController controller)
    {
        return openOnProximity ? 20 : 30;
    }

    public void SetDetectedCharacter(GameObject character)
    {
        if (currentCharacter == character)
        {
            return;
        }

        currentCharacter = character;
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

    public void SetNetworkBuildingId(ulong id)
    {
        networkBuildingId = id;
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        LocalPlayerContext.LocalCharacterChanged += OnLocalCharacterChanged;
        nextOpenPanelRefreshTime = 0f;
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

        if (!CanDisplayWorldUi())
        {
            CloseInfoPanels();
            TrackVisibilityState("update_hidden_by_torch_vision");
            return;
        }

        if (currentCharacter != null && HasBuildingData())
        {
            EnsureLocalPanel();
            if (localPanelInstance != null)
            {
                if (!localPanelInstance.IsOpen || localPanelInstance.CurrentBuilding != this)
                {
                    PrepareLocalPanelForDisplay();
                    localPanelInstance.OpenPanel(this);
                    nextOpenPanelRefreshTime = Time.time + Mathf.Max(0.02f, openPanelRefreshInterval);
                }
                else
                {
                    RefreshOpenLocalPanelIfDue();
                }
            }
        }
        else
        {
            CloseInfoPanels();
        }

        TrackVisibilityState("update");
    }

    private void LateUpdate()
    {
        UpdateOpenLocalPanelPose();
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
        if (!CanDisplayWorldUi())
        {
            return;
        }

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

        if (!CanDisplayWorldUi())
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

            PrepareLocalPanelForDisplay();
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

    private bool ShouldResolveCraftingPanel()
    {
        return openCraftingPanelOnInteract
            && buildingItem != null
            && buildingItem.isBuilding
            && buildingItem.isCraftingBuilding;
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
        if (UsesControllerDrivenDetection())
        {
            return;
        }

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
        if (UsesControllerDrivenDetection())
        {
            return;
        }

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

    private void ResolveRuntimeReferences(bool resolveLocalPanelPrefab = false)
    {
        if (targetCamera == null && (!targetCameraLookupCompleted || resolveLocalPanelPrefab))
        {
            targetCamera = Camera.main;
            targetCameraLookupCompleted = true;
        }

        ResolveVisibilityGate();

        ResolveLocalPanelParent(createIfMissing: Application.isPlaying);

        bool wantsItemPanel = ShouldUseItemInformationPanel();
        if (localPanelPrefabResolvedAutomatically && resolvedAutoLocalPanelForItem != wantsItemPanel)
        {
            CloseInfoPanels();
            if (localPanelInstance != null)
            {
                Destroy(localPanelInstance.gameObject);
                localPanelInstance = null;
            }

            localInformationPanelPrefab = null;
            warnedMissingPrefab = false;
            attemptedAutoLocalPanelResolution = false;
        }

        if (resolveLocalPanelPrefab
            && localInformationPanelPrefab == null
            && (!attemptedAutoLocalPanelResolution || resolvedAutoLocalPanelForItem != wantsItemPanel))
        {
            attemptedAutoLocalPanelResolution = true;
            localInformationPanelPrefab = ResolveDefaultLocalPanelPrefab(wantsItemPanel);
            localPanelPrefabResolvedAutomatically = localInformationPanelPrefab != null;
            resolvedAutoLocalPanelForItem = wantsItemPanel;
        }

        if (localInformationPanelPrefab != null)
        {
            CacheSharedLocalPanelPrefab(localInformationPanelPrefab, wantsItemPanel);
            warnedMissingPrefab = false;
        }

        if (craftingPanel == null && ShouldResolveCraftingPanel())
        {
            craftingPanel = ResolveCraftingPanel();
        }

        runtimeReferencesResolved = targetCamera != null || localInformationPanelPrefab != null || localPanelParent != null || craftingPanel != null;
    }

    private Transform ResolveLocalPanelParent(bool createIfMissing)
    {
        if (IsDefaultLocalPanelParent(localPanelParent))
        {
            sharedLocalPanelParent = localPanelParent;
            return localPanelParent;
        }

        if (IsDefaultLocalPanelParent(sharedLocalPanelParent))
        {
            localPanelParent = sharedLocalPanelParent;
            return localPanelParent;
        }

        Transform resolvedParent = null;
        if (!sharedLocalPanelParentLookupCompleted || createIfMissing)
        {
            resolvedParent = FindDefaultLocalPanelParent();
            sharedLocalPanelParentLookupCompleted = true;
        }

        if (resolvedParent == null && createIfMissing)
        {
            resolvedParent = CreateDefaultLocalPanelParent();
        }

        if (resolvedParent != null)
        {
            localPanelParent = resolvedParent;
            sharedLocalPanelParent = resolvedParent;
            return localPanelParent;
        }

        return localPanelParent;
    }

    private static bool IsDefaultLocalPanelParent(Transform parent)
    {
        return parent != null && parent.name == DefaultLocalPanelParentName;
    }

    private static Transform FindDefaultLocalPanelParent()
    {
        GameObject activeParent = GameObject.Find(DefaultLocalPanelParentName);
        if (activeParent != null)
        {
            return activeParent.transform;
        }

        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != DefaultLocalPanelParentName)
            {
                continue;
            }

            if (!candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static Transform CreateDefaultLocalPanelParent()
    {
        GameObject parentObject = new GameObject(DefaultLocalPanelParentName, typeof(RectTransform));
        RectTransform rect = parentObject.GetComponent<RectTransform>();

        Canvas canvas = FindDefaultLocalPanelCanvas();
        if (canvas != null)
        {
            rect.SetParent(canvas.transform, false);
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(100f, 100f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    private static Canvas FindDefaultLocalPanelCanvas()
    {
#if UNITY_2023_1_OR_NEWER
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
#else
        Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
#endif
        if (canvases == null)
        {
            return null;
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.isRootCanvas && canvas.gameObject.activeInHierarchy)
            {
                return canvas;
            }
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.gameObject.activeInHierarchy)
            {
                return canvas;
            }
        }

        return canvases.Length > 0 ? canvases[0] : null;
    }

    private void ResolveVisibilityGate()
    {
        if (visibilityGate != null)
        {
            return;
        }

        if (visibilityGateLookupCompleted
            && Application.isPlaying
            && Time.time < nextVisibilityGateLookupTime)
        {
            return;
        }

        visibilityGate = GetComponent<TorchVisionSensitive>();
        if (visibilityGate == null)
        {
            visibilityGate = GetComponentInParent<TorchVisionSensitive>(true);
        }

        if (visibilityGate == null)
        {
            visibilityGate = GetComponentInChildren<TorchVisionSensitive>(true);
        }

        if (visibilityGate != null)
        {
            visibilityGateLookupCompleted = true;
            nextVisibilityGateLookupTime = 0f;
            return;
        }

        Transform scope = transform.parent;
        while (scope != null)
        {
            TorchVisionSensitive[] candidates = scope.GetComponentsInChildren<TorchVisionSensitive>(true);
            visibilityGate = SelectClosestVisibilityGate(candidates);
            if (visibilityGate != null)
            {
                visibilityGateLookupCompleted = true;
                nextVisibilityGateLookupTime = 0f;
                return;
            }

            scope = scope.parent;
        }

        visibilityGateLookupCompleted = true;
        nextVisibilityGateLookupTime = Application.isPlaying
            ? Time.time + VisibilityGateRetryInterval
            : 0f;
    }

    private bool CanDisplayWorldUi()
    {
        ResolveVisibilityGate();
        return visibilityGate == null || visibilityGate.IsWorldUiVisible;
    }

    private void OnTransformParentChanged()
    {
        visibilityGate = null;
        visibilityGateLookupCompleted = false;
        nextVisibilityGateLookupTime = 0f;
    }

    private TorchVisionSensitive SelectClosestVisibilityGate(TorchVisionSensitive[] candidates)
    {
        if (candidates == null || candidates.Length == 0)
        {
            return null;
        }

        TorchVisionSensitive best = null;
        int bestHierarchyDistance = int.MaxValue;
        float bestWorldDistance = float.PositiveInfinity;

        for (int i = 0; i < candidates.Length; i++)
        {
            TorchVisionSensitive candidate = candidates[i];
            if (candidate == null)
            {
                continue;
            }

            int hierarchyDistance = GetHierarchyDistance(transform, candidate.transform);
            float worldDistance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (hierarchyDistance < bestHierarchyDistance
                || (hierarchyDistance == bestHierarchyDistance && worldDistance < bestWorldDistance))
            {
                best = candidate;
                bestHierarchyDistance = hierarchyDistance;
                bestWorldDistance = worldDistance;
            }
        }

        return best;
    }

    private static int GetHierarchyDistance(Transform from, Transform to)
    {
        if (from == null || to == null)
        {
            return int.MaxValue;
        }

        if (from == to)
        {
            return 0;
        }

        List<Transform> fromParents = new List<Transform>();
        Transform current = from;
        while (current != null)
        {
            fromParents.Add(current);
            current = current.parent;
        }

        int toDepth = 0;
        current = to;
        while (current != null)
        {
            int fromDepth = fromParents.IndexOf(current);
            if (fromDepth >= 0)
            {
                return fromDepth + toDepth;
            }

            current = current.parent;
            toDepth++;
        }

        return int.MaxValue;
    }

    private bool ShouldUseItemInformationPanel()
    {
        return buildingItem != null && !buildingItem.isBuilding;
    }

    private GameObject ResolveDefaultLocalPanelPrefab(bool wantsItemPanel)
    {
        string prefabPath = wantsItemPanel ? DefaultItemLocalPanelPrefabPath : DefaultBuildingLocalPanelPrefabPath;
        string resourceName = wantsItemPanel ? DefaultItemLocalPanelResourceName : DefaultBuildingLocalPanelResourceName;
        string resourcePath = wantsItemPanel ? DefaultItemLocalPanelResourcePath : DefaultBuildingLocalPanelResourcePath;
        GameObject panelPrefab = null;

#if UNITY_EDITOR
        panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (panelPrefab == null)
        {
            string[] prefabGuids = AssetDatabase.FindAssets($"{resourceName} t:prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (IsMatchingLocalPanelPrefab(panelPrefab, wantsItemPanel))
                {
                    break;
                }

                panelPrefab = null;
            }
        }
#endif
        if (panelPrefab == null)
        {
            panelPrefab = Resources.Load<GameObject>(resourceName);
        }

        if (panelPrefab == null)
        {
            panelPrefab = Resources.Load<GameObject>(resourcePath);
        }

        if (panelPrefab == null)
        {
            panelPrefab = ResolveSharedLocalPanelPrefab(wantsItemPanel);
        }

        return panelPrefab;
    }

    private void CacheSharedLocalPanelPrefab(GameObject panelPrefab, bool itemPanel)
    {
        if (panelPrefab == null)
        {
            return;
        }

        if (itemPanel)
        {
            sharedItemLocalInformationPanelPrefab = panelPrefab;
            return;
        }

        sharedBuildingLocalInformationPanelPrefab = panelPrefab;
    }

    private void EnsureLocalPanel()
    {
        ResolveRuntimeReferences(resolveLocalPanelPrefab: true);
        if (localInformationPanelPrefab == null)
        {
            if (!warnedMissingPrefab)
            {
                Debug.LogWarning("BuildingInfoInteractable: prefab LocalInformationPanel manquant.", this);
                warnedMissingPrefab = true;
            }

            LogMissingWorldUiBinding("local world UI prefab missing after fallback resolution");
            return;
        }

        Transform parent = ResolveLocalPanelParent(createIfMissing: true);
        if (localPanelInstance != null)
        {
            if (parent != null && localPanelInstance.transform.parent != parent)
            {
                localPanelInstance.transform.SetParent(parent, true);
            }

            if (!localPanelInstance.IsOpen)
            {
                UpdateLocalPanelAnchor();
            }

            return;
        }

        GameObject instance = Instantiate(localInformationPanelPrefab, parent, false);
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
        localPanelInstance.faceCamera = true;
        localPanelInstance.ClosePanel();
        if (localPanelInstance.deactivatePanelOnClose && localPanelInstance.informationPanel != null)
        {
            localPanelInstance.informationPanel.SetActive(false);
        }
        UpdateLocalPanelAnchor();
        lastWorldUiBindingFailureReason = string.Empty;
        LogBuildingPresentation(
            "world_ui_initialized",
            "local world UI initialized");
    }

    private void PrepareLocalPanelForDisplay()
    {
        UpdateLocalPanelAnchor(orientToCamera: true);
    }

    private void RefreshOpenLocalPanelIfDue()
    {
        if (localPanelInstance == null)
        {
            return;
        }

        float interval = Mathf.Max(0.02f, openPanelRefreshInterval);
        if (Time.time < nextOpenPanelRefreshTime)
        {
            return;
        }

        nextOpenPanelRefreshTime = Time.time + interval;
        localPanelInstance.RefreshPanel();
    }

    private void UpdateOpenLocalPanelPose()
    {
        if (localPanelInstance == null ||
            !localPanelInstance.IsOpen ||
            localPanelInstance.CurrentBuilding != this)
        {
            return;
        }

        UpdateLocalPanelAnchor(orientToCamera: true);
    }

    private void UpdateLocalPanelAnchor()
    {
        UpdateLocalPanelAnchor(orientToCamera: false);
    }

    private void UpdateLocalPanelAnchor(bool orientToCamera)
    {
        if (localPanelInstance == null)
        {
            return;
        }

        Transform anchor = informationAnchor != null ? informationAnchor : transform;
        PositionLocalPanel(localPanelInstance.transform, anchor, informationOffset, orientToCamera);
    }

    private void PositionLocalPanel(Transform panelTransform, Transform anchor, Vector3 offset, bool orientToCamera)
    {
        if (panelTransform == null || anchor == null)
        {
            return;
        }

        Camera cam = ResolveLocalPanelCamera();
        Vector3 worldPosition = ResolveLocalPanelWorldPosition(anchor, offset, cam);
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
        if (orientToCamera && cam != null && localPanelInstance != null && localPanelInstance.faceCamera)
        {
            Vector3 toCamera = panelTransform.position - cam.transform.position;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                panelTransform.rotation = Quaternion.LookRotation(toCamera);
            }
        }
    }

    private Camera ResolveLocalPanelCamera()
    {
        if (targetCamera != null && targetCamera.isActiveAndEnabled)
        {
            return targetCamera;
        }

        targetCamera = Camera.main;
        return targetCamera;
    }

    private Vector3 ResolveLocalPanelWorldPosition(Transform anchor, Vector3 offset, Camera cam)
    {
        Vector3 anchorPosition = anchor.position;
        if (!keepLocalPanelBetweenAnchorAndCamera || cam == null)
        {
            return anchorPosition + offset;
        }

        Vector3 toCamera = cam.transform.position - anchorPosition;
        float cameraDistance = toCamera.magnitude;
        if (cameraDistance <= 0.0001f)
        {
            return anchorPosition + offset;
        }

        Vector3 toCameraDirection = toCamera / cameraDistance;
        Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, toCameraDirection);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.ProjectOnPlane(Vector3.right, toCameraDirection);
        }

        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.Cross(Vector3.up, toCameraDirection);
        }

        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.right;
        }

        right.Normalize();

        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, toCameraDirection);
        if (up.sqrMagnitude <= 0.0001f)
        {
            up = Vector3.ProjectOnPlane(cam.transform.up, toCameraDirection);
        }

        if (up.sqrMagnitude <= 0.0001f)
        {
            up = Vector3.up;
        }

        up.Normalize();

        float cameraSideDistance = Mathf.Max(localPanelCameraSideDistance, offset.z);
        if (TryResolveObjectFrontDistance(anchorPosition, toCameraDirection, out float objectFrontDistance))
        {
            cameraSideDistance = Mathf.Max(cameraSideDistance, objectFrontDistance + localPanelCameraSideDistance);
        }

        cameraSideDistance = Mathf.Min(cameraSideDistance, Mathf.Max(0f, cameraDistance - 0.05f));
        return anchorPosition
            + right * offset.x
            + up * offset.y
            + toCameraDirection * cameraSideDistance;
    }

    private bool TryResolveObjectFrontDistance(Vector3 origin, Vector3 direction, out float distance)
    {
        distance = 0f;
        if (!TryResolveLocalPanelObjectBounds(out Bounds bounds))
        {
            return false;
        }

        Vector3 centerOffset = bounds.center - origin;
        Vector3 extents = bounds.extents;
        float centerDistance = Vector3.Dot(centerOffset, direction);
        float projectedExtent =
            Mathf.Abs(direction.x) * extents.x +
            Mathf.Abs(direction.y) * extents.y +
            Mathf.Abs(direction.z) * extents.z;

        distance = centerDistance + projectedExtent;
        return distance > 0f;
    }

    private bool TryResolveLocalPanelObjectBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        localPanelBoundsColliders.Clear();
        GetComponentsInChildren(true, localPanelBoundsColliders);
        for (int i = 0; i < localPanelBoundsColliders.Count; i++)
        {
            Collider candidate = localPanelBoundsColliders[i];
            if (candidate == null || !candidate.enabled || candidate.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = candidate.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(candidate.bounds);
            }
        }
        localPanelBoundsColliders.Clear();

        if (hasBounds)
        {
            return true;
        }

        localPanelBoundsRenderers.Clear();
        GetComponentsInChildren(true, localPanelBoundsRenderers);
        for (int i = 0; i < localPanelBoundsRenderers.Count; i++)
        {
            Renderer candidate = localPanelBoundsRenderers[i];
            if (candidate == null || !candidate.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = candidate.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(candidate.bounds);
            }
        }
        localPanelBoundsRenderers.Clear();

        return hasBounds;
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

        bool shouldBindLocalPanel = currentCharacter != null
            || (localPanelInstance != null && localPanelInstance.CurrentBuilding == this);
        if (shouldBindLocalPanel)
        {
            EnsureLocalPanel();
        }

        if (localPanelInstance != null)
        {
            if (!localPanelInstance.IsOpen)
            {
                UpdateLocalPanelAnchor();
            }

            if (localPanelInstance.CurrentBuilding == this)
            {
                localPanelInstance.RefreshPanel();
            }

            LogBuildingPresentation(
                "world_ui_rebound",
                $"World UI is initialized / rebound reason='{reason}'");
        }
        else if (shouldBindLocalPanel)
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
        interactionTrigger = ResolveInteractionColliderReference();
        useSelfTriggerEvents = false;

        if (interactionTrigger == null)
        {
            Debug.LogWarning("BuildingInfoInteractable: aucun collider trouve pour l'interaction.", this);
        }
    }

    private Collider ResolveInteractionColliderReference()
    {
        interactionTrigger = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionTrigger);
        return interactionTrigger;
    }

    private static bool UsesControllerDrivenDetection()
    {
        return true;
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
        return CharacterInteractionDetection.IsCharacterWithinRange(
            character != null ? character.transform : null,
            ResolveInteractionColliderReference(),
            GetInteractionAnchor(),
            interactionMaxDistance);
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
        bool visibilityActive = IsProximityActiveForPresentation();
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
        if (!logBuildingSyncEvents)
        {
            return;
        }

        BuilderController builder = GetComponentInParent<BuilderController>();
        int authoritativeLevel = builder != null && builder.TryGetSyncedBuildingLevel(this, out int syncedLevel)
            ? syncedLevel
            : 0;
        bool worldUiBound = localPanelInstance != null;
        bool proximityActive = IsProximityActiveForPresentation();
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

    private bool IsProximityActiveForPresentation()
    {
        if (!openOnProximity || currentCharacter == null)
        {
            return false;
        }

        return UsesControllerDrivenDetection()
            || charactersInRange.Contains(currentCharacter);
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

    private GameObject ResolveSharedLocalPanelPrefab(bool wantsItemPanel)
    {
        GameObject sharedPrefab = wantsItemPanel
            ? sharedItemLocalInformationPanelPrefab
            : sharedBuildingLocalInformationPanelPrefab;
        if (sharedPrefab != null)
        {
            return sharedPrefab;
        }

        BuildingInfoInteractable[] buildings = Resources.FindObjectsOfTypeAll<BuildingInfoInteractable>();
        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingInfoInteractable building = buildings[i];
            if (building == null || building == this || building.localInformationPanelPrefab == null)
            {
                continue;
            }

            if (!IsMatchingLocalPanelPrefab(building.localInformationPanelPrefab, wantsItemPanel))
            {
                continue;
            }

            CacheSharedLocalPanelPrefab(building.localInformationPanelPrefab, wantsItemPanel);
            return building.localInformationPanelPrefab;
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

            if (!IsMatchingLocalPanelPrefab(panelTemplate, wantsItemPanel))
            {
                continue;
            }

            CacheSharedLocalPanelPrefab(panelTemplate, wantsItemPanel);
            return panelTemplate;
        }

        return null;
    }

    private bool IsMatchingLocalPanelPrefab(GameObject panelPrefab, bool wantsItemPanel)
    {
        if (panelPrefab == null)
        {
            return false;
        }

        string prefabName = panelPrefab.name;
        bool looksLikeItemPanel = prefabName.IndexOf("item", System.StringComparison.OrdinalIgnoreCase) >= 0;
        return wantsItemPanel ? looksLikeItemPanel : !looksLikeItemPanel;
    }
}
