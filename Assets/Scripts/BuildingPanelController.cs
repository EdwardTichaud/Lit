using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Unity.Netcode;
using UnityEngine.UI;

// Controle le panel de construction/amelioration des batiments.
public class BuildingPanelController : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Root du panel de construction.")]
    public GameObject buildingPanel;
    [Tooltip("Desactive le panel a la fermeture.")]
    public bool deactivatePanelOnClose = true;
    [Tooltip("Duree du fade d'ouverture/fermeture.")]
    public float panelFadeDuration = 0.5f;
    [Tooltip("Met l'alpha a 0 au demarrage.")]
    public bool setAlphaToZeroOnStart = true;
    [Tooltip("Ajoute un CanvasGroup si manquant.")]
    public bool addCanvasGroupIfMissing = true;
    [Tooltip("Desactive les raycasts quand cache.")]
    public bool disableRaycastsWhenHidden = true;

    [Header("Slots")]
    [Tooltip("Parent des slots de batiments.")]
    public Transform slotsParent;
    [Tooltip("Prefab d'un slot de batiment.")]
    public GameObject slotPrefab;
    [Tooltip("Curseur de selection des slots.")]
    public RectTransform slotCursor;
    [Tooltip("Controleur de curseur (optionnel).")]
    public CursorController cursorController;
    [Tooltip("Utilise le CursorController si assigne.")]
    public bool useCursorControllerIfAssigned = true;
    [Tooltip("Synchronise les parametres vers le CursorController.")]
    public bool syncCursorControllerSettings = true;
    [Tooltip("Padding ajoute au curseur.")]
    public Vector2 cursorPadding = new Vector2(10f, 10f);
    [Tooltip("Cree un curseur si manquant.")]
    public bool createCursorIfMissing = false;
    [Tooltip("Deadzone du stick pour naviguer.")]
    public float moveDeadzone = 0.5f;
    [Tooltip("Delai avant repetition de navigation.")]
    public float initialRepeatDelay = 0.35f;
    [Tooltip("Intervalle entre repetitions de navigation.")]
    public float repeatInterval = 0.12f;
    [Tooltip("Autorise le wrap du curseur.")]
    public bool wrapCursor = false;

    [Header("Description")]
    [Tooltip("Texte de description du batiment.")]
    public TMP_Text descriptionText;

    [Header("Requirements")]
    [Tooltip("Parent des slots de ressources necessaires.")]
    public Transform requirementsParent;
    [Tooltip("Prefab d'un slot de ressource.")]
    public GameObject requirementSlotPrefab;
    [Tooltip("Couleur quand la ressource est suffisante.")]
    public Color requirementAvailableColor = Color.white;
    [Tooltip("Couleur quand la ressource manque.")]
    public Color requirementMissingColor = new Color(1f, 0.2f, 0.2f, 1f);
    [Tooltip("Cache la liste si aucune ressource n'est requise.")]
    public bool hideRequirementsWhenEmpty = true;
    [Tooltip("Utilise les ressources des coffres maison.")]
    public bool useHomeResources = true;
    [Tooltip("Message si les ressources sont insuffisantes.")]
    public string missingResourcesMessage = "Ressources insuffisantes.";

    [Header("Action Box")]
    [Tooltip("ActionBox du panel.")]
    public GameObject actionBox;
    [Tooltip("Curseur de l'ActionBox.")]
    public RectTransform actionBoxCursor;
    [Tooltip("Padding du curseur ActionBox.")]
    public Vector2 actionBoxCursorPadding = new Vector2(8f, 8f);
    [Tooltip("Cree un curseur ActionBox si manquant.")]
    public bool actionBoxCreateCursorIfMissing = true;
    [Tooltip("Duree du fade de l'ActionBox.")]
    public float actionBoxFadeDuration = 0.15f;
    [Tooltip("Met l'alpha a 0 au demarrage.")]
    public bool actionBoxSetAlphaToZeroOnStart = true;
    [Tooltip("Ajoute un CanvasGroup si manquant.")]
    public bool actionBoxAddCanvasGroupIfMissing = true;
    [Tooltip("Desactive les raycasts quand cache.")]
    public bool actionBoxDisableRaycastsWhenHidden = true;
    [Tooltip("Autorise le wrap du curseur dans l'ActionBox.")]
    public bool actionBoxWrap = true;
    [Tooltip("Alpha du cadre selectionne.")]
    public float actionBoxSelectedFrameAlpha = 1f;
    [Tooltip("Alpha du cadre non selectionne.")]
    public float actionBoxUnselectedFrameAlpha = 0.25f;
    [Tooltip("Alpha du texte selectionne.")]
    public float actionBoxSelectedTextAlpha = 1f;
    [Tooltip("Alpha du texte non selectionne.")]
    public float actionBoxUnselectedTextAlpha = 0.6f;
    [Tooltip("Facteur d'alpha pour une action indisponible.")]
    public float actionBoxDisabledAlpha = 0.4f;
    [Tooltip("Couleur de flash en cas d'action invalide.")]
    public Color actionBoxInvalidFlashColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    [Tooltip("Duree du flash d'erreur.")]
    public float actionBoxInvalidFlashDuration = 0.12f;
    [Tooltip("Nombre de flashes d'erreur.")]
    public int actionBoxInvalidFlashCount = 2;

    [Header("Build Spawn")]
    [Tooltip("Point d'ancrage pour instancier un batiment.")]
    public Transform buildSpawnAnchor;
    [Tooltip("Offset local applique a l'ancrage.")]
    public Vector3 buildSpawnOffset = new Vector3(0f, 0f, 2f);

    [Header("Placement")]
    [Tooltip("Rayon de placement autour du joueur.")]
    public float placementRadius = 5f;
    [Tooltip("Vitesse de deplacement du batiment.")]
    public float placementMoveSpeed = 3f;
    [Tooltip("Distance initiale devant le joueur.")]
    public float placementStartDistance = 1.5f;
    [Tooltip("Deplacement relatif a la camera.")]
    public bool placementUseCameraRelative = true;
    [Tooltip("Camera utilisee pour le placement.")]
    public Camera placementCamera;
    [Tooltip("Snap sur le sol.")]
    public bool placementSnapToGround = true;
    [Tooltip("Layer du sol.")]
    public LayerMask placementGroundMask = ~0;
    [Tooltip("Hauteur de depart du raycast sol.")]
    public float placementGroundRaycastHeight = 2f;
    [Tooltip("Distance du raycast sol.")]
    public float placementGroundRaycastDistance = 6f;
    [Tooltip("Offset vertical applique apres snap.")]
    public float placementGroundOffset = 0f;
    [Tooltip("Layers qui bloquent le placement.")]
    public LayerMask placementCollisionMask = ~0;
    [Tooltip("Layers ignores pour le placement.")]
    public LayerMask placementIgnoreMask = 0;
    [Tooltip("Prend en compte les triggers dans le test.")]
    public bool placementBlockTriggers = false;
    [Tooltip("Padding ajoute aux bounds pour le test de collision.")]
    public float placementBoundsPadding = 0.02f;
    [Tooltip("Affiche un feedback visuel de validite.")]
    public bool placementShowValidity = true;
    [Tooltip("Couleur de placement valide.")]
    public Color placementValidColor = new Color(0.2f, 1f, 0.2f, 0.65f);
    [Tooltip("Couleur de placement invalide.")]
    public Color placementInvalidColor = new Color(1f, 0.2f, 0.2f, 0.65f);
    [Range(0f, 89f)]
    [Tooltip("Angle maximal de pente autorise pour un placement horizontal.")]
    public float placementMaxSlopeAngle = 35f;
    [Tooltip("Hauteur du probe pour les supports muraux.")]
    public float placementWallProbeHeight = 1.2f;
    [Tooltip("Rayon du sphere cast utilise pour detecter un support mural.")]
    public float placementWallProbeRadius = 0.18f;
    [Range(0f, 1f)]
    [Tooltip("Normale maximale sur Y pour considerer un support comme un mur.")]
    public float placementWallNormalMaxY = 0.6f;
    [Tooltip("Message si la position est invalide.")]
    public string placementInvalidMessage = "Position invalide.";
    [Tooltip("Reouvre le panel si la pose est annulee.")]
    public bool placementReopenPanelOnCancel = true;

    private bool panelOpen;
    private bool squadInputLocked;
    private CanvasGroup panelCanvasGroup;
    private Coroutine panelFadeRoutine;
    private bool suppressPanelDeactivate;

    private BuilderController currentBuilder;
    private BuilderController subscribedBuilder;
    private readonly List<BuildingSlotUI> buildingSlots = new List<BuildingSlotUI>();
    private BuildingSlotUI currentFocusedSlot;
    private int lastCursorIndex = -1;
    private int lastMoveDirection;
    private float nextMoveTime;
    private bool cursorDirty;
    private Item restoreSelectedItem;
    private bool isRebuildingSlots;
    private bool pendingRebuildSlots;

    private readonly List<GameObject> requirementSlots = new List<GameObject>();

    private CanvasGroup actionBoxCanvasGroup;
    private Coroutine actionBoxFadeRoutine;
    private Coroutine actionBoxFlashRoutine;
    private bool actionBoxVisible;
    private int actionBoxSuppressFrame = -1;
    private readonly List<ActionBoxEntry> actionBoxEntries = new List<ActionBoxEntry>();
    private int actionBoxIndex = -1;
    private int actionBoxLastDirection;
    private float actionBoxNextMoveTime;
    private bool actionBoxCursorDirty;

    private Maison cachedMaison;

    private bool placementActive;
    private Item placementItem;
    private GameObject placementInstance;
    private Quaternion placementBaseRotation = Quaternion.identity;
    private Transform placementAnchor;
    private Collider placementGroundCollider;
    private readonly WorldPlacementUtility.PreviewCaches placementPreviewCaches = new WorldPlacementUtility.PreviewCaches();
    private Item placementRestoreItem;
    private Camera placementCameraCache;

    public bool IsOpen => panelOpen;

    private void Awake()
    {
        if (!LegacyBuildingSystem.Enabled)
        {
            enabled = false;
            return;
        }

        ResolveSceneReferences();
        ResolvePrefabReferences();
        InitializePanel();
        InitializeActionBox();
    }

    private bool ShouldUseCursorController()
    {
        if (!useCursorControllerIfAssigned)
        {
            return false;
        }

        ResolveCursorController();
        return cursorController != null;
    }

    private void ResolveCursorController()
    {
        if (cursorController == null)
        {
            if (slotCursor != null)
            {
                cursorController = slotCursor.GetComponent<CursorController>();
            }

            if (cursorController == null && buildingPanel != null)
            {
                cursorController = buildingPanel.GetComponentInChildren<CursorController>(true);
            }

            if (cursorController == null && slotsParent != null)
            {
                cursorController = slotsParent.GetComponentInChildren<CursorController>(true);
            }
        }

        if (slotCursor == null && cursorController != null)
        {
            if (cursorController.cursor != null)
            {
                slotCursor = cursorController.cursor;
            }
            else
            {
                slotCursor = cursorController.GetComponent<RectTransform>();
                if (slotCursor != null)
                {
                    cursorController.cursor = slotCursor;
                }
            }
        }
        else if (cursorController != null && cursorController.cursor == null && slotCursor != null)
        {
            cursorController.cursor = slotCursor;
        }

        if (cursorController != null)
        {
            if (cursorController.itemsParent == null && slotsParent != null)
            {
                cursorController.itemsParent = slotsParent as RectTransform;
            }

            if (cursorController.layoutGroup == null && slotsParent != null)
            {
                cursorController.layoutGroup = slotsParent.GetComponent<LayoutGroup>();
            }

            if (syncCursorControllerSettings)
            {
                cursorController.cursorPadding = cursorPadding;
                cursorController.moveDeadzone = moveDeadzone;
                cursorController.initialRepeatDelay = initialRepeatDelay;
                cursorController.repeatInterval = repeatInterval;
                cursorController.wrap = wrapCursor;
            }
        }
    }

    private void SetCursorControllerInputEnabled(bool enabled)
    {
        ResolveCursorController();
        if (cursorController != null)
        {
            cursorController.allowInput = enabled;
        }
    }

    private void RefreshCursorController()
    {
        ResolveCursorController();
        if (cursorController != null)
        {
            if (!cursorController.gameObject.activeSelf)
            {
                cursorController.gameObject.SetActive(true);
            }
            cursorController.Refresh();
        }
    }

    private void SyncSelectionWithCursorController()
    {
        if (buildingSlots.Count == 0)
        {
            UpdateDescription(null);
            UpdateRequirements(null);
            currentFocusedSlot = null;
            lastCursorIndex = -1;
            return;
        }

        ResolveCursorController();
        if (cursorController == null)
        {
            if (currentFocusedSlot == null)
            {
                FocusSlot(buildingSlots[0], false);
            }
            return;
        }

        int index = cursorController.CurrentIndex;
        if (index < 0)
        {
            index = 0;
        }
        if (index >= buildingSlots.Count)
        {
            index = buildingSlots.Count - 1;
        }

        if (index != lastCursorIndex || currentFocusedSlot != buildingSlots[index])
        {
            FocusSlot(buildingSlots[index], false);
            lastCursorIndex = index;
        }
    }


    private void OnEnable()
    {
        if (!LegacyBuildingSystem.Enabled)
        {
            enabled = false;
            return;
        }

        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        LocalInputRouter.Return += OnReturnPerformed;
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;
        LocalInputRouter.Return -= OnReturnPerformed;

        if (placementActive)
        {
            CancelPlacement(false);
        }

        if (panelOpen)
        {
            ClosePanel(true, true);
        }
    }

    private void Update()
    {
        if (!HasInputFocus())
        {
            SetCursorControllerInputEnabled(false);
            return;
        }

        if (placementActive)
        {
            UpdatePlacement();
            return;
        }

        if (!panelOpen)
        {
            SetCursorControllerInputEnabled(false);
            return;
        }

        if (actionBoxVisible)
        {
            SetCursorControllerInputEnabled(false);
            HandleActionBoxNavigation();
        }
        else
        {
            if (ShouldUseCursorController())
            {
                SetCursorControllerInputEnabled(true);
            }
            else
            {
                SetCursorControllerInputEnabled(false);
                HandleNavigation();
            }
        }

        if (!ShouldUseCursorController())
        {
            UpdateCursorVisual();
        }

        UpdateActionBoxCursor();
    }
    private void LateUpdate()
    {
        if (!panelOpen)
        {
            return;
        }

        if (!HasInputFocus())
        {
            return;
        }

        if (ShouldUseCursorController() && !actionBoxVisible)
        {
            SyncSelectionWithCursorController();
        }
    }


