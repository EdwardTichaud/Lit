using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CursorController))]
public class VirtualKeyboardCursorController : MonoBehaviour
{
    [Header("Cursor")]
    [SerializeField] private CursorController cursor;
    [SerializeField] private RectTransform keysRoot;
    [SerializeField] private LayoutGroup keysLayout;

    [Header("Actions")]
    [SerializeField] private MainMenuController controller;
    [SerializeField] private bool autoAssignVkActions = true;

    [Header("Focus")]
    [SerializeField] private bool requireFocus = true;
    [SerializeField] private bool pushFocus = true;

    [Header("Shared Cursor")]
    [SerializeField] private CursorController sharedCursor;
    [SerializeField] private bool disableSharedCursorInput = true;
    [SerializeField] private bool hideSharedCursorVisual = true;

    [Header("Input")]
    [SerializeField] private bool useInteractInput = true;
    [SerializeField] private bool useReturnInput = false;
    [SerializeField] private UnityEvent onCancel;

    private RectTransform currentItem;
    private MenuCursorItem currentCursorItem;
    private IMenuCursorHandler currentHandler;
    private bool cachedSharedAllowInput;
    private bool cachedSharedActive;
    private bool cachedSharedVisualActive;

    private void Awake()
    {
        if (cursor == null)
        {
            cursor = GetComponent<CursorController>();
        }

        ResolveController();

        if (keysRoot == null)
        {
            keysRoot = FindKeysRoot();
        }

        if (keysLayout == null && keysRoot != null)
        {
            keysLayout = keysRoot.GetComponent<LayoutGroup>();
        }

        if (sharedCursor == null)
        {
            GameObject sharedCursorObject = GameObject.Find("MainMenu_Cursor");
            if (sharedCursorObject != null)
            {
                sharedCursor = sharedCursorObject.GetComponent<CursorController>();
            }
        }

        ConfigureCursor();
        AssignActions();
    }

    private void OnEnable()
    {
        ConfigureCursor();
        AssignActions();
        RegisterInput(true);

        if (pushFocus)
        {
            InputFocusStack.Push(this);
        }

        DisableSharedCursor(true);

        if (cursor != null)
        {
            cursor.Refresh();
            cursor.SelectFirst();
        }
    }

    private void OnDisable()
    {
        RegisterInput(false);
        DisableSharedCursor(false);

        if (pushFocus)
        {
            InputFocusStack.Pop(this);
        }

        ClearCurrent();
    }

    private void Update()
    {
        if (!CanProcessInput())
        {
            return;
        }

        RectTransform item = cursor != null ? cursor.CurrentItem : null;
        if (item == currentItem)
        {
            return;
        }

        SetCurrent(item);
    }

    private void RegisterInput(bool enabled)
    {
        LocalInputRouter.EnsureInitialized();
        if (enabled)
        {
            if (useInteractInput)
            {
                LocalInputRouter.Interact += OnInteractPerformed;
            }
            if (useReturnInput)
            {
                LocalInputRouter.Return += OnReturnPerformed;
            }
            return;
        }

        if (useInteractInput)
        {
            LocalInputRouter.Interact -= OnInteractPerformed;
        }
        if (useReturnInput)
        {
            LocalInputRouter.Return -= OnReturnPerformed;
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!CanProcessInput())
        {
            return;
        }

        if (currentHandler != null)
        {
            currentHandler.OnCursorSubmit();
            return;
        }

        if (currentCursorItem != null)
        {
            currentCursorItem.Submit();
            return;
        }

        if (currentItem != null)
        {
            MenuCursorAction fallbackAction = currentItem.GetComponentInChildren<MenuCursorAction>(true);
            if (fallbackAction != null && fallbackAction.isActiveAndEnabled)
            {
                fallbackAction.OnCursorSubmit();
            }
        }
    }

    private void OnReturnPerformed(InputAction.CallbackContext context)
    {
        if (!CanProcessInput())
        {
            return;
        }

        if (onCancel != null)
        {
            onCancel.Invoke();
        }
    }

