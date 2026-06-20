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
        return stack.Count > 0;
    }

    public static bool HasAnyFocusBlockingCamera()
    {
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
        if (owner == null || stack.Count == 0)
        {
            return false;
        }

        return ReferenceEquals(stack[stack.Count - 1], owner);
    }

    public static void Push(object owner)
    {
        if (owner == null)
        {
            return;
        }

        // Evite les doublons en replacant l'owner en haut de pile.
        Remove(owner);
        stack.Add(owner);
    }

    public static void Pop(object owner)
    {
        if (owner == null)
        {
            return;
        }

        Remove(owner);
    }

    public static void Clear()
    {
        stack.Clear();
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
}
