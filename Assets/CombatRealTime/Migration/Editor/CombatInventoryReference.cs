using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>Conservative, testable inventory rules; no asset mutation or implicit parent-project lookup.</summary>
public static class CombatInventoryReference
{
    public static readonly Regex Loads = new Regex(@"\b(Resources\.(?:LoadAll|LoadAsync|Load)|Addressables\.(?:LoadAssetAsync|LoadAssetsAsync|InstantiateAsync|LoadSceneAsync)|AssetBundle\.(?:LoadFromFile|LoadFromMemory))\s*(?:<(?<type>[^;\r\n]+?)>)?\s*\(([^)\r\n]*)", RegexOptions.Compiled);

    public static string ClassifyGuid(string guid, string resolvedPath, bool payloadExists)
    {
        if (guid == new string('0', 32)) return "null-guid";
        // Exact Unity resource namespaces, not every GUID with a zero prefix.
        if (guid == "0000000000000000e000000000000000" || guid == "0000000000000000f000000000000000") return "builtin-resource";
        if (guid == "0000000000000000d000000000000000" && resolvedPath == "Library/unity editor resources") return "builtin-resource";
        if (string.IsNullOrEmpty(resolvedPath)) return "missing-guid";
        return payloadExists ? "asset-guid" : "missing-guid-payload";
    }

    // Keep offsets/newlines stable so call-site identities refer to the original source.
    public static string WithoutComments(string source)
    {
        return Regex.Replace(source, "@\"(?:\"\"|[^\"])*\"|\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'|//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/", match =>
            match.Value.StartsWith("//", StringComparison.Ordinal) || match.Value.StartsWith("/*", StringComparison.Ordinal)
                ? new string(match.Value.Select(c => c == '\r' || c == '\n' ? c : ' ').ToArray()) : match.Value);
    }

    public static bool TryConstant(string source, string expression, out string value)
    {
        var literal = Regex.Match(expression.Trim(), "^\"([^\"\\\\]*)\"$");
        if (literal.Success) { value = literal.Groups[1].Value; return true; }
        // Require one unambiguous const declaration. Mutable fields/locals are not constants.
        var constants = Regex.Matches(source, @"\bconst\s+string\s+" + Regex.Escape(expression.Trim()) + "\\s*=\\s*\"([^\"\\\\]*)\"\\s*;");
        value = constants.Count == 1 ? constants[0].Groups[1].Value : null;
        return constants.Count == 1;
    }

    public static bool TryResourcePrefix(string expression, out string prefix)
    {
        var match = Regex.Match(expression.Trim(), "^\"([^\"\\\\]+/)\"\\s*\\+\\s*[A-Za-z_][A-Za-z0-9_]*(?:\\.name)?$");
        prefix = match.Success ? match.Groups[1].Value : null;
        return match.Success;
    }

    public static string[] ConstantChoices(string source, string expression)
    {
        if (TryConstant(source, expression, out string literal)) return new[] { literal };
        if (!Regex.IsMatch(expression, @"^[A-Za-z_]\w*$")) return Array.Empty<string>();
        var definitions = Regex.Matches(source, @"\bstring\s+" + Regex.Escape(expression) + @"\s*=\s*\w+\s*\?\s*(\w+)\s*:\s*(\w+)\s*;");
        if (definitions.Count != 1 || !TryConstant(source, definitions[0].Groups[1].Value, out string first) ||
            !TryConstant(source, definitions[0].Groups[2].Value, out string second)) return Array.Empty<string>();
        // Discovery only: a local can later be reassigned, so this does not close the call-site coverage gate.
        return new[] { first, second }.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static IEnumerable<string> ResourceCandidates(IEnumerable<string> keys, string key, bool namespacePrefix, bool loadAll)
    {
        return keys.Where(candidate => namespacePrefix ? candidate.StartsWith(key, StringComparison.Ordinal) :
            candidate == key || loadAll && (key.Length == 0 || candidate.StartsWith(key.TrimEnd('/') + "/", StringComparison.Ordinal)));
    }

    public static string[] UnusedTextureSlots(string material, string guid, Func<string, bool> shaderHasProperty)
    {
        string saved = Regex.Match(material, @"(?ms)^  m_SavedProperties:\s*\r?\n(?<body>.*)").Groups["body"].Value;
        string textures = Regex.Match(saved, @"(?ms)^    m_TexEnvs:\s*\r?\n(?<body>.*?)(?=^    m_\w+:|\z)").Groups["body"].Value;
        var matches = Regex.Matches(textures, @"(?m)^    - ([^:\r\n]+):[ \t]*\r?\n[ \t]*m_Texture: \{[^}\r\n]*\bguid: " + Regex.Escape(guid) + @"\b[^}\r\n]*\}");
        int references = Regex.Matches(material, @"\bguid:\s*" + Regex.Escape(guid) + @"\b").Count;
        var slots = matches.Cast<Match>().Select(match => match.Groups[1].Value.Trim()).ToArray();
        return references > 0 && references == slots.Length && slots.All(slot => !shaderHasProperty(slot)) ? slots : Array.Empty<string>();
    }
}
