using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Read-only inventory. Reports are deliberately outside Assets.</summary>
[InitializeOnLoad]
public static class PlayerInPlaceAudit
{
    public const string ControllerPath = "Assets/Characters/4_Animations/Player_Model.controller";
    public const string LucianPath = "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab";
    public const string ReportPath = "Library/PlayerInPlaceAudit.json";
    [Serializable] public sealed class Entry
    {
        public string guid, path, name, replacement;
        public long localId;
        public float duration;
        public bool rootCandidate, humanMotion;
        public string[] bindings;
        public string[] consumers;
        public string[] events;
    }
    [Serializable] public sealed class Report { public Entry[] clips; public string[] failures; }

    static PlayerInPlaceAudit() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string request = "Library/PlayerInPlace.request";
        if (!File.Exists(request)) return;
        string command = File.ReadAllText(request).Trim();
        File.Delete(request);
        try
        {
            if (command == "audit") Audit();
            else if (command == "Measure") Measure();
            else
            {
                // Keep the command bridge independent of a migration assembly reload.
                var type = typeof(PlayerInPlaceAudit).Assembly.GetType("PlayerInPlaceMigration");
                var method = type?.GetMethod(command, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method == null) throw new InvalidOperationException("Unknown InPlace command: " + command);
                method.Invoke(null, null);
            }
            File.WriteAllText("Library/PlayerInPlace.result", command + " OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText("Library/PlayerInPlace.result", e.ToString());
        }
    }

    [MenuItem("Lit/Animation/Audit Player InPlace")]
    public static void Audit()
    {
        var failures = new List<string>();
        var consumers = Collect(failures);
        var paths = AssetDatabase.GetAllAssetPaths();
        var report = new Report {
            failures = failures.ToArray(),
            clips = consumers.Select(pair => {
                AnimationClip clip = pair.Key;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long id);
                string path = AssetDatabase.GetAssetPath(clip);
                return new Entry {
                    guid = guid, localId = id, path = path, name = clip.name, duration = clip.length,
                    rootCandidate = IsRootCandidate(clip), humanMotion = clip.humanMotion,
                    replacement = FindEquivalent(path, paths), consumers = pair.Value.ToArray(),
                    bindings = AnimationUtility.GetCurveBindings(clip).Where(b => b.path == "").Select(b => b.type.Name + ":" + b.propertyName).ToArray(),
                    events = AnimationUtility.GetAnimationEvents(clip).Select(e => e.time + ":" + e.functionName).ToArray()
                };
            }).OrderBy(e => e.path).ToArray()
        };
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        Debug.Log($"[Player InPlace] Audit: {report.clips.Length} clips, {report.clips.Count(e => e.rootCandidate)} Root candidates, {failures.Count} unresolved. {ReportPath}");
        PlayerInPlaceMigration.Preflight();
    }

    [MenuItem("Lit/Animation/Measure Player Root Candidates")]
    public static void Measure()
    {
        var lines = new List<string>();
        using (var sampler = new PlayerInPlaceSampling())
        foreach (var clip in Collect(new List<string>()).Keys)
        {
            var s = sampler.Sample(clip);
            lines.Add(clip.name + " | distance=" + s.Distance + " max=" + s.MaxDisplacement + " yaw=" + s.MaxYaw + " scale=" + s.humanScale + " | " + AssetDatabase.GetAssetPath(clip));
        }
        File.WriteAllLines("Library/PlayerInPlaceMeasurements.txt", lines);
    }

    public static bool IsRootCandidate(AnimationClip clip)
    {
        string path = AssetDatabase.GetAssetPath(clip);
        if (path.StartsWith("Assets/Characters/1_Squad/Lucian/Animation/PlayerInPlace/", StringComparison.Ordinal)) return false;
        return path.IndexOf("root", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static string FindEquivalent(string path, string[] paths)
    {
        string wanted = System.Text.RegularExpressions.Regex.Replace(path, "root_motion|rootmotion|root", "inplace", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var exact = paths.Where(p => string.Equals(p, wanted, StringComparison.OrdinalIgnoreCase)).ToArray();
        return exact.Length == 1 ? exact[0] : null;
    }

    public static Dictionary<AnimationClip, List<string>> Collect(List<string> failures)
    {
        var result = new Dictionary<AnimationClip, List<string>>();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) throw new InvalidOperationException("Player_Model is missing");
        foreach (var layer in controller.layers)
            Visit(layer.stateMachine, layer.name, (state, path) => CollectMotion(state.motion, path, result, failures));
        foreach (var guid in AssetDatabase.FindAssets("t:SkillSO", new[] { "Assets/CombatRealTime/Skills" }))
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill != null && skill.animationClip != null) Add(result, skill.animationClip, AssetDatabase.GetAssetPath(skill));
        }
        foreach (var sequence in PlayerSequences())
            foreach (var step in sequence.steps.Where(s => s != null))
            {
                if (step.qtePresentationClip != null) Add(result, step.qtePresentationClip, AssetDatabase.GetAssetPath(sequence) + ":QTE");
                if (step.successPlayerAnimationClip != null) Add(result, step.successPlayerAnimationClip, AssetDatabase.GetAssetPath(sequence) + ":Success");
            }
        foreach (var overrides in PlayerOverrides())
        {
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrides.GetOverrides(pairs);
            foreach (var pair in pairs)
                if (pair.Value != null) Add(result, pair.Value, AssetDatabase.GetAssetPath(overrides));
        }
        return result;
    }
    public static IEnumerable<ThresholdSequence> PlayerSequences() => AssetDatabase.FindAssets("t:ThresholdSequence")
        .Select(g => AssetDatabase.LoadAssetAtPath<ThresholdSequence>(AssetDatabase.GUIDToAssetPath(g))).Where(s => s != null);
    public static IEnumerable<AnimatorOverrideController> PlayerOverrides()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AnimatorOverrideController"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(AssetDatabase.GUIDToAssetPath(guid));
            RuntimeAnimatorController basis = asset;
            var seen = new HashSet<RuntimeAnimatorController>();
            while (basis is AnimatorOverrideController wrapper && seen.Add(basis)) basis = wrapper.runtimeAnimatorController;
            if (AssetDatabase.GetAssetPath(basis) == ControllerPath) yield return asset;
        }
    }

    public static void Visit(AnimatorStateMachine machine, string path, Action<AnimatorState, string> visitor)
    {
        foreach (var child in machine.states) visitor(child.state, path + "." + child.state.name);
        foreach (var child in machine.stateMachines) Visit(child.stateMachine, path + "." + child.stateMachine.name, visitor);
    }
    private static void CollectMotion(Motion motion, string path, Dictionary<AnimationClip, List<string>> result, List<string> failures)
    {
        if (motion is AnimationClip clip) Add(result, clip, path);
        else if (motion is BlendTree tree)
            foreach (var child in tree.children) CollectMotion(child.motion, path + "/" + tree.name, result, failures);
        else if (motion == null) failures.Add("Empty motion: " + path);
    }
    private static void Add(Dictionary<AnimationClip, List<string>> result, AnimationClip clip, string consumer)
    {
        if (!result.TryGetValue(clip, out var entries)) result.Add(clip, entries = new List<string>());
        if (!entries.Contains(consumer)) entries.Add(consumer);
    }
}
