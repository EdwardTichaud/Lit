using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Button-to-button navigation; the mouse alone owns the torch pointer.
[DefaultExecutionOrder(-100)]
public sealed class MainMenuNavigation : MonoBehaviour
{
    public static bool Active { get; private set; }
    public static bool UsingGamepad { get; private set; }
    public static bool Directional => Active;
    private GameObject selected;
    private readonly Dictionary<Transform, GameObject> remembered = new Dictionary<Transform, GameObject>();
    private float nextMove;
    private Vector2 lastDirection;
    private readonly List<GameObject> targets = new List<GameObject>();
    private MainMenuController menu;
    private readonly Dictionary<CanvasScaler, Vector2> canvasSizes = new Dictionary<CanvasScaler, Vector2>();
    private float nextScaleCheck;
    private EventSystem navigationSystem;
    private bool previousNavigationEvents;
    private GameObject focusHighlight;
    private GameObject highlightedTarget;
    private bool showSelectionHighlight;
    private int suppressSubmitFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() { Active = false; UsingGamepad = false; }
    private void OnDisable() { Active = false; ClearSelection(); RestoreNavigationEvents(); }
    private void RestoreNavigationEvents()
    {
        if (navigationSystem != null) navigationSystem.sendNavigationEvents = previousNavigationEvents;
        navigationSystem = null;
    }

    public void Focus(GameObject target)
    {
        if (!Usable(target)) return;
        showSelectionHighlight = true;
        // A physical press that closes the keyboard must not submit the form too.
        suppressSubmitFrame = Time.frameCount;
        CollectTargets(PrivateSessionService.Instance);
        Select(target);
    }

