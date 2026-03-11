using System;
using System.Text;
using UnityEngine;

// Identite persistante cote client (PlayerPrefs) pour reconnecter au meme personnage.
public static class NetcodeClientIdentity
{
    private const string PlayerIdKey = "LitPlayerId";
    private const string PlayerSlotArgumentPrefix = "-litPlayerSlot=";
    private const string PlayerSlotEnvKey = "LIT_PLAYER_SLOT";

    public static string GetOrCreatePlayerId()
    {
        string key = GetResolvedPlayerIdKey();
        string existing = PlayerPrefs.GetString(key, string.Empty);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        string created = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(key, created);
        PlayerPrefs.Save();
        return created;
    }

    public static byte[] BuildPayload()
    {
        string playerId = GetOrCreatePlayerId();
        return Encoding.UTF8.GetBytes(playerId);
    }

    public static bool TryGetPlayerId(byte[] payload, out string playerId)
    {
        playerId = string.Empty;
        if (payload == null || payload.Length == 0)
        {
            return false;
        }

        try
        {
            playerId = Encoding.UTF8.GetString(payload);
        }
        catch (Exception)
        {
            playerId = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(playerId))
        {
            playerId = string.Empty;
            return false;
        }

        return true;
    }

    private static string GetResolvedPlayerIdKey()
    {
        if (!TryResolvePlayerSlot(out string slot))
        {
            return PlayerIdKey;
        }

        return $"{PlayerIdKey}.{slot}";
    }

    private static bool TryResolvePlayerSlot(out string slot)
    {
        slot = Environment.GetEnvironmentVariable(PlayerSlotEnvKey);
        if (!string.IsNullOrWhiteSpace(slot))
        {
            slot = slot.Trim();
            return true;
        }

        string[] args = Environment.GetCommandLineArgs();
        if (args == null)
        {
            slot = string.Empty;
            return false;
        }

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.IsNullOrWhiteSpace(arg) || !arg.StartsWith(PlayerSlotArgumentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            slot = arg.Substring(PlayerSlotArgumentPrefix.Length).Trim();
            return !string.IsNullOrWhiteSpace(slot);
        }

        slot = string.Empty;
        return false;
    }
}
