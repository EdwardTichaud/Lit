using System.Collections.Generic;
using UnityEngine;

// Pile statique pour gerer le focus d'input (UI vs gameplay).
public interface ICameraInputPassthrough
{
    bool AllowCameraInput { get; }
}

public static class InputFocusStack
{
    private static readonly List<object> stack = new List<object>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        stack.Clear();
    }

    public static bool HasAnyFocus()
    {
        PurgeDestroyedOwners();
        return stack.Count > 0;
    }

    public static bool HasAnyFocusBlockingCamera()
    {
        PurgeDestroyedOwners();
        if (stack.Count == 0)
        {
            return false;
        }

        object top = stack[stack.Count - 1];
        if (top is ICameraInputPassthrough passthrough && passthrough.AllowCameraInput)
        {
            return false;
        }

        return true;
    }

    public static bool HasFocus(object owner)
    {
        PurgeDestroyedOwners();
        if (owner == null || stack.Count == 0)
        {
            return false;
        }

        return ReferenceEquals(stack[stack.Count - 1], owner);
    }

    public static void Push(object owner)
    {
        PurgeDestroyedOwners();
        if (owner == null)
        {
            return;
        }

        // Evite les doublons en replacant l'owner en haut de pile.
        Remove(owner);
        stack.Add(owner);
        InputModeCoordinator.Enter(owner, InputMode.UserInterface);
    }

    public static void PushExclusive(object owner)
    {
        PurgeDestroyedOwners();
        if (owner == null)
        {
            return;
        }

        for (int i = stack.Count - 1; i >= 0; i--)
        {
            InputModeCoordinator.Exit(stack[i]);
        }
        stack.Clear();
        stack.Add(owner);
        InputModeCoordinator.Enter(owner, InputMode.UserInterface);
    }

    public static void Pop(object owner)
    {
        PurgeDestroyedOwners();
        if (owner == null)
        {
            return;
        }

        Remove(owner);
        InputModeCoordinator.Exit(owner);
    }

    public static void Clear()
    {
        stack.Clear();
        InputModeCoordinator.Clear();
    }

    public static void PushDialogue(object owner)
    {
        PurgeDestroyedOwners();
        if (owner == null) return;
        Remove(owner);
        stack.Add(owner);
        InputModeCoordinator.Enter(owner, InputMode.Dialogue);
    }

    public static void PushPlacement(object owner)
    {
        PurgeDestroyedOwners();
        if (owner == null) return;
        Remove(owner);
        stack.Add(owner);
        InputModeCoordinator.Enter(owner, InputMode.Placement);
    }

    private static void Remove(object owner)
    {
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(stack[i], owner))
            {
                stack.RemoveAt(i);
            }
        }
    }

    // Un panneau detruit sans recevoir OnDisable ne doit jamais laisser le
    // gameplay bloque. Les piles statiques ne sont pas nettoyees par Unity.
    private static void PurgeDestroyedOwners()
    {
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            object owner = stack[i];
            if (owner is UnityEngine.Object unityOwner && unityOwner == null)
            {
                stack.RemoveAt(i);
                InputModeCoordinator.Exit(owner);
            }
        }
    }
}
