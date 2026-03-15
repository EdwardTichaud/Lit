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

        playerInputs = new PlayerInputs();
        playerInputs.Player.SetCallbacks(this);
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

        LocalInputRouter.ResetMove();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        LocalInputRouter.SetMoveValue(context.ReadValue<Vector2>());
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            LocalInputRouter.RaiseInteract(context);
        }
    }

    public void OnLeftShoulder(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            LocalInputRouter.RaiseLeftShoulder(context);
        }
    }

    public void OnToggleTorch(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            LocalInputRouter.RaiseToggleTorch(context);
        }
    }

    public void OnTakeAll(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            LocalInputRouter.RaiseTakeAll(context);
        }
    }

    public void OnReturn(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            LocalInputRouter.RaiseReturn(context);
        }
    }

    public void OnInventory(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            LocalInputRouter.RaiseInventory(context);
        }
    }

    public void OnMulti(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            LocalInputRouter.RaiseMulti(context);
        }
    }

    public void OnStart(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            LocalInputRouter.RaiseStart(context);
        }
    }
}
