using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Action de menu pour choisir le mode d'entree actif.
public class MainMenuInputModeAction : MonoBehaviour, IMenuCursorHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private MainMenuInputSettings.InputMode targetMode = MainMenuInputSettings.InputMode.KeyboardMouse;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private string automaticLabel = "Automatique";
    [SerializeField] private string keyboardMouseLabel = "Clavier / souris";
    [SerializeField] private string gamepadLabel = "Gamepad";
    [SerializeField] private string activeSuffix = " [actif]";
    [SerializeField] private bool syncCursorOnHover = true;
    [SerializeField] private MenuCursorLink cursorLink;

    private static readonly List<MainMenuInputModeAction> activeActions = new List<MainMenuInputModeAction>();

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
    }

    private void OnEnable()
    {
        if (!activeActions.Contains(this))
        {
            activeActions.Add(this);
        }

        MainMenuInputSettings.ApplySavedModeIfNeeded();
        MainMenuInputSettings.ModeChanged += OnModeChanged;
        RefreshAll();
    }

    private void OnDisable()
    {
        activeActions.Remove(this);
        MainMenuInputSettings.ModeChanged -= OnModeChanged;
    }

    public void Configure(MainMenuInputSettings.InputMode mode, TMP_Text text)
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
        MainMenuInputSettings.SetMode(targetMode);
        RefreshAll();
    }

    private void OnModeChanged(MainMenuInputSettings.InputMode mode)
    {
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (labelText == null)
        {
            return;
        }

        string label = ResolveLabel(targetMode);
        if (MainMenuInputSettings.GetCurrentMode() == targetMode)
        {
            label += activeSuffix;
        }

        labelText.text = label;
    }

    private static void RefreshAll()
    {
        for (int i = 0; i < activeActions.Count; i++)
        {
            MainMenuInputModeAction action = activeActions[i];
            if (action != null)
            {
                action.RefreshLabel();
            }
        }
    }

    private string ResolveLabel(MainMenuInputSettings.InputMode mode)
    {
        switch (mode)
        {
            case MainMenuInputSettings.InputMode.Gamepad:
                return gamepadLabel;
            case MainMenuInputSettings.InputMode.KeyboardMouse:
                return keyboardMouseLabel;
            default:
                return automaticLabel;
        }
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
}
