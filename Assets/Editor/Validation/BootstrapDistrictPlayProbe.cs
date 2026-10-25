using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in editor acceptance probe. Only its Bootstrap copy is saved;
/// the production menu, portal, zone loader and avatars run without substitutes.</summary>
[InitializeOnLoad]
public static partial class BootstrapDistrictPlayProbe
{
    private const string Root = "Library/StartupDistrictValidation";
    private const string Request = Root + "/PlayProbe.request";
    private const string Key = "Lit.StartupDistrictPlayProbe";
    private const string Bootstrap = "Assets/Scenes/Bootstrap.unity";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Report report;
    private static double nextReport;
    private static int settledFrames;

    [Serializable] private sealed class SceneEntry { public string path; public bool loaded, active; }
    [Serializable] private sealed class FileEntry { public string path, hash; }
    [Serializable] private sealed class Report
    {
        public string run, clone, utc, status, reason, oldPlayScene, fixtureId, actorEntity, diagnostics;
        public int stage, oldOptions, limbesRouteIndex;
        public double started, stageStarted;
        public bool oldOptionsEnabled, passed, restored, recovery, limbes, limbesCycles, limbesRoutes;
        public bool fullProductionReadiness = false;
        public string scope = "Real editor Bootstrap/menu solo creation, authored portal, all District_1 manifest scenes and return to menu. Public menu handlers and a UCC portal approach fixture; not manual input, cycle completion, multiplayer or visual quality proof. Saves isolated under this run directory.";
        public List<SceneEntry> scenes = new();
        public List<FileEntry> files = new();
        public List<string> checks = new(), errors = new();
        public List<string> expectedCaptureErrors = new(), warnings = new();
    }

