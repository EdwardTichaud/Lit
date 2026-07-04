using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Role: affiche les trois items defensifs assignes pendant la reaction ennemie.
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
    [SerializeField] private Button[] slotButtons = new Button[SlotCount];
    [SerializeField] private TextMeshProUGUI[] slotLabels = new TextMeshProUGUI[SlotCount];
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

    public static void SetReactionVisible(bool shouldBeVisible)
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

    private void Update()
    {
        if (!visible)
        {
            return;
        }

        CombatSessionManager combatManager = CombatSessionManager.Instance;
        if (combatManager == null || !combatManager.IsLocalDefensiveReactionActive())
        {
            SetVisible(false);
            return;
        }

        HandleQuickSelectInput();
        RefreshSlots();
    }

    public void SetVisible(bool shouldBeVisible)
    {
        if (shouldBeVisible && !ResolvePanel())
        {
            visible = false;
            if (!warnedMissingPanel)
            {
                warnedMissingPanel = true;
                Debug.LogWarning("CombatDefensePanelController: CombatDefensePanel introuvable dans la scene.", this);
            }
            return;
        }

        visible = shouldBeVisible;
        if (panelRoot == null)
        {
            return;
        }

        if (shouldBeVisible && !panelRoot.activeSelf)
        {
            panelRoot.SetActive(true);
        }

        SetCanvasGroupVisible(panelCanvasGroup, shouldBeVisible);
        if (shouldBeVisible)
        {
            RefreshSlots();
            SelectFirstInteractableSlot();
        }
        else
        {
            panelRoot.SetActive(false);
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

        if (slotLabels == null || slotLabels.Length != SlotCount)
        {
            slotLabels = new TextMeshProUGUI[SlotCount];
        }

        Button[] foundButtons = panelRoot.GetComponentsInChildren<Button>(true);
        int foundIndex = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            if (slotButtons[i] == null && foundButtons != null && foundIndex < foundButtons.Length)
            {
                slotButtons[i] = foundButtons[foundIndex];
                foundIndex++;
            }

            if (slotButtons[i] == null && createMissingSlots)
            {
                slotButtons[i] = CreateSlotButton(i);
            }

            if (slotButtons[i] == null)
            {
                continue;
            }

            if (slotLabels[i] == null)
            {
                slotLabels[i] = slotButtons[i].GetComponentInChildren<TextMeshProUGUI>(true);
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
        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        image.type = Image.Type.Sliced;

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
        label.text = $"{index + 1}. -";
        slotLabels[index] = label;

        return slot.GetComponent<Button>();
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
            if (slotLabels != null && i < slotLabels.Length && slotLabels[i] != null)
            {
                slotLabels[i].text = item != null
                    ? $"{i + 1}. {ResolveItemDisplayName(item)}"
                    : $"{i + 1}. -";
            }

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

    private void HandleQuickSelectInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
        {
            SelectSlot(0);
        }
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
        {
            SelectSlot(1);
        }
        else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
        {
            SelectSlot(2);
        }
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
        return controlled != null ? controlled.GetComponentInChildren<SquadCharacterController>(true) : null;
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

    private static void SetCanvasGroupVisible(CanvasGroup canvasGroup, bool shouldBeVisible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = shouldBeVisible ? 1f : 0f;
        canvasGroup.interactable = shouldBeVisible;
        canvasGroup.blocksRaycasts = shouldBeVisible;
    }

    private static GameObject FindSceneGameObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject active = GameObject.Find(objectName);
        if (active != null)
        {
            return active;
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
