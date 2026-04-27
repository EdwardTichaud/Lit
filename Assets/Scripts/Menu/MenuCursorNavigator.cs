using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public interface IMenuCursorHandler
{
    void OnCursorFocus();
    void OnCursorBlur();
    void OnCursorSubmit();
}

// Gere un curseur de navigation et declenche l'action de l'element selectionne.
public class MenuCursorNavigator : MonoBehaviour
{
    [SerializeField] private CursorController cursor;
    [SerializeField] private MonoBehaviour focusOwner;
    [SerializeField] private bool requireFocus = true;
    [SerializeField] private bool pushFocusOnEnable = true;
    [SerializeField] private bool useInteractInput = true;
    [SerializeField] private bool useReturnInput = false;
    [SerializeField] private bool allowButtonFallback = false;
    [SerializeField] private UnityEvent onCancel;

    private RectTransform currentItem;
    private MenuCursorItem currentCursorItem;
    private IMenuCursorHandler currentHandler;
    private object focusTarget;

    private void Awake()
    {
        if (cursor == null)
        {
            cursor = GetComponentInChildren<CursorController>(true);
        }
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        ResolveFocusTarget();
        if (pushFocusOnEnable)
        {
            InputFocusStack.Push(focusTarget);
        }
        if (useInteractInput)
        {
            LocalInputRouter.Interact += OnInteractPerformed;
        }
        if (useReturnInput)
        {
            LocalInputRouter.Return += OnReturnPerformed;
        }
    }

    private void OnDisable()
    {
        if (useInteractInput)
        {
            LocalInputRouter.Interact -= OnInteractPerformed;
        }
        if (useReturnInput)
        {
            LocalInputRouter.Return -= OnReturnPerformed;
        }

        if (pushFocusOnEnable)
        {
            InputFocusStack.Pop(focusTarget);
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

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!CanProcessInput())
        {
            return;
        }

        LocalInputRouter.ConsumeInteract();

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

        if (!allowButtonFallback || currentItem == null)
        {
            return;
        }

        Button button = currentItem.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.Invoke();
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

    public bool HasInputFocus()
    {
        if (!isActiveAndEnabled)
        {
            return false;
        }

        if (!requireFocus)
        {
            return true;
        }

        ResolveFocusTarget();
        return InputFocusStack.HasFocus(focusTarget);
    }

    private bool CanProcessInput()
    {
        if (!isActiveAndEnabled || cursor == null)
        {
            return false;
        }

        return HasInputFocus();
    }

    private void ResolveFocusTarget()
    {
        if (focusTarget != null)
        {
            return;
        }

        focusTarget = focusOwner != null ? focusOwner : this;
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

    public void ConfigureRuntime(
        CursorController runtimeCursor,
        bool runtimeRequireFocus,
        bool runtimePushFocusOnEnable,
        bool runtimeUseInteractInput,
        bool runtimeUseReturnInput,
        bool runtimeAllowButtonFallback)
    {
        cursor = runtimeCursor;
        requireFocus = runtimeRequireFocus;
        pushFocusOnEnable = runtimePushFocusOnEnable;
        useInteractInput = runtimeUseInteractInput;
        useReturnInput = runtimeUseReturnInput;
        allowButtonFallback = runtimeAllowButtonFallback;
        focusTarget = null;
    }

    public void ReplaceCancelHandler(UnityAction callback)
    {
        if (onCancel == null)
        {
            onCancel = new UnityEvent();
        }

        onCancel.RemoveAllListeners();
        if (callback != null)
        {
            onCancel.AddListener(callback);
        }
    }
}
