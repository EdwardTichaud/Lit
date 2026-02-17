using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Conteneur de loot avec interaction, UI et ActionBox (prendre/deposer/casser).
[RequireComponent(typeof(Collider))]
public class LootContainer : MonoBehaviour, ISerializationCallbackReceiver
{
    [System.Serializable]
    public class LootItemEntry
    {
        [Tooltip("Item stocke.")]
        public Item item;
        [Tooltip("Quantite stockee.")]
        public int quantity = 1;
    }

    [Header("Items")]
    [Tooltip("Liste des items dans ce conteneur.")]
    public List<LootItemEntry> lootItems = new List<LootItemEntry>();
    [Tooltip("Item associe au conteneur (icone/description).")]
    public Item containerItem;
    [Tooltip("Detruit l'objet si vide.")]
    public bool destroyWhenEmpty = false;
    [Tooltip("Si false, le joueur ne peut pas prendre (ex: MaisonChest).")]
    public bool collectable = true;
    [Tooltip("Capacite max de toutes les quantites (0 = infini).")]
    public int maxTotalQuantity = 0;

    [Header("Break")]
    [Tooltip("Autorise l'action Casser quand le conteneur est non collectable.")]
    public bool allowBreakWhenNotCollectable = true;
    [Tooltip("Message si l'item ne peut pas etre casse.")]
    public string breakInvalidMessage = "Cet objet ne peut pas etre casse.";
    [Tooltip("Message si le conteneur est plein apres casse.")]
    public string breakNoSpaceMessage = "Pas assez de place dans le coffre.";

    [Header("Feedback")]
    [Tooltip("Message si l'objet ne peut pas etre pris.")]
    public string takeNotAllowedMessage = "Impossible de prendre cet objet.";
    [Tooltip("Message si le container est plein.")]
    public string depositNoSpaceMessage = "Pas assez de place dans le coffre.";

    [Header("Action Box")]
    [Tooltip("ActionBox utilisee par le loot. Laisse vide pour auto-detecter.")]
    public GameObject actionBox;
    [Tooltip("Offset en UI par rapport au slot selectionne.")]
    public Vector2 actionBoxOffset = Vector2.zero;
    [Tooltip("Duree du fade de l'ActionBox.")]
    public float actionBoxFadeDuration = 0.15f;
    [Tooltip("Met l'alpha a 0 au demarrage.")]
    public bool actionBoxSetAlphaToZeroOnStart = true;
    [Tooltip("Ajoute un CanvasGroup si manquant.")]
    public bool actionBoxAddCanvasGroupIfMissing = true;
    [Tooltip("Desactive les raycasts quand cache.")]
    public bool actionBoxDisableRaycastsWhenHidden = true;
    [Tooltip("Alpha des actions disponibles.")]
    public float actionBoxEnabledAlpha = 1f;
    [Tooltip("Alpha des actions indisponibles.")]
    public float actionBoxDisabledAlpha = 0.25f;

    [Header("Interaction")]
    [Tooltip("Trigger d'interaction. Laisse vide pour auto-detecter.")]
    public Collider interactionTrigger;
    [Tooltip("Panel d'inventaire pour deposer/retirer.")]
    public InventoryPanelController inventoryPanelController;

    private readonly List<GameObject> charactersInRange = new List<GameObject>();
    private readonly Dictionary<GameObject, int> characterColliderCounts = new Dictionary<GameObject, int>();
    private GameObject currentCharacter;
    private bool lootOpen;
    private PlayerInputs playerInputs;
    private bool useSelfTriggerEvents;
    private bool depositInventoryOpen;
    private LootSlotUI currentFocusedSlot;
    private bool squadInputLocked;
    private readonly List<LootSlotUI> lootSlots = new List<LootSlotUI>();
    private int currentSlotIndex;
    private int lastMoveDirection;
    private float nextMoveTime;
    private bool cursorDirty;
    private CanvasGroup actionBoxCanvasGroup;
    private Coroutine actionBoxFadeRoutine;
    private bool actionBoxVisible;
    private readonly List<ActionBoxEntry> actionBoxEntries = new List<ActionBoxEntry>();
    [SerializeField, HideInInspector, FormerlySerializedAs("items")]
    private List<Item> legacyItems = new List<Item>();

    private void Awake()
    {
        InitializeInteractionTrigger();

        playerInputs = new PlayerInputs();
        LootUISettings settings = GetSettings();
        if (settings != null)
        {
            settings.InitializePanel();
        }

        InitializeActionBox();
    }

