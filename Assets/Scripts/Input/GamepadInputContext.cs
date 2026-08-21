using System;
using System.Collections.Generic;
using UnityEngine;

// Definit les proprietaires d'input exclusifs. Les contextes gameplay existants
// peuvent etre migres progressivement sans laisser un bouton atteindre le monde
// pendant un combat, une UI ou une cinematique.
public enum GamepadInputContext
{
    Gameplay,
    UserInterface,
    Placement,
    Combat,
    Cinematic
}

public static class GamepadInputContextStack
{
    private readonly struct Entry
    {
        public readonly object Owner;
        public readonly GamepadInputContext Context;

        public Entry(object owner, GamepadInputContext context)
        {
            Owner = owner;
            Context = context;
        }
    }

    private static readonly List<Entry> stack = new List<Entry>();

    public static event Action<GamepadInputContext> Changed;

    public static GamepadInputContext Current
    {
        get
        {
            PurgeDestroyedOwners();
            return CurrentUnsafe;
        }
    }

    public static bool IsGameplayInputSuppressed => Current != GamepadInputContext.Gameplay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        stack.Clear();
        Changed = null;
    }

    public static void Push(object owner, GamepadInputContext context)
    {
        PurgeDestroyedOwners();
        if (owner == null)
        {
            return;
        }

        Pop(owner, notify: false);
        stack.Add(new Entry(owner, context));
        Changed?.Invoke(Current);
    }

    public static void Pop(object owner)
    {
        PurgeDestroyedOwners();
        Pop(owner, notify: true);
    }

    public static void Clear()
    {
        if (stack.Count == 0)
        {
            return;
        }

        stack.Clear();
        Changed?.Invoke(Current);
    }

    private static void Pop(object owner, bool notify)
    {
        if (owner == null)
        {
            return;
        }

        bool removed = false;
        for (int i = stack.Count - 1; i >= 0; --i)
        {
            if (ReferenceEquals(stack[i].Owner, owner))
            {
                stack.RemoveAt(i);
                removed = true;
            }
        }

        if (removed && notify)
        {
            Changed?.Invoke(Current);
        }
    }

    private static GamepadInputContext CurrentUnsafe => stack.Count == 0
        ? GamepadInputContext.Gameplay
        : stack[stack.Count - 1].Context;

    // Les contextes proviennent souvent d'elements de scene. Si l'owner est
    // detruit pendant une transition, il ne doit pas conserver le gamepad.
    private static void PurgeDestroyedOwners()
    {
        for (int i = stack.Count - 1; i >= 0; --i)
        {
            if (stack[i].Owner is UnityEngine.Object unityOwner && unityOwner == null)
            {
                stack.RemoveAt(i);
            }
        }
    }
}
