using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Route les inputs du joueur local vers les systems interessés.
public static class LocalInputRouter
{
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

    public static float InputDebounceSeconds { get; set; } = 0.15f;

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
    internal static bool IsInteractConsumed => interactConsumed;
    internal static bool IsTriggerMuninConsumed => triggerMuninConsumed;

    public static void EnsureInitialized()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        LocalPlayerInput.EnsureInstance();
    }

    internal static void SetMoveValue(Vector2 value)
    {
        if (JoinSyncSystem.IsGameplayBlocked)
        {
            value = Vector2.zero;
        }

        rawMoveValue = value;
        ApplyMoveRouting(suppressImmediateCharacterMove: false);
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

        cameraPanValue = value;
    }

    internal static void SetCameraOrbitValue(Vector2 value)
    {
        if (JoinSyncSystem.IsGameplayBlocked)
        {
            value = Vector2.zero;
        }

        cameraOrbitValue = value;
    }

    internal static void SetCameraZoomValue(float value)
    {
        if (JoinSyncSystem.IsGameplayBlocked)
        {
            value = 0f;
        }

        cameraZoomValue = value;
    }

    internal static void SetFlightVerticalValue(float value)
    {
        if (JoinSyncSystem.IsGameplayBlocked)
        {
            value = 0f;
        }

        flightVerticalValue = Mathf.Clamp(value, -1f, 1f);
    }

    internal static void SetCameraPointerScrollValue(float value)
    {
        if (JoinSyncSystem.IsGameplayBlocked)
        {
            value = 0f;
        }

        cameraPointerScrollValue = value;
    }

    internal static void SetCameraPointerDelta(Vector2 value)
    {
        if (JoinSyncSystem.IsGameplayBlocked)
        {
            value = Vector2.zero;
        }

        cameraPointerDelta = value;
    }

    internal static void SetCameraPointerPosition(Vector2 value)
    {
        cameraPointerPosition = value;
    }

    internal static void SetCameraOrbitModifierPressed(bool value)
    {
        cameraOrbitModifierPressed = value && !JoinSyncSystem.IsGameplayBlocked;
    }

    internal static void SetCameraPanModifierPressed(bool value)
    {
        cameraPanModifierPressed = value && !JoinSyncSystem.IsGameplayBlocked;
    }

    internal static void SetRightShoulderPressed(bool value)
    {
        rightShoulderPressed = value && !JoinSyncSystem.IsGameplayBlocked;
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
        if (RuntimeOutlineSelectionManager.ActiveInteractable is ILocalInteractHandler activeHandler &&
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

        SetCameraFreeModeActive(false, suppressImmediateCharacterMove: true);
        CameraRecenter?.Invoke();
    }

    internal static void RaiseCameraToggleFreeMode()
    {
        // Free camera was part of the archived custom camera stack. UCC owns gameplay camera motion now.
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
            return true;
        }

        float now = Time.unscaledTime;
        if (lastInputTimes.TryGetValue(gate, out float lastTime) && now - lastTime < debounce)
        {
            return false;
        }

        lastInputTimes[gate] = now;
        return true;
    }
}