    private void OnEnable()
    {
        if (playerInputs == null)
        {
            playerInputs = new PlayerInputs();
        }

        playerInputs.Enable();
        playerInputs.Player.Interact.performed += OnInteractPerformed;
        playerInputs.Player.TakeAll.performed += OnTakeAllPerformed;
        playerInputs.Player.Return.performed += OnReturnPerformed;
        playerInputs.Player.ToggleTorch.performed += OnToggleTorchPerformed;
    }

    private void OnDisable()
    {
        if (playerInputs != null)
        {
            playerInputs.Player.Interact.performed -= OnInteractPerformed;
            playerInputs.Player.TakeAll.performed -= OnTakeAllPerformed;
            playerInputs.Player.Return.performed -= OnReturnPerformed;
            playerInputs.Player.ToggleTorch.performed -= OnToggleTorchPerformed;
            playerInputs.Disable();
        }

        InputFocusStack.Pop(this);
        CloseLoot();
        HideActionBoxImmediate();
        charactersInRange.Clear();
        characterColliderCounts.Clear();
        currentCharacter = null;
        depositInventoryOpen = false;
    }

    private void LateUpdate()
    {
        UpdateCurrentCharacter();

        if (lootOpen && HasInputFocus())
        {
            UpdateCursorVisual();
            if (actionBoxVisible)
            {
                PositionActionBox();
            }
        }
    }

    private void Update()
    {
        // Input loop: only when loot is open and this container has focus.
        if (!lootOpen)
        {
            return;
        }

        if (!HasInputFocus())
        {
            return;
        }

        if (depositInventoryOpen)
        {
            return;
        }

        HandleLootNavigation();
    }

    private LootUISettings GetSettings(bool logWarning = false)
    {
        LootUISettings settings = LootUISettings.Instance;

        if (logWarning && settings == null)
        {
            Debug.LogWarning("LootContainer: LootUISettings manquant.");
        }

        return settings;
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

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!CanProcessInteract())
        {
            return;
        }

