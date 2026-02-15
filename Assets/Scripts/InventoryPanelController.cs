using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Controle l'UI d'inventaire: navigation, ActionBox, depot et placement d'objets.
public class InventoryPanelController : MonoBehaviour
{
    private struct InventoryEntry
    {
        public Item item;
        public int quantity;
    }

    private PlayerInputs playerInputs;
    private bool inventoryOpen;
    private bool squadInputLocked;
    [Header("Action Box")]
    [Tooltip("ActionBox utilisee par l'inventaire.")]
    public GameObject actionBox;
    [Tooltip("Offset en UI par rapport au slot selectionne.")]
    public Vector2 actionBoxOffset = new Vector2(0f, 0f);
    [Tooltip("Duree du fade de l'ActionBox.")]
    public float actionBoxFadeDuration = 0.5f;
    [Tooltip("Met l'alpha a 0 au demarrage.")]
    public bool actionBoxSetAlphaToZeroOnStart = true;
    [Tooltip("Ajoute un CanvasGroup si manquant.")]
    public bool actionBoxAddCanvasGroupIfMissing = true;
    [Tooltip("Desactive les raycasts quand cache.")]
    public bool actionBoxDisableRaycastsWhenHidden = true;

    [Header("Action Box Navigation")]
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

    [Header("Action Box Feedback")]
    [Tooltip("Couleur de flash en cas d'action invalide.")]
    public Color actionBoxInvalidFlashColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    [Tooltip("Duree du flash d'erreur.")]
    public float actionBoxInvalidFlashDuration = 0.12f;
    [Tooltip("Nombre de flashes d'erreur.")]
    public int actionBoxInvalidFlashCount = 2;

    [Header("Action Box Cursor")]
    [Tooltip("Curseur de l'ActionBox.")]
    public RectTransform actionBoxCursor;
    [Tooltip("Padding du curseur ActionBox.")]
    public Vector2 actionBoxCursorPadding = new Vector2(8f, 8f);
    [Tooltip("Cree un curseur si manquant.")]
    public bool actionBoxCreateCursorIfMissing = true;

    [Header("Deposit Quantity")]
    [Tooltip("Panel pour choisir une quantite a deposer.")]
    public GameObject depositQuantityPanel;
    [Tooltip("Texte affiche dans le panel de quantite.")]
    public TextMeshProUGUI depositQuantityText;
    [Tooltip("Offset en UI par rapport au slot selectionne.")]
    public Vector2 depositQuantityPanelOffset = new Vector2(0f, 0f);
    [Tooltip("Duree du fade du panel de quantite.")]
    public float depositQuantityFadeDuration = 0.15f;
    [Tooltip("Met l'alpha a 0 au demarrage.")]
    public bool depositQuantitySetAlphaToZeroOnStart = true;
    [Tooltip("Ajoute un CanvasGroup si manquant.")]
    public bool depositQuantityAddCanvasGroupIfMissing = true;
    [Tooltip("Desactive les raycasts quand cache.")]
    public bool depositQuantityDisableRaycastsWhenHidden = true;
    [Tooltip("Cree le panel si manquant.")]
    public bool depositQuantityCreateIfMissing = true;
    [Tooltip("Format d'affichage (quantite/total).")]
    public string depositQuantityFormat = "Deposer {0}/{1}";

    [Header("Item Placement")]
    [Tooltip("Rayon de placement autour du joueur.")]
    public float placementRadius = 5f;
    [Tooltip("Vitesse de deplacement de l'objet place.")]
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
    [Tooltip("Cree un LootContainer si le prefab n'en a pas.")]
    public bool placementCreateLootContainer = true;
    [Tooltip("Detruit le LootContainer si vide apres depot.")]
    public bool placementDestroyWhenEmpty = true;
    [Tooltip("Message si l'objet ne peut pas etre pose.")]
    public string placementCannotPlaceMessage = "Cet objet ne peut pas etre pose.";
    [Tooltip("Message si la position est invalide.")]
    public string placementInvalidMessage = "Position invalide.";
    [Tooltip("Duree d'affichage des messages de placement.")]
    public float placementFeedbackDuration = 1.2f;

    [Header("Building Placement")]
    [Tooltip("Utilise les ressources des coffres maison pour les buildings.")]
    public bool placementUseHomeResources = true;
    [Tooltip("Message si les ressources sont insuffisantes pour un building.")]
    public string placementMissingResourcesMessage = "Ressources insuffisantes.";

    [Header("Drop")]
    [Tooltip("Autorise le drop si le prefab est manquant.")]
    public bool allowDropWithoutWorldPrefab = true;
    [Tooltip("Offset vers l'avant lors du drop.")]
    public float dropForwardOffset = 0.6f;
    [Tooltip("Offset vertical lors du drop.")]
    public float dropHeightOffset = 0.1f;
    [Header("Action Feedback")]
    [Tooltip("Duree d'affichage des messages d'action.")]
    public float actionFeedbackDuration = 1.2f;

    private CanvasGroup actionBoxCanvasGroup;
    private Coroutine actionBoxFadeRoutine;
    private bool actionBoxVisible;
    private readonly List<ActionBoxEntry> actionBoxEntries = new List<ActionBoxEntry>();
    private int actionBoxIndex = -1;
    private int actionBoxLastDirection;
    private float actionBoxNextMoveTime;
    private bool actionBoxCursorDirty;
    private Coroutine actionBoxFlashRoutine;
    private bool depositMode;
    private LootContainer depositContainer;
    private bool depositQuantityActive;
    private int depositQuantity;
    private int depositQuantityMax;
    private Item depositQuantityItem;
    private int depositQuantityLastDirection;
    private float depositQuantityNextMoveTime;
    private CanvasGroup depositQuantityCanvasGroup;
    private Coroutine depositQuantityFadeRoutine;
    private bool depositQuantityVisible;
    private bool placementActive;
    private Item placementItem;
    private GameObject placementInstance;
    private readonly List<PlacementRigidbodyState> placementRigidbodies = new List<PlacementRigidbodyState>();
    private Collider[] placementColliders;
    private Transform placementAnchor;
    private readonly List<PlacementRendererState> placementRenderers = new List<PlacementRendererState>();
    private MaterialPropertyBlock placementPropertyBlock;
    private bool placementLastValid;
    private bool restoreSelectionOnNextOpen;
    private Item restoreSelectedItem;
    private bool restoreActionBoxOnNextOpen;
    private int restoreActionBoxIndex = -1;
    private Coroutine placementFeedbackRoutine;
    private Maison cachedMaison;
    private static Sprite depositQuantityFallbackSprite;
    private static Texture2D depositQuantityFallbackTexture;

    private readonly List<InventorySlotUI> inventorySlots = new List<InventorySlotUI>();
    private readonly List<InventoryEntry> entries = new List<InventoryEntry>();
    private InventorySlotUI currentFocusedSlot;
    private int lastMoveDirection;
    private float nextMoveTime;
    private bool cursorDirty;

    private void Awake()
    {
        playerInputs = new PlayerInputs();
        InventoryUISettings settings = GetSettings();
        if (settings != null)
        {
            settings.InitializePanel();
        }

        InitializeActionBox();
        InitializeDepositQuantityPanel();
    }

    private void OnEnable()
    {
        if (playerInputs == null)
        {
            playerInputs = new PlayerInputs();
        }

        playerInputs.Enable();
        playerInputs.Player.Inventory.performed += OnInventoryPerformed;
        playerInputs.Player.Return.performed += OnReturnPerformed;
        playerInputs.Player.Interact.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        if (playerInputs != null)
        {
            playerInputs.Player.Inventory.performed -= OnInventoryPerformed;
            playerInputs.Player.Return.performed -= OnReturnPerformed;
            playerInputs.Player.Interact.performed -= OnInteractPerformed;
            playerInputs.Disable();
        }

        InputFocusStack.Pop(this);
        if (placementActive)
        {
            CancelPlacement(false);
        }

        CloseInventory();
    }

    private void LateUpdate()
    {
        if (!HasInputFocus())
        {
            return;
        }

        if (!inventoryOpen)
        {
            return;
        }

        UpdateCursorVisual();
        if (actionBoxVisible)
        {
            UpdateActionBoxCursor();
        }

        if (depositQuantityVisible)
        {
            PositionDepositQuantityPanel();
        }
    }

