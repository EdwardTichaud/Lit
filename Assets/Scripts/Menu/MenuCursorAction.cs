using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuCursorAction : MonoBehaviour, IMenuCursorHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public enum MenuAction
    {
        None = 0,
        NewGame = 1,
        LoadMenu = 2,
        Multiplayer = 3,
        Options = 4,
        Quit = 5,
        BackToGameOptions = 6,
        LoadSelected = 7,
        DeleteSelected = 8,
        Refresh = 9,
        ConfirmNewGame = 10,
        CancelNewGame = 11,
        ConfirmLoad = 12,
        CancelLoad = 13,
        Save = 14,
        Solo = 15,
        [InspectorName("vk - Input (Nom GO)")] Vk_Input = 99
    }

    [SerializeField] private MainMenuController controller;
    [SerializeField] private MenuAction action;
    [SerializeField] private bool syncCursorOnHover = true;
    [SerializeField] private MenuCursorLink cursorLink;

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        if (cursorLink == null)
        {
            cursorLink = GetComponentInParent<MenuCursorLink>();
        }
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

    public void Configure(MainMenuController menuController, MenuAction menuAction)
    {
        controller = menuController;
        action = menuAction;
    }

    private void Execute()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (TryHandleVirtualKeyboardAction())
        {
            return;
        }

        if (TryHandlePauseAction())
        {
            return;
        }

        if (!EnsureMainMenuController())
        {
            return;
        }

        switch (action)
        {
            case MenuAction.NewGame:
                controller.UI_NewGame();
                break;
            case MenuAction.LoadMenu:
                controller.UI_ShowLoadMenu();
                break;
            case MenuAction.Multiplayer:
                controller.UI_Multiplayer();
                break;
            case MenuAction.Solo:
                controller.UI_Solo();
                break;
            case MenuAction.Options:
                controller.UI_Options();
                break;
            case MenuAction.Quit:
                controller.UI_Quit();
                break;
            case MenuAction.BackToGameOptions:
                controller.UI_ShowGameOptions();
                break;
            case MenuAction.LoadSelected:
                controller.UI_LoadSelected();
                break;
            case MenuAction.DeleteSelected:
                controller.UI_DeleteSelected();
                break;
            case MenuAction.Refresh:
                controller.UI_Refresh();
                break;
            case MenuAction.ConfirmNewGame:
                controller.UI_ConfirmNewGame();
                break;
            case MenuAction.CancelNewGame:
                controller.UI_CancelNewGame();
                break;
            case MenuAction.ConfirmLoad:
                controller.UI_ConfirmLoad();
                break;
            case MenuAction.CancelLoad:
                controller.UI_CancelLoad();
                break;
        }
    }

    private bool TryHandleVirtualKeyboardAction()
    {
        if (action == MenuAction.Vk_Input)
        {
            if (!EnsureMainMenuController())
            {
                return false;
            }

            return HandleVirtualKeyboardInputFromName();
        }

        return false;
    }

    private bool HandleVirtualKeyboardInputFromName()
    {
        string label = NormalizeVirtualKeyLabel(gameObject != null ? gameObject.name : null);
        if (string.IsNullOrEmpty(label))
        {
            return false;
        }

        if (IsValidateLabel(label))
        {
            controller.UI_VirtualValidate();
            return true;
        }

        if (IsBackspaceLabel(label))
        {
            controller.UI_VirtualKey('\b');
            return true;
        }

        if (IsSpaceLabel(label))
        {
            controller.UI_VirtualKey(' ');
            return true;
        }

        if (label.Length == 1)
        {
            controller.UI_VirtualKey(label[0]);
            return true;
        }

        return false;
    }

    private static string NormalizeVirtualKeyLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }

        string trimmed = label.Trim();
        if (trimmed.StartsWith("vk - ", System.StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(5);
        }
        else if (trimmed.StartsWith("vk_", System.StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(3);
        }
        else if (trimmed.StartsWith("key_", System.StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(4);
        }

        return trimmed.Trim();
    }

    private static bool IsSpaceLabel(string label)
    {
        return string.Equals(label, "Space", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "Espace", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "Blank", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBackspaceLabel(string label)
    {
        return string.Equals(label, "Backspace", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "Retour", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "Delete", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "Del", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidateLabel(string label)
    {
        return string.Equals(label, "Validate", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "Valider", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "Enter", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "OK", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "Confirm", System.StringComparison.OrdinalIgnoreCase);
    }

    private bool TryHandlePauseAction()
    {
        if (action != MenuAction.Save && action != MenuAction.Quit)
        {
            return false;
        }

        PausePanelController pausePanel = GetComponentInParent<PausePanelController>(true);
        if (pausePanel == null)
        {
            pausePanel = FindObjectOfType<PausePanelController>(true);
        }

        if (pausePanel == null)
        {
            return false;
        }

        if (action == MenuAction.Save)
        {
            pausePanel.UI_Save();
            return true;
        }

        if (action == MenuAction.Quit)
        {
            pausePanel.UI_Quit();
            return true;
        }

        return false;
    }

    private bool EnsureMainMenuController()
    {
        if (controller != null)
        {
            return true;
        }

        controller = GetComponentInParent<MainMenuController>();
        if (controller == null)
        {
            controller = FindObjectOfType<MainMenuController>(true);
        }

        return controller != null;
    }

    private void SyncSharedCursor()
    {
        CursorController sharedCursor = cursorLink != null ? cursorLink.Cursor : null;
        if (sharedCursor == null || rectTransform == null)
        {
            return;
        }

        RectTransform parent = rectTransform.parent as RectTransform;
        if (parent != null)
        {
            sharedCursor.itemsParent = parent;
            sharedCursor.layoutGroup = parent.GetComponent<LayoutGroup>();
        }

        sharedCursor.Refresh();
        sharedCursor.TrySetCurrentItem(rectTransform, false);
    }
}
