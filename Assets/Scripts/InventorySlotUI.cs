using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventorySlotUI : MonoBehaviour, IMenuCursorHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image itemSpriteImage;

    public InventoryPanelController Owner { get; private set; }
    public Item Item { get; private set; }
    public int Quantity { get; private set; }
    public bool HasCombatDefenseHitPoints { get; private set; }
    public int CombatDefenseHitPoints { get; private set; }
    public int CombatDefenseMaxHitPoints { get; private set; }
    public RectTransform SlotRect { get; private set; }
    public string SelectionKey => Item != null ? $"{Item.GetInstanceID()}:{(HasCombatDefenseHitPoints ? CombatDefenseHitPoints : -1)}" : string.Empty;

    public void Initialize(InventoryPanelController owner, Item item, int quantity)
    {
        Initialize(owner, item, quantity, false, 0, 0);
    }

    public void Initialize(
        InventoryPanelController owner,
        Item item,
        int quantity,
        bool hasCombatDefenseHitPoints,
        int combatDefenseHitPoints,
        int combatDefenseMaxHitPoints)
    {
        Owner = owner;
        Item = item;
        Quantity = Mathf.Max(0, quantity);
        HasCombatDefenseHitPoints = hasCombatDefenseHitPoints;
        CombatDefenseHitPoints = Mathf.Max(0, combatDefenseHitPoints);
        CombatDefenseMaxHitPoints = Mathf.Max(0, combatDefenseMaxHitPoints);
        SlotRect = GetComponent<RectTransform>();
        ResolveReferences();

        if (quantityText != null)
        {
            quantityText.text = hasCombatDefenseHitPoints
                ? $"{Quantity}\nPV {CombatDefenseHitPoints}/{CombatDefenseMaxHitPoints}"
                : Quantity.ToString();
        }

        if (itemSpriteImage != null)
        {
            itemSpriteImage.sprite = Item != null ? Item.itemSprite : null;
            itemSpriteImage.enabled = itemSpriteImage.sprite != null;
        }
    }

    public void ResetForPool()
    {
        Owner = null;
        Item = null;
        Quantity = 0;
        HasCombatDefenseHitPoints = false;
        CombatDefenseHitPoints = 0;
        CombatDefenseMaxHitPoints = 0;
        if (quantityText != null) quantityText.text = string.Empty;
        if (itemSpriteImage != null)
        {
            itemSpriteImage.sprite = null;
            itemSpriteImage.enabled = false;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Owner != null) Owner.FocusSlot(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (Owner != null) Owner.ClearSlotHighlight(this);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (Owner != null) Owner.FocusSlot(this);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (Owner != null) Owner.ClearSlotHighlight(this);
    }

    public void OnCursorFocus()
    {
        if (Owner != null) Owner.FocusSlot(this);
    }

    public void OnCursorBlur()
    {
        if (Owner != null) Owner.ClearSlotHighlight(this);
    }

    public void OnCursorSubmit()
    {
        if (Owner != null) Owner.OpenActionsForSlot(this);
    }

    private void ResolveReferences()
    {
        if (quantityText == null)
        {
            Transform quantity = transform.Find("ItemSlot_ItemQuantity");
            quantityText = quantity != null ? quantity.GetComponent<TextMeshProUGUI>() : null;
        }

        if (itemSpriteImage == null)
        {
            Transform sprite = transform.Find("ItemSlot_ItemSprite");
            itemSpriteImage = sprite != null ? sprite.GetComponent<Image>() : null;
        }
    }
}
