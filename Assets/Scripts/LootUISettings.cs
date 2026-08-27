using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Contient les references UI et comportements du panel de loot.
[DisallowMultipleComponent]
public class LootUISettings : MonoBehaviour
{
    public static LootUISettings Instance { get; private set; }

    [Header("Loot UI")]
    [Tooltip("Root du panel de loot.")]
    public GameObject lootPanel;
    [Tooltip("Parent des slots de loot.")]
    public Transform lootItemsParent;
    [Tooltip("Prefab d'un slot de loot.")]
    public GameObject lootItemPrefab;
    [Tooltip("Curseur UI de selection.")]
    public RectTransform slotCursor;
    [Tooltip("Texte de description de l'item selectionne.")]
    public TextMeshProUGUI lootDescriptionText;

    [Header("Container Header")]
    [Tooltip("Texte affichant le nom du container (coffre, cadavre, etc.). Si null, tentative d'auto-detection dans le panel.")]
    public TextMeshProUGUI containerNameText;

    [Tooltip("Image affichant l'icone du container (sprite). Si null, tentative d'auto-detection dans le panel.")]
    public Image containerIconImage;

    [Tooltip("Padding ajoute autour du slot selectionne.")]
    public Vector2 cursorPadding = new Vector2(10f, 10f);
    [Tooltip("Cree un curseur si aucun n'est assigne.")]
    public bool createCursorIfMissing = true;
    [Tooltip("Cache l'icone si aucune image n'est disponible.")]
    public bool hideIconWhenMissing = true;
    [Tooltip("Ferme le loot si le joueur quitte la zone.")]
    public bool closeLootWhenLeaving = true;
    [Tooltip("Permet d'ouvrir/fermer le loot via Interact.")]
    public bool toggleLootOnInteract = true;

    [Header("Loot Navigation")]
    [Tooltip("Deadzone du stick pour naviguer.")]
    public float moveDeadzone = 0.5f;
    [Tooltip("Delai avant repetition de navigation.")]
    public float initialRepeatDelay = 0.35f;
    [Tooltip("Intervalle entre repetitions de navigation.")]
    public float repeatInterval = 0.12f;
    [Tooltip("Autorise le wrap du curseur.")]
    public bool wrapCursor = false;

    [Header("Loot Panel Fade")]
    [Tooltip("Duree du fade d'ouverture/fermeture.")]
    public float lootOpenFadeDuration = 0.5f;
    [Tooltip("Met l'alpha a 0 au demarrage.")]
    public bool setAlphaToZeroOnStart = true;
    [Tooltip("Ajoute un CanvasGroup si manquant.")]
    public bool addCanvasGroupIfMissing = true;
    [Tooltip("Desactive les raycasts quand cache.")]
    public bool disableRaycastsWhenHidden = true;

    private Coroutine fadeRoutine;

