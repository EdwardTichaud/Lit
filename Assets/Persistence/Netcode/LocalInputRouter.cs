using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Route les inputs du joueur local vers les systems interessés.
public static class LocalInputRouter
{
    private const float DefaultInputDebounceSeconds = 0.15f;

    private enum InputGate
    {
        Jump,
        Interact,
        TriggerMunin,
        TakeAll,
        Return,
        Inventory,
        LeftShoulder,
        RightShoulder,
        LocomotionMode,
        SwitchTarget,
        Multi,
        Start
    }

    public static event Action<Vector2> Move;
    public static event Action<InputAction.CallbackContext> Jump;
    public static event Action<InputAction.CallbackContext> Interact;
    public static event Action<InputAction.CallbackContext> TriggerMunin;
    public static event Action<InputAction.CallbackContext> TakeAll;
    public static event Action<InputAction.CallbackContext> Return;
    public static event Action<InputAction.CallbackContext> Inventory;
    public static event Action<InputAction.CallbackContext> LeftShoulder;
    public static event Action<InputAction.CallbackContext> RightShoulder;
    public static event Action<InputAction.CallbackContext> LocomotionMode;
    public static event Action<InputAction.CallbackContext> SwitchTarget;
    public static event Action<InputAction.CallbackContext> Multi;
    public static event Action<InputAction.CallbackContext> Start;
    public static event Action CameraRecenter;
    public static event Action CameraToggleFreeMode;

    private static Vector2 moveValue;
    private static Vector2 rawMoveValue;
    private static Vector2 cameraPanValue;
    private static Vector2 cameraPanFromMoveValue;
    private static Vector2 cameraOrbitValue;
    private static Vector2 cameraPointerDelta;
    private static Vector2 cameraPointerPosition;
    private static float cameraZoomValue;
    private static float flightVerticalValue;
    private static float cameraPointerScrollValue;
    private static bool cameraOrbitModifierPressed;
    private static bool cameraPanModifierPressed;
    private static bool cameraFreeModeActive;
    private static bool rightShoulderPressed;
    private static readonly System.Collections.Generic.Dictionary<InputGate, float> lastInputTimes = new System.Collections.Generic.Dictionary<InputGate, float>();
    private static bool interactConsumed;
    private static bool triggerMuninConsumed;
    private static float lastGameplayActivityTime = float.NegativeInfinity;
    private static uint gameplayActivityVersion;
    private static uint characterMovementActivityVersion;

    public static float InputDebounceSeconds { get; set; } = DefaultInputDebounceSeconds;

    public static Vector2 MoveValue => moveValue;
    public static Vector2 CameraPanValue => Vector2.ClampMagnitude(cameraPanValue + cameraPanFromMoveValue, 1f);
    public static Vector2 CameraOrbitValue => cameraOrbitValue;
    public static Vector2 CameraPointerDelta => cameraPointerDelta;
    public static Vector2 CameraPointerPosition => cameraPointerPosition;
    public static float CameraZoomValue => cameraZoomValue;
    public static float FlightVerticalValue => flightVerticalValue;
    public static float CameraPointerScrollValue => cameraPointerScrollValue;
    public static bool CameraOrbitModifierPressed => cameraOrbitModifierPressed;
    public static bool CameraPanModifierPressed => cameraPanModifierPressed;
    public static bool CameraFreeModeActive => cameraFreeModeActive;
    public static bool RightShoulderPressed => rightShoulderPressed;
    public static float LastGameplayActivityTime => lastGameplayActivityTime;
    public static uint GameplayActivityVersion => gameplayActivityVersion;
    public static uint CharacterMovementActivityVersion => characterMovementActivityVersion;
    internal static bool IsInteractConsumed => interactConsumed;
    internal static bool IsTriggerMuninConsumed => triggerMuninConsumed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        Move = null;
        Jump = null;
        Interact = null;
        TriggerMunin = null;
        TakeAll = null;
        Return = null;
        Inventory = null;
        LeftShoulder = null;
        RightShoulder = null;
        LocomotionMode = null;
        SwitchTarget = null;
        Multi = null;
        Start = null;
        CameraRecenter = null;
        CameraToggleFreeMode = null;

