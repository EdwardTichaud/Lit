using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Contient les references UI et le fade du panel Inventaire.
[DisallowMultipleComponent]
public class InventoryUISettings : MonoBehaviour
{
    public static InventoryUISettings Instance { get; private set; }

    [Header("Inventory UI")]
    [Tooltip("Root du panel d'inventaire.")]
    public GameObject inventoryPanel;
    [Tooltip("Parent des slots d'items.")]
    public Transform itemsParent;
    [Tooltip("Prefab d'un slot d'item.")]
    public GameObject itemPrefab;
    [Tooltip("Curseur UI de selection.")]
    public RectTransform slotCursor;
    [Tooltip("Controleur de curseur (optionnel).")]
    public CursorController cursorController;
    [Tooltip("Texte de description de l'item selectionne.")]
    public TextMeshProUGUI descriptionText;
    [Tooltip("Padding ajoute autour du slot selectionne.")]
    public Vector2 cursorPadding = new Vector2(10f, 10f);
    [Tooltip("Cree un curseur si aucun n'est assigne.")]
    public bool createCursorIfMissing = true;
    [Tooltip("Synchronise les parametres vers le CursorController.")]
    public bool syncCursorControllerSettings = true;

    [Header("Inventory Navigation")]
    [Tooltip("Deadzone du stick pour naviguer dans l'inventaire.")]
    public float moveDeadzone = 0.5f;
    [Tooltip("Delai avant la repetition de navigation.")]
    public float initialRepeatDelay = 0.35f;
    [Tooltip("Intervalle entre repetitions de navigation.")]
    public float repeatInterval = 0.12f;
    [Tooltip("Autorise le wrap du curseur.")]
    public bool wrapCursor = false;

    [Header("Inventory Panel Fade")]
    [Tooltip("Duree du fade d'ouverture/fermeture.")]
    public float panelFadeDuration = 0.5f;
    [Tooltip("Met l'alpha a 0 au demarrage.")]
    public bool setAlphaToZeroOnStart = true;
    [Tooltip("Ajoute un CanvasGroup si manquant.")]
    public bool addCanvasGroupIfMissing = true;
    [Tooltip("Desactive les raycasts quand cache.")]
    public bool disableRaycastsWhenHidden = true;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            return;
        }

        Instance = this;
        ResolveCursorController();
        InitializePanel();
    }
    private void ResolveCursorController()
    {
        if (cursorController == null)
        {
            if (slotCursor != null)
            {
                cursorController = slotCursor.GetComponent<CursorController>();
            }

            if (cursorController == null && inventoryPanel != null)
            {
                cursorController = inventoryPanel.GetComponentInChildren<CursorController>(true);
            }
        }

        if (slotCursor == null && cursorController != null)
        {
            if (cursorController.cursor != null)
            {
                slotCursor = cursorController.cursor;
            }
            else
            {
                slotCursor = cursorController.GetComponent<RectTransform>();
                if (slotCursor != null)
                {
                    cursorController.cursor = slotCursor;
                }
            }
        }
        else if (cursorController != null && cursorController.cursor == null && slotCursor != null)
        {
            cursorController.cursor = slotCursor;
        }

        if (cursorController != null)
        {
            if (cursorController.itemsParent == null && itemsParent != null)
            {
                cursorController.itemsParent = itemsParent as RectTransform;
            }

            if (cursorController.layoutGroup == null && itemsParent != null)
            {
                cursorController.layoutGroup = itemsParent.GetComponent<LayoutGroup>();
            }

            if (syncCursorControllerSettings)
            {
                cursorController.cursorPadding = cursorPadding;
                cursorController.moveDeadzone = moveDeadzone;
                cursorController.initialRepeatDelay = initialRepeatDelay;
                cursorController.repeatInterval = repeatInterval;
                cursorController.wrap = wrapCursor;
            }
        }
    }


    public void InitializePanel()
    {
        ResolveCursorController();
        if (inventoryPanel == null)
        {
            return;
        }

        CanvasGroup canvasGroup = GetCanvasGroup();
        if (canvasGroup != null && setAlphaToZeroOnStart)
        {
            canvasGroup.alpha = 0f;
            if (disableRaycastsWhenHidden)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
    }

    public void OpenPanel()
    {
        ResolveCursorController();
        if (inventoryPanel == null)
        {
            return;
        }

        inventoryPanel.SetActive(true);
        CanvasGroup canvasGroup = GetCanvasGroup();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            if (disableRaycastsWhenHidden)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        FadePanelTo(1f, panelFadeDuration);
    }

    public void ClosePanel()
    {
        if (inventoryPanel == null)
        {
            return;
        }

        if (!CanRunCoroutines())
        {
            CanvasGroup canvasGroup = GetCanvasGroup();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                if (disableRaycastsWhenHidden)
                {
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }
            }
            return;
        }

        FadePanelTo(0f, panelFadeDuration);
    }

    public void UpdateDescription(Item item)
    {
        if (descriptionText == null)
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

        descriptionText.text = description;
        descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(description));
    }

    public void HideCursor()
    {
        if (slotCursor != null)
        {
            if (slotCursor.GetComponent<CursorController>() == null)
            {
                slotCursor.gameObject.SetActive(false);
            }
        }
    }

    public RectTransform EnsureSlotCursor(Transform parent)
    {
        ResolveCursorController();
        if (slotCursor != null)
        {
            return slotCursor;
        }

        if (!createCursorIfMissing || parent == null)
        {
            return null;
        }

        GameObject cursorObject = new GameObject("InventoryPanel_SlotCursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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
        return rect;
    }

    private CanvasGroup GetCanvasGroup()
    {
        if (inventoryPanel == null)
        {
            return null;
        }

        CanvasGroup canvasGroup = inventoryPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null && addCanvasGroupIfMissing)
        {
            canvasGroup = inventoryPanel.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private void FadePanelTo(float targetAlpha, float duration)
    {
        CanvasGroup canvasGroup = GetCanvasGroup();
        if (canvasGroup == null)
        {
            return;
        }

        if (!CanRunCoroutines())
        {
            canvasGroup.alpha = targetAlpha;
            if (disableRaycastsWhenHidden)
            {
                bool visible = targetAlpha > 0.001f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        float startAlpha = canvasGroup.alpha;
        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            if (disableRaycastsWhenHidden)
            {
                bool visible = targetAlpha > 0.001f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
            return;
        }

        fadeRoutine = StartCoroutine(FadeRoutine(canvasGroup, startAlpha, targetAlpha, duration));
    }

    private IEnumerator FadeRoutine(CanvasGroup canvasGroup, float startAlpha, float targetAlpha, float duration)
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        float time = 0f;
        if (disableRaycastsWhenHidden)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        if (disableRaycastsWhenHidden)
        {
            bool visible = targetAlpha > 0.001f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }

    private bool CanRunCoroutines()
    {
        return isActiveAndEnabled && gameObject.activeInHierarchy;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveCursorController();
    }
#endif
}