    private void Update()
    {
        // Input loop: placement > deposit selection > inventory navigation.
        if (!HasInputFocus())
        {
            return;
        }

        if (placementActive)
        {
            UpdatePlacement();
            return;
        }

        if (!inventoryOpen)
        {
            return;
        }

        if (depositQuantityActive)
        {
            HandleDepositQuantityInput();
            return;
        }

        if (!depositMode && actionBoxVisible)
        {
            HandleActionBoxNavigation();
        }
        else
        {
            HandleNavigation();
        }
    }

    private void OnInventoryPerformed(InputAction.CallbackContext context)
    {
        if (!CanReceiveInventoryInput())
        {
            return;
        }

        if (placementActive)
        {
            return;
        }

        if (inventoryOpen)
        {
            CloseInventory();
            return;
        }

        OpenInventory();
    }

    private void OnReturnPerformed(InputAction.CallbackContext context)
    {
        if (!HasInputFocus())
        {
            return;
        }

        if (depositQuantityActive)
        {
            CancelDepositQuantity();
            return;
        }

        if (placementActive)
        {
            CancelPlacement(true);
            OpenInventory();
            return;
        }

        if (!inventoryOpen)
        {
            return;
        }

        if (actionBoxVisible)
        {
            HideActionBox();
            return;
        }

        CloseInventory();
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!HasInputFocus())
        {
            return;
        }

        if (depositQuantityActive)
        {
            ConfirmDepositQuantity();
            return;
        }

        if (placementActive)
        {
            TryConfirmPlacement();
            return;
        }

        if (!inventoryOpen)
        {
            return;
        }

        if (depositMode)
        {
            TryDepositSelectedItem();
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

    private InventoryUISettings GetSettings()
    {
        InventoryUISettings settings = InventoryUISettings.Instance;

        return settings;
    }

    public bool TryOpenForLootDeposit(LootContainer container)
    {
        if (container == null)
        {
            return false;
        }

        if (placementActive)
        {
            return false;
        }

        depositContainer = container;
        depositMode = true;
        HideActionBoxImmediate();
        HideDepositQuantityPanelImmediate();
        ResetDepositQuantityState();

        if (inventoryOpen)
        {
            RebuildInventorySlots();
            return true;
        }

        OpenInventory();
        if (!inventoryOpen)
        {
            depositMode = false;
            depositContainer = null;
            return false;
        }

        return true;
    }

    public void CloseDepositInventory()
    {
        if (!depositMode)
        {
            return;
        }

        if (inventoryOpen)
        {
            CloseInventory();
        }
        else
        {
            depositMode = false;
            depositContainer = null;
        }
    }

    public bool TryOpenInventory()
    {
        if (inventoryOpen)
        {
            return true;
        }

        if (InputFocusStack.HasAnyFocus())
        {
            return false;
        }

        OpenInventory();
        return inventoryOpen;
    }

    public bool IsOpen => inventoryOpen;

    private void OpenInventory()
    {
        InventoryUISettings settings = GetSettings();
        if (settings == null || settings.inventoryPanel == null)
        {
            Debug.LogWarning("InventoryPanelController: InventoryUISettings manquant.");
            return;
        }

        settings.OpenPanel();
        inventoryOpen = true;
        InputFocusStack.Push(this);
        SetSquadInputLock(true);
        RebuildInventorySlots();
        RestorePendingSelection();
    }

    private void CloseInventory()
    {
        InventoryUISettings settings = GetSettings();
        if (settings != null)
        {
            settings.ClosePanel();
            settings.UpdateDescription(null);
            settings.HideCursor();
        }

        HideActionBoxImmediate();
        HideDepositQuantityPanelImmediate();
        inventoryOpen = false;
        if (!placementActive)
        {
            InputFocusStack.Pop(this);
        }
        SetSquadInputLock(false);
        currentFocusedSlot = null;
        lastMoveDirection = 0;
        nextMoveTime = 0f;
        cursorDirty = false;
        ResetActionBoxNavigation();
        ResetDepositQuantityState();
        LootContainer previousDeposit = depositContainer;
        bool wasDeposit = depositMode;
        depositMode = false;
        depositContainer = null;
        inventorySlots.Clear();
        entries.Clear();

        if (wasDeposit && previousDeposit != null)
        {
            previousDeposit.NotifyDepositInventoryClosed();
        }
    }

    private bool HasInputFocus()
    {
        return InputFocusStack.HasFocus(this);
    }

    private bool CanReceiveInventoryInput()
    {
        return !InputFocusStack.HasAnyFocus() || InputFocusStack.HasFocus(this);
    }

    private void RebuildInventorySlots()
    {
        currentFocusedSlot = null;
        inventorySlots.Clear();
        entries.Clear();

        InventoryUISettings settings = GetSettings();
        if (settings == null || settings.itemsParent == null)
        {
            Debug.LogWarning("InventoryPanelController: itemsParent manquant.");
            return;
        }

        Transform itemsParent = settings.itemsParent;
        GameObject itemPrefab = settings.itemPrefab;

        for (int i = itemsParent.childCount - 1; i >= 0; i--)
        {
            Destroy(itemsParent.GetChild(i).gameObject);
        }

        BuildEntries(entries);

        InventorySlotUI firstSlot = null;
        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (entry.item == null || entry.quantity <= 0)
            {
                continue;
            }

            GameObject slotObject = CreateInstance(itemPrefab, itemsParent);
            if (slotObject == null)
            {
                slotObject = CreateTextEntry(itemsParent);
            }

            if (slotObject != null)
            {
                SetEntryText(slotObject, entry.quantity.ToString());
                SetEntrySprite(slotObject, entry.item);
                InventorySlotUI slotUi = slotObject.GetComponent<InventorySlotUI>();
                if (slotUi == null)
                {
                    slotUi = slotObject.AddComponent<InventorySlotUI>();
                }
                slotUi.Initialize(this, entry.item, entry.quantity);
                inventorySlots.Add(slotUi);
                if (firstSlot == null)
                {
                    firstSlot = slotUi;
                }
            }
        }

        if (firstSlot != null)
        {
            FocusSlot(firstSlot);
        }
        else if (settings != null)
        {
            settings.UpdateDescription(null);
            settings.HideCursor();
        }
    }

    private void BuildEntries(List<InventoryEntry> target)
    {
        if (target == null)
        {
            return;
        }

        SquadCharacterController controller = GetCurrentCharacterController();
        if (controller == null)
        {
            return;
        }

        Item torchItem = controller.TorchItem;
        int torchSeconds = controller.TorchSecondsRemaining;
        if (torchItem != null && torchSeconds > 0)
        {
            target.Add(new InventoryEntry { item = torchItem, quantity = torchSeconds });
        }

        IReadOnlyList<Item> items = controller.Items;
        if (items == null)
        {
            return;
        }

        Dictionary<Item, int> counts = new Dictionary<Item, int>();
        List<Item> order = new List<Item>();
        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null)
            {
                continue;
            }

            if (torchItem != null && item == torchItem)
            {
                continue;
            }

