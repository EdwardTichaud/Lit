using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Action assignable pour un element navigue par un curseur de menu.
public class MenuCursorItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private UnityEvent onFocus;
    [SerializeField] private UnityEvent onBlur;
    [SerializeField] private UnityEvent onSubmit;
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

    public void Focus()
    {
        if (onFocus != null)
        {
            onFocus.Invoke();
        }
    }

    public void Blur()
    {
        if (onBlur != null)
        {
            onBlur.Invoke();
        }
    }

    public void Submit()
    {
        if (onSubmit != null)
        {
            onSubmit.Invoke();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Focus();
        if (syncCursorOnHover)
        {
            SyncCursor();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Blur();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Submit();
    }

    private void SyncCursor()
    {
        if (rectTransform == null)
        {
            return;
        }

        CursorController cursor = cursorLink != null ? cursorLink.Cursor : null;
        if (cursor == null)
        {
            return;
        }

        MenuCursorSyncUtility.SyncCursorToItem(cursor, rectTransform);
    }
}