        moveValue = Vector2.zero;
        rawMoveValue = Vector2.zero;
        cameraPanValue = Vector2.zero;
        cameraPanFromMoveValue = Vector2.zero;
        cameraOrbitValue = Vector2.zero;
        cameraPointerDelta = Vector2.zero;
        cameraPointerPosition = Vector2.zero;
        cameraZoomValue = 0f;
        flightVerticalValue = 0f;
        cameraPointerScrollValue = 0f;
        cameraOrbitModifierPressed = false;
        cameraPanModifierPressed = false;
        cameraFreeModeActive = false;
        rightShoulderPressed = false;

        lastInputTimes.Clear();
        interactConsumed = false;
        triggerMuninConsumed = false;
        lastGameplayActivityTime = float.NegativeInfinity;
        gameplayActivityVersion = 0;
        characterMovementActivityVersion = 0;
        InputDebounceSeconds = DefaultInputDebounceSeconds;
    }

    public static void EnsureInitialized()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (float.IsNegativeInfinity(lastGameplayActivityTime))
        {
            lastGameplayActivityTime = Time.unscaledTime;
        }

        LocalPlayerInput.EnsureInstance();
    }

    internal static void SetMoveValue(Vector2 value)
    {
        if (JoinSyncSystem.IsGameplayBlocked)
        {
            value = Vector2.zero;
        }

        if (value.sqrMagnitude > 0.0001f)
        {
            NotifyGameplayActivity();
        }

        rawMoveValue = value;
        ApplyMoveRouting(suppressImmediateCharacterMove: false);
        if (moveValue.sqrMagnitude > 0.0001f)
        {
            characterMovementActivityVersion++;
        }
    }

    internal static void SetCameraFreeModeActive(bool active, bool suppressImmediateCharacterMove)
    {
        cameraFreeModeActive = active;
        ApplyMoveRouting(suppressImmediateCharacterMove);
    }

    private static void ApplyMoveRouting(bool suppressImmediateCharacterMove)
    {
        bool rerouteMoveToCamera = cameraFreeModeActive && !InputFocusStack.HasAnyFocus();

        if (rerouteMoveToCamera)
        {
            moveValue = Vector2.zero;
            cameraPanFromMoveValue = rawMoveValue;
        }
        else
        {
            cameraPanFromMoveValue = Vector2.zero;
            moveValue = suppressImmediateCharacterMove ? Vector2.zero : rawMoveValue;
        }

        Move?.Invoke(moveValue);
    }

    internal static void SetCameraPanValue(Vector2 value)
    {
        if (JoinSyncSystem.IsGameplayBlocked)
        {
            value = Vector2.zero;
        }

        if (value.sqrMagnitude > 0.0001f)
        {
            NotifyGameplayActivity();
        }

        cameraPanValue = value;
    }

    internal static void SetCameraOrbitValue(Vector2 value)
    {
        if (JoinSyncSystem.IsGameplayBlocked)
        {
            value = Vector2.zero;
        }

        if (value.sqrMagnitude > 0.0001f)
        {
            NotifyGameplayActivity();
        }

        cameraOrbitValue = value;
    }

    internal static void SetCameraZoomValue(float value)
    {
        if (JoinSyncSystem.IsGameplayBlocked)
        {
            value = 0f;
        }

        if (Mathf.Abs(value) > 0.0001f)
        {
            NotifyGameplayActivity();
        }

        cameraZoomValue = value;
    }

    internal static void SetFlightVerticalValue(float value)
    {
        if (JoinSyncSystem.IsGameplayBlocked)
        {
            value = 0f;
        }

        if (Mathf.Abs(value) > 0.0001f)
        {
            NotifyGameplayActivity();
        }

        flightVerticalValue = Mathf.Clamp(value, -1f, 1f);
    }

    internal static void SetCameraPointerScrollValue(float value)
    {
        if (JoinSyncSystem.IsGameplayBlocked)
        {
            value = 0f;
        }

        if (Mathf.Abs(value) > 0.0001f)
        {
            NotifyGameplayActivity();
        }

        cameraPointerScrollValue = value;
    }

    internal static void SetCameraPointerDelta(Vector2 value)
    {
        if (JoinSyncSystem.IsGameplayBlocked)
        {
            value = Vector2.zero;
        }

        if (value.sqrMagnitude > 0.0001f)
        {
            NotifyGameplayActivity();
        }

        cameraPointerDelta = value;
    }

    internal static void SetCameraPointerPosition(Vector2 value)
    {
        if ((value - cameraPointerPosition).sqrMagnitude > 0.01f)
        {
            NotifyGameplayActivity();
        }

        cameraPointerPosition = value;
    }

    internal static void SetCameraOrbitModifierPressed(bool value)
    {
        cameraOrbitModifierPressed = value && !JoinSyncSystem.IsGameplayBlocked;
        if (cameraOrbitModifierPressed)
        {
            NotifyGameplayActivity();
        }
    }

    internal static void SetCameraPanModifierPressed(bool value)
    {
        cameraPanModifierPressed = value && !JoinSyncSystem.IsGameplayBlocked;
        if (cameraPanModifierPressed)
        {
            NotifyGameplayActivity();
        }
    }

    internal static void SetRightShoulderPressed(bool value)
    {
        rightShoulderPressed = value && !JoinSyncSystem.IsGameplayBlocked;
        if (rightShoulderPressed)
        {
            NotifyGameplayActivity();
        }
    }

    internal static Vector2 ConsumeCameraPointerDelta()
    {
        Vector2 value = cameraPointerDelta;
        cameraPointerDelta = Vector2.zero;
        return value;
    }

    internal static float ConsumeCameraPointerScrollValue()
    {
        float value = cameraPointerScrollValue;
        cameraPointerScrollValue = 0f;
        return value;
    }

    internal static void RaiseInteract(InputAction.CallbackContext context)
    {
        if (!AllowInput(InputGate.Interact))
        {
            return;
        }

        interactConsumed = false;
        if (!InputFocusStack.HasAnyFocus() &&
            RuntimeOutlineSelectionManager.ActiveInteractable is ILocalInteractHandler activeHandler &&
            activeHandler.TryHandleLocalInteract())
        {
            interactConsumed = true;
            return;
        }

        Interact?.Invoke(context);
        if (interactConsumed || InputFocusStack.HasAnyFocus())
        {
            return;
        }

        RaiseJump(context);
    }

    internal static void RaiseJump(InputAction.CallbackContext context)
    {
        if (!AllowInput(InputGate.Jump))
        {
            return;
        }

        Jump?.Invoke(context);
    }

    internal static void ConsumeInteract()
    {
        interactConsumed = true;
    }

    internal static bool TryConsumeInteract()
    {
        if (interactConsumed)
        {
            return false;
        }

        interactConsumed = true;
        return true;
    }

    internal static void ConsumeTriggerMunin()
    {
        triggerMuninConsumed = true;
    }

    internal static bool TryConsumeTriggerMunin()
    {
        if (triggerMuninConsumed)
        {
            return false;
        }

        triggerMuninConsumed = true;
        return true;
    }

    internal static void RaiseTriggerMunin(InputAction.CallbackContext context)
    {
        if (!AllowInput(InputGate.TriggerMunin))
        {
            return;
        }

        triggerMuninConsumed = false;
        TriggerMunin?.Invoke(context);
    }

    internal static void RaiseTakeAll(InputAction.CallbackContext context)
    {
        if (!AllowInput(InputGate.TakeAll))
        {
            return;
        }
        TakeAll?.Invoke(context);
    }

    internal static void RaiseReturn(InputAction.CallbackContext context)
    {
        if (!AllowInput(InputGate.Return))
        {
            return;
        }
        Return?.Invoke(context);
    }

    internal static void RaiseInventory(InputAction.CallbackContext context)
    {
        if (!AllowInput(InputGate.Inventory))
        {
            return;
        }
        Inventory?.Invoke(context);
    }

    internal static void RaiseLeftShoulder(InputAction.CallbackContext context)
    {
        if (!AllowInput(InputGate.LeftShoulder))
        {
            return;
        }
        LeftShoulder?.Invoke(context);
    }

    internal static void RaiseRightShoulder(InputAction.CallbackContext context)
    {
        if (!AllowInput(InputGate.RightShoulder))
        {
            return;
        }

        RightShoulder?.Invoke(context);
    }

    internal static void RaiseLocomotionMode(InputAction.CallbackContext context)
    {
        if (!AllowInput(InputGate.LocomotionMode))
        {
            return;
        }

        LocomotionMode?.Invoke(context);
    }

    internal static void RaiseSwitchTarget(InputAction.CallbackContext context)
    {
        if (!AllowInput(InputGate.SwitchTarget))
        {
            return;
        }

        SwitchTarget?.Invoke(context);
    }

    internal static void RaiseMulti(InputAction.CallbackContext context)
    {
        if (!AllowInput(InputGate.Multi))
        {
            return;
        }
        Multi?.Invoke(context);
    }

    internal static void RaiseStart(InputAction.CallbackContext context)
    {
        if (!AllowInput(InputGate.Start))
        {
            return;
        }
        Start?.Invoke(context);
    }

    internal static void RaiseCameraRecenter()
    {
        if (JoinSyncSystem.IsGameplayBlocked)
        {
            return;
        }

        NotifyGameplayActivity();
        SetCameraFreeModeActive(false, suppressImmediateCharacterMove: true);
        CameraRecenter?.Invoke();
    }

    internal static void RaiseCameraToggleFreeMode()
    {
        // Free camera was part of the archived custom camera stack. UCC owns gameplay camera motion now.
        NotifyGameplayActivity();
        SetCameraFreeModeActive(false, suppressImmediateCharacterMove: false);
        CameraToggleFreeMode?.Invoke();
    }

    internal static void ResetMove()
    {
        SetRightShoulderPressed(false);
        SetFlightVerticalValue(0f);
        SetMoveValue(Vector2.zero);
    }

    internal static void ResetCamera()
    {
        rawMoveValue = Vector2.zero;
        cameraFreeModeActive = false;
        cameraPanFromMoveValue = Vector2.zero;
        cameraPanValue = Vector2.zero;
        cameraOrbitValue = Vector2.zero;
        cameraPointerDelta = Vector2.zero;
        cameraPointerPosition = Vector2.zero;
        cameraZoomValue = 0f;
        flightVerticalValue = 0f;
        cameraPointerScrollValue = 0f;
        cameraOrbitModifierPressed = false;
        cameraPanModifierPressed = false;
        rightShoulderPressed = false;
    }

    private static bool AllowInput(InputGate gate)
    {
        if (JoinSyncSystem.IsGameplayBlocked)
        {
            return false;
        }

        float debounce = InputDebounceSeconds;
        if (debounce <= 0f)
        {
            NotifyGameplayActivity();
            return true;
        }

        float now = Time.unscaledTime;
        if (lastInputTimes.TryGetValue(gate, out float lastTime) &&
            now >= lastTime &&
            now - lastTime < debounce)
        {
            return false;
        }

        lastInputTimes[gate] = now;
        NotifyGameplayActivity();
        return true;
    }

    private static void NotifyGameplayActivity()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        lastGameplayActivityTime = Time.unscaledTime;
        gameplayActivityVersion++;
    }
}
