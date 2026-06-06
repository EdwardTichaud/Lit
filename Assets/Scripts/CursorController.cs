using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// Controle un curseur UI dans un layout (Grid/Vertical/Horizontal) avec l'input.
[DisallowMultipleComponent]
public class CursorController : MonoBehaviour
{
    public enum CursorPlacement
    {
        Overlay = 0,
        RightOfTarget = 1
    }
    public enum LayoutFallback
    {
        None = 0,
        Vertical = 1,
        Horizontal = 2,
        Grid = 3
    }
    public enum ItemFilter
    {
        All = 0,
        MenuCursorActionOnly = 1,
        MenuCursorHandlerOnly = 2
    }
    private enum LayoutKind
    {
        Grid,
        Vertical,
        Horizontal,
        Unknown
    }

    [Header("Layout")]
    [Tooltip("LayoutGroup a utiliser (GridLayoutGroup, VerticalLayoutGroup ou HorizontalLayoutGroup).")]
    public LayoutGroup layoutGroup;
    [Tooltip("Parent des elements a naviguer (utilise si layoutGroup est nul).")]
    public RectTransform itemsParent;
    [Tooltip("Layout a utiliser si aucun LayoutGroup n'est present.")]
    public LayoutFallback fallbackLayout = LayoutFallback.None;
    [Tooltip("Reconstruit la liste si les enfants changent.")]
    public bool autoCollectItems = true;
    [Tooltip("Inclut les objets inactifs dans la navigation.")]
    public bool includeInactive = false;
    [Tooltip("Filtre les elements navigables.")]
    public ItemFilter itemFilter = ItemFilter.All;

    [Header("Cursor")]
    [Tooltip("Curseur UI de selection.")]
    public RectTransform cursor;
    [Tooltip("Parent force du curseur (optionnel).")]
    public RectTransform cursorParentOverride;
    [Tooltip("Padding ajoute autour de l'element selectionne.")]
    public Vector2 cursorPadding = new Vector2(0f, 0f);
    [Tooltip("Positionnement du curseur par rapport a la cible.")]
    public CursorPlacement placement = CursorPlacement.Overlay;
    [Tooltip("Decalage ajoute quand le curseur est a droite de la cible.")]
    public Vector2 rightOffset = new Vector2(20f, 0f);
    [Tooltip("Utilise la largeur du texte (TMP) pour placer le curseur a droite.")]
    public bool useTextBoundsForRightOffset = false;
    [Tooltip("Texte TMP a utiliser pour le placement a droite. Laisse vide pour detection automatique.")]
    public TMP_Text rightPlacementTextOverride;
    [Tooltip("Marge ajoutee apres le texte quand le curseur est place a droite.")]
    public float rightTextMargin = 20f;
    [Tooltip("Ajuste la taille du curseur a celle de la cible.")]
    public bool matchTargetSize = true;

    [Header("Audio")]
    [Tooltip("SFX joue quand le curseur se deplace.")]
    public AudioClipSO moveSfx;
    [Tooltip("Temps minimum entre deux sons de deplacement.")]
    public float moveSfxCooldown = 0.05f;

    [Header("Navigation")]
    [Tooltip("Deadzone du stick pour naviguer.")]
    public float moveDeadzone = 0.5f;
    [Tooltip("Delai avant repetition.")]
    public float initialRepeatDelay = 0.35f;
    [Tooltip("Intervalle de repetition.")]
    public float repeatInterval = 0.12f;
    [Tooltip("Autorise le wrap du curseur.")]
    public bool wrap = false;

    [Header("Input")]
    [Tooltip("InputAction de mouvement (optionnel).")]
    public InputActionReference moveActionReference;
    [Tooltip("Autorise la navigation via input.")]
    public bool allowInput = true;
    [Tooltip("Desactive l'InputAction externe a la desactivation.")]
    public bool disableExternalActionOnDisable = false;
    [Tooltip("Utilise Time.unscaledTime.")]
    public bool useUnscaledTime = true;

    [Header("Selection")]
    [Tooltip("Index de depart si aucun element n'est selectionne.")]
    public int startIndex = 0;
    [Tooltip("Force la selection du premier element a l'ouverture.")]
    public bool resetToFirstOnEnable = true;

