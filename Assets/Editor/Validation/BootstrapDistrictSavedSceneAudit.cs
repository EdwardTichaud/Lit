using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Saved-content inspection only. Preview scenes are closed, never saved.
/// Does not enter Play Mode, change the user's scene setup or access save slots.</summary>
[InitializeOnLoad]
public static class BootstrapDistrictSavedSceneAudit
{
    private const string Request = "Library/StartupDistrictValidation/SavedSceneAudit.request";
    private const string ResultPath = "Library/StartupDistrictValidation/SavedSceneAudit.json";
    private const string StatusPath = "Library/StartupDistrictValidation/SavedSceneAudit.result";
    private const string ManifestPath = "Assets/Scenes/Maison/ZoneManifest_District_1.asset";
    private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
    private const string MenuPath = "Assets/Scenes/MainMenu/MainMenu.unity";
    private static bool running;

    [Serializable] private sealed class Report
    {
        public string utc;
        public bool authoringChecksPassed;
        public bool fullPlaythroughValidated = false;
        public string scope = "Saved Bootstrap, MainMenu and all District_1 manifest scenes; preview inspection only. Does not validate rendering, navigation reachability, manual inputs, save restoration or real multiplayer scene synchronization.";
        public List<string> issues = new();
        public List<string> notes = new();
        public List<SceneReport> scenes = new();
    }
    [Serializable] private sealed class SceneReport
    {
        public string path, sha256;
        public int objects, networkObjects, cycles, flames, interactables;
        public List<string> missingScripts = new();
    }

    static BootstrapDistrictSavedSceneAudit() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (running || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        File.Delete(Request);
        Run();
    }

