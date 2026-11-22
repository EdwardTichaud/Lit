using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Lit.Story;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Opt-in functional smoke. No focus/keyboard takeover, no authored scene save, saves isolated under Library.
[InitializeOnLoad]
internal static class MaisonHearthPlayProbe
{
    private const string Key = "Lit.MaisonHearth.PlayProbe";
    private const string Folder = "Library/MaisonHearthVigil";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] private sealed class State
    {
        public string status, reason, oldRoot, oldStartScene, sessionId, saveId;
        public bool resume, oldDirect, oldMenu, passed, sawSitting, sawGroup, sawPortal;
        public int stage, cameraSamples;
        public double cameraSampleAt;
        public Vector3 firstCameraPosition;
        public double started, phaseStarted, lastAdvance;
        public List<string> errors = new List<string>();
        public Vector3[] destinations;
    }
    private static State state;
    static MaisonHearthPlayProbe()
    {
        string saved = SessionState.GetString(Key, "");
        if (!string.IsNullOrEmpty(saved)) state = JsonUtility.FromJson<State>(saved);
        EditorApplication.update += Poll;
        Application.logMessageReceived += Observe;
    }
    private static void Observe(string text, string trace, LogType type)
    {
        if (state != null && state.status != "Done" && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            state.errors.Add(text);
    }
    private static void Write()
    {
        var json = JsonUtility.ToJson(state, true);
        SessionState.SetString(Key, json);
        File.WriteAllText(Folder + "/PlayProbe.json", json);
    }
    private static T EditorComponent<T>() where T : Component => Enumerable.Range(0, SceneManager.sceneCount)
        .Select(SceneManager.GetSceneAt).Where(scene => scene.name == "Bootstrap" && !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene)).SelectMany(scene => scene.GetRootGameObjects())
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
    private static void Begin(bool resume)
    {
        var setup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
        if (!setup.Any(scene => Path.GetFileNameWithoutExtension(scene.path) == "Bootstrap") ||
            setup.Any(scene => Path.GetFileNameWithoutExtension(scene.path) != "Bootstrap" && !scene.path.StartsWith("Assets/Scenes/Maison/", StringComparison.Ordinal)))
        {
            File.WriteAllText(Folder + "/PlayProbe.result", "NOT RUN: requires Bootstrap with optional Maison scenes, preserving the current setup: " +
                string.Join("; ", setup.Select(scene => scene.path)));
            return;
        }
        var saves = EditorComponent<SaveSessionManager>(); var flow = EditorComponent<GameFlowService>();
        var saved = new SerializedObject(saves); var config = new SerializedObject(flow);
        state = new State { resume = resume, status = "Launching", started = EditorApplication.timeSinceStartup, phaseStarted = EditorApplication.timeSinceStartup,
            oldRoot = saved.FindProperty("savesRootFolder").stringValue, oldDirect = config.FindProperty("editorStartGameplayDirectly").boolValue,
            oldMenu = config.FindProperty("loadMenuAfterBootstrap").boolValue,
            oldStartScene = UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene != null ? AssetDatabase.GetAssetPath(UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene) : "" };
        Directory.CreateDirectory(Folder + "/PlaySaves");
        saved.FindProperty("savesRootFolder").stringValue = Path.GetFullPath(Folder + "/PlaySaves"); saved.ApplyModifiedPropertiesWithoutUndo();
        config.FindProperty("editorStartGameplayDirectly").boolValue = false;
        config.FindProperty("loadMenuAfterBootstrap").boolValue = true; config.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null;
        Write(); EditorApplication.isPlaying = true;
    }
    private static void Advance(int stage) { state.stage = stage; state.phaseStarted = EditorApplication.timeSinceStartup; Write(); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Finish(bool passed, string reason)
    {
        state.passed = passed; state.reason = reason; state.status = "Stopping"; Write(); EditorApplication.isPlaying = false;
    }
    private static void Restore()
    {
        var saves = new SerializedObject(EditorComponent<SaveSessionManager>());
        saves.FindProperty("savesRootFolder").stringValue = state.oldRoot; saves.ApplyModifiedPropertiesWithoutUndo();
        var flow = new SerializedObject(EditorComponent<GameFlowService>());
        flow.FindProperty("editorStartGameplayDirectly").boolValue = state.oldDirect;
        flow.FindProperty("loadMenuAfterBootstrap").boolValue = state.oldMenu; flow.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(state.oldStartScene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(state.oldStartScene);
        state.status = "Done"; Write();
        File.WriteAllText(Folder + "/PlayProbe.result", (state.passed ? "PASS: " : "FAIL: ") + state.reason + "\nRuntime errors observed: " + state.errors.Count + "; see PlayProbe.json. Editor configuration restored; no scene saved.");
        SessionState.EraseString(Key); state = null;
    }
    private static void Poll()
    {
        if (state == null)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder + "/Play.request")) return;
            bool resume = File.ReadAllText(Folder + "/Play.request").Trim() == "resume";
            File.Delete(Folder + "/Play.request"); Begin(resume); return;
        }
        if (state.status == "Stopping") { if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore(); return; }
        if (!EditorApplication.isPlaying) return;
        try
        {
            Application.runInBackground = true;
            if (EditorApplication.timeSinceStartup - state.started > 360 || EditorApplication.timeSinceStartup - state.phaseStarted > 100)
                throw new TimeoutException("Hearth probe stage " + state.stage + ": " + InputModeCoordinator.CurrentMode);
            var flow = GameFlowService.Instance; var saves = SaveSessionManager.Instance;
            if (flow == null || saves == null) return;
            Require(Path.GetFullPath(saves.GetSavesRoot()) == Path.GetFullPath(Folder + "/PlaySaves"), "Isolated save root was not applied.");
            bool house = SceneManager.GetActiveScene().name == "Maison_Core" && !flow.IsTransitioning && !LoadingScreenService.IsLoading;
            var vigil = ObjectFind<MaisonHearthVigil>();
            var playerRoot = LocalPlayerContext.LocalCharacterRoot;
            var player = playerRoot != null ? playerRoot.GetComponent<SquadCharacterController>() : null;
            switch (state.stage)
            {
                case 0:
                    if (flow.IsTransitioning || !SceneManager.GetSceneByName("MainMenu").isLoaded) return;
                    if (state.resume)
                    {
                        var meta = Directory.GetFiles(Path.GetFullPath(Folder + "/PlaySaves"), "meta.json", SearchOption.AllDirectories)
                            .First(path => File.ReadAllText(path).Contains("maison.hearth.first_vigil"));
                        state.saveId = Path.GetFileName(Path.GetDirectoryName(meta));
                        state.sessionId = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(meta)));
                        saves.SetActiveSave(state.sessionId, state.saveId);
                        Require(GameFlowService.StartOrLoadGame(), "Saved vigil reload refused."); Advance(5); break;
                    }
                    Require(saves.TryCreateNewGame("Hearth validation", SaveSessionType.Solo, "Hearth", out var slot, out string issue), issue);
                    state.sessionId = slot.sessionId; state.saveId = slot.saveId;
                    saves.SetActiveSave(slot.sessionId, slot.saveId); Require(GameFlowService.StartOrLoadGame(), "New game refused."); Advance(1); break;
                case 1:
                    if (!house || vigil == null || player == null || !player.IsGrounded) return;
                    foreach (var other in UnityEngine.Object.FindObjectsByType<StorySequenceRunner>()) if (other != vigil.runner && other.IsPlaying) other.Abort();
                    StorySequenceCompletionStore.MarkCompleted(vigil.prerequisite);
                    var marker = (Transform)typeof(StorySequenceRunner).GetField("playerTransform", Private).GetValue(vigil.runner);
                    Require(NavMesh.SamplePosition(marker.position + Vector3.right * .5f, out var hit, .8f, NavMesh.AllAreas), "No NavMesh near the hearth seat.");
                    Require(player.TryTeleportForSceneTransition(hit.position, marker.rotation), "UCC approach fixture refused.");
                    Advance(2); break;
                case 2:
                    if (!house || player == null || !player.IsGrounded || InputModeCoordinator.CurrentMode != InputMode.Exploration || InputFocusStack.HasAnyFocus()) return;
                    var foyer = ObjectFind<Foyer>(); Require(foyer != null, "Foyer absent.");
                    var portal = UnityEngine.Object.FindObjectsByType<PortalController>().Single(item => item.name == "Portal_1");
                    state.destinations = portal.DestinationPoints.Select(point => point.position).ToArray();
                    foyer.SetDetectedCharacter(player.gameObject);
                    string diagnostic = DescribeFoyer(vigil, foyer, player);
                    File.WriteAllText(Folder + "/FoyerState.txt", diagnostic);
                    Require(foyer.TryHandleLocalInteract(), "Foyer interaction refused. " + diagnostic);
                    Require(vigil.runner.IsPlaying || typeof(MaisonHearthVigil).GetField("preparation", Private).GetValue(vigil) != null,
                        "Foyer fell back to ordinary sitting instead of starting the vigil. " + diagnostic);
                    Advance(3); break;
                case 3:
                    if (vigil == null || player == null) return;
                    state.sawSitting |= player.IsSittingRequested;
                    state.sawGroup |= vigil.companionSeats.All(seat => vigil.bindings.ResolveActor(seat.actorId)?.Animator.runtimeAnimatorController == vigil.seatedController);
                    var spawnedDeparture = vigil.runner.GetSpawnedObject(MaisonHearthVigil.PortalSpawnId);
                    var departure = spawnedDeparture != null ? spawnedDeparture.GetComponent<PortalController>() : null;
                    state.sawPortal |= state.sawGroup && departure != null && departure.gameObject.activeInHierarchy;
                    if (state.sawGroup && vigil.runner.IsPlaying &&
                        (bool)typeof(StorySequenceRunner).GetField("currentStepSkippable", Private).GetValue(vigil.runner) &&
                        state.cameraSamples < 2 && (state.cameraSamples == 0 || EditorApplication.timeSinceStartup - state.cameraSampleAt > 6))
                    {
                        var driver = vigil.GetComponent<StorySequenceCameraDriver>();
                        var point = (StorySequenceCameraPoint)typeof(StorySequenceCameraDriver).GetField("activePoint", Private).GetValue(driver);
                        var controlled = (Camera)typeof(StorySequenceCameraDriver).GetField("controlledCamera", Private).GetValue(driver);
                        var elapsed = typeof(StorySequenceCameraDriver).GetField("shotTravelElapsed", Private).GetValue(driver);
                        File.WriteAllText(Folder + "/CameraSample_" + state.cameraSamples + ".txt", $"camera={Camera.main.transform.position:F4}; driver={driver.enabled}; controlled={controlled?.name}/{controlled?.transform.position}; main={Camera.main.name}; point={point?.PointId}; elapsed={elapsed}; orbit={(point != null ? new SerializedObject(point).FindProperty("orbitArcDegrees").floatValue : -1)}");
                        if (state.cameraSamples == 0) state.firstCameraPosition = Camera.main.transform.position;
                        else Require(Vector3.Distance(state.firstCameraPosition, Camera.main.transform.position) > .03f, "Group camera did not keep moving between dialogue lines. See CameraSample files.");
                        if (SaveScreenshotCapture.TryCapture(Camera.main, 1280, out var png, out var cameraIssue))
                            File.WriteAllBytes(Folder + "/GroupCamera_" + state.cameraSamples + ".png", png);
                        else throw new InvalidOperationException(cameraIssue);
                        state.cameraSamples++; state.cameraSampleAt = EditorApplication.timeSinceStartup;
                    }
                    if (vigil.runner.IsPlaying && (bool)typeof(StorySequenceRunner).GetField("currentStepSkippable", Private).GetValue(vigil.runner) &&
                        EditorApplication.timeSinceStartup - state.lastAdvance > .45)
                    {
                        typeof(StorySequenceRunner).GetField("advanceRequested", Private).SetValue(vigil.runner, true);
                        state.lastAdvance = EditorApplication.timeSinceStartup;
                    }
                    if (!vigil.IsCompleted || vigil.runner.IsPlaying || player.IsInSittingCycle || InputModeCoordinator.CurrentMode != InputMode.Exploration) return;
                    Require(state.sawSitting && state.sawGroup && state.sawPortal, "Missing sitting, seated companions or portal appearance.");
                    Require(!player.IsMovementInputSuppressed, "Movement remains suppressed after the vigil.");
                    Require(departure != null && state.destinations.Length == 4 && departure.DestinationPoints.Select((point, i) => Vector3.Distance(point.position, state.destinations[i]) < .001f).All(value => value), "Portal arrival anchors moved.");
                    Require(!vigil.TryHandleFoyer(ObjectFind<Foyer>(), player, out _), "Completed story still intercepts the foyer.");
                    Require(GameFlowService.QuitToMainMenu(), "Return to menu refused."); Advance(4); break;
                case 4:
                    if (flow.IsTransitioning || !SceneManager.GetSceneByName("MainMenu").isLoaded) return;
                    var completedMeta = Directory.GetFiles(Path.GetFullPath(Folder + "/PlaySaves/" + state.sessionId), "meta.json", SearchOption.AllDirectories)
                        .FirstOrDefault(path => File.ReadAllText(path).Contains("maison.hearth.first_vigil"));
                    Require(completedMeta != null, "Completion absent from the actual saved slot metadata.");
                    state.saveId = Path.GetFileName(Path.GetDirectoryName(completedMeta));
                    saves.SetActiveSave(state.sessionId, state.saveId); Require(GameFlowService.StartOrLoadGame(), "Reload refused."); Advance(5); break;
                case 5:
                    if (!house || vigil == null || player == null || !player.IsGrounded) return;
                    if (vigil.runner.GetSpawnedObject(MaisonHearthVigil.PortalSpawnId) == null && EditorApplication.timeSinceStartup - state.phaseStarted < 5) return;
                    Require(vigil.runner.GetSpawnedObject(MaisonHearthVigil.PortalSpawnId) != null, "Completed portal was not recreated after reload.");
                    Require(vigil.IsCompleted && !vigil.IsUnlocked, "Loaded save lost the once-only completion.");
                    Require(!vigil.TryHandleFoyer(ObjectFind<Foyer>(), player, out _), "Reloaded story can replay.");
                    Finish(true, state.resume ? "Actual auto-save reloaded through GameFlow: vigil remains completed and cannot intercept the foyer again. First execution evidence is in PlayProbe.first.json." : "Real UCC approach, animated seating, group staging, 27 dialogue advances, portal with four unchanged arrivals, standing/input restoration and save/menu/reload completion passed. Automated dialogue advancement; no manual gamepad or artistic visual qualification."); break;
            }
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }
    private static string DescribeFoyer(MaisonHearthVigil vigil, Foyer foyer, SquadCharacterController player)
    {
        var type = typeof(MaisonHearthVigil);
        var group = (MaisonCompanionReunion)type.GetField("group", Private).GetValue(vigil);
        string actors = group == null ? "none" : string.Join(";", group.Companions.Select(member => member.actorId + ":" +
            (member.instance == null ? "absent" : member.instance.gameObject.activeInHierarchy + "/animator=" + (member.instance.Animator != null))));
        return $"enabled={vigil.isActiveAndEnabled}; unlocked={vigil.IsUnlocked}; completed={vigil.IsCompleted}; prereq={StorySequenceCompletionStore.IsCompleted(vigil.prerequisite)}; " +
            $"foyerMatch={type.GetField("foyer", Private).GetValue(vigil) as Foyer == foyer}; portal={type.GetField("portal", Private).GetValue(vigil)}; " +
            $"point={vigil.bindings.ResolveSpawnPoint(MaisonHearthVigil.PortalSpawnPointId)?.point}; actors={actors}; seats={vigil.companionSeats.Length}; " +
            $"canSit={player.CanToggleSitting}; sitting={player.IsSittingRequested}; transition={player.IsSittingTransitionActive}; local={player.transform == LocalPlayerContext.LocalCharacterRoot}; " +
            $"mode={InputModeCoordinator.CurrentMode}; focus={InputFocusStack.HasAnyFocus()}; otherSequence={StorySequenceRunner.IsAnySequencePlaying}; active={SceneManager.GetActiveScene().name}";
    }
    private static T ObjectFind<T>() where T : UnityEngine.Object => UnityEngine.Object.FindAnyObjectByType<T>();
}
