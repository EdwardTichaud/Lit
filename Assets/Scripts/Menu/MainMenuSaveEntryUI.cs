using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Entree de sauvegarde dans la liste.
public class MainMenuSaveEntryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IMenuCursorHandler
{
    [SerializeField] private TMP_Text sessionNameText;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private MenuCursorLink cursorLink;

    private MainMenuController owner;
    private SaveSlotInfo save;
    private Color normalColor;
    private Color hoverColor;
    private Color selectedColor;
    private bool isSelected;
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        ResolveTextFields();
        if (cursorLink == null)
        {
            cursorLink = GetComponentInParent<MenuCursorLink>();
        }
    }

    public void Initialize(MainMenuController menu, SaveSlotInfo data, Color normal, Color hover, Color selected)
    {
        owner = menu;
        save = data;
        normalColor = normal;
        hoverColor = hover;
        selectedColor = selected;

        ApplySaveData();
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateColor(isSelected ? selectedColor : normalColor);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        HandleFocus();
        SyncSharedCursor();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HandleBlur();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        HandleSubmit();
    }

    public void OnCursorFocus()
    {
        HandleFocus();
    }

    public void OnCursorBlur()
    {
        HandleBlur();
    }

    public void OnCursorSubmit()
    {
        HandleSubmit();
    }

    private void HandleFocus()
    {
        if (owner != null && save != null)
        {
            owner.OnSaveHovered(save);
        }

        if (!isSelected)
        {
            UpdateColor(hoverColor);
        }
    }

    private void HandleBlur()
    {
        if (!isSelected)
        {
            UpdateColor(normalColor);
        }
    }

    private void HandleSubmit()
    {
        if (owner != null && save != null)
        {
            owner.OnSaveSelected(save, this, true);
        }
    }

    private void ApplySaveData()
    {
        ResolveTextFields();
        if (save == null)
        {
            return;
        }

        string sessionName = string.IsNullOrWhiteSpace(save.sessionName) ? "Session" : save.sessionName;
        string dateLabel = FormatDate(save.savedAtUtcTicks);

        if (sessionNameText != null)
        {
            sessionNameText.text = sessionName;
        }

        if (dateText != null)
        {
            dateText.text = dateLabel;
        }
        else if (sessionNameText != null)
        {
            sessionNameText.text = $"{sessionName} - {dateLabel}";
        }
    }

    private static string FormatDate(long utcTicks)
    {
        if (utcTicks <= 0)
        {
            return "Date inconnue";
        }

        DateTime savedAt = new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime();
        return savedAt.ToString("dd/MM/yyyy HH:mm");
    }

    private void ResolveTextFields()
    {
        if (sessionNameText == null)
        {
            Transform found = transform.Find("SessionName");
            if (found != null)
            {
                sessionNameText = found.GetComponent<TMP_Text>();
            }
        }

        if (dateText == null)
        {
            Transform found = transform.Find("Date");
            if (found != null)
            {
                dateText = found.GetComponent<TMP_Text>();
            }
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

    private void UpdateColor(Color color)
    {
        if (sessionNameText != null)
        {
            sessionNameText.color = color;
        }

        if (dateText != null)
        {
            dateText.color = color;
        }
    }
}