    private void Update()
    {
        PrivateSessionService session = PrivateSessionService.Instance;
        Active = SceneManager.GetActiveScene().name == MainMenuController.DefaultMenuSceneName ||
            (session != null && (session.IsBusy || session.Phase == PrivateSessionPhase.Lobby));
        if (!Active)
        {
            RestoreNavigationEvents();
            return;
        }
        if (EventSystem.current != null && navigationSystem != EventSystem.current)
        {
            RestoreNavigationEvents();
            navigationSystem = EventSystem.current;
            previousNavigationEvents = navigationSystem.sendNavigationEvents;
            navigationSystem.sendNavigationEvents = false;
        }
        if (menu == null) menu = FindAnyObjectByType<MainMenuController>();
        if (Time.unscaledTime >= nextScaleCheck)
        {
            nextScaleCheck = Time.unscaledTime + .5f;
            foreach (CanvasScaler scaler in FindObjectsByType<CanvasScaler>(FindObjectsSortMode.None))
            {
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
                if (scaler.gameObject.scene.name != MainMenuController.DefaultMenuSceneName && scaler.GetComponentInParent<PrivateSessionPanel>() == null) continue;
                if (!canvasSizes.TryGetValue(scaler, out Vector2 original)) { original = scaler.referenceResolution; canvasSizes[scaler] = original; }
                scaler.referenceResolution = original / MainMenuPreferences.UiScale;
            }
        }
        Keyboard keyboard = MainMenuInputSettings.AllowsKeyboardMouse() ? Keyboard.current : null;
        Mouse mouse = MainMenuInputSettings.AllowsKeyboardMouse() ? Mouse.current : null;
        Gamepad pad = MainMenuInputSettings.AllowsGamepad() ? Gamepad.current : null;
        if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > .2f || pad.dpad.ReadValue().sqrMagnitude > .2f ||
            pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame ||
            pad.buttonWest.wasPressedThisFrame || pad.startButton.wasPressedThisFrame))
        { UsingGamepad = true; showSelectionHighlight = true; }
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) { UsingGamepad = false; showSelectionHighlight = true; }
        if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.leftButton.wasPressedThisFrame))
        { UsingGamepad = false; showSelectionHighlight = false; }
        if (UsingGamepad) Cursor.visible = false;
        if (Gamepad.current == null && Keyboard.current != null && !MainMenuInputSettings.AllowsKeyboardMouse())
            MainMenuInputSettings.SetMode(MainMenuInputSettings.InputMode.Automatic);
        if (menu != null && menu.IsTitleActive && !(session?.IsActive ?? false)) return;
        // ConfirmationManager already owns navigation and cancel on its modal.
        if (ConfirmationManager.IsVisible) return;
        bool back = (keyboard?.escapeKey.wasPressedThisFrame ?? false) || (pad?.buttonEast.wasPressedThisFrame ?? false);
        if (back)
        {
            if (session != null && session.IsActive) session.Leave(); else menu?.UI_Back();
            return;
        }
        if (pad != null && pad.buttonNorth.wasPressedThisFrame && !(session?.IsActive ?? false)) menu?.UI_OpenKeyboard();
        if (!Directional) return;
        Vector2 direction = pad != null && UsingGamepad ? pad.dpad.ReadValue() : Vector2.zero;
        if (pad != null && UsingGamepad && direction.sqrMagnitude < .1f) direction = pad.leftStick.ReadValue();
        if (keyboard != null)
        {
            direction += new Vector2((keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.leftArrowKey.isPressed ? 1 : 0),
                (keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.downArrowKey.isPressed ? 1 : 0));
        }
        bool tab = keyboard?.tabKey.wasPressedThisFrame ?? false;
        bool submit = (keyboard?.enterKey.wasPressedThisFrame ?? false) || (pad?.buttonSouth.wasPressedThisFrame ?? false);
        TMP_InputField editing = EventSystem.current?.currentSelectedGameObject?.GetComponent<TMP_InputField>();
        if (editing != null && editing.isFocused && !UsingGamepad && !tab)
        {
            if (submit) { editing.DeactivateInputField(); SelectNext(Vector2.down, true); }
            return;
        }
        if (tab) direction = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ? Vector2.up : Vector2.down;
        if (direction.sqrMagnitude < .2f) { lastDirection = Vector2.zero; nextMove = 0; }
        bool move = direction.sqrMagnitude >= .2f && (Time.unscaledTime >= nextMove || Vector2.Dot(direction, lastDirection) <= 0);
        if (move || submit || selected == null || !Usable(selected) || (menu != null && menu.NavigationModalRoot != null && !selected.transform.IsChildOf(menu.NavigationModalRoot)))
        {
            CollectTargets(session);
            if (selected == null || !targets.Contains(selected))
            {
                GameObject fallback = targets.FirstOrDefault();
                if (fallback != null && remembered.TryGetValue(fallback.transform.parent, out GameObject previous) && targets.Contains(previous)) fallback = previous;
                Select(fallback);
            }
        }
        if (move) { SelectNext(direction, tab); nextMove = Time.unscaledTime + (lastDirection == Vector2.zero ? .32f : .13f); lastDirection = direction; }
        if (submit && Time.frameCount != suppressSubmitFrame && selected != null && Usable(selected))
        {
            IMenuCursorHandler handler = selected.GetComponents<MonoBehaviour>().OfType<IMenuCursorHandler>().FirstOrDefault();
            if (handler != null) handler.OnCursorSubmit();
            else if (selected.TryGetComponent(out Button button)) button.onClick.Invoke();
            else if (selected.TryGetComponent(out TMP_InputField field))
            { field.ActivateInputField(); if (UsingGamepad) menu?.UI_OpenKeyboard(); }
        }
    }
    private void CollectTargets(PrivateSessionService session)
    {
        targets.Clear();
        bool privatePanel = session != null && (session.IsBusy || session.Phase == PrivateSessionPhase.Lobby);
        foreach (MonoBehaviour component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (!(component is IMenuCursorHandler) && !(component is Button) && !(component is TMP_InputField)) continue;
            GameObject go = component.gameObject;
            if (privatePanel != (go.GetComponentInParent<PrivateSessionPanel>() != null)) continue;
            if (!privatePanel && menu != null && menu.NavigationModalRoot != null && !go.transform.IsChildOf(menu.NavigationModalRoot)) continue;
            if (Usable(go) && !targets.Contains(go)) targets.Add(go);
        }
        GameObject[] candidates = targets.ToArray();
        targets.RemoveAll(go => candidates.Any(parent => parent != go && go.transform.IsChildOf(parent.transform)));
        targets.Sort((a, b) => { Vector2 pa = Position(a), pb = Position(b); return Mathf.Abs(pa.y - pb.y) > 10 ? pb.y.CompareTo(pa.y) : pa.x.CompareTo(pb.x); });
    }
    private static bool Usable(GameObject go)
    {
        if (go == null || !go.activeInHierarchy) return false;
        if (go.TryGetComponent(out Selectable selectable) && !selectable.IsInteractable()) return false;
        foreach (CanvasGroup group in go.GetComponentsInParent<CanvasGroup>())
        { if (!group.interactable || !group.blocksRaycasts || group.alpha < .01f) return false; if (group.ignoreParentGroups) break; }
        return true;
    }
    private static Vector2 Position(GameObject go)
    {
        RectTransform rect = go.transform as RectTransform;
        Canvas canvas = go.GetComponentInParent<Canvas>();
        return RectTransformUtility.WorldToScreenPoint(canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null, rect != null ? rect.TransformPoint(rect.rect.center) : go.transform.position);
    }
    private void SelectNext(Vector2 direction, bool sequential)
    {
        if (targets.Count == 0) return;
        if (sequential) { Select(targets[(targets.IndexOf(selected) + (direction.y > 0 ? targets.Count - 1 : 1)) % targets.Count]); return; }
        if (selected == null) { Select(targets[0]); return; }
        Vector2 origin = Position(selected);
        GameObject best = null; float score = float.MaxValue;
        foreach (GameObject target in targets)
        {
            if (target == selected) continue;
            Vector2 delta = Position(target) - origin;
            float along = Vector2.Dot(delta, direction.normalized);
            if (along < 1) continue;
            float cost = delta.sqrMagnitude / along;
            if (cost < score) { score = cost; best = target; }
        }
        if (best != null) Select(best);
    }
    private void Select(GameObject go)
    {
        if (go == selected) return;
        ClearSelection(); selected = go;
        if (go == null) return;
        remembered[go.transform.parent] = go;
        EventSystem.current?.SetSelectedGameObject(go);
        go.GetComponents<MonoBehaviour>().OfType<IMenuCursorHandler>().FirstOrDefault()?.OnCursorFocus();
        ScrollRect scroll = go.GetComponentInParent<ScrollRect>();
        if (scroll != null && scroll.content != null && scroll.viewport != null)
        {
            Canvas.ForceUpdateCanvases();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, go.transform);
            float shift = bounds.min.y < scroll.viewport.rect.yMin ? scroll.viewport.rect.yMin - bounds.min.y :
                bounds.max.y > scroll.viewport.rect.yMax ? scroll.viewport.rect.yMax - bounds.max.y : 0;
            scroll.content.anchoredPosition += new Vector2(0, shift); scroll.StopMovement();
        }
    }
    private void LateUpdate()
    {
        GameObject target = selected;
        if (ConfirmationManager.IsVisible) target = ConfirmationManager.CurrentSelection;
        SetHighlight(Active && (UsingGamepad || showSelectionHighlight) && target != null && target.activeInHierarchy ? target : null);
    }

    private void SetHighlight(GameObject target)
    {
        if (target == highlightedTarget && (target == null || focusHighlight != null)) return;
        if (focusHighlight != null) { focusHighlight.SetActive(false); Destroy(focusHighlight); }
        highlightedTarget = target;
        if (target == null || !(target.transform is RectTransform)) return;
        // A separate UGUI rectangle also highlights TMP text buttons; TMP does not
        // render a standard UI Outline mesh effect applied to the text itself.
        focusHighlight = new GameObject("SelectionHighlight", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(LayoutElement), typeof(UnityEngine.UI.Outline));
        focusHighlight.transform.SetParent(target.transform, false);
        focusHighlight.transform.SetAsFirstSibling();
        RectTransform rect = (RectTransform)focusHighlight.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-4, -4); rect.offsetMax = new Vector2(4, 4);
        focusHighlight.GetComponent<LayoutElement>().ignoreLayout = true;
        Image fill = focusHighlight.GetComponent<Image>();
        fill.color = new Color(1f, .72f, .22f, .16f); fill.raycastTarget = false;
        UnityEngine.UI.Outline border = focusHighlight.GetComponent<UnityEngine.UI.Outline>();
        border.effectColor = new Color(1f, .72f, .22f, 1f);
        border.effectDistance = new Vector2(3, 3); border.useGraphicAlpha = false;
    }

    private void ClearSelection()
    {
        if (selected != null) selected.GetComponents<MonoBehaviour>().OfType<IMenuCursorHandler>().FirstOrDefault()?.OnCursorBlur();
        SetHighlight(null);
        selected = null;
    }
}
