using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Runs the same recipe without bypassing a production player-build validator.
[InitializeOnLoad]
public static class PersistenceEditorPlayProbe
{
    private const string Key = "Lit.PersistenceEditorRecipe";
    private static double nextHeartbeat;
    static PersistenceEditorPlayProbe()
    {
        string run = SessionState.GetString(Key + ".run", "");
        if (SessionState.GetBool(Key + ".exitPending", false))
            EditorApplication.update += ExitWhenStopped;
        else if (!SessionState.GetBool(Key, false) && !string.IsNullOrEmpty(run) &&
            Environment.GetCommandLineArgs().Contains("-persistence-validation") && File.Exists(Path.Combine(run, "editor-complete.txt")))
        {
            // Recover a completed old recipe whose delayed exit was lost in
            // the Play Mode domain reload. Never relaunch its gameplay.
            bool success = File.Exists(Path.Combine(run, "result.json")) &&
                JsonUtility.FromJson<EndSummary>(File.ReadAllText(Path.Combine(run, "result.json"))).passed;
            QueueExit(success);
        }
        else if (SessionState.GetBool(Key, false)) Hook();
    }
    public static void Run()
    {
        try
        {
            string project = Path.GetFullPath(".");
            string networkRole = Environment.GetCommandLineArgs().FirstOrDefault(value => value.StartsWith("-persistence-network-role="))?.Split('=')[1];
            bool network = networkRole == "host" || networkRole == "client";
            if (!Environment.GetCommandLineArgs().Contains("-persistence-validation") ||
                Directory.Exists(Path.Combine(project, ".git")) || File.Exists(Path.Combine(project, ".git")) ||
                !(project.EndsWith("MovingLegsValidationProject", StringComparison.OrdinalIgnoreCase) ||
                  network && project.EndsWith("PersistenceNetworkClientProject", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Explicitly requested isolated validation copy required.");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            var flow = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameFlowService>(true)).Single();
            var flowData = new SerializedObject(flow);
            flowData.FindProperty("editorStartGameplayDirectly").boolValue = false;
            flowData.FindProperty("loadMenuAfterBootstrap").boolValue = true;
            flowData.ApplyModifiedPropertiesWithoutUndo();
            var house = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_Maison.asset");
            var district = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset");
            if (house == null || district == null) throw new InvalidOperationException("Real manifests required.");
            string networkRun = Environment.GetCommandLineArgs().FirstOrDefault(value => value.StartsWith("-persistence-network-run="))?.Substring("-persistence-network-run=".Length);
            string shared = null;
            if (network)
            {
                string allowed = Path.GetFullPath("../PersistenceContinuityValidation/NetworkRuns/").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                shared = Path.GetFullPath(networkRun ?? throw new InvalidOperationException("Shared network recipe directory required."));
                if (!shared.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Network recipe outside owned reports root.");
            }
            string run = network ? Path.Combine(shared, networkRole) : Path.GetFullPath("../PersistenceContinuityValidation/EditorRuns/" + Guid.NewGuid().ToString("N"));
            if (network)
            {
                var probe = new GameObject("PersistenceNetworkProbe").AddComponent<PersistenceNetworkProbe>();
                probe.house = house; probe.district = district; probe.runDirectory = run; probe.sharedDirectory = shared; probe.role = networkRole;
            }
            else
            {
                var probe = new GameObject("PersistencePlayerProbe").AddComponent<PersistencePlayerProbe>();
                probe.house = house; probe.district = district; probe.runDirectory = run;
            }
            Directory.CreateDirectory(run);
            string recipeSources = Path.Combine(run, "RecipeSources");
            Directory.CreateDirectory(recipeSources);
            foreach (string path in new[] { "Assets/__PersistenceValidation/PersistencePlayerProbe.cs",
                "Assets/__PersistenceValidation/PersistenceNetworkProbe.cs", "Assets/__PersistenceValidation/Editor/PersistenceEditorPlayProbe.cs" })
                File.Copy(path, Path.Combine(recipeSources, Path.GetFileName(path)));
            foreach (string report in new[] { "production-dependency-hashes.log", "production-unresolved-guids.log" })
            {
                string reportPath = Path.GetFullPath("../PersistenceContinuityValidation/" + report);
                if (File.Exists(reportPath)) File.Copy(reportPath, Path.Combine(run, report));
            }
            var saves = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SaveSessionManager>(true)).Single();
            var data = new SerializedObject(saves);
            data.FindProperty("savesRootFolder").stringValue = Path.Combine(run, "Saves");
            data.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory("Assets/__PersistenceValidation/Scene");
            if (!EditorSceneManager.SaveScene(scene, "Assets/__PersistenceValidation/Scene/Bootstrap.unity"))
                throw new InvalidOperationException("Could not save isolated Bootstrap clone.");
            const string pendingCellPath = "Assets/__PersistenceValidation/Scene/PendingPostCell.unity";
            var cell = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var sentinel = new GameObject("Pending request ownership sentinel");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sentinel, cell);
            if (!EditorSceneManager.SaveScene(cell, pendingCellPath))
                throw new InvalidOperationException("Could not save isolated cancellation fixture.");
            EditorSceneManager.CloseScene(cell, true);
            // NGO's normal editor check stays enabled for the cloned scene.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene.path, true), new EditorBuildSettingsScene(pendingCellPath, true) }.Concat(
                EditorBuildSettings.scenes.Where(entry => entry.path != "Assets/Scenes/Bootstrap.unity" && entry.path != scene.path && entry.path != pendingCellPath)).ToArray();
            SessionState.SetString(Key + ".run", run);
            SessionState.SetString(Key + ".cancelFile", network ? Path.Combine(shared, "cancel-requested.txt") : "");
            SessionState.SetInt(Key + ".oldOptions", (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetBool(Key + ".oldEnabled", EditorSettings.enterPlayModeOptionsEnabled);
            EditorSettings.enterPlayModeOptionsEnabled = false;
            File.WriteAllText(Path.GetFullPath("../PersistenceContinuityValidation/" + (network ? "latest-network-" + networkRole + "-run.txt" : "latest-editor-run.txt")), run);
            SessionState.SetFloat(Key + ".started", (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".launchPending", true);
            // The client can finish its real Bootstrap/menu before the host
            // starts its bounded gameplay gate. No gameplay signal is fabricated.
            SessionState.SetString(Key + ".waitForHost", "");
            SessionState.SetString(Key + ".waitForClient", networkRole == "host" ? Path.Combine(shared, "client") : "");
            Hook();
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static void Hook()
    {
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        string cancellation = SessionState.GetString(Key + ".cancelFile", "");
        if (!string.IsNullOrEmpty(cancellation) && File.Exists(cancellation))
        {
            Finish(false, "Validation cancelled by its coordinator; inspect runtime stage report.");
            return;
        }
        if (EditorApplication.timeSinceStartup > nextHeartbeat)
        {
            nextHeartbeat = EditorApplication.timeSinceStartup + 15;
            Debug.Log("[PersistenceEditor] heartbeat playing=" + EditorApplication.isPlaying + " changing=" +
                EditorApplication.isPlayingOrWillChangePlaymode + " compiling=" + EditorApplication.isCompiling +
                " updating=" + EditorApplication.isUpdating + " frame=" + Time.frameCount);
        }
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".started", 0) > 1800)
        {
            Finish(false, "Editor recipe exceeded 30 minutes; inspect runtime stage report.");
            return;
        }
        if (SessionState.GetBool(Key + ".launchPending", false) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            string waitForHost = SessionState.GetString(Key + ".waitForHost", "");
            if (!string.IsNullOrEmpty(waitForHost) && !File.Exists(Path.Combine(waitForHost, "host-ready"))) return;
            string waitForClient = SessionState.GetString(Key + ".waitForClient", "");
            if (!string.IsNullOrEmpty(waitForClient))
            {
                string clientRecipe = Path.GetFullPath("../PersistenceContinuityValidation/latest-network-client-run.txt");
                if (!File.Exists(clientRecipe) || !string.Equals(File.ReadAllText(clientRecipe).Trim(), waitForClient, StringComparison.OrdinalIgnoreCase)) return;
                if (!File.Exists(Path.Combine(Path.GetDirectoryName(waitForClient), "client-menu-ready"))) return;
            }
            SessionState.SetBool(Key + ".launchPending", false);
            var gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameView == null) throw new InvalidOperationException("Native GameView unavailable.");
            EditorWindow.GetWindow(gameView, false, "Persistence validation", false).Show();
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;
        }
    }
    private static void Log(string message, string stack, LogType type)
    {
        if (message.StartsWith("[PersistencePlayer] COMPLETE ", StringComparison.Ordinal)) Finish(true, message);
        else if (message.StartsWith("[PersistencePlayer] FAILED ", StringComparison.Ordinal)) Finish(false, message);
    }
    private static void Finish(bool success, string message)
    {
        if (!SessionState.GetBool(Key, false)) return;
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= Log;
        EditorApplication.update -= Tick;
        File.WriteAllText(Path.Combine(SessionState.GetString(Key + ".run", ""), "editor-complete.txt"), message);
        QueueExit(success);
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
    }

    private static void QueueExit(bool success)
    {
        SessionState.SetBool(Key + ".exitPending", true);
        SessionState.SetInt(Key + ".exitCode", success ? 0 : 1);
        EditorApplication.update -= ExitWhenStopped;
        EditorApplication.update += ExitWhenStopped;
    }

    private static void ExitWhenStopped()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= ExitWhenStopped;
        EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Key + ".oldOptions", 0);
        EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + ".oldEnabled", false);
        int code = SessionState.GetInt(Key + ".exitCode", 1);
        SessionState.SetBool(Key + ".exitPending", false);
        EditorApplication.Exit(code);
    }

    [Serializable] private sealed class EndSummary { public bool passed; }
}
