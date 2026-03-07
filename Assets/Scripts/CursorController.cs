using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Controle un curseur UI dans un layout (Grid/Vertical/Horizontal) avec l'input.
[DisallowMultipleComponent]
public class CursorController : MonoBehaviour
{
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
    [Tooltip("Reconstruit la liste si les enfants changent.")]
    public bool autoCollectItems = true;
    [Tooltip("Inclut les objets inactifs dans la navigation.")]
    public bool includeInactive = false;

    [Header("Cursor")]
    [Tooltip("Curseur UI de selection.")]
    public RectTransform cursor;
    [Tooltip("Parent force du curseur (optionnel).")]
    public RectTransform cursorParentOverride;
    [Tooltip("Padding ajoute autour de l'element selectionne.")]
    public Vector2 cursorPadding = new Vector2(0f, 0f);
    [Tooltip("Cree un curseur si manquant.")]
    public bool createCursorIfMissing = true;

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
    private bool isUpdatingCursor;
    private Vector3 cursorTargetPosition;
    private Vector2 cursorTargetSize;
    private bool cursorHasTarget;
    private bool cursorInitialized;

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

    private void ResolveLayout()
    {
        if (layoutGroup == null && itemsParent != null)
        {
            layoutGroup = itemsParent.GetComponent<LayoutGroup>();
        }

        if (layoutGroup == null)
        {
            layoutKind = LayoutKind.Unknown;
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

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (!includeInactive && !child.gameObject.activeInHierarchy)
            {
                continue;
            }

            RectTransform rect = child as RectTransform;
            if (rect == null)
            {
                continue;
            }

            if (cursor != null && rect == cursor)
            {
                continue;
            }

            items.Add(rect);
        }
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
        isUpdatingCursor = true;
        RectTransform target = CurrentItem;
        if (target == null)
        {
            HideCursor();
            cursorDirty = false;
            cursorHasTarget = false;
            cursorInitialized = false;
            isUpdatingCursor = false;
            return;
        }

        RectTransform rect = EnsureCursor();
        if (rect == null)
        {
            cursorDirty = false;
            isUpdatingCursor = false;
            return;
        }

        RectTransform parent = GetCursorParent();
        if (parent != null && rect.parent != parent)
        {
            rect.SetParent(parent, false);
        }

        ForceLayoutRebuild();
        rect.gameObject.SetActive(true);
        Vector2 size = target.rect.size;
        cursorTargetPosition = target.position;
        cursorTargetSize = size + cursorPadding;
        cursorHasTarget = true;
        rect.pivot = new Vector2(0.5f, 0.5f);

        if (!smoothCursor || !cursorInitialized)
        {
            ApplyCursorImmediate(rect, cursorTargetPosition, cursorTargetSize);
        }

        cursorDirty = false;
        isUpdatingCursor = false;
    }

    private void HideCursor()
    {
        if (cursor != null)
        {
            cursor.gameObject.SetActive(false);
        }
    }

    private RectTransform EnsureCursor()
    {
        if (cursor != null)
        {
            return cursor;
        }

        if (!createCursorIfMissing)
        {
            return null;
        }

        RectTransform parent = GetCursorParent();
        if (parent == null)
        {
            return null;
        }

        GameObject cursorObject = new GameObject("Cursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        RectTransform rect = cursorObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = cursorObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.25f);
        image.raycastTarget = false;
        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        image.type = Image.Type.Sliced;

        LayoutElement layoutElement = cursorObject.GetComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        cursor = rect;
        if (!isUpdatingCursor)
        {
            SnapCursorImmediate();
        }
        return rect;
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
    }
}
