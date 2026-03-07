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
        Refresh = 9
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

    private void Execute()
    {
        if (controller == null)
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
        }
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