    private Image cachedContainerIconImage;
    private TextMeshProUGUI cachedContainerNameText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            return;
        }

        Instance = this;
    }

    public void InitializePanel()
    {
        if (lootPanel == null)
        {
            return;
        }

        CacheContainerHeaderRefsIfNeeded();
        ConfigureSlotCursor();

        CanvasGroup lootCanvasGroup = GetLootCanvasGroup();
        if (lootCanvasGroup != null && setAlphaToZeroOnStart)
        {
            lootCanvasGroup.alpha = 0f;
            if (disableRaycastsWhenHidden)
            {
                lootCanvasGroup.interactable = false;
                lootCanvasGroup.blocksRaycasts = false;
            }
        }
    }

    public void OpenPanel()
    {
        if (lootPanel == null)
        {
            return;
        }

        lootPanel.SetActive(true);
        CacheContainerHeaderRefsIfNeeded();
        ConfigureSlotCursor();

        CanvasGroup lootCanvasGroup = GetLootCanvasGroup();
        if (lootCanvasGroup != null)
        {
            lootCanvasGroup.alpha = 0f;
            if (disableRaycastsWhenHidden)
            {
                lootCanvasGroup.interactable = false;
                lootCanvasGroup.blocksRaycasts = false;
            }
        }

        FadePanelTo(1f, lootOpenFadeDuration);
    }

    public void ClosePanel()
    {
        if (lootPanel == null)
        {
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        CanvasGroup lootCanvasGroup = GetLootCanvasGroup();
        if (lootCanvasGroup != null)
        {
            lootCanvasGroup.alpha = 0f;
            if (disableRaycastsWhenHidden)
            {
                lootCanvasGroup.interactable = false;
                lootCanvasGroup.blocksRaycasts = false;
            }
        }

        lootPanel.SetActive(false);
    }

    public void UpdateDescription(Item item)
    {
        if (lootDescriptionText == null)
        {
            return;
        }

        string description = string.Empty;
        if (item != null)
        {
            description = item.description;
            if (string.IsNullOrWhiteSpace(description))
            {
                description = !string.IsNullOrWhiteSpace(item.itemName) ? item.itemName : item.name;
            }
        }

        lootDescriptionText.text = description;
        lootDescriptionText.gameObject.SetActive(!string.IsNullOrEmpty(description));
    }

    public void HideCursor()
    {
        if (slotCursor != null)
        {
            slotCursor.gameObject.SetActive(false);
        }
    }

    public RectTransform EnsureSlotCursor(Transform parent)
    {
        if (slotCursor != null)
        {
            ConfigureSlotCursor();
            return slotCursor;
        }

        if (!createCursorIfMissing || parent == null)
        {
            return null;
        }

        GameObject cursorObject = new GameObject("LootPanel_SlotCursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = cursorObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = cursorObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.25f);
        image.raycastTarget = false;
        image.sprite = RuntimeUiSpriteUtility.SolidSprite;
        image.type = Image.Type.Simple;

        slotCursor = rect;
        ConfigureSlotCursor();
        return rect;
    }

    private void ConfigureSlotCursor()
    {
        if (slotCursor == null)
        {
            return;
        }

        UIManager.ConfigureDecorativeCursor(slotCursor, true);
    }

    /// <summary>
    /// Met à jour le nom + l'icône du container depuis un InteractableItem (MonoBehaviour en scène).
    /// Cherche le nom dans lootContainer.representedItem.itemName et le sprite dans lootContainer.representedItem.itemSprite.
    /// </summary>
    public void UpdateContainerHeader(InteractableItem lootContainer)
    {
        if (lootPanel == null)
        {
            return;
        }

        CacheContainerHeaderRefsIfNeeded();

        Item representedItem = lootContainer != null ? lootContainer.representedItem : null;
        if (representedItem == null)
        {
            SetContainerIcon(null);
            SetContainerName(string.Empty);
            return;
        }

        SetContainerIcon(representedItem.itemSprite);
        SetContainerName(representedItem.itemName);
    }

    private void SetContainerIcon(Sprite sprite)
    {
        if (cachedContainerIconImage == null)
        {
            return;
        }

        cachedContainerIconImage.sprite = sprite;
        if (hideIconWhenMissing)
        {
            cachedContainerIconImage.enabled = sprite != null;
        }
    }

    private void SetContainerName(string containerName)
    {
        if (cachedContainerNameText == null)
        {
            return;
        }

        cachedContainerNameText.text = containerName;
        cachedContainerNameText.gameObject.SetActive(!string.IsNullOrWhiteSpace(containerName));
    }

    private void CacheContainerHeaderRefsIfNeeded()
    {
        if (lootPanel == null)
        {
            return;
        }

        if (cachedContainerIconImage == null)
        {
            cachedContainerIconImage = containerIconImage != null
                ? containerIconImage
                : FindContainerIcon(lootPanel.transform, lootItemsParent);
        }

        if (cachedContainerNameText == null)
        {
            cachedContainerNameText = containerNameText != null
                ? containerNameText
                : FindContainerNameText(lootPanel.transform, lootItemsParent);
        }
    }

    private Image FindContainerIcon(Transform root, Transform itemsRoot)
    {
        if (root == null)
        {
            return null;
        }

        Image[] images = root.GetComponentsInChildren<Image>(true);
        if (images == null || images.Length == 0)
        {
            return null;
        }

        List<Image> candidates = new List<Image>();
        foreach (Image image in images)
        {
            if (image == null)
            {
                continue;
            }

            if (itemsRoot != null && image.transform.IsChildOf(itemsRoot))
            {
                continue;
            }

            candidates.Add(image);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            string name = candidates[i].name;
            if (name.IndexOf("container", System.StringComparison.OrdinalIgnoreCase) >= 0
                && name.IndexOf("sprite", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return candidates[i];
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            string name = candidates[i].name;
            if (name.IndexOf("sprite", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("icon", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return candidates[i];
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            string name = candidates[i].name;
            if (name.IndexOf("container", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return candidates[i];
            }
        }

        Image rootImage = root.GetComponent<Image>();
        if (rootImage != null)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] != rootImage)
                {
                    return candidates[i];
                }
            }
        }

        return candidates[0];
    }

    private TextMeshProUGUI FindContainerNameText(Transform root, Transform itemsRoot)
    {
        if (root == null)
        {
            return null;
        }

        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts == null || texts.Length == 0)
        {
            return null;
        }

        List<TextMeshProUGUI> candidates = new List<TextMeshProUGUI>();
        foreach (TextMeshProUGUI t in texts)
        {
            if (t == null)
            {
                continue;
            }

            if (itemsRoot != null && t.transform.IsChildOf(itemsRoot))
            {
                continue;
            }

            if (lootDescriptionText != null && t == lootDescriptionText)
            {
                continue;
            }

            candidates.Add(t);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            string n = candidates[i].name;
            if (n.IndexOf("container", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("title", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("name", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("header", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return candidates[i];
            }
        }

        return candidates[0];
    }

    private CanvasGroup GetLootCanvasGroup()
    {
        if (lootPanel == null)
        {
            return null;
        }

        CanvasGroup lootCanvasGroup = lootPanel.GetComponent<CanvasGroup>();
        if (lootCanvasGroup == null && addCanvasGroupIfMissing)
        {
            lootCanvasGroup = lootPanel.AddComponent<CanvasGroup>();
        }

        return lootCanvasGroup;
    }

    private void FadePanelTo(float targetAlpha, float duration)
    {
        CanvasGroup lootCanvasGroup = GetLootCanvasGroup();
        if (lootCanvasGroup == null)
        {
            return;
        }
        UIManager.TransitionCanvasGroup(this, lootCanvasGroup, targetAlpha > 0.001f, duration);
    }
}
