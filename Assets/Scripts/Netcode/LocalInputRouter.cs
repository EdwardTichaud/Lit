using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Route les inputs du joueur local vers les systems interessés.
public static class LocalInputRouter
{
    private enum InputGate
    {
        Interact,
        ToggleTorch,
        TakeAll,
        Return,
        Inventory,
        LeftShoulder,
        Multi,
        Start
    }

    public static event Action<Vector2> Move;
    public static event Action<InputAction.CallbackContext> Interact;
    public static event Action<InputAction.CallbackContext> ToggleTorch;
    public static event Action<InputAction.CallbackContext> TakeAll;
    public static event Action<InputAction.CallbackContext> Return;
    public static event Action<InputAction.CallbackContext> Inventory;
    public static event Action<InputAction.CallbackContext> LeftShoulder;
    public static event Action<InputAction.CallbackContext> Multi;
    public static event Action<InputAction.CallbackContext> Start;

    private static Vector2 moveValue;
    private static readonly System.Collections.Generic.Dictionary<InputGate, float> lastInputTimes = new System.Collections.Generic.Dictionary<InputGate, float>();

    public static float InputDebounceSeconds { get; set; } = 0.15f;

    public static Vector2 MoveValue => moveValue;

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

        moveValue = value;
        Move?.Invoke(moveValue);
    }

    internal static void RaiseInteract(InputAction.CallbackContext context)
    {
        if (!AllowInput(InputGate.Interact))
        {
            return;
        }
        Interact?.Invoke(context);
    }

    internal static void RaiseToggleTorch(InputAction.CallbackContext context)
    {
        if (!AllowInput(InputGate.ToggleTorch))
        {
            return;
        }
        ToggleTorch?.Invoke(context);
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

    internal static void ResetMove()
    {
        SetMoveValue(Vector2.zero);
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
