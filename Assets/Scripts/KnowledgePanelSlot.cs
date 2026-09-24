using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Une entree de la liste de connaissances. Elle reste passive : le survol affiche
/// uniquement la description dans le panneau parent.
/// </summary>
[DisallowMultipleComponent]
public sealed class KnowledgePanelSlot : MonoBehaviour, IMenuCursorHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("References")]
    [SerializeField] private TMP_Text knowledgeTitleText;
    [SerializeField] private GameObject cursor;

    [Header("Hover")]
    [SerializeField, Min(1f)] private float hoverScale = 1.2f;
    [SerializeField, Min(0f)] private float hoverScaleSpeed = 12f;

    private KnowledgePanelController owner;
    private KnowledgeSO knowledge;
    private RectTransform rectTransform;
    private Vector3 defaultScale;
    private bool isFocused;

    public KnowledgeSO Knowledge => knowledge;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        defaultScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
        ResolveReferences();
    }

    private void OnDisable()
    {
        SetFocused(false);
    }

    private void Update()
    {
        if (rectTransform == null)
        {
            return;
        }

        Vector3 targetScale = isFocused ? defaultScale * hoverScale : defaultScale;
        float blend = 1f - Mathf.Exp(-hoverScaleSpeed * Time.unscaledDeltaTime);
        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, blend);
    }

    public void Initialize(KnowledgePanelController panelOwner, KnowledgeSO knowledgeData)
    {
        owner = panelOwner;
        knowledge = knowledgeData;
        ResolveReferences();

        if (knowledgeTitleText != null)
        {
            knowledgeTitleText.text = !string.IsNullOrWhiteSpace(knowledge?.title) ? knowledge.title : knowledge != null ? knowledge.name : string.Empty;
        }

        SetFocused(false);
    }

    public void ResetSlot()
    {
        SetFocused(false);
        owner = null;
        knowledge = null;
        if (knowledgeTitleText != null)
        {
            knowledgeTitleText.text = string.Empty;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetFocused(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetFocused(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        SetFocused(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SetFocused(false);
    }

    public void OnCursorFocus()
    {
        SetFocused(true);
    }

    public void OnCursorBlur()
    {
        SetFocused(false);
    }

    public void OnCursorSubmit()
    {
        // A knowledge entry is read-only; focusing it is the intended action.
    }

    private void SetFocused(bool focused)
    {
        isFocused = focused;
        if (cursor != null && cursor.activeSelf != focused)
        {
            cursor.SetActive(focused);
        }

        if (rectTransform != null && !focused)
        {
            rectTransform.localScale = defaultScale;
        }

        if (owner == null || knowledge == null)
        {
            return;
        }

        if (focused)
        {
            owner.FocusSlot(this);
        }
        else
        {
            owner.ClearDescription(knowledge);
        }
    }

    private void ResolveReferences()
    {
        if (knowledgeTitleText == null)
        {
            Transform title = transform.Find("KnowledgeTitle");
            knowledgeTitleText = title != null ? title.GetComponent<TMP_Text>() : null;
        }

        if (cursor == null)
        {
            Transform cursorTransform = transform.Find("Cursor");
            cursor = cursorTransform != null ? cursorTransform.gameObject : null;
        }
    }
}