    static BootstrapDistrictPlayProbe()
    {
        string run = SessionState.GetString(Key, "");
        if (!string.IsNullOrEmpty(run) && File.Exists(Path.Combine(run, "result.json")))
            report = JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(run, "result.json")));
        EditorApplication.update += Update;
        Application.logMessageReceived += ObserveLog;
    }

    [MenuItem("Lit/Validation/Play Bootstrap MainMenu District_1 (isolated save)")]
    public static void Run() => Begin(false);

    [MenuItem("Lit/Validation/Play rejected portal departures (isolated save)")]
    public static void RunRecovery() => Begin(true);

    public static void RunLimbes() => Begin(false, true);
    public static void RunLimbesCycles() => Begin(false, true, true);
    public static void RunLimbesRoutes() => Begin(false, true, true, true);

    private static void Begin(bool recovery, bool limbes = false, bool limbesCycles = false, bool limbesRoutes = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || !string.IsNullOrEmpty(SessionState.GetString(Key, ""))) return;
        Directory.CreateDirectory(Root);
        if (recovery && typeof(GameFlowService).GetMethod("RevealRejectedDepartureRoutine", Private) == null)
        {
            File.WriteAllText(Root + "/PlayProbe.result", "NOT RUN: import the departure recovery runtime before testing it.");
            return;
        }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var unsafeScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
            .Where(scene => scene.isDirty || string.IsNullOrEmpty(scene.path)).Select(scene => scene.name + " (" + scene.path + ")").ToArray();
        if (unsafeScenes.Length > 0)
        {
            File.WriteAllText(Root + "/PlayProbe.result", "NOT RUN: save scenes voluntarily first:\n" + string.Join("\n", unsafeScenes));
            return;
        }
        string id = Guid.NewGuid().ToString("N");
        report = new Report
        {
            run = Path.GetFullPath(Root + "/PlayRuns/" + id), clone = "Assets/__StartupDistrictValidation/" + id + "/Bootstrap.unity",
            recovery = recovery, limbes = limbes, limbesCycles = limbesCycles, limbesRoutes = limbesRoutes, fixtureId = "scene:validation:departure:" + id,
            utc = DateTime.UtcNow.ToString("O"), status = "Preparing", started = EditorApplication.timeSinceStartup,
            oldOptions = (int)EditorSettings.enterPlayModeOptions, oldOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled,
            oldPlayScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),
            scenes = setup.Select(scene => new SceneEntry { path = scene.path, loaded = scene.isLoaded, active = scene.isActive }).ToList()
        };
        Directory.CreateDirectory(report.run);
        if (limbes) report.scope = "Actual Bootstrap/menu solo creation, authored Maison/Limbes portals, six loaded Limbes scenes, one driven UCC jump and void recovery. Isolated save. Does not prove all routes, cycle completion, save continuation or multiplayer.";
        if (limbesCycles) report.scope = "Actual Bootstrap/menu, isolated solo session created through public SaveSessionManager/GameFlow services (intro presentation omitted), authored portals, real pickup inventory/knowledge handlers after range/light checks, both voluntary cycle interactions, permanent bridge, zone departure and re-entry. Fixture approaches use UCC positioning; not manual input, all jumps or multiplayer.";
        if (limbesRoutes) report.scope = "Actual Bootstrap and isolated session, authored portal, every authored jump link tested individually with standard walking input, actual UCC physics and landing. Fixtures position the actor at takeoff points; no sprint, optional ability, continuous manual traversal or multiplayer test.";
        if (recovery) report.scope += " Also injects one invalid-identity scene object before each departure (Maison to District_1 and District_1 to Maison), requires actual rejection, visible same world, restored navigation/control and a successful retry after removing only that fixture.";
        File.WriteAllText(Root + "/latest-play-run.txt", report.run);
        foreach (var path in setup.Select(scene => scene.path).Concat(new[] { Bootstrap }).Concat(EditorBuildSettings.scenes
                     .Where(scene => Path.GetFileName(scene.path).StartsWith("District_1", StringComparison.Ordinal)).Select(scene => scene.path)).Distinct())
            if (File.Exists(path)) report.files.Add(new FileEntry { path = path, hash = Hash(path) });
        SessionState.SetString(Key, report.run);
        Write();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(report.clone));
            File.Copy(Bootstrap, report.clone, false);
            AssetDatabase.ImportAsset(report.clone, ImportAssetOptions.ForceSynchronousImport);
            var clone = EditorSceneManager.OpenScene(report.clone, OpenSceneMode.Single);
            var roots = clone.GetRootGameObjects();
            var flow = roots.SelectMany(root => root.GetComponentsInChildren<GameFlowService>(true)).Single();
            var flowData = new SerializedObject(flow);
            flowData.FindProperty("editorStartGameplayDirectly").boolValue = false;
            flowData.FindProperty("loadMenuAfterBootstrap").boolValue = true;
            flowData.ApplyModifiedPropertiesWithoutUndo();
            var saves = roots.SelectMany(root => root.GetComponentsInChildren<SaveSessionManager>(true)).Single();
            var savesData = new SerializedObject(saves);
            savesData.FindProperty("savesRootFolder").stringValue = Path.Combine(report.run, "Saves");
            savesData.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(clone, report.clone)) throw new IOException("Could not save the owned Bootstrap fixture.");
            // NGO checks even an offline Play launch and otherwise opens an
            // Add Scene dialog. Register just our fixture and remove that exact
            // entry on teardown, preserving all other build-setting entries.
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(report.clone, true) }).ToArray();
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(report.clone);
            EditorSettings.enterPlayModeOptionsEnabled = false;
            Advance(0);
            var gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameView == null) throw new InvalidOperationException("Native GameView unavailable.");
            var gameWindow = EditorWindow.GetWindow(gameView, false, "Game", false);
            gameWindow.Show();
            gameWindow.Focus();
            report.status = "Launching"; Write();
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    private static void Update()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess()) return;
        string run = SessionState.GetString(Key, "");
        if (string.IsNullOrEmpty(run))
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating && File.Exists(Root + "/AuthoringCleanup.request"))
            {
                File.Delete(Root + "/AuthoringCleanup.request");
                foreach(string name in new[]{"", ".Access", ".Environment", ".Integration", ".Materials", ".Preview", ".Quests"})
                {
                    string path="Assets/Editor/Validation/ReveLimbesAuthoringSession"+name+".cs";
                    if(!File.Exists(path)) continue;
                    if(File.ReadAllText(path).Trim()!="// Owned import-deletion stub; authoring code archived outside Assets.")
                        throw new InvalidOperationException("Preserve a modified authoring stub: "+path);
                    if(!AssetDatabase.DeleteAsset(path)) throw new IOException("Native authoring cleanup failed: "+path);
                }
                File.WriteAllText(Root+"/AuthoringCleanup.result","FINISHED native AssetDatabase deletion of owned import stubs; generator code remains outside Assets.");
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                return;
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating && File.Exists(Root + "/RefreshOnly.request"))
            {
                File.Delete(Root + "/RefreshOnly.request");
                File.WriteAllText(Root + "/RefreshOnly.result", "REQUESTED " + DateTime.UtcNow.ToString("O") + "; pid=" + System.Diagnostics.Process.GetCurrentProcess().Id + "; import only, no scene save or Play launch.");
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                return;
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating && File.Exists(Root + "/Cleanup.request"))
            {
                File.Delete(Root + "/Cleanup.request");
                CleanupCompletedFixture();
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating && File.Exists(Request))
            {
                string mode = File.ReadAllText(Request).Trim(); File.Delete(Request);
                if (mode == "recovery") RunRecovery();
                else if (mode == "limbes") RunLimbes();
                else if (mode == "limbes-cycles") RunLimbesCycles();
                else if (mode == "limbes-routes") RunLimbesRoutes();
                else if (mode == "run") Run();
                else File.WriteAllText(Root + "/PlayProbe.result", "NOT RUN: unknown probe mode.");
            }
            return;
        }
        report ??= JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(run, "result.json")));
        if (report.status == "Stopping")
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore();
            return;
        }
        try
        {
            if (File.Exists(Path.Combine(run, "cancel.request"))) { Finish(false, "Cancelled by validation coordinator."); return; }
            if (EditorApplication.timeSinceStartup - report.started > 720) throw new TimeoutException("Overall play probe deadline exceeded.");
            if (!EditorApplication.isPlaying)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling &&
                    !EditorApplication.isUpdating && report.status == "Launching" && EditorApplication.timeSinceStartup-report.stageStarted>10)
                { Finish(false,"Unity returned to idle before Play Mode; inspect compilation or cancelled launch.");return; }
                if (!EditorApplication.isPlayingOrWillChangePlaymode && report.status != "Launching") Finish(false, "Play Mode was stopped before completion.");
                return;
            }
            if (EditorApplication.isPaused)
            {
                if (EditorApplication.timeSinceStartup >= nextReport) { nextReport = EditorApplication.timeSinceStartup + 10; Write(); }
                return;
            }
            report.status = "Playing";
            if (EditorApplication.timeSinceStartup - report.stageStarted > 240)
                throw new TimeoutException("Stage " + report.stage + " timed out; nav=" + NavMeshWorldService.Instance?.State + "; mode=" + InputModeCoordinator.CurrentMode);
            Tick();
            if (EditorApplication.timeSinceStartup >= nextReport) { nextReport = EditorApplication.timeSinceStartup + 10; Write(); }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    private static void Tick()
    {
        var flow = GameFlowService.Instance;
        var saves = SaveSessionManager.Instance;
        if (flow == null || saves == null) return;
        if (!string.Equals(Path.GetFullPath(saves.GetSavesRoot()), Path.Combine(report.run, "Saves"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unexpected save root: aborting before any menu action.");
        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        if (report.limbes && report.stage >= 60) { TickLimbes(); return; }
        switch (report.stage)
        {
            case 0:
                if (flow.IsTransitioning || !SceneManager.GetSceneByName("MainMenu").isLoaded || menu == null || Time.frameCount < 10) return;
                Check(!flow.HasGameplaySession && !saves.HasActiveSave, "Bootstrap reached the actual menu with no active slot.");
                if (report.limbesCycles)
                {
                    Check(saves.TryCreateNewGame("Validation Limbes cycles",SaveSessionType.Solo,"Validation",out var slot,out string error),"Created isolated cycle-test slot through production save service: "+error);
                    saves.SetActiveSave(slot.sessionId,slot.saveId);
                    Check(GameFlowService.StartOrLoadGame(flow.HubSceneName),"Production startup service accepted Maison; intro presentation omitted in this focused cycle probe.");
                    Advance(3);return;
                }
                menu.UI_Solo(); Advance(1); return;
            case 1:
                if (menu == null || (string)typeof(MainMenuController).GetField("currentMenu", Private)?.GetValue(menu)?.ToString() != "SoloOptions") return;
                Check(true, "Solo options selected through the production menu handler.");
                menu.UI_NewGame(); Advance(2); return;
            case 2:
                if (menu == null) return;
                var input = (TMP_InputField)typeof(MainMenuController).GetField("newGameNameInput", Private)?.GetValue(menu);
                if (input == null || !input.gameObject.activeInHierarchy) return;
                input.text = "Validation District_1 " + DateTime.UtcNow.ToString("HHmmss");
                menu.UI_ConfirmNewGame(); Advance(3); return;
            case 3:
                if (!ZoneReady("Assets/Scenes/Maison/ZoneManifest_Maison.asset") || !ControlReady(out var actor)) return;
                Check(saves.HasActiveSave && saves.CurrentSessionType == SaveSessionType.Solo, "Actual menu created an isolated solo session and spawned the controlled avatar.");
                Check(Camera.allCameras.Any(camera => camera.isActiveAndEnabled && camera.targetTexture == null), "A screen camera is enabled after Maison loading.");
                if (report.limbesCycles)
                    foreach(string id in LimbesProofIds)
                        Check(ItemRegistry.Resolve(id)!=null,"Limbes proof definition resolves from startup catalog before loading cycle scenes: "+id);
                if (report.recovery)
                {
                    InjectCaptureFailure(actor); Advance(30); UsePortal(actor);
                }
                else { UsePortal(actor, report.limbes ? AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Reve/ZoneManifest_Reve_Limbes.asset") : null); Advance(4); }
                return;
            case 4:
                if (report.limbes) { BeginLimbesChecks(); return; }
                if (!ZoneReady("Assets/Scenes/Maison/ZoneManifest_District_1.asset") || !ControlReady(out _)) return;
                Check(true, "All District_1 manifest scenes loaded, navigation ready, avatar grounded and exploration input restored.");
                Check(Camera.allCameras.Any(camera => camera.isActiveAndEnabled && camera.targetTexture == null), "A screen camera is enabled after District_1 loading.");
                if (report.recovery)
                {
                    ControlReady(out var districtActor); InjectCaptureFailure(districtActor); Advance(40);
                    Check(GameFlowService.ReturnToHub(), "Requested real District_1-to-Maison departure with an uncapturable fixture."); return;
                }
                Check(GameFlowService.OpenMainMenu(), "Production return-to-menu request accepted without an exit save."); Advance(5); return;
            case 30:
                if (!ZoneReady("Assets/Scenes/Maison/ZoneManifest_Maison.asset") || !ControlReady(out var rejectedActor)) return;
                ValidateRejectedDeparture(rejectedActor, 1); RemoveCaptureFixture(); Advance(31); return;
            case 31:
                if (FindCaptureFixture() != null || !ControlReady(out var retryActor)) return;
                UsePortal(retryActor); Advance(4); return;
            case 40:
                if (!ZoneReady("Assets/Scenes/Maison/ZoneManifest_District_1.asset") || !ControlReady(out var rejectedDistrictActor)) return;
                ValidateRejectedDeparture(rejectedDistrictActor, 2); RemoveCaptureFixture(); Advance(41); return;
            case 41:
                if (FindCaptureFixture() != null) return;
                Check(GameFlowService.ReturnToHub(), "Retried the real return to Maison after removing only the invalid fixture."); Advance(42); return;
            case 42:
                if (!ZoneReady("Assets/Scenes/Maison/ZoneManifest_Maison.asset") || !ControlReady(out _)) return;
                Check(true, "Return to Maison succeeded after the rejected capture; navigation and exploration restored.");
                Check(GameFlowService.OpenMainMenu(), "Production return-to-menu request accepted without an exit save."); Advance(5); return;
            case 5:
                if (flow.IsTransitioning || flow.HasGameplaySession || !SceneManager.GetSceneByName("MainMenu").isLoaded || menu == null) return;
                Check(LocalPlayerContext.LocalCharacterRoot == null, "Returning to MainMenu released the controlled avatar.");
                Check(report.errors.Count == 0, "No runtime Error/Exception/Assert was observed during this probe.");
                Finish(true, report.limbesRoutes ? "Every authored Limbes jump link traversed with actual UCC physics and landing." : report.limbesCycles ? "Actual pickups, both cycles, bridge activation and zone reload retention completed." : report.limbes ? "Actual Maison/Limbes portals, first UCC jump, fall recovery and return to menu completed." : report.recovery ? "Both real departure rejections recovered, retries completed and MainMenu restored." : "Solo startup, authored portal to District_1 and return to menu completed."); return;
        }
    }

    private static bool ZoneReady(string path)
    {
        var zone = AssetDatabase.LoadAssetAtPath<ZoneManifest>(path);
        if (zone == null) throw new InvalidOperationException("Manifest missing: " + path);
        return !GameFlowService.Instance.IsTransitioning && zone.LoadingSceneNames.Concat(zone.PostLoadingSceneNames)
            .Concat(new[] { zone.PrimarySceneName }).All(name => SceneManager.GetSceneByName(name).isLoaded ||
                CycleProgressionService.Instance != null && CycleProgressionService.Instance.IsCompletedScene(name)) &&
            NavMeshWorldService.Instance != null && NavMeshWorldService.Instance.IsReady && NavMeshWorldService.Instance.LastReport.zoneId == zone.PrimarySceneName;
    }

    private static bool ControlReady(out SquadCharacterController actor)
    {
        var root = LocalPlayerContext.LocalCharacterRoot;
        actor = root != null ? root.GetComponent<SquadCharacterController>() : null;
        bool ready = actor != null && actor.IsGrounded && InputModeCoordinator.CurrentMode == InputMode.Exploration;
        settledFrames = ready ? settledFrames + 1 : 0;
        return settledFrames >= 3;
    }

    private static void UsePortal(SquadCharacterController actor, ZoneManifest destination = null)
    {
        var district = destination != null ? destination : AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset");
        var candidates = UnityEngine.Object.FindObjectsByType<PortalController>(FindObjectsSortMode.None).Where(candidate => candidate.IsSceneTransition &&
            (ZoneManifest)typeof(PortalController).GetField("destinationZone", Private)?.GetValue(candidate) == district);
        var portal = destination != null ? candidates.OrderBy(candidate=>Vector3.SqrMagnitude(candidate.transform.position-actor.transform.position)).First() : candidates.Single();
        Vector3 anchor = portal.GetInteractionAnchor().position;
        foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
        foreach (float distance in new[] { 2f, 1.5f, 1f })
        foreach (var hit in Physics.RaycastAll(anchor + direction * distance + Vector3.up * 2, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance))
        {
            if (hit.transform.GetComponentInParent<SquadCharacterController>() != null || hit.normal.y < .7f || Mathf.Abs(hit.point.y - anchor.y) > 2.5f) continue;
            var position = hit.point + Vector3.up * .02f;
            if (Physics.OverlapCapsule(position + Vector3.up * .3f, position + Vector3.up * 1.5f, .25f, ~0, QueryTriggerInteraction.Ignore)
                .Any(collider => !collider.transform.IsChildOf(actor.transform))) continue;
            Check(actor.TrySetUccExternalPositionAndRotation(position, actor.transform.rotation, true), "UCC fixture approached the real portal on a clear physical floor.");
            Physics.SyncTransforms();
            Check(portal.ServerTryUse(actor.gameObject, out _, out _), "The authored portal accepted the normal production transition request.");
            return;
        }
        throw new InvalidOperationException("No safe physical approach to the authored District_1 portal.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        report.checks.Add(message); Write();
    }
    private static void Advance(int stage)
    {
        report.stage = stage; report.stageStarted = EditorApplication.timeSinceStartup; settledFrames = 0; Write();
    }
    private static void ObserveLog(string message, string stack, LogType type)
    {
        if (report == null || report.status != "Playing" && report.status != "Launching") return;
        if (type == LogType.Warning) { if (report.warnings.Count < 100) report.warnings.Add(message); return; }
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (report.recovery && (report.stage == 30 || report.stage == 40) && type == LogType.Error &&
            message.StartsWith("[PersistentWorld] snapshot export rejected invalid identity persistentId='" + report.fixtureId + "'", StringComparison.Ordinal))
        {
            report.expectedCaptureErrors.Add(message); return;
        }
        if (report.errors.Count < 100) report.errors.Add(message + "\n" + stack);
    }

    private static PersistentNetworkObject FindCaptureFixture() => UnityEngine.Object.FindObjectsByType<PersistentNetworkObject>(FindObjectsInactive.Include)
        .SingleOrDefault(candidate => candidate.PersistentId == report.fixtureId);

    private static void InjectCaptureFailure(SquadCharacterController actor)
    {
        Check(FindCaptureFixture() == null, "No previous rejection fixture remains in the world.");
        report.actorEntity = actor.GetEntityId().ToString();
        var fixture = new GameObject("Owned validation departure rejection");
        fixture.SetActive(false);
        SceneManager.MoveGameObjectToScene(fixture, SceneManager.GetActiveScene());
        var persistent = fixture.AddComponent<PersistentNetworkObject>();
        persistent.AssignSceneIdentity(report.fixtureId);
        persistent.SetRuntimePrefabId("validation:forbidden-on-scene-object");
        NetworkObjectRegistry.Instance.Register(persistent);
    }

    private static void ValidateRejectedDeparture(SquadCharacterController actor, int count)
    {
        Check(report.expectedCaptureErrors.Count == count, "The production archive rejected exactly one invalid fixture on departure " + count + ".");
        Check(actor.GetEntityId().ToString() == report.actorEntity, "Rejected departure retained the same controlled avatar.");
        var service = (LoadingScreenService)typeof(LoadingScreenService).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
        var group = service != null ? (CanvasGroup)typeof(LoadingScreenService).GetField("canvasGroup", Private)?.GetValue(service) : null;
        Check(group != null && group.alpha <= .001f && !group.blocksRaycasts, "Rejected departure released the opaque overlay and pointer blocker.");
        Check(Camera.allCameras.Any(camera => camera.isActiveAndEnabled && camera.targetTexture == null), "Rejected departure restored a screen camera.");
    }

    private static void RemoveCaptureFixture()
    {
        var fixture = FindCaptureFixture();
        Check(fixture != null, "The invalid fixture was preserved by the rejected departure.");
        NetworkObjectRegistry.Instance.Unregister(fixture);
        UnityEngine.Object.Destroy(fixture.gameObject);
    }
    private static void Finish(bool passed, string reason)
    {
        report.passed = passed; report.reason = reason; report.status = "Stopping"; Write();
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        else if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore();
    }
    private static void Restore()
    {
        try
        {
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)report.oldOptions;
            EditorSettings.enterPlayModeOptionsEnabled = report.oldOptionsEnabled;
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(report.oldPlayScene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(report.oldPlayScene);
            var modified = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                .Where(scene => scene.path != report.clone && scene.isDirty).ToArray();
            if (modified.Length != 0) throw new InvalidOperationException("Restoration deferred: an original/user scene has unsaved changes: " + string.Join(", ", modified.Select(scene => scene.path)));
            EditorSceneManager.RestoreSceneManagerSetup(report.scenes.Select(scene => new SceneSetup { path = scene.path, isLoaded = scene.loaded, isActive = scene.active }).ToArray());
            foreach (var file in report.files)
                if (!File.Exists(file.path) || Hash(file.path) != file.hash) throw new IOException("Saved source scene changed during probe: " + file.path);
            report.restored = true;
            RemoveOwnedBuildEntry(report.clone);
            AssetDatabase.DeleteAsset(report.clone);
            report.status = report.passed ? "Passed" : "Failed";
        }
        catch (Exception error) { report.passed = false; report.status = "Needs inspection"; report.errors.Add(error.ToString()); }
        finally { Write(); SessionState.EraseString(Key); report = null; }
    }
    private static void Write()
    {
        if (EditorApplication.isPlaying && report.status == "Playing")
        {
            var actorRoot = LocalPlayerContext.LocalCharacterRoot;
            var actor = actorRoot != null ? actorRoot.GetComponent<SquadCharacterController>() : null;
            report.diagnostics = "frame=" + Time.frameCount + "; paused=" + EditorApplication.isPaused +
                "; active=" + SceneManager.GetActiveScene().name + "; transition=" + GameFlowService.Instance?.IsTransitioning +
                "; actor=" + actor?.transform.position + "; grounded=" + actor?.IsGrounded +
                "; input=" + InputModeCoordinator.CurrentMode + "; nav=" + NavMeshWorldService.Instance?.State +
                "; navZone=" + NavMeshWorldService.Instance?.LastReport.zoneId;
            if(report.limbesRoutes) report.diagnostics+="; jumpProbe="+actor?.GetComponent<LimbesJumpProbeDriver>()?.reason;
        }
        File.WriteAllText(Path.Combine(report.run, "result.json"), JsonUtility.ToJson(report, true));
        File.WriteAllText(Root + "/PlayProbe.result", report.status + " stage=" + report.stage + " " + report.reason + "\n" + report.run);
    }
    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path); using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

    private static void RemoveOwnedBuildEntry(string path)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(path ?? "", @"^Assets/__StartupDistrictValidation/[a-f0-9]{32}/Bootstrap\.unity$"))
            throw new InvalidOperationException("Not an owned fixture path.");
        var scenes = EditorBuildSettings.scenes;
        if (scenes.Any(scene => scene.path == path)) EditorBuildSettings.scenes = scenes.Where(scene => scene.path != path).ToArray();
    }

    private static void CleanupCompletedFixture()
    {
        string latest = Root + "/latest-play-run.txt";
        if (!File.Exists(latest)) return;
        string run = Path.GetFullPath(File.ReadAllText(latest).Trim());
        string prefix = Path.GetFullPath(Root + "/PlayRuns") + Path.DirectorySeparatorChar;
        if (!run.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unowned report directory.");
        var completed = JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(run, "result.json")));
        if (!completed.restored || File.Exists(completed.clone)) throw new InvalidOperationException("Only a completed, deleted fixture can be cleaned up.");
        RemoveOwnedBuildEntry(completed.clone);
        File.WriteAllText(Root + "/Cleanup.result", "Removed only the completed fixture's build entry: " + completed.clone);
    }
}