    [Header("Cursor Motion")]
    [Tooltip("Active un lerp pour adoucir le deplacement.")]
    public bool smoothCursor = true;
    [Tooltip("Vitesse de lerp de position.")]
    public float cursorPositionLerpSpeed = 18f;
    [Tooltip("Vitesse de lerp de taille.")]
    public float cursorSizeLerpSpeed = 18f;

    private InputAction moveAction;
    private bool usingExternalAction;
    private Vector2 cachedMoveInput;
    private readonly List<RectTransform> items = new List<RectTransform>();
    private int currentIndex = -1;
    private int lastMoveDirection;
    private float nextMoveTime;
    private bool cursorDirty;
    private int cachedChildCount = -1;
    private RectTransform cachedParent;
    private LayoutKind layoutKind = LayoutKind.Unknown;
    private Vector3 cursorTargetPosition;
    private Vector2 cursorTargetSize;
    private bool cursorHasTarget;
    private bool cursorInitialized;
    private float lastMoveSfxTime = -999f;
    private bool cursorVisualVisible;
    private bool cursorParticleRestartPending;
    private RectTransform lastParticleTarget;
    private void Awake()
    {
        ResolveLayout();
        RebuildItems();
        EnsureSelection();
        EnsureCursor();
        SnapCursorImmediate();
    }

    private void OnEnable()
    {
        ResolveLayout();
        SetupInput();
        RebuildItems();
        if (resetToFirstOnEnable)
        {
            currentIndex = -1;
        }
        EnsureSelection();
        SnapCursorImmediate();
    }

    private void OnDisable()
    {
        if (usingExternalAction)
        {
            if (disableExternalActionOnDisable && moveAction != null)
            {
                moveAction.Disable();
            }
        }
        else
        {
            LocalInputRouter.Move -= OnMoveChanged;
        }
    }

    private void Update()
    {
        if (usingExternalAction && moveAction == null)
        {
            return;
        }

        if (autoCollectItems)
        {
            UpdateItemsIfNeeded();
        }

        if (items.Count == 0)
        {
            HideCursor();
            return;
        }

        if (allowInput)
        {
            HandleNavigation();
        }
    }

    private void LateUpdate()
    {
        if (cursorDirty)
        {
            UpdateCursorVisual();
        }

        if (smoothCursor && cursorHasTarget && cursor != null && cursor.gameObject.activeSelf)
        {
            StepCursorLerp();
        }
    }

    public void Refresh()
    {
        ResolveLayout();
        RebuildItems();
        EnsureSelection();
        SnapCursorImmediate();
    }

    public int CurrentIndex => currentIndex;

    public RectTransform CurrentItem => currentIndex >= 0 && currentIndex < items.Count ? items[currentIndex] : null;

    public bool TrySetCurrentItem(RectTransform target, bool rebuildItems = true)
    {
        if (target == null)
        {
            return false;
        }

        if (rebuildItems)
        {
            ResolveLayout();
            RebuildItems();
            EnsureSelection();
        }

        int index = items.IndexOf(target);
        if (index < 0)
        {
            return false;
        }

        if (currentIndex != index)
        {
            currentIndex = index;
            cursorDirty = true;
        }

        return true;
    }

    public bool SelectFirst()
    {
        ResolveLayout();
        RebuildItems();
        if (items.Count == 0)
        {
            currentIndex = -1;
            HideCursor();
            return false;
        }

        currentIndex = 0;
        cursorDirty = true;
        return true;
    }

    private void ResolveLayout()
    {
        if (layoutGroup == null && itemsParent != null)
        {
            layoutGroup = itemsParent.GetComponent<LayoutGroup>();
        }

        if (layoutGroup == null)
        {
            layoutKind = ResolveFallbackLayout();
            return;
        }

        if (layoutGroup is GridLayoutGroup)
        {
            layoutKind = LayoutKind.Grid;
        }
        else if (layoutGroup is VerticalLayoutGroup)
        {
            layoutKind = LayoutKind.Vertical;
        }
        else if (layoutGroup is HorizontalLayoutGroup)
        {
            layoutKind = LayoutKind.Horizontal;
        }
        else
        {
            layoutKind = LayoutKind.Unknown;
        }
    }

