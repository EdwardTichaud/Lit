using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Pause submenu for exploration abilities. Equipping remains Maison-only and host-validated.</summary>
public sealed class AbilitySelectionPanel : MonoBehaviour
{
    private AbilityService service;
    private GameObject canvasRoot, menu;
    private RectTransform content;
    private ScrollRect scroll;
    private readonly List<Button> options = new List<Button>();
    private Button selected;
    private GameObject fallbackEventSystem;
    private bool open;
    private bool dirty = true;
    private float refreshAt;
    private TMP_Text status;
    private PausePanelController pauseOwner;
    private GameObject previousSelection;
    private int openedFrame;
    private int lastActivationFrame = -1;
    private int moveDirection;
    private float nextMoveAt;
    private string feedback;
    public bool IsOpen => open;
    public event System.Action Closed;
    private void Awake()
    {
        service = GetComponent<AbilityService>();
        if (service != null) service.Changed += MarkDirty;
    }
    private void MarkDirty() { dirty = true; feedback = null; }
    private void Update()
    {
        if (!open) return;
        if (service == null || !service.CanOpenEquipmentMenu ||
            pauseOwner == null || !pauseOwner.IsOpen)
        { Close(); return; }
        if (dirty || Time.unscaledTime >= refreshAt)
        { dirty = false; refreshAt = Time.unscaledTime + 1f; RefreshStatus(); }
        if (Time.frameCount <= openedFrame || !InputFocusStack.HasFocus(this)) return;

        var keyboard = Keyboard.current;
        var pad = Gamepad.current;
        if (keyboard?.escapeKey.wasPressedThisFrame == true || pad?.buttonEast.wasPressedThisFrame == true)
        { Close(); return; }
        float vertical = pad != null ? pad.dpad.ReadValue().y : 0f;
        if (Mathf.Abs(vertical) < .5f && pad != null) vertical = pad.leftStick.ReadValue().y;
        if (keyboard?.upArrowKey.isPressed == true) vertical = 1f;
        else if (keyboard?.downArrowKey.isPressed == true) vertical = -1f;
        int direction = vertical > .5f ? -1 : vertical < -.5f ? 1 : 0;
        if (direction != 0 && (direction != moveDirection || Time.unscaledTime >= nextMoveAt))
        {
            MoveSelection(direction);
            nextMoveAt = Time.unscaledTime + (direction == moveDirection ? .14f : .30f);
        }
        moveDirection = direction;
        if (pad?.buttonSouth.wasPressedThisFrame == true || keyboard?.enterKey.wasPressedThisFrame == true)
            if (selected != null && selected.interactable) selected.onClick.Invoke();
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
        var title = Text(menu.transform, "CAPACITÉS D’EXPLORATION", 30); Place(title.rectTransform, new Vector2(0, 292), new Vector2(690, 55));
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
        scroll = scrollRoot.GetComponent<ScrollRect>(); scroll.viewport = scrollRoot.GetComponent<RectTransform>(); scroll.content = content;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        foreach (var ability in service.Catalog)
        {
            var local = ability;
            var button = Button(content, ability.DisplayName, () => Equip(local), out var description);
            var element = button.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 82;
            description.alignment = TextAlignmentOptions.MidlineLeft;
            description.fontSize = 22;
            description.rectTransform.anchorMax = new Vector2(.72f, 1f);
            var action = Text(button.transform, "", 20);
            action.raycastTarget = false;
            action.fontStyle = FontStyles.Bold;
            action.rectTransform.anchorMin = new Vector2(.72f, 0f);
            action.rectTransform.anchorMax = Vector2.one;
            action.rectTransform.sizeDelta = new Vector2(-16, -12);
            var row = button.gameObject.AddComponent<AbilityMenuRow>();
            row.ability = ability;
            row.button = button;
            row.description = description;
            row.action = action;
            options.Add(button);
        }
        var close = Button(menu.transform, "Retour", Close, out _);
        options.Add(close);
        Place(close.GetComponent<RectTransform>(), new Vector2(0, -265), new Vector2(220, 55));
        var controls = Text(menu.transform, "Haut / Bas  Choisir     A / Entrée  Valider     B / Échap  Retour", 17);
        Place(controls.rectTransform, new Vector2(0, -314), new Vector2(680, 32));
        menu.SetActive(false);
    }
    public void Open()
    {
        PausePanelController.Instance?.UI_OpenAbilitiesPanel();
    }
    public bool OpenFromPause(PausePanelController owner)
    {
        if (open) return pauseOwner == owner;
        if (owner == null || !owner.IsOpen || service == null || !service.CanOpenEquipmentMenu ||
            !owner.HasPauseInputFocus()) return false;
        if (canvasRoot == null) Build();
        pauseOwner = owner;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        openedFrame = Time.frameCount;
        moveDirection = 0;
        feedback = null;
        canvasRoot.SetActive(true);
        EnsurePointerInput();
        LitSystemCursorLease.Acquire(this, true, CursorLockMode.None);
        EventSystem.current?.SetSelectedGameObject(null);
        open = true; menu.SetActive(true); InputFocusStack.Push(this); dirty = true;
        scroll.verticalNormalizedPosition = 1f;
        RefreshStatus();
        return true;
    }

