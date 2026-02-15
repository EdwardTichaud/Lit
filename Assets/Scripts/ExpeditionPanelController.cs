using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// UI de selection des expeditions (navigation + lancement + transition).
public class ExpeditionPanelController : MonoBehaviour
{
    [Header("Expeditions")]
    [Tooltip("Liste d'expeditions disponibles dans ce panel.")]
    public List<Expedition> expeditions = new List<Expedition>();
    [Tooltip("Affiche les expeditions verouillees.")]
    public bool showLockedExpeditions = true;
    [Tooltip("Auto-collecte les Expedition assets en scene.")]
    public bool autoCollectExpeditions = true;
    [Tooltip("Remplace la liste locale par celle collecte.")]
    public bool overwriteExpeditionList = true;
    [Tooltip("Trie les expeditions par nom.")]
    public bool sortExpeditionsByName = true;

    [Header("UI")]
    [Tooltip("Root du panel d'expedition.")]
    public GameObject expeditionPanel;
    [Tooltip("Parent des slots d'expedition.")]
    public Transform expeditionItemsParent;
    [Tooltip("Prefab d'un slot d'expedition.")]
    public GameObject expeditionItemPrefab;
    [Tooltip("Curseur UI de selection.")]
    public RectTransform expeditionCursor;
    [Tooltip("Padding applique autour du slot selectionne.")]
    public Vector2 cursorPadding = new Vector2(10f, 10f);
    [Tooltip("Cree un curseur si manquant.")]
    public bool createCursorIfMissing = true;
    [Tooltip("Texte de description de l'expedition selectionnee.")]
    public TextMeshProUGUI descriptionText;

    [Header("Navigation")]
    [Tooltip("Deadzone du stick pour naviguer.")]
    public float moveDeadzone = 0.5f;
    [Tooltip("Delai avant repetition de navigation.")]
    public float initialRepeatDelay = 0.35f;
    [Tooltip("Intervalle entre repetitions de navigation.")]
    public float repeatInterval = 0.12f;
    [Tooltip("Autorise le wrap du curseur.")]
    public bool wrapCursor = false;

    [Header("Scroll")]
    [Tooltip("Active un scroll lisse.")]
    public bool smoothScroll = true;
    [Tooltip("Duree de lissage du scroll.")]
    public float scrollSmoothTime = 0.08f;
    [Tooltip("Vitesse max du scroll lisse.")]
    public float scrollMaxSpeed = 4000f;
    [Tooltip("Padding du viewport pour la mise au centre.")]
    public Vector2 scrollPadding = new Vector2(16f, 16f);

    [Header("Input")]
    [Tooltip("Retour ferme le panel.")]
    public bool closeOnReturn = true;
    [Tooltip("Interact lance l'expedition.")]
    public bool launchOnInteract = true;
    [Tooltip("Ferme le panel apres le lancement.")]
    public bool closeOnLaunch = true;

    [Header("Labyrinths")]
    [Tooltip("Desactive les labyrinthes non selectionnes.")]
    public bool deactivateOtherLabyrinths = true;

    [Header("Expedition Transition")]
    [Tooltip("Utilise un fondu avant/apres teleportation.")]
    public bool useTransitionFade = true;
    [Tooltip("Duree du fondu sortant.")]
    public float fadeOutDuration = 1f;
    [Tooltip("Duree du fondu entrant.")]
    public float fadeInDuration = 1f;
    [Tooltip("VFX instancie lors du teleport.")]
    public GameObject teleportVfxPrefab;
    [Tooltip("Offset applique au VFX.")]
    public Vector3 teleportVfxOffset = Vector3.zero;
    [Tooltip("Parent pour instancier le VFX.")]
    public Transform teleportVfxParent;
    [Tooltip("Duree de vie du VFX.")]
    public float teleportVfxLifetime = 2.5f;
    [Tooltip("Utilise un tag pour trouver le spawn.")]
    public bool useSpawnPointTag = true;
    [Tooltip("Tag du spawn de labyrinthe.")]
    public string labyrinthSpawnPointTag = "LabyrinthSpawnPoint";
    [Tooltip("Nom fallback du spawn de labyrinthe.")]
    public string labyrinthSpawnPointName = "Labyrinth_SpawnPoint";
    [Tooltip("Offset applique au spawn.")]
    public Vector3 spawnPointOffset = Vector3.zero;
    [Tooltip("Rayon de dispersion des personnages au spawn.")]
    public float spawnSpreadRadius = 1.5f;

    [Header("Panel Fade")]
    [Tooltip("Duree du fade du panel.")]
    public float panelFadeDuration = 0.5f;
    [Tooltip("Met l'alpha a 0 au demarrage.")]
    public bool setAlphaToZeroOnStart = true;
    [Tooltip("Ajoute un CanvasGroup si manquant.")]
    public bool addCanvasGroupIfMissing = true;
    [Tooltip("Desactive les raycasts quand cache.")]
    public bool disableRaycastsWhenHidden = true;
    [Tooltip("Desactive le GameObject du panel a la fermeture.")]
    public bool deactivatePanelOnClose = true;

    [Header("Screen Fade")]
    [Tooltip("Panel de fondu plein ecran.")]
    public GameObject screenFadePanel;
    [Tooltip("Couleur du fondu plein ecran.")]
    public Color screenFadeColor = Color.black;
    [Tooltip("Cree un panel de fondu si manquant.")]
    public bool createScreenFadeIfMissing = true;
    [Tooltip("Le panel de fondu bloque les raycasts UI.")]
    public bool screenFadeBlocksRaycasts = true;

