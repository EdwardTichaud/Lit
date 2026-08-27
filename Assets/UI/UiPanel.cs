using System.Collections;
using UnityEngine;

public enum UiPanelLayer { Hud, Screen, Modal, LoadingOverlay, World }
public enum UiPanelInputPolicy { None, UserInterface, Dialogue, Placement }

// Contrat commun pour les panneaux UGUI migrés vers UIManager.
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class UiPanel : MonoBehaviour
{
    [SerializeField] private string panelId;
    [SerializeField] private UiPanelLayer layer = UiPanelLayer.Screen;
    [SerializeField] private UiPanelInputPolicy inputPolicy = UiPanelInputPolicy.None;
    [SerializeField] private bool visibleAtStartup;
    [SerializeField] private bool persistent;
    [SerializeField] private bool deactivateWhenHidden;
    [SerializeField, Min(0f)] private float transitionDuration = 0.15f;
    [SerializeField] private CanvasGroup canvasGroup;

    private Coroutine transition;
    private bool visible;
    private bool focusHeld;

    public string PanelId => string.IsNullOrWhiteSpace(panelId) ? gameObject.name : panelId;
    public UiPanelLayer Layer => layer;
    public UiPanelInputPolicy InputPolicy => inputPolicy;
    public bool VisibleAtStartup => visibleAtStartup;
    public bool Persistent => persistent;
    public bool IsVisible => visible;
    public CanvasGroup CanvasGroup => canvasGroup;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable() => UIManager.RegisterPanel(this);
    private void OnDisable() => ReleaseFocus();
    private void OnDestroy() => UIManager.UnregisterPanel(this);

    public void ApplyStartupState() => SetVisible(visibleAtStartup || persistent, true);
    public void Show(bool immediate = false) => SetVisible(true, immediate);
    public void Hide(bool immediate = false) => SetVisible(false, immediate);

    public void SetVisible(bool nextVisible, bool immediate = false)
    {
        if (canvasGroup == null) return;
        if (nextVisible && !gameObject.activeSelf) gameObject.SetActive(true);

        visible = nextVisible;
        if (nextVisible) AcquireFocus(); else ReleaseFocus();

        if (transition != null)
        {
            StopCoroutine(transition);
            transition = null;
        }

        if (immediate || transitionDuration <= 0f || !isActiveAndEnabled)
        {
            ApplyVisualState(nextVisible ? 1f : 0f);
            if (!nextVisible && deactivateWhenHidden) gameObject.SetActive(false);
            return;
        }

        transition = StartCoroutine(TransitionTo(nextVisible ? 1f : 0f));
    }

    private IEnumerator TransitionTo(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        canvasGroup.interactable = targetAlpha > 0f;
        canvasGroup.blocksRaycasts = targetAlpha > 0f;
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / transitionDuration));
            yield return null;
        }

        ApplyVisualState(targetAlpha);
        transition = null;
        if (targetAlpha <= 0f && deactivateWhenHidden) gameObject.SetActive(false);
    }

    private void ApplyVisualState(float alpha)
    {
        canvasGroup.alpha = alpha;
        bool shown = alpha > 0.001f;
        canvasGroup.interactable = shown;
        canvasGroup.blocksRaycasts = shown;
    }

    private void AcquireFocus()
    {
        if (focusHeld || inputPolicy == UiPanelInputPolicy.None) return;
        focusHeld = true;
        UIManager.PushFocus(this, inputPolicy == UiPanelInputPolicy.Dialogue ? InputMode.Dialogue : inputPolicy == UiPanelInputPolicy.Placement ? InputMode.Placement : InputMode.UserInterface);
    }

    private void ReleaseFocus()
    {
        if (!focusHeld) return;
        focusHeld = false;
        UIManager.PopFocus(this);
    }
}
