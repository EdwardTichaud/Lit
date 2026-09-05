using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Point d'entree unique pour toutes les confirmations d'action.
[DisallowMultipleComponent]
public class ConfirmationManager : MonoBehaviour
{
    private const string ManagerObjectName = "ConfirmationManager";
    private const string RootObjectName = "ConfirmationBox_Root";
    private const string PreviousRootObjectName = "ConfirmationBoxes";
    private const string BoxObjectName = "ConfirmationBox";
    private const string RuntimeCanvasObjectName = "ConfirmationCanvas_Auto";

    [Header("Runtime UI")]
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private ConfirmationBox confirmationBox;
    [SerializeField] private bool createRuntimeFallback = true;
    [SerializeField] private int fallbackSortingOrder = 250;

    private ConfirmationRequest activeRequest;
    private int shownFrame = -1;
    private bool inputLocked;

    private static ConfirmationManager instance;

    public static GameObject CurrentSelection => IsVisible && instance.confirmationBox != null &&
        instance.confirmationBox.CursorController != null && instance.confirmationBox.CursorController.CurrentItem != null
        ? instance.confirmationBox.CursorController.CurrentItem.gameObject : null;

    public static bool IsVisible => instance != null && instance.activeRequest != null;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent == null && string.Equals(gameObject.name, ManagerObjectName, StringComparison.Ordinal))
        {
            DontDestroyOnLoad(gameObject);
        }

        HideImmediate();
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        LocalInputRouter.Return += OnReturnPerformed;
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;
        LocalInputRouter.Return -= OnReturnPerformed;
        ReleaseInputLock();
        HideImmediate();
        activeRequest = null;
        shownFrame = -1;

        if (instance == this)
        {
            instance = null;
        }
    }

    public static bool TryShow(ConfirmationRequest request)
    {
        return EnsureInstance().TryShowInternal(request);
    }

    public static bool TryShow(
        object owner,
        string message,
        Action onConfirm,
        Action onCancel = null,
        string confirmLabel = null,
        string cancelLabel = null,
        string title = null,
        string debugContext = null)
    {
        ConfirmationRequest request = new ConfirmationRequest(owner, message, onConfirm, onCancel)
        {
            Title = title,
            ConfirmLabel = confirmLabel,
            CancelLabel = cancelLabel,
            DebugContext = debugContext
        };

        return TryShow(request);
    }

    public static void Dismiss(object owner, bool invokeCancel = false)
    {
        if (instance == null)
        {
            return;
        }

        instance.DismissInternal(owner, invokeCancel);
    }

    public static bool IsShowingFor(object owner)
    {
        return instance != null && MatchesOwner(instance.activeRequest != null ? instance.activeRequest.Owner : null, owner);
    }

    private static ConfirmationManager EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

#if UNITY_2023_1_OR_NEWER
        instance = FindAnyObjectByType<ConfirmationManager>(FindObjectsInactive.Include);
#else
        instance = FindAnyObjectByType<ConfirmationManager>();
