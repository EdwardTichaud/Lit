using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Real Bootstrap smoke in an isolated project; never uses or deletes a player's save slot.</summary>
[InitializeOnLoad]
public static partial class CombatBootstrapPlayValidation
{
    private const string Key = "Lit.CombatMigration.BootstrapSmoke";
    private const string DefaultManifestPath = "Assets/Scenes/Maison/ZoneManifest_District_1.asset";
    private static string ManifestPath => ResolveManifestPath(Environment.GetCommandLineArgs());
    private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
    private static int stage;
    private static float actionStarted;
    private static float actionStartedGameTime;
    private static int actionStartedFrame;
    private static int actionGeneration;
    private static string sessionId, saveId, characterId, defeatedMarkerId, teamState;
    private static int savedHealth;
    private static Vector3 savedPosition;
    private static int cursorProbeStage, cursorProbeFrame;
    private static GameObject cursorProbeOwner, cursorProbeVisual;
    private static string firstActorId;
    private static bool SaveRoundTrip => Environment.GetCommandLineArgs().Contains("-combatMigrationSaveRoundTrip");
    private static bool RepeatPlay => Environment.GetCommandLineArgs().Contains("-combatMigrationRepeatPlay");
    private static string RepeatMode => Environment.GetCommandLineArgs().Contains("-combatMigrationDomainReloadOff") ? "off" : "on";
    private static readonly List<string> checks = new List<string>();

    [Serializable] private sealed class Result
    {
        public bool passed;
        public int playCycle;
        public string domainReload;
        public string runId, reason, saveDirectory;
        public string zoneManifest;
        public bool fullProductionReadiness = false;
        public string limitation = "Bootstrap, binding, one Basic and snapshot disk roundtrip only; not a zone playthrough, real save restoration, manual feel or multiplayer evidence.";
        public List<string> checks;
    }

