using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Entree de session (partie) dans la liste des sauvegardes.
public class MainMenuSessionEntryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IMenuCursorHandler
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private GameObject cursor;
    [SerializeField] private Transform savesRoot;
    [SerializeField] private MenuCursorLink cursorLink;

    private MainMenuController owner;
    private bool expanded;
    private bool hovered;
    private RectTransform rectTransform;

    public Transform SavesRoot => savesRoot;
    public bool IsExpanded => expanded;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        ResolveCursor();
        if (cursorLink == null)
        {
            cursorLink = GetComponentInParent<MenuCursorLink>();
        }
        SetCursorVisible(false);
    }

    public void Initialize(MainMenuController menu, string sessionName, bool expandedByDefault = false)
    {
        owner = menu;
        if (titleText != null)
        {
            titleText.text = sessionName;
        }

        ResolveCursor();
        SetCursorVisible(false);
        SetExpanded(expandedByDefault);
    }

    public void SetExpanded(bool value)
    {
        expanded = value;

        if (savesRoot != null)
        {
            savesRoot.gameObject.SetActive(expanded);
        }
    }

    public void Toggle()
    {
        SetExpanded(!expanded);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHovered(true);
        SyncSharedCursor();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHovered(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        HandleSubmit();
    }

    public void OnCursorFocus()
    {
        SetHovered(true);
    }

    public void OnCursorBlur()
    {
        SetHovered(false);
    }

    public void OnCursorSubmit()
    {
        HandleSubmit();
    }

    private void SetHovered(bool value)
    {
        hovered = value;
        SetCursorVisible(value);
        if (owner != null)
        {
            if (value)
            {
                owner.OnSessionHovered(this);
            }
            else
            {
                owner.OnSessionUnhovered(this);
            }
        }
    }

    private void HandleSubmit()
    {
        if (owner != null)
        {
            owner.OnSessionInteract(this);
        }
        else
        {
            Toggle();
        }
    }

    private void OnDisable()
    {
        if (hovered && owner != null)
        {
            owner.OnSessionUnhovered(this);
        }

        hovered = false;
        SetCursorVisible(false);
    }

    private void ResolveCursor()
    {
        if (cursor != null)
        {
            return;
        }

        Transform found = transform.Find("Cursor");
        if (found != null)
        {
            cursor = found.gameObject;
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

    private void SetCursorVisible(bool visible)
    {
        if (cursor != null)
        {
            cursor.SetActive(visible);
        }
    }
}