    private LayoutKind ResolveFallbackLayout()
    {
        switch (fallbackLayout)
        {
            case LayoutFallback.Vertical:
                return LayoutKind.Vertical;
            case LayoutFallback.Horizontal:
                return LayoutKind.Horizontal;
            case LayoutFallback.Grid:
                return LayoutKind.Grid;
            default:
                return LayoutKind.Unknown;
        }
    }

    private void SetupInput()
    {
        if (moveActionReference != null && moveActionReference.action != null)
        {
            moveAction = moveActionReference.action;
            usingExternalAction = true;
            if (!moveAction.enabled)
            {
                moveAction.Enable();
            }
            return;
        }

        usingExternalAction = false;
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Move += OnMoveChanged;
        moveAction = null;
    }

    private RectTransform GetItemsParent()
    {
        if (itemsParent != null)
        {
            return itemsParent;
        }

        return layoutGroup != null ? layoutGroup.transform as RectTransform : null;
    }

    private RectTransform GetCursorParent()
    {
        if (cursorParentOverride != null)
        {
            return cursorParentOverride;
        }

        RectTransform parent = GetItemsParent();
        if (parent != null && parent.parent is RectTransform parentRect)
        {
            return parentRect;
        }

        return parent;
    }

    private void UpdateItemsIfNeeded()
    {
        RectTransform parent = GetItemsParent();
        int childCount = parent != null ? parent.childCount : 0;
        if (parent != cachedParent || childCount != cachedChildCount)
        {
            RebuildItems();
            EnsureSelection();
            SnapCursorImmediate();
        }
    }

    private void RebuildItems()
    {
        items.Clear();

        RectTransform parent = GetItemsParent();
        cachedParent = parent;
        cachedChildCount = parent != null ? parent.childCount : -1;

        if (parent == null)
        {
            return;
        }

        if (CollectDirectItems(parent, items) > 0)
        {
            return;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            CollectNestedItems(parent.GetChild(i) as RectTransform, items);
        }
    }

    private int CollectDirectItems(RectTransform parent, List<RectTransform> destination)
    {
        if (parent == null || destination == null)
        {
            return 0;
        }

        int startCount = destination.Count;
        for (int i = 0; i < parent.childCount; i++)
        {
            RectTransform rect = parent.GetChild(i) as RectTransform;
            if (!ShouldIncludeItem(rect))
            {
                continue;
            }

            destination.Add(rect);
        }

        return destination.Count - startCount;
    }

    private void CollectNestedItems(RectTransform root, List<RectTransform> destination)
    {
        if (root == null || destination == null)
        {
            return;
        }

        if (ShouldIncludeItem(root))
        {
            destination.Add(root);
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            CollectNestedItems(root.GetChild(i) as RectTransform, destination);
        }
    }

    private bool ShouldIncludeItem(RectTransform rect)
    {
        if (rect == null)
        {
            return false;
        }

        if (!includeInactive && !rect.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (cursor != null && rect == cursor)
        {
            return false;
        }

        return PassesItemFilter(rect);
    }

    private bool PassesItemFilter(RectTransform rect)
    {
        if (rect == null)
        {
            return false;
        }

        switch (itemFilter)
        {
            case ItemFilter.MenuCursorActionOnly:
                return rect.GetComponent<MenuCursorAction>() != null;
            case ItemFilter.MenuCursorHandlerOnly:
                return HasMenuCursorHandler(rect);
            default:
                return true;
        }
    }

    private static bool HasMenuCursorHandler(RectTransform rect)
    {
        if (rect == null)
        {
            return false;
        }

        MenuCursorAction action = rect.GetComponent<MenuCursorAction>();
        if (action != null)
        {
            return action.isActiveAndEnabled;
        }

        MonoBehaviour[] behaviours = rect.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i].isActiveAndEnabled && behaviours[i] is IMenuCursorHandler)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureSelection()
    {
        if (items.Count == 0)
        {
            currentIndex = -1;
            return;
        }

        if (currentIndex < 0)
        {
            currentIndex = Mathf.Clamp(startIndex, 0, items.Count - 1);
            return;
        }

        if (currentIndex >= items.Count)
        {
            currentIndex = Mathf.Clamp(currentIndex, 0, items.Count - 1);
        }
    }

