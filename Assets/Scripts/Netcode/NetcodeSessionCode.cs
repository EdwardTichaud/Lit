using UnityEngine;

// Gere la generation et la normalisation des codes de session (host/join).
public static class NetcodeSessionCode
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate(int length)
    {
        if (length <= 0)
        {
            length = 6;
        }

        char[] buffer = new char[length];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = Alphabet[Random.Range(0, Alphabet.Length)];
        }

        return new string(buffer);
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        char[] buffer = new char[value.Length];
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '-' || c == ' ' || c == '_')
            {
                continue;
            }

            c = char.ToUpperInvariant(c);
            if (IsAllowed(c))
            {
                buffer[count++] = c;
            }
        }

        return count > 0 ? new string(buffer, 0, count) : string.Empty;
    }

    public static bool TryNormalize(string value, out string normalized)
    {
        normalized = Normalize(value);
        return !string.IsNullOrEmpty(normalized);
    }

    public static bool IsValid(string value, int minLength = 4, int maxLength = 12)
    {
        string normalized = Normalize(value);
        if (normalized.Length < minLength || normalized.Length > maxLength)
        {
            return false;
        }

        for (int i = 0; i < normalized.Length; i++)
        {
            if (!IsAllowed(normalized[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryGetPort(string value, ushort basePort, ushort range, out ushort port, out string normalized)
    {
        port = 0;
        normalized = Normalize(value);
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        if (range == 0)
        {
            range = 1;
        }

        uint hash = NetcodeStableHash.Hash32(normalized);
        port = (ushort)(basePort + (hash % range));
        if (port == 0)
        {
            port = basePort;
        }

        return true;
    }

    public static string NormalizeAddress(string value, string fallback = "")
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback.Trim();
    }

    public static bool TryCreateEndpoint(string code, string address, ushort basePort, ushort range, out NetcodeSessionEndpoint endpoint)
    {
        endpoint = default;
        if (!TryGetPort(code, basePort, range, out ushort port, out string normalizedCode))
        {
            return false;
        }

        string normalizedAddress = NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalizedAddress))
        {
            return false;
        }

        endpoint = new NetcodeSessionEndpoint(normalizedCode, normalizedAddress, port);
        return true;
    }

    private static bool IsAllowed(char c)
    {
        return Alphabet.IndexOf(c) >= 0;
    }
}
