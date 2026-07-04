using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Role: affiche les trois items defensifs assignes quand un AnimationEvent de combat le demande.
// Usage: appele par CombatHudController; se branche sur le GameObject "CombatDefensePanel" de la scene.
// Dependencies: CombatSessionManager, SquadCharacterController, LocalPlayerContext, TMP, Unity UI.
public class CombatDefensePanelController : MonoBehaviour
{
    private const string DefaultPanelName = "CombatDefensePanel";
    private const int SlotCount = 3;

    public static CombatDefensePanelController Instance { get; private set; }

    [SerializeField] private string panelName = DefaultPanelName;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private GameObject[] slotRoots = new GameObject[SlotCount];
    [SerializeField] private Button[] slotButtons = new Button[SlotCount];
    [SerializeField] private TextMeshProUGUI[] slotLabels = new TextMeshProUGUI[SlotCount];
    [SerializeField] private Text[] slotLegacyLabels = new Text[SlotCount];
    [SerializeField] private bool createMissingSlots = true;

    private readonly List<Item> visibleItems = new List<Item>(SlotCount);
    private bool visible;
    private bool warnedMissingPanel;

    public static CombatDefensePanelController EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        Instance = FindExistingController();
        if (Instance != null)
        {
            return Instance;
        }

        GameObject panel = FindSceneGameObjectByName(DefaultPanelName);
        if (panel != null)
        {
            Instance = panel.GetComponent<CombatDefensePanelController>();
            if (Instance == null)
            {
                Instance = panel.AddComponent<CombatDefensePanelController>();
            }

            return Instance;
        }

