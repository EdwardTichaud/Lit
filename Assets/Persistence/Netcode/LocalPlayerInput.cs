using UnityEngine;
using UnityEngine.InputSystem;

// Singleton local qui capture les inputs et les envoie au LocalInputRouter.
public class LocalPlayerInput : MonoBehaviour, PlayerInputs.IPlayerActions, PlayerInputs.ICameraActions, PlayerInputs.ICombatActions
{
    public static LocalPlayerInput Instance { get; private set; }

    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField, Tooltip("Logs the single reconciliation performed after an ActionMap or cinematic handoff.")]
    private bool logLocomotionReconciliationDiagnostics;

    private PlayerInputs playerInputs;
    private bool combatInputActive;
    private bool inputMapsConfigured;
    private Coroutine locomotionReconciliationRoutine;
    private int locomotionReconciliationToken;

    public static void EnsureInstance()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Instance != null)
        {
            return;
        }

        Instance = Object.FindAnyObjectByType<LocalPlayerInput>();
        if (Instance != null)
        {
            return;
        }

        GameObject host = new GameObject("LocalPlayerInput");
        Instance = host.AddComponent<LocalPlayerInput>();
    }

    /// <summary>
    /// Returns an ActionMap from the sole runtime PlayerInputs instance. Runtime
    /// consumers must subscribe to this map, not to the imported asset, because
    /// the coordinator enables this instance exclusively.
    /// </summary>
    public static InputActionMap FindSharedActionMap(string mapName)
    {
        EnsureInstance();
        return Instance != null && Instance.playerInputs != null
            ? Instance.playerInputs.asset.FindActionMap(mapName, false)
            : null;
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
            if (transform.parent != null)
            {
                transform.SetParent(null, true);
            }

            DontDestroyOnLoad(gameObject);
        }

        MainMenuInputSettings.ApplySavedModeIfNeeded();
        playerInputs = new PlayerInputs();
        playerInputs.Player.SetCallbacks(this);
        playerInputs.Camera.SetCallbacks(this);
        playerInputs.Combat.SetCallbacks(this);
        MainMenuInputSettings.ModeChanged += OnInputModeChanged;
        InputModeCoordinator.ModeChanged += OnCoordinatorModeChanged;
        InputModeCoordinator.Configure(playerInputs.asset);
        ApplyCombatInputActive(false);
    }

    private void Update()
    {
        if (combatInputActive)
        {
            LocalInputRouter.SetFlightVerticalValue(0f);
            return;
        }

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
            if (Application.isPlaying)
            {
                playerInputs.Dispose();
            }
            else if (playerInputs.asset != null)
            {
                // Scene teardown in the editor cannot process a deferred Destroy.
                DestroyImmediate(playerInputs.asset);
            }

            playerInputs = null;
        }

        MainMenuInputSettings.ModeChanged -= OnInputModeChanged;
        InputModeCoordinator.ModeChanged -= OnCoordinatorModeChanged;

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
        if (context.performed && CanProcessGameplayAction(context))
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

    public void OnSwitchTarget(InputAction.CallbackContext context)
    {
        if (context.performed && CanProcessGameplayAction(context))
        {
            LocalInputRouter.RaiseSwitchTarget(context);
        }
    }

    public void OnTriggerMunin(InputAction.CallbackContext context)
    {
        if (context.performed && CanProcessGameplayAction(context))
        {
            LocalInputRouter.RaiseTriggerMunin(context);
        }
    }

    public void OnMelt(InputAction.CallbackContext context)
    {
        if (context.performed && CanProcessGameplayAction(context))
        {
            LocalInputRouter.RaiseCompanionFusion(context);
        }
    }

    public void OnToggleTorch(InputAction.CallbackContext context)
    {
        if (context.performed && CanProcessGameplayAction(context))
        {
            LocalInputRouter.RaiseToggleTorch(context);
        }
    }

    public void OnTakeAll(InputAction.CallbackContext context)
    {
        if (context.performed && CanProcessGameplayAction(context))
        {
            LocalInputRouter.RaiseTakeAll(context);
        }
    }

    public void OnReturn(InputAction.CallbackContext context)
    {
        if (context.performed && CanProcessGameplayAction(context))
        {
            LocalInputRouter.RaiseReturn(context);
        }
    }

    public void OnInventory(InputAction.CallbackContext context)
    {
        if (combatInputActive || IsLocalCombatActive())
        {
            return;
        }

        if (context.performed && CanProcessGameplayAction(context))
        {
            LocalInputRouter.RaiseInventory(context);
        }
    }

    public void OnMulti(InputAction.CallbackContext context)
    {
        if (context.performed && CanProcessGameplayAction(context))
        {
            LocalInputRouter.RaiseMulti(context);
        }
    }

    public void OnSelect(InputAction.CallbackContext context)
    {
        if (context.performed && CanProcessGameplayAction(context))
        {
            LocalInputRouter.RaiseSelect(context);
        }
    }

    public void OnLightSkill(InputAction.CallbackContext context)
    {
        if (context.performed && CanProcessGameplayAction(context))
        {
            LocalInputRouter.RaiseLightSkill(context);
        }
    }

    public void OnStart(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseStart(context);
        }
    }

    public void OnUseItem1(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseCombatUseItem(context, 0);
        }
    }

    public void OnUseItem2(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseCombatUseItem(context, 1);
        }
    }

    public void OnUseItem3(InputAction.CallbackContext context)
    {
        if (context.performed && ShouldProcess(context))
        {
            LocalInputRouter.RaiseCombatUseItem(context, 2);
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

    public static void SetCombatInputActive(bool active)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Instance == null && !active)
        {
            return;
        }

        EnsureInstance();
        if (Instance != null)
        {
            Instance.ApplyCombatInputActive(active);
        }
    }

    /// <summary>
    /// Re-reads held locomotion controls after a map handoff or action exit.
    /// Input System does not necessarily emit a performed callback for a
    /// control that was already held while its map was disabled.
    /// </summary>
    public static void RequestHeldLocomotionReconciliation(string reason = null)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureInstance();
        Instance?.ScheduleHeldLocomotionReconciliation(reason);
    }

    private void ApplyCombatInputActive(bool active)
    {
        if (playerInputs == null)
        {
            return;
        }

        // InputModeCoordinator may have been reset by a scene/UI transition
        // while this persistent input host still remembers the previous combat
        // flag. Always re-apply the requested base profile so RealTimeCombat
        // cannot remain disabled after a valid manual lock.
        inputMapsConfigured = true;
        combatInputActive = active;
        InputModeCoordinator.SetBaseMode(active ? InputMode.Combat : InputMode.Exploration);
    }

    private void OnCoordinatorModeChanged(InputMode mode)
    {
        if (logLocomotionReconciliationDiagnostics)
        {
            Debug.Log("[Locomotion Handoff] Input mode changed | mode=" + mode + ".", this);
        }

        if (mode == InputMode.Exploration || mode == InputMode.Combat)
        {
            ScheduleHeldLocomotionReconciliation("ModeChanged " + mode);
        }
        else
        {
            CancelHeldLocomotionReconciliation();
        }
    }

    private void ScheduleHeldLocomotionReconciliation(string reason)
    {
        locomotionReconciliationToken++;
        if (locomotionReconciliationRoutine != null)
        {
            StopCoroutine(locomotionReconciliationRoutine);
        }

        locomotionReconciliationRoutine = StartCoroutine(ReconcileHeldLocomotionAfterHandoff(
            locomotionReconciliationToken,
            string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason));
    }

    private void CancelHeldLocomotionReconciliation()
    {
        locomotionReconciliationToken++;
        if (locomotionReconciliationRoutine != null)
        {
            StopCoroutine(locomotionReconciliationRoutine);
            locomotionReconciliationRoutine = null;
        }
    }

    private System.Collections.IEnumerator ReconcileHeldLocomotionAfterHandoff(int token, string reason)
    {
        // Let Input System enable the destination map and let UCC release any
        // external lock before sampling the controls again.
        yield return null;

        while (token == locomotionReconciliationToken)
        {
            InputMode mode = InputModeCoordinator.CurrentMode;
            if (mode != InputMode.Exploration && mode != InputMode.Combat)
            {
                yield break;
            }

            if (InputFocusStack.HasAnyFocus())
            {
                yield return null;
                continue;
            }

            InputAction moveAction = playerInputs != null
                ? playerInputs.asset.FindAction("Player/Move", false)
                : null;
            InputAction sprintAction = playerInputs != null
                ? playerInputs.asset.FindAction("Player/RightShoulder", false)
                : null;
            if (moveAction == null || !moveAction.enabled)
            {
                yield return null;
                continue;
            }

            Vector2 move = moveAction.ReadValue<Vector2>();
            bool sprint = sprintAction != null && sprintAction.enabled && sprintAction.ReadValue<float>() > 0.5f;
            LocalInputRouter.SetRightShoulderPressed(sprint);
            LocalInputRouter.SetMoveValue(move);

            if (SquadManager.Instance != null && SquadManager.Instance.ReapplyHeldLocomotionIntent())
            {
                if (logLocomotionReconciliationDiagnostics)
                {
                    Debug.Log("[Locomotion Handoff] Reconciled | reason=" + reason +
                              " | mode=" + mode + " | move=" + move + " | sprint=" + sprint + ".", this);
                }

                locomotionReconciliationRoutine = null;
                yield break;
            }

            yield return null;
        }
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

    private static bool CanProcessGameplayAction(InputAction.CallbackContext context)
    {
        return !GamepadInputContextStack.IsGameplayInputSuppressed && ShouldProcess(context);
    }

    private static bool IsLocalCombatActive()
    {
        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        return manager != null && manager.IsCombatActive;
    }
}
