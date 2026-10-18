using System;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>Read-only proof of SceneMarker's direct-child CharacterInfo fallback, never a bake.</summary>
public static class CombatMarkerInventory
{
    public static string Field(string text, string name) => Regex.Match(text, @"(?m)^[ \t]*" + Regex.Escape(name) + @":[ \t]*([^\r\n]*)").Groups[1].Value.Trim();
    private static string Id(string reference) => Regex.Match(reference, @"\bfileID:\s*(-?\d+)").Groups[1].Value;
    private static string Guid(string reference) => Regex.Match(reference, @"\bguid:\s*([a-fA-F0-9]{32})").Groups[1].Value;
    private static string DocumentId(string document) => Regex.Match(document, @"\A!u!\d+ &(-?\d+)").Groups[1].Value;

    public static string DirectChildFallback(string scene, string marker, Func<string, string> prefabSource, Func<string, string> scriptPath)
    {
        string data = Guid(Field(marker, "characterData"));
        if (data.Length == 0 || Id(Field(marker, "characterData")) == "0") return null;
        string[] documents = Regex.Split(scene, @"(?m)^--- ");
        string parent = documents.SingleOrDefault(document => document.StartsWith("!u!4 &", StringComparison.Ordinal) &&
            !document.Split('\n')[0].Contains("stripped") && Id(Field(document, "m_GameObject")) == Id(Field(marker, "m_GameObject")));
        if (parent == null) return null;
        foreach (string child in documents.Where(document => document.StartsWith("!u!4 &", StringComparison.Ordinal)))
        {
            string childId = DocumentId(child);
            if (!Regex.IsMatch(parent, @"(?m)^[ \t]*-[ \t]*\{fileID: " + Regex.Escape(childId) + @"\}[ \t]*\r?$")) continue;
            string instanceId = Id(Field(child, "m_PrefabInstance"));
            string instance = documents.SingleOrDefault(document => document.StartsWith("!u!1001 &", StringComparison.Ordinal) && DocumentId(document) == instanceId);
            if (instance == null || Id(Field(instance, "m_TransformParent")) != DocumentId(parent)) continue;
            // Unknown overrides/removals could invalidate the component/data proof. Leave those blocked.
            if (Regex.IsMatch(instance, @"propertyPath:\s*characterData\b") ||
                new[] { "m_RemovedComponents", "m_RemovedGameObjects", "m_AddedComponents" }.Any(field => Field(instance, field) != "[]")) continue;
            string prefabGuid = Guid(Field(instance, "m_SourcePrefab"));
            if (Guid(Field(child, "m_CorrespondingSourceObject")) != prefabGuid) continue;
            string prefab = prefabSource(prefabGuid);
            if (string.IsNullOrEmpty(prefab)) continue;
            string[] parts = Regex.Split(prefab, @"(?m)^--- ");
            string transform = parts.SingleOrDefault(part => part.StartsWith("!u!4 &", StringComparison.Ordinal) && DocumentId(part) == Id(Field(child, "m_CorrespondingSourceObject")));
            if (transform == null || Id(Field(transform, "m_Father")) != "0") continue;
            string root = Id(Field(transform, "m_GameObject"));
            foreach (string component in parts.Where(part => part.StartsWith("!u!114 &", StringComparison.Ordinal) && Id(Field(part, "m_GameObject")) == root))
            {
                string script = scriptPath(Guid(Field(component, "m_Script")));
                if ((script.EndsWith("/CharacterInfo.cs", StringComparison.Ordinal) || script.EndsWith("/LitBrainsEnemy.cs", StringComparison.Ordinal)) &&
                    Guid(Field(component, "characterData")) == data && Id(Field(component, "characterData")) == Id(Field(marker, "characterData")))
                    return "SceneMarker.ResolveBakedCharacterInstance direct-child fallback; childTransform=" + childId + "; prefab=" + prefabGuid + "; component=" + DocumentId(component) + "; CharacterData=" + data;
            }
        }
        return null;
    }
}