    private bool CanProcessInput()
    {
        if (!isActiveAndEnabled || cursor == null)
        {
            return false;
        }

        if (!requireFocus)
        {
            return true;
        }

        return !InputFocusStack.HasAnyFocus() || InputFocusStack.HasFocus(this);
    }

    private void SetCurrent(RectTransform item)
    {
        if (currentItem == item)
        {
            return;
        }

        ClearCurrent();
        currentItem = item;

        if (currentItem == null)
        {
            return;
        }

        currentCursorItem = currentItem.GetComponent<MenuCursorItem>();
        currentHandler = currentItem.GetComponent<IMenuCursorHandler>();

        if (currentHandler != null)
        {
            currentHandler.OnCursorFocus();
        }
        else if (currentCursorItem != null)
        {
            currentCursorItem.Focus();
        }
    }

    private void ClearCurrent()
    {
        if (currentHandler != null)
        {
            currentHandler.OnCursorBlur();
        }
        else if (currentCursorItem != null)
        {
            currentCursorItem.Blur();
        }

        currentHandler = null;
        currentCursorItem = null;
        currentItem = null;
    }

    private void ConfigureCursor()
    {
        if (cursor == null)
        {
            return;
        }

        if (cursor.cursor == null)
        {
            cursor.cursor = cursor.transform as RectTransform;
        }

        if (keysRoot != null)
        {
            cursor.itemsParent = keysRoot;
        }

        if (keysLayout != null)
        {
            cursor.layoutGroup = keysLayout;
        }

        cursor.itemFilter = CursorController.ItemFilter.MenuCursorActionOnly;
        cursor.resetToFirstOnEnable = true;
    }

    private void AssignActions()
    {
        if (!autoAssignVkActions || keysRoot == null)
        {
            return;
        }

        ResolveController();
        if (controller == null)
        {
            return;
        }

        MenuCursorAction[] actions = keysRoot.GetComponentsInChildren<MenuCursorAction>(true);
        for (int i = 0; i < actions.Length; i++)
        {
            if (actions[i] == null)
            {
                continue;
            }

            actions[i].Configure(controller, MenuCursorAction.MenuAction.Vk_Input);
            if (!actions[i].enabled)
            {
                actions[i].enabled = true;
            }
        }
    }

    private void ResolveController()
    {
        if (controller != null)
        {
            return;
        }

        controller = GetComponentInParent<MainMenuController>();
        if (controller != null)
        {
            return;
        }

        controller = FindObjectOfType<MainMenuController>(true);
    }

    private RectTransform FindKeysRoot()
    {
        Transform root = transform.root;
        if (root == null)
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == "VirtualKeyboardButtons")
            {
                return all[i] as RectTransform;
            }
        }

        return null;
    }

    private void DisableSharedCursor(bool disable)
    {
        if (sharedCursor == null)
        {
            return;
        }
        if (sharedCursor == cursor)
        {
            return;
        }

        if (disable)
        {
            cachedSharedAllowInput = sharedCursor.allowInput;
            cachedSharedActive = sharedCursor.enabled;
            cachedSharedVisualActive = sharedCursor.cursor != null && sharedCursor.cursor.gameObject.activeSelf;

            if (disableSharedCursorInput)
            {
                sharedCursor.allowInput = false;
                sharedCursor.enabled = false;
            }

            if (hideSharedCursorVisual && sharedCursor.cursor != null)
            {
                sharedCursor.cursor.gameObject.SetActive(false);
            }
            return;
        }

        if (disableSharedCursorInput)
        {
            sharedCursor.allowInput = cachedSharedAllowInput;
            sharedCursor.enabled = cachedSharedActive;
        }

        if (hideSharedCursorVisual && sharedCursor.cursor != null)
        {
            sharedCursor.cursor.gameObject.SetActive(cachedSharedVisualActive);
        }
    }
}