    internal static string ResolveManifestPath(string[] arguments)
    {
        int index = Array.IndexOf(arguments, "-combatMigrationZoneManifest");
        if (index < 0) return DefaultManifestPath;
        if (index + 1 >= arguments.Length || !arguments[index + 1].StartsWith("Assets/", StringComparison.Ordinal) ||
            !arguments[index + 1].EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("-combatMigrationZoneManifest requires an explicit Assets/.../*.asset path.");
        return arguments[index + 1];
    }

    static CombatBootstrapPlayValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated Editor smoke entry outside batch mode.");
        if (RepeatPlay)
        {
            if (Directory.Exists(".git") || File.Exists(".git")) throw new InvalidOperationException("Repeat play requires the isolated project.");
            SessionState.SetBool(Key + ".optionsStored", true);
            SessionState.SetBool(Key + ".optionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Key + ".options", (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetInt(Key + ".cycle", 1);
            SessionState.SetBool(Key + ".awaitingExit", false);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = RepeatMode == "off" ? EnterPlayModeOptions.DisableDomainReload : EnterPlayModeOptions.None;
        }
        try { PrepareSmoke(); }
        catch { RestoreEditorOptions(); throw; }
    }

    public static void RunRenderedIsolated()
    {
        if (Application.isBatchMode || !Environment.GetCommandLineArgs().Contains("-combatMigrationRenderedSmoke"))
            throw new InvalidOperationException("Explicit -combatMigrationRenderedSmoke in a rendered isolated Editor is required.");
        if (Directory.Exists(".git") || File.Exists(".git"))
            throw new InvalidOperationException("The rendered smoke cannot run in the source checkout.");
        PrepareSmoke();
        EditorApplication.ExecuteMenuItem("Window/General/Game");
    }

    [MenuItem("Lit/Combat/Production Migration/Validate Bootstrap In Isolated Editor")]
    public static void RunIsolatedEditor()
    {
        if (Application.isBatchMode) throw new InvalidOperationException("Use RunBatch for an isolated batch Editor.");
        if (Directory.Exists(".git") || File.Exists(".git")) throw new InvalidOperationException("Do not run this smoke in the source checkout.");
        if (!EditorUtility.DisplayDialog("Isolated Bootstrap validation",
            "This opens Bootstrap, creates a temporary Library-only save and closes this isolated Editor after the check. Keep Game View rendering. No production readiness is inferred.", "Run", "Cancel")) return;
        PrepareSmoke();
    }

    private static void PrepareSmoke()
    {
        if (Directory.Exists(".git") || File.Exists(".git"))
            throw new InvalidOperationException("Run only in a disposable isolated project, not the user's source checkout.");
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            throw new InvalidOperationException("Bootstrap's HDRP/loading presentation requires rendered frames. A -nographics run cannot validate this flow; use an isolated graphics-enabled Editor with Game View rendering.");
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(ManifestPath);
        if (manifest == null || !manifest.IsValid) throw new InvalidOperationException("Zone manifest is missing or invalid: " + ManifestPath);
        if (ManifestPath != DefaultManifestPath && (EncounterProbe || SaveRoundTrip))
            throw new InvalidOperationException("Encounter and enemy save fixtures currently require the authored District 1 manifest.");
        if (ZoneRoundTrip && (manifest.PrimarySceneName != "Maison_Core" || EncounterProbe || SaveRoundTrip || RepeatPlay))
            throw new InvalidOperationException("Zone roundtrip requires the Maison manifest and a separate single Play Mode recipe.");
        if (GamepadProbe && !ZoneRoundTrip) throw new InvalidOperationException("The gamepad probe requires the zone-roundtrip recipe.");
        var requiredScenes = manifest.LoadingSceneNames.Concat(new[] { manifest.PrimarySceneName }).Distinct().ToArray();
        var buildScenes = EditorBuildSettings.scenes.Where(entry => entry.enabled)
            .Select(entry => Path.GetFileNameWithoutExtension(entry.path)).ToArray();
        string[] missing = requiredScenes.Except(buildScenes).ToArray();
        if (missing.Length != 0) throw new InvalidOperationException("Required zone scenes missing from enabled Build Settings: " + string.Join(", ", missing));
        var scene = EditorSceneManager.OpenScene(BootstrapPath);
        var flows = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameFlowService>(true)).ToArray();
        var saves = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SaveSessionManager>(true)).ToArray();
        if (flows.Length != 1 || saves.Length != 1) throw new InvalidOperationException("Bootstrap must author one flow and one save service.");
        string runId = Guid.NewGuid().ToString("N");
        string saveDirectory = Path.GetFullPath("Library/ProductionCombatMigration/BootstrapSaves/" + runId);
        // Unsaved scene-instance overrides prevent menu/editor shortcuts and isolate disk writes.
        Set(flows[0], "editorStartGameplayDirectly", false);
        Set(flows[0], "loadMenuAfterBootstrap", false);
        Set(saves[0], "savesRootFolder", saveDirectory);
        EditorSceneManager.MarkSceneDirty(scene);
        SessionState.SetString(Key + ".run", runId);
        SessionState.SetString(Key + ".save", saveDirectory);
        SessionState.SetFloat(Key + ".started", (float)EditorApplication.timeSinceStartup);
        SessionState.SetBool(Key, true);
        stage = cursorProbeStage = 0; cursorProbeOwner = cursorProbeVisual = null; checks.Clear();
        ResetEncounterProbe();
        ResetZoneTransferProbe();
        CleanupGamepadProbe();
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }

    private static void Set(UnityEngine.Object owner, string name, object value)
    {
        var serialized = new SerializedObject(owner);
        var property = serialized.FindProperty(name);
        if (property == null) throw new InvalidOperationException("Missing Bootstrap fixture setting: " + name);
        if (value is bool flag) property.boolValue = flag;
        else if (value is string text) property.stringValue = text;
        else throw new ArgumentException("Unsupported Bootstrap fixture setting: " + name);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks.Add(message);
        Debug.Log("[CombatBootstrapSmoke] PASS " + message);
    }

    private static void Tick()
    {
        if (SessionState.GetBool(Key + ".awaitingExit", false) &&
            EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".exitStarted", 0) > 45)
        {
            CompleteRepeat(false, "Timed out waiting for native Play Mode exit.");
            return;
        }
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".started", 0) > 180)
                throw new TimeoutException("Bootstrap smoke exceeded 180 seconds; stage=" + stage +
                    ", nav=" + (NavMeshWorldService.Instance != null ? NavMeshWorldService.Instance.State.ToString() : "absent"));
            var flow = GameFlowService.Instance;
            if (stage == 0)
            {
                if (flow == null || SaveSessionManager.Instance == null || Time.frameCount < 3) return;
                var saves = SaveSessionManager.Instance;
                Require(Path.GetFullPath(saves.GetSavesRoot()) == SessionState.GetString(Key + ".save", ""), "Save writes isolated under the validation project");
                Require(!flow.IsTransitioning && !flow.HasGameplaySession, "Bootstrap shortcuts disabled before creating the temporary slot");
                Require(!saves.HasActiveSave, "No existing player save selected");
                var session = saves.CreateSession("Combat migration validation", SaveSessionType.Solo);
                var slot = saves.CreateSave(session.sessionId, "Bootstrap smoke");
                Require(slot != null, "Temporary real save slot created");
                sessionId = session.sessionId; saveId = slot.saveId;
                saves.SetActiveSave(session.sessionId, slot.saveId);
                var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(ManifestPath);
                Require(GameFlowService.StartOrLoadGame(manifest.PrimarySceneName), "Gameplay requested through the normal Bootstrap flow");
                Advance(1);
                return;
            }
            var manager = RealTimeCombatManager.Instance;
            var root = LocalPlayerContext.LocalCharacterRoot;
            if (stage == 1)
            {
                if (flow == null || flow.IsTransitioning || manager == null || root == null) return;
                Require(NavMeshWorldService.Instance != null && NavMeshWorldService.Instance.IsReady, "Destination NavMesh ready without a compensating warp");
                var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(ManifestPath);
                foreach (string requiredScene in manifest.LoadingSceneNames.Concat(new[] { manifest.PrimarySceneName }).Distinct())
                    Require(SceneManager.GetSceneByName(requiredScene).isLoaded, "Required zone scene loaded: " + requiredScene);
                var worlds = UnityEngine.Object.FindObjectsByType<CombatWorldRuntime>();
                Require(worlds.Length == 1, "Exactly one active common combat world");
                Require(manager.PlayerRoot == root && manager.AllowsFreeAttacks, "Controlled actor bound to production free-attack runtime");
                Require(UnityEngine.Object.FindObjectsByType<LucianBrainsCombatArena>().Length == 0, "No laboratory arena in production");
                var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
                var actor = root.GetComponent<SquadCharacterController>();
                Require(actor != null && catalog != null && catalog.IsCharacter(actor.CharacterData, CombatSkillKit.Lucian), "Solo controlled actor retains Lucian identity");
                var spawn = ZoneSpawnPoint.FindFirstInScene(manifest.PrimarySceneName);
                Require(spawn != null && Vector3.ProjectOnPlane(root.position - spawn.transform.position, Vector3.up).magnitude < .25f &&
                    Mathf.Abs(root.position.y - spawn.transform.position.y) < .5f,
                    "New game arrives at the authored zone spawn, not the squad origin: " + root.position);
                Require(actor.IsGrounded, "New-game controlled actor has reached physical ground before attacking");
                foreach (var member in UnityEngine.Object.FindObjectsByType<SquadCharacterController>())
                    foreach (var kit in new[] { CombatSkillKit.Vesta, CombatSkillKit.Soren, CombatSkillKit.Aurell })
                        Require(!catalog.IsCharacter(member.CharacterData, kit), "No physical solo companion: " + kit);
                var basic = actor.CharacterData.groundBasicSkills?.FirstOrDefault(skill => skill != null);
                Require(basic != null && manager.TryUseSkill(basic), "Authored Basic launches through the production facade");
                actionGeneration = root.GetComponent<PlayerActionPresentationController>().ActionGeneration;
                actionStarted = Time.unscaledTime;
                actionStartedGameTime = Time.time;
                actionStartedFrame = Time.frameCount;
                Advance(2);
                return;
            }
            if (stage == 2)
            {
                var action = root != null ? root.GetComponent<PlayerActionPresentationController>() : null;
                if (action == null) throw new InvalidOperationException("Controlled actor disappeared during Basic execution.");
                if (action.IsCurrentSession(actionGeneration))
                {
                    if (Time.unscaledTime - actionStarted > 8) throw new TimeoutException("Production Basic retained its session beyond eight seconds. " + DescribeAction(action));
                    return;
                }
                if (cursorProbeStage == 0) Require(!action.IsActionActive, "Basic session completed and restored control");
                if (!ValidateCursorLifecycle(manager, root)) return;
                if (ZoneRoundTrip && !ValidateZoneTransfer(flow, manager, root.GetComponent<SquadCharacterController>())) return;
                if (EncounterProbe && !ValidateProductionEncounter(manager, root)) return;
                var adapter = WorldSaveAdapter.Instance;
                Require(adapter != null, "Production world save adapter available");
                adapter.SaveWorldSnapshot();
                Require(adapter.TryLoadWorldSnapshot(out var snapshot) && snapshot != null, "Real world snapshot written and read from the isolated slot");
                string primaryScene = AssetDatabase.LoadAssetAtPath<ZoneManifest>(ManifestPath).PrimarySceneName;
                Require(SceneManager.GetSceneByName(primaryScene).isLoaded, "Selected zone remains loaded after the smoke: " + primaryScene);
                if (SaveRoundTrip)
                {
                    var actor = root.GetComponent<SquadCharacterController>();
                    characterId = actor.CharacterData.UniqueId;
                    firstActorId = actor.GetEntityId().ToString();
                    savedHealth = Mathf.Max(1, actor.MaxHp - 7);
                    actor.SetCurrentHp(savedHealth);
                    var spawn = ZoneSpawnPoint.FindFirstInScene("District_1_Core");
                    Require(TryFindSaveFixturePosition(root, spawn.transform, out var savedPose),
                        "Save fixture selects clear physical ground near the spawn; player movement does not require a NavMesh polygon");
                    Require(actor.TrySetUccExternalPositionAndRotation(savedPose, root.rotation, true),
                        "Save fixture moves only Lucian through UCC before capturing his actual position");
                    savedPosition = root.position;
                    Require(Vector3.ProjectOnPlane(savedPosition - spawn.transform.position, Vector3.up).magnitude > 1,
                        "Saved position is not the initial spawn, so restoration cannot pass via a fallback reset");
                    teamState = TeamState();
                    var marker = UnityEngine.Object.FindObjectsByType<SceneMarker>()
                        .Where(candidate => candidate.CharacterData != null && candidate.CharacterData.UniqueId == "c54e94b14c7826c4482529b6d4d739ec" && candidate.RuntimeInstance != null)
                        .OrderBy(candidate => candidate.MarkerId, StringComparer.Ordinal).FirstOrDefault();
                    Require(marker != null, "Actual production Juggernaut marker available for persistence check");
                    defeatedMarkerId = marker.MarkerId;
                    var health = marker.RuntimeInstance.GetComponent<CharacterInfo>();
                    Require(health != null, "Marker-owned enemy retains CharacterInfo");
                    health.ForceDefeat();
                    Require(health.IsDead, "Persistence fixture defeats the actual marker-owned enemy, not a replacement");
                    Require(CharacterStateStore.Instance != null, "Character save store available");
                    CharacterStateStore.Instance.Save();
                    Advance(3);
                    return;
                }
                Finish(true, "Bounded production smoke complete; full delivery gates remain separate.");
            }
            if (stage == 3)
            {
                var store = CharacterStateStore.Instance;
                if (store != null && store.IsSaving) return;
                Require(store != null && store.LastSaveSucceeded, "Character and world save completed through the production store");
                // Unsaved divergence proves that the subsequent actor is restored from disk.
                root.GetComponent<SquadCharacterController>().RestoreHealthToMax();
                Require(GameFlowService.OpenMainMenu(), "Normal menu return requested without overwriting the saved health");
                Advance(4);
                return;
            }
            if (stage == 4)
            {
                if (flow == null || flow.IsTransitioning || flow.HasGameplaySession) return;
                Require(LocalPlayerContext.LocalCharacterRoot == null, "Previous controlled actor released on menu return");
                SaveSessionManager.Instance.SetActiveSave(sessionId, saveId);
                Require(GameFlowService.StartOrLoadGame(), "Saved destination requested from real slot metadata");
                Advance(5);
                return;
            }
            if (stage == 5)
            {
                if (flow == null || flow.IsTransitioning || root == null) return;
                var actor = root.GetComponent<SquadCharacterController>();
                var adapter = WorldSaveAdapter.Instance;
                Require(UnityEngine.Object.FindObjectsByType<SquadCharacterController>(FindObjectsInactive.Include)
                    .Count(candidate => candidate.CharacterData != null && candidate.CharacterData.UniqueId == characterId) == 1,
                    "Reload retains exactly one physical instance of the controlled character");
                Require(actor != null && actor.GetEntityId().ToString() != firstActorId && actor.CharacterData.UniqueId == characterId,
                    "Reload spawned a new actor with the same persistent character identity");
                Require(actor.CurrentHp == savedHealth, "Saved health restored instead of the unsaved runtime value (expected=" +
                    savedHealth + ", actual=" + actor.CurrentHp + ")");
                Require(Vector3.ProjectOnPlane(root.position - savedPosition, Vector3.up).magnitude < .15f &&
                    Mathf.Abs(root.position.y - savedPosition.y) < .35f,
                    "Reload retains the saved actor position instead of resetting to the new-game spawn: " + root.position);
                Require(adapter != null && adapter.LastRestoreSucceeded && adapter.LastRestoreIdentityValidated && adapter.LastRestoreIdentityIssues == 0,
                    "World snapshot restoration succeeded with validated identities");
                Require(TeamState() == teamState, "Companion recruitment and skill unlocks preserved without artificial unlocks");
                var marker = UnityEngine.Object.FindObjectsByType<SceneMarker>(FindObjectsInactive.Include)
                    .SingleOrDefault(candidate => candidate.MarkerId == defeatedMarkerId);
                Require(marker != null && marker.RuntimeInstance != null && marker.RuntimeInstance.transform.parent == marker.transform,
                    "Saved enemy retained its original marker identity and parentage");
                Require(marker.RuntimeInstance.GetComponent<CharacterInfo>()?.IsDead == true,
                    "Defeated marker-owned enemy did not resurrect on reload");
                Require(NavMeshWorldService.Instance != null && NavMeshWorldService.Instance.IsReady,
                    "Navigation validated again after real reload");
                Finish(true, "Bootstrap, Basic, menu return and real save reload complete; combat feel, all zones and multiplayer remain separate.");
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false, exception.ToString()); }
    }

    private static bool ValidateCursorLifecycle(RealTimeCombatManager manager, Transform actor)
    {
        if (cursorProbeStage == 3) return true;
        if (cursorProbeStage == 0)
        {
            var world = manager.GetComponent<CombatWorldRuntime>();
            Require(world != null && world.lockCursorPrefab != null, "Production world owns an authored lock cursor prefab");
            cursorProbeOwner = new GameObject("Lock cursor lifecycle probe");
            cursorProbeOwner.transform.position = actor.position;
            cursorProbeOwner.transform.localScale = new Vector3(2, 4, 6);
            var indicator = cursorProbeOwner.AddComponent<CombatLockIndicator>();
            world.ConfigureLockIndicator(indicator);
            indicator.SetLocked(true, false);
            cursorProbeVisual = ReadCursorVisual(indicator);
            Require(cursorProbeVisual != null && cursorProbeVisual.activeSelf && cursorProbeVisual.transform.parent == null &&
                cursorProbeVisual.scene == cursorProbeOwner.scene && cursorProbeVisual.GetComponentInChildren<Canvas>() == null &&
                Vector3.Distance(cursorProbeVisual.transform.position, actor.position + Vector3.up * 3) < .001f,
                "World lock visual is unparented at owner +3 Y without a UI canvas");
            indicator.enabled = false;
            cursorProbeFrame = Time.frameCount;
            cursorProbeStage = 1;
            return false;
        }
        if (Time.frameCount - cursorProbeFrame < 2) return false;
        if (cursorProbeStage == 1)
        {
            Require(cursorProbeVisual == null, "Native OnDisable releases the world cursor");
            var indicator = cursorProbeOwner.GetComponent<CombatLockIndicator>();
            indicator.enabled = true;
            indicator.SetLocked(true, false);
            cursorProbeVisual = ReadCursorVisual(indicator);
            Require(cursorProbeVisual != null && cursorProbeVisual.activeSelf, "A new owner presentation recreates its cursor");
            UnityEngine.Object.Destroy(cursorProbeOwner);
            cursorProbeStage = 2;
            cursorProbeFrame = Time.frameCount;
            return false;
        }
        Require(cursorProbeOwner == null && cursorProbeVisual == null, "Native owner destruction releases its unparented cursor");
        cursorProbeStage = 3;
        return true;
    }

    private static bool TryFindSaveFixturePosition(Transform actor, Transform spawn, out Vector3 position)
    {
        foreach (var direction in new[] { spawn.forward, -spawn.forward, spawn.right, -spawn.right })
        {
            Vector3 expected = actor.position + Vector3.ProjectOnPlane(direction, Vector3.up).normalized * 2;
            foreach (var ground in Physics.RaycastAll(expected + Vector3.up, Vector3.down, 2, ~0, QueryTriggerInteraction.Ignore)
                .OrderBy(hit => hit.distance))
            {
                if (ground.transform.IsChildOf(actor) || ground.transform.GetComponentInParent<SquadCharacterController>() != null ||
                    Vector3.Dot(ground.normal, Vector3.up) < .7f || Mathf.Abs(ground.point.y - actor.position.y) > .35f) continue;
                Vector3 candidate = ground.point + Vector3.up * .02f;
                bool blocked = Physics.OverlapCapsule(candidate + Vector3.up * .3f, candidate + Vector3.up * 1.5f,
                    .25f, ~0, QueryTriggerInteraction.Ignore).Any(collider => !collider.transform.IsChildOf(actor));
                if (blocked) continue;
                position = candidate;
                return true;
            }
        }
        position = default;
        return false;
    }

    private static GameObject ReadCursorVisual(CombatLockIndicator indicator) =>
        (GameObject)typeof(CombatLockIndicator).GetField("visualRoot", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).GetValue(indicator);

    private static string DescribeAction(PlayerActionPresentationController action)
    {
        var animator = action.GetComponentInChildren<Animator>(true);
        var state = animator != null ? animator.GetCurrentAnimatorStateInfo(0) : default;
        var next = animator != null && animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : default;
        var domain = action.GetComponent<CombatTimeDomain>();
        string floats = animator != null ? string.Join(",", animator.parameters.Where(p => p.type == AnimatorControllerParameterType.Float)
            .Select(p => p.name + "=" + animator.GetFloat(p.nameHash))) : "absent";
        return "frames=" + (Time.frameCount - actionStartedFrame) + " gameSeconds=" + (Time.time - actionStartedGameTime) +
            " globalScale=" + Time.timeScale + " localScale=" + (domain != null ? domain.Scale : 1) +
            " enabled=" + action.isActiveAndEnabled + " charging=" + action.IsPreparingBasicTail +
            " animator=" + (animator != null ? animator.isActiveAndEnabled + "/" + animator.speed : "absent") +
            " state=" + state.fullPathHash + "/" + state.normalizedTime + " next=" + next.fullPathHash + "/" + next.normalizedTime +
            " floats=" + floats;
    }

    private static void Advance(int next)
    {
        stage = next;
        SessionState.SetFloat(Key + ".started", (float)EditorApplication.timeSinceStartup);
    }

    private static string TeamState()
    {
        var team = SoloTeamProgression.Resolve();
        Require(team != null, "Solo progression service available");
        return string.Join("|", Enum.GetValues(typeof(CombatSkillKit)).Cast<CombatSkillKit>().Select(kit =>
            kit + ":" + team.IsRecruited(kit) + ":" + string.Concat(Enumerable.Range(0, 4).Select(slot => team.IsUnlocked(kit, slot) ? "1" : "0"))));
    }

    private static void Finish(bool passed, string reason)
    {
        CleanupGamepadProbe();
        CleanupEncounterProbe();
        if (cursorProbeOwner != null) UnityEngine.Object.Destroy(cursorProbeOwner);
        if (cursorProbeVisual != null) UnityEngine.Object.Destroy(cursorProbeVisual);
        SessionState.SetBool(Key, false);
        string directory = ProductionCombatMigration.ReportDirectory;
        Directory.CreateDirectory(directory);
        string json = JsonUtility.ToJson(new Result {
            passed = passed, reason = reason, checks = checks, runId = SessionState.GetString(Key + ".run", ""),
            playCycle = RepeatPlay ? SessionState.GetInt(Key + ".cycle", 1) : 1,
            domainReload = RepeatPlay ? RepeatMode : "editor-current-options",
            saveDirectory = SessionState.GetString(Key + ".save", ""), zoneManifest = ManifestPath,
            limitation = GamepadProbe ? "Real solo zone roundtrip plus virtual-gamepad stick/X routed through the production Input System. Short physical movement and single Basic in each zone; no manual feel, interaction-key, narrative, save reload or multiplayer proof." :
                ZoneRoundTrip ? "Real solo Maison portal admission, District arrival and return-to-hub with actor/control continuity and Basic sessions. Player fixture placed near the authored portal through UCC; not manual interaction, movement feel, narrative gates, save reload or multiplayer evidence." :
                EncounterProbe ? "Bounded actual production Juggernaut vision/navigation/repeated impacts probe. Optional explicit lit fixture is reported in checks; no proof of Flame interaction/progression, manual feel, all encounters, victory or multiplayer." :
                SaveRoundTrip ? "Real isolated Bootstrap/menu/save reload with health, existing unlocks and marker-owned enemy persistence; no claim of manual combat feel, all zones, authored combat kill or multiplayer validation." :
                "Bootstrap, binding, one Basic and snapshot disk roundtrip only; not a zone playthrough, real save restoration, manual feel or multiplayer evidence."
        }, true);
        File.WriteAllText(directory + "/bootstrap-smoke.json", json);
        Debug.Log("[CombatBootstrapSmoke] " + (passed ? "PASS" : "FAIL") + " " + reason);
        if (RepeatPlay)
        {
            File.WriteAllText(directory + "/bootstrap-repeat-" + RepeatMode + "-" + SessionState.GetInt(Key + ".cycle", 1) + ".json", json);
            SessionState.SetBool(Key + ".cyclePassed", passed);
            SessionState.SetBool(Key + ".awaitingExit", true);
            SessionState.SetFloat(Key + ".exitStarted", (float)EditorApplication.timeSinceStartup);
            EditorApplication.isPlaying = false;
            return;
        }
        EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key + ".awaitingExit", false)) return;
        SessionState.SetBool(Key + ".awaitingExit", false);
        EditorApplication.delayCall += ContinueRepeat;
    }

    private static void ContinueRepeat()
    {
        try
        {
            if (!SessionState.GetBool(Key + ".cyclePassed", false)) { CompleteRepeat(false, "A production play cycle failed."); return; }
            if (LocalPlayerContext.LocalCharacterRoot != null || RealTimeCombatManager.Instance != null ||
                NavMeshWorldService.Instance != null || TimeManager.Instance != null ||
                UnityEngine.Object.FindObjectsByType<SquadCharacterController>(FindObjectsInactive.Include).Length != 0)
                throw new InvalidOperationException("Play Mode exit retained a player or combat/navigation/time singleton.");
            int cycle = SessionState.GetInt(Key + ".cycle", 1);
            Debug.Log("[CombatBootstrapRepeat] PASS native cleanup after cycle=" + cycle + " domainReload=" + RepeatMode);
            if (cycle >= 2) { CompleteRepeat(true, "Two native play entries and exits, production checks and cleanup passed."); return; }
            SessionState.SetInt(Key + ".cycle", cycle + 1);
            PrepareSmoke();
        }
        catch (Exception exception) { Debug.LogException(exception); CompleteRepeat(false, exception.ToString()); }
    }

    private static void CompleteRepeat(bool passed, string reason)
    {
        SessionState.SetBool(Key, false);
        SessionState.SetBool(Key + ".awaitingExit", false);
        Directory.CreateDirectory(ProductionCombatMigration.ReportDirectory);
        File.WriteAllText(ProductionCombatMigration.ReportDirectory + "/bootstrap-repeat-" + RepeatMode + ".json",
            JsonUtility.ToJson(new Result { passed = passed, reason = reason, domainReload = RepeatMode, zoneManifest = ManifestPath,
                playCycle = SessionState.GetInt(Key + ".cycle", 0), checks = new List<string> {
                    "Per-cycle artifacts: bootstrap-repeat-" + RepeatMode + "-1.json and -2.json; no full delivery gate inferred." } }, true));
        RestoreEditorOptions();
        Debug.Log("[CombatBootstrapRepeat] " + (passed ? "PASS " : "FAIL ") + reason);
        EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void RestoreEditorOptions()
    {
        if (!SessionState.GetBool(Key + ".optionsStored", false)) return;
        EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + ".optionsEnabled", false);
        EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Key + ".options", 0);
        SessionState.SetBool(Key + ".optionsStored", false);
    }
}
