#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Keeps TimeManager as the sole writer of Unity's global time settings.
/// Reads of Time.* deliberately remain allowed: they select a scaled or
/// realtime domain but do not take ownership of global time.
/// </summary>
public sealed class TimeAuthorityAudit : IPreprocessBuildWithReport
{
    private const string TimeManagerPath = "Assets/Scripts/Time/TimeManager.cs";
    private static readonly Regex IllegalWrite = new Regex(
        @"\bTime\s*\.\s*(?:timeScale|fixedDeltaTime)\s*(?:=|\+=|-=|\*=|/=)",
        RegexOptions.Compiled);

    public int callbackOrder => -1000;

    [MenuItem("Lit/Time/Audit Global Time Authority")]
    private static void AuditFromMenu()
    {
        List<string> violations = FindViolations();
        if (violations.Count == 0)
        {
            Debug.Log("[Time Authority Audit] OK : TimeManager est l'unique ecrivain de Time.timeScale et Time.fixedDeltaTime.");
            return;
        }

        Debug.LogError("[Time Authority Audit] Ecritures interdites :\n" + string.Join("\n", violations));
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        List<string> violations = FindViolations();
        if (violations.Count == 0) return;
        throw new BuildFailedException(
            "[Time Authority Audit] Seul TimeManager peut ecrire Time.timeScale ou Time.fixedDeltaTime.\n" +
            string.Join("\n", violations));
    }

    private static List<string> FindViolations()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string assetsRoot = Path.Combine(projectRoot, "Assets");
        var violations = new List<string>();
        foreach (string path in Directory.EnumerateFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string normalizedPath = path.Replace('\\', '/');
            string relativePath = normalizedPath.Substring(projectRoot.Replace('\\', '/').Length + 1);
            if (string.Equals(relativePath, TimeManagerPath, StringComparison.OrdinalIgnoreCase)) continue;

            string[] lines = File.ReadAllLines(path);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                if (IllegalWrite.IsMatch(lines[lineIndex]))
                {
                    violations.Add(relativePath + ":" + (lineIndex + 1));
                }
            }
        }
        return violations;
    }
}
#endif
