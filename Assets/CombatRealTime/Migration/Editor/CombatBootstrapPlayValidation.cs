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
public static class CombatBootstrapPlayValidation
{
    private const string Key = "Lit.CombatMigration.BootstrapSmoke";
    private const string ManifestPath = "Assets/Scenes/Maison/ZoneManifest_District_1.asset";
    private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
    private static int stage;
    private static float actionStarted;
    private static int actionGeneration;
    private static string sessionId, saveId, characterId, defeatedMarkerId, teamState;
    private static int savedHealth;
    private static string firstActorId;
    private static bool SaveRoundTrip => Environment.GetCommandLineArgs().Contains("-combatMigrationSaveRoundTrip");
    private static readonly List<string> checks = new List<string>();

    [Serializable] private sealed class Result
    {
        public bool passed;
        public string runId, reason, saveDirectory;
        public bool fullProductionReadiness = false;
        public string limitation = "Bootstrap, binding, one Basic and snapshot disk roundtrip only; not a zone playthrough, real save restoration, manual feel or multiplayer evidence.";
        public List<string> checks;
    }

    static CombatBootstrapPlayValidation() => EditorApplication.update += Tick;

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated Editor smoke entry outside batch mode.");
        PrepareSmoke();
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
        if (manifest == null || !manifest.IsValid) throw new InvalidOperationException("District 1 manifest is missing or invalid.");
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
        stage = 0; checks.Clear();
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
                foreach (var member in UnityEngine.Object.FindObjectsByType<SquadCharacterController>())
                    foreach (var kit in new[] { CombatSkillKit.Vesta, CombatSkillKit.Soren, CombatSkillKit.Aurell })
                        Require(!catalog.IsCharacter(member.CharacterData, kit), "No physical solo companion: " + kit);
                var basic = actor.CharacterData.groundBasicSkills?.FirstOrDefault(skill => skill != null);
                Require(basic != null && manager.TryUseSkill(basic), "Authored Basic launches through the production facade");
                actionGeneration = root.GetComponent<PlayerActionPresentationController>().ActionGeneration;
                actionStarted = Time.unscaledTime;
                Advance(2);
                return;
            }
            if (stage == 2)
            {
                var action = root != null ? root.GetComponent<PlayerActionPresentationController>() : null;
                if (action == null) throw new InvalidOperationException("Controlled actor disappeared during Basic execution.");
                if (action.IsCurrentSession(actionGeneration))
                {
                    if (Time.unscaledTime - actionStarted > 8) throw new TimeoutException("Production Basic retained its session beyond eight seconds.");
                    return;
                }
                Require(!action.IsActionActive, "Basic session completed and restored control");
                var adapter = WorldSaveAdapter.Instance;
                Require(adapter != null, "Production world save adapter available");
                adapter.SaveWorldSnapshot();
                Require(adapter.TryLoadWorldSnapshot(out var snapshot) && snapshot != null, "Real world snapshot written and read from the isolated slot");
                Require(SceneManager.GetSceneByName("District_1_Core").isLoaded, "District 1 remains loaded after the smoke");
                if (SaveRoundTrip)
                {
                    var actor = root.GetComponent<SquadCharacterController>();
                    characterId = actor.CharacterData.UniqueId;
                    firstActorId = actor.GetEntityId().ToString();
                    savedHealth = Mathf.Max(1, actor.MaxHp - 7);
                    actor.SetCurrentHp(savedHealth);
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
                Require(actor != null && actor.GetEntityId().ToString() != firstActorId && actor.CharacterData.UniqueId == characterId,
                    "Reload spawned a new actor with the same persistent character identity");
                Require(actor.CurrentHp == savedHealth, "Saved health restored instead of the unsaved runtime value");
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
        SessionState.SetBool(Key, false);
        string directory = ProductionCombatMigration.ReportDirectory;
        Directory.CreateDirectory(directory);
        File.WriteAllText(directory + "/bootstrap-smoke.json", JsonUtility.ToJson(new Result {
            passed = passed, reason = reason, checks = checks, runId = SessionState.GetString(Key + ".run", ""),
            saveDirectory = SessionState.GetString(Key + ".save", ""),
            limitation = SaveRoundTrip ? "Real isolated Bootstrap/menu/save reload with health, existing unlocks and marker-owned enemy persistence; no claim of manual combat feel, all zones, authored combat kill or multiplayer validation." :
                "Bootstrap, binding, one Basic and snapshot disk roundtrip only; not a zone playthrough, real save restoration, manual feel or multiplayer evidence."
        }, true));
        Debug.Log("[CombatBootstrapSmoke] " + (passed ? "PASS" : "FAIL") + " " + reason);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