    private PlayerInputs playerInputs;
    private bool panelOpen;
    private bool squadInputLocked;
    private readonly List<ExpeditionSlotUI> slots = new List<ExpeditionSlotUI>();
    private ExpeditionSlotUI currentFocusedSlot;
    private int currentSlotIndex;
    private int lastMoveDirection;
    private float nextMoveTime;
    private bool cursorDirty;
    private bool scrollDirty;
    private Coroutine panelFadeRoutine;
    private CanvasGroup panelCanvasGroup;
    private Transform resolvedItemsParent;
    private ScrollRect resolvedScrollRect;
    private Vector2 scrollVelocity;
    private Vector2 scrollTargetPosition;
    private bool hasScrollTarget;
    private bool isTransitioning;
    private CanvasGroup screenFadeCanvasGroup;
    private bool suppressPanelDeactivate;
    private static Texture2D cursorFallbackTexture;
    private static Sprite cursorFallbackSprite;

    public bool IsOpen => panelOpen;

    public Expedition SelectedExpedition => currentFocusedSlot != null ? currentFocusedSlot.Expedition : null;

    private void Awake()
    {
        playerInputs = new PlayerInputs();
        if (autoCollectExpeditions)
        {
            CollectExpeditions();
        }

        InitializePanelFade();
    }

    private void OnEnable()
    {
        if (playerInputs == null)
        {
            playerInputs = new PlayerInputs();
        }

        playerInputs.Enable();
        playerInputs.Player.Interact.performed += OnInteractPerformed;
        playerInputs.Player.Return.performed += OnReturnPerformed;
    }

    private void OnDisable()
    {
        if (playerInputs != null)
        {
            playerInputs.Player.Interact.performed -= OnInteractPerformed;
            playerInputs.Player.Return.performed -= OnReturnPerformed;
            playerInputs.Disable();
        }

        InputFocusStack.Pop(this);
        ClosePanel();
    }

    private void Update()
    {
        if (!panelOpen)
        {
            return;
        }

        if (!HasInputFocus())
        {
            return;
        }

        // Navigation dans la liste d'expeditions.
        HandleNavigation();
    }

    private void LateUpdate()
    {
        if (!panelOpen)
        {
            return;
        }

        if (!HasInputFocus())
        {
            return;
        }

        // Mise a jour scroll/cursor apres layout.
        UpdateScroll();
        UpdateCursorVisual();
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!panelOpen || !launchOnInteract)
        {
            return;
        }

        if (!HasInputFocus())
        {
            return;
        }

