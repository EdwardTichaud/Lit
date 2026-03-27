using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Action simple pour le panel Pause (sauvegarde / quit / resume).
public class PauseCursorAction : MonoBehaviour, IMenuCursorHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public enum PauseAction
    {
        None = 0,
        Quit = 5,
        AudioOptions = 9,
        Save = 14,
        Resume = 16,
        AudioOptionsBack = 17
    }

    [SerializeField] private PausePanelController controller;
    [SerializeField] private PauseAction action;
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

    public void Configure(PausePanelController pauseController, PauseAction pauseAction)
    {
        controller = pauseController;
        action = pauseAction;
    }

    private void Execute()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (!EnsurePauseController())
        {
            return;
        }

        switch (action)
        {
            case PauseAction.AudioOptions:
                controller.UI_OpenAudioOptions();
                break;
            case PauseAction.AudioOptionsBack:
                controller.UI_CloseAudioOptions();
                break;
            case PauseAction.Save:
                controller.UI_Save();
                break;
            case PauseAction.Quit:
                controller.UI_Quit();
                break;
            case PauseAction.Resume:
                controller.ClosePanel();
                break;
        }
    }

    private bool EnsurePauseController()
    {
        if (controller != null)
        {
            return true;
        }

        controller = GetComponentInParent<PausePanelController>(true);
        if (controller == null)
        {
            controller = FindObjectOfType<PausePanelController>(true);
        }

        return controller != null;
    }

    private void SyncSharedCursor()
    {
        CursorController sharedCursor = cursorLink != null ? cursorLink.Cursor : controller != null ? controller.cursorController : null;
        if (sharedCursor == null || rectTransform == null)
        {
            return;
        }

        MenuCursorSyncUtility.SyncCursorToItem(sharedCursor, rectTransform);
    }
}