        HandleInteract();
    }

    private void OnTakeAllPerformed(InputAction.CallbackContext context)
    {
        if (!HasInputFocus())
        {
            return;
        }

        HandleTakeAll();
    }

    private void OnReturnPerformed(InputAction.CallbackContext context)
    {
        if (!HasInputFocus())
        {
            return;
        }

        if (!lootOpen)
        {
            return;
        }

        if (depositInventoryOpen)
        {
            return;
        }

        if (actionBoxVisible)
        {
            HideActionBox();
            return;
        }

        CloseLoot();
    }

    private void OnToggleTorchPerformed(InputAction.CallbackContext context)
    {
        if (!HasInputFocus())
        {
            return;
        }

        if (!lootOpen)
        {
            return;
        }

        if (depositInventoryOpen)
        {
            return;
        }

        HideActionBox();

        UpdateCurrentCharacter();
        if (currentCharacter == null)
        {
            return;
        }

        InventoryPanelController inventory = GetInventoryPanelController();
        if (inventory == null)
        {
            return;
        }

        if (inventory.TryOpenForLootDeposit(this))
        {
            depositInventoryOpen = true;
        }
    }

    private void HandleInteract()
    {
        if (!lootOpen && InputFocusStack.HasAnyFocus())
        {
            return;
        }

        if (depositInventoryOpen)
        {
            return;
        }

        UpdateCurrentCharacter();
        if (currentCharacter == null)
        {
            return;
        }

        LootUISettings settings = GetSettings();
        if (lootOpen)
        {
            if (!collectable)
            {
                if (allowBreakWhenNotCollectable)
                {
                    if (actionBoxVisible)
                    {
                        if (TryBreakFocusedItem())
                        {
                            return;
                        }
                    }
                    else
                    {
                        ShowActionBox();
                    }
                }

                return;
            }

            if (TryTakeFocusedItem())
            {
                return;
            }

            if (settings != null && settings.toggleLootOnInteract)
            {
                CloseLoot();
            }

            return;
        }

        OpenLoot();
    }

    private void HandleTakeAll()
    {
        UpdateCurrentCharacter();
        if (currentCharacter == null || !lootOpen)
        {
            return;
        }

        if (depositInventoryOpen)
        {
            return;
        }

        TakeAllItems();
    }

    private void UpdateCurrentCharacter()
    {
        if (charactersInRange.Count == 0)
        {
            currentCharacter = null;
            return;
        }

        if (SquadManager.Instance != null && SquadManager.Instance.currentCharacter != null)
        {
            GameObject selected = SquadManager.Instance.currentCharacter;
            currentCharacter = charactersInRange.Contains(selected) ? selected : null;
            return;
        }

        currentCharacter = charactersInRange[0];
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

        UpdateCurrentCharacter();
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
        UpdateCurrentCharacter();

        if (currentCharacter == null)
        {
            LootUISettings settings = GetSettings();
            if (settings != null && settings.closeLootWhenLeaving)
            {
                CloseLoot();
            }
        }
    }

    public void NotifyTriggerEnter(Collider other)
    {
        HandleCharacterEnter(other);
    }

    public void NotifyTriggerExit(Collider other)
    {
        HandleCharacterExit(other);
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
            Debug.LogWarning("LootContainer: aucun collider trouve pour l'interaction.");
            useSelfTriggerEvents = false;
            return;
        }

        if (IsConcaveMeshCollider(interactionTrigger))
        {
            Collider fallback = CreateBoxTrigger(interactionTrigger);
            if (fallback != null)
            {
                interactionTrigger = fallback;
                Debug.LogWarning("LootContainer: MeshCollider concave detecte, ajout d'un BoxCollider Trigger pour l'interaction.", this);
            }
        }
        else if (!interactionTrigger.isTrigger)
        {
            interactionTrigger.isTrigger = true;
            Debug.LogWarning("LootContainer: le collider d'interaction n'etait pas en Trigger. Il a ete force en Trigger.", this);
        }

        useSelfTriggerEvents = interactionTrigger.gameObject == gameObject;
        if (!useSelfTriggerEvents)
        {
            LootContainerTriggerProxy proxy = interactionTrigger.GetComponent<LootContainerTriggerProxy>();
            if (proxy == null)
            {
                proxy = interactionTrigger.gameObject.AddComponent<LootContainerTriggerProxy>();
            }
            proxy.Owner = this;
        }
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

    private void OpenLoot()
    {
        LootUISettings settings = GetSettings(true);
        if (settings == null || settings.lootPanel == null)
        {
            Debug.LogWarning("LootContainer: aucun lootPanel defini.");
            return;
        }

        settings.OpenPanel();
        HideActionBoxImmediate();
        lootOpen = true;
        InputFocusStack.Push(this);
        SetSquadInputLock(true);
        settings.UpdateContainerHeader(this);

        RebuildLootSlots(null);
    }

    private void CloseLoot()
    {
        HideActionBoxImmediate();
        if (depositInventoryOpen)
        {
            InventoryPanelController inventory = GetInventoryPanelController();
            if (inventory != null)
            {
                inventory.CloseDepositInventory();
            }
            depositInventoryOpen = false;
        }

        LootUISettings settings = GetSettings();
        if (settings != null)
        {
            settings.ClosePanel();
        }

        lootOpen = false;
        InputFocusStack.Pop(this);
        SetSquadInputLock(false);
        currentFocusedSlot = null;
        currentSlotIndex = 0;
        lastMoveDirection = 0;
        nextMoveTime = 0f;
        cursorDirty = false;
        if (settings != null)
        {
            settings.UpdateDescription(null);
            settings.HideCursor();
        }
    }

    private bool HasInputFocus()
    {
        return InputFocusStack.HasFocus(this);
    }

    private bool CanProcessInteract()
    {
        if (InputFocusStack.HasFocus(this))
        {
            return true;
        }

        return !lootOpen && !InputFocusStack.HasAnyFocus();
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
        GameObject obj = new GameObject("LootItem");
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
        if (sprite == null)
        {
            targetImage.enabled = false;
        }
        else
        {
            targetImage.enabled = true;
        }
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

    public void FocusSlot(LootSlotUI slot)
    {
        if (slot == null || slot.SlotRect == null)
        {
            return;
        }

        currentFocusedSlot = slot;
        cursorDirty = true;
        if (lootSlots.Count > 0)
        {
            int index = lootSlots.IndexOf(slot);
            if (index >= 0)
            {
                currentSlotIndex = index;
            }
        }

        LootUISettings settings = GetSettings();
        if (settings != null)
        {
            settings.UpdateDescription(slot.Item);
        }
    }

    private void RebuildLootSlots(Item preferredItem, int preferredIndex = -1)
    {
        currentFocusedSlot = null;
        currentSlotIndex = 0;
        lootSlots.Clear();

        LootUISettings settings = GetSettings(true);
        if (settings == null || settings.lootItemsParent == null)
        {
            Debug.LogWarning("LootContainer: lootItemsParent manquant.");
            return;
        }

        Transform itemsParent = settings.lootItemsParent;
        GameObject itemPrefab = settings.lootItemPrefab;

        for (int i = itemsParent.childCount - 1; i >= 0; i--)
        {
            Destroy(itemsParent.GetChild(i).gameObject);
        }

        LootSlotUI firstSlot = null;
        LootSlotUI preferredSlot = null;
        for (int i = 0; i < lootItems.Count; i++)
        {
            LootItemEntry entryData = lootItems[i];
            if (entryData == null || entryData.item == null)
            {
                continue;
            }

            int quantity = Mathf.Max(0, entryData.quantity);
            if (quantity <= 0)
            {
                continue;
            }

            Item item = entryData.item;
            GameObject entry = CreateInstance(itemPrefab, itemsParent);
            if (entry == null)
            {
                entry = CreateTextEntry(itemsParent);
            }

            if (entry != null)
            {
                SetEntryText(entry, quantity.ToString());
                SetEntrySprite(entry, item);
                LootSlotUI slotUi = entry.GetComponent<LootSlotUI>();
                if (slotUi == null)
                {
                    slotUi = entry.AddComponent<LootSlotUI>();
                }
                slotUi.Initialize(this, entryData);
                lootSlots.Add(slotUi);
                if (firstSlot == null)
                {
                    firstSlot = slotUi;
                }
                if (preferredSlot == null && preferredItem != null && preferredItem == item)
                {
                    preferredSlot = slotUi;
                }
            }
        }

        if (lootSlots.Count > 0 && preferredIndex >= 0)
        {
            int clampedIndex = Mathf.Clamp(preferredIndex, 0, lootSlots.Count - 1);
            preferredSlot = lootSlots[clampedIndex];
        }

        if (preferredSlot != null)
        {
            FocusSlot(preferredSlot);
        }
        else if (firstSlot != null)
        {
            FocusSlot(firstSlot);
        }
        else
        {
            if (settings != null)
            {
                settings.UpdateDescription(null);
            }
            if (settings != null)
            {
                settings.HideCursor();
            }
        }
    }

    private void UpdateCursorVisual()
    {
        LootUISettings settings = GetSettings();
        if (settings == null)
        {
            return;
        }

        if (currentFocusedSlot == null)
        {
            GetFocusedSlot();
        }

        LootSlotUI slot = currentFocusedSlot;
        if (slot == null || slot.SlotRect == null)
        {
            settings.HideCursor();
            cursorDirty = false;
            return;
        }

        if (cursorDirty)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform itemsRect = settings.lootItemsParent != null ? settings.lootItemsParent as RectTransform : null;
            if (itemsRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRect);
            }
        }

        Transform itemsParent = settings.lootItemsParent;
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

        cursorDirty = false;
    }

    private bool TryTakeFocusedItem()
    {
        if (!collectable)
        {
            ShowActionFeedback(takeNotAllowedMessage);
            return false;
        }

        LootSlotUI focusedSlot = GetFocusedSlot();
        if (focusedSlot == null || focusedSlot.Entry == null)
        {
            return false;
        }

        LootItemEntry entry = focusedSlot.Entry;
        Item item = entry.item;
        if (item == null)
        {
            return false;
        }

        int quantity = Mathf.Max(0, entry.quantity);
        if (quantity <= 0)
        {
            return false;
        }

        if (!TryAddItemToCurrentCharacter(item, quantity))
        {
            return false;
        }

        lootItems.Remove(entry);
        RebuildLootSlots(null, currentSlotIndex);
        HandleEmptyContainer();
        ShowActionFeedback(item.GetTakeSuccessMessage());
        return true;
    }

    private bool TryBreakFocusedItem()
    {
        LootSlotUI focusedSlot = GetFocusedSlot();
        if (focusedSlot == null || focusedSlot.Entry == null)
        {
            return false;
        }

        LootItemEntry entry = focusedSlot.Entry;
        Item item = entry.item;
        if (item == null)
        {
            return false;
        }

        if (!item.HasBreakResults())
        {
            ShowBreakFeedback(breakInvalidMessage);
            return false;
        }

        int totalResults = GetBreakResultTotal(item);
        if (maxTotalQuantity > 0)
        {
            int remaining = GetRemainingCapacity();
            int effectiveRemaining = remaining + 1;
            if (totalResults > effectiveRemaining)
            {
                ShowBreakFeedback(breakNoSpaceMessage);
                return false;
            }
        }

        entry.quantity = Mathf.Max(0, entry.quantity - 1);
        if (entry.quantity <= 0)
        {
            lootItems.Remove(entry);
        }

        ApplyBreakResults(item);
        RebuildLootSlots(null, currentSlotIndex);
        HandleEmptyContainer();
        ShowBreakFeedback(item.GetBreakSuccessMessage());
        return true;
    }

    private int GetBreakResultTotal(Item item)
    {
        if (item == null || item.breakResults == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < item.breakResults.Count; i++)
        {
            Item.BreakResult result = item.breakResults[i];
            if (result == null || result.item == null)
            {
                continue;
            }

            int amount = Mathf.Max(0, result.quantity);
            total += amount;
        }

        return total;
    }

    private void ApplyBreakResults(Item item)
    {
        if (item == null || item.breakResults == null)
        {
            return;
        }

        for (int i = 0; i < item.breakResults.Count; i++)
        {
            Item.BreakResult result = item.breakResults[i];
            if (result == null || result.item == null)
            {
                continue;
            }

            int amount = Mathf.Max(0, result.quantity);
            if (amount <= 0)
            {
                continue;
            }

            AddItemsInternal(result.item, amount);
        }
    }

    private void AddItemsInternal(Item item, int quantity)
    {
        if (item == null || quantity <= 0)
        {
            return;
        }

        if (lootItems == null)
        {
            lootItems = new List<LootItemEntry>();
        }

        LootItemEntry existing = null;
        for (int i = 0; i < lootItems.Count; i++)
        {
            LootItemEntry entry = lootItems[i];
            if (entry != null && entry.item == item)
            {
                existing = entry;
                break;
            }
        }

        if (existing != null)
        {
            existing.quantity = Mathf.Max(0, existing.quantity + quantity);
        }
        else
        {
            lootItems.Add(new LootItemEntry { item = item, quantity = quantity });
        }
    }

    private void ShowBreakFeedback(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        InfoBoxUI.TryShow(message);
    }

    private void InitializeActionBox()
    {
        if (actionBox == null)
        {
            LootUISettings settings = GetSettings();
            if (settings != null && settings.lootPanel != null)
            {
                Transform found = settings.lootPanel.transform.Find("ActionBox");
                if (found != null)
                {
                    actionBox = found.gameObject;
                }
            }
        }

        if (actionBox == null)
        {
            return;
        }

        actionBoxCanvasGroup = GetActionBoxCanvasGroup();
        if (actionBoxCanvasGroup != null && actionBoxSetAlphaToZeroOnStart)
        {
            SetActionBoxAlpha(0f);
        }

        BuildActionBoxEntries();
        HideActionBoxCursor();
        actionBoxVisible = false;
    }

    private void EnsureActionBox()
    {
        if (actionBox == null)
        {
            LootUISettings settings = GetSettings();
            if (settings != null && settings.lootPanel != null)
            {
                Transform found = settings.lootPanel.transform.Find("ActionBox");
                if (found != null)
                {
                    actionBox = found.gameObject;
                }
            }
        }

        if (actionBox == null)
        {
            GameObject source = GameObject.Find("ActionBox");
            if (source != null)
            {
                Transform parent = null;
                LootUISettings settings = GetSettings();
                if (settings != null && settings.lootPanel != null)
                {
                    parent = settings.lootPanel.transform.parent;
                }

                actionBox = Instantiate(source, parent != null ? parent : source.transform.parent);
                actionBox.name = "LootActionBox";
            }
        }

        if (actionBox == null)
        {
            return;
        }

        actionBoxCanvasGroup = GetActionBoxCanvasGroup();
        if (actionBoxCanvasGroup != null && actionBoxSetAlphaToZeroOnStart)
        {
            SetActionBoxAlpha(0f);
        }

        BuildActionBoxEntries();
        HideActionBoxCursor();
    }

    private void ShowActionBox()
    {
        if (actionBoxVisible)
        {
            return;
        }

        EnsureActionBox();
        if (actionBox == null)
        {
            return;
        }

        actionBox.SetActive(true);
        PositionActionBox();
        ApplyActionBoxVisuals();
        FadeActionBoxTo(1f, actionBoxFadeDuration);
        actionBoxVisible = true;
    }

    private void HideActionBox()
    {
        if (!actionBoxVisible)
        {
            return;
        }

        FadeActionBoxTo(0f, actionBoxFadeDuration);
        actionBoxVisible = false;
    }

    private void HideActionBoxImmediate()
    {
        if (actionBox == null)
        {
            return;
        }

        FadeActionBoxTo(0f, 0f);
        actionBoxVisible = false;
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
        if (parentRect != null
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 localPoint))
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
                || name.IndexOf("_Text", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Cursor", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            RectTransform rect = child as RectTransform;
            Image frame = FindActionBoxFrame(child);
            TextMeshProUGUI label = FindActionBoxLabel(child);
            ActionBoxEntry entry = new ActionBoxEntry(rect, frame, label, name);
            actionBoxEntries.Add(entry);
        }
    }

    private void ApplyActionBoxVisuals()
    {
        for (int i = 0; i < actionBoxEntries.Count; i++)
        {
            ActionBoxEntry entry = actionBoxEntries[i];
            bool isBreak = entry.Name.IndexOf("casser", System.StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Name.IndexOf("break", System.StringComparison.OrdinalIgnoreCase) >= 0;
            float alpha = isBreak ? actionBoxEnabledAlpha : actionBoxDisabledAlpha;

            if (entry.Frame != null)
            {
                Color color = entry.FrameBaseColor;
                color.a *= alpha;
                entry.Frame.color = color;
            }

            if (entry.Label != null)
            {
                Color color = entry.LabelBaseColor;
                color.a *= alpha;
                entry.Label.color = color;
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
        CanvasGroup canvasGroup = GetActionBoxCanvasGroup();
        if (canvasGroup == null)
        {
            return;
        }

        if (!CanRunCoroutines())
        {
            SetActionBoxAlpha(targetAlpha);
            return;
        }

        if (actionBoxFadeRoutine != null)
        {
            StopCoroutine(actionBoxFadeRoutine);
        }

        float startAlpha = canvasGroup.alpha;
        if (duration <= 0f)
        {
            SetActionBoxAlpha(targetAlpha);
            return;
        }

        actionBoxFadeRoutine = StartCoroutine(FadeActionBoxRoutine(canvasGroup, startAlpha, targetAlpha, duration));
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

        SetActionBoxAlpha(targetAlpha);
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

    private void HideActionBoxCursor()
    {
        if (actionBox == null)
        {
            return;
        }

        Transform cursor = actionBox.transform.Find("ActionBox_Cursor");
        if (cursor == null)
        {
            return;
        }

        cursor.gameObject.SetActive(false);
    }

    private bool CanRunCoroutines()
    {
        return isActiveAndEnabled && gameObject.activeInHierarchy;
    }

    private LootSlotUI GetFocusedSlot()
    {
        if (currentFocusedSlot != null)
        {
            return currentFocusedSlot;
        }

        if (lootSlots.Count > 0)
        {
            int clampedIndex = Mathf.Clamp(currentSlotIndex, 0, lootSlots.Count - 1);
            FocusSlot(lootSlots[clampedIndex]);
            return currentFocusedSlot;
        }

        LootUISettings settings = GetSettings();
        Transform itemsParent = settings != null ? settings.lootItemsParent : null;
        if (itemsParent == null)
        {
            return null;
        }

        LootSlotUI slot = itemsParent.GetComponentInChildren<LootSlotUI>();
        if (slot != null)
        {
            FocusSlot(slot);
        }

        return slot;
    }

    public bool TryDepositItem(Item item, int quantity)
    {
        if (item == null || quantity <= 0)
        {
            return false;
        }

        if (!item.CanDepositToContainer(this, out string reason))
        {
            ShowActionFeedback(reason);
            return false;
        }

        SquadCharacterController controller = GetCurrentCharacterController();
        if (controller == null)
        {
            return false;
        }

        int remainingCapacity = GetRemainingCapacity();
        if (remainingCapacity <= 0)
        {
            ShowActionFeedback(depositNoSpaceMessage);
            return false;
        }

        if (quantity > remainingCapacity)
        {
            ShowActionFeedback(depositNoSpaceMessage);
            return false;
        }

        if (!controller.TryRemoveItemQuantity(item, quantity))
        {
            return false;
        }

        if (lootItems == null)
        {
            lootItems = new List<LootItemEntry>();
        }

        LootItemEntry existing = null;
        for (int i = 0; i < lootItems.Count; i++)
        {
            LootItemEntry entry = lootItems[i];
            if (entry != null && entry.item == item)
            {
                existing = entry;
                break;
            }
        }

        if (existing != null)
        {
            existing.quantity = Mathf.Max(0, existing.quantity + quantity);
        }
        else
        {
            lootItems.Add(new LootItemEntry { item = item, quantity = quantity });
        }

        if (lootOpen)
        {
            RebuildLootSlots(item, currentSlotIndex);
        }

        ShowActionFeedback(item.GetDepositSuccessMessage());
        return true;
    }

    public void AddItems(Item item, int quantity)
    {
        if (item == null || quantity <= 0)
        {
            return;
        }
        AddItemsInternal(item, quantity);

        if (lootOpen)
        {
            RebuildLootSlots(item, currentSlotIndex);
        }
    }

    public int AddItemsWithCapacity(Item item, int quantity)
    {
        if (item == null || quantity <= 0)
        {
            return 0;
        }

        int remaining = GetRemainingCapacity();
        if (remaining <= 0)
        {
            return 0;
        }

        int toAdd = maxTotalQuantity > 0 ? Mathf.Min(quantity, remaining) : quantity;
        if (toAdd <= 0)
        {
            return 0;
        }

        AddItems(item, toAdd);
        return toAdd;
    }

    public int GetTotalQuantity()
    {
        if (lootItems == null || lootItems.Count == 0)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < lootItems.Count; i++)
        {
            LootItemEntry entry = lootItems[i];
            if (entry == null)
            {
                continue;
            }

            int amount = Mathf.Max(0, entry.quantity);
            total += amount;
        }

        return total;
    }

    public int GetRemainingCapacity()
    {
        if (maxTotalQuantity <= 0)
        {
            return int.MaxValue;
        }

        int used = GetTotalQuantity();
        return Mathf.Max(0, maxTotalQuantity - used);
    }

    public int GetItemCount(Item item)
    {
        if (item == null || lootItems == null || lootItems.Count == 0)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < lootItems.Count; i++)
        {
            LootItemEntry entry = lootItems[i];
            if (entry == null || entry.item != item)
            {
                continue;
            }

            total += Mathf.Max(0, entry.quantity);
        }

        return total;
    }

    public int RemoveItems(Item item, int quantity)
    {
        if (item == null || quantity <= 0 || lootItems == null || lootItems.Count == 0)
        {
            return 0;
        }

        int remaining = quantity;
        for (int i = lootItems.Count - 1; i >= 0 && remaining > 0; i--)
        {
            LootItemEntry entry = lootItems[i];
            if (entry == null || entry.item != item)
            {
                continue;
            }

            int available = Mathf.Max(0, entry.quantity);
            if (available <= 0)
            {
                lootItems.RemoveAt(i);
                continue;
            }

            int toRemove = Mathf.Min(available, remaining);
            entry.quantity = Mathf.Max(0, available - toRemove);
            remaining -= toRemove;

            if (entry.quantity <= 0)
            {
                lootItems.RemoveAt(i);
            }
        }

        if (lootOpen)
        {
            RebuildLootSlots(item, currentSlotIndex);
        }

        return quantity - remaining;
    }

    public void SetLootItems(List<LootItemEntry> entries, bool rebuildIfOpen = true)
    {
        lootItems = entries ?? new List<LootItemEntry>();
        if (lootOpen && rebuildIfOpen)
        {
            RebuildLootSlots(null, currentSlotIndex);
        }
    }

    private void TakeAllItems()
    {
        if (!collectable)
        {
            ShowActionFeedback(takeNotAllowedMessage);
            return;
        }

        if (lootItems == null || lootItems.Count == 0)
        {
            return;
        }

        bool showedFeedback = false;
        for (int i = lootItems.Count - 1; i >= 0; i--)
        {
            LootItemEntry entry = lootItems[i];
            if (entry == null || entry.item == null)
            {
                lootItems.RemoveAt(i);
                continue;
            }

            int quantity = Mathf.Max(0, entry.quantity);
            if (quantity <= 0)
            {
                lootItems.RemoveAt(i);
                continue;
            }

            if (TryAddItemToCurrentCharacter(entry.item, quantity, !showedFeedback))
            {
                lootItems.RemoveAt(i);
            }
            else
            {
                showedFeedback = true;
            }
        }

        RebuildLootSlots(null, currentSlotIndex);
        HandleEmptyContainer();
    }

    private void HandleEmptyContainer()
    {
        if (!destroyWhenEmpty)
        {
            return;
        }

        if (lootItems == null || lootItems.Count > 0)
        {
            return;
        }

        CloseLoot();
        Destroy(gameObject);
    }

    private void HandleLootNavigation()
    {
        if (playerInputs == null || lootSlots.Count == 0)
        {
            return;
        }

        LootUISettings settings = GetSettings();
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
        LootSlotUI current = GetFocusedSlot();
        if (current == null)
        {
            return;
        }

        LootSlotUI next = FindNeighborSlot(current, direction, wrap);
        if (next == null || next == current)
        {
            return;
        }

        FocusSlot(next);
    }

    private LootSlotUI FindNeighborSlot(LootSlotUI current, int direction, bool wrap)
    {
        if (current == null || current.SlotRect == null)
        {
            return null;
        }

        LootUISettings settings = GetSettings();
        Canvas canvas = settings != null && settings.lootPanel != null
            ? settings.lootPanel.GetComponentInParent<Canvas>()
            : null;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (cursorDirty && settings != null && settings.lootItemsParent != null)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform itemsRect = settings.lootItemsParent as RectTransform;
            if (itemsRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRect);
            }
        }

        List<SlotInfo> slotInfos = new List<SlotInfo>(lootSlots.Count);
        for (int i = 0; i < lootSlots.Count; i++)
        {
            LootSlotUI slot = lootSlots[i];
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

    private float GetSlotScreenHeight(LootSlotUI slot, Camera uiCamera)
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

    private readonly struct SlotInfo
    {
        public SlotInfo(LootSlotUI slot, Vector2 position)
        {
            Slot = slot;
            Position = position;
        }

        public LootSlotUI Slot { get; }
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

    private bool TryAddItemToCurrentCharacter(Item item, int quantity, bool showFeedback = true)
    {
        if (item == null)
        {
            return false;
        }

        if (!item.CanTakeFromContainer(this, out string reason))
        {
            if (showFeedback)
            {
                ShowActionFeedback(reason);
            }
            return false;
        }

        SquadCharacterController controller = GetCurrentCharacterController();
        if (controller == null)
        {
            Debug.LogWarning("LootContainer: aucun personnage valide pour recevoir l'item.");
            return false;
        }

        controller.AddItem(item, quantity);
        return true;
    }

    private void ShowActionFeedback(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        InfoBoxUI.TryShow(message);
    }

    private SquadCharacterController GetCurrentCharacterController()
    {
        if (currentCharacter == null)
        {
            return null;
        }

        return currentCharacter.GetComponent<SquadCharacterController>();
    }

    private InventoryPanelController GetInventoryPanelController()
    {
        if (inventoryPanelController != null)
        {
            return inventoryPanelController;
        }

#if UNITY_2023_1_OR_NEWER
        inventoryPanelController = FindFirstObjectByType<InventoryPanelController>();
#else
        inventoryPanelController = FindFirstObjectByType<InventoryPanelController>();
#endif
        return inventoryPanelController;
    }

    public void NotifyDepositInventoryClosed()
    {
        depositInventoryOpen = false;
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

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        if ((lootItems == null || lootItems.Count == 0) && legacyItems != null && legacyItems.Count > 0)
        {
            lootItems = new List<LootItemEntry>(legacyItems.Count);
            for (int i = 0; i < legacyItems.Count; i++)
            {
                Item item = legacyItems[i];
                if (item == null)
                {
                    continue;
                }

                lootItems.Add(new LootItemEntry { item = item, quantity = 1 });
            }

            legacyItems.Clear();
        }
    }

    private GameObject GetSquadCharacter(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        Transform current = other.transform;
        bool hasPlayerTag = false;
        GameObject taggedPlayerRoot = null;
        GameObject squadRoot = null;
        bool hasSquadList = SquadManager.Instance != null && SquadManager.Instance.squadCharacters != null;
        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                hasPlayerTag = true;
                taggedPlayerRoot = current.gameObject;
            }

            if (hasSquadList && SquadManager.Instance.squadCharacters.Contains(current.gameObject))
            {
                squadRoot = current.gameObject;
            }

            current = current.parent;
        }

        if (squadRoot == null && hasSquadList)
        {
            Transform root = other.transform.root;
            if (root != null)
            {
                if (root.CompareTag("Player"))
                {
                    hasPlayerTag = true;
                    taggedPlayerRoot = root.gameObject;
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

        if (squadRoot != null)
        {
            return squadRoot;
        }

        if (hasPlayerTag && taggedPlayerRoot != null)
        {
            return taggedPlayerRoot;
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
}

public class LootContainerTriggerProxy : MonoBehaviour
{
    public LootContainer Owner { get; set; }

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

public class LootSlotUI : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public LootContainer Owner { get; private set; }
    public LootContainer.LootItemEntry Entry { get; private set; }
    public Item Item { get; private set; }
    public int Quantity { get; private set; }
    public RectTransform SlotRect { get; private set; }

    public void Initialize(LootContainer owner, LootContainer.LootItemEntry entry)
    {
        Owner = owner;
        Entry = entry;
        Item = entry != null ? entry.item : null;
        Quantity = entry != null ? Mathf.Max(0, entry.quantity) : 0;
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
