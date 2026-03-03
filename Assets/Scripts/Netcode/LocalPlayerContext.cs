using System;
using UnityEngine;

// Context local au client pour referencer le personnage controle.
public static class LocalPlayerContext
{
    public static event Action<Transform> LocalCharacterChanged;

    private static Transform localCharacterRoot;

    public static Transform LocalCharacterRoot => localCharacterRoot;

    public static void SetLocalCharacter(Transform characterRoot)
    {
        if (localCharacterRoot == characterRoot)
        {
            return;
        }

        localCharacterRoot = characterRoot;
        LocalCharacterChanged?.Invoke(localCharacterRoot);
    }

    public static void ClearIfMatch(Transform characterRoot)
    {
        if (localCharacterRoot != characterRoot)
        {
            return;
        }

        localCharacterRoot = null;
        LocalCharacterChanged?.Invoke(null);
    }
}
