using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public static class CombatDiagnosticCleanupChecks
{
    private const string Motion = "435fbfc02c5b2ca419c1c60b6da164fc";
    private static string Diagnostic(string id = "100") => "--- !u!114 &" + id + "\nMonoBehaviour:\n  m_ObjectHideFlags: 1\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: " + CombatDiagnosticCleanupPlan.ScriptGuid + ", type: 3}\n  m_Name: \n  m_EditorClassIdentifier: " + CombatDiagnosticCleanupPlan.TypeName + "\n";
    private static string State(string entries = "  - {fileID: 100}\n  - {fileID: 200}\n") => "--- !u!1102 &10\nAnimatorState:\n  m_Name: Twinblades_Idle_Inplace_LucianAligned\n  m_StateMachineBehaviours:\n" + entries + "  m_Motion: {fileID: 7400000, guid: " + Motion + ", type: 2}\n  m_Transitions: []\n";
    private const string Valid = "--- !u!114 &200\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, type: 3}\n  m_EditorClassIdentifier: Assembly-CSharp::GameplayBehaviour\n  authoredValue: 17\n";
    public static IEnumerable<KeyValuePair<string, Action>> Cases()
    {
        yield return Case("Cleanup removes only exact diagnostic and keeps valid behaviour state motion and transitions", () => {
            string input = "%YAML 1.1\n" + State() + Diagnostic() + Valid;
            var result = CombatDiagnosticCleanupPlan.Build(input);
            Check(result.text == "%YAML 1.1\n" + State("  - {fileID: 200}\n") + Valid);
            Check(result.behaviourIds.SequenceEqual(new[] { "100" }) && result.removedReferences == 1 && result.orphanedBehaviours == 0);
            Check(result.text.Contains(Motion));
        });
        yield return Case("Cleanup empties diagnostic-only list without deleting its state", () => {
            var result = CombatDiagnosticCleanupPlan.Build(State("  - {fileID: 100}\n") + Diagnostic());
            Check(result.text == State().Replace("  m_StateMachineBehaviours:\n  - {fileID: 100}\n  - {fileID: 200}\n", "  m_StateMachineBehaviours: []\n"));
        });
        yield return Case("Cleanup preserves CRLF and UTF8 BOM exactly outside edits", () => {
            string source = (State() + Diagnostic() + Valid).Replace("\n", "\r\n");
            byte[] original = new byte[] {239,187,191}.Concat(Encoding.UTF8.GetBytes(source)).ToArray();
            var result = CombatDiagnosticCleanupPlan.Build(CombatDiagnosticCleanupPlan.Decode(original));
            byte[] after = CombatDiagnosticCleanupPlan.EncodeLike(original, result.text);
            Check(after.Take(3).SequenceEqual(original.Take(3)));
            Check(CombatDiagnosticCleanupPlan.Decode(after) == (State("  - {fileID: 200}\n") + Valid).Replace("\n", "\r\n"));
        });
        yield return Case("Cleanup supports orphan diagnostic subassets and is idempotent", () => {
            var result = CombatDiagnosticCleanupPlan.Build(Valid + Diagnostic());
            Check(result.text == Valid && result.orphanedBehaviours == 1 && result.removedReferences == 0);
            var again = CombatDiagnosticCleanupPlan.Build(result.text);
            Check(again.text == result.text && again.behaviourIds.Length == 0);
        });
        yield return Case("Cleanup accepts owner state-machine list without removing unrelated entries", () => {
            var result = CombatDiagnosticCleanupPlan.Build(State().Replace("!u!1102", "!u!1107").Replace("AnimatorState:", "AnimatorStateMachine:") + Diagnostic() + Valid);
            Check(result.removedReferences == 1 && result.text.Contains("  - {fileID: 200}"));
        });
        yield return Case("Cleanup rejects mismatched type or YAML class or script fileID", () => {
            Reject(Diagnostic().Replace(CombatDiagnosticCleanupPlan.TypeName, "Assembly-CSharp::GameplayBehaviour"));
            Reject(Diagnostic().Replace("!u!114", "!u!1102"));
            Reject(Diagnostic().Replace("fileID: 11500000", "fileID: 11400000"));
        });
        yield return Case("Cleanup rejects custom diagnostic fields and prefab-attached components", () => {
            Reject(Diagnostic() + "  gameplayDamage: 20\n");
            Reject(Diagnostic() + "    gameplayDamage: 20\n");
            Reject(Diagnostic() + "  m_EditorClassIdentifier: Assembly-CSharp::GameplayBehaviour\n");
            Reject(Diagnostic().Replace("m_GameObject: {fileID: 0}", "m_GameObject: {fileID: 17}"));
        });
        yield return Case("Cleanup rejects unsupported references and duplicate IDs before any edit", () => {
            Reject(Diagnostic() + "--- !u!91 &9\nAnimatorController:\n  arbitraryReference: {fileID: 100}\n");
            Reject(Diagnostic() + Diagnostic());
            Reject(State().Replace("!u!1102", "!u!91") + Diagnostic());
            Reject(Valid + "  arbitraryReferences: [{fileID: 100}, {fileID: 200}]\n" + Diagnostic());
            Reject(State().Replace("m_StateMachineBehaviours:", "m_StateMachineBehaviours: []") + Diagnostic());
        });
        yield return Case("Cleanup never substitutes a same-name diagnostic with another GUID", () => {
            string other = Diagnostic().Replace(CombatDiagnosticCleanupPlan.ScriptGuid, new string('c',32));
            Check(CombatDiagnosticCleanupPlan.Build(other).text == other);
            Reject(other + "# " + CombatDiagnosticCleanupPlan.ScriptGuid);
        });
        yield return Case("Cleanup preserves repeated valid behaviours and removes shared diagnostic references precisely", () => {
            string input = State("  - {fileID: 200}\n  - {fileID: 100}\n  - {fileID: 200}\n  - {fileID: 100}\n") + Diagnostic() + Valid;
            var result = CombatDiagnosticCleanupPlan.Build(input);
            Check(result.removedReferences == 2 && result.text == State("  - {fileID: 200}\n  - {fileID: 200}\n") + Valid);
        });
    }
    public static IEnumerable<string> InspectControllers(string root)
    {
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*.controller", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
        {
            byte[] bytes = File.ReadAllBytes(path);
            string source = CombatDiagnosticCleanupPlan.Decode(bytes);
            if (!source.Contains(CombatDiagnosticCleanupPlan.ScriptGuid)) continue;
            var result = CombatDiagnosticCleanupPlan.Build(source);
            Check(!result.text.Contains(CombatDiagnosticCleanupPlan.ScriptGuid));
            Check(Count(source, Motion) == Count(result.text, Motion));
            Check(Count(source, "--- !u!1102 &") == Count(result.text, "--- !u!1102 &"));
            Check(Count(source, "--- !u!1107 &") == Count(result.text, "--- !u!1107 &"));
            Check(CombatDiagnosticCleanupPlan.Build(result.text).text == result.text);
            Check(CombatDiagnosticCleanupPlan.Hash(bytes) == CombatDiagnosticCleanupPlan.Hash(File.ReadAllBytes(path)));
            yield return path + "; diagnostic-documents=" + result.behaviourIds.Length + "; owner-list-references=" + result.removedReferences + "; orphan-documents=" + result.orphanedBehaviours + "; before-sha256=" + CombatDiagnosticCleanupPlan.Hash(bytes);
        }
    }
    private static int Count(string source, string value) => System.Text.RegularExpressions.Regex.Matches(source, System.Text.RegularExpressions.Regex.Escape(value)).Count;
    private static void Reject(string input) { try { CombatDiagnosticCleanupPlan.Build(input); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Unsafe cleanup accepted."); }
    private static KeyValuePair<string, Action> Case(string name, Action test) => new KeyValuePair<string, Action>(name, test);
    private static void Check(bool value) { if (!value) throw new InvalidOperationException("Diagnostic cleanup assertion failed."); }
}