    private void HandleNavigation()
    {
        Vector2 moveInput = usingExternalAction && moveAction != null ? moveAction.ReadValue<Vector2>() : cachedMoveInput;
        int direction = GetMoveDirection(moveInput, moveDeadzone);
        if (direction == 0)
        {
            lastMoveDirection = 0;
            nextMoveTime = 0f;
            return;
        }

        if (!AllowsDirection(direction))
        {
            lastMoveDirection = 0;
            nextMoveTime = 0f;
            return;
        }

        float now = useUnscaledTime ? Time.unscaledTime : Time.time;
        if (direction != lastMoveDirection)
        {
            MoveSelection(direction);
            lastMoveDirection = direction;
            nextMoveTime = now + initialRepeatDelay;
            return;
        }

        if (now >= nextMoveTime)
        {
            MoveSelection(direction);
            nextMoveTime = now + repeatInterval;
        }
    }

    private void OnMoveChanged(Vector2 value)
    {
        cachedMoveInput = value;
    }

    private bool AllowsDirection(int direction)
    {
        bool horizontal = direction == 2 || direction == -2;
        bool vertical = direction == 1 || direction == -1;

        if (layoutKind == LayoutKind.Vertical && horizontal)
        {
            return false;
        }

        if (layoutKind == LayoutKind.Horizontal && vertical)
        {
            return false;
        }

        return true;
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

    private void MoveSelection(int direction)
    {
        if (items.Count == 0)
        {
            return;
        }

        int nextIndex = GetNextIndex(direction);
        if (nextIndex == currentIndex || nextIndex < 0 || nextIndex >= items.Count)
        {
            return;
        }

        currentIndex = nextIndex;
        cursorDirty = true;
        PlayMoveSfx();
    }

    private void PlayMoveSfx()
    {
        if (moveSfx == null || moveSfx.audioClip == null)
        {
            return;
        }

        float now = useUnscaledTime ? Time.unscaledTime : Time.time;
        if (now - lastMoveSfxTime < Mathf.Max(0f, moveSfxCooldown))
        {
            return;
        }

        lastMoveSfxTime = now;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClip(moveSfx, Vector3.zero);
        }
        else
        {
            AudioSource.PlayClipAtPoint(moveSfx.audioClip, Vector3.zero, Mathf.Clamp01(moveSfx.volume));
        }
    }

    private int GetNextIndex(int direction)
    {
        if (currentIndex < 0 || currentIndex >= items.Count)
        {
            return -1;
        }

        if (layoutKind == LayoutKind.Vertical)
        {
            int next = currentIndex + (direction == 1 ? 1 : -1);
            if (next < 0 || next >= items.Count)
            {
                if (!wrap)
                {
                    return currentIndex;
                }

                next = direction == 1 ? 0 : items.Count - 1;
            }
            return next;
        }

        if (layoutKind == LayoutKind.Horizontal)
        {
            int next = currentIndex + (direction == 2 ? 1 : -1);
            if (next < 0 || next >= items.Count)
            {
                if (!wrap)
                {
                    return currentIndex;
                }

                next = direction == 2 ? 0 : items.Count - 1;
            }
            return next;
        }

        return GetNextGridIndex(direction);
    }

    private int GetNextGridIndex(int direction)
    {
        int itemCount = items.Count;
        if (itemCount == 0)
        {
            return -1;
        }

        int columns = Mathf.Max(1, GetGridColumns(itemCount));
        int rows = Mathf.CeilToInt(itemCount / (float)columns);
        int row = currentIndex / columns;
        int col = currentIndex % columns;

        if (direction == 2 || direction == -2)
        {
            int rowStart = row * columns;
            int rowEnd = Mathf.Min(rowStart + columns, itemCount) - 1;
            int rowLength = Mathf.Max(1, rowEnd - rowStart + 1);
            int colInRow = Mathf.Clamp(col, 0, rowLength - 1);
            int nextCol = colInRow + (direction == 2 ? 1 : -1);

            if (nextCol < 0 || nextCol >= rowLength)
            {
                if (!wrap)
                {
                    return currentIndex;
                }

                nextCol = direction == 2 ? 0 : rowLength - 1;
            }

            return rowStart + nextCol;
        }

        if (direction == 1 || direction == -1)
        {
            int nextRow = row + (direction == 1 ? 1 : -1);
            if (nextRow < 0 || nextRow >= rows)
            {
                if (!wrap)
                {
                    return currentIndex;
                }

                nextRow = direction == 1 ? 0 : rows - 1;
            }

            int targetRowStart = nextRow * columns;
            int targetRowEnd = Mathf.Min(targetRowStart + columns, itemCount) - 1;
            if (targetRowEnd < targetRowStart)
            {
                return currentIndex;
            }

            int targetRowLength = targetRowEnd - targetRowStart + 1;
            int targetCol = Mathf.Clamp(col, 0, targetRowLength - 1);
            return targetRowStart + targetCol;
        }

        return currentIndex;
    }