            if (!counts.TryGetValue(item, out int count))
            {
                counts[item] = 1;
                order.Add(item);
            }
            else
            {
                counts[item] = count + 1;
            }
        }

        for (int i = 0; i < order.Count; i++)
        {
            Item item = order[i];
            if (!counts.TryGetValue(item, out int count))
            {
                continue;
            }

            target.Add(new InventoryEntry { item = item, quantity = count });
        }
    }

    public void FocusSlot(InventorySlotUI slot)
    {
        if (slot == null || slot.SlotRect == null)
        {
            return;
        }

        currentFocusedSlot = slot;
        cursorDirty = true;
        InventoryUISettings settings = GetSettings();
        if (settings != null)
        {
            settings.UpdateDescription(slot.Item);
        }
    }

    private void UpdateCursorVisual()
    {
        InventoryUISettings settings = GetSettings();
        if (settings == null)
        {
            return;
        }

        if (currentFocusedSlot == null && inventorySlots.Count > 0)
        {
            FocusSlot(inventorySlots[0]);
        }

        InventorySlotUI slot = currentFocusedSlot;
        if (slot == null || slot.SlotRect == null)
        {
            settings.HideCursor();
            if (actionBoxVisible)
            {
                HideActionBoxImmediate();
            }
            cursorDirty = false;
            return;
        }

        settings.UpdateDescription(slot.Item);

        if (cursorDirty)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform itemsRect = settings.itemsParent as RectTransform;
            if (itemsRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRect);
            }
        }

        Transform itemsParent = settings.itemsParent;
        Transform cursorParent = itemsParent != null ? itemsParent.parent : slot.SlotRect.parent;
        RectTransform cursor = settings.EnsureSlotCursor(cursorParent);
        if (cursor == null)
        {
            cursorDirty = false;
            return;
        }

        cursor.gameObject.SetActive(true);
        if (cursorParent != null)
        {
            cursor.SetParent(cursorParent, false);
        }
        cursor.SetAsLastSibling();
        cursor.pivot = new Vector2(0.5f, 0.5f);
        cursor.position = slot.SlotRect.position;
        Vector2 size = slot.SlotRect.rect.size;
        Vector2 padding = settings.cursorPadding;
        cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x + padding.x);
        cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y + padding.y);

        if (actionBoxVisible)
        {
            PositionActionBox();
        }

        cursorDirty = false;
    }

    private void HandleNavigation()
    {
        if (playerInputs == null || inventorySlots.Count == 0)
        {
            return;
        }

        InventoryUISettings settings = GetSettings();
        if (settings == null)
        {
            return;
        }

        Vector2 moveInput = playerInputs.Player.Move.ReadValue<Vector2>();
        int direction = GetMoveDirection(moveInput, settings.moveDeadzone);
        if (direction == 0)
        {
            lastMoveDirection = 0;
            nextMoveTime = 0f;
            return;
        }

        float now = Time.unscaledTime;
        if (direction != lastMoveDirection)
        {
            MoveSlot(direction, settings.wrapCursor);
            lastMoveDirection = direction;
            nextMoveTime = now + settings.initialRepeatDelay;
            return;
        }

        if (now >= nextMoveTime)
        {
            MoveSlot(direction, settings.wrapCursor);
            nextMoveTime = now + settings.repeatInterval;
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
        InventorySlotUI current = currentFocusedSlot;
        if (current == null)
        {
            return;
        }

        InventorySlotUI next = FindNeighborSlot(current, direction, wrap);
        if (next == null || next == current)
        {
            return;
        }

        FocusSlot(next);
    }

    private InventorySlotUI FindNeighborSlot(InventorySlotUI current, int direction, bool wrap)
    {
        if (current == null || current.SlotRect == null)
        {
            return null;
        }

        InventoryUISettings settings = GetSettings();
        Canvas canvas = settings != null && settings.inventoryPanel != null
            ? settings.inventoryPanel.GetComponentInParent<Canvas>()
            : null;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (cursorDirty && settings != null && settings.itemsParent != null)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform itemsRect = settings.itemsParent as RectTransform;
            if (itemsRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRect);
            }
        }

        List<SlotInfo> slotInfos = new List<SlotInfo>(inventorySlots.Count);
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            InventorySlotUI slot = inventorySlots[i];
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

    private float GetSlotScreenHeight(InventorySlotUI slot, Camera uiCamera)
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

    private SquadCharacterController GetCurrentCharacterController()
    {
        if (SquadManager.Instance == null || SquadManager.Instance.currentCharacter == null)
        {
            return null;
        }

        return SquadManager.Instance.currentCharacter.GetComponent<SquadCharacterController>();
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

    private GameObject CreateTextEntry(Transform parent)
    {
        GameObject obj = new GameObject("InventoryItem");
        if (parent != null)
        {
            obj.transform.SetParent(parent, false);
        }

        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Left;
        return obj;
    }

    private void SetEntryText(GameObject entry, string text)
    {
        if (entry == null)
        {
            return;
        }

        TextMeshProUGUI tmp = FindEntryQuantityText(entry);
        if (tmp != null)
        {
            tmp.text = text;
        }
    }

    private TextMeshProUGUI FindEntryQuantityText(GameObject entry)
    {
        TextMeshProUGUI[] texts = entry.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts == null || texts.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI tmp = texts[i];
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

    private void SetEntrySprite(GameObject entry, Item item)
    {
        if (entry == null)
        {
            return;
        }

        Image targetImage = FindEntrySpriteImage(entry);
        if (targetImage == null)
        {
            return;
        }

        Sprite sprite = null;
        if (item != null)
        {
            sprite = item.itemSprite;
        }

        targetImage.sprite = sprite;
        targetImage.enabled = sprite != null;
    }

    private Image FindEntrySpriteImage(GameObject entry)
    {
        Image[] images = entry.GetComponentsInChildren<Image>(true);
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

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            string name = image.name;
            if (name.IndexOf("frame", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("font", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("background", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("bg", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            return image;
        }

        return images[0];
    }

    private void InitializeActionBox()
    {
        if (actionBox == null)
        {
            Transform found = transform.Find("ActionBox");
            if (found != null)
            {
                actionBox = found.gameObject;
            }
        }

        if (actionBox == null)
        {
            return;
        }

        actionBoxCanvasGroup = GetActionBoxCanvasGroup();
        if (actionBoxCanvasGroup != null && actionBoxSetAlphaToZeroOnStart)
        {
            actionBoxCanvasGroup.alpha = 0f;
            if (actionBoxDisableRaycastsWhenHidden)
            {
                actionBoxCanvasGroup.interactable = false;
                actionBoxCanvasGroup.blocksRaycasts = false;
            }
        }

        BuildActionBoxEntries();
        actionBoxVisible = false;
        ResetActionBoxNavigation();
    }

    private void InitializeDepositQuantityPanel()
    {
        if (depositQuantityPanel == null)
        {
            Transform found = transform.Find("DepositQuantityPanel");
            if (found != null)
            {
                depositQuantityPanel = found.gameObject;
            }
        }

        if (depositQuantityPanel == null && depositQuantityCreateIfMissing)
        {
            depositQuantityPanel = CreateDepositQuantityPanel();
        }

        if (depositQuantityPanel == null)
        {
            return;
        }

        if (depositQuantityText == null)
        {
            depositQuantityText = depositQuantityPanel.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        depositQuantityCanvasGroup = GetDepositQuantityCanvasGroup();
        if (depositQuantityCanvasGroup != null && depositQuantitySetAlphaToZeroOnStart)
        {
            depositQuantityCanvasGroup.alpha = 0f;
            if (depositQuantityDisableRaycastsWhenHidden)
            {
                depositQuantityCanvasGroup.interactable = false;
                depositQuantityCanvasGroup.blocksRaycasts = false;
            }
        }

        depositQuantityVisible = false;
        ResetDepositQuantityState();
    }

    private void ShowActionBox()
    {
        if (actionBox == null)
        {
            return;
        }

        if (depositMode)
        {
            return;
        }

        if (currentFocusedSlot == null && inventorySlots.Count > 0)
        {
            FocusSlot(inventorySlots[0]);
        }

        if (currentFocusedSlot == null)
        {
            return;
        }

        EnsureActionBoxEntries();
        if (actionBoxEntries.Count == 0)
        {
            return;
        }

        actionBoxVisible = true;
        ResetInventoryNavigation();
        actionBox.SetActive(true);
        actionBox.transform.SetAsLastSibling();
        SelectActionBoxIndex(0, true);
        PositionActionBox();
        actionBoxCursorDirty = true;
        ShowActionBoxCursor();
        FadeActionBoxTo(1f, actionBoxFadeDuration);
    }

    private void HideActionBox()
    {
        if (actionBox == null)
        {
            return;
        }

        actionBoxVisible = false;
        ResetActionBoxNavigation();
        ResetInventoryNavigation();
        StopActionBoxFlash();
        HideActionBoxCursor();
        FadeActionBoxTo(0f, actionBoxFadeDuration);
    }

    private void HideActionBoxImmediate()
    {
        if (actionBox == null)
        {
            return;
        }

        actionBoxVisible = false;
        ResetActionBoxNavigation();
        ResetInventoryNavigation();
        StopActionBoxFlash();
        if (actionBoxFadeRoutine != null)
        {
            StopCoroutine(actionBoxFadeRoutine);
            actionBoxFadeRoutine = null;
        }

        actionBoxCanvasGroup = GetActionBoxCanvasGroup();
        if (actionBoxCanvasGroup != null)
        {
            actionBoxCanvasGroup.alpha = 0f;
            if (actionBoxDisableRaycastsWhenHidden)
            {
                actionBoxCanvasGroup.interactable = false;
                actionBoxCanvasGroup.blocksRaycasts = false;
            }
        }

        HideActionBoxCursor();
    }

    private void ShowDepositQuantityPanel()
    {
        if (depositQuantityPanel == null)
        {
            return;
        }

        depositQuantityVisible = true;
        depositQuantityPanel.SetActive(true);
        PositionDepositQuantityPanel();
        FadeDepositQuantityTo(1f, depositQuantityFadeDuration);
    }

    private void HideDepositQuantityPanel()
    {
        if (depositQuantityPanel == null)
        {
            return;
        }

        depositQuantityVisible = false;
        FadeDepositQuantityTo(0f, depositQuantityFadeDuration);
    }

    private void HideDepositQuantityPanelImmediate()
    {
        if (depositQuantityPanel == null)
        {
            return;
        }

        depositQuantityVisible = false;
        if (depositQuantityFadeRoutine != null)
        {
            StopCoroutine(depositQuantityFadeRoutine);
            depositQuantityFadeRoutine = null;
        }

        depositQuantityCanvasGroup = GetDepositQuantityCanvasGroup();
        if (depositQuantityCanvasGroup != null)
        {
            depositQuantityCanvasGroup.alpha = 0f;
            if (depositQuantityDisableRaycastsWhenHidden)
            {
                depositQuantityCanvasGroup.interactable = false;
                depositQuantityCanvasGroup.blocksRaycasts = false;
            }
        }
    }

    private void PositionDepositQuantityPanel()
    {
        if (depositQuantityPanel == null)
        {
            return;
        }

        RectTransform panelRect = depositQuantityPanel.GetComponent<RectTransform>();
        if (panelRect == null)
        {
            return;
        }

        RectTransform slotRect = currentFocusedSlot != null ? currentFocusedSlot.SlotRect : null;
        Transform parent = panelRect.parent;
        RectTransform parentRect = parent as RectTransform;

        if (slotRect == null || parentRect == null)
        {
            panelRect.anchoredPosition = depositQuantityPanelOffset;
            return;
        }

        Canvas canvas = depositQuantityPanel.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, slotRect.position);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 localPoint))
        {
            panelRect.anchoredPosition = localPoint + depositQuantityPanelOffset;
            return;
        }

        panelRect.position = slotRect.position + (Vector3)depositQuantityPanelOffset;
    }

    private void PositionActionBox()
    {
        if (actionBox == null || currentFocusedSlot == null || currentFocusedSlot.SlotRect == null)
        {
            return;
        }

        RectTransform actionRect = actionBox.GetComponent<RectTransform>();
        RectTransform slotRect = currentFocusedSlot.SlotRect;
        Transform parent = actionRect != null ? actionRect.parent : actionBox.transform.parent;
        RectTransform parentRect = parent as RectTransform;

        Canvas canvas = actionBox.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, slotRect.position);
        if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 localPoint))
        {
            Vector2 anchored = localPoint + actionBoxOffset;
            if (actionRect != null)
            {
                actionRect.anchoredPosition = anchored;
            }
            else
            {
                actionBox.transform.localPosition = anchored;
            }
            return;
        }

        Vector3 fallbackWorld = slotRect.position + (Vector3)actionBoxOffset;
        actionBox.transform.position = fallbackWorld;
    }

    private bool TryUseSelectedItem()
    {
        if (currentFocusedSlot == null || currentFocusedSlot.Item == null)
        {
            return false;
        }

        SquadCharacterController controller = GetCurrentCharacterController();
        if (controller == null)
        {
            return false;
        }

        if (controller.TryUseItem(currentFocusedSlot.Item, out string reason))
        {
            return true;
        }

        ShowActionFeedback(reason);
        return false;
    }

    private void TryDepositSelectedItem()
    {
        if (!depositMode || depositContainer == null)
        {
            return;
        }

        if (currentFocusedSlot == null || currentFocusedSlot.Item == null)
        {
            return;
        }

        int available = Mathf.Max(0, currentFocusedSlot.Quantity);
        if (available <= 0)
        {
            return;
        }

        int remainingCapacity = depositContainer.GetRemainingCapacity();
        if (remainingCapacity <= 0)
        {
            FlashActionBoxInvalid();
            return;
        }

        if (remainingCapacity != int.MaxValue)
        {
            available = Mathf.Min(available, remainingCapacity);
        }

        if (available > 1)
        {
            BeginDepositQuantity(currentFocusedSlot.Item, available);
            return;
        }

        PerformDeposit(currentFocusedSlot.Item, 1);
    }

    private void BeginDepositQuantity(Item item, int maxQuantity)
    {
        if (item == null)
        {
            return;
        }

        depositQuantityItem = item;
        depositQuantityMax = Mathf.Max(1, maxQuantity);
        depositQuantity = 1;
        depositQuantityActive = true;
        depositQuantityLastDirection = 0;
        depositQuantityNextMoveTime = 0f;
        UpdateDepositQuantityText();
        ShowDepositQuantityPanel();
    }

    private void CancelDepositQuantity()
    {
        if (!depositQuantityActive)
        {
            return;
        }

        depositQuantityActive = false;
        HideDepositQuantityPanel();
        ResetDepositQuantityState();
    }

    private void ConfirmDepositQuantity()
    {
        if (!depositQuantityActive)
        {
            return;
        }

        int quantity = Mathf.Clamp(depositQuantity, 1, depositQuantityMax);
        Item item = depositQuantityItem;
        depositQuantityActive = false;
        HideDepositQuantityPanel();
        ResetDepositQuantityState();
        PerformDeposit(item, quantity);
    }

    private bool PerformDeposit(Item item, int quantity)
    {
        if (!depositMode || depositContainer == null)
        {
            return false;
        }

        if (item == null || quantity <= 0)
        {
            return false;
        }

        if (!depositContainer.TryDepositItem(item, quantity))
        {
            return false;
        }

        Item previousItem = item;
        RebuildInventorySlots();
        InventorySlotUI slot = FindSlotByItem(previousItem);
        if (slot != null)
        {
            FocusSlot(slot);
        }
        else if (inventorySlots.Count > 0)
        {
            FocusSlot(inventorySlots[0]);
        }

        return true;
    }

    private void HandleDepositQuantityInput()
    {
        if (playerInputs == null || !depositQuantityActive)
        {
            return;
        }

        InventoryUISettings settings = GetSettings();
        if (settings == null)
        {
            return;
        }

        Vector2 moveInput = playerInputs.Player.Move.ReadValue<Vector2>();
        int direction = GetMoveDirection(moveInput, settings.moveDeadzone);
        if (direction != -1 && direction != 1)
        {
            depositQuantityLastDirection = 0;
            depositQuantityNextMoveTime = 0f;
            return;
        }

        float now = Time.unscaledTime;
        if (direction != depositQuantityLastDirection)
        {
            AdjustDepositQuantity(direction);
            depositQuantityLastDirection = direction;
            depositQuantityNextMoveTime = now + settings.initialRepeatDelay;
            return;
        }

        if (now >= depositQuantityNextMoveTime)
        {
            AdjustDepositQuantity(direction);
            depositQuantityNextMoveTime = now + settings.repeatInterval;
        }
    }

    private void AdjustDepositQuantity(int direction)
    {
        if (direction == -1)
        {
            depositQuantity = Mathf.Min(depositQuantity + 1, depositQuantityMax);
        }
        else if (direction == 1)
        {
            depositQuantity = Mathf.Max(1, depositQuantity - 1);
        }

        UpdateDepositQuantityText();
    }

    private void UpdateDepositQuantityText()
    {
        if (depositQuantityText == null)
        {
            return;
        }

        int current = Mathf.Clamp(depositQuantity, 1, depositQuantityMax);
        int max = Mathf.Max(1, depositQuantityMax);
        string itemName = GetItemDisplayName(depositQuantityItem);
        string text = $"{current}/{max}";
        if (!string.IsNullOrWhiteSpace(depositQuantityFormat) && depositQuantityFormat.Contains("{0"))
        {
            text = string.Format(depositQuantityFormat, current, max);
        }

        if (!string.IsNullOrEmpty(itemName))
        {
            text = $"{itemName}\n{text}";
        }

        depositQuantityText.text = text;
        depositQuantityText.gameObject.SetActive(true);
    }

    private string GetItemDisplayName(Item item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(item.itemName))
        {
            return item.itemName;
        }

        return item.name;
    }

    private void ResetDepositQuantityState()
    {
        depositQuantityActive = false;
        depositQuantity = 1;
        depositQuantityMax = 1;
        depositQuantityItem = null;
        depositQuantityLastDirection = 0;
        depositQuantityNextMoveTime = 0f;
    }

    private void HandleActionBoxSelection()
    {
        ActionBoxEntry entry = GetCurrentActionBoxEntry();
        if (entry == null)
        {
            return;
        }

        string name = entry.Name;
        if (name.IndexOf("utiliser", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("use", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (TryUseSelectedItem())
            {
                RebuildInventorySlots();
            }

            return;
        }

        if (name.IndexOf("poser", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("drop", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            TryStartPlacementFromSelectedItem();
            return;
        }

        if (name.IndexOf("casser", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("break", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (TryBreakSelectedItem())
            {
                RebuildInventorySlots();
            }
            else
            {
                FlashActionBoxInvalid();
            }

            return;
        }

        if (name.IndexOf("fermer", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("close", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return;
        }
    }

    private void HandleActionBoxNavigation()
    {
        if (playerInputs == null || actionBoxEntries.Count == 0)
        {
            return;
        }

        InventoryUISettings settings = GetSettings();
        if (settings == null)
        {
            return;
        }

        Vector2 moveInput = playerInputs.Player.Move.ReadValue<Vector2>();
        int direction = GetActionBoxMoveDirection(moveInput, settings.moveDeadzone);
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
            actionBoxNextMoveTime = now + settings.initialRepeatDelay;
            return;
        }

        if (now >= actionBoxNextMoveTime)
        {
            MoveActionBox(direction, actionBoxWrap);
            actionBoxNextMoveTime = now + settings.repeatInterval;
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
            SelectActionBoxIndex(0, true);
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

        EnsureBreakActionBoxEntry(container);

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child == null)
            {
                continue;
            }

            string name = child.name;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (name.IndexOf("ActionBox_", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (name.IndexOf("_Frame", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("_Text", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            RectTransform rect = child as RectTransform;
            Image frame = FindActionBoxFrame(child);
            TextMeshProUGUI label = FindActionBoxLabel(child);
            ActionBoxEntry entry = new ActionBoxEntry(rect, frame, label, name);
            actionBoxEntries.Add(entry);
        }

        if (actionBoxEntries.Count == 0)
        {
            return;
        }
    }

    private void EnsureActionBoxEntries()
    {
        if (actionBoxEntries.Count == 0)
        {
            BuildActionBoxEntries();
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

        TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (labels == null || labels.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < labels.Length; i++)
        {
            TextMeshProUGUI label = labels[i];
            if (label == null)
            {
                continue;
            }

            if (label.name.IndexOf("text", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return label;
            }
        }

        return labels[0];
    }

    private bool TryBreakSelectedItem()
    {
        if (currentFocusedSlot == null || currentFocusedSlot.Item == null)
        {
            return false;
        }

        Item item = currentFocusedSlot.Item;
        SquadCharacterController controller = GetCurrentCharacterController();
        if (controller == null)
        {
            return false;
        }

        if (item.TryBreak(controller, out string reason))
        {
            return true;
        }

        ShowActionFeedback(reason);
        return false;
    }

    private void ShowActionFeedback(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        InfoBoxUI.TryShow(message, actionFeedbackDuration);
    }

    private void EnsureBreakActionBoxEntry(Transform container)
    {
        if (container == null)
        {
            return;
        }

        Transform existing = FindActionBoxEntry(container, "ActionBox_Casser");
        if (existing != null)
        {
            EnsureActionBoxLabelText(existing, "Casser");
            return;
        }

        Transform template = FindFirstActionBoxEntry(container);
        if (template == null)
        {
            return;
        }

        GameObject clone = Instantiate(template.gameObject, container);
        clone.name = "ActionBox_Casser";
        RenameActionBoxChildren(clone.transform, "ActionBox_Casser");
        EnsureActionBoxLabelText(clone.transform, "Casser");
    }

    private Transform FindActionBoxEntry(Transform container, string name)
    {
        if (container == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private Transform FindFirstActionBoxEntry(Transform container)
    {
        if (container == null)
        {
            return null;
        }

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child == null)
            {
                continue;
            }

            string name = child.name;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (name.IndexOf("ActionBox_", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (name.IndexOf("_Frame", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("_Text", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Cursor", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            return child;
        }

        return null;
    }

    private void EnsureActionBoxLabelText(Transform entry, string labelText)
    {
        if (entry == null)
        {
            return;
        }

        TextMeshProUGUI label = FindActionBoxLabel(entry);
        if (label == null)
        {
            return;
        }

        label.text = labelText;
    }

    private void RenameActionBoxChildren(Transform entry, string baseName)
    {
        if (entry == null || string.IsNullOrWhiteSpace(baseName))
        {
            return;
        }

        for (int i = 0; i < entry.childCount; i++)
        {
            Transform child = entry.GetChild(i);
            if (child == null)
            {
                continue;
            }

            string name = child.name;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (name.IndexOf("frame", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                child.name = $"{baseName}_Frame";
            }
            else if (name.IndexOf("text", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                child.name = $"{baseName}_Text";
            }
        }
    }

    private ActionBoxEntry GetCurrentActionBoxEntry()
    {
        if (actionBoxEntries.Count == 0)
        {
            return null;
        }

        if (actionBoxIndex < 0 || actionBoxIndex >= actionBoxEntries.Count)
        {
            return actionBoxEntries[0];
        }

        return actionBoxEntries[actionBoxIndex];
    }

    private void SelectActionBoxIndex(int index, bool force)
    {
        if (actionBoxEntries.Count == 0)
        {
            actionBoxIndex = -1;
            return;
        }

        int clampedIndex = Mathf.Clamp(index, 0, actionBoxEntries.Count - 1);
        if (!force && clampedIndex == actionBoxIndex)
        {
            return;
        }

        actionBoxIndex = clampedIndex;
        ApplyActionBoxVisuals();

        actionBoxCursorDirty = true;
    }

    private void ApplyActionBoxVisuals()
    {
        for (int i = 0; i < actionBoxEntries.Count; i++)
        {
            ActionBoxEntry entry = actionBoxEntries[i];
            bool selected = i == actionBoxIndex;
            if (entry.Frame != null)
            {
                Color color = entry.FrameBaseColor;
                color.a *= selected ? actionBoxSelectedFrameAlpha : actionBoxUnselectedFrameAlpha;
                entry.Frame.color = color;
            }

            if (entry.Label != null)
            {
                Color color = entry.LabelBaseColor;
                color.a *= selected ? actionBoxSelectedTextAlpha : actionBoxUnselectedTextAlpha;
                entry.Label.color = color;
            }
        }
    }

    private void FlashActionBoxInvalid()
    {
        if (!actionBoxVisible || actionBoxEntries.Count == 0)
        {
            return;
        }

        if (actionBoxFlashRoutine != null)
        {
            StopCoroutine(actionBoxFlashRoutine);
        }

        actionBoxFlashRoutine = StartCoroutine(ActionBoxFlashRoutine());
    }

    private IEnumerator ActionBoxFlashRoutine()
    {
        int flashes = Mathf.Max(1, actionBoxInvalidFlashCount);
        float duration = Mathf.Max(0.05f, actionBoxInvalidFlashDuration);

        for (int i = 0; i < flashes; i++)
        {
            SetActionBoxFlashColor(actionBoxInvalidFlashColor);
            yield return new WaitForSecondsRealtime(duration);
            ApplyActionBoxVisuals();
            yield return new WaitForSecondsRealtime(duration);
        }

        ApplyActionBoxVisuals();
        actionBoxFlashRoutine = null;
    }

    private void StopActionBoxFlash()
    {
        if (actionBoxFlashRoutine != null)
        {
            StopCoroutine(actionBoxFlashRoutine);
            actionBoxFlashRoutine = null;
        }

        ApplyActionBoxVisuals();
    }

    private void SetActionBoxFlashColor(Color color)
    {
        for (int i = 0; i < actionBoxEntries.Count; i++)
        {
            ActionBoxEntry entry = actionBoxEntries[i];
            if (entry.Frame != null)
            {
                entry.Frame.color = color;
            }

            if (entry.Label != null)
            {
                entry.Label.color = color;
            }
        }
    }

    private void ResetActionBoxNavigation()
    {
        actionBoxLastDirection = 0;
        actionBoxNextMoveTime = 0f;
        actionBoxIndex = -1;
        actionBoxCursorDirty = true;
    }

    private void ResetInventoryNavigation()
    {
        lastMoveDirection = 0;
        nextMoveTime = 0f;
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

        Vector2 moveInput = playerInputs != null ? playerInputs.Player.Move.ReadValue<Vector2>() : Vector2.zero;
        Vector3 moveDir = GetPlacementMoveDirection(moveInput);
        Vector3 position = placementInstance.transform.position;
        if (moveDir.sqrMagnitude > 0f)
        {
            position += moveDir * placementMoveSpeed * Time.unscaledDeltaTime;
        }

        Vector3 anchorPos = placementAnchor.position;
        Vector3 offset = position - anchorPos;
        offset.y = 0f;
        float radius = Mathf.Max(0f, placementRadius);
        if (offset.magnitude > radius)
        {
            offset = offset.normalized * radius;
        }

        position = new Vector3(anchorPos.x + offset.x, position.y, anchorPos.z + offset.z);
        position = SnapPlacementToGround(position);
        placementInstance.transform.position = position;

        bool valid = IsPlacementValid();
        UpdatePlacementVisuals(valid);
    }

    private Vector3 GetPlacementMoveDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        if (placementUseCameraRelative)
        {
            Camera cam = placementCamera != null ? placementCamera : Camera.main;
            if (cam != null)
            {
                forward = cam.transform.forward;
                right = cam.transform.right;
            }
        }

        forward.y = 0f;
        right.y = 0f;
        forward = forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
        right = right.sqrMagnitude > 0f ? right.normalized : Vector3.right;

        Vector3 move = forward * input.y + right * input.x;
        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        return move;
    }

    private Vector3 SnapPlacementToGround(Vector3 position)
    {
        if (!placementSnapToGround)
        {
            return position;
        }

        float height = Mathf.Max(0f, placementGroundRaycastHeight);
        float distance = Mathf.Max(0f, placementGroundRaycastDistance);
        Vector3 origin = position + Vector3.up * height;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, height + distance, placementGroundMask, QueryTriggerInteraction.Ignore))
        {
            position.y = hit.point.y + placementGroundOffset;
        }

        return position;
    }

    private bool TryStartPlacementFromSelectedItem()
    {
        if (placementActive)
        {
            return false;
        }

        if (currentFocusedSlot == null || currentFocusedSlot.Item == null)
        {
            return false;
        }

        Item item = currentFocusedSlot.Item;
        SquadCharacterController controller = GetCurrentCharacterController();
        if (allowDropWithoutWorldPrefab
            && item != null
            && item.ShouldInstantDropInsteadOfPlacement(controller, allowDropWithoutWorldPrefab))
        {
            if (TryInstantDropItem(item, 1))
            {
                RebuildInventorySlots();
                InventorySlotUI slot = FindSlotByItem(item);
                if (slot != null)
                {
                    FocusSlot(slot);
                }
                return true;
            }
        }

        if (!TryBeginPlacement(item))
        {
            FlashActionBoxInvalid();
            return false;
        }

        restoreSelectionOnNextOpen = true;
        restoreSelectedItem = item;
        restoreActionBoxOnNextOpen = actionBoxVisible;
        restoreActionBoxIndex = actionBoxIndex;

        CloseInventory();
        InputFocusStack.Push(this);
        SetSquadInputLock(true);
        return true;
    }

    private bool TryBeginPlacement(Item item)
    {
        if (item == null)
        {
            return false;
        }

        SquadCharacterController controller = GetCurrentCharacterController();
        if (!item.CanPlaceFromInventory(controller, out string reason))
        {
            Debug.LogWarning($"InventoryPanelController: l'item {item.name} ne peut pas etre place ou n'a pas de prefab.");
            ShowPlacementFeedback(string.IsNullOrWhiteSpace(reason) ? placementCannotPlaceMessage : reason);
            FlashActionBoxInvalid();
            return false;
        }

        if (placementActive)
        {
            return false;
        }

        if (controller == null)
        {
            return false;
        }

        if (item.isBuilding)
        {
            if (!HasBuildingResources(item, controller, out string resourceReason))
            {
                ShowPlacementFeedback(resourceReason);
                FlashActionBoxInvalid();
                return false;
            }
        }

        placementAnchor = controller.transform;
        GameObject prefab = ResolvePlacementPrefab(item);
        placementInstance = prefab != null ? Instantiate(prefab) : null;
        if (placementInstance == null)
        {
            return false;
        }

        placementItem = item;
        placementActive = true;
        CachePlacementPhysics(placementInstance);

        Vector3 startPos = placementAnchor.position;
        Vector3 forward = placementAnchor.forward;
        if (placementUseCameraRelative)
        {
            Camera cam = placementCamera != null ? placementCamera : Camera.main;
            if (cam != null)
            {
                forward = cam.transform.forward;
            }
        }

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = placementAnchor.forward;
            forward.y = 0f;
        }

        forward = forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
        startPos += forward * Mathf.Max(0f, placementStartDistance);
        Vector3 startOffset = startPos - placementAnchor.position;
        startOffset.y = 0f;
        float radius = Mathf.Max(0f, placementRadius);
        if (startOffset.magnitude > radius)
        {
            startOffset = startOffset.normalized * radius;
            startPos = new Vector3(placementAnchor.position.x + startOffset.x, startPos.y, placementAnchor.position.z + startOffset.z);
        }
        startPos = SnapPlacementToGround(startPos);
        placementInstance.transform.position = startPos;
        CachePlacementVisuals(placementInstance);
        UpdatePlacementVisuals(IsPlacementValid());
        return true;
    }

    private GameObject ResolvePlacementPrefab(Item item)
    {
        return item != null ? item.ResolveWorldPrefab() : null;
    }

    private bool HasBuildingResources(Item building, SquadCharacterController controller, out string reason)
    {
        reason = placementMissingResourcesMessage;
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
        List<LootContainer> homeContainers = ResolveHomeContainers();

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
        reason = placementMissingResourcesMessage;
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
        List<LootContainer> homeContainers = ResolveHomeContainers();

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

    private List<LootContainer> ResolveHomeContainers()
    {
        if (!placementUseHomeResources)
        {
            return null;
        }

        Maison maison = GetMaison();
        if (maison == null)
        {
            return null;
        }

        List<LootContainer> containers = maison.ResolveMaisonLootContainers(null);
        return containers != null && containers.Count > 0 ? containers : null;
    }

    private int GetHomeItemCount(Item item, List<LootContainer> containers)
    {
        if (item == null || containers == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < containers.Count; i++)
        {
            LootContainer container = containers[i];
            if (container == null)
            {
                continue;
            }

            total += container.GetItemCount(item);
        }

        return total;
    }

    private int RemoveFromHomeContainers(Item item, int quantity, List<LootContainer> containers)
    {
        if (item == null || quantity <= 0 || containers == null)
        {
            return 0;
        }

        int remaining = quantity;
        for (int i = 0; i < containers.Count && remaining > 0; i++)
        {
            LootContainer container = containers[i];
            if (container == null)
            {
                continue;
            }

            int removed = container.RemoveItems(item, remaining);
            remaining -= removed;
        }

        return quantity - remaining;
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

    private bool TryInstantDropItem(Item item, int quantity)
    {
        if (item == null || quantity <= 0)
        {
            return false;
        }

        SquadCharacterController controller = GetCurrentCharacterController();
        if (controller == null)
        {
            return false;
        }

        if (!item.CanInstantDropFromInventory(controller, allowDropWithoutWorldPrefab, out string reason))
        {
            ShowPlacementFeedback(reason);
            return false;
        }

        Vector3 position = controller.transform.position;
        Vector3 forward = controller.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward = forward.normalized;
        position += forward * Mathf.Max(0f, dropForwardOffset);
        position += Vector3.up * dropHeightOffset;

        GameObject instance = item.CreateWorldInstance(position, Quaternion.identity);

        if (instance == null)
        {
            return false;
        }

        if (!controller.TryRemoveItemQuantity(item, quantity))
        {
            Destroy(instance);
            return false;
        }

        CreateDroppedLootContainer(instance, item, quantity);
        return true;
    }

    private void CachePlacementPhysics(GameObject instance)
    {
        placementRigidbodies.Clear();
        if (instance == null)
        {
            placementColliders = null;
            return;
        }

        Rigidbody[] bodies = instance.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body == null)
            {
                continue;
            }

            placementRigidbodies.Add(new PlacementRigidbodyState(body, body.isKinematic, body.useGravity));
            body.isKinematic = true;
            body.useGravity = false;
        }

        placementColliders = instance.GetComponentsInChildren<Collider>(true);
    }

    private void RestorePlacementPhysics()
    {
        for (int i = 0; i < placementRigidbodies.Count; i++)
        {
            PlacementRigidbodyState state = placementRigidbodies[i];
            if (state.Body == null)
            {
                continue;
            }

            state.Body.isKinematic = state.WasKinematic;
            state.Body.useGravity = state.UsedGravity;
        }

        placementRigidbodies.Clear();
        placementColliders = null;
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
            if (!string.IsNullOrWhiteSpace(placementInvalidMessage))
            {
                ShowPlacementFeedback(placementInvalidMessage);
            }
            return;
        }

        SquadCharacterController controller = GetCurrentCharacterController();
        if (controller == null)
        {
            CancelPlacement(false);
            return;
        }

        if (placementItem.isBuilding)
        {
            if (!TryConsumeBuildingResources(placementItem, controller, out string resourceReason))
            {
                ShowPlacementFeedback(resourceReason);
                CancelPlacement(false);
                return;
            }
        }

        if (!controller.TryRemoveItem(placementItem, 1))
        {
            Debug.LogWarning("InventoryPanelController: impossible de retirer l'item de l'inventaire.");
            CancelPlacement(false);
            return;
        }

        RestorePlacementPhysics();
        ClearPlacementVisuals();
        if (placementItem.isBuilding)
        {
            ConfigurePlacedBuilding(placementInstance, placementItem);
        }
        else if (placementCreateLootContainer)
        {
            CreateDroppedLootContainer(placementInstance, placementItem);
        }
        ClearPlacementRestore();
        placementActive = false;
        placementItem = null;
        placementInstance = null;
        placementAnchor = null;
        SetSquadInputLock(false);
        ReleasePlacementFocus();
    }

    private void ConfigurePlacedBuilding(GameObject instance, Item building)
    {
        if (instance == null || building == null || !building.isBuilding)
        {
            return;
        }

        BuildingInfoInteractable info = instance.GetComponent<BuildingInfoInteractable>();
        if (info == null)
        {
            info = instance.AddComponent<BuildingInfoInteractable>();
        }

        string id = GetBuildingItemId(building);
        info.Initialize(id, building, 1);

        BuilderController builder = GetBuilderController();
        if (builder != null)
        {
            builder.RegisterBuiltBuilding(building, 1, info);
            builder.ApplyBuildingEffects(building, 1);
        }

        LootContainer container = instance.GetComponentInChildren<LootContainer>();
        if (container != null)
        {
            container.containerItem = building;
        }

        if (building.isHomeChest)
        {
            TryAssignMaisonChestTag(instance);
            if (container != null)
            {
                EnsureHomeChestDefaults(container);
            }
        }
    }

    private BuilderController GetBuilderController()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<BuilderController>();
#else
        return FindObjectOfType<BuilderController>();
#endif
    }

    private string GetBuildingItemId(Item item)
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

    private void EnsureHomeChestDefaults(LootContainer container)
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
            container.collectable = false;
        }
    }

    private void CancelPlacement(bool preserveRestore)
    {
        RestorePlacementPhysics();
        ClearPlacementVisuals();
        if (placementInstance != null)
        {
            Destroy(placementInstance);
        }

        placementActive = false;
        placementItem = null;
        placementInstance = null;
        placementAnchor = null;
        if (!preserveRestore)
        {
            ClearPlacementRestore();
        }
        SetSquadInputLock(false);
        ReleasePlacementFocus();
    }

    private void ReleasePlacementFocus()
    {
        if (!inventoryOpen)
        {
            InputFocusStack.Pop(this);
        }
    }

    private void ClearPlacementRestore()
    {
        restoreSelectionOnNextOpen = false;
        restoreSelectedItem = null;
        restoreActionBoxOnNextOpen = false;
        restoreActionBoxIndex = -1;
    }

    private void RestorePendingSelection()
    {
        if (!restoreSelectionOnNextOpen)
        {
            return;
        }

        Item item = restoreSelectedItem;
        bool reopenActionBox = restoreActionBoxOnNextOpen;
        int actionIndex = restoreActionBoxIndex;
        ClearPlacementRestore();

        if (item != null)
        {
            InventorySlotUI target = FindSlotByItem(item);
            if (target != null)
            {
                FocusSlot(target);
            }
        }

        if (currentFocusedSlot == null && inventorySlots.Count > 0)
        {
            FocusSlot(inventorySlots[0]);
        }

        if (reopenActionBox)
        {
            ShowActionBox();
            if (actionIndex >= 0 && actionBoxEntries.Count > 0)
            {
                int clampedIndex = Mathf.Clamp(actionIndex, 0, actionBoxEntries.Count - 1);
                SelectActionBoxIndex(clampedIndex, true);
            }
        }
    }

    private InventorySlotUI FindSlotByItem(Item item)
    {
        if (item == null)
        {
            return null;
        }

        for (int i = 0; i < inventorySlots.Count; i++)
        {
            InventorySlotUI slot = inventorySlots[i];
            if (slot != null && slot.Item == item)
            {
                return slot;
            }
        }

        return null;
    }

    private void ShowPlacementFeedback(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (InfoBoxUI.TryShow(message, placementFeedbackDuration))
        {
            return;
        }

        InventoryUISettings settings = GetSettings();
        if (settings == null || settings.descriptionText == null)
        {
            Debug.LogWarning(message);
            return;
        }

        if (placementFeedbackRoutine != null)
        {
            StopCoroutine(placementFeedbackRoutine);
        }

        placementFeedbackRoutine = StartCoroutine(PlacementFeedbackRoutine(settings, message, placementFeedbackDuration));
    }

    private IEnumerator PlacementFeedbackRoutine(InventoryUISettings settings, string message, float duration)
    {
        if (settings == null || settings.descriptionText == null)
        {
            yield break;
        }

        TextMeshProUGUI description = settings.descriptionText;
        string previousText = description.text;
        bool previousActive = description.gameObject.activeSelf;
        description.text = message;
        description.gameObject.SetActive(true);

        float time = 0f;
        float wait = Mathf.Max(0f, duration);
        while (time < wait)
        {
            time += Time.unscaledDeltaTime;
            yield return null;
        }

        if (inventoryOpen && currentFocusedSlot != null)
        {
            settings.UpdateDescription(currentFocusedSlot.Item);
        }
        else
        {
            description.text = previousText;
            description.gameObject.SetActive(previousActive);
        }

        placementFeedbackRoutine = null;
    }

    private bool IsPlacementValid()
    {
        if (placementInstance == null)
        {
            return false;
        }

        if (placementColliders == null || placementColliders.Length == 0)
        {
            return true;
        }

        Bounds bounds = placementColliders[0].bounds;
        for (int i = 1; i < placementColliders.Length; i++)
        {
            Collider col = placementColliders[i];
            if (col == null)
            {
                continue;
            }

            bounds.Encapsulate(col.bounds);
        }

        Vector3 extents = bounds.extents + Vector3.one * Mathf.Max(0f, placementBoundsPadding);
        QueryTriggerInteraction triggerInteraction = placementBlockTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        Collider[] overlaps = Physics.OverlapBox(bounds.center, extents, Quaternion.identity, placementCollisionMask, triggerInteraction);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider hit = overlaps[i];
            if (hit == null)
            {
                continue;
            }

            if (hit.transform.IsChildOf(placementInstance.transform))
            {
                continue;
            }

            if ((placementIgnoreMask.value & (1 << hit.gameObject.layer)) != 0)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void CachePlacementVisuals(GameObject instance)
    {
        placementRenderers.Clear();
        placementPropertyBlock = null;
        placementLastValid = false;

        if (instance == null || !placementShowValidity)
        {
            return;
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMaterial == null)
            {
                continue;
            }

            string property = null;
            if (renderer.sharedMaterial.HasProperty("_BaseColor"))
            {
                property = "_BaseColor";
            }
            else if (renderer.sharedMaterial.HasProperty("_Color"))
            {
                property = "_Color";
            }

            if (string.IsNullOrEmpty(property))
            {
                continue;
            }

            placementRenderers.Add(new PlacementRendererState(renderer, property));
        }

        if (placementRenderers.Count > 0)
        {
            placementPropertyBlock = new MaterialPropertyBlock();
        }
    }

    private void UpdatePlacementVisuals(bool isValid)
    {
        if (!placementShowValidity || placementRenderers.Count == 0)
        {
            return;
        }

        if (placementLastValid == isValid && placementPropertyBlock != null)
        {
            return;
        }

        Color color = isValid ? placementValidColor : placementInvalidColor;
        if (placementPropertyBlock == null)
        {
            placementPropertyBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < placementRenderers.Count; i++)
        {
            PlacementRendererState state = placementRenderers[i];
            if (state.Renderer == null)
            {
                continue;
            }

            placementPropertyBlock.Clear();
            placementPropertyBlock.SetColor(state.ColorProperty, color);
            state.Renderer.SetPropertyBlock(placementPropertyBlock);
        }

        placementLastValid = isValid;
    }

    private void ClearPlacementVisuals()
    {
        if (placementRenderers.Count == 0)
        {
            return;
        }

        for (int i = 0; i < placementRenderers.Count; i++)
        {
            PlacementRendererState state = placementRenderers[i];
            if (state.Renderer == null)
            {
                continue;
            }

            state.Renderer.SetPropertyBlock(null);
        }

        placementRenderers.Clear();
        placementPropertyBlock = null;
    }

    private void CreateDroppedLootContainer(GameObject instance, Item item)
    {
        CreateDroppedLootContainer(instance, item, 1);
    }

    private void CreateDroppedLootContainer(GameObject instance, Item item, int quantity)
    {
        if (item == null)
        {
            return;
        }

        item.CreateDroppedLootContainer(instance, quantity, placementDestroyWhenEmpty);
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

    private CanvasGroup GetActionBoxCanvasGroup()
    {
        if (actionBox == null)
        {
            return null;
        }

        CanvasGroup canvasGroup = actionBox.GetComponent<CanvasGroup>();
        if (canvasGroup == null && actionBoxAddCanvasGroupIfMissing)
        {
            canvasGroup = actionBox.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private void FadeActionBoxTo(float targetAlpha, float duration)
    {
        actionBoxCanvasGroup = GetActionBoxCanvasGroup();
        if (actionBoxCanvasGroup == null)
        {
            return;
        }

        if (!CanRunCoroutines() || !actionBox.activeInHierarchy)
        {
            actionBoxCanvasGroup.alpha = targetAlpha;
            if (actionBoxDisableRaycastsWhenHidden)
            {
                bool visible = targetAlpha > 0.001f;
                actionBoxCanvasGroup.interactable = visible;
                actionBoxCanvasGroup.blocksRaycasts = visible;
            }
            return;
        }

        if (actionBoxFadeRoutine != null)
        {
            StopCoroutine(actionBoxFadeRoutine);
        }

        float startAlpha = actionBoxCanvasGroup.alpha;
        if (duration <= 0f)
        {
            actionBoxCanvasGroup.alpha = targetAlpha;
            if (actionBoxDisableRaycastsWhenHidden)
            {
                bool visible = targetAlpha > 0.001f;
                actionBoxCanvasGroup.interactable = visible;
                actionBoxCanvasGroup.blocksRaycasts = visible;
            }
            return;
        }

        actionBoxFadeRoutine = StartCoroutine(FadeActionBoxRoutine(actionBoxCanvasGroup, startAlpha, targetAlpha, duration));
    }

    private IEnumerator FadeActionBoxRoutine(CanvasGroup canvasGroup, float startAlpha, float targetAlpha, float duration)
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        float time = 0f;
        if (actionBoxDisableRaycastsWhenHidden)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

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
    }

    private CanvasGroup GetDepositQuantityCanvasGroup()
    {
        if (depositQuantityPanel == null)
        {
            return null;
        }

        CanvasGroup canvasGroup = depositQuantityPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null && depositQuantityAddCanvasGroupIfMissing)
        {
            canvasGroup = depositQuantityPanel.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private void FadeDepositQuantityTo(float targetAlpha, float duration)
    {
        depositQuantityCanvasGroup = GetDepositQuantityCanvasGroup();
        if (depositQuantityCanvasGroup == null)
        {
            return;
        }

        if (!CanRunCoroutines() || !depositQuantityPanel.activeInHierarchy)
        {
            depositQuantityCanvasGroup.alpha = targetAlpha;
            if (depositQuantityDisableRaycastsWhenHidden)
            {
                bool visible = targetAlpha > 0.001f;
                depositQuantityCanvasGroup.interactable = visible;
                depositQuantityCanvasGroup.blocksRaycasts = visible;
            }
            return;
        }

        if (depositQuantityFadeRoutine != null)
        {
            StopCoroutine(depositQuantityFadeRoutine);
        }

        float startAlpha = depositQuantityCanvasGroup.alpha;
        if (duration <= 0f)
        {
            depositQuantityCanvasGroup.alpha = targetAlpha;
            if (depositQuantityDisableRaycastsWhenHidden)
            {
                bool visible = targetAlpha > 0.001f;
                depositQuantityCanvasGroup.interactable = visible;
                depositQuantityCanvasGroup.blocksRaycasts = visible;
            }
            return;
        }

        depositQuantityFadeRoutine = StartCoroutine(FadeDepositQuantityRoutine(depositQuantityCanvasGroup, startAlpha, targetAlpha, duration));
    }

    private IEnumerator FadeDepositQuantityRoutine(CanvasGroup canvasGroup, float startAlpha, float targetAlpha, float duration)
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        float time = 0f;
        if (depositQuantityDisableRaycastsWhenHidden)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        if (depositQuantityDisableRaycastsWhenHidden)
        {
            bool visible = targetAlpha > 0.001f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }

    private GameObject CreateDepositQuantityPanel()
    {
        InventoryUISettings settings = GetSettings();
        Transform parent = settings != null && settings.inventoryPanel != null ? settings.inventoryPanel.transform : transform;

        GameObject panel = new GameObject("DepositQuantityPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(260f, 110f);
        rect.anchoredPosition = depositQuantityPanelOffset;

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.75f);
        image.raycastTarget = false;
        image.sprite = GetDepositQuantitySprite();
        image.type = Image.Type.Simple;

        GameObject textObject = new GameObject("DepositQuantity_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(8f, 8f);
        textRect.offsetMax = new Vector2(-8f, -8f);

        TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 28f;
        tmp.text = string.Empty;
        tmp.raycastTarget = false;

        if (settings != null && settings.descriptionText != null)
        {
            tmp.font = settings.descriptionText.font;
            tmp.fontSharedMaterial = settings.descriptionText.fontSharedMaterial;
            tmp.color = settings.descriptionText.color;
            tmp.fontSize = Mathf.Max(16f, settings.descriptionText.fontSize);
        }

        depositQuantityText = tmp;
        return panel;
    }

    private static Sprite GetDepositQuantitySprite()
    {
        if (depositQuantityFallbackSprite != null)
        {
            return depositQuantityFallbackSprite;
        }

        if (depositQuantityFallbackTexture == null)
        {
            depositQuantityFallbackTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            depositQuantityFallbackTexture.SetPixel(0, 0, Color.white);
            depositQuantityFallbackTexture.Apply();
        }

        depositQuantityFallbackSprite = Sprite.Create(
            depositQuantityFallbackTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);

        return depositQuantityFallbackSprite;
    }

    private bool CanRunCoroutines()
    {
        return isActiveAndEnabled && gameObject.activeInHierarchy;
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

    private readonly struct PlacementRigidbodyState
    {
        public PlacementRigidbodyState(Rigidbody body, bool wasKinematic, bool usedGravity)
        {
            Body = body;
            WasKinematic = wasKinematic;
            UsedGravity = usedGravity;
        }

        public Rigidbody Body { get; }
        public bool WasKinematic { get; }
        public bool UsedGravity { get; }
    }

    private readonly struct PlacementRendererState
    {
        public PlacementRendererState(Renderer renderer, string colorProperty)
        {
            Renderer = renderer;
            ColorProperty = colorProperty;
        }

        public Renderer Renderer { get; }
        public string ColorProperty { get; }
    }

    private readonly struct SlotInfo
    {
        public SlotInfo(InventorySlotUI slot, Vector2 position)
        {
            Slot = slot;
            Position = position;
        }

        public InventorySlotUI Slot { get; }
        public Vector2 Position { get; }
    }
}

public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public InventoryPanelController Owner { get; private set; }
    public Item Item { get; private set; }
    public int Quantity { get; private set; }
    public RectTransform SlotRect { get; private set; }

    public void Initialize(InventoryPanelController owner, Item item, int quantity)
    {
        Owner = owner;
        Item = item;
        Quantity = Mathf.Max(0, quantity);
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
