using System;
using System.Text;
using UnityEngine;

// Gere la generation et la normalisation des codes de session (host/join).
public static class NetcodeSessionCode
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const string InvitePrefix = "LIT1";
    private const char InviteSeparator = '.';

    public static string Generate(int length)
    {
        if (length <= 0)
        {
            length = 6;
        }

        char[] buffer = new char[length];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = Alphabet[UnityEngine.Random.Range(0, Alphabet.Length)];
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

    public static string CreateJoinCode(string sessionCode, string address)
    {
        string normalizedCode = Normalize(sessionCode);
        string normalizedAddress = NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalizedCode) || string.IsNullOrWhiteSpace(normalizedAddress))
        {
            return string.Empty;
        }

        return $"{InvitePrefix}{InviteSeparator}{normalizedCode}{InviteSeparator}{EncodeAddress(normalizedAddress)}";
    }

    public static string NormalizeJoinInput(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string compact = RemoveWhitespace(value);
        if (!LooksLikeJoinCode(compact))
        {
            if (LooksLikePartialJoinCodePrefix(compact))
            {
                return compact.ToUpperInvariant();
            }

            return Normalize(compact);
        }

        string[] parts = compact.Split(InviteSeparator);
        if (parts.Length < 2)
        {
            return compact.ToUpperInvariant();
        }

        string code = Normalize(parts[1]);
        if (parts.Length < 3)
        {
            return string.IsNullOrEmpty(code)
                ? InvitePrefix
                : $"{InvitePrefix}{InviteSeparator}{code}";
        }

        string addressToken = parts[2].Trim();
        for (int i = 3; i < parts.Length; i++)
        {
            addressToken += parts[i].Trim();
        }

        return string.IsNullOrEmpty(addressToken)
            ? $"{InvitePrefix}{InviteSeparator}{code}{InviteSeparator}"
            : $"{InvitePrefix}{InviteSeparator}{code}{InviteSeparator}{addressToken}";
    }

    public static bool TryParseJoinCode(string value, out string sessionCode, out string address)
    {
        sessionCode = string.Empty;
        address = string.Empty;

        string normalized = NormalizeJoinInput(value);
        if (!LooksLikeJoinCode(normalized))
        {
            return false;
        }

        string[] parts = normalized.Split(InviteSeparator);
        if (parts.Length != 3)
        {
            return false;
        }

        string code = Normalize(parts[1]);
        if (!IsValid(code))
        {
            return false;
        }

        string decodedAddress = DecodeAddress(parts[2]);
        if (string.IsNullOrWhiteSpace(decodedAddress))
        {
            return false;
        }

        sessionCode = code;
        address = NormalizeAddress(decodedAddress);
        return !string.IsNullOrWhiteSpace(address);
    }

    public static bool IsValidJoinInput(string value)
    {
        if (TryParseJoinCode(value, out _, out _))
        {
            return true;
        }

        if (LooksLikePartialJoinCodePrefix(RemoveWhitespace(value)))
        {
            return false;
        }

        return IsValid(value);
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

    public static bool TryCreateEndpointFromJoinInput(
        string joinInput,
        string fallbackAddress,
        ushort basePort,
        ushort range,
        out NetcodeSessionEndpoint endpoint)
    {
        if (TryParseJoinCode(joinInput, out string sessionCode, out string embeddedAddress))
        {
            return TryCreateEndpoint(sessionCode, embeddedAddress, basePort, range, out endpoint);
        }

        return TryCreateEndpoint(joinInput, fallbackAddress, basePort, range, out endpoint);
    }

    private static bool IsAllowed(char c)
    {
        return Alphabet.IndexOf(c) >= 0;
    }

    private static bool LooksLikeJoinCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.StartsWith(InvitePrefix + InviteSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePartialJoinCodePrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return InvitePrefix.StartsWith(value, StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(InvitePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            if (!char.IsWhiteSpace(value[i]))
            {
                builder.Append(value[i]);
            }
        }

        return builder.ToString();
    }

    private static string EncodeAddress(string address)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(address);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string DecodeAddress(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        try
        {
            string normalized = token.Trim().Replace('-', '+').Replace('_', '/');
            int padding = normalized.Length % 4;
            if (padding > 0)
            {
                normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
            }

            byte[] bytes = Convert.FromBase64String(normalized);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}