#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveSceneReferences();
        ResolvePrefabReferences();
    }
#endif

    public void OpenPanel()
    {
        if (!LegacyBuildingSystem.Enabled)
        {
            return;
        }

        BuilderController builder = currentBuilder;
        if (builder == null)
        {
#if UNITY_2023_1_OR_NEWER
            builder = FindAnyObjectByType<BuilderController>();
#else
            builder = FindAnyObjectByType<BuilderController>();
#endif
        }

        OpenPanel(builder);
    }

    public void OpenPanel(BuilderController builder)
    {
        if (!LegacyBuildingSystem.Enabled)
        {
            return;
        }

        ResolveSceneReferences();
        ResolvePrefabReferences();

        if (panelOpen)
        {
            currentBuilder = builder != null ? builder : currentBuilder;
            SubscribeBuilder(currentBuilder);
            InputFocusStack.Push(this);
            SetSquadInputLock(true);
            return;
        }

        suppressPanelDeactivate = false;
        currentBuilder = builder != null ? builder : currentBuilder;
        SubscribeBuilder(currentBuilder);

        if (buildingPanel == null)
        {
            buildingPanel = gameObject;
        }

        if (buildingPanel != null)
        {
            buildingPanel.SetActive(true);
            panelCanvasGroup = GetPanelCanvasGroup();
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
                if (disableRaycastsWhenHidden)
                {
                    panelCanvasGroup.interactable = false;
                    panelCanvasGroup.blocksRaycasts = false;
                }
            }
        }

        panelOpen = true;
        PlayUiActionAudio(ActionAudioCue.BuildPanelOpen);
        InputFocusStack.Push(this);
        SetSquadInputLock(true);
        actionBoxSuppressFrame = Time.frameCount;
        HideActionBoxImmediate();
        RebuildSlotsSafely();
        FadePanelTo(1f, panelFadeDuration);
    }

    public void ClosePanel()
    {
        ClosePanel(false, false);
    }

    public void ClosePanel(bool keepInputLock)
    {
        ClosePanel(keepInputLock, false);
    }

    public void ClosePanel(bool keepInputLock, bool keepPanelActive)
    {
        if (!panelOpen)
        {
            if (!keepInputLock)
            {
                SetSquadInputLock(false);
            }

            if (!keepPanelActive)
            {
                suppressPanelDeactivate = false;
            }

            return;
        }

        suppressPanelDeactivate = keepPanelActive;
        panelOpen = false;
        PlayUiActionAudio(ActionAudioCue.BuildPanelClose);
        UnsubscribeBuilder();
        InputFocusStack.Pop(this);
        if (!keepInputLock)
        {
            SetSquadInputLock(false);
        }

        currentFocusedSlot = null;
        lastCursorIndex = -1;
        lastMoveDirection = 0;
        nextMoveTime = 0f;
        cursorDirty = false;
        restoreSelectedItem = null;
        actionBoxSuppressFrame = -1;

        ClearSlots();
        ClearRequirements();
        SetCursorControllerInputEnabled(false);
        if (!ShouldUseCursorController())
        {
            HideCursor();
        }
        HideActionBoxImmediate();

        if (descriptionText != null)
        {
            descriptionText.text = string.Empty;
            descriptionText.gameObject.SetActive(false);
        }

        FadePanelTo(0f, panelFadeDuration);
    }

    private void InitializePanel()
    {
        if (buildingPanel == null)
        {
            buildingPanel = gameObject;
        }

        panelCanvasGroup = GetPanelCanvasGroup();
        if (panelCanvasGroup != null && setAlphaToZeroOnStart)
        {
            panelCanvasGroup.alpha = 0f;
            if (disableRaycastsWhenHidden)
            {
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
            }
        }
    }

    private void InitializeActionBox()
    {
        if (actionBox == null)
        {
            Transform found = transform.Find("BuildingActionBox");
            if (found == null)
            {
                found = transform.Find("ActionBox");
            }

            if (found != null)
            {
                actionBox = found.gameObject;
            }
        }

        actionBoxCanvasGroup = GetActionBoxCanvasGroup();
        if (actionBoxCanvasGroup != null && actionBoxSetAlphaToZeroOnStart)
        {
            SetActionBoxAlpha(0f);
        }

        BuildActionBoxEntries();
        HideActionBoxCursor();
    }

    private CanvasGroup GetPanelCanvasGroup()
    {
        if (buildingPanel == null)
        {
            return null;
        }

        CanvasGroup group = buildingPanel.GetComponent<CanvasGroup>();
        if (group == null && addCanvasGroupIfMissing)
        {
            group = buildingPanel.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private CanvasGroup GetActionBoxCanvasGroup()
    {
        if (actionBox == null)
        {
            return null;
        }

        CanvasGroup group = actionBox.GetComponent<CanvasGroup>();
        if (group == null && actionBoxAddCanvasGroupIfMissing)
        {
            group = actionBox.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private void FadePanelTo(float targetAlpha, float duration)
    {
        CanvasGroup canvasGroup = GetPanelCanvasGroup();
        if (canvasGroup == null)
        {
            return;
        }

        if (panelFadeRoutine != null)
        {
            StopCoroutine(panelFadeRoutine);
            panelFadeRoutine = null;
        }

        if (duration <= 0f || !gameObject.activeInHierarchy)
        {
            canvasGroup.alpha = targetAlpha;
            if (disableRaycastsWhenHidden)
            {
                bool visible = targetAlpha > 0.001f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            if (!suppressPanelDeactivate && deactivatePanelOnClose && targetAlpha <= 0.001f && buildingPanel != null)
            {
                buildingPanel.SetActive(false);
            }

            return;
        }

        panelFadeRoutine = StartCoroutine(FadePanelRoutine(canvasGroup, targetAlpha, duration));
    }

    private IEnumerator FadePanelRoutine(CanvasGroup canvasGroup, float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        if (disableRaycastsWhenHidden)
        {
            bool visible = targetAlpha > 0.001f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (!suppressPanelDeactivate && deactivatePanelOnClose && targetAlpha <= 0.001f && buildingPanel != null)
        {
            buildingPanel.SetActive(false);
        }

        panelFadeRoutine = null;
    }

    private void FadeActionBoxTo(float targetAlpha, float duration)
    {
        CanvasGroup canvasGroup = GetActionBoxCanvasGroup();
        if (canvasGroup == null)
        {
            return;
        }

        if (actionBoxFadeRoutine != null)
        {
            StopCoroutine(actionBoxFadeRoutine);
            actionBoxFadeRoutine = null;
        }

        if (duration <= 0f || !gameObject.activeInHierarchy)
        {
            SetActionBoxAlpha(targetAlpha);
            return;
        }

        actionBoxFadeRoutine = StartCoroutine(FadeActionBoxRoutine(canvasGroup, targetAlpha, duration));
    }

    private IEnumerator FadeActionBoxRoutine(CanvasGroup canvasGroup, float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        if (actionBoxDisableRaycastsWhenHidden)
        {
            bool visible = targetAlpha > 0.001f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        actionBoxFadeRoutine = null;
    }

    private void SetActionBoxAlpha(float alpha)
    {
        CanvasGroup canvasGroup = GetActionBoxCanvasGroup();
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = alpha;
        if (actionBoxDisableRaycastsWhenHidden)
        {
            bool visible = alpha > 0.001f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!HasInputFocus())
        {
            return;
        }

        if (placementActive || panelOpen)
        {
            LocalInputRouter.ConsumeInteract();
        }

        if (placementActive)
        {
            TryConfirmPlacement();
            return;
        }

        if (!panelOpen)
        {
            return;
        }

        if (Time.frameCount == actionBoxSuppressFrame)
        {
            actionBoxSuppressFrame = -1;
            return;
        }

        if (!actionBoxVisible)
        {
            ShowActionBox();
            return;
        }

        HandleActionBoxSelection();
        HideActionBox();
    }

    private void OnReturnPerformed(InputAction.CallbackContext context)
    {
        if (!HasInputFocus())
        {
            return;
        }

        if (placementActive)
        {
            CancelPlacement(placementReopenPanelOnCancel);
            return;
        }

        if (!panelOpen)
        {
            return;
        }

        if (actionBoxVisible)
        {
            HideActionBox();
            return;
        }

        ClosePanel();
    }

    private bool HasInputFocus()
    {
        return InputFocusStack.HasFocus(this);
    }

    private void HandleNavigation()
    {
        if (buildingSlots.Count == 0)
        {
            return;
        }

        Vector2 moveInput = LocalInputRouter.MoveValue;
        int direction = GetMoveDirection(moveInput, moveDeadzone);
        if (direction == 0)
        {
            lastMoveDirection = 0;
            nextMoveTime = 0f;
            return;
        }

        float now = Time.unscaledTime;
        if (direction != lastMoveDirection)
        {
            MoveSlot(direction, wrapCursor);
            lastMoveDirection = direction;
            nextMoveTime = now + initialRepeatDelay;
            return;
        }

        if (now >= nextMoveTime)
        {
            MoveSlot(direction, wrapCursor);
            nextMoveTime = now + repeatInterval;
        }
    }

    private int GetMoveDirection(Vector2 input, float deadzone)
    {
        float absX = Mathf.Abs(input.x);
        float absY = Mathf.Abs(input.y);

        if (absX < deadzone && absY < deadzone)
        {
            return 0;
        }

        if (absX >= absY)
        {
            return input.x > 0f ? 2 : -2;
        }

        return input.y > 0f ? -1 : 1;
    }

    private void MoveSlot(int direction, bool wrap)
    {
        BuildingSlotUI current = currentFocusedSlot;
        if (current == null)
        {
            return;
        }

        BuildingSlotUI next = FindNeighborSlot(current, direction, wrap);
        if (next == null || next == current)
        {
            return;
        }

        FocusSlot(next);
    }

    private BuildingSlotUI FindNeighborSlot(BuildingSlotUI current, int direction, bool wrap)
    {
        if (current == null || current.SlotRect == null)
        {
            return null;
        }

        Canvas canvas = buildingPanel != null ? buildingPanel.GetComponentInParent<Canvas>() : null;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (cursorDirty && slotsParent != null)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform itemsRect = slotsParent as RectTransform;
            if (itemsRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRect);
            }
        }

        List<SlotInfo> slotInfos = new List<SlotInfo>(buildingSlots.Count);
        for (int i = 0; i < buildingSlots.Count; i++)
        {
            BuildingSlotUI slot = buildingSlots[i];
            if (slot == null || slot.SlotRect == null)
            {
                continue;
            }

            Vector2 pos = RectTransformUtility.WorldToScreenPoint(uiCamera, slot.SlotRect.position);
            slotInfos.Add(new SlotInfo(slot, pos));
        }

        if (slotInfos.Count == 0)
        {
            return null;
        }

        slotInfos.Sort((a, b) => b.Position.y.CompareTo(a.Position.y));

        float rowTolerance = GetSlotScreenHeight(current, uiCamera) * 0.6f;
        if (rowTolerance <= 0f)
        {
            rowTolerance = 10f;
        }

        List<List<SlotInfo>> rows = new List<List<SlotInfo>>();
        for (int i = 0; i < slotInfos.Count; i++)
        {
            SlotInfo info = slotInfos[i];
            if (rows.Count == 0 || Mathf.Abs(info.Position.y - rows[rows.Count - 1][0].Position.y) > rowTolerance)
            {
                rows.Add(new List<SlotInfo>());
            }

            rows[rows.Count - 1].Add(info);
        }

        int currentRow = -1;
        int currentCol = -1;
        float currentX = 0f;

        for (int r = 0; r < rows.Count; r++)
        {
            rows[r].Sort((a, b) => a.Position.x.CompareTo(b.Position.x));
            for (int c = 0; c < rows[r].Count; c++)
            {
                if (rows[r][c].Slot == current)
                {
                    currentRow = r;
                    currentCol = c;
                    currentX = rows[r][c].Position.x;
                }
            }
        }

        if (currentRow < 0)
        {
            return null;
        }

        if (direction == -2 || direction == 2)
        {
            List<SlotInfo> row = rows[currentRow];
            if (row.Count <= 1)
            {
                return null;
            }

            int nextCol = currentCol + (direction == 2 ? 1 : -1);
            if (nextCol < 0 || nextCol >= row.Count)
            {
                if (!wrap)
                {
                    return null;
                }

                nextCol = direction == 2 ? 0 : row.Count - 1;
                if (nextCol == currentCol)
                {
                    return null;
                }
            }

            return row[nextCol].Slot;
        }

        if (direction == -1 || direction == 1)
        {
            int nextRow = currentRow + (direction == 1 ? 1 : -1);
            if (nextRow < 0 || nextRow >= rows.Count)
            {
                if (!wrap)
                {
                    return null;
                }

                nextRow = direction == 1 ? 0 : rows.Count - 1;
                if (nextRow == currentRow)
                {
                    return null;
                }
            }

            List<SlotInfo> targetRow = rows[nextRow];
            if (targetRow.Count == 0)
            {
                return null;
            }

            SlotInfo best = targetRow[0];
            float bestDx = Mathf.Abs(best.Position.x - currentX);
            for (int i = 1; i < targetRow.Count; i++)
            {
                float dx = Mathf.Abs(targetRow[i].Position.x - currentX);
                if (dx < bestDx)
                {
                    bestDx = dx;
                    best = targetRow[i];
                }
            }

            return best.Slot;
        }

        return null;
    }

    private float GetSlotScreenHeight(BuildingSlotUI slot, Camera uiCamera)
    {
        if (slot == null || slot.SlotRect == null)
        {
            return 0f;
        }

        Vector3[] corners = new Vector3[4];
        slot.SlotRect.GetWorldCorners(corners);
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        for (int i = 0; i < corners.Length; i++)
        {
            float y = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]).y;
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }

        return Mathf.Max(0f, maxY - minY);
    }

    public void FocusSlot(BuildingSlotUI slot, bool syncCursor = true)
    {
        if (slot == null || slot.SlotRect == null)
        {
            return;
        }

        currentFocusedSlot = slot;
        restoreSelectedItem = slot.Item;
        int index = buildingSlots.IndexOf(slot);
        if (index >= 0)
        {
            lastCursorIndex = index;
        }
        cursorDirty = true;
        UpdateDescription(slot.Item);
        UpdateRequirements(slot.Item);
        ApplyActionBoxVisuals();
    }

    private void UpdateCursorVisual()
    {
        if (currentFocusedSlot == null || currentFocusedSlot.SlotRect == null)
        {
            HideCursor();
            return;
        }

        if (!cursorDirty)
        {
            return;
        }

        RectTransform cursor = EnsureSlotCursor(currentFocusedSlot.SlotRect.parent);
        if (cursor == null)
        {
            cursorDirty = false;
            return;
        }

        cursor.gameObject.SetActive(true);
        cursor.SetParent(currentFocusedSlot.SlotRect.parent, false);
        cursor.SetAsLastSibling();
        cursor.position = currentFocusedSlot.SlotRect.position;
        Vector2 size = currentFocusedSlot.SlotRect.rect.size;
        Vector2 padding = cursorPadding;
        cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x + padding.x);
        cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y + padding.y);

        cursorDirty = false;
    }

    private void HideCursor()
    {
        if (slotCursor != null)
        {
            if (slotCursor.GetComponent<CursorController>() == null)
            {
                slotCursor.gameObject.SetActive(false);
            }
        }
    }

    private RectTransform EnsureSlotCursor(Transform parent)
    {
        if (slotCursor != null)
        {
            return slotCursor;
        }

        Transform found = transform.Find("BuildingPanel_Cursor");
        if (found == null)
        {
            RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i] != null && rects[i].name == "BuildingPanel_Cursor")
                {
                    found = rects[i];
                    break;
                }
            }
        }

        if (found != null)
        {
            slotCursor = found as RectTransform;
            return slotCursor;
        }

        if (!createCursorIfMissing || parent == null)
        {
            return null;
        }

        GameObject cursorObject = new GameObject("BuildingPanel_Cursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = cursorObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        Image image = cursorObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.25f);
        image.raycastTarget = false;
        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        image.type = Image.Type.Sliced;
        slotCursor = rect;
        return rect;
    }

    private void UpdateDescription(Item item)
    {
        if (descriptionText == null)
        {
            return;
        }

        string description = string.Empty;
        if (item != null)
        {
            description = item.description;
            if (string.IsNullOrWhiteSpace(description))
            {
                description = !string.IsNullOrWhiteSpace(item.itemName) ? item.itemName : item.name;
            }
        }

        descriptionText.text = description;
        descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(description));
    }

    private void UpdateRequirements(Item building)
    {
        ClearRequirements();
        if (building == null || !building.isBuilding)
        {
            return;
        }

        Dictionary<Item, int> requiredCounts = BuildRequirementCounts(building);
        if (requiredCounts.Count == 0)
        {
            if (requirementsParent != null && hideRequirementsWhenEmpty)
            {
                requirementsParent.gameObject.SetActive(false);
            }
            return;
        }

        if (requirementsParent != null)
        {
            requirementsParent.gameObject.SetActive(true);
        }

        SquadCharacterController controller = GetCurrentCharacterController();
        Dictionary<Item, int> inventoryCounts = BuildInventoryCounts(controller);
        List<InteractableItem> homeContainers = ResolveHomeContainers();

        foreach (KeyValuePair<Item, int> requirement in requiredCounts)
        {
            Item requiredItem = requirement.Key;
            int requiredQuantity = requirement.Value;
            if (requiredItem == null || requiredQuantity <= 0)
            {
                continue;
            }

            int available = 0;
            if (inventoryCounts.TryGetValue(requiredItem, out int invCount))
            {
                available += invCount;
            }

            if (homeContainers != null)
            {
                available += GetHomeItemCount(requiredItem, homeContainers);
            }

            GameObject slot = CreateRequirementSlot();
            if (slot == null)
            {
                continue;
            }

            SetSlotSprite(slot, requiredItem);
            SetSlotQuantityText(slot, $"{available}/{requiredQuantity}", available >= requiredQuantity);
        }
    }

    private void ClearRequirements()
    {
        for (int i = requirementSlots.Count - 1; i >= 0; i--)
        {
            GameObject slot = requirementSlots[i];
            if (slot != null)
            {
                Destroy(slot);
            }
        }

        requirementSlots.Clear();
    }

    private GameObject CreateRequirementSlot()
    {
        Transform parent = requirementsParent != null ? requirementsParent : transform;
        GameObject slot = CreateInstance(requirementSlotPrefab, parent);
        if (slot == null)
        {
            slot = CreateFallbackRequirementSlot(parent);
        }

        if (slot != null)
        {
            requirementSlots.Add(slot);
        }

        return slot;
    }

    private GameObject CreateFallbackRequirementSlot(Transform parent)
    {
        GameObject root = new GameObject("RequirementSlot", typeof(RectTransform));
        if (parent != null)
        {
            root.transform.SetParent(parent, false);
        }

        GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.transform.SetParent(root.transform, false);
        Image iconImage = icon.GetComponent<Image>();
        iconImage.raycastTarget = false;

        GameObject label = new GameObject("Quantity", typeof(RectTransform));
        label.transform.SetParent(root.transform, false);
        TextMeshProUGUI text = label.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 20f;
        return root;
    }

    private void RebuildSlots()
    {
        ClearSlots();

        if (currentBuilder != null)
        {
            currentBuilder.EnsureAvailableBuildings();
        }

        List<Item> available = currentBuilder != null ? currentBuilder.availableBuildings : null;
        if (available == null || available.Count == 0)
        {
            UpdateDescription(null);
            UpdateRequirements(null);
            return;
        }

        BuildingSlotUI firstSlot = null;
        BuildingSlotUI preferredSlot = null;

        for (int i = 0; i < available.Count; i++)
        {
            Item item = available[i];
            if (item == null || !item.isBuilding)
            {
                continue;
            }

            GameObject slotObj = CreateSlotInstance();
            if (slotObj == null)
            {
                continue;
            }

            BuildingInfoInteractable info = null;
            int level = GetBuildingLevel(item, out info);
            BuildingSlotUI slotUi = slotObj.GetComponent<BuildingSlotUI>();
            if (slotUi == null)
            {
                slotUi = slotObj.AddComponent<BuildingSlotUI>();
            }

            slotUi.Initialize(this, item, level, info);
            UpdateSlotVisual(slotObj, item, level);
            buildingSlots.Add(slotUi);

            if (firstSlot == null)
            {
                firstSlot = slotUi;
            }

            if (preferredSlot == null && restoreSelectedItem != null && restoreSelectedItem == item)
            {
                preferredSlot = slotUi;
            }
        }

        if (ShouldUseCursorController())
        {
            RefreshCursorController();
            SyncSelectionWithCursorController();
        }
        else if (preferredSlot != null)
        {
            FocusSlot(preferredSlot);
        }
        else if (firstSlot != null)
        {
            FocusSlot(firstSlot);
        }
        else
        {
            UpdateDescription(null);
            UpdateRequirements(null);
        }
    }

    private void RebuildSlotsSafely()
    {
        if (isRebuildingSlots)
        {
            pendingRebuildSlots = true;
            return;
        }

        isRebuildingSlots = true;
        try
        {
            RebuildSlots();
        }
        finally
        {
            isRebuildingSlots = false;
        }

        if (pendingRebuildSlots)
        {
            pendingRebuildSlots = false;
            RebuildSlotsSafely();
        }
    }

    private GameObject CreateSlotInstance()
    {
        Transform parent = slotsParent != null ? slotsParent : transform;
        GameObject slotObj = CreateInstance(slotPrefab, parent);
        if (slotObj == null)
        {
            slotObj = CreateFallbackSlot(parent);
        }

        return slotObj;
    }

    private GameObject CreateFallbackSlot(Transform parent)
    {
        GameObject root = new GameObject("BuildingSlot", typeof(RectTransform));
        if (parent != null)
        {
            root.transform.SetParent(parent, false);
        }

        GameObject icon = new GameObject("ItemSprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.transform.SetParent(root.transform, false);
        Image iconImage = icon.GetComponent<Image>();
        iconImage.raycastTarget = false;

        GameObject label = new GameObject("Quantity", typeof(RectTransform));
        label.transform.SetParent(root.transform, false);
        TextMeshProUGUI text = label.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.TopRight;
        text.fontSize = 18f;
        return root;
    }

    private void ClearSlots()
    {
        for (int i = buildingSlots.Count - 1; i >= 0; i--)
        {
            BuildingSlotUI slot = buildingSlots[i];
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }

        buildingSlots.Clear();
        currentFocusedSlot = null;
        lastCursorIndex = -1;
        if (!ShouldUseCursorController())
        {
            HideCursor();
        }
    }

    private void UpdateSlotVisual(GameObject slotObj, Item item, int level)
    {
        if (slotObj == null)
        {
            return;
        }

        SetSlotSprite(slotObj, item);

        TMP_Text quantityText = FindSlotQuantityText(slotObj);
        if (quantityText != null)
        {
            quantityText.text = level > 0 ? level.ToString() : string.Empty;
        }
    }

    private void SetSlotSprite(GameObject slotObj, Item item)
    {
        if (slotObj == null)
        {
            return;
        }

        Image image = FindSlotSpriteImage(slotObj);
        if (image == null)
        {
            return;
        }

        Sprite sprite = item != null ? item.itemSprite : null;
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private void SetSlotQuantityText(GameObject slotObj, string text, bool enough)
    {
        TMP_Text tmp = FindSlotQuantityText(slotObj);
        if (tmp == null)
        {
            return;
        }

        tmp.text = text;
        tmp.color = enough ? requirementAvailableColor : requirementMissingColor;
    }

    private TMP_Text FindSlotQuantityText(GameObject slotObj)
    {
        TMP_Text[] texts = slotObj.GetComponentsInChildren<TMP_Text>(true);
        if (texts == null || texts.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text tmp = texts[i];
            if (tmp == null)
            {
                continue;
            }

            string name = tmp.name;
            if (name.IndexOf("quantity", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("count", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("qty", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return tmp;
            }
        }

        return texts[0];
    }

    private Image FindSlotSpriteImage(GameObject slotObj)
    {
        Image[] images = slotObj.GetComponentsInChildren<Image>(true);
        if (images == null || images.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            string name = image.name;
            if (name.IndexOf("itemsprite", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("icon", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }
        }

        return images[0];
    }

    private GameObject CreateInstance(GameObject source, Transform parent)
    {
        if (source == null)
        {
            return null;
        }

        if (parent != null)
        {
            return Instantiate(source, parent);
        }

        return Instantiate(source);
    }

    private int GetBuildingLevel(Item building, out BuildingInfoInteractable info)
    {
        info = null;
        if (building == null || !building.isBuilding)
        {
            return 0;
        }

        if (currentBuilder != null)
        {
            return currentBuilder.GetCurrentLevel(building, currentBuilder.GetUpgradeOriginPosition(), out info);
        }

        return BuildingRuntimeState.GetLevel(building);
    }

    private void HandleActionBoxNavigation()
    {
        if (actionBoxEntries.Count == 0)
        {
            return;
        }

        Vector2 moveInput = LocalInputRouter.MoveValue;
        int direction = GetActionBoxMoveDirection(moveInput, moveDeadzone);
        if (direction == 0)
        {
            actionBoxLastDirection = 0;
            actionBoxNextMoveTime = 0f;
            return;
        }

        float now = Time.unscaledTime;
        if (direction != actionBoxLastDirection)
        {
            MoveActionBox(direction, actionBoxWrap);
            actionBoxLastDirection = direction;
            actionBoxNextMoveTime = now + initialRepeatDelay;
            return;
        }

        if (now >= actionBoxNextMoveTime)
        {
            MoveActionBox(direction, actionBoxWrap);
            actionBoxNextMoveTime = now + repeatInterval;
        }
    }

    private int GetActionBoxMoveDirection(Vector2 input, float deadzone)
    {
        float absX = Mathf.Abs(input.x);
        float absY = Mathf.Abs(input.y);

        if (absX < deadzone && absY < deadzone)
        {
            return 0;
        }

        if (absY >= absX)
        {
            return input.y > 0f ? -1 : 1;
        }

        return input.x > 0f ? 1 : -1;
    }

    private void MoveActionBox(int direction, bool wrap)
    {
        if (actionBoxEntries.Count == 0)
        {
            return;
        }

        if (actionBoxIndex < 0)
        {
            SelectActionBoxIndex(FindFirstAvailableActionIndex(), true);
            return;
        }

        int nextIndex = actionBoxIndex + (direction > 0 ? 1 : -1);
        if (nextIndex < 0 || nextIndex >= actionBoxEntries.Count)
        {
            if (!wrap)
            {
                return;
            }

            nextIndex = nextIndex < 0 ? actionBoxEntries.Count - 1 : 0;
        }

        SelectActionBoxIndex(nextIndex, false);
    }

    private void ShowActionBox()
    {
        if (actionBox == null)
        {
            return;
        }

        BuildActionBoxEntries();
        if (actionBoxEntries.Count == 0)
        {
            return;
        }

        actionBox.SetActive(true);
        actionBoxVisible = true;
        PlayUiActionAudio(ActionAudioCue.UiOpen);
        SelectActionBoxIndex(FindFirstAvailableActionIndex(), true);
        ApplyActionBoxVisuals();
        ShowActionBoxCursor();
        FadeActionBoxTo(1f, actionBoxFadeDuration);
    }

    private void HideActionBox()
    {
        if (!actionBoxVisible)
        {
            return;
        }

        actionBoxVisible = false;
        PlayUiActionAudio(ActionAudioCue.UiClose);
        actionBoxIndex = -1;
        actionBoxLastDirection = 0;
        actionBoxNextMoveTime = 0f;
        actionBoxCursorDirty = false;
        HideActionBoxCursor();
        FadeActionBoxTo(0f, actionBoxFadeDuration);
    }

    private void HideActionBoxImmediate()
    {
        if (actionBoxVisible)
        {
            actionBoxVisible = false;
        }

        actionBoxIndex = -1;
        actionBoxLastDirection = 0;
        actionBoxNextMoveTime = 0f;
        actionBoxCursorDirty = false;
        HideActionBoxCursor();
        FadeActionBoxTo(0f, 0f);
    }

    private void BuildActionBoxEntries()
    {
        actionBoxEntries.Clear();
        if (actionBox == null)
        {
            return;
        }

        Transform container = actionBox.transform.Find("ActionBox_Frame");
        if (container == null)
        {
            container = actionBox.transform;
        }

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child == null)
            {
                continue;
            }

            string name = child.name;
            if (name.IndexOf("ActionBox_", System.StringComparison.OrdinalIgnoreCase) < 0
                && name.IndexOf("BuildingActionBox_", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            RectTransform rect = child as RectTransform;
            if (rect == null)
            {
                rect = child.GetComponent<RectTransform>();
            }

            Image frame = FindActionBoxFrame(child);
            TextMeshProUGUI label = FindActionBoxLabel(child);
            ActionBoxEntry entry = new ActionBoxEntry(rect, frame, label, name);
            actionBoxEntries.Add(entry);
        }

        ApplyActionBoxVisuals();
    }

    private void ApplyActionBoxVisuals()
    {
        for (int i = 0; i < actionBoxEntries.Count; i++)
        {
            ActionBoxEntry entry = actionBoxEntries[i];
            if (entry == null)
            {
                continue;
            }

            bool selected = i == actionBoxIndex;
            bool available = IsActionAvailable(entry);
            float frameAlpha = selected ? actionBoxSelectedFrameAlpha : actionBoxUnselectedFrameAlpha;
            float textAlpha = selected ? actionBoxSelectedTextAlpha : actionBoxUnselectedTextAlpha;
            if (!available)
            {
                frameAlpha *= actionBoxDisabledAlpha;
                textAlpha *= actionBoxDisabledAlpha;
            }

            if (entry.Frame != null)
            {
                Color color = entry.FrameBaseColor;
                color.a = frameAlpha;
                entry.Frame.color = color;
            }

            if (entry.Label != null)
            {
                Color color = entry.LabelBaseColor;
                color.a = textAlpha;
                entry.Label.color = color;
            }
        }

        actionBoxCursorDirty = true;
    }

    private void SelectActionBoxIndex(int index, bool force)
    {
        if (actionBoxEntries.Count == 0)
        {
            actionBoxIndex = -1;
            return;
        }

        int clampedIndex = Mathf.Clamp(index, 0, actionBoxEntries.Count - 1);
        if (!force && actionBoxIndex == clampedIndex)
        {
            return;
        }

        actionBoxIndex = clampedIndex;
        ApplyActionBoxVisuals();
    }

    private int FindFirstAvailableActionIndex()
    {
        for (int i = 0; i < actionBoxEntries.Count; i++)
        {
            if (IsActionAvailable(actionBoxEntries[i]))
            {
                return i;
            }
        }

        return actionBoxEntries.Count > 0 ? 0 : -1;
    }

    private ActionBoxEntry GetCurrentActionBoxEntry()
    {
        if (actionBoxIndex < 0 || actionBoxIndex >= actionBoxEntries.Count)
        {
            return null;
        }

        return actionBoxEntries[actionBoxIndex];
    }

    private void HandleActionBoxSelection()
    {
        ActionBoxEntry entry = GetCurrentActionBoxEntry();
        if (entry == null)
        {
            return;
        }

        string name = entry.Name ?? string.Empty;
        if (ContainsActionName(name, "Construire") || ContainsActionName(name, "Build"))
        {
            if (!TryBuildSelected())
            {
                FlashActionBoxInvalid();
            }

            return;
        }

        if (ContainsActionName(name, "Amelior") || ContainsActionName(name, "Ameliorer")
            || ContainsActionName(name, "Amelioration") || ContainsActionName(name, "Ameli")
            || ContainsActionName(name, "Am\u00E9lior"))
        {
            if (!TryUpgradeSelected())
            {
                FlashActionBoxInvalid();
            }

            return;
        }

        if (ContainsActionName(name, "Annuler") || ContainsActionName(name, "Close") || ContainsActionName(name, "Fermer"))
        {
            return;
        }
    }

    private bool ContainsActionName(string value, string keyword)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        return value.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool IsActionAvailable(ActionBoxEntry entry)
    {
        if (entry == null)
        {
            return false;
        }

        string name = entry.Name ?? string.Empty;
        if (ContainsActionName(name, "Construire") || ContainsActionName(name, "Build"))
        {
            return CanBuildSelected();
        }

        if (ContainsActionName(name, "Amelior") || ContainsActionName(name, "Ameliorer")
            || ContainsActionName(name, "Amelioration") || ContainsActionName(name, "Ameli")
            || ContainsActionName(name, "Am\u00E9lior"))
        {
            return CanUpgradeSelected();
        }

        return true;
    }

    private bool CanBuildSelected()
    {
        if (!TryGetSelectedBuildingState(out Item building, out int currentLevel, out _, out _))
        {
            return false;
        }

        return building != null && building.isBuilding && currentLevel <= 0;
    }

    private bool CanUpgradeSelected()
    {
        if (!TryGetSelectedBuildingState(out Item building, out int currentLevel, out int maxLevel, out _))
        {
            return false;
        }

        return building != null && building.isBuilding && currentLevel > 0 && currentLevel < maxLevel;
    }

    private bool TryBuildSelected()
    {
        if (!TryGetSelectedBuildingState(out Item building, out int currentLevel, out _, out BuildingInfoInteractable info))
        {
            return false;
        }

        if (building == null || !building.isBuilding)
        {
            return false;
        }

        if (currentLevel > 0)
        {
            return false;
        }

        SquadCharacterController controller = GetCurrentCharacterController();
        if (controller == null)
        {
            Debug.LogWarning("BuildingPanelController: aucun personnage pour construire.");
            return false;
        }

        if (!HasBuildingResources(building, controller, out string reason))
        {
            Debug.LogWarning($"BuildingPanelController: {reason}");
            ShowPlacementFeedback(reason);
            return false;
        }

        if (!TryBeginPlacement(building, controller))
        {
            return false;
        }

        placementRestoreItem = building;
        ClosePanel(true, true);
        InputFocusStack.Push(this);
        SetSquadInputLock(true);
        return true;
    }

    private bool TryUpgradeSelected()
    {
        if (!TryGetSelectedBuildingState(out Item building, out int currentLevel, out int maxLevel, out BuildingInfoInteractable info))
        {
            return false;
        }

        if (building == null || !building.isBuilding)
        {
            return false;
        }

        if (currentLevel <= 0 || currentLevel >= maxLevel)
        {
            return false;
        }

        SquadCharacterController controller = GetCurrentCharacterController();
        if (controller == null)
        {
            Debug.LogWarning("BuildingPanelController: aucun personnage pour ameliorer.");
            return false;
        }

        string reason;
        if (IsNetworked() && !IsServer())
        {
            if (!HasBuildingResources(building, controller, out reason))
            {
                Debug.LogWarning($"BuildingPanelController: {reason}");
                ShowPlacementFeedback(reason);
                return false;
            }

            if (currentBuilder != null)
            {
                currentBuilder.RequestUpgrade(info, currentLevel + 1);
            }

            restoreSelectedItem = building;
            RebuildSlotsSafely();
            return true;
        }

        if (!TryConsumeBuildingResources(building, controller, out reason))
        {
            Debug.LogWarning($"BuildingPanelController: {reason}");
            return false;
        }

        int targetLevel = currentLevel + 1;
        BuildingInfoInteractable targetInfo = info != null ? info : SpawnBuildingInstance(building, targetLevel);
        if (targetInfo == null)
        {
            return false;
        }

        if (currentBuilder != null)
        {
            currentBuilder.TryUpgradeBuildingInstance(targetInfo, targetLevel);
            currentBuilder.ApplyBuildingEffects(building, currentLevel, targetLevel - currentLevel);
        }
        else
        {
            targetInfo.SetLevel(targetLevel);
            BuildingRuntimeState.SetLevel(building, targetLevel, true);
        }

        SyncNetworkInventory(controller);
        restoreSelectedItem = building;
        RebuildSlotsSafely();
        PlayActionAudio(ActionAudioCue.BuildUpgrade);
        return true;
    }

    private bool TryGetSelectedBuildingState(out Item building, out int currentLevel, out int maxLevel, out BuildingInfoInteractable info)
    {
        building = null;
        currentLevel = 0;
        maxLevel = 1;
        info = null;

        if (currentFocusedSlot == null || currentFocusedSlot.Item == null)
        {
            return false;
        }

        building = currentFocusedSlot.Item;
        if (!building.isBuilding)
        {
            return false;
        }

        maxLevel = Mathf.Max(1, building.buildingMaxLevel);
        currentLevel = GetBuildingLevel(building, out info);
        return true;
    }

    private bool TryBeginPlacement(Item building, SquadCharacterController controller)
    {
        if (placementActive)
        {
            return false;
        }

        if (building == null || !building.isBuilding)
        {
            return false;
        }

        if (controller == null)
        {
            return false;
        }

        GameObject prefab = building.buildingPrefab != null ? building.buildingPrefab : building.worldPrefab;
        if (prefab == null)
        {
            Debug.LogWarning("BuildingPanelController: prefab de construction manquant.");
            return false;
        }

        placementAnchor = controller.transform;
        placementInstance = Instantiate(prefab);
        if (placementInstance == null)
        {
            return false;
        }

        placementBaseRotation = placementInstance.transform.rotation;

        if (currentBuilder != null)
        {
            currentBuilder.EnsureBuildingParent(placementInstance.transform);
        }

        placementItem = building;
        placementActive = true;
        CachePlacementPhysics(placementInstance);
        SetPlacementCameraOverride(placementInstance.transform);

        Vector3 startPos = WorldPlacementUtility.GetPlacementStartPosition(
            placementAnchor,
            building,
            GetPlacementSettings());
        Quaternion startRotation = placementBaseRotation;
        ResolvePlacementPose(building, startPos, startRotation, out startPos, out startRotation);
        placementInstance.transform.SetPositionAndRotation(startPos, startRotation);
        CachePlacementVisuals(placementInstance);
        UpdatePlacementVisuals(IsPlacementValid());
        PlayActionAudio(ActionAudioCue.BuildPlacementStart);
        return true;
    }

    private WorldPlacementUtility.Settings GetPlacementSettings()
    {
        return new WorldPlacementUtility.Settings
        {
            placementRadius = placementRadius,
            placementStartDistance = placementStartDistance,
            placementUseCameraRelative = placementUseCameraRelative,
            placementCamera = placementCamera,
            placementSnapToGround = placementSnapToGround,
            placementGroundMask = placementGroundMask,
            placementGroundRaycastHeight = placementGroundRaycastHeight,
            placementGroundRaycastDistance = placementGroundRaycastDistance,
            placementGroundOffset = placementGroundOffset,
            placementCollisionMask = placementCollisionMask,
            placementIgnoreMask = placementIgnoreMask,
            placementBlockTriggers = placementBlockTriggers,
            placementBoundsPadding = placementBoundsPadding,
            placementShowValidity = placementShowValidity,
            placementValidColor = placementValidColor,
            placementInvalidColor = placementInvalidColor,
            wallProbeHeight = placementWallProbeHeight,
            wallProbeRadius = placementWallProbeRadius,
            wallNormalMaxY = placementWallNormalMaxY,
            horizontalPlacementMaxSlopeAngle = placementMaxSlopeAngle
        };
    }

    private void UpdatePlacement()
    {
        if (!placementActive)
        {
            return;
        }

        if (placementInstance == null || placementAnchor == null)
        {
            CancelPlacement(false);
            return;
        }

        Vector2 moveInput = LocalInputRouter.MoveValue;
        Vector3 moveDir = GetPlacementMoveDirection(moveInput);
        Vector3 position = placementInstance.transform.position;
        if (moveDir.sqrMagnitude > 0f)
        {
            position += moveDir * placementMoveSpeed * Time.unscaledDeltaTime;
        }

        position = WorldPlacementUtility.ClampPositionAroundAnchor(
            placementAnchor,
            placementItem,
            position,
            GetPlacementSettings());

        Quaternion rotation = placementBaseRotation;
        ResolvePlacementPose(placementItem, position, rotation, out position, out rotation);
        placementInstance.transform.SetPositionAndRotation(position, rotation);

        bool valid = IsPlacementValid();
        UpdatePlacementVisuals(valid);
    }

    private Vector3 GetPlacementMoveDirection(Vector2 input)
    {
        return WorldPlacementUtility.GetPlacementMoveDirection(input, GetPlacementSettings());
    }

    private void ResolvePlacementPose(Item item, Vector3 desiredPosition, Quaternion currentRotation, out Vector3 resolvedPosition, out Quaternion resolvedRotation)
    {
        WorldPlacementUtility.TryResolvePlacementPose(
            item,
            placementInstance,
            placementAnchor,
            placementPreviewCaches,
            GetPlacementSettings(),
            desiredPosition,
            currentRotation,
            ref placementGroundCollider,
            out resolvedPosition,
            out resolvedRotation);
    }

    private Camera ResolvePlacementCamera()
    {
        if (placementCamera != null && placementCamera.isActiveAndEnabled)
        {
            placementCameraCache = placementCamera;
            return placementCamera;
        }

        if (placementCameraCache != null && placementCameraCache.isActiveAndEnabled)
        {
            return placementCameraCache;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            placementCameraCache = mainCamera;
            return mainCamera;
        }

        return null;
    }

    private void SetPlacementCameraOverride(Transform target)
    {
        placementCamera = ResolvePlacementCamera();
    }

    private void ClearPlacementCameraOverride(Transform target)
    {
        placementCameraCache = placementCamera != null && placementCamera.isActiveAndEnabled ? placementCamera : null;
    }

    private void TryConfirmPlacement()
    {
        if (!placementActive || placementInstance == null || placementItem == null)
        {
            CancelPlacement(false);
            return;
        }

        if (!IsPlacementValid())
        {
            PlayUiActionAudio(ActionAudioCue.UiInvalid);
            ShowPlacementFeedback(placementInvalidMessage);
            return;
        }

        SquadCharacterController controller = GetCurrentCharacterController();
        if (controller == null)
        {
            CancelPlacement(false);
            return;
        }

        string resourceReason;
        if (IsNetworked() && !IsServer())
        {
            if (!HasBuildingResources(placementItem, controller, out resourceReason))
            {
                ShowPlacementFeedback(resourceReason);
                CancelPlacement(false);
                return;
            }

            if (currentBuilder != null)
            {
                currentBuilder.RequestBuild(placementItem, placementInstance.transform.position, placementInstance.transform.rotation);
            }

            RestorePlacementPhysics();
            ClearPlacementVisuals();
            ClearPlacementCameraOverride(placementInstance.transform);
            placementActive = false;
            placementItem = null;
            placementInstance = null;
            placementBaseRotation = Quaternion.identity;
            placementAnchor = null;
            placementGroundCollider = null;
            placementRestoreItem = null;
            SetSquadInputLock(false);
            InputFocusStack.Pop(this);
            return;
        }

        if (!TryConsumeBuildingResources(placementItem, controller, out resourceReason))
        {
            ShowPlacementFeedback(resourceReason);
            CancelPlacement(false);
            return;
        }

        Item placedItem = placementItem;
        RestorePlacementPhysics();
        ClearPlacementVisuals();
        ClearPlacementCameraOverride(placementInstance != null ? placementInstance.transform : null);

        BuildingInfoInteractable info = ConfigurePlacedBuilding(placementInstance, placementItem, 1);
        if (currentBuilder != null)
        {
            currentBuilder.RegisterBuiltBuilding(placementItem, 1, info);
            currentBuilder.ApplyBuildingEffects(placementItem, 0, 1);
        }
        else if (placementItem != null)
        {
            BuildingRuntimeState.SetLevel(placementItem, 1, true);
        }

        SyncNetworkInventory(controller);
        placementActive = false;
        placementItem = null;
        placementInstance = null;
        placementBaseRotation = Quaternion.identity;
        placementAnchor = null;
        placementGroundCollider = null;
        placementRestoreItem = null;
        SetSquadInputLock(false);
        InputFocusStack.Pop(this);
        if (placedItem != null)
        {
            PlayActionAudio(ActionAudioCue.BuildComplete);
            ShowPlacementFeedback(placedItem.GetPlaceSuccessMessage());
        }
    }

    private void CancelPlacement(bool reopenPanel)
    {
        if (!placementActive)
        {
            return;
        }

        RestorePlacementPhysics();
        ClearPlacementVisuals();
        ClearPlacementCameraOverride(placementInstance != null ? placementInstance.transform : null);
        if (placementInstance != null)
        {
            Destroy(placementInstance);
        }

        placementActive = false;
        placementItem = null;
        placementInstance = null;
        placementBaseRotation = Quaternion.identity;
        placementAnchor = null;
        placementGroundCollider = null;
        InputFocusStack.Pop(this);
        SetSquadInputLock(false);

        Item restore = placementRestoreItem;
        placementRestoreItem = null;
        if (reopenPanel)
        {
            restoreSelectedItem = restore;
            OpenPanel(currentBuilder);
        }

        PlayUiActionAudio(ActionAudioCue.InventoryPlaceCancel);
    }

    private BuildingInfoInteractable ConfigurePlacedBuilding(GameObject instance, Item building, int level)
    {
        if (instance == null || building == null || !building.isBuilding)
        {
            return null;
        }

        if (currentBuilder != null)
        {
            currentBuilder.EnsureBuildingParent(instance.transform);
        }
        else
        {
#if UNITY_2023_1_OR_NEWER
            BuilderController builder = FindAnyObjectByType<BuilderController>();
#else
            BuilderController builder = FindAnyObjectByType<BuilderController>();
#endif
            if (builder != null)
            {
                builder.EnsureBuildingParent(instance.transform);
            }
        }

        BuildingInfoInteractable info = instance.GetComponent<BuildingInfoInteractable>();
        if (info == null)
        {
            info = instance.AddComponent<BuildingInfoInteractable>();
        }

        info.Initialize(GetBuildingItemId(building), building, Mathf.Max(1, level));

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

    private void CachePlacementPhysics(GameObject instance)
    {
        WorldPlacementUtility.CachePlacementPhysics(instance, placementPreviewCaches);
    }

    private void RestorePlacementPhysics()
    {
        WorldPlacementUtility.RestorePlacementPhysics(placementPreviewCaches);
    }

    private bool IsPlacementValid()
    {
        return WorldPlacementUtility.IsPlacementValid(
            placementItem,
            placementInstance,
            placementAnchor,
            placementGroundCollider,
            placementPreviewCaches,
            GetPlacementSettings());
    }

    private void CachePlacementVisuals(GameObject instance)
    {
        WorldPlacementUtility.CachePlacementVisuals(instance, placementPreviewCaches, placementShowValidity);
    }

    private void UpdatePlacementVisuals(bool isValid)
    {
        WorldPlacementUtility.UpdatePlacementVisuals(
            placementPreviewCaches,
            placementShowValidity,
            isValid,
            placementValidColor,
            placementInvalidColor);
    }

    private void ClearPlacementVisuals()
    {
        WorldPlacementUtility.ClearPlacementVisuals(placementPreviewCaches);
    }

    private void ShowPlacementFeedback(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        InfoBoxUI.TryShow(message);
    }

    private void PlayActionAudio(ActionAudioCue cue)
    {
        if (cue == ActionAudioCue.None)
        {
            return;
        }

        AudioManager manager = AudioManager.EnsureInstance();
        if (manager != null)
        {
            SquadCharacterController controller = GetCurrentCharacterController();
            Vector3 position = controller != null ? controller.transform.position : transform.position;
            manager.PlayActionCue(cue, position);
        }
    }

    private void PlayUiActionAudio(ActionAudioCue cue)
    {
        if (cue == ActionAudioCue.None)
        {
            return;
        }

        AudioManager manager = AudioManager.EnsureInstance();
        if (manager != null)
        {
            manager.PlayUiActionCue(cue);
        }
    }

    private BuildingInfoInteractable SpawnBuildingInstance(Item building, int level)
    {
        if (building == null)
        {
            return null;
        }

        GameObject prefab = building.buildingPrefab != null ? building.buildingPrefab : building.worldPrefab;
        if (prefab == null)
        {
            Debug.LogWarning("BuildingPanelController: prefab de construction manquant.");
            return null;
        }

        Transform anchor = ResolveSpawnAnchor();
        Vector3 position = anchor != null ? anchor.TransformPoint(buildSpawnOffset) : transform.position;
        Quaternion rotation = anchor != null ? anchor.rotation : Quaternion.identity;

        GameObject instance = Instantiate(prefab, position, rotation);
        if (instance == null)
        {
            return null;
        }

        RuntimeOutlineUtility.EnsureOutlineTargets(instance);
        if (currentBuilder != null)
        {
            currentBuilder.EnsureBuildingParent(instance.transform);
        }
        else
        {
#if UNITY_2023_1_OR_NEWER
            BuilderController builder = FindAnyObjectByType<BuilderController>();
#else
            BuilderController builder = FindAnyObjectByType<BuilderController>();
#endif
            if (builder != null)
            {
                builder.EnsureBuildingParent(instance.transform);
            }
        }

        BuildingInfoInteractable info = instance.GetComponent<BuildingInfoInteractable>();
        if (info == null)
        {
            info = instance.AddComponent<BuildingInfoInteractable>();
        }

        info.Initialize(GetBuildingItemId(building), building, level);

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

    private Transform ResolveSpawnAnchor()
    {
        if (buildSpawnAnchor != null)
        {
            return buildSpawnAnchor;
        }

        if (currentBuilder != null)
        {
            return currentBuilder.transform;
        }

        if (SquadManager.Instance != null && SquadManager.Instance.currentCharacter != null)
        {
            return SquadManager.Instance.currentCharacter.transform;
        }

        return null;
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
        else
        {
            container.allowTake = false;
        }
    }

    private string GetBuildingItemId(Item building)
    {
        if (building == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(building.itemId))
        {
            return building.itemId;
        }

        if (!string.IsNullOrWhiteSpace(building.itemName))
        {
            return building.itemName;
        }

        return building.name;
    }

    private SquadCharacterController GetCurrentCharacterController()
    {
        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        if (controlled == null)
        {
            return null;
        }

        return controlled.GetComponent<SquadCharacterController>();
    }

    private void SubscribeBuilder(BuilderController builder)
    {
        if (subscribedBuilder == builder)
        {
            return;
        }

        if (subscribedBuilder != null)
        {
            subscribedBuilder.BuildingsChanged -= OnBuildingsChanged;
        }

        subscribedBuilder = builder;
        if (subscribedBuilder != null)
        {
            subscribedBuilder.BuildingsChanged += OnBuildingsChanged;
        }
    }

    private void UnsubscribeBuilder()
    {
        if (subscribedBuilder != null)
        {
            subscribedBuilder.BuildingsChanged -= OnBuildingsChanged;
            subscribedBuilder = null;
        }
    }

    private void OnBuildingsChanged()
    {
        if (!panelOpen)
        {
            return;
        }

        RebuildSlotsSafely();
    }

    private static bool IsNetworked()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    private static bool IsServer()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }

    private void SyncNetworkInventory(SquadCharacterController controller)
    {
        if (!IsNetworked() || !IsServer() || controller == null)
        {
            return;
        }

        NetworkInventory inventory = controller.GetComponent<NetworkInventory>();
        if (inventory == null)
        {
            inventory = controller.GetComponentInChildren<NetworkInventory>(true);
        }

        if (inventory != null)
        {
            inventory.SyncFromController();
        }
    }

    private void SetSquadInputLock(bool locked)
    {
        if (SquadManager.Instance == null)
        {
            return;
        }

        if (locked)
        {
            if (squadInputLocked)
            {
                return;
            }

            SquadManager.Instance.SetInputLocked(true);
            squadInputLocked = true;
            return;
        }

        if (!squadInputLocked)
        {
            return;
        }

        SquadManager.Instance.SetInputLocked(false);
        squadInputLocked = false;
    }

    private Dictionary<Item, int> BuildRequirementCounts(Item building)
    {
        Dictionary<Item, int> counts = new Dictionary<Item, int>();
        if (building == null || building.buildingRequirements == null)
        {
            return counts;
        }

        for (int i = 0; i < building.buildingRequirements.Count; i++)
        {
            Item.BuildingRequirement requirement = building.buildingRequirements[i];
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

            if (!counts.TryGetValue(item, out int count))
            {
                counts[item] = 1;
            }
            else
            {
                counts[item] = count + 1;
            }
        }

        return counts;
    }

    private List<InteractableItem> ResolveHomeContainers()
    {
        if (!useHomeResources)
        {
            return null;
        }

        Maison maison = GetMaison();
        if (maison == null)
        {
            return null;
        }

        List<InteractableItem> containers = maison.ResolveMaisonLootContainers(null);
        return containers != null && containers.Count > 0 ? containers : null;
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

    private bool HasBuildingResources(Item building, SquadCharacterController controller, out string reason)
    {
        reason = missingResourcesMessage;
        if (building == null || !building.isBuilding)
        {
            return true;
        }

        if (building.buildingRequirements == null || building.buildingRequirements.Count == 0)
        {
            return true;
        }

        if (controller == null)
        {
            return false;
        }

        Dictionary<Item, int> requiredCounts = BuildRequirementCounts(building);
        Dictionary<Item, int> inventoryCounts = BuildInventoryCounts(controller);
        List<InteractableItem> homeContainers = ResolveHomeContainers();

        foreach (KeyValuePair<Item, int> requirement in requiredCounts)
        {
            Item requiredItem = requirement.Key;
            int requiredQuantity = requirement.Value;

            int available = 0;
            if (inventoryCounts.TryGetValue(requiredItem, out int invCount))
            {
                available += invCount;
            }

            if (homeContainers != null)
            {
                available += GetHomeItemCount(requiredItem, homeContainers);
            }

            if (available < requiredQuantity)
            {
                return false;
            }
        }

        return true;
    }

    private bool TryConsumeBuildingResources(Item building, SquadCharacterController controller, out string reason)
    {
        reason = missingResourcesMessage;
        if (building == null || !building.isBuilding)
        {
            return true;
        }

        if (building.buildingRequirements == null || building.buildingRequirements.Count == 0)
        {
            return true;
        }

        if (controller == null)
        {
            return false;
        }

        Dictionary<Item, int> requiredCounts = BuildRequirementCounts(building);
        Dictionary<Item, int> inventoryCounts = BuildInventoryCounts(controller);
        List<InteractableItem> homeContainers = ResolveHomeContainers();

        foreach (KeyValuePair<Item, int> requirement in requiredCounts)
        {
            Item requiredItem = requirement.Key;
            int requiredQuantity = requirement.Value;

            int available = 0;
            if (inventoryCounts.TryGetValue(requiredItem, out int invCount))
            {
                available += invCount;
            }

            if (homeContainers != null)
            {
                available += GetHomeItemCount(requiredItem, homeContainers);
            }

            if (available < requiredQuantity)
            {
                return false;
            }
        }

        foreach (KeyValuePair<Item, int> requirement in requiredCounts)
        {
            Item requiredItem = requirement.Key;
            int remaining = requirement.Value;
            if (inventoryCounts.TryGetValue(requiredItem, out int invCount))
            {
                int fromInventory = Mathf.Min(invCount, remaining);
                if (fromInventory > 0)
                {
                    controller.TryRemoveItemQuantity(requiredItem, fromInventory);
                    remaining -= fromInventory;
                }
            }

            if (remaining > 0 && homeContainers != null)
            {
                remaining -= RemoveFromHomeContainers(requiredItem, remaining, homeContainers);
            }

            if (remaining > 0)
            {
                return false;
            }
        }

        return true;
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
        cachedMaison = FindAnyObjectByType<Maison>();
#else
        cachedMaison = FindAnyObjectByType<Maison>();
#endif

        return cachedMaison;
    }

    private void UpdateActionBoxCursor()
    {
        if (!actionBoxVisible)
        {
            HideActionBoxCursor();
            return;
        }

        ActionBoxEntry entry = GetCurrentActionBoxEntry();
        if (entry == null || entry.Rect == null)
        {
            HideActionBoxCursor();
            return;
        }

        if (actionBoxCursorDirty)
        {
            Canvas.ForceUpdateCanvases();
            Transform container = actionBox != null ? actionBox.transform.Find("ActionBox_Frame") : null;
            RectTransform containerRect = container as RectTransform;
            if (containerRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
            }
        }

        Transform parent = actionBox != null ? actionBox.transform : entry.Rect.parent;
        RectTransform cursor = EnsureActionBoxCursor(parent);
        if (cursor == null)
        {
            actionBoxCursorDirty = false;
            return;
        }

        cursor.gameObject.SetActive(true);
        cursor.SetParent(parent, false);
        cursor.SetAsLastSibling();
        cursor.pivot = new Vector2(0.5f, 0.5f);
        cursor.position = entry.Rect.position;
        Vector2 size = entry.Rect.rect.size;
        Vector2 padding = actionBoxCursorPadding;
        cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x + padding.x);
        cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y + padding.y);

        actionBoxCursorDirty = false;
    }

    private void HideActionBoxCursor()
    {
        if (actionBoxCursor != null)
        {
            actionBoxCursor.gameObject.SetActive(false);
        }
    }

    private void ShowActionBoxCursor()
    {
        RectTransform cursor = EnsureActionBoxCursor(actionBox != null ? actionBox.transform : transform);
        if (cursor == null)
        {
            return;
        }

        cursor.gameObject.SetActive(true);
        UpdateActionBoxCursor();
    }

    private RectTransform EnsureActionBoxCursor(Transform parent)
    {
        if (actionBoxCursor != null)
        {
            return actionBoxCursor;
        }

        if (actionBox != null)
        {
            Transform found = actionBox.transform.Find("ActionBox_Cursor");
            if (found == null)
            {
                RectTransform[] rects = actionBox.GetComponentsInChildren<RectTransform>(true);
                for (int i = 0; i < rects.Length; i++)
                {
                    if (rects[i] != null && rects[i].name == "ActionBox_Cursor")
                    {
                        found = rects[i];
                        break;
                    }
                }
            }

            if (found != null)
            {
                actionBoxCursor = found as RectTransform;
                return actionBoxCursor;
            }
        }

        if (!actionBoxCreateCursorIfMissing || parent == null)
        {
            return null;
        }

        GameObject cursorObject = new GameObject("ActionBox_Cursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        RectTransform rect = cursorObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        Image image = cursorObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.25f);
        image.raycastTarget = false;
        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        image.type = Image.Type.Sliced;
        LayoutElement layout = cursorObject.GetComponent<LayoutElement>();
        layout.ignoreLayout = true;
        actionBoxCursor = rect;
        return rect;
    }

    private void FlashActionBoxInvalid()
    {
        ActionBoxEntry entry = GetCurrentActionBoxEntry();
        if (entry == null)
        {
            return;
        }

        PlayUiActionAudio(ActionAudioCue.UiInvalid);
        if (actionBoxFlashRoutine != null)
        {
            StopCoroutine(actionBoxFlashRoutine);
        }

        actionBoxFlashRoutine = StartCoroutine(FlashActionBoxInvalidRoutine(entry));
    }

    private IEnumerator FlashActionBoxInvalidRoutine(ActionBoxEntry entry)
    {
        if (entry == null)
        {
            yield break;
        }

        int flashes = Mathf.Max(1, actionBoxInvalidFlashCount);
        float duration = Mathf.Max(0.01f, actionBoxInvalidFlashDuration);

        for (int i = 0; i < flashes; i++)
        {
            ApplyActionBoxFlash(entry, actionBoxInvalidFlashColor);
            yield return new WaitForSecondsRealtime(duration);
            ApplyActionBoxFlash(entry, null);
            yield return new WaitForSecondsRealtime(duration);
        }

        actionBoxFlashRoutine = null;
    }

    private void ApplyActionBoxFlash(ActionBoxEntry entry, Color? flashColor)
    {
        if (entry == null)
        {
            return;
        }

        if (entry.Frame != null)
        {
            if (flashColor.HasValue)
            {
                entry.Frame.color = flashColor.Value;
            }
            else
            {
                entry.Frame.color = entry.FrameBaseColor;
            }
        }

        if (entry.Label != null)
        {
            if (flashColor.HasValue)
            {
                entry.Label.color = flashColor.Value;
            }
            else
            {
                entry.Label.color = entry.LabelBaseColor;
            }
        }
    }

    private Image FindActionBoxFrame(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Image[] images = root.GetComponentsInChildren<Image>(true);
        if (images == null || images.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            if (image.name.IndexOf("frame", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }
        }

        return images[0];
    }

    private TextMeshProUGUI FindActionBoxLabel(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts == null || texts.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (text.name.IndexOf("text", System.StringComparison.OrdinalIgnoreCase) >= 0
                || text.name.IndexOf("label", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return text;
            }
        }

        return texts[0];
    }

    private void ResolveSceneReferences()
    {
        if (buildingPanel == null)
        {
            buildingPanel = gameObject;
        }

        if (slotsParent == null)
        {
            Transform found = transform.Find("BuildingPanel_Frame");
            if (found != null)
            {
                slotsParent = found;
            }
        }

        if (descriptionText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == "BuildingPanel_Description_Text")
                {
                    descriptionText = texts[i];
                    break;
                }
            }
        }

        if (requirementsParent == null)
        {
            Transform found = transform.Find("BuildingPanel_NecessaryResources");
            if (found != null)
            {
                requirementsParent = found;
            }
        }

        if (slotCursor == null)
        {
            Transform found = transform.Find("BuildingPanel_Cursor");
            if (found != null)
            {
                slotCursor = found as RectTransform;
            }
        }

        if (cursorController == null)
        {
            if (slotCursor != null)
            {
                cursorController = slotCursor.GetComponent<CursorController>();
            }

            if (cursorController == null)
            {
                cursorController = GetComponentInChildren<CursorController>(true);
            }
        }

        if (actionBox == null)
        {
            Transform found = transform.Find("BuildingActionBox");
            if (found != null)
            {
                actionBox = found.gameObject;
            }
        }

        if (actionBoxCursor == null && actionBox != null)
        {
            Transform found = actionBox.transform.Find("ActionBox_Cursor");
            if (found != null)
            {
                actionBoxCursor = found as RectTransform;
            }
        }
    }

    private void ResolvePrefabReferences()
    {
#if UNITY_EDITOR
        if (slotPrefab == null)
        {
            slotPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BuildingSlot.prefab");
        }

        if (requirementSlotPrefab == null)
        {
            requirementSlotPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/NecessaryResourcesSlot.prefab");
        }
#endif
    }

    private readonly struct SlotInfo
    {
        public SlotInfo(BuildingSlotUI slot, Vector2 position)
        {
            Slot = slot;
            Position = position;
        }

        public BuildingSlotUI Slot { get; }
        public Vector2 Position { get; }
    }

    private sealed class ActionBoxEntry
    {
        public ActionBoxEntry(RectTransform rect, Image frame, TextMeshProUGUI label, string name)
        {
            Rect = rect;
            Frame = frame;
            Label = label;
            Name = name;
            FrameBaseColor = frame != null ? frame.color : Color.white;
            LabelBaseColor = label != null ? label.color : Color.white;
        }

        public RectTransform Rect { get; }
        public Image Frame { get; }
        public TextMeshProUGUI Label { get; }
        public string Name { get; }
        public Color FrameBaseColor { get; }
        public Color LabelBaseColor { get; }
    }
}

public class BuildingSlotUI : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public BuildingPanelController Owner { get; private set; }
    public Item Item { get; private set; }
    public int Level { get; private set; }
    public BuildingInfoInteractable Info { get; private set; }
    public RectTransform SlotRect { get; private set; }

    public void Initialize(BuildingPanelController owner, Item item, int level, BuildingInfoInteractable info)
    {
        Owner = owner;
        Item = item;
        Level = Mathf.Max(0, level);
        Info = info;
        SlotRect = GetComponent<RectTransform>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Owner != null)
        {
            Owner.FocusSlot(this);
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (Owner != null)
        {
            Owner.FocusSlot(this);
        }
    }
}