    private int GetGridColumns(int itemCount)
    {
        GridLayoutGroup grid = layoutGroup as GridLayoutGroup;
        if (grid == null)
        {
            return Mathf.Max(1, itemCount);
        }

        if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            return Mathf.Max(1, grid.constraintCount);
        }

        if (grid.constraint == GridLayoutGroup.Constraint.FixedRowCount)
        {
            int rows = Mathf.Max(1, grid.constraintCount);
            return Mathf.Max(1, Mathf.CeilToInt(itemCount / (float)rows));
        }

        RectTransform rect = grid.transform as RectTransform;
        float width = rect != null ? rect.rect.width : 0f;
        float available = width - grid.padding.left - grid.padding.right;
        float cellWidth = grid.cellSize.x + grid.spacing.x;

        if (cellWidth <= 0f || available <= 0f)
        {
            return Mathf.Max(1, itemCount);
        }

        int columns = Mathf.FloorToInt((available + grid.spacing.x) / cellWidth);
        return Mathf.Max(1, Mathf.Min(columns, itemCount));
    }

    private void UpdateCursorVisual()
    {
        RectTransform target = CurrentItem;
        if (target == null)
        {
            HideCursor();
            cursorDirty = false;
            cursorHasTarget = false;
            cursorInitialized = false;
            return;
        }

        RectTransform rect = EnsureCursor();
        if (rect == null)
        {
            cursorDirty = false;
            return;
        }

        RectTransform parent = GetCursorParent();
        if (parent != null && rect.parent != parent)
        {
            rect.SetParent(parent, false);
        }

        ForceLayoutRebuild();
        rect.gameObject.SetActive(true);
        SetCursorGraphicsActive(true);
        Vector2 targetSize = target.rect.size;
        Vector2 baseSize = matchTargetSize ? targetSize : rect.rect.size;
        cursorTargetSize = baseSize + cursorPadding;

        if (placement == CursorPlacement.RightOfTarget)
        {
            rect.pivot = new Vector2(0f, 0.5f);
            if (useTextBoundsForRightOffset && TryGetTextBounds(target, out Vector3 textCenter, out float textHalfWidth, out Vector3 textRight, out Vector3 textUp))
            {
                float offset = textHalfWidth + ResolveRightTextMargin();
                cursorTargetPosition = textCenter + textRight * offset + textUp * rightOffset.y;
            }
            else
            {
                Vector3 right = target.right;
                Vector3 up = target.up;
                float halfTarget = targetSize.x * 0.5f;
                float offset = halfTarget + rightOffset.x;
                cursorTargetPosition = target.position + right * offset + up * rightOffset.y;
            }
        }
        else
        {
            rect.pivot = new Vector2(0.5f, 0.5f);
            cursorTargetPosition = target.position;
        }
        cursorHasTarget = true;

        if (!smoothCursor || !cursorInitialized)
        {
            ApplyCursorImmediate(rect, cursorTargetPosition, cursorTargetSize);
        }

        bool targetChanged = lastParticleTarget != target;
        bool becameVisible = !cursorVisualVisible;
        if (targetChanged || becameVisible)
        {
            QueueCursorParticleRestart(target);
        }

        cursorVisualVisible = true;
        lastParticleTarget = target;

        cursorDirty = false;
    }

    private void HideCursor()
    {
        cursorVisualVisible = false;
        cursorParticleRestartPending = false;

        if (cursor != null)
        {
            if (cursor == transform as RectTransform)
            {
                SetCursorGraphicsActive(false);
                return;
            }

            cursor.gameObject.SetActive(false);
        }
    }

    private void SetCursorGraphicsActive(bool visible)
    {
        if (cursor == null)
        {
            return;
        }

        Graphic[] graphics = cursor.GetComponentsInChildren<Graphic>(true);
        if (graphics == null)
        {
            return;
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == null)
            {
                continue;
            }

            graphics[i].enabled = visible;
        }
    }

    private bool TryGetTextBounds(RectTransform target, out Vector3 centerWorld, out float halfWidth, out Vector3 right, out Vector3 up)
    {
        centerWorld = Vector3.zero;
        halfWidth = 0f;
        right = Vector3.right;
        up = Vector3.up;

        if (target == null)
        {
            return false;
        }

        Vector3 scoreAxis = target.right.sqrMagnitude > 0.0001f ? target.right.normalized : Vector3.right;
        float bestScore = float.NegativeInfinity;
        bool found = false;

        TMP_Text preferredText = ResolvePreferredTextTarget(target);
        if (TryAssignBestTextBounds(preferredText, scoreAxis, ref bestScore, ref found, ref centerWorld, ref halfWidth, ref right, ref up))
        {
            return true;
        }

        TMP_Text[] texts = target.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TryAssignBestTextBounds(texts[i], scoreAxis, ref bestScore, ref found, ref centerWorld, ref halfWidth, ref right, ref up);
        }

        return found;
    }

    private TMP_Text ResolvePreferredTextTarget(RectTransform target)
    {
        if (rightPlacementTextOverride != null)
        {
            return rightPlacementTextOverride;
        }

        if (target == null)
        {
            return null;
        }

        string expectedName = target.name + "_Text";
        TMP_Text[] texts = target.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text tmp = texts[i];
            if (tmp != null && string.Equals(tmp.gameObject.name, expectedName, System.StringComparison.Ordinal))
            {
                return tmp;
            }
        }

        TMP_Text directText = target.GetComponent<TMP_Text>();
        if (directText != null)
        {
            return directText;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text tmp = texts[i];
            if (tmp != null && tmp.transform.parent == target)
            {
                return tmp;
            }
        }

        return texts.Length > 0 ? texts[0] : null;
    }

    private float ResolveRightTextMargin()
    {
        return rightTextMargin;
    }

    private static bool TryAssignBestTextBounds(
        TMP_Text tmp,
        Vector3 scoreAxis,
        ref float bestScore,
        ref bool found,
        ref Vector3 bestCenterWorld,
        ref float bestHalfWidth,
        ref Vector3 bestRight,
        ref Vector3 bestUp)
    {
        if (!TryGetRenderedTextBounds(tmp, out Vector3 centerWorld, out float halfWidth, out Vector3 right, out Vector3 up))
        {
            return false;
        }

        float score = Vector3.Dot(centerWorld, scoreAxis) + halfWidth;
        if (found && score <= bestScore)
        {
            return false;
        }

        bestScore = score;
        bestCenterWorld = centerWorld;
        bestHalfWidth = halfWidth;
        bestRight = right;
        bestUp = up;
        found = true;
        return true;
    }

    private static bool TryGetRenderedTextBounds(TMP_Text tmp, out Vector3 centerWorld, out float halfWidth, out Vector3 right, out Vector3 up)
    {
        centerWorld = Vector3.zero;
        halfWidth = 0f;
        right = Vector3.right;
        up = Vector3.up;

        if (tmp == null)
        {
            return false;
        }

        tmp.ForceMeshUpdate(true, true);

        TMP_TextInfo textInfo = tmp.textInfo;
        bool hasVisibleCharacters = false;
        float minX = 0f;
        float maxX = 0f;
        float minY = 0f;
        float maxY = 0f;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = textInfo.characterInfo[i];
            if (!character.isVisible)
            {
                continue;
            }

            Vector3 bottomLeft = character.bottomLeft;
            Vector3 topRight = character.topRight;
            if (!hasVisibleCharacters)
            {
                minX = bottomLeft.x;
                maxX = topRight.x;
                minY = bottomLeft.y;
                maxY = topRight.y;
                hasVisibleCharacters = true;
                continue;
            }

            minX = Mathf.Min(minX, bottomLeft.x);
            maxX = Mathf.Max(maxX, topRight.x);
            minY = Mathf.Min(minY, bottomLeft.y);
            maxY = Mathf.Max(maxY, topRight.y);
        }

        if (!hasVisibleCharacters)
        {
            Bounds bounds = tmp.textBounds;
            if (bounds.size == Vector3.zero)
            {
                return false;
            }

            Vector3 localLeft = new Vector3(bounds.min.x, bounds.center.y, bounds.center.z);
            Vector3 localRight = new Vector3(bounds.max.x, bounds.center.y, bounds.center.z);
            Vector3 worldLeft = tmp.transform.TransformPoint(localLeft);
            Vector3 worldRight = tmp.transform.TransformPoint(localRight);
            Vector3 worldSpan = worldRight - worldLeft;

            centerWorld = (worldLeft + worldRight) * 0.5f;
            halfWidth = worldSpan.magnitude * 0.5f;
            right = worldSpan.sqrMagnitude > 0.0001f ? worldSpan.normalized : tmp.transform.right;
            up = tmp.transform.up;
            return halfWidth > 0.0001f;
        }

        Vector3 localCenter = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
        Vector3 localLeftVisible = new Vector3(minX, localCenter.y, 0f);
        Vector3 localRightVisible = new Vector3(maxX, localCenter.y, 0f);
        Vector3 worldLeftVisible = tmp.transform.TransformPoint(localLeftVisible);
        Vector3 worldRightVisible = tmp.transform.TransformPoint(localRightVisible);
        Vector3 visibleSpan = worldRightVisible - worldLeftVisible;

        centerWorld = (worldLeftVisible + worldRightVisible) * 0.5f;
        halfWidth = visibleSpan.magnitude * 0.5f;
        right = visibleSpan.sqrMagnitude > 0.0001f ? visibleSpan.normalized : tmp.transform.right;
        up = tmp.transform.up;
        return halfWidth > 0.0001f;
    }

    private RectTransform EnsureCursor()
    {
        return cursor;
    }

    private void SnapCursorImmediate()
    {
        cursorInitialized = false;
        cursorDirty = true;
        ForceLayoutRebuild();
        if (isActiveAndEnabled)
        {
            UpdateCursorVisual();
        }
    }

    private void ForceLayoutRebuild()
    {
        RectTransform parent = GetItemsParent();
        if (parent == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
    }

    private void ApplyCursorImmediate(RectTransform rect, Vector3 position, Vector2 size)
    {
        if (rect == null)
        {
            return;
        }

        rect.position = position;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        cursorInitialized = true;
    }

    private void StepCursorLerp()
    {
        if (cursor == null)
        {
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        float posT = 1f - Mathf.Exp(-Mathf.Max(0f, cursorPositionLerpSpeed) * deltaTime);
        cursor.position = Vector3.Lerp(cursor.position, cursorTargetPosition, posT);

        float sizeT = 1f - Mathf.Exp(-Mathf.Max(0f, cursorSizeLerpSpeed) * deltaTime);
        Vector2 currentSize = cursor.rect.size;
        Vector2 lerpedSize = Vector2.Lerp(currentSize, cursorTargetSize, sizeT);
        cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, lerpedSize.x);
        cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, lerpedSize.y);

        if (cursorParticleRestartPending && (cursor.position - cursorTargetPosition).sqrMagnitude <= 1f)
        {
            RestartCursorChildParticleSystems();
            cursorParticleRestartPending = false;
        }
    }

    private void QueueCursorParticleRestart(RectTransform target)
    {
        if (target == null)
        {
            cursorParticleRestartPending = false;
            return;
        }

        if (!smoothCursor || !cursorInitialized || cursor == null || (cursor.position - cursorTargetPosition).sqrMagnitude <= 1f)
        {
            RestartCursorChildParticleSystems();
            cursorParticleRestartPending = false;
            return;
        }

        cursorParticleRestartPending = true;
    }

    private void RestartCursorChildParticleSystems()
    {
        if (cursor == null)
        {
            return;
        }

        ParticleSystem[] particleSystems = cursor.GetComponentsInChildren<ParticleSystem>(true);
        if (particleSystems == null || particleSystems.Length == 0)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem system = particleSystems[i];
            if (system == null || !system.gameObject.activeInHierarchy)
            {
                continue;
            }

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Clear(true);
            system.Play(true);
        }
    }
}
