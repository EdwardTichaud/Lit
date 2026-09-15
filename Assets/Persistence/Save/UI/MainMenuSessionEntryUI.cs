using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Entree de session (partie) dans la liste des sauvegardes.
public class MainMenuSessionEntryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IMenuCursorHandler
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private GameObject cursor;
    [SerializeField] private MenuCursorLink cursorLink;

    private MainMenuController owner;
    private SaveSessionInfo session;
    private bool hovered;
    private RectTransform rectTransform;
    private bool useLocalCursor = true;
    private bool selected;

    public SaveSessionInfo Session => session;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        ResolveCursor();
        if (cursorLink == null)
        {
            cursorLink = GetComponentInParent<MenuCursorLink>();
        }
        useLocalCursor = cursorLink == null;
        SetCursorVisible(false);
    }

    public void Initialize(MainMenuController menu, string sessionName, bool expandedByDefault = false)
    {
        Initialize(menu, new SaveSessionInfo { sessionName = sessionName }, expandedByDefault);
    }

    public void Initialize(MainMenuController menu, SaveSessionInfo sessionData, bool selectedByDefault = false)
    {
        owner = menu;
        session = sessionData;

        SetSelected(selectedByDefault);

        ResolveCursor();
        if (cursorLink == null)
        {
            cursorLink = GetComponentInParent<MenuCursorLink>();
        }
        useLocalCursor = cursorLink == null;
        SetCursorVisible(false);
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
        if (eventData.button != PointerEventData.InputButton.Left) return;
        HandleSubmit();
    }

    public void SetSelected(bool value)
    {
        // Keep the session selection state in sync with the save browser.
        selected = value;
        if (titleText == null) return;
        string label = string.IsNullOrWhiteSpace(session?.sessionName) ? "Session" : session.sessionName;
        int count = session?.saves?.Count ?? 0;
        titleText.text = (selected ? "› " : "") + label + "  <size=70%>(" + count + ")</size>";
        titleText.color = selected ? new Color(1f, .78f, .4f, 1f) : Color.white;
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
        MainMenuFrameHighlight.SetFocused(this, value);
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
    }

    private void OnDisable()
    {
        MainMenuFrameHighlight.SetFocused(this, false);
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
        FindAnyObjectByType<MainMenuNavigation>()?.Focus(gameObject);
        CursorController sharedCursor = cursorLink != null ? cursorLink.Cursor : null;
        if (sharedCursor == null || rectTransform == null)
        {
            return;
        }

        MenuCursorSyncUtility.SyncCursorToItem(sharedCursor, rectTransform);
    }

    private void SetCursorVisible(bool visible)
    {
        if (!useLocalCursor)
        {
            if (cursor != null)
            {
                cursor.SetActive(false);
            }
            return;
        }

        if (cursor != null)
        {
            cursor.SetActive(visible);
        }
    }
}
