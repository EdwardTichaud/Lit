using System;
using Unity.Netcode;
using UnityEngine;

// Context local au client pour referencer le personnage controle.
public static class LocalPlayerContext
{
    private const bool LogContextWrites = false;

    public enum Authority
    {
        Default = 0,
        MultiplayerAssignment = 1
    }

    public static event Action<Transform> LocalCharacterChanged;

    private static Transform localCharacterRoot;
    private static Authority localCharacterAuthority;
    private static string localCharacterSource = string.Empty;

    public static Transform LocalCharacterRoot => localCharacterRoot;
    public static Authority LocalCharacterAuthority => localCharacterAuthority;
    public static string LocalCharacterSource => localCharacterSource;

    public static void SetLocalCharacter(
        Transform characterRoot,
        string source = null,
        Authority authority = Authority.Default)
    {
        if (characterRoot == null)
        {
            Clear(source, authority);
            return;
        }

        if (ShouldIgnoreWrite(characterRoot, authority, source, "set"))
        {
            return;
        }

        if (localCharacterRoot == characterRoot)
        {
            localCharacterAuthority = authority;
            localCharacterSource = source ?? string.Empty;
            return;
        }

        localCharacterRoot = characterRoot;
        localCharacterAuthority = authority;
        localCharacterSource = source ?? string.Empty;
        LogContextWrite("set", characterRoot, authority, source, "local character updated");
        LocalCharacterChanged?.Invoke(localCharacterRoot);
    }

    public static void ClearIfMatch(
        Transform characterRoot,
        string source = null,
        Authority authority = Authority.Default)
    {
        if (localCharacterRoot != characterRoot)
        {
            return;
        }

        if (ShouldIgnoreWrite(characterRoot, authority, source, "clear_if_match"))
        {
            return;
        }

        localCharacterRoot = null;
        localCharacterAuthority = Authority.Default;
        localCharacterSource = string.Empty;
        LogContextWrite("clear_if_match", characterRoot, authority, source, "local character cleared");
        LocalCharacterChanged?.Invoke(null);
    }

    public static void Clear(
        string source = null,
        Authority authority = Authority.Default)
    {
        if (localCharacterRoot == null)
        {
            return;
        }

        if (ShouldIgnoreWrite(localCharacterRoot, authority, source, "clear"))
        {
            return;
        }

        Transform previousRoot = localCharacterRoot;
        localCharacterRoot = null;
        localCharacterAuthority = Authority.Default;
        localCharacterSource = string.Empty;
        LogContextWrite("clear", previousRoot, authority, source, "local character cleared");
        LocalCharacterChanged?.Invoke(null);
    }

    private static bool ShouldIgnoreWrite(Transform target, Authority incomingAuthority, string source, string operation)
    {
        if (!IsMultiplayerListening())
        {
            return false;
        }

        if (localCharacterRoot == null)
        {
            return false;
        }

        if (incomingAuthority >= localCharacterAuthority)
        {
            return false;
        }

        if (localCharacterRoot == target)
        {
            return true;
        }

        LogContextWrite(
            $"{operation}_ignored",
            target,
            incomingAuthority,
            source,
            $"ignored weaker local character authority currentAuthority='{localCharacterAuthority}' currentSource='{localCharacterSource}'");
        return true;
    }

    private static bool IsMultiplayerListening()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    private static void LogContextWrite(string operation, Transform target, Authority authority, string source, string reason)
    {
        if (!LogContextWrites)
        {
            return;
        }

        string path = target != null ? NetcodePlayerUtils.GetTransformPath(target) : string.Empty;
        Debug.Log(
            $"[NetcodeControl] system='local_context' operation='{operation}' path='{path}' authority='{authority}' source='{source ?? string.Empty}' reason='{reason}'");
    }
}
