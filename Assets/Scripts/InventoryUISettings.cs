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
    [Header("World information panels")]
    public GameObject localItemInformationPanelPrefab;
    public GameObject localBuildingInformationPanelPrefab;
    [Tooltip("Ancien curseur global. Il reste masque : chaque slot utilise son propre Cursor.")]
    public RectTransform slotCursor;
    [Tooltip("Ancien controleur global. La navigation est pilotee par InventoryPanelController.")]
    public CursorController cursorController;
    [Tooltip("Texte de description de l'item selectionne.")]
    public TextMeshProUGUI descriptionText;
    [Tooltip("Padding ajoute autour du slot selectionne.")]
    public Vector2 cursorPadding = new Vector2(10f, 10f);
    [Tooltip("Compatibilite scene. Aucun curseur runtime n'est cree.")]
    public bool createCursorIfMissing;
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
    [Tooltip("Compatibilite scene. Un CanvasGroup doit etre configure dans la scene.")]
    public bool addCanvasGroupIfMissing;
    [Tooltip("Desactive les raycasts quand cache.")]
    public bool disableRaycastsWhenHidden = true;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private UiPanel uiPanel;

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

            // Le cadre global et son input concurrencent les cursors embarques dans les slots.
            cursorController.cursor = null;
            cursorController.allowInput = false;
            cursorController.enabled = false;
        }
    }


    public void InitializePanel()
    {
        ResolveCursorController();
        if (inventoryPanel == null)
        {
            return;
        }

        UiPanel managedPanel = GetUiPanel();
        if (managedPanel != null)
        {
            managedPanel.Hide(true);
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

        UiPanel managedPanel = GetUiPanel();
        if (managedPanel != null)
        {
            managedPanel.Show();
            return;
        }

        FadePanelTo(1f, panelFadeDuration);
    }

    public void ClosePanel()
    {
        if (inventoryPanel == null)
        {
            return;
        }

        UiPanel managedPanel = GetUiPanel();
        if (managedPanel != null)
        {
            managedPanel.Hide();
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
        if (slotCursor != null) slotCursor.gameObject.SetActive(false);
    }

    public RectTransform EnsureSlotCursor(Transform parent)
    {
        return null;
    }

    private CanvasGroup GetCanvasGroup()
    {
        if (inventoryPanel == null)
        {
            return null;
        }

        if (canvasGroup == null) canvasGroup = inventoryPanel.GetComponent<CanvasGroup>();
        return canvasGroup;
    }

    private UiPanel GetUiPanel()
    {
        if (inventoryPanel == null)
        {
            return null;
        }

        if (uiPanel == null)
        {
            uiPanel = inventoryPanel.GetComponent<UiPanel>();
        }

        return uiPanel;
    }

    private void FadePanelTo(float targetAlpha, float duration)
    {
        CanvasGroup canvasGroup = GetCanvasGroup();
        if (canvasGroup == null)
        {
            return;
        }

        UIManager.TransitionCanvasGroup(this, canvasGroup, targetAlpha > 0.001f, duration);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveCursorController();
    }
#endif
}
