using System;
using System.Text;
using UnityEngine;

// Identite persistante cote client (PlayerPrefs) pour reconnecter au meme personnage.
public static class NetcodeClientIdentity
{
    private const string PlayerIdKey = "LitPlayerId";

    public static string GetOrCreatePlayerId()
    {
        string key = PlayerIdKey;
        foreach (string argument in Environment.GetCommandLineArgs())
            if (argument.StartsWith("-lit-profile=", StringComparison.Ordinal))
                key += "." + argument.Substring("-lit-profile=".Length);
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
        return Encoding.UTF8.GetBytes("lit-private-1|" + Application.version + "|" + playerId);
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
            if (payload.Length > 256) return false;
            string[] parts = Encoding.UTF8.GetString(payload).Split('|');
            if (parts.Length != 3 || parts[0] != "lit-private-1" || parts[1] != Application.version ||
                !Guid.TryParseExact(parts[2], "N", out _)) return false;
            playerId = parts[2];
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
}