#endif
        if (instance != null)
        {
            return instance;
        }

        GameObject managerObject = new GameObject(ManagerObjectName);
        instance = managerObject.AddComponent<ConfirmationManager>();
        return instance;
    }

    private bool TryShowInternal(ConfirmationRequest request)
    {
        if (request == null)
        {
            Debug.LogWarning("[Confirmation] request ignored because it is null.", this);
            return false;
        }

        if (!EnsureUiReferences())
        {
            Debug.LogWarning($"[Confirmation] request ignored because UI is unavailable. owner='{GetOwnerLabel(request.Owner)}' context='{request.DebugContext}'", this);
            return false;
        }

        if (activeRequest != null && !MatchesOwner(activeRequest.Owner, request.Owner))
        {
            Debug.LogWarning(
                $"[Confirmation] request rejected because another confirmation is already open. activeOwner='{GetOwnerLabel(activeRequest.Owner)}' requester='{GetOwnerLabel(request.Owner)}' activeContext='{activeRequest.DebugContext}' requestedContext='{request.DebugContext}'",
                GetLogContext(request.Owner));
            return false;
        }

        activeRequest = request;
        shownFrame = Time.frameCount;

        SetRootVisible(true);
        confirmationBox.SetQuestion(request.Message);
        confirmationBox.SetOptions(request.ConfirmLabel, request.CancelLabel);
        SetBoxInteractive(true);
        SetSelectionToConfirm();
        WireButtons();
        AcquireInputLock();

        Debug.Log(
            $"[Confirmation] show owner='{GetOwnerLabel(request.Owner)}' context='{request.DebugContext}' message='{SanitizeForLog(request.Message)}'",
            GetLogContext(request.Owner));
        return true;
    }

    private void DismissInternal(object owner, bool invokeCancel)
    {
        if (activeRequest == null)
        {
            return;
        }

        if (owner != null && !MatchesOwner(activeRequest.Owner, owner))
        {
            return;
        }

        ConfirmationRequest request = activeRequest;
        HideAndClear();

        Debug.Log(
            $"[Confirmation] dismiss owner='{GetOwnerLabel(request.Owner)}' context='{request.DebugContext}' invokeCancel={invokeCancel}",
            GetLogContext(request.Owner));

        if (invokeCancel)
        {
            SafeInvoke(request.OnCancel, request, "cancel");
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (activeRequest == null)
        {
            return;
        }

        if (!InputFocusStack.HasFocus(this))
        {
            return;
        }

        if (shownFrame == Time.frameCount)
        {
            Debug.Log("[Confirmation] ignored same-frame confirm input.", this);
            return;
        }

        if (!LocalInputRouter.TryConsumeInteract())
        {
            return;
        }

        if (!IsConfirmSelected())
        {
            HandleCancel();
            return;
        }

        HandleConfirm();
    }

    private void OnReturnPerformed(InputAction.CallbackContext context)
    {
        if (activeRequest == null)
        {
            return;
        }

        if (!InputFocusStack.HasFocus(this))
        {
            return;
        }

        if (shownFrame == Time.frameCount)
        {
            Debug.Log("[Confirmation] ignored same-frame cancel input.", this);
            return;
        }

        if (activeRequest.DismissOnReturn)
        {
            HandleDismiss();
            return;
        }

        HandleCancel();
    }

    private void HandleConfirm()
    {
        if (activeRequest == null)
        {
            return;
        }

        ConfirmationRequest request = activeRequest;
        HideAndClear();

        Debug.Log(
            $"[Confirmation] confirm owner='{GetOwnerLabel(request.Owner)}' context='{request.DebugContext}'",
            GetLogContext(request.Owner));

        SafeInvoke(request.OnConfirm, request, "confirm");
    }

    private void HandleCancel()
    {
        if (activeRequest == null)
        {
            return;
        }

        ConfirmationRequest request = activeRequest;
        HideAndClear();

        Debug.Log(
            $"[Confirmation] cancel owner='{GetOwnerLabel(request.Owner)}' context='{request.DebugContext}'",
            GetLogContext(request.Owner));

        SafeInvoke(request.OnCancel, request, "cancel");
    }

    private void HandleDismiss()
    {
        if (activeRequest == null)
        {
            return;
        }

        ConfirmationRequest request = activeRequest;
        HideAndClear();
        Debug.Log(
            $"[Confirmation] dismiss owner='{GetOwnerLabel(request.Owner)}' context='{request.DebugContext}'",
            GetLogContext(request.Owner));
    }

    private void HideAndClear()
    {
        UnwireButtons();
        SetBoxInteractive(false);
        SetRootVisible(false);
        ReleaseInputLock();
        activeRequest = null;
        shownFrame = -1;
    }

    private void HideImmediate()
    {
        SetBoxInteractive(false);
        SetRootVisible(false);
        UnwireButtons();
    }

    private void WireButtons()
    {
        UnwireButtons();

        if (confirmationBox == null)
        {
            return;
        }

        if (confirmationBox.ConfirmButton != null)
        {
            confirmationBox.ConfirmButton.onClick.AddListener(HandleConfirmButtonClicked);
        }

        if (confirmationBox.CancelButton != null)
        {
            confirmationBox.CancelButton.onClick.AddListener(HandleCancelButtonClicked);
        }
    }

    private void UnwireButtons()
    {
        if (confirmationBox == null)
        {
            return;
        }

        if (confirmationBox.ConfirmButton != null)
        {
            confirmationBox.ConfirmButton.onClick.RemoveListener(HandleConfirmButtonClicked);
        }

        if (confirmationBox.CancelButton != null)
        {
            confirmationBox.CancelButton.onClick.RemoveListener(HandleCancelButtonClicked);
        }
    }

    private void HandleConfirmButtonClicked()
    {
        if (shownFrame == Time.frameCount)
        {
            Debug.Log("[Confirmation] ignored same-frame confirm click.", this);
            return;
        }

        HandleConfirm();
    }

    private void HandleCancelButtonClicked()
    {
        if (shownFrame == Time.frameCount)
        {
            Debug.Log("[Confirmation] ignored same-frame cancel click.", this);
            return;
        }

        HandleCancel();
    }

    private void AcquireInputLock()
    {
        if (inputLocked)
        {
            InputFocusStack.Push(this);
            return;
        }

        inputLocked = true;
        InputFocusStack.Push(this);
        if (SquadManager.Instance != null)
        {
            SquadManager.Instance.SetInputLocked(true);
        }
    }

    private void ReleaseInputLock()
    {
        if (!inputLocked)
        {
            InputFocusStack.Pop(this);
            return;
        }

        inputLocked = false;
        InputFocusStack.Pop(this);
        if (SquadManager.Instance != null)
        {
            SquadManager.Instance.SetInputLocked(false);
        }
    }

    private void SetRootVisible(bool visible)
    {
        if (rootCanvasGroup == null)
        {
            return;
        }

        GameObject rootObject = rootCanvasGroup.gameObject;
        if (!rootObject.activeSelf)
        {
            rootObject.SetActive(true);
        }

        rootCanvasGroup.alpha = visible ? 1f : 0f;
        rootCanvasGroup.interactable = visible;
        rootCanvasGroup.blocksRaycasts = visible;

        if (visible && rootCanvasGroup.transform.parent != null)
        {
            rootCanvasGroup.transform.SetAsLastSibling();
        }
    }

    private bool EnsureUiReferences()
    {
        if (rootCanvasGroup != null && confirmationBox != null && confirmationBox.ResolveReferences())
        {
            return true;
        }

        RectTransform root = ResolveRootTransform();
        if (root == null)
        {
            return false;
        }

        rootCanvasGroup = root.GetComponent<CanvasGroup>();
        if (rootCanvasGroup == null)
        {
            rootCanvasGroup = root.gameObject.AddComponent<CanvasGroup>();
        }

        confirmationBox = ResolveConfirmationBox(root);
        if (confirmationBox == null)
        {
            return false;
        }

        if (!confirmationBox.ResolveReferences())
        {
            Debug.LogWarning($"[Confirmation] confirmation box on '{confirmationBox.name}' is missing required references.", confirmationBox);
            return false;
        }

        return true;
    }

    private RectTransform ResolveRootTransform()
    {
        if (rootCanvasGroup != null)
        {
            return rootCanvasGroup.transform as RectTransform;
        }

        Transform explicitRoot = FindSceneTransformByName(RootObjectName);
        if (explicitRoot != null)
        {
            return explicitRoot as RectTransform;
        }

        Transform previousRoot = FindSceneTransformByName(PreviousRootObjectName);
        if (previousRoot != null)
        {
            Transform child = previousRoot.Find(RootObjectName);
            if (child == null)
            {
                child = CreateRootChild(previousRoot);
            }

            return child as RectTransform;
        }

        if (!createRuntimeFallback)
        {
            return null;
        }

        return CreateRuntimeCanvasRoot();
    }

    private ConfirmationBox ResolveConfirmationBox(RectTransform root)
    {
        if (root == null)
        {
            return null;
        }

        if (confirmationBox != null)
        {
            return confirmationBox;
        }

        Transform existing = root.Find(BoxObjectName);
        if (existing == null)
        {
            existing = FindChildByNameRecursive(root, BoxObjectName);
        }

        if (existing == null)
        {
            CursorController existingCursor = root.GetComponentInChildren<CursorController>(true);
            if (existingCursor != null && existingCursor.transform.parent != null)
            {
                existing = existingCursor.transform.parent;
            }
        }

        if (existing == null && string.Equals(root.name, BoxObjectName, StringComparison.Ordinal))
        {
            existing = root;
        }

        RectTransform boxTransform = existing as RectTransform;
        if (boxTransform == null)
        {
            if (!createRuntimeFallback)
            {
                Debug.LogWarning($"[Confirmation] no '{BoxObjectName}' found under '{root.name}'.", this);
                return null;
            }

            return CreateRuntimeFallbackBox(root);
        }

        ConfirmationBox box = boxTransform.GetComponent<ConfirmationBox>();
        if (box == null)
        {
            box = boxTransform.gameObject.AddComponent<ConfirmationBox>();
        }

        return box;
    }

    private RectTransform CreateRootChild(Transform parent)
    {
        GameObject rootObject = new GameObject(RootObjectName, typeof(RectTransform));
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0.5f);
        return root;
    }

    private RectTransform CreateRuntimeCanvasRoot()
    {
        Transform existingCanvas = FindSceneTransformByName(RuntimeCanvasObjectName);
        if (existingCanvas is RectTransform existingCanvasRect)
        {
            Transform existingRoot = existingCanvas.Find(RootObjectName);
            if (existingRoot is RectTransform existingRootRect)
            {
                return existingRootRect;
            }
        }

        GameObject canvasObject = new GameObject(
            RuntimeCanvasObjectName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasObject);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = fallbackSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        return CreateRootChild(canvasRect);
    }

    private void SetBoxInteractive(bool interactive)
    {
        if (confirmationBox == null)
        {
            return;
        }

        if (confirmationBox.ConfirmButton != null)
        {
            confirmationBox.ConfirmButton.interactable = interactive;
        }

        if (confirmationBox.CancelButton != null)
        {
            confirmationBox.CancelButton.interactable = interactive;
        }

        CursorController cursor = confirmationBox.CursorController;
        if (cursor != null)
        {
            cursor.allowInput = interactive;
            if (interactive)
            {
                cursor.Refresh();
            }
        }

        if (confirmationBox.CursorRoot != null)
        {
            bool shouldShowCursor = interactive && (cursor == null || cursor.CurrentItem != null);
            confirmationBox.CursorRoot.gameObject.SetActive(shouldShowCursor);
        }
    }

    private void SetSelectionToConfirm()
    {
        if (confirmationBox == null)
        {
            return;
        }

        CursorController cursor = confirmationBox.CursorController;
        if (cursor == null)
        {
            return;
        }

        cursor.allowInput = true;
        cursor.Refresh();

        RectTransform initialTarget = activeRequest != null && activeRequest.PreferCancel ? confirmationBox.CancelTarget : confirmationBox.ConfirmTarget;
        bool selected = initialTarget != null && cursor.TrySetCurrentItem(initialTarget, true);
        if (!selected)
        {
            cursor.SelectFirst();
        }

        if (confirmationBox.CursorRoot != null)
        {
            confirmationBox.CursorRoot.gameObject.SetActive(cursor.CurrentItem != null);
        }
    }

    private bool IsConfirmSelected()
    {
        if (confirmationBox == null)
        {
            return true;
        }

        CursorController cursor = confirmationBox.CursorController;
        if (cursor == null)
        {
            return true;
        }

        RectTransform current = cursor.CurrentItem;
        if (current == null)
        {
            return true;
        }

        if (confirmationBox.CancelTarget != null && current == confirmationBox.CancelTarget)
        {
            return false;
        }

        if (confirmationBox.ConfirmTarget != null && current == confirmationBox.ConfirmTarget)
        {
            return true;
        }

        return cursor.CurrentIndex <= 0;
    }

    private ConfirmationBox CreateRuntimeFallbackBox(RectTransform root)
    {
        GameObject boxObject = new GameObject(BoxObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform boxRect = boxObject.GetComponent<RectTransform>();
        boxRect.SetParent(root, false);
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(980f, 420f);
        boxRect.anchoredPosition = Vector2.zero;

        Image background = boxObject.GetComponent<Image>();
        background.color = new Color(0.08f, 0.06f, 0.04f, 0.96f);
        background.raycastTarget = false;

        TextMeshProUGUI question = CreateFallbackText("Question", boxRect, new Vector2(0.5f, 0.76f), new Vector2(820f, 180f), 48f);
        question.enableWordWrapping = true;
        question.alignment = TextAlignmentOptions.Center;

        GameObject choicesObject = new GameObject("Choix", typeof(RectTransform), typeof(GridLayoutGroup));
        RectTransform choicesRect = choicesObject.GetComponent<RectTransform>();
        choicesRect.SetParent(boxRect, false);
        choicesRect.anchorMin = new Vector2(0.5f, 0.24f);
        choicesRect.anchorMax = new Vector2(0.5f, 0.24f);
        choicesRect.pivot = new Vector2(0.5f, 0.5f);
        choicesRect.sizeDelta = new Vector2(520f, 90f);

        GridLayoutGroup grid = choicesObject.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(220f, 70f);
        grid.spacing = new Vector2(40f, 0f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;

        CreateFallbackText("Oui", choicesRect, new Vector2(0.5f, 0.5f), new Vector2(220f, 70f), 42f);
        CreateFallbackText("Non", choicesRect, new Vector2(0.5f, 0.5f), new Vector2(220f, 70f), 42f);

        GameObject cursorObject = new GameObject("Cursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CursorController));
        RectTransform cursorRect = cursorObject.GetComponent<RectTransform>();
        cursorRect.SetParent(boxRect, false);
        cursorRect.anchorMin = new Vector2(0.5f, 0.5f);
        cursorRect.anchorMax = new Vector2(0.5f, 0.5f);
        cursorRect.pivot = new Vector2(0.5f, 0.5f);
        cursorRect.sizeDelta = new Vector2(26f, 26f);

        Image cursorImage = cursorObject.GetComponent<Image>();
        cursorImage.color = new Color(0.95f, 0.79f, 0.31f, 1f);
        cursorImage.raycastTarget = false;

        CursorController cursor = cursorObject.GetComponent<CursorController>();
        cursor.layoutGroup = grid;
        cursor.itemsParent = choicesRect;
        cursor.cursor = cursorRect;
        cursor.cursorParentOverride = boxRect;
        cursor.placement = CursorController.CursorPlacement.RightOfTarget;
        cursor.rightOffset = new Vector2(26f, 0f);
        cursor.matchTargetSize = false;
        cursor.allowInput = false;
        cursor.resetToFirstOnEnable = true;
        cursor.startIndex = 0;

        ConfirmationBox box = boxObject.AddComponent<ConfirmationBox>();
        if (!box.ResolveReferences())
        {
            Debug.LogWarning("[Confirmation] runtime fallback confirmation box could not resolve its references.", box);
        }

        Debug.LogWarning("[Confirmation] no confirmation box instance found in scene. Using runtime fallback UI.", this);
        return box;
    }

    private static TextMeshProUGUI CreateFallbackText(string objectName, RectTransform parent, Vector2 anchor, Vector2 size, float fontSize)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = objectName;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        return text;
    }

    private static Transform FindSceneTransformByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

#if UNITY_2023_1_OR_NEWER
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
#else
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
#endif
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform == null)
            {
                continue;
            }

            if (string.Equals(transform.name, objectName, StringComparison.Ordinal))
            {
                return transform;
            }
        }

        return null;
    }

    private static Transform FindChildByNameRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (string.Equals(child.name, childName, StringComparison.Ordinal))
            {
                return child;
            }

            Transform nested = FindChildByNameRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static bool MatchesOwner(object left, object right)
    {
        if (left == null || right == null)
        {
            return left == right;
        }

        return ReferenceEquals(left, right);
    }

    private static void SafeInvoke(Action callback, ConfirmationRequest request, string callbackType)
    {
        if (callback == null)
        {
            return;
        }

        try
        {
            callback.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogException(new Exception(
                $"[Confirmation] exception while executing {callbackType} callback for owner='{GetOwnerLabel(request != null ? request.Owner : null)}' context='{(request != null ? request.DebugContext : null)}'.",
                ex));
        }
    }

    private static string GetOwnerLabel(object owner)
    {
        if (owner == null)
        {
            return "null";
        }

        if (owner is Component component)
        {
            return $"{component.GetType().Name}({component.name})";
        }

        if (owner is GameObject gameObject)
        {
            return $"GameObject({gameObject.name})";
        }

        return owner.GetType().Name;
    }

    private static UnityEngine.Object GetLogContext(object owner)
    {
        if (owner is UnityEngine.Object unityObject)
        {
            return unityObject;
        }

        return instance;
    }

    private static string SanitizeForLog(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }
}
