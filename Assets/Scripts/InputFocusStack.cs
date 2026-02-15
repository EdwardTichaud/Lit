using System.Collections.Generic;

// Pile statique pour gerer le focus d'input (UI vs gameplay).
public static class InputFocusStack
{
    private static readonly List<object> stack = new List<object>();

    public static bool HasAnyFocus()
    {
        return stack.Count > 0;
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
