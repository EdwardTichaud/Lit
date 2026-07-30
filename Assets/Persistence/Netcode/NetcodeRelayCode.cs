using System;

/// <summary>
/// Validation volontairement permissive des codes Relay. Le service Unity reste
/// la source de verite : ce filtre evite seulement une requete manifestement vide.
/// </summary>
public static class NetcodeRelayCode
{
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
            char current = value[i];
            if (!char.IsLetterOrDigit(current))
            {
                continue;
            }

            buffer[count++] = char.ToUpperInvariant(current);
        }

        return count == 0 ? string.Empty : new string(buffer, 0, count);
    }

    public static bool IsValid(string value)
    {
        string normalized = Normalize(value);
        return normalized.Length >= 4 && normalized.Length <= 16;
    }
}
