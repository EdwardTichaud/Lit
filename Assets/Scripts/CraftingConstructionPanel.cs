using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Unity.Netcode;
using UnityEngine.UI;

// Panel de craft pour les buildings de type "crafting".
public class CraftingConstructionPanel : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Root du panel.")]
    public GameObject craftingPanel;
    [Tooltip("Desactive le panel a la fermeture.")]
    public bool deactivatePanelOnClose = true;
    [Tooltip("Duree du fade d'ouverture/fermeture.")]
    public float panelFadeDuration = 0.15f;
    [Tooltip("Met l'alpha a 0 au demarrage.")]
    public bool setAlphaToZeroOnStart = true;
    [Tooltip("Desactive les raycasts quand cache.")]
    public bool disableRaycastsWhenHidden = true;

    [Header("Building Info")]
    [Tooltip("Nom du building.")]
    public TMP_Text buildingNameText;
    [Tooltip("Niveau actuel du building.")]
    public TMP_Text buildingLevelText;
    [Tooltip("Format du niveau (ex: \"Niveau {0}\").")]
    public string buildingLevelFormat = "Niveau {0}";

    [Header("Slots")]
    [Tooltip("Parent des slots de craft.")]
    public Transform slotsParent;
    [Tooltip("Prefab d'un slot de craft.")]
    public GameObject slotPrefab;
    [Tooltip("Curseur de selection des slots.")]
    public RectTransform slotCursor;
    [Tooltip("Padding ajoute au curseur.")]
    public Vector2 cursorPadding = new Vector2(10f, 10f);
    [Tooltip("Cree un curseur si manquant.")]
    public bool createCursorIfMissing = true;
    [Tooltip("Deadzone du stick pour naviguer.")]
    public float moveDeadzone = 0.5f;
    [Tooltip("Delai avant repetition de navigation.")]
    public float initialRepeatDelay = 0.35f;
    [Tooltip("Intervalle entre repetitions de navigation.")]
    public float repeatInterval = 0.12f;
    [Tooltip("Autorise le wrap du curseur.")]
    public bool wrapCursor = false;

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

    [Header("Resources")]
    [Tooltip("Utilise les ressources des coffres maison.")]
    public bool useHomeResources = true;

    [Header("Input")]
    [Tooltip("Craft avec Interact.")]
    public bool craftOnInteract = true;
    [Tooltip("Ferme le panel avec Return.")]
    public bool closeOnReturn = true;

    [Header("Messages")]
    [Tooltip("Message si le craft reussit.")]
    public string craftSuccessMessage = "Craft reussi.";
    [Tooltip("Message si le craft echoue.")]
    public string craftFailedMessage = "Ressources insuffisantes.";

    private bool panelOpen;
    private bool squadInputLocked;
    private CanvasGroup panelCanvasGroup;
    private Coroutine panelFadeRoutine;

    private BuildingInfoInteractable currentBuilding;
    private SquadCharacterController currentController;
    private BuilderController subscribedBuilder;
    private NetworkInventory subscribedInventory;
    private readonly List<CraftingSlotUI> craftingSlots = new List<CraftingSlotUI>();
    private CraftingSlotUI currentFocusedSlot;
    private int lastCursorIndex = -1;
    private int lastMoveDirection;
    private float nextMoveTime;
    private bool cursorDirty;
    private bool isRebuildingSlots;
    private bool pendingRebuildSlots;

    private readonly List<GameObject> requirementSlots = new List<GameObject>();
    private Maison cachedMaison;

    public bool IsOpen => panelOpen;

    private void Awake()
    {
        if (!LegacyBuildingSystem.Enabled)
        {
            enabled = false;
            return;
        }

        if (craftingPanel == null)
        {
            craftingPanel = gameObject;
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

        if (deactivatePanelOnClose && craftingPanel != null && craftingPanel != gameObject)
        {
            craftingPanel.SetActive(false);
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

        if (panelOpen)
        {
            ClosePanel();
        }
    }

    private void Update()
    {
        if (!panelOpen || !HasInputFocus())
        {
            return;
        }

        HandleNavigation();
        UpdateCursorVisual();
    }

    public void OpenPanel(BuildingInfoInteractable building, SquadCharacterController controller)
    {
        if (!LegacyBuildingSystem.Enabled)
        {
            return;
        }

        if (building == null || controller == null)
        {
            return;
        }

        currentBuilding = building;
        currentController = controller;
        SubscribeBuilder(ResolveBuilder());
        SubscribeInventory(currentController);

        if (craftingPanel == null)
        {
            craftingPanel = gameObject;
        }

        if (craftingPanel != null)
        {
            craftingPanel.SetActive(true);
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
        PlayUiActionAudio(ActionAudioCue.UiOpen);
        InputFocusStack.Push(this);
        SetSquadInputLock(true);
        RebuildSlotsSafely();
        UpdateBuildingInfo();
        FadePanelTo(1f, panelFadeDuration);
    }

    public void ClosePanel()
    {
        if (!panelOpen)
        {
            return;
        }

        panelOpen = false;
        PlayUiActionAudio(ActionAudioCue.UiClose);
        InputFocusStack.Pop(this);
        SetSquadInputLock(false);
        UnsubscribeBuilder();
        UnsubscribeInventory();
        currentBuilding = null;
        currentController = null;
        currentFocusedSlot = null;
        lastCursorIndex = -1;
        lastMoveDirection = 0;
        nextMoveTime = 0f;
        cursorDirty = false;
        ClearSlots();
        ClearRequirements();
        UpdateBuildingInfo();
        FadePanelTo(0f, panelFadeDuration);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!panelOpen || !craftOnInteract || !HasInputFocus())
        {
            return;
        }

        LocalInputRouter.ConsumeInteract();

        CraftingSlotUI slot = currentFocusedSlot;
        if (slot == null || slot.CraftItem == null || currentController == null || currentBuilding == null)
        {
            return;
        }

        if (IsNetworked() && !IsServer())
        {
            BuilderController builder = ResolveBuilder();
            if (builder != null)
            {
                builder.RequestCraft(currentBuilding, slot.CraftItem, craftSuccessMessage, craftFailedMessage);
            }
            return;
        }

        if (!HasResources(slot.CraftItem, currentController, out _))
        {
            PlayActionAudio(ActionAudioCue.CraftFailure);
            InfoBoxUI.TryShow(craftFailedMessage);
            return;
        }

        if (!TryConsumeCraftResources(slot.CraftItem, currentController))
        {
            PlayActionAudio(ActionAudioCue.CraftFailure);
            InfoBoxUI.TryShow(craftFailedMessage);
            return;
        }

        currentController.AddItem(slot.CraftItem, 1);
        SyncNetworkInventory(currentController);

        PlayActionAudio(ActionAudioCue.CraftSuccess);
        InfoBoxUI.TryShow(craftSuccessMessage);
        UpdateRequirements(slot.CraftItem);
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
            Vector3 position = currentBuilding != null ? currentBuilding.transform.position : transform.position;
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

    private void OnReturnPerformed(InputAction.CallbackContext context)
    {
        if (!panelOpen || !closeOnReturn || !HasInputFocus())
        {
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
        if (craftingSlots.Count == 0)
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

        if (absY >= absX)
        {
            return input.y > 0f ? -1 : 1;
        }

        return input.x > 0f ? 1 : -1;
    }

    private void MoveSlot(int direction, bool wrap)
    {
        if (craftingSlots.Count == 0)
        {
            return;
        }

        if (currentFocusedSlot == null)
        {
            FocusSlot(craftingSlots[0]);
            return;
        }

        int index = craftingSlots.IndexOf(currentFocusedSlot);
        if (index < 0)
        {
            FocusSlot(craftingSlots[0]);
            return;
        }

        int nextIndex = index + (direction > 0 ? 1 : -1);
        if (nextIndex < 0 || nextIndex >= craftingSlots.Count)
        {
            if (!wrap)
            {
                return;
            }

            nextIndex = nextIndex < 0 ? craftingSlots.Count - 1 : 0;
        }

        FocusSlot(craftingSlots[nextIndex]);
    }

    public void FocusSlot(CraftingSlotUI slot)
    {
        if (slot == null)
        {
            return;
        }

        currentFocusedSlot = slot;
        int index = craftingSlots.IndexOf(slot);
        if (index >= 0)
        {
            lastCursorIndex = index;
        }

        if (EventSystem.current != null && slot.gameObject != EventSystem.current.currentSelectedGameObject)
        {
            EventSystem.current.SetSelectedGameObject(slot.gameObject);
        }

        UpdateRequirements(slot.CraftItem);
        cursorDirty = true;
    }

    private void UpdateCursorVisual()
    {
        if (slotCursor == null && createCursorIfMissing)
        {
            slotCursor = CreateCursor();
        }

        if (slotCursor == null)
        {
            return;
        }

        if (currentFocusedSlot == null || currentFocusedSlot.SlotRect == null)
        {
            slotCursor.gameObject.SetActive(false);
            return;
        }

        if (cursorDirty)
        {
            Canvas.ForceUpdateCanvases();
        }

        RectTransform target = currentFocusedSlot.SlotRect;
        slotCursor.gameObject.SetActive(true);
        slotCursor.SetParent(target.parent, false);
        slotCursor.SetAsLastSibling();
        slotCursor.pivot = new Vector2(0.5f, 0.5f);
        slotCursor.position = target.position;
        Vector2 size = target.rect.size;
        slotCursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x + cursorPadding.x);
        slotCursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y + cursorPadding.y);
        cursorDirty = false;
    }

    private RectTransform CreateCursor()
    {
        Transform parent = slotsParent != null ? slotsParent : transform;
        GameObject cursorObject = new GameObject("CraftingPanel_Cursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = cursorObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        Image image = cursorObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.25f);
        image.raycastTarget = false;
        image.sprite = RuntimeUiSpriteUtility.SolidSprite;
        image.type = Image.Type.Simple;
        return rect;
    }

    private void UpdateBuildingInfo()
    {
        Item building = currentBuilding != null ? currentBuilding.BuildingItem : null;
        if (buildingNameText != null)
        {
            string name = building != null
                ? (!string.IsNullOrWhiteSpace(building.itemName) ? building.itemName : building.name)
                : string.Empty;
            buildingNameText.text = name;
        }

        if (buildingLevelText != null)
        {
            if (currentBuilding != null)
            {
                string level = Mathf.Max(1, currentBuilding.Level).ToString();
                buildingLevelText.text = string.Format(buildingLevelFormat, level);
            }
            else
            {
                buildingLevelText.text = string.Empty;
            }
        }
    }

    private void RebuildSlots()
    {
        ClearSlots();

        Item building = currentBuilding != null ? currentBuilding.BuildingItem : null;
        if (building == null || building.availableCrafts == null || building.availableCrafts.Count == 0)
        {
            return;
        }

        int level = currentBuilding != null ? currentBuilding.Level : 1;
        List<Item> crafts = building.GetUnlockedCraftsForLevel(level);
        if (crafts == null || crafts.Count == 0)
        {
            return;
        }

        for (int i = 0; i < crafts.Count; i++)
        {
            Item craftItem = crafts[i];
            if (craftItem == null)
            {
                continue;
            }

            GameObject slotObj = CreateSlotInstance();
            if (slotObj == null)
            {
                continue;
            }

            CraftingSlotUI slotUi = slotObj.GetComponent<CraftingSlotUI>();
            if (slotUi == null)
            {
                slotUi = slotObj.AddComponent<CraftingSlotUI>();
            }

            slotUi.Initialize(this, craftItem);
            UpdateSlotVisual(slotObj, craftItem);
            craftingSlots.Add(slotUi);
        }

        if (craftingSlots.Count > 0)
        {
            FocusSlot(craftingSlots[0]);
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
        if (slotPrefab != null)
        {
            return Instantiate(slotPrefab, parent);
        }

        GameObject root = new GameObject("CraftingSlot", typeof(RectTransform));
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
        for (int i = craftingSlots.Count - 1; i >= 0; i--)
        {
            CraftingSlotUI slot = craftingSlots[i];
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }

        craftingSlots.Clear();
        currentFocusedSlot = null;
        lastCursorIndex = -1;
        cursorDirty = false;
        if (slotCursor != null)
        {
            slotCursor.gameObject.SetActive(false);
        }
    }

    private void UpdateSlotVisual(GameObject slotObj, Item craftItem)
    {
        if (slotObj == null || craftItem == null)
        {
            return;
        }

        Image image = FindSlotSpriteImage(slotObj);
        if (image != null)
        {
            image.sprite = craftItem.itemSprite;
            image.enabled = image.sprite != null;
        }

        TMP_Text quantityText = FindSlotQuantityText(slotObj);
        if (quantityText != null)
        {
            quantityText.text = string.Empty;
        }
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

    private void UpdateRequirements(Item craftItem)
    {
        ClearRequirements();
        if (craftItem == null)
        {
            if (requirementsParent != null && hideRequirementsWhenEmpty)
            {
                requirementsParent.gameObject.SetActive(false);
            }
            return;
        }

        Dictionary<Item, int> requiredCounts = BuildRequirementCounts(craftItem);
        if (requiredCounts.Count == 0)
        {
            if (requirementsParent != null && hideRequirementsWhenEmpty)
            {
                requirementsParent.gameObject.SetActive(false);
            }
            return;
        }

        SquadCharacterController controller = currentController;
        BuilderController builder = ResolveBuilder();
        BuilderController.RequirementAvailability availability = builder != null
            ? builder.EvaluateRequirements(craftItem, controller, builder.useHomeResourcesForCraft)
            : null;
        Dictionary<Item, int> inventoryCounts = BuildInventoryCounts(controller);
        List<InteractableItem> homeContainers = builder != null
            ? (builder.useHomeResourcesForCraft ? ResolveHomeContainers() : null)
            : ResolveHomeContainers();

        if (builder != null && availability != null)
        {
            builder.LogCraftRequirementAnalysis(
                "preview",
                craftItem,
                availability,
                previewCraftable: availability.Craftable,
                validationCraftable: availability.Craftable,
                consumptionSources: "preview_only");
        }

        if (requirementsParent != null)
        {
            requirementsParent.gameObject.SetActive(true);
        }

        foreach (KeyValuePair<Item, int> requirement in requiredCounts)
        {
            Item requiredItem = requirement.Key;
            int requiredQuantity = requirement.Value;
            if (requiredItem == null || requiredQuantity <= 0)
            {
                continue;
            }

            int available = 0;
            if (availability != null)
            {
                available = availability.GetCombinedContribution(requiredItem);
            }
            else
            {
                if (inventoryCounts.TryGetValue(requiredItem, out int invCount))
                {
                    available += invCount;
                }

                if (homeContainers != null)
                {
                    available += GetHomeItemCount(requiredItem, homeContainers);
                }
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

    private bool HasResources(Item craftItem, SquadCharacterController controller, out int available)
    {
        available = 0;
        if (craftItem == null || controller == null)
        {
            return false;
        }

        BuilderController builder = ResolveBuilder();
        if (builder != null)
        {
            BuilderController.RequirementAvailability availability = builder.EvaluateRequirements(
                craftItem,
                controller,
                builder.useHomeResourcesForCraft);
            builder.LogCraftRequirementAnalysis(
                "preview_validation",
                craftItem,
                availability,
                previewCraftable: availability.Craftable,
                validationCraftable: availability.Craftable,
                consumptionSources: "preview_only");
            foreach (KeyValuePair<Item, int> requirement in availability.RequiredCounts)
            {
                available = availability.GetCombinedContribution(requirement.Key);
                if (available < requirement.Value)
                {
                    return false;
                }
            }

            return true;
        }

        Dictionary<Item, int> requiredCounts = BuildRequirementCounts(craftItem);
        if (requiredCounts.Count == 0)
        {
            return true;
        }

        Dictionary<Item, int> inventoryCounts = BuildInventoryCounts(controller);
        List<InteractableItem> homeContainers = ResolveHomeContainers();

        foreach (KeyValuePair<Item, int> requirement in requiredCounts)
        {
            Item requiredItem = requirement.Key;
            int requiredQuantity = requirement.Value;

            int current = 0;
            if (inventoryCounts.TryGetValue(requiredItem, out int invCount))
            {
                current += invCount;
            }

            if (useHomeResources && homeContainers != null)
            {
                current += GetHomeItemCount(requiredItem, homeContainers);
            }

            available = current;
            if (current < requiredQuantity)
            {
                return false;
            }
        }

        return true;
    }

    private bool TryConsumeCraftResources(Item craftItem, SquadCharacterController controller)
    {
        if (craftItem == null || controller == null)
        {
            return false;
        }

        BuilderController builder = ResolveBuilder();
        if (builder != null)
        {
            return builder.TryConsumeCraftRequirements(craftItem, controller, out _);
        }

        Dictionary<Item, int> requiredCounts = BuildRequirementCounts(craftItem);
        if (requiredCounts.Count == 0)
        {
            return true;
        }

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

            if (useHomeResources && homeContainers != null)
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

            if (remaining > 0 && useHomeResources && homeContainers != null)
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
        GameObject slot = null;
        if (requirementSlotPrefab != null)
        {
            slot = Instantiate(requirementSlotPrefab, parent);
        }
        else
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
            slot = root;
        }

        if (slot != null)
        {
            requirementSlots.Add(slot);
        }

        return slot;
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

    private List<InteractableItem> ResolveHomeContainersForOutput()
    {
        Maison maison = GetMaison();
        if (maison == null)
        {
            return null;
        }

        List<InteractableItem> containers = maison.ResolveMaisonLootContainers(null);
        return containers != null && containers.Count > 0 ? containers : null;
    }

    private bool CanAddToHomeContainers(Item item, int quantity, out List<InteractableItem> containers)
    {
        containers = ResolveHomeContainersForOutput();
        if (item == null || quantity <= 0 || containers == null || containers.Count == 0)
        {
            return false;
        }

        Maison maison = GetMaison();
        if (maison != null)
        {
            maison.EnsureHomeContainers(containers);
        }

        int remaining = GetTotalRemainingCapacity(containers);
        return remaining >= quantity;
    }

    private bool AddToHomeContainers(Item item, int quantity, List<InteractableItem> containers)
    {
        if (item == null || quantity <= 0 || containers == null || containers.Count == 0)
        {
            return false;
        }

        int remaining = quantity;
        for (int i = 0; i < containers.Count && remaining > 0; i++)
        {
            InteractableItem container = containers[i];
            if (container == null)
            {
                continue;
            }

            int available = container.GetRemainingCapacity();
            if (available <= 0)
            {
                continue;
            }

            int toAdd = available == int.MaxValue ? remaining : Mathf.Min(available, remaining);
            if (toAdd <= 0)
            {
                continue;
            }

            container.AddItems(item, toAdd);
            remaining -= toAdd;
        }

        return remaining <= 0;
    }

    private int GetTotalRemainingCapacity(List<InteractableItem> containers)
    {
        if (containers == null || containers.Count == 0)
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

            int remaining = container.GetRemainingCapacity();
            if (remaining == int.MaxValue)
            {
                return int.MaxValue;
            }

            total += remaining;
        }

        return total;
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

    private CanvasGroup GetPanelCanvasGroup()
    {
        if (craftingPanel == null)
        {
            return null;
        }

        CanvasGroup group = craftingPanel.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = craftingPanel.AddComponent<CanvasGroup>();
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

            if (deactivatePanelOnClose && targetAlpha <= 0.001f && craftingPanel != null && craftingPanel != gameObject)
            {
                craftingPanel.SetActive(false);
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

        if (deactivatePanelOnClose && targetAlpha <= 0.001f && craftingPanel != null && craftingPanel != gameObject)
        {
            craftingPanel.SetActive(false);
        }

        panelFadeRoutine = null;
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

    private Dictionary<Item, int> BuildRequirementCounts(Item craftItem)
    {
        Dictionary<Item, int> counts = new Dictionary<Item, int>();
        if (craftItem == null || craftItem.buildingRequirements == null)
        {
            return counts;
        }

        for (int i = 0; i < craftItem.buildingRequirements.Count; i++)
        {
            Item.BuildingRequirement requirement = craftItem.buildingRequirements[i];
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

    private void SubscribeInventory(SquadCharacterController controller)
    {
        if (!IsNetworked())
        {
            return;
        }

        NetworkInventory inventory = null;
        if (controller != null)
        {
            inventory = controller.GetComponent<NetworkInventory>();
            if (inventory == null)
            {
                inventory = controller.GetComponentInChildren<NetworkInventory>(true);
            }
        }

        if (subscribedInventory == inventory)
        {
            return;
        }

        if (subscribedInventory != null)
        {
            subscribedInventory.InventoryChanged -= OnInventoryChanged;
        }

        subscribedInventory = inventory;
        if (subscribedInventory != null)
        {
            subscribedInventory.InventoryChanged += OnInventoryChanged;
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

    private void UnsubscribeInventory()
    {
        if (subscribedInventory != null)
        {
            subscribedInventory.InventoryChanged -= OnInventoryChanged;
            subscribedInventory = null;
        }
    }

    private void OnBuildingsChanged()
    {
        if (!panelOpen)
        {
            return;
        }

        UpdateBuildingInfo();
        RebuildSlotsSafely();
        CraftingSlotUI slot = currentFocusedSlot;
        if (slot != null && slot.CraftItem != null)
        {
            UpdateRequirements(slot.CraftItem);
        }
    }

    private void OnInventoryChanged()
    {
        if (!panelOpen)
        {
            return;
        }

        CraftingSlotUI slot = currentFocusedSlot;
        if (slot != null && slot.CraftItem != null)
        {
            UpdateRequirements(slot.CraftItem);
        }
    }

    private BuilderController ResolveBuilder()
    {
        if (currentBuilding != null)
        {
            BuilderController builder = currentBuilding.GetComponentInParent<BuilderController>();
            if (builder != null)
            {
                return builder;
            }
        }

#if UNITY_2023_1_OR_NEWER
        return FindAnyObjectByType<BuilderController>();
#else
        return FindAnyObjectByType<BuilderController>();
#endif
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
}

public class CraftingSlotUI : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public CraftingConstructionPanel Owner { get; private set; }
    public Item CraftItem { get; private set; }
    public RectTransform SlotRect { get; private set; }

    public void Initialize(CraftingConstructionPanel owner, Item craftItem)
    {
        Owner = owner;
        CraftItem = craftItem;
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