    [MenuItem("Lit/Validation/Audit Saved Bootstrap MainMenu District_1")]
    public static void Run()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        Directory.CreateDirectory("Library/StartupDistrictValidation");
        File.WriteAllText(StatusPath, "RUNNING " + DateTime.UtcNow.ToString("O"));
        var report = new Report { utc = DateTime.UtcNow.ToString("O") };
        var original = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var dirty = original.ToDictionary(scene => scene.handle, scene => scene.isDirty);
        var active = SceneManager.GetActiveScene();
        var paths = new List<string> { BootstrapPath, MenuPath };
        var fileHashes = new Dictionary<string, string>();
        try
        {
            var enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            if (enabled.Length == 0 || enabled[0].path != BootstrapPath) report.issues.Add("Bootstrap must be the first enabled build scene.");
            var byName = enabled.GroupBy(scene => Path.GetFileNameWithoutExtension(scene.path)).ToDictionary(group => group.Key, group => group.ToArray());
            foreach (var duplicate in byName.Where(pair => pair.Value.Length > 1)) report.issues.Add("Ambiguous enabled scene name: " + duplicate.Key);
            var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(ManifestPath);
            if (manifest == null || !manifest.IsValid) report.issues.Add("District_1 manifest missing or invalid.");
            else
            {
                var names = new[] { manifest.PrimarySceneName }.Concat(manifest.LoadingSceneNames).Concat(manifest.PostLoadingSceneNames).ToArray();
                if (names.Distinct().Count() != names.Length) report.issues.Add("District_1 manifest contains duplicate scene names.");
                foreach (var name in names)
                    if (byName.TryGetValue(name, out var entries) && entries.Length == 1) paths.Add(entries[0].path);
                    else report.issues.Add("Required manifest scene missing or ambiguous in enabled build settings: " + name);
                report.notes.Add("Baked navigation: " + (manifest.BakedNavMeshData != null ? "assigned, not a path reachability test" : "no baked data; runtime generation required"));
            }
            foreach (var path in paths.Distinct())
                if (File.Exists(path)) fileHashes[path] = Hash(path);
                else report.issues.Add("Saved scene does not exist: " + path);
            foreach (var path in paths.Distinct().Where(File.Exists)) Inspect(path, report);
            report.issues.AddRange(CycleProjectValidator.CollectIssues().Select(issue => "Cycle definition: " + issue));
        }
        catch (Exception error) { report.issues.Add(error.ToString()); }
        finally
        {
            // No restoration/save command: the original setup must not have moved.
            if (SceneManager.GetActiveScene() != active) report.issues.Add("The original active scene changed during preview inspection.");
            foreach (var scene in original)
                if (!scene.IsValid() || !scene.isLoaded || scene.isDirty != dirty[scene.handle]) report.issues.Add("Original scene state changed: " + scene.path);
            var after = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Select(scene => scene.handle).ToArray();
            if (!original.Select(scene => scene.handle).SequenceEqual(after)) report.issues.Add("Original loaded scene order changed during inspection.");
            foreach (var pair in fileHashes)
                if (!File.Exists(pair.Key) || Hash(pair.Key) != pair.Value) report.issues.Add("A saved scene file changed during inspection: " + pair.Key);
            report.authoringChecksPassed = report.issues.Count == 0;
            File.WriteAllText(ResultPath, JsonUtility.ToJson(report, true));
            File.WriteAllText(StatusPath, "FINISHED scenes=" + report.scenes.Count + " issues=" + report.issues.Count + " authoringOnly=true");
            running = false;
        }
    }

    private static void Inspect(string path, Report report)
    {
        var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
            var entry = new SceneReport { path = path, sha256 = Hash(path), objects = objects.Length };
            report.scenes.Add(entry);
            var cycles = objects.SelectMany(go => go.GetComponents<CycleController>()).ToArray();
            entry.cycles = cycles.Length;
            entry.networkObjects = objects.Count(go => go.GetComponent<Unity.Netcode.NetworkObject>() != null);
            entry.flames = objects.Count(go => go.GetComponent<Flame>() != null);
            entry.interactables = objects.Sum(go => go.GetComponents<MonoBehaviour>().Count(component => component is ICharacterDetectedInteractable));
            foreach (var go in objects)
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
                { entry.missingScripts.Add(go.name); report.issues.Add(path + " : missing script on " + go.name); }
            if (path == BootstrapPath)
            {
                var flows = objects.SelectMany(go => go.GetComponents<GameFlowService>()).ToArray();
                if (flows.Length != 1) report.issues.Add("Bootstrap requires exactly one GameFlowService.");
                if (objects.Sum(go => go.GetComponents<ApplicationRoot>().Length) != 1) report.issues.Add("Bootstrap requires exactly one ApplicationRoot.");
                if (objects.Sum(go => go.GetComponents<SaveSessionManager>().Length) != 1) report.issues.Add("Bootstrap requires exactly one SaveSessionManager.");
                if (flows.Length == 1)
                {
                    var data = new SerializedObject(flows[0]);
                    if (data.FindProperty("menuSceneName").stringValue != "MainMenu") report.issues.Add("Bootstrap menu route does not point to MainMenu.");
                    if (!data.FindProperty("loadMenuAfterBootstrap").boolValue) report.issues.Add("Normal Bootstrap startup does not request the menu.");
                    foreach (string key in new[] { "hubManifest", "gameplaySessionPrefab" })
                        if (data.FindProperty(key).objectReferenceValue == null) report.issues.Add("Bootstrap missing " + key);
                    report.notes.Add("Editor direct startup enabled: " + data.FindProperty("editorStartGameplayDirectly").boolValue + "; not a validation of MainMenu startup.");
                    var district = AssetDatabase.LoadAssetAtPath<ZoneManifest>(ManifestPath);
                    var zones = data.FindProperty("zoneManifests");
                    if (!Enumerable.Range(0, zones.arraySize).Any(index => zones.GetArrayElementAtIndex(index).objectReferenceValue == district))
                        report.issues.Add("District_1 manifest is not registered on Bootstrap GameFlowService.");
                }
            }
            if (path == MenuPath)
            {
                try { new MainMenuBuildValidator().OnProcessScene(scene, null); }
                catch (Exception error) { report.issues.Add("MainMenu authoring: " + error.Message); }
            }
            foreach (var cycle in cycles)
            {
                if (cycle.definition == null) { report.issues.Add(path + " : CycleController has no definition."); continue; }
                if (cycle.definition.cycleSceneName != scene.name) report.issues.Add(path + " : definition does not match the cycle scene name.");
                if (cycle.director != null)
                    report.issues.AddRange(Lit.Editor.TimelineBindingsValidator.CollectBindingCoverageIssues(cycle.director, cycle.bindingProfile).Select(issue => path + " : " + issue));
                foreach (var sequence in cycle.sequences ?? Array.Empty<CycleSequenceBinding>())
                    if (sequence != null)
                        report.issues.AddRange(Lit.Editor.TimelineBindingsValidator.CollectBindingCoverageIssues(sequence.director, sequence.profile).Select(issue => path + " [" + sequence.id + "] : " + issue));
            }
        }
        catch (Exception error) { report.issues.Add(path + " : " + error); }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
}
