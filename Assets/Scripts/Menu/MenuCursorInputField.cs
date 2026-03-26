using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuCursorInputField : MonoBehaviour, IMenuCursorHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private bool focusOnHover = true;
    [SerializeField] private bool focusOnSubmit = true;
    [SerializeField] private bool blurOnExit = false;
    [SerializeField] private bool syncCursorOnHover = true;
    [SerializeField] private MenuCursorLink cursorLink;

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        if (inputField == null)
        {
            inputField = GetComponentInChildren<TMP_InputField>(true);
        }
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

        if (focusOnHover)
        {
            FocusField();
        }
    }

    public void OnCursorBlur()
    {
        if (blurOnExit)
        {
            BlurField();
        }
    }

    public void OnCursorSubmit()
    {
        if (focusOnSubmit)
        {
            FocusField();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (syncCursorOnHover)
        {
            SyncSharedCursor();
        }

        if (focusOnHover)
        {
            FocusField();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (blurOnExit)
        {
            BlurField();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (focusOnHover || focusOnSubmit)
        {
            FocusField();
        }
    }

    private void FocusField()
    {
        if (inputField == null || !inputField.interactable)
        {
            return;
        }

        inputField.Select();
        inputField.ActivateInputField();
        MoveCaretToEnd();
    }

    private void BlurField()
    {
        if (inputField == null)
        {
            return;
        }

        inputField.DeactivateInputField();
    }

    private void MoveCaretToEnd()
    {
        if (inputField == null)
        {
            return;
        }

        string text = inputField.text ?? string.Empty;
        int caret = text.Length;
        inputField.caretPosition = caret;
        inputField.selectionAnchorPosition = caret;
        inputField.selectionFocusPosition = caret;
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