        GameObject host = new GameObject("CombatDefensePanelController");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<CombatDefensePanelController>();
        return Instance;
    }

    public static void SetAnimationEventVisible(bool shouldBeVisible)
    {
        CombatDefensePanelController controller = EnsureInstance();
        if (controller != null)
        {
            controller.SetVisible(shouldBeVisible);
        }
    }

    public static void HideActive()
    {
        if (Instance != null)
        {
            Instance.SetVisible(false);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolvePanel();
        SetVisible(false);
    }

    private void OnEnable()
    {
        LocalInputRouter.CombatUseItem += OnCombatUseItem;
    }

    private void OnDisable()
    {
        LocalInputRouter.CombatUseItem -= OnCombatUseItem;
        LocalPlayerInput.SetCombatInputActive(false);
    }

    private void Update()
    {
        if (!visible)
        {
            return;
        }

        RefreshSlots();
    }

    public void SetVisible(bool shouldBeVisible)
    {
        if (shouldBeVisible && !ResolvePanel())
        {
            visible = false;
            LocalPlayerInput.SetCombatInputActive(false);
            if (!warnedMissingPanel)
            {
                warnedMissingPanel = true;
                Debug.LogWarning("CombatDefensePanelController: CombatDefensePanel introuvable dans la scene.", this);
            }
            return;
        }

        visible = shouldBeVisible;
        LocalPlayerInput.SetCombatInputActive(shouldBeVisible);
        if (panelRoot == null)
        {
            return;
        }

        SetCanvasGroupVisible(panelCanvasGroup, shouldBeVisible);
        if (shouldBeVisible)
        {
            RefreshSlots();
            SelectFirstInteractableSlot();
        }
    }

    private bool ResolvePanel()
    {
        if (panelRoot == null)
        {
            if (string.Equals(gameObject.name, panelName, System.StringComparison.Ordinal))
            {
                panelRoot = gameObject;
            }
            else
            {
                panelRoot = FindSceneGameObjectByName(panelName);
            }
        }

        if (panelRoot == null)
        {
            return false;
        }

        if (panelCanvasGroup == null)
        {
            panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = panelRoot.AddComponent<CanvasGroup>();
            }
        }

        ResolveSlots();
        return true;
    }

    private void ResolveSlots()
    {
        if (slotButtons == null || slotButtons.Length != SlotCount)
        {
            slotButtons = new Button[SlotCount];
        }

        if (slotRoots == null || slotRoots.Length != SlotCount)
        {
            slotRoots = new GameObject[SlotCount];
        }

        if (slotLabels == null || slotLabels.Length != SlotCount)
        {
            slotLabels = new TextMeshProUGUI[SlotCount];
        }

        if (slotLegacyLabels == null || slotLegacyLabels.Length != SlotCount)
        {
            slotLegacyLabels = new Text[SlotCount];
        }

        Button[] foundButtons = panelRoot.GetComponentsInChildren<Button>(true);
        int foundIndex = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            Transform namedSlot = FindChildByName(panelRoot.transform, $"EnableItem_{i + 1}");
            if (namedSlot != null)
            {
                slotRoots[i] = namedSlot.gameObject;
                slotLabels[i] = null;
                slotLegacyLabels[i] = null;
                slotButtons[i] = EnsureSlotRootButton(namedSlot);
                ResolveSlotLabel(i, namedSlot);
            }

            if (slotButtons[i] == null &&
                slotRoots[i] == null &&
                foundButtons != null &&
                foundIndex < foundButtons.Length)
            {
                slotButtons[i] = foundButtons[foundIndex];
                slotRoots[i] = slotButtons[i] != null ? slotButtons[i].gameObject : null;
                foundIndex++;
            }

            if (slotButtons[i] == null && slotRoots[i] == null && createMissingSlots)
            {
                slotButtons[i] = CreateSlotButton(i);
                slotRoots[i] = slotButtons[i] != null ? slotButtons[i].gameObject : null;
            }

            if (slotLabels[i] == null && slotButtons[i] != null)
            {
                ResolveSlotLabel(i, slotButtons[i].transform);
            }

            if (slotButtons[i] == null)
            {
                continue;
            }

            int slotIndex = i;
            slotButtons[i].onClick.RemoveAllListeners();
            slotButtons[i].onClick.AddListener(() => SelectSlot(slotIndex));
        }
    }

    private Button CreateSlotButton(int index)
    {
        GameObject slot = new GameObject(
            $"CombatDefenseItem_{index + 1}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        slot.transform.SetParent(panelRoot.transform, false);

        RectTransform rect = slot.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(260f, 54f);

        Image image = slot.GetComponent<Image>();
        image.color = new Color(0.04f, 0.04f, 0.04f, 0.82f);
        image.sprite = RuntimeUiSpriteUtility.SolidSprite;
        image.type = Image.Type.Simple;

        LayoutElement layout = slot.GetComponent<LayoutElement>();
        layout.minHeight = 48f;
        layout.preferredHeight = 54f;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(slot.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 4f);
        labelRect.offsetMax = new Vector2(-12f, -4f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 22f;
        label.text = "-";
        slotLabels[index] = label;

        return slot.GetComponent<Button>();
    }

    private Button EnsureSlotRootButton(Transform slotRoot)
    {
        if (slotRoot == null)
        {
            return null;
        }

        Button button = slotRoot.GetComponent<Button>();
        if (button == null)
        {
            button = slotRoot.gameObject.AddComponent<Button>();
        }

        bool createdHitArea = false;
        Graphic targetGraphic = slotRoot.GetComponent<Graphic>();
        if (targetGraphic == null)
        {
            Image hitArea = slotRoot.gameObject.AddComponent<Image>();
            hitArea.sprite = RuntimeUiSpriteUtility.SolidSprite;
            hitArea.type = Image.Type.Simple;
            hitArea.color = Color.white;
            hitArea.raycastTarget = true;
            targetGraphic = hitArea;
            createdHitArea = true;
        }
        else
        {
            targetGraphic.raycastTarget = true;
        }

        button.targetGraphic = targetGraphic;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = BuildSlotButtonColors(button.colors);
        if (createdHitArea)
        {
            targetGraphic.canvasRenderer.SetColor(button.colors.normalColor);
        }

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Automatic;
        button.navigation = navigation;

        return button;
    }

    private static ColorBlock BuildSlotButtonColors(ColorBlock source)
    {
        source.normalColor = new Color(1f, 1f, 1f, 0f);
        source.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
        source.pressedColor = new Color(1f, 1f, 1f, 0.2f);
        source.selectedColor = new Color(1f, 1f, 1f, 0.14f);
        source.disabledColor = new Color(1f, 1f, 1f, 0f);
        source.colorMultiplier = 1f;
        source.fadeDuration = 0.08f;
        return source;
    }

    private void RefreshSlots()
    {
        ResolvePanel();
        visibleItems.Clear();

        SquadCharacterController controller = ResolveLocalController();
        if (controller != null)
        {
            List<Item> items = controller.GetEnabledCombatDefensiveItems();
            for (int i = 0; i < items.Count && visibleItems.Count < SlotCount; i++)
            {
                visibleItems.Add(items[i]);
            }
        }

        CombatSessionManager combatManager = CombatSessionManager.Instance;
        for (int i = 0; i < SlotCount; i++)
        {
            Item item = i < visibleItems.Count ? visibleItems[i] : null;
            bool hasItem = item != null;
            SetSlotVisible(i, hasItem);
            SetSlotLabel(i, hasItem ? ResolveItemDisplayName(item) : string.Empty);

            if (slotButtons == null || i >= slotButtons.Length || slotButtons[i] == null)
            {
                continue;
            }

            bool canUse = item != null
                && controller != null
                && combatManager != null
                && combatManager.CanUseDefensiveItemNow(controller, item, out _);
            slotButtons[i].interactable = canUse;
        }
    }

    private void SelectSlot(int index)
    {
        if (index < 0 || index >= visibleItems.Count)
        {
            return;
        }

        Item item = visibleItems[index];
        if (item == null)
        {
            return;
        }

        CombatSessionManager manager = CombatSessionManager.Instance;
        if (manager != null && manager.RequestLocalDefensiveItem(item))
        {
            RefreshSlots();
        }
    }

    private void OnCombatUseItem(int slotIndex)
    {
        if (!visible)
        {
            return;
        }

        SelectSlot(slotIndex);
    }

    private void SelectFirstInteractableSlot()
    {
        if (EventSystem.current == null || slotButtons == null)
        {
            return;
        }

        for (int i = 0; i < slotButtons.Length; i++)
        {
            Button button = slotButtons[i];
            if (button != null && button.interactable)
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                return;
            }
        }
    }

    private static SquadCharacterController ResolveLocalController()
    {
        Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
        if (localRoot != null)
        {
            return localRoot.GetComponentInChildren<SquadCharacterController>(true);
        }

        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        if (controlled != null)
        {
            return controlled.GetComponentInChildren<SquadCharacterController>(true);
        }

        CombatSessionManager manager = CombatSessionManager.Instance;
        if (manager != null &&
            manager.TryGetLocalCombatCameraContext(
                out Transform player,
                out _,
                out _,
                out _))
        {
            return player != null ? player.GetComponentInChildren<SquadCharacterController>(true) : null;
        }

        return null;
    }

    private static string ResolveItemDisplayName(Item item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(item.itemName))
        {
            return item.itemName;
        }

        return !string.IsNullOrWhiteSpace(item.name) ? item.name : "Item";
    }

    private void ResolveSlotLabel(int index, Transform slotRoot)
    {
        if (slotRoot == null)
        {
            return;
        }

        Transform textChild = FindChildByName(slotRoot, "Text");
        if (slotLabels[index] == null)
        {
            if (textChild != null)
            {
                slotLabels[index] = textChild.GetComponent<TextMeshProUGUI>();
            }

            if (slotLabels[index] == null)
            {
                slotLabels[index] = slotRoot.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        if (slotLegacyLabels[index] == null)
        {
            if (textChild != null)
            {
                slotLegacyLabels[index] = textChild.GetComponent<Text>();
            }

            if (slotLegacyLabels[index] == null)
            {
                slotLegacyLabels[index] = slotRoot.GetComponentInChildren<Text>(true);
            }
        }
    }

    private void SetSlotLabel(int index, string value)
    {
        if (slotLabels != null && index < slotLabels.Length && slotLabels[index] != null)
        {
            slotLabels[index].text = value;
        }

        if (slotLegacyLabels != null && index < slotLegacyLabels.Length && slotLegacyLabels[index] != null)
        {
            slotLegacyLabels[index].text = value;
        }
    }

    private void SetSlotVisible(int index, bool shouldBeVisible)
    {
        if (slotRoots == null || index < 0 || index >= slotRoots.Length || slotRoots[index] == null)
        {
            return;
        }

        if (slotRoots[index].activeSelf != shouldBeVisible)
        {
            slotRoots[index].SetActive(shouldBeVisible);
        }
    }

    private static void SetCanvasGroupVisible(CanvasGroup canvasGroup, bool shouldBeVisible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        if (shouldBeVisible)
        {
            EnsureActiveHierarchy(canvasGroup.transform);
        }

        canvasGroup.alpha = shouldBeVisible ? 1f : 0f;
        canvasGroup.interactable = shouldBeVisible;
        canvasGroup.blocksRaycasts = shouldBeVisible;
    }

    private static void EnsureActiveHierarchy(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Transform parent = target.parent;
        if (parent != null && parent.gameObject.scene.IsValid())
        {
            EnsureActiveHierarchy(parent);
        }

        if (!target.gameObject.activeSelf)
        {
            target.gameObject.SetActive(true);
        }

        if (target.localScale.sqrMagnitude <= 0.0001f)
        {
            target.localScale = Vector3.one;
        }
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        if (string.Equals(root.name, childName, System.StringComparison.Ordinal))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChildByName(root.GetChild(i), childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static GameObject FindSceneGameObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.gameObject == null)
            {
                continue;
            }

            if (!candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            if (string.Equals(candidate.name, objectName, System.StringComparison.Ordinal))
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private static CombatDefensePanelController FindExistingController()
    {
        CombatDefensePanelController[] controllers = Resources.FindObjectsOfTypeAll<CombatDefensePanelController>();
        if (controllers == null)
        {
            return null;
        }

        for (int i = 0; i < controllers.Length; i++)
        {
            CombatDefensePanelController controller = controllers[i];
            if (controller == null || controller.gameObject == null)
            {
                continue;
            }

            if (!controller.gameObject.scene.IsValid())
            {
                continue;
            }

            return controller;
        }

        return null;
    }
}
