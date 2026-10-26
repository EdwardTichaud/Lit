using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Pause submenu for exploration abilities. Equipping remains Maison-only and host-validated.</summary>
public sealed class AbilitySelectionPanel : MonoBehaviour
{
    private AbilityService service;
    private GameObject canvasRoot, menu;
    private RectTransform content;
    private bool open;
    private bool dirty = true;
    private float refreshAt;
    private TMP_Text status;
    private PausePanelController pauseOwner;
    private GameObject previousSelection;
    private int openedFrame;
    public bool IsOpen => open;
    public event System.Action Closed;
    private void Awake()
    {
        service = GetComponent<AbilityService>();
        if (service != null) service.Changed += MarkDirty;
    }
    private void MarkDirty() => dirty = true;
    private void Update()
    {
        if (!open) return;
        if (service == null || !service.IsReady || LocalPlayerUtils.GetControlledCharacter() == null ||
            pauseOwner == null || !pauseOwner.IsOpen)
        { Close(); return; }
        if (Time.frameCount > openedFrame && InputFocusStack.HasFocus(this) &&
            (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Gamepad.current?.buttonEast.wasPressedThisFrame == true)) Close();
        if (open && (dirty || Time.unscaledTime >= refreshAt))
        { dirty = false; refreshAt = Time.unscaledTime + 1f; RefreshStatus(); }
    }
    private void Build()
    {
        canvasRoot = new GameObject("Maison_Abilities", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(transform, false);
        var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = short.MaxValue;
        var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        menu = new GameObject("AbilitySelection", typeof(RectTransform), typeof(Image)); menu.transform.SetParent(canvasRoot.transform, false);
        menu.GetComponent<Image>().color = new Color(.035f, .045f, .055f, .98f);
        var rect = menu.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(760, 690);
        var title = Text(menu.transform, "CAPACITÉ D’EXPLORATION", 30); Place(title.rectTransform, new Vector2(0, 292), new Vector2(690, 55));
        status = Text(menu.transform, "", 21); Place(status.rectTransform, new Vector2(0, 222), new Vector2(680, 90));
        var scrollRoot = new GameObject("Abilities", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        scrollRoot.transform.SetParent(menu.transform, false); Place(scrollRoot.GetComponent<RectTransform>(), new Vector2(0, -20), new Vector2(690, 380));
        scrollRoot.GetComponent<Image>().color = new Color(1, 1, 1, .025f);
        var contentRoot = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentRoot.transform.SetParent(scrollRoot.transform, false); content = contentRoot.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>(); layout.spacing = 12; layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlHeight = layout.childControlWidth = true; layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = scrollRoot.GetComponent<ScrollRect>(); scroll.viewport = scrollRoot.GetComponent<RectTransform>(); scroll.content = content; scroll.horizontal = false;
        foreach (var ability in service.Catalog)
        {
            var local = ability;
            var button = Button(content, ability.DisplayName, () => { service.RequestEquip(local); dirty = true; });
            var element = button.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 130;
            button.gameObject.AddComponent<AbilityMenuRow>().ability = ability;
        }
        var close = Button(menu.transform, "Fermer", Close); Place(close.GetComponent<RectTransform>(), new Vector2(0, -285), new Vector2(220, 55));
        menu.SetActive(false);
    }
    public void Open()
    {
        PausePanelController.Instance?.UI_OpenAbilitiesPanel();
    }
    public bool OpenFromPause(PausePanelController owner)
    {
        if (open) return pauseOwner == owner;
        if (owner == null || !owner.IsOpen || service == null || !service.IsReady ||
            LocalPlayerUtils.GetControlledCharacter() == null || !InputFocusStack.HasFocus(owner)) return false;
        if (canvasRoot == null) Build();
        pauseOwner = owner;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        openedFrame = Time.frameCount;
        canvasRoot.SetActive(true);
        open = true; menu.SetActive(true); InputFocusStack.Push(this); dirty = true;
        RefreshStatus();
        var first = System.Array.Find(menu.GetComponentsInChildren<Button>(), button => button.interactable);
        if (first != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        return true;
    }
    private void RefreshStatus()
    {
        var equipped = service.Equipped(service.LocalPlayerId);
        bool canEquip = service.IsInMaison();
        status.text = "Une seule capacité équipée par joueur. Changement à la Maison uniquement.\nÉquipée : " + (equipped != null ? equipped.DisplayName : "Synchronisation…");
        foreach (var row in content.GetComponentsInChildren<AbilityMenuRow>())
        {
            bool unlocked = service.IsUnlocked(row.ability);
            row.GetComponent<Button>().interactable = canEquip && unlocked && row.ability != equipped;
            row.GetComponentInChildren<TMP_Text>().text = row.ability.DisplayName +
                (row.ability == equipped ? " — ÉQUIPÉE" : unlocked ? (canEquip ? " — Équiper" : " — À la Maison") : " — VERROUILLÉE") + "\n<size=75%>" + row.ability.description +
                (!unlocked && row.ability.unlockCycle != null ? "\nDéblocage : " + row.ability.unlockCycle.title : "") + "</size>";
        }
    }
    public void Close()
    {
        if (!open) return;
        open = false; InputFocusStack.Pop(this); if (menu != null) menu.SetActive(false);
        if (canvasRoot != null) canvasRoot.SetActive(false);
        pauseOwner = null;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
        Closed?.Invoke();
    }
    private void OnDisable() => Close();
    private void OnDestroy() { Close(); if (service != null) service.Changed -= MarkDirty; }
    private static Button Button(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        var obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button)); obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = new Color(.16f, .20f, .23f, 1f);
        var button = obj.GetComponent<Button>(); button.onClick.AddListener(action);
        var text = Text(obj.transform, label, 24); text.raycastTarget = false;
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.sizeDelta = new Vector2(-24, -12);
        return button;
    }
    private static TMP_Text Text(Transform parent, string value, int size)
    {
        var obj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
        var text = obj.GetComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = size; text.alignment = TextAlignmentOptions.Center; text.color = new Color(.94f, .86f, .66f);
        return text;
    }
    private static void Place(RectTransform rect, Vector2 position, Vector2 size) { rect.anchoredPosition = position; rect.sizeDelta = size; }
}

internal sealed class AbilityMenuRow : MonoBehaviour { public AbilitySO ability; }
