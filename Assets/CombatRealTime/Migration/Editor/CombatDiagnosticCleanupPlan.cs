using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>Surgical text plan for one deliberately deleted diagnostic. Never repairs gameplay behaviours or motions.</summary>
public static class CombatDiagnosticCleanupPlan
{
    public const string ScriptGuid = "adfec8d2897f4f6f84cb8cf8dcf590ff";
    public const string TypeName = "Assembly-CSharp::LucianJumpStateTraceBehaviour";
    public const string DeletionCommit = "0b219dcdc4b9fd67224cacda8d57ddb5c0210726";
    private static readonly Regex Headers = new Regex(@"(?m)^--- !u!(\d+) &(-?\d+)[ \t]*\r?$");
    private static readonly Regex Lists = new Regex(@"(?m)^  m_StateMachineBehaviours:[ \t]*(?<empty>\[\])?[ \t]*(?<eol>\r?\n)(?<items>(?:  - \{fileID: -?\d+\}[ \t]*(?:\r?\n|\z))*)");

    public sealed class Result
    {
        public string text;
        public string[] behaviourIds;
        public int removedReferences;
        public int orphanedBehaviours;
    }
    private sealed class Document { public int start, length, kind; public string id, text; }
    private sealed class Edit { public int start, length; public string replacement; }
    public static string Hash(byte[] bytes)
    {
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    public static Result Build(string source)
    {
        var headers = Headers.Matches(source).Cast<Match>().ToArray();
        var documents = headers.Select((header, index) => new Document {
            start = header.Index, length = (index + 1 < headers.Length ? headers[index + 1].Index : source.Length) - header.Index,
            kind = int.Parse(header.Groups[1].Value), id = header.Groups[2].Value
        }).ToArray();
        if (documents.Select(document => document.id).Distinct().Count() != documents.Length) throw new InvalidOperationException("Duplicate YAML document fileID.");
        foreach (var document in documents) document.text = source.Substring(document.start, document.length);
        var targets = documents.Where(document => document.text.Contains(ScriptGuid)).ToArray();
        if (Regex.Matches(source, Regex.Escape(ScriptGuid), RegexOptions.IgnoreCase).Count != targets.Length)
            throw new InvalidOperationException("Diagnostic GUID must occur exactly once per diagnostic document and nowhere else.");
        var allowed = new HashSet<string> { "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier" };
        foreach (var target in targets)
        {
            string[] keys = Regex.Matches(target.text, @"(?m)^  ([^ :\r\n]+):").Cast<Match>().Select(match => match.Groups[1].Value).ToArray();
            if (target.kind != 114 || !Regex.IsMatch(target.text, @"(?m)^MonoBehaviour:\r?$") ||
                Field(target.text, "m_Script") != "{fileID: 11500000, guid: " + ScriptGuid + ", type: 3}" ||
                Field(target.text, "m_EditorClassIdentifier") != TypeName || Field(target.text, "m_GameObject") != "{fileID: 0}" ||
                new[] { "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset" }.Any(field => Field(target.text, field) != "{fileID: 0}") ||
                keys.Length != allowed.Count || keys.Distinct().Count() != allowed.Count || keys.Any(key => !allowed.Contains(key)) ||
                Regex.Split(target.text, "\r?\n").Skip(2).Where(line => !string.IsNullOrWhiteSpace(line)).Any(line => !Regex.IsMatch(line, @"^  [^ :\r\n]+:[^\r\n]*$")))
                throw new InvalidOperationException("Unexpected GUID/type/schema for diagnostic fileID " + target.id + ". No cleanup planned.");
        }
        var edits = targets.Select(target => new Edit { start = target.start, length = target.length, replacement = "" }).ToList();
        var ids = new HashSet<string>(targets.Select(target => target.id));
        var used = new HashSet<string>();
        int removedReferences = 0;
        foreach (var document in documents.Where(document => !ids.Contains(document.id)))
        {
            var approvedRanges = new List<Tuple<int, int>>();
            foreach (Match list in Lists.Matches(document.text))
            {
                var entries = Regex.Matches(list.Groups["items"].Value, @"(?m)^  - \{fileID: (-?\d+)\}[ \t]*(?:\r?\n|\z)").Cast<Match>().ToArray();
                var deleted = entries.Where(entry => ids.Contains(entry.Groups[1].Value)).ToArray();
                if (deleted.Length == 0) continue;
                if (list.Groups["empty"].Success) throw new InvalidOperationException("Malformed empty behaviour list with child entries.");
                if (document.kind != 1102 && document.kind != 1107) throw new InvalidOperationException("Diagnostic reference outside AnimatorState/AnimatorStateMachine.");
                foreach (var entry in deleted)
                {
                    approvedRanges.Add(Tuple.Create(list.Groups["items"].Index + entry.Index, entry.Length));
                    used.Add(entry.Groups[1].Value); removedReferences++;
                }
                string kept = string.Concat(entries.Where(entry => !ids.Contains(entry.Groups[1].Value)).Select(entry => entry.Value));
                edits.Add(new Edit { start = document.start + list.Index, length = list.Length,
                    replacement = "  m_StateMachineBehaviours:" + (kept.Length == 0 ? " []" : "") + list.Groups["eol"].Value + kept });
            }
            foreach (Match reference in Regex.Matches(document.text, @"\bfileID:\s*(-?\d+)\b"))
                if (ids.Contains(reference.Groups[1].Value) && !approvedRanges.Any(range => reference.Index >= range.Item1 && reference.Index + reference.Length <= range.Item1 + range.Item2))
                    throw new InvalidOperationException("Diagnostic fileID " + reference.Groups[1].Value + " referenced outside a supported behaviour list.");
        }
        string output = source;
        foreach (var edit in edits.OrderByDescending(edit => edit.start)) output = output.Remove(edit.start, edit.length).Insert(edit.start, edit.replacement);
        return new Result { text = output, behaviourIds = targets.Select(target => target.id).ToArray(), removedReferences = removedReferences, orphanedBehaviours = targets.Length - used.Count };
    }
    private static string Field(string text, string name) => Regex.Match(text, @"(?m)^  " + Regex.Escape(name) + @":[ \t]*([^\r\n]*)").Groups[1].Value.Trim();

    public static byte[] EncodeLike(byte[] original, string text)
    {
        byte[] body = new UTF8Encoding(false, true).GetBytes(text);
        return HasBom(original) ? new byte[] { 239, 187, 191 }.Concat(body).ToArray() : body;
    }
    public static string Decode(byte[] bytes) => new UTF8Encoding(false, true).GetString(bytes, HasBom(bytes) ? 3 : 0, bytes.Length - (HasBom(bytes) ? 3 : 0));
    private static bool HasBom(byte[] bytes) => bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191;
}
