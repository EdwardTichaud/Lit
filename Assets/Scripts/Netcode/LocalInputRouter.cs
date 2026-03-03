using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Route les inputs du joueur local vers les systems interessés.
public static class LocalInputRouter
{
    public static event Action<Vector2> Move;
    public static event Action<InputAction.CallbackContext> Interact;
    public static event Action<InputAction.CallbackContext> ToggleTorch;
    public static event Action<InputAction.CallbackContext> TakeAll;
    public static event Action<InputAction.CallbackContext> Return;
    public static event Action<InputAction.CallbackContext> Inventory;
    public static event Action<InputAction.CallbackContext> LeftShoulder;
    public static event Action<InputAction.CallbackContext> Multi;

    private static Vector2 moveValue;

    public static Vector2 MoveValue => moveValue;

    public static void EnsureInitialized()
    {
        LocalPlayerInput.EnsureInstance();
    }

    internal static void SetMoveValue(Vector2 value)
    {
        moveValue = value;
        Move?.Invoke(moveValue);
    }

    internal static void RaiseInteract(InputAction.CallbackContext context)
    {
        Interact?.Invoke(context);
    }

    internal static void RaiseToggleTorch(InputAction.CallbackContext context)
    {
        ToggleTorch?.Invoke(context);
    }

    internal static void RaiseTakeAll(InputAction.CallbackContext context)
    {
        TakeAll?.Invoke(context);
    }

    internal static void RaiseReturn(InputAction.CallbackContext context)
    {
        Return?.Invoke(context);
    }

    internal static void RaiseInventory(InputAction.CallbackContext context)
    {
        Inventory?.Invoke(context);
    }

    internal static void RaiseLeftShoulder(InputAction.CallbackContext context)
    {
        LeftShoulder?.Invoke(context);
    }

    internal static void RaiseMulti(InputAction.CallbackContext context)
    {
        Multi?.Invoke(context);
    }

    internal static void ResetMove()
    {
        SetMoveValue(Vector2.zero);
    }
}