    private void EnsurePointerInput()
    {
        if (EventSystem.current != null && EventSystem.current != fallbackEventSystem?.GetComponent<EventSystem>())
        {
            if (fallbackEventSystem != null) fallbackEventSystem.SetActive(false);
            return;
        }
        if (fallbackEventSystem == null)
        {
            fallbackEventSystem = new GameObject("AbilityPointerInput", typeof(EventSystem), typeof(InputSystemUIInputModule));
            fallbackEventSystem.transform.SetParent(canvasRoot.transform, false);
            fallbackEventSystem.GetComponent<EventSystem>().sendNavigationEvents = false;
        }
        fallbackEventSystem.SetActive(true);
    }

    private void RefreshStatus()
    {
        string playerId = service.LocalPlayerId;
        bool solo = service.IsSolo;
        int equippedCount = service.EquippedAbilities(playerId).Count;
        status.text = (solo ? "Choisis les capacités à emporter : tu peux en équiper plusieurs." :
            "Les capacités sont partagées : choisir celle d’un allié permet de l’échanger.") +
            "\n" + (feedback ?? (equippedCount == 0 ? "Aucune capacité équipée" :
                equippedCount == 1 ? "1 capacité équipée" : equippedCount + " capacités équipées"));
        foreach (var row in content.GetComponentsInChildren<AbilityMenuRow>())
        {
            bool unlocked = service.IsUnlocked(row.ability);
            bool equipped = service.IsEquipped(playerId, row.ability);
            bool testing = service.IsTestAbilityActive(row.ability);
            row.button.interactable = unlocked && !testing && (solo || !equipped);
            row.description.text = row.ability.DisplayName + (equipped ? "  <color=#A8E2BA>· Équipée</color>" : "") +
                "\n<size=75%>" + row.ability.description +
                (!unlocked && row.ability.unlockCycle != null ? " — " + row.ability.unlockCycle.title : "") + "</size>";
            row.action.text = testing ? "Test actif" : !unlocked ? "Verrouillée" : equipped ? (solo ? "Retirer" : "Équipée") :
                solo || equippedCount == 0 ? "Équiper" : "Échanger";
            row.action.color = unlocked ? new Color(.94f, .86f, .66f) : new Color(.64f, .67f, .70f);
        }
        if (selected == null || !selected.interactable) MoveSelection(1);
    }

    private void Equip(AbilitySO ability)
    {
        if (service == null || !service.CanOpenEquipmentMenu) return;
        bool equipped = service.IsEquipped(service.LocalPlayerId, ability);
        if (service.RequestEquip(ability))
            feedback = service.Authority ? ability.DisplayName + (equipped ? " : retirée" : " : équipée") :
                "Demande envoyée à l’hôte…";
        else feedback = "Impossible de modifier cet équipement pour le moment.";
        RefreshStatus();
    }

    private void MoveSelection(int direction)
    {
        int index = options.IndexOf(selected);
        for (int i = 0; i < options.Count; i++)
        {
            index = (index + direction + options.Count) % options.Count;
            var candidate = options[index];
            if (!candidate.interactable) continue;
            if (selected != null) selected.image.color = new Color(.16f, .20f, .23f);
            selected = candidate;
            selected.image.color = new Color(.32f, .41f, .44f);
            if (selected.transform.parent == content)
            {
                Canvas.ForceUpdateCanvases();
                float overflow = content.rect.height - scroll.viewport.rect.height;
                if (overflow > 0f)
                {
                    var rect = (RectTransform)selected.transform;
                    float top = -rect.anchoredPosition.y - rect.rect.height * (1f - rect.pivot.y);
                    float offset = (1f - scroll.verticalNormalizedPosition) * overflow;
                    float target = Mathf.Clamp(offset, top + rect.rect.height - scroll.viewport.rect.height, top);
                    scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(target / overflow);
                }
            }
            return;
        }
    }
    public void Close()
    {
        if (!open) return;
        open = false; InputFocusStack.Pop(this); if (menu != null) menu.SetActive(false);
        LitSystemCursorLease.Release(this);
        if (canvasRoot != null) canvasRoot.SetActive(false);
        pauseOwner = null;
        if (selected != null) selected.image.color = new Color(.16f, .20f, .23f);
        selected = null;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
        Closed?.Invoke();
    }
    private void OnDisable() => Close();
    private void OnDestroy() { Close(); if (service != null) service.Changed -= MarkDirty; }
    private Button Button(Transform parent, string label, UnityEngine.Events.UnityAction action, out TMP_Text text)
    {
        var obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button)); obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = new Color(.16f, .20f, .23f, 1f);
        var button = obj.GetComponent<Button>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() =>
        {
            if (!open || !InputFocusStack.HasFocus(this) || !button.interactable || lastActivationFrame == Time.frameCount) return;
            lastActivationFrame = Time.frameCount;
            action();
        });
        text = Text(obj.transform, label, 24); text.raycastTarget = false;
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

internal sealed class AbilityMenuRow : MonoBehaviour
{
    public AbilitySO ability;
    public Button button;
    public TMP_Text description, action;
}
