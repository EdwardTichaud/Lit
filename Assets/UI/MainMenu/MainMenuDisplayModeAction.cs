using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Action de menu pour appliquer un mode fenetre ou plein ecran.
public class MainMenuDisplayModeAction : MonoBehaviour, IMenuCursorHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private const string InfoObjectName = "Affichage_Info";

    public enum DisplayModeOption
    {
        Windowed = 0,
        Fullscreen = 1
    }

    [SerializeField] private DisplayModeOption targetMode;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private string windowedLabel = "Fenetre";
    [SerializeField] private string fullscreenLabel = "Plein ecran";
    [SerializeField] private string activeSuffix = " [actif]";
    [SerializeField] private string currentWindowedMessage = "Mode actuel : fenetre.";
    [SerializeField] private string currentFullscreenMessage = "Mode actuel : plein ecran.";
    [SerializeField] private string switchedToWindowedMessage = "Passage en mode fenetre effectue.";
    [SerializeField] private string switchedToFullscreenMessage = "Passage en plein ecran effectue.";
    [SerializeField] private string alreadyWindowedMessage = "Le mode fenetre est deja actif.";
    [SerializeField] private string alreadyFullscreenMessage = "Le mode plein ecran est deja actif.";
    [SerializeField] private bool syncCursorOnHover = true;
    [SerializeField] private MenuCursorLink cursorLink;

    private static readonly List<MainMenuDisplayModeAction> activeActions = new List<MainMenuDisplayModeAction>();

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        if (cursorLink == null)
        {
            cursorLink = GetComponentInParent<MenuCursorLink>();
        }

        if (labelText == null)
        {
            labelText = GetComponentInChildren<TMP_Text>(true);
        }

        ResolveInfoText();
    }

    private void OnEnable()
    {
        if (!activeActions.Contains(this))
        {
            activeActions.Add(this);
        }

        MainMenuDisplaySettings.ApplySavedModeIfNeeded();
        EnsureInfoTextInitialized();
        RefreshAll();
    }

    private void OnDisable()
    {
        activeActions.Remove(this);
    }

    public void Configure(DisplayModeOption mode, TMP_Text text)
    {
        targetMode = mode;
        labelText = text;
        RefreshLabel();
    }

    public void OnCursorFocus()
    {
        if (syncCursorOnHover)
        {
            SyncSharedCursor();
        }
    }

    public void OnCursorBlur()
    {
    }

    public void OnCursorSubmit()
    {
        Execute();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (syncCursorOnHover)
        {
            SyncSharedCursor();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Execute();
    }

    private void Execute()
    {
        MainMenuDisplaySettings.DisplayMode mode = ToDisplayMode(targetMode);
        bool changed = MainMenuDisplaySettings.SetMode(mode);
        RefreshAll();
        SetInfoTextAll(BuildFeedbackMessage(mode, changed));
    }

    private void RefreshLabel()
    {
        if (labelText == null)
        {
            return;
        }

        MainMenuDisplaySettings.DisplayMode currentMode = MainMenuDisplaySettings.GetCurrentMode();
        MainMenuDisplaySettings.DisplayMode mode = ToDisplayMode(targetMode);
        string label = mode == MainMenuDisplaySettings.DisplayMode.Fullscreen ? fullscreenLabel : windowedLabel;

        if (currentMode == mode)
        {
            label += activeSuffix;
        }

        labelText.text = label;
    }

    private static void RefreshAll()
    {
        for (int i = 0; i < activeActions.Count; i++)
        {
            MainMenuDisplayModeAction action = activeActions[i];
            if (action != null)
            {
                action.RefreshLabel();
            }
        }
    }

    private void EnsureInfoTextInitialized()
    {
        TMP_Text resolvedInfoText = ResolveInfoText();
        if (resolvedInfoText == null || !string.IsNullOrWhiteSpace(resolvedInfoText.text))
        {
            return;
        }

        resolvedInfoText.text = BuildCurrentModeMessage(MainMenuDisplaySettings.GetCurrentMode());
    }

    private static void SetInfoTextAll(string message)
    {
        for (int i = 0; i < activeActions.Count; i++)
        {
            MainMenuDisplayModeAction action = activeActions[i];
            if (action == null)
            {
                continue;
            }

            TMP_Text resolvedInfoText = action.ResolveInfoText();
            if (resolvedInfoText != null)
            {
                resolvedInfoText.text = message;
            }
        }
    }

    private TMP_Text ResolveInfoText()
    {
        if (infoText != null)
        {
            return infoText;
        }

        RectTransform parent = rectTransform != null ? rectTransform.parent as RectTransform : transform.parent as RectTransform;
        if (parent == null)
        {
            return null;
        }

        Transform existing = parent.Find(InfoObjectName);
        if (existing != null)
        {
            infoText = existing.GetComponent<TMP_Text>();
            if (infoText == null)
            {
                infoText = existing.GetComponentInChildren<TMP_Text>(true);
            }

            if (infoText != null)
            {
                ConfigureInfoText(infoText);
            }

            return infoText;
        }

        infoText = CreateInfoText(parent);
        return infoText;
    }

    private TMP_Text CreateInfoText(RectTransform parent)
    {
        GameObject infoObject = new GameObject(InfoObjectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        infoObject.layer = parent.gameObject.layer;
        infoObject.transform.SetParent(parent, false);
        infoObject.transform.SetAsLastSibling();

        RectTransform infoRect = infoObject.GetComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0f, 0f);
        infoRect.anchorMax = new Vector2(0f, 0f);
        infoRect.anchoredPosition = Vector2.zero;
        infoRect.sizeDelta = new Vector2(700f, 80f);
        infoRect.pivot = new Vector2(0.5f, 0.5f);

        TextMeshProUGUI createdText = infoObject.GetComponent<TextMeshProUGUI>();
        ConfigureInfoText(createdText);
        return createdText;
    }

    private void ConfigureInfoText(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        if (labelText != null)
        {
            text.font = labelText.font;
            text.fontSharedMaterial = labelText.fontSharedMaterial;
            text.isRightToLeftText = labelText.isRightToLeftText;
        }

        text.raycastTarget = false;
        text.color = new Color(1f, 1f, 1f, 0.78f);
        text.fontSize = 24f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 18f;
        text.fontSizeMax = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.margin = new Vector4(20f, 0f, 20f, 0f);
    }

    private string BuildFeedbackMessage(MainMenuDisplaySettings.DisplayMode mode, bool changed)
    {
        if (mode == MainMenuDisplaySettings.DisplayMode.Fullscreen)
        {
            return changed ? switchedToFullscreenMessage : alreadyFullscreenMessage;
        }

        return changed ? switchedToWindowedMessage : alreadyWindowedMessage;
    }

    private string BuildCurrentModeMessage(MainMenuDisplaySettings.DisplayMode mode)
    {
        return mode == MainMenuDisplaySettings.DisplayMode.Fullscreen
            ? currentFullscreenMessage
            : currentWindowedMessage;
    }

    private void SyncSharedCursor()
    {
        CursorController sharedCursor = cursorLink != null ? cursorLink.Cursor : null;
        if (sharedCursor == null || rectTransform == null)
        {
            return;
        }

        MenuCursorSyncUtility.SyncCursorToItem(sharedCursor, rectTransform);
    }

    private static MainMenuDisplaySettings.DisplayMode ToDisplayMode(DisplayModeOption mode)
    {
        return mode == DisplayModeOption.Fullscreen
            ? MainMenuDisplaySettings.DisplayMode.Fullscreen
            : MainMenuDisplaySettings.DisplayMode.Windowed;
    }
}
