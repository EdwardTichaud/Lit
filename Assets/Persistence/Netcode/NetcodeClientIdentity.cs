using System;
using System.Text;
using UnityEngine;

// Identite persistante cote client (PlayerPrefs) pour reconnecter au meme personnage.
public static class NetcodeClientIdentity
{
    private const string PlayerIdKey = "LitPlayerId";

    public static string GetOrCreatePlayerId()
    {
        string existing = PlayerPrefs.GetString(PlayerIdKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        string created = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(PlayerIdKey, created);
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
}
