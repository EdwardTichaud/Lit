using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(2000)]
public class UIManager : MonoBehaviour
{
    private const string DefaultOverlayName = "UI_Overlay";

    private readonly struct FocusEntry
    {
        public FocusEntry(object owner, InputMode mode) { Owner = owner; Mode = mode; }
        public object Owner { get; }
        public InputMode Mode { get; }
    }

    public static UIManager Instance { get; private set; }
    private static readonly List<FocusEntry> FocusStack = new List<FocusEntry>();
    private static readonly Dictionary<CanvasGroup, TransitionEntry> Transitions = new Dictionary<CanvasGroup, TransitionEntry>();

    private sealed class TransitionEntry
    {
        public MonoBehaviour Owner;
        public Coroutine Routine;
    }

    [Header("Panel Registry")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private List<UiPanel> panels = new List<UiPanel>();
    [Header("Legacy Startup Compatibility")]
    [SerializeField] private List<RectTransform> visibleAtStartupPanels = new List<RectTransform>();
    [SerializeField] private bool applyLegacyStartupVisibility = true;

    private void Awake()
    {
        if (Instance != null && Instance != this) return;
        Instance = this;
        CollectPanels();
    }

    private void Start() => ApplyStartupVisibility();

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void RegisterPanel(UiPanel panel)
    {
        if (panel == null || Instance == null || Instance.panels.Contains(panel)) return;
        Instance.panels.Add(panel);
    }

    public static void UnregisterPanel(UiPanel panel)
    {
        if (Instance != null && panel != null) Instance.panels.Remove(panel);
    }

    public bool ShowPanel(string panelId, bool immediate = false)
    {
        UiPanel panel = FindPanel(panelId);
        if (panel == null) return false;
        panel.Show(immediate);
        return true;
    }

    public bool HidePanel(string panelId, bool immediate = false)
    {
        UiPanel panel = FindPanel(panelId);
        if (panel == null) return false;
        panel.Hide(immediate);
        return true;
    }

    public void ApplyStartupVisibility()
    {
        CollectPanels();
        for (int i = panels.Count - 1; i >= 0; i--)
        {
            UiPanel panel = panels[i];
            if (panel == null)
            {
                panels.RemoveAt(i);
                continue;
            }
            panel.ApplyStartupState();
        }

        if (!applyLegacyStartupVisibility) return;
        Transform root = ResolveUiRoot();
        if (root == null) return;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.GetComponent<UiPanel>() != null || ContainsStartupPanel(child) || IsLegacySelfManaged(child)) continue;
            SetLegacyPanelVisible(child, false, false);
        }
        for (int i = 0; i < visibleAtStartupPanels.Count; i++) SetPanelVisible(visibleAtStartupPanels[i], true);
    }

    public void SetPanelVisible(Transform panel, bool visible)
    {
        if (panel == null) return;
        UiPanel managedPanel = panel.GetComponent<UiPanel>();
        if (managedPanel != null)
        {
            managedPanel.SetVisible(visible, true);
            return;
        }
        SetLegacyPanelVisible(panel, visible, visible);
    }

    public static void ConfigureDecorativeCursor(RectTransform cursor, bool disableNavigationController)
    {
        if (cursor == null) return;
        if (disableNavigationController)
        {
            CursorController controller = cursor.GetComponent<CursorController>();
            if (controller != null) controller.enabled = false;
        }
        Graphic[] graphics = cursor.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++) if (graphics[i] != null) graphics[i].raycastTarget = false;
    }

    public static void TransitionCanvasGroup(
        MonoBehaviour owner,
        CanvasGroup canvasGroup,
        bool visible,
        float duration,
        bool deactivateWhenHidden = false)
    {
        if (owner == null || canvasGroup == null) return;
        if (visible && !canvasGroup.gameObject.activeSelf) canvasGroup.gameObject.SetActive(true);

        if (Transitions.TryGetValue(canvasGroup, out TransitionEntry previous) && previous.Owner != null && previous.Routine != null)
        {
            previous.Owner.StopCoroutine(previous.Routine);
        }

        if (duration <= 0f || !owner.isActiveAndEnabled || !canvasGroup.gameObject.activeInHierarchy)
        {
            ApplyCanvasGroupState(canvasGroup, visible ? 1f : 0f);
            if (!visible && deactivateWhenHidden) canvasGroup.gameObject.SetActive(false);
            Transitions.Remove(canvasGroup);
            return;
        }

        TransitionEntry entry = new TransitionEntry { Owner = owner };
        entry.Routine = owner.StartCoroutine(FadeCanvasGroup(entry, canvasGroup, visible ? 1f : 0f, duration, deactivateWhenHidden));
        Transitions[canvasGroup] = entry;
    }

    public static bool HasAnyFocus()
    {
        PurgeDestroyedFocusOwners();
        return FocusStack.Count > 0;
    }

    public static bool HasFocus(object owner)
    {
        PurgeDestroyedFocusOwners();
        return owner != null && FocusStack.Count > 0 && ReferenceEquals(FocusStack[FocusStack.Count - 1].Owner, owner);
    }

    public static bool HasAnyFocusBlockingCamera()
    {
        PurgeDestroyedFocusOwners();
        if (FocusStack.Count == 0) return false;
        object owner = FocusStack[FocusStack.Count - 1].Owner;
        return !(owner is ICameraInputPassthrough passthrough && passthrough.AllowCameraInput);
    }

    public static void PushFocus(object owner, InputMode mode = InputMode.UserInterface, bool exclusive = false)
    {
        if (owner == null) return;
        PurgeDestroyedFocusOwners();
        if (exclusive)
        {
            for (int i = FocusStack.Count - 1; i >= 0; i--) InputModeCoordinator.Exit(FocusStack[i].Owner);
            FocusStack.Clear();
        }
        else RemoveFocus(owner);
        FocusStack.Add(new FocusEntry(owner, mode));
        InputModeCoordinator.Enter(owner, mode);
    }

    public static void PopFocus(object owner)
    {
        if (owner == null) return;
        RemoveFocus(owner);
        InputModeCoordinator.Exit(owner);
    }

    public static void ClearFocus()
    {
        FocusStack.Clear();
        InputModeCoordinator.Clear();
    }

    private void CollectPanels()
    {
        Transform root = ResolveUiRoot();
        if (root == null) return;
        UiPanel[] foundPanels = root.GetComponentsInChildren<UiPanel>(true);
        for (int i = 0; i < foundPanels.Length; i++) if (foundPanels[i] != null && !panels.Contains(foundPanels[i])) panels.Add(foundPanels[i]);
    }

    private UiPanel FindPanel(string panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId)) return null;
        CollectPanels();
        for (int i = 0; i < panels.Count; i++)
        {
            UiPanel panel = panels[i];
            if (panel != null && string.Equals(panel.PanelId, panelId, System.StringComparison.Ordinal)) return panel;
        }
        return null;
    }

    private Transform ResolveUiRoot()
    {
        if (uiRoot != null) return uiRoot;
        if (string.Equals(name, DefaultOverlayName, System.StringComparison.Ordinal)) return uiRoot = transform;
        GameObject overlay = GameObject.Find(DefaultOverlayName);
        return uiRoot = overlay != null ? overlay.transform : transform;
    }

    private bool ContainsStartupPanel(Transform root)
    {
        for (int i = 0; i < visibleAtStartupPanels.Count; i++)
        {
            RectTransform panel = visibleAtStartupPanels[i];
            if (panel != null && (root == panel || panel.IsChildOf(root))) return true;
        }
        return false;
    }

    private static void SetLegacyPanelVisible(Transform panel, bool visible, bool receiveInput)
    {
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible && receiveInput;
        canvasGroup.blocksRaycasts = visible && receiveInput;
    }

    private static bool IsLegacySelfManaged(Transform panel)
    {
        return panel.GetComponentInChildren<InventoryPanelController>(true) != null ||
               panel.GetComponentInChildren<LootUISettings>(true) != null ||
               panel.GetComponentInChildren<PausePanelController>(true) != null ||
               panel.GetComponentInChildren<DialoguePanelUI>(true) != null ||
               panel.GetComponentInChildren<ConfirmationManager>(true) != null ||
               panel.GetComponentInChildren<QuantityBox>(true) != null ||
               panel.GetComponent<MuninUI>() != null;
    }

    private static void RemoveFocus(object owner)
    {
        for (int i = FocusStack.Count - 1; i >= 0; i--) if (ReferenceEquals(FocusStack[i].Owner, owner)) FocusStack.RemoveAt(i);
    }

    private static void PurgeDestroyedFocusOwners()
    {
        for (int i = FocusStack.Count - 1; i >= 0; i--)
        {
            object owner = FocusStack[i].Owner;
            if (owner is Object unityOwner && unityOwner == null)
            {
                FocusStack.RemoveAt(i);
                InputModeCoordinator.Exit(owner);
            }
        }
    }

    private static IEnumerator FadeCanvasGroup(TransitionEntry entry, CanvasGroup canvasGroup, float targetAlpha, float duration, bool deactivateWhenHidden)
    {
        float startAlpha = canvasGroup.alpha;
        canvasGroup.interactable = targetAlpha > 0f;
        canvasGroup.blocksRaycasts = targetAlpha > 0f;
        float elapsed = 0f;
        while (elapsed < duration && canvasGroup != null)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        if (canvasGroup != null)
        {
            ApplyCanvasGroupState(canvasGroup, targetAlpha);
            if (targetAlpha <= 0f && deactivateWhenHidden) canvasGroup.gameObject.SetActive(false);
            if (Transitions.TryGetValue(canvasGroup, out TransitionEntry current) && ReferenceEquals(current, entry)) Transitions.Remove(canvasGroup);
        }
    }

    private static void ApplyCanvasGroupState(CanvasGroup canvasGroup, float alpha)
    {
        canvasGroup.alpha = alpha;
        bool visible = alpha > 0.001f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        Instance = null;
        FocusStack.Clear();
        Transitions.Clear();
    }
}
