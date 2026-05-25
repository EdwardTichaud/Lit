using UnityEngine;
using UnityEngine.InputSystem;

// Singleton local qui capture les inputs et les envoie au LocalInputRouter.
public class LocalPlayerInput : MonoBehaviour, PlayerInputs.IPlayerActions, PlayerInputs.ICameraActions
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
        playerInputs.Camera.SetCallbacks(this);
        MainMenuInputSettings.ModeChanged += OnInputModeChanged;
        playerInputs.Enable();
    }

    private void Update()
    {
        LocalInputRouter.SetFlightVerticalValue(ReadFlightVerticalInput());
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
        LocalInputRouter.ResetCamera();
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

    public void OnRightShoulder(InputAction.CallbackContext context)
    {
        bool shouldProcess = ShouldProcess(context);
        LocalInputRouter.SetRightShoulderPressed(shouldProcess && context.ReadValueAsButton());

        if (context.performed && shouldProcess)
        {
            LocalInputRouter.RaiseRightShoulder(context);
        }
    }

    public void OnLocomotionMode(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseLocomotionMode(context);
        }
    }

    public void OnTriggerMunin(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseTriggerMunin(context);
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

    public void OnPan(InputAction.CallbackContext context)
    {
        if (!ShouldProcess(context))
        {
            return;
        }

        LocalInputRouter.SetCameraPanValue(context.ReadValue<Vector2>());
    }

    public void OnOrbit(InputAction.CallbackContext context)
    {
        if (!ShouldProcess(context))
        {
            return;
        }

        LocalInputRouter.SetCameraOrbitValue(context.ReadValue<Vector2>());
    }

    public void OnZoom(InputAction.CallbackContext context)
    {
        if (!ShouldProcess(context))
        {
            return;
        }

        LocalInputRouter.SetCameraZoomValue(context.ReadValue<float>());
    }

    public void OnPointerScroll(InputAction.CallbackContext context)
    {
        if (!ShouldProcess(context))
        {
            return;
        }

        LocalInputRouter.SetCameraPointerScrollValue(context.ReadValue<float>());
    }

    public void OnPointerDelta(InputAction.CallbackContext context)
    {
        if (!ShouldProcess(context))
        {
            return;
        }

        LocalInputRouter.SetCameraPointerDelta(context.ReadValue<Vector2>());
    }

    public void OnPointerPosition(InputAction.CallbackContext context)
    {
        if (!ShouldProcess(context))
        {
            return;
        }

        LocalInputRouter.SetCameraPointerPosition(context.ReadValue<Vector2>());
    }

    public void OnOrbitModifier(InputAction.CallbackContext context)
    {
        if (!ShouldProcess(context))
        {
            return;
        }

        LocalInputRouter.SetCameraOrbitModifierPressed(context.ReadValueAsButton());
    }

    public void OnPanModifier(InputAction.CallbackContext context)
    {
        if (!ShouldProcess(context))
        {
            return;
        }

        LocalInputRouter.SetCameraPanModifierPressed(context.ReadValueAsButton());
    }

    public void OnRecenter(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseCameraRecenter();
        }
    }

    public void OnToggleFreeCamera(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseCameraToggleFreeMode();
        }
    }

    private void OnInputModeChanged(MainMenuInputSettings.InputMode mode)
    {
        LocalInputRouter.ResetMove();
        LocalInputRouter.ResetCamera();
    }

    private static float ReadFlightVerticalInput()
    {
        if (!MainMenuInputSettings.AllowsGamepad() || Gamepad.current == null)
        {
            return 0f;
        }

        Gamepad gamepad = Gamepad.current;
        return gamepad.rightTrigger.ReadValue() - gamepad.leftTrigger.ReadValue();
    }

    private static bool ShouldProcess(InputAction.CallbackContext context)
    {
        return MainMenuInputSettings.IsActionAllowed(context);
    }
}