        LaunchSelectedExpedition();
    }

    private void OnReturnPerformed(InputAction.CallbackContext context)
    {
        if (!panelOpen || !closeOnReturn)
        {
            return;
        }

        if (!HasInputFocus())
        {
            return;
        }

        ClosePanel();
    }

    public void OpenPanel()
    {
        if (panelOpen)
        {
            SetSquadInputLock(true);
            InputFocusStack.Push(this);
            return;
        }

        if (expeditionPanel != null)
        {
            expeditionPanel.SetActive(true);
            panelCanvasGroup = GetPanelCanvasGroup();
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
                if (disableRaycastsWhenHidden)
                {
                    panelCanvasGroup.interactable = false;
                    panelCanvasGroup.blocksRaycasts = false;
                }
            }
        }

        panelOpen = true;
        InputFocusStack.Push(this);
        SetSquadInputLock(true);
        RebuildSlots();
        FadePanelTo(1f, panelFadeDuration);
    }

    public void ClosePanel()
    {
        ClosePanel(false, false);
    }

    public void ClosePanel(bool keepInputLock)
    {
        ClosePanel(keepInputLock, false);
    }

    public void ClosePanel(bool keepInputLock, bool keepPanelActive)
    {
        if (!panelOpen)
        {
            if (squadInputLocked)
            {
                if (!keepInputLock)
                {
                    SetSquadInputLock(false);
                }
            }

            if (!keepPanelActive)
            {
                suppressPanelDeactivate = false;
            }
            return;
        }

        suppressPanelDeactivate = keepPanelActive;

        panelOpen = false;
        InputFocusStack.Pop(this);
        if (!keepInputLock)
        {
            SetSquadInputLock(false);
        }
        currentFocusedSlot = null;
        currentSlotIndex = 0;
        lastMoveDirection = 0;
        nextMoveTime = 0f;
        cursorDirty = false;
        scrollDirty = false;
        slots.Clear();
        resolvedItemsParent = null;
        resolvedScrollRect = null;
        scrollVelocity = Vector2.zero;
        hasScrollTarget = false;
        if (descriptionText != null)
        {
            descriptionText.text = string.Empty;
            descriptionText.gameObject.SetActive(false);
        }

        if (expeditionCursor != null)
        {
            expeditionCursor.gameObject.SetActive(false);
        }

        FadePanelTo(0f, panelFadeDuration);
    }

    private void RebuildSlots()
    {
        currentFocusedSlot = null;
        currentSlotIndex = 0;
        slots.Clear();

        Transform itemsParent = GetItemsParent();
        if (itemsParent == null)
        {
            return;
        }

        if (expeditionItemPrefab != null && expeditions != null && expeditions.Count > 0)
        {
            ClearSlotChildren(itemsParent);

            for (int i = 0; i < expeditions.Count; i++)
            {
                Expedition expedition = expeditions[i];
                if (expedition == null)
                {
                    continue;
                }

                if (!showLockedExpeditions && !expedition.unlocked)
                {
                    continue;
                }

                GameObject entry = CreateInstance(expeditionItemPrefab, itemsParent);
                if (entry == null)
                {
                    continue;
                }

                SetEntryText(entry, expedition);
                SetEntrySprite(entry, expedition);

                ExpeditionSlotUI slotUi = entry.GetComponent<ExpeditionSlotUI>();
                if (slotUi == null)
                {
                    slotUi = entry.AddComponent<ExpeditionSlotUI>();
                }
                slotUi.Initialize(this, expedition);
                slots.Add(slotUi);
            }
        }
        else
        {
            int expeditionIndex = 0;
            for (int i = 0; i < itemsParent.childCount; i++)
            {
                Transform child = itemsParent.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                ExpeditionSlotUI slotUi = child.GetComponent<ExpeditionSlotUI>();
                if (slotUi == null)
                {
                    slotUi = child.gameObject.AddComponent<ExpeditionSlotUI>();
                }

                Expedition expedition = slotUi.Expedition;
                if (expedition == null && expeditions != null && expeditionIndex < expeditions.Count)
                {
                    expedition = expeditions[expeditionIndex];
                    expeditionIndex++;
                }

                if (expedition != null && !showLockedExpeditions && !expedition.unlocked)
                {
                    continue;
                }

                slotUi.Initialize(this, expedition);
                slots.Add(slotUi);
            }
        }

        if (slots.Count > 0)
        {
            FocusSlot(slots[0]);
        }
    }

    private Transform ResolveItemsParent(Transform provided)
    {
        if (provided == null)
        {
            resolvedScrollRect = null;
            return null;
        }

        ScrollRect scrollRect = provided.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = provided.GetComponentInParent<ScrollRect>();
        }

        if (scrollRect == null)
        {
            scrollRect = provided.GetComponentInChildren<ScrollRect>(true);
        }

        resolvedScrollRect = scrollRect;
        if (scrollRect != null && scrollRect.content != null)
        {
            return scrollRect.content;
        }

        return provided;
    }

    private Transform GetItemsParent()
    {
        if (expeditionItemsParent == null)
        {
            resolvedItemsParent = null;
            return null;
        }

        if (resolvedItemsParent == null)
        {
            resolvedItemsParent = ResolveItemsParent(expeditionItemsParent);
        }

        return resolvedItemsParent;
    }

    private ScrollRect GetItemsScrollRect()
    {
        if (resolvedScrollRect != null)
        {
            return resolvedScrollRect;
        }

        if (expeditionItemsParent == null)
        {
            return null;
        }

        ResolveItemsParent(expeditionItemsParent);
        return resolvedScrollRect;
    }

    private void ClearSlotChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.GetComponent<ExpeditionSlotUI>() != null || child.GetComponentInChildren<ExpeditionSlotUI>(true) != null)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void CollectExpeditions()
    {
        List<Expedition> found = new List<Expedition>();
        Expedition[] assets = Resources.FindObjectsOfTypeAll<Expedition>();
        for (int i = 0; i < assets.Length; i++)
        {
            Expedition expedition = assets[i];
            if (expedition != null && !found.Contains(expedition))
            {
                found.Add(expedition);
            }
        }

        if (!overwriteExpeditionList && expeditions != null)
        {
            for (int i = 0; i < expeditions.Count; i++)
            {
                Expedition expedition = expeditions[i];
                if (expedition != null && !found.Contains(expedition))
                {
                    found.Add(expedition);
                }
            }
        }

        if (sortExpeditionsByName)
        {
            found.Sort((a, b) =>
            {
                string nameA = a != null ? (!string.IsNullOrWhiteSpace(a.expeditionName) ? a.expeditionName : a.name) : string.Empty;
                string nameB = b != null ? (!string.IsNullOrWhiteSpace(b.expeditionName) ? b.expeditionName : b.name) : string.Empty;
                return string.Compare(nameA, nameB, System.StringComparison.OrdinalIgnoreCase);
            });
        }

        expeditions = found;
    }

    private void InitializePanelFade()
    {
        if (expeditionPanel == null)
        {
            return;
        }

        panelCanvasGroup = GetPanelCanvasGroup();
        if (panelCanvasGroup != null && setAlphaToZeroOnStart)
        {
            panelCanvasGroup.alpha = 0f;
            if (disableRaycastsWhenHidden)
            {
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
            }

            if (deactivatePanelOnClose)
            {
                expeditionPanel.SetActive(false);
            }
        }
    }

    private CanvasGroup GetPanelCanvasGroup()
    {
        if (expeditionPanel == null)
        {
            return null;
        }

        CanvasGroup canvasGroup = expeditionPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null && addCanvasGroupIfMissing)
        {
            canvasGroup = expeditionPanel.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private void FadePanelTo(float targetAlpha, float duration)
    {
        panelCanvasGroup = GetPanelCanvasGroup();
        if (panelCanvasGroup == null)
        {
            if (targetAlpha <= 0f && ShouldDeactivatePanelOnClose() && expeditionPanel != null)
            {
                expeditionPanel.SetActive(false);
            }
            return;
        }

        if (!CanRunCoroutines() || expeditionPanel == null || !expeditionPanel.activeInHierarchy)
        {
            panelCanvasGroup.alpha = targetAlpha;
            if (disableRaycastsWhenHidden)
            {
                bool visible = targetAlpha > 0.001f;
                panelCanvasGroup.interactable = visible;
                panelCanvasGroup.blocksRaycasts = visible;
            }
            if (targetAlpha <= 0f && ShouldDeactivatePanelOnClose() && expeditionPanel != null)
            {
                expeditionPanel.SetActive(false);
            }
            return;
        }

        if (panelFadeRoutine != null)
        {
            StopCoroutine(panelFadeRoutine);
        }

        float startAlpha = panelCanvasGroup.alpha;
        if (duration <= 0f)
        {
            panelCanvasGroup.alpha = targetAlpha;
            if (disableRaycastsWhenHidden)
            {
                bool visible = targetAlpha > 0.001f;
                panelCanvasGroup.interactable = visible;
                panelCanvasGroup.blocksRaycasts = visible;
            }
            if (targetAlpha <= 0f && ShouldDeactivatePanelOnClose() && expeditionPanel != null)
            {
                expeditionPanel.SetActive(false);
            }
            return;
        }

        panelFadeRoutine = StartCoroutine(FadePanelRoutine(panelCanvasGroup, startAlpha, targetAlpha, duration));
    }

    private IEnumerator FadePanelRoutine(CanvasGroup canvasGroup, float startAlpha, float targetAlpha, float duration)
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

        if (targetAlpha <= 0f && ShouldDeactivatePanelOnClose() && expeditionPanel != null)
        {
            expeditionPanel.SetActive(false);
        }
    }

    private bool ShouldDeactivatePanelOnClose()
    {
        return deactivatePanelOnClose && !suppressPanelDeactivate;
    }

    private bool CanRunCoroutines()
    {
        return isActiveAndEnabled && gameObject.activeInHierarchy;
    }

    public void FocusSlot(ExpeditionSlotUI slot)
    {
        if (slot == null || slot.SlotRect == null)
        {
            return;
        }

        currentFocusedSlot = slot;
        cursorDirty = true;
        scrollDirty = true;
        if (slots.Count > 0)
        {
            int index = slots.IndexOf(slot);
            if (index >= 0)
            {
                currentSlotIndex = index;
            }
        }

        UpdateDescription(slot.Expedition);
    }

    private void UpdateDescription(Expedition expedition)
    {
        if (descriptionText == null)
        {
            return;
        }

        string text = string.Empty;
        if (expedition != null)
        {
            text = expedition.description;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = !string.IsNullOrWhiteSpace(expedition.expeditionName)
                    ? expedition.expeditionName
                    : expedition.name;
            }
        }

        descriptionText.text = text;
        descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    private void UpdateCursorVisual()
    {
        if (currentFocusedSlot == null)
        {
            if (slots.Count > 0)
            {
                FocusSlot(slots[0]);
            }
        }

        ExpeditionSlotUI slot = currentFocusedSlot;
        if (slot == null || slot.SlotRect == null)
        {
            if (expeditionCursor != null)
            {
                expeditionCursor.gameObject.SetActive(false);
            }
            cursorDirty = false;
            return;
        }

        if (cursorDirty)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform itemsRect = GetItemsParent() as RectTransform;
            if (itemsRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRect);
            }
        }

        Transform cursorParent = slot.SlotRect.parent != null ? slot.SlotRect.parent : GetItemsParent();
        RectTransform cursor = EnsureCursor(cursorParent);
        if (cursor == null)
        {
            cursorDirty = false;
            return;
        }

        cursor.gameObject.SetActive(true);
        if (cursorParent != null)
        {
            cursor.SetParent(cursorParent, false);
        }
        cursor.SetAsLastSibling();
        cursor.pivot = new Vector2(0.5f, 0.5f);
        cursor.position = slot.SlotRect.position;
        Vector2 size = slot.SlotRect.rect.size;
        cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x + cursorPadding.x);
        cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y + cursorPadding.y);

        if (scrollDirty)
        {
            EnsureSlotVisible(slot);
            scrollDirty = false;
        }

        cursorDirty = false;
    }

    private RectTransform EnsureCursor(Transform parent)
    {
        if (expeditionCursor != null)
        {
            return expeditionCursor;
        }

        Transform found = null;
        if (expeditionPanel != null)
        {
            found = expeditionPanel.transform.Find("ExpeditionPanel_Cursor");
        }

        if (found == null && expeditionPanel != null)
        {
            RectTransform[] rects = expeditionPanel.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i] != null && rects[i].name == "ExpeditionPanel_Cursor")
                {
                    found = rects[i];
                    break;
                }
            }
        }

        if (found != null)
        {
            expeditionCursor = found as RectTransform;
            return expeditionCursor;
        }

        if (!createCursorIfMissing || parent == null)
        {
            return null;
        }

        GameObject cursorObject = new GameObject("ExpeditionPanel_Cursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        RectTransform rect = cursorObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        Image image = cursorObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.25f);
        image.raycastTarget = false;
        image.sprite = GetCursorSprite();
        image.type = Image.Type.Sliced;
        LayoutElement layout = cursorObject.GetComponent<LayoutElement>();
        layout.ignoreLayout = true;
        expeditionCursor = rect;
        return rect;
    }

    private void EnsureSlotVisible(ExpeditionSlotUI slot)
    {
        if (slot == null || slot.SlotRect == null)
        {
            return;
        }

        ScrollRect scrollRect = GetItemsScrollRect();
        if (scrollRect == null || scrollRect.content == null)
        {
            return;
        }

        RectTransform viewport = scrollRect.viewport != null
            ? scrollRect.viewport
            : scrollRect.GetComponent<RectTransform>();
        if (viewport == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        Vector3[] slotCorners = new Vector3[4];
        Vector3[] viewCorners = new Vector3[4];
        slot.SlotRect.GetWorldCorners(slotCorners);
        viewport.GetWorldCorners(viewCorners);

        float padX = GetWorldPaddingX(viewport);
        float padY = GetWorldPaddingY(viewport);

        float viewLeft = viewCorners[0].x + padX;
        float viewBottom = viewCorners[0].y + padY;
        float viewTop = viewCorners[1].y - padY;
        float viewRight = viewCorners[2].x - padX;

        float slotLeft = slotCorners[0].x;
        float slotBottom = slotCorners[0].y;
        float slotTop = slotCorners[1].y;
        float slotRight = slotCorners[2].x;

        Vector3 worldDelta = Vector3.zero;
        bool changed = false;

        if (scrollRect.vertical)
        {
            if (slotTop > viewTop)
            {
                worldDelta.y -= slotTop - viewTop;
                changed = true;
            }
            else if (slotBottom < viewBottom)
            {
                worldDelta.y += viewBottom - slotBottom;
                changed = true;
            }
        }

        if (scrollRect.horizontal)
        {
            if (slotLeft < viewLeft)
            {
                worldDelta.x += viewLeft - slotLeft;
                changed = true;
            }
            else if (slotRight > viewRight)
            {
                worldDelta.x -= slotRight - viewRight;
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        Vector2 localDelta = viewport.InverseTransformVector(worldDelta);
        Vector2 desiredPosition = scrollRect.content.anchoredPosition + localDelta;
        Vector2 clamped = ClampContentPositionWorld(scrollRect, viewport, desiredPosition);
        scrollTargetPosition = clamped;
        hasScrollTarget = true;
        scrollRect.StopMovement();

        if (!smoothScroll)
        {
            scrollRect.content.anchoredPosition = clamped;
            hasScrollTarget = false;
        }
    }

    private Vector2 ClampContentPositionWorld(ScrollRect scrollRect, RectTransform viewport, Vector2 desiredPosition)
    {
        if (scrollRect == null || scrollRect.content == null || viewport == null)
        {
            return desiredPosition;
        }

        RectTransform content = scrollRect.content;
        Vector2 originalPosition = content.anchoredPosition;
        content.anchoredPosition = desiredPosition;
        Canvas.ForceUpdateCanvases();

        Vector3[] viewCorners = new Vector3[4];
        Vector3[] contentCorners = new Vector3[4];
        viewport.GetWorldCorners(viewCorners);
        content.GetWorldCorners(contentCorners);

        float viewLeft = viewCorners[0].x;
        float viewBottom = viewCorners[0].y;
        float viewTop = viewCorners[1].y;
        float viewRight = viewCorners[2].x;

        float contentLeft = contentCorners[0].x;
        float contentBottom = contentCorners[0].y;
        float contentTop = contentCorners[1].y;
        float contentRight = contentCorners[2].x;

        Vector3 worldDelta = Vector3.zero;

        if (scrollRect.vertical)
        {
            if (contentTop < viewTop)
            {
                worldDelta.y += viewTop - contentTop;
            }
            else if (contentBottom > viewBottom)
            {
                worldDelta.y += viewBottom - contentBottom;
            }
        }

        if (scrollRect.horizontal)
        {
            if (contentLeft > viewLeft)
            {
                worldDelta.x += viewLeft - contentLeft;
            }
            else if (contentRight < viewRight)
            {
                worldDelta.x += viewRight - contentRight;
            }
        }

        Vector2 localDelta = viewport.InverseTransformVector(worldDelta);
        Vector2 result = desiredPosition + localDelta;
        content.anchoredPosition = originalPosition;
        return result;
    }

    private float GetWorldPaddingX(RectTransform viewport)
    {
        if (viewport == null)
        {
            return scrollPadding.x;
        }

        Vector3 origin = viewport.TransformPoint(Vector3.zero);
        Vector3 point = viewport.TransformPoint(new Vector3(scrollPadding.x, 0f, 0f));
        return Vector3.Distance(origin, point);
    }

    private float GetWorldPaddingY(RectTransform viewport)
    {
        if (viewport == null)
        {
            return scrollPadding.y;
        }

        Vector3 origin = viewport.TransformPoint(Vector3.zero);
        Vector3 point = viewport.TransformPoint(new Vector3(0f, scrollPadding.y, 0f));
        return Vector3.Distance(origin, point);
    }

    private void UpdateScroll()
    {
        if (!hasScrollTarget)
        {
            return;
        }

        ScrollRect scrollRect = GetItemsScrollRect();
        if (scrollRect == null || scrollRect.content == null)
        {
            hasScrollTarget = false;
            return;
        }

        Vector2 current = scrollRect.content.anchoredPosition;
        Vector2 target = scrollTargetPosition;
        if (!smoothScroll)
        {
            scrollRect.content.anchoredPosition = target;
            hasScrollTarget = false;
            return;
        }

        float smoothTime = Mathf.Max(0.01f, scrollSmoothTime);
        float maxSpeed = Mathf.Max(0f, scrollMaxSpeed);
        Vector2 newPos = Vector2.SmoothDamp(current, target, ref scrollVelocity, smoothTime, maxSpeed, Time.unscaledDeltaTime);
        scrollRect.content.anchoredPosition = newPos;

        if ((newPos - target).sqrMagnitude <= 0.25f)
        {
            scrollRect.content.anchoredPosition = target;
            hasScrollTarget = false;
            scrollVelocity = Vector2.zero;
        }
    }

    private static Sprite GetCursorSprite()
    {
        if (cursorFallbackSprite != null)
        {
            return cursorFallbackSprite;
        }

        if (cursorFallbackTexture == null)
        {
            cursorFallbackTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            cursorFallbackTexture.SetPixel(0, 0, Color.white);
            cursorFallbackTexture.Apply();
        }

        cursorFallbackSprite = Sprite.Create(
            cursorFallbackTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);

        return cursorFallbackSprite;
    }

    private void HandleNavigation()
    {
        if (playerInputs == null || slots.Count == 0)
        {
            return;
        }

        Vector2 moveInput = playerInputs.Player.Move.ReadValue<Vector2>();
        int direction = GetMoveDirection(moveInput, moveDeadzone);
        if (direction == 0)
        {
            lastMoveDirection = 0;
            nextMoveTime = 0f;
            return;
        }

        float now = Time.unscaledTime;
        if (direction != lastMoveDirection)
        {
            MoveSlot(direction, wrapCursor);
            lastMoveDirection = direction;
            nextMoveTime = now + initialRepeatDelay;
            return;
        }

        if (now >= nextMoveTime)
        {
            MoveSlot(direction, wrapCursor);
            nextMoveTime = now + repeatInterval;
        }
    }

    private int GetMoveDirection(Vector2 input, float deadzone)
    {
        float absX = Mathf.Abs(input.x);
        float absY = Mathf.Abs(input.y);

        if (absX < deadzone && absY < deadzone)
        {
            return 0;
        }

        if (absX >= absY)
        {
            return input.x > 0f ? 2 : -2;
        }

        return input.y > 0f ? -1 : 1;
    }

    private void MoveSlot(int direction, bool wrap)
    {
        ExpeditionSlotUI current = GetFocusedSlot();
        if (current == null)
        {
            return;
        }

        ExpeditionSlotUI next = FindNeighborSlot(current, direction, wrap);
        if (next == null || next == current)
        {
            return;
        }

        FocusSlot(next);
    }

    private ExpeditionSlotUI FindNeighborSlot(ExpeditionSlotUI current, int direction, bool wrap)
    {
        if (current == null || current.SlotRect == null)
        {
            return null;
        }

        Canvas canvas = expeditionPanel != null ? expeditionPanel.GetComponentInParent<Canvas>() : null;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (cursorDirty && expeditionItemsParent != null)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform itemsRect = GetItemsParent() as RectTransform;
            if (itemsRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRect);
            }
        }

        List<SlotInfo> slotInfos = new List<SlotInfo>(slots.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            ExpeditionSlotUI slot = slots[i];
            if (slot == null || slot.SlotRect == null)
            {
                continue;
            }

            Vector2 pos = RectTransformUtility.WorldToScreenPoint(uiCamera, slot.SlotRect.position);
            slotInfos.Add(new SlotInfo(slot, pos));
        }

        if (slotInfos.Count == 0)
        {
            return null;
        }

        slotInfos.Sort((a, b) => b.Position.y.CompareTo(a.Position.y));

        float rowTolerance = GetSlotScreenHeight(current, uiCamera) * 0.6f;
        if (rowTolerance <= 0f)
        {
            rowTolerance = 10f;
        }

        List<List<SlotInfo>> rows = new List<List<SlotInfo>>();
        for (int i = 0; i < slotInfos.Count; i++)
        {
            SlotInfo info = slotInfos[i];
            if (rows.Count == 0 || Mathf.Abs(info.Position.y - rows[rows.Count - 1][0].Position.y) > rowTolerance)
            {
                rows.Add(new List<SlotInfo>());
            }

            rows[rows.Count - 1].Add(info);
        }

        int currentRow = -1;
        int currentCol = -1;
        float currentX = 0f;

        for (int r = 0; r < rows.Count; r++)
        {
            rows[r].Sort((a, b) => a.Position.x.CompareTo(b.Position.x));
            for (int c = 0; c < rows[r].Count; c++)
            {
                if (rows[r][c].Slot == current)
                {
                    currentRow = r;
                    currentCol = c;
                    currentX = rows[r][c].Position.x;
                }
            }
        }

        if (currentRow < 0)
        {
            return null;
        }

        if (direction == -2 || direction == 2)
        {
            List<SlotInfo> row = rows[currentRow];
            if (row.Count <= 1)
            {
                return null;
            }

            int nextCol = currentCol + (direction == 2 ? 1 : -1);
            if (nextCol < 0 || nextCol >= row.Count)
            {
                if (!wrap)
                {
                    return null;
                }

                nextCol = direction == 2 ? 0 : row.Count - 1;
                if (nextCol == currentCol)
                {
                    return null;
                }
            }

            return row[nextCol].Slot;
        }

        if (direction == -1 || direction == 1)
        {
            int nextRow = currentRow + (direction == 1 ? 1 : -1);
            if (nextRow < 0 || nextRow >= rows.Count)
            {
                if (!wrap)
                {
                    return null;
                }

                nextRow = direction == 1 ? 0 : rows.Count - 1;
                if (nextRow == currentRow)
                {
                    return null;
                }
            }

            List<SlotInfo> targetRow = rows[nextRow];
            if (targetRow.Count == 0)
            {
                return null;
            }

            SlotInfo best = targetRow[0];
            float bestDx = Mathf.Abs(best.Position.x - currentX);
            for (int i = 1; i < targetRow.Count; i++)
            {
                float dx = Mathf.Abs(targetRow[i].Position.x - currentX);
                if (dx < bestDx)
                {
                    bestDx = dx;
                    best = targetRow[i];
                }
            }

            return best.Slot;
        }

        return null;
    }

    private float GetSlotScreenHeight(ExpeditionSlotUI slot, Camera uiCamera)
    {
        if (slot == null || slot.SlotRect == null)
        {
            return 0f;
        }

        Vector3[] corners = new Vector3[4];
        slot.SlotRect.GetWorldCorners(corners);
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        for (int i = 0; i < corners.Length; i++)
        {
            float y = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]).y;
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }

        return Mathf.Max(0f, maxY - minY);
    }

    private ExpeditionSlotUI GetFocusedSlot()
    {
        if (currentFocusedSlot != null)
        {
            return currentFocusedSlot;
        }

        if (slots.Count > 0)
        {
            int clampedIndex = Mathf.Clamp(currentSlotIndex, 0, slots.Count - 1);
            FocusSlot(slots[clampedIndex]);
            return currentFocusedSlot;
        }

        Transform itemsParent = GetItemsParent();
        if (itemsParent == null)
        {
            return null;
        }

        ExpeditionSlotUI slot = itemsParent.GetComponentInChildren<ExpeditionSlotUI>(true);
        if (slot != null)
        {
            FocusSlot(slot);
        }

        return slot;
    }

    public void LaunchSelectedExpedition()
    {
        Expedition expedition = SelectedExpedition;
        if (expedition == null)
        {
            return;
        }

        if (!expedition.unlocked)
        {
            return;
        }

        GameObject target = expedition.FindLabyrinthRoot();
        if (target == null)
        {
            Debug.LogWarning($"ExpeditionPanelController: Labyrinthe introuvable pour {expedition.name}.");
            return;
        }
        if (useTransitionFade)
        {
            if (!isTransitioning)
            {
                StartCoroutine(RunExpeditionTransition(expedition, target));
            }
            return;
        }

        if (target != null)
        {
            if (deactivateOtherLabyrinths)
            {
                SetLabyrinthsActive(target);
            }
            else
            {
                target.SetActive(true);
            }

            PrepareLabyrinth(target);
            TeleportSquadToLabyrinth(target);
        }

        if (closeOnLaunch)
        {
            ClosePanel();
        }
    }

    private void SetSquadInputLock(bool locked)
    {
        if (SquadManager.Instance == null)
        {
            return;
        }

        if (locked)
        {
            if (squadInputLocked)
            {
                return;
            }

                    SquadManager.Instance.SetInputLocked(true);
            squadInputLocked = true;
            return;
        }

        if (!squadInputLocked)
        {
            return;
        }

        SquadManager.Instance.SetInputLocked(false);
        squadInputLocked = false;
    }

    private bool HasInputFocus()
    {
        return InputFocusStack.HasFocus(this);
    }

    private GameObject CreateInstance(GameObject source, Transform parent)
    {
        if (source == null)
        {
            return null;
        }

        if (parent != null)
        {
            return Instantiate(source, parent);
        }

        return Instantiate(source);
    }

    private void SetEntryText(GameObject entry, Expedition expedition)
    {
        if (entry == null)
        {
            return;
        }

        TextMeshProUGUI tmp = FindEntryNameText(entry);
        if (tmp == null)
        {
            return;
        }

        string name = expedition != null
            ? (!string.IsNullOrWhiteSpace(expedition.expeditionName) ? expedition.expeditionName : expedition.name)
            : string.Empty;

        tmp.text = name;
    }

    private TextMeshProUGUI FindEntryNameText(GameObject entry)
    {
        TextMeshProUGUI[] texts = entry.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts == null || texts.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI tmp = texts[i];
            if (tmp == null)
            {
                continue;
            }

            string name = tmp.name;
            if (name.IndexOf("name", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("title", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("label", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return tmp;
            }
        }

        return texts[0];
    }

    private void SetEntrySprite(GameObject entry, Expedition expedition)
    {
        if (entry == null)
        {
            return;
        }

        Image targetImage = FindEntrySpriteImage(entry);
        if (targetImage == null)
        {
            return;
        }

        Sprite sprite = expedition != null ? expedition.expeditionSprite : null;
        targetImage.sprite = sprite;
        targetImage.enabled = sprite != null;
    }

    private Image FindEntrySpriteImage(GameObject entry)
    {
        Image[] images = entry.GetComponentsInChildren<Image>(true);
        if (images == null || images.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            string name = image.name;
            if (name.IndexOf("icon", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("sprite", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }
        }

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            string name = image.name;
            if (name.IndexOf("frame", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("background", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("bg", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            return image;
        }

        return images[0];
    }

    private void SetLabyrinthsActive(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        for (int i = 0; i < expeditions.Count; i++)
        {
            Expedition expedition = expeditions[i];
            if (expedition == null)
            {
                continue;
            }

            GameObject root = expedition.FindLabyrinthRoot();
            if (root == null)
            {
                continue;
            }

            bool shouldBeActive = root == target;
            if (root.activeSelf != shouldBeActive)
            {
                root.SetActive(shouldBeActive);
            }
        }
    }

    private void PrepareLabyrinth(GameObject labyrinthRoot)
    {
        if (labyrinthRoot == null)
        {
            return;
        }

        if (!labyrinthRoot.activeSelf)
        {
            labyrinthRoot.SetActive(true);
        }

        Transform rootTransform = labyrinthRoot.transform;
        if (rootTransform.childCount > 0)
        {
            Transform firstChild = rootTransform.GetChild(0);
            if (firstChild != null && !firstChild.gameObject.activeSelf)
            {
                firstChild.gameObject.SetActive(true);
            }
        }
    }

    private IEnumerator RunExpeditionTransition(Expedition expedition, GameObject target)
    {
        isTransitioning = true;
        SetSquadInputLock(true);

        if (closeOnLaunch)
        {
            ClosePanel(true, true);
        }

        yield return FadeScreenTo(1f, fadeOutDuration);

        if (target != null)
        {
            if (deactivateOtherLabyrinths)
            {
                SetLabyrinthsActive(target);
            }
            else
            {
                target.SetActive(true);
            }

            PrepareLabyrinth(target);
            TeleportSquadToLabyrinth(target);
        }

        yield return FadeScreenTo(0f, fadeInDuration);

        FinalizePanelAfterTransition();
        SetSquadInputLock(false);
        isTransitioning = false;
    }

    private void FinalizePanelAfterTransition()
    {
        if (!suppressPanelDeactivate)
        {
            return;
        }

        suppressPanelDeactivate = false;
        if (deactivatePanelOnClose && expeditionPanel != null && !panelOpen)
        {
            expeditionPanel.SetActive(false);
        }
    }

    private void TeleportSquadToLabyrinth(GameObject labyrinthRoot)
    {
        if (labyrinthRoot == null || SquadManager.Instance == null)
        {
            return;
        }

        List<GameObject> squad = CollectSquadInstances();
        if (squad.Count == 0)
        {
            return;
        }

        Transform spawnPoint = FindLabyrinthSpawnPoint(labyrinthRoot);
        Vector3 basePosition = spawnPoint != null ? spawnPoint.position : labyrinthRoot.transform.position;
        Quaternion baseRotation = spawnPoint != null ? spawnPoint.rotation : labyrinthRoot.transform.rotation;
        basePosition += baseRotation * spawnPointOffset;

        for (int i = 0; i < squad.Count; i++)
        {
            GameObject character = squad[i];
            if (character == null)
            {
                continue;
            }

            Vector3 offset = GetFormationOffset(i);
            Vector3 worldOffset = baseRotation * offset;
            Vector3 finalPosition = basePosition + worldOffset;
            TeleportCharacter(character, finalPosition, baseRotation);
            SpawnTeleportVfx(finalPosition, baseRotation);
        }

        Physics.SyncTransforms();
    }

    private List<GameObject> CollectSquadInstances()
    {
        List<GameObject> results = new List<GameObject>();
        SquadManager manager = SquadManager.Instance;
        if (manager == null)
        {
            return results;
        }

        if (manager.squadCharacters != null)
        {
            for (int i = 0; i < manager.squadCharacters.Count; i++)
            {
                GameObject instance = manager.squadCharacters[i];
                if (instance != null && !results.Contains(instance))
                {
                    results.Add(instance);
                }
            }
        }

        if (results.Count == 0 && manager.currentSquad != null)
        {
            for (int i = 0; i < manager.currentSquad.Count; i++)
            {
                CharacterData data = manager.currentSquad[i];
                if (data == null)
                {
                    continue;
                }

                GameObject instance = manager.GetCharacterInstance(data);
                if (instance != null && !results.Contains(instance))
                {
                    results.Add(instance);
                }
            }
        }

        if (results.Count == 0)
        {
            try
            {
                GameObject[] tagged = GameObject.FindGameObjectsWithTag("Player");
                for (int i = 0; i < tagged.Length; i++)
                {
                    GameObject instance = tagged[i];
                    if (instance != null && instance.GetComponent<SquadCharacterController>() != null && !results.Contains(instance))
                    {
                        results.Add(instance);
                    }
                }
            }
            catch (UnityException)
            {
                // Tag missing, ignore.
            }
        }

        return results;
    }

    private Vector3 GetFormationOffset(int index)
    {
        if (index <= 0)
        {
            return Vector3.zero;
        }

        float angle = (index - 1) * 60f * Mathf.Deg2Rad;
        float radius = Mathf.Max(0f, spawnSpreadRadius);
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
    }

    private void TeleportCharacter(GameObject character, Vector3 position, Quaternion rotation)
    {
        if (character == null)
        {
            return;
        }

        Rigidbody rb = character.GetComponent<Rigidbody>();
        CharacterController controller = character.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        if (rb != null)
        {
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        character.transform.SetPositionAndRotation(position, rotation);

        if (controller != null)
        {
            controller.enabled = true;
        }

        SquadCharacterController squadController = character.GetComponent<SquadCharacterController>();
        if (squadController != null)
        {
            squadController.Stop();
        }
    }

    private void SpawnTeleportVfx(Vector3 position, Quaternion rotation)
    {
        if (teleportVfxPrefab == null)
        {
            return;
        }

        Transform parent = teleportVfxParent != null ? teleportVfxParent : null;
        GameObject instance = Instantiate(teleportVfxPrefab, position + teleportVfxOffset, rotation, parent);
        if (teleportVfxLifetime > 0f)
        {
            Destroy(instance, teleportVfxLifetime);
        }
    }

    private Transform FindLabyrinthSpawnPoint(GameObject labyrinthRoot)
    {
        if (labyrinthRoot == null)
        {
            return null;
        }

        Transform[] children = labyrinthRoot.GetComponentsInChildren<Transform>(true);
        if (children == null || children.Length == 0)
        {
            return null;
        }

        const string spawnPointTag = "SpawnPoint";
        bool tagValid = true;
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null)
            {
                continue;
            }

            if (!tagValid)
            {
                break;
            }

            try
            {
                if (child.CompareTag(spawnPointTag))
                {
                    return child;
                }
            }
            catch (UnityException)
            {
                tagValid = false;
                Debug.LogWarning($"ExpeditionPanelController: tag introuvable \"{spawnPointTag}\".");
            }
        }

        return labyrinthRoot.transform;
    }

    private IEnumerator FadeScreenTo(float targetAlpha, float duration)
    {
        CanvasGroup canvasGroup = GetScreenFadeCanvasGroup();
        if (canvasGroup == null)
        {
            yield break;
        }

        if (!canvasGroup.gameObject.activeSelf)
        {
            canvasGroup.gameObject.SetActive(true);
        }

        if (screenFadeBlocksRaycasts)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        float startAlpha = canvasGroup.alpha;
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = duration > 0f ? Mathf.Clamp01(time / duration) : 1f;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        if (screenFadeBlocksRaycasts)
        {
            bool visible = targetAlpha > 0.001f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }
    }

    private CanvasGroup GetScreenFadeCanvasGroup()
    {
        if (screenFadeCanvasGroup != null)
        {
            return screenFadeCanvasGroup;
        }

        if (screenFadePanel == null && createScreenFadeIfMissing)
        {
            screenFadePanel = CreateScreenFadePanel();
        }

        if (screenFadePanel == null)
        {
            return null;
        }

        screenFadeCanvasGroup = screenFadePanel.GetComponent<CanvasGroup>();
        if (screenFadeCanvasGroup == null)
        {
            screenFadeCanvasGroup = screenFadePanel.AddComponent<CanvasGroup>();
        }

        Image image = screenFadePanel.GetComponent<Image>();
        if (image != null)
        {
            image.color = screenFadeColor;
            image.raycastTarget = screenFadeBlocksRaycasts;
        }

        screenFadePanel.transform.SetAsLastSibling();

        return screenFadeCanvasGroup;
    }

    private GameObject CreateScreenFadePanel()
    {
        Canvas canvas = expeditionPanel != null
            ? expeditionPanel.GetComponentInParent<Canvas>()
            : GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return null;
        }

        GameObject panel = new GameObject("ScreenFade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = panel.GetComponent<Image>();
        image.color = screenFadeColor;
        image.raycastTarget = screenFadeBlocksRaycasts;

        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        panel.transform.SetAsLastSibling();
        return panel;
    }

    private readonly struct SlotInfo
    {
        public SlotInfo(ExpeditionSlotUI slot, Vector2 position)
        {
            Slot = slot;
            Position = position;
        }

        public ExpeditionSlotUI Slot { get; }
        public Vector2 Position { get; }
    }
}

// Slot UI d'une expedition (selection par curseur/pointeur).
public class ExpeditionSlotUI : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public ExpeditionPanelController Owner { get; private set; }
    public Expedition Expedition { get; private set; }
    public RectTransform SlotRect { get; private set; }

    public void Initialize(ExpeditionPanelController owner, Expedition expedition)
    {
        Owner = owner;
        Expedition = expedition;
        SlotRect = GetComponent<RectTransform>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Owner != null)
        {
            Owner.FocusSlot(this);
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (Owner != null)
        {
            Owner.FocusSlot(this);
        }
    }
}
