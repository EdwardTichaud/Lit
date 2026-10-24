using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LootSlotUI : MonoBehaviour, IMenuCursorHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image itemSpriteImage;
    [SerializeField] private GameObject cursor;
    public InteractableItem Owner { get; private set; }
    public InteractableItem.LootItemEntry Entry { get; private set; }
    public Item Item { get; private set; }
    public int Quantity { get; private set; }
    public RectTransform SlotRect { get; private set; }
    public string SelectionKey => Entry != null ? Entry.GetHashCode().ToString() : string.Empty;

    public void Initialize(InteractableItem owner, InteractableItem.LootItemEntry entry)
    {
        Owner = owner;
        Entry = entry;
        Item = entry != null ? entry.item : null;
        Quantity = entry != null ? Mathf.Max(0, entry.quantity) : 0;
        SlotRect = GetComponent<RectTransform>();
        ResolveReferences();
        if (quantityText != null) quantityText.text = Quantity.ToString();
        if (itemSpriteImage != null)
        {
            itemSpriteImage.sprite = Item != null ? Item.itemSprite : null;
            itemSpriteImage.enabled = itemSpriteImage.sprite != null;
        }
        SetFocused(false);
    }

    public void ResetForPool()
    {
        SetFocused(false);
        Owner = null;
        Entry = null;
        Item = null;
        Quantity = 0;
        if (quantityText != null) quantityText.text = string.Empty;
        if (itemSpriteImage != null) { itemSpriteImage.sprite = null; itemSpriteImage.enabled = false; }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Owner != null) Owner.FocusSlot(this);
    }

    public void OnPointerExit(PointerEventData eventData) => SetFocused(false);

    public void OnSelect(BaseEventData eventData)
    {
        if (Owner != null) Owner.FocusSlot(this);
    }

    public void OnDeselect(BaseEventData eventData) => SetFocused(false);
    public void OnCursorFocus() { if (Owner != null) Owner.FocusSlot(this); }
    public void OnCursorBlur() => SetFocused(false);
    public void OnCursorSubmit() { }

    public void SetFocused(bool focused)
    {
        if (cursor != null && cursor.activeSelf != focused) cursor.SetActive(focused);
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
        if (cursor == null)
        {
            Transform cursorTransform = transform.Find("Cursor");
            cursor = cursorTransform != null ? cursorTransform.gameObject : null;
        }
    }
}
