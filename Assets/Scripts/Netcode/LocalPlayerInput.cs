using UnityEngine;
using UnityEngine.InputSystem;

// Singleton local qui capture les inputs et les envoie au LocalInputRouter.
public class LocalPlayerInput : MonoBehaviour, PlayerInputs.IPlayerActions
{
    public static LocalPlayerInput Instance { get; private set; }

    [SerializeField] private bool dontDestroyOnLoad = true;

    private PlayerInputs playerInputs;

    public static void EnsureInstance()
    {
        if (Instance != null)
        {
            return;
        }

        Instance = Object.FindFirstObjectByType<LocalPlayerInput>();
        if (Instance != null)
        {
            return;
        }

        GameObject host = new GameObject("LocalPlayerInput");
        Instance = host.AddComponent<LocalPlayerInput>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        MainMenuInputSettings.ApplySavedModeIfNeeded();
        playerInputs = new PlayerInputs();
        playerInputs.Player.SetCallbacks(this);
        MainMenuInputSettings.ModeChanged += OnInputModeChanged;
        playerInputs.Enable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (playerInputs != null)
        {
            playerInputs.Disable();
            playerInputs.Dispose();
        }

        MainMenuInputSettings.ModeChanged -= OnInputModeChanged;

        LocalInputRouter.ResetMove();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!ShouldProcess(context))
        {
            return;
        }

        LocalInputRouter.SetMoveValue(context.ReadValue<Vector2>());
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseInteract(context);
        }
    }

    public void OnLeftShoulder(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseLeftShoulder(context);
        }
    }

    public void OnToggleTorch(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseToggleTorch(context);
        }
    }

    public void OnTakeAll(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseTakeAll(context);
        }
    }

    public void OnReturn(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseReturn(context);
        }
    }

    public void OnInventory(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseInventory(context);
        }
    }

    public void OnMulti(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseMulti(context);
        }
    }

    public void OnStart(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseStart(context);
        }
    }

    private void OnInputModeChanged(MainMenuInputSettings.InputMode mode)
    {
        LocalInputRouter.ResetMove();
    }

    private static bool ShouldProcess(InputAction.CallbackContext context)
    {
        return MainMenuInputSettings.IsActionAllowed(context);
    }
}
