using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Shared edit-time authoring. Runtime discovery never creates attack volumes.</summary>
public static class CombatClashAuthoring
{
    public static bool NeedsChanges(GameObject actor, CombatLabClashController config, bool enemy)
    {
        ValidateActor(actor, enemy);
        var volume = actor.GetComponent<CombatLabClashActor>();
        if (volume != null) { ValidateVolume(actor, volume); return false; }
        ValidateGeometry(config, enemy);
        return true;
    }

    public static bool Ensure(GameObject actor, CombatLabClashController config, bool enemy)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Clash volumes must be authored before Play Mode/spawn.");
        if (!NeedsChanges(actor, config, enemy)) return false;
        if (EditorUtility.IsPersistent(actor)) throw new InvalidOperationException("Load prefab contents before calling CombatClashAuthoring.Ensure.");
        var volume = actor.AddComponent<CombatLabClashActor>();
        volume.localCenter = enemy ? config.enemyCenter : config.playerCenter;
        volume.radius = enemy ? config.enemyRadius : config.playerRadius;
        EditorUtility.SetDirty(volume);
        ValidateVolume(actor, volume);
        return true;
    }

    public static void Validate(GameObject actor, bool enemy)
    {
        ValidateActor(actor, enemy);
        var volume = actor.GetComponent<CombatLabClashActor>();
        if (volume == null) throw new InvalidOperationException(actor.name + ": missing explicitly authored CombatLabClashActor.");
        ValidateVolume(actor, volume);
    }

    public static bool EnsurePrefab(GameObject prefab, CombatLabClashController config, bool enemy)
    {
        string path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("An explicit actor prefab asset is required.");
        if (!NeedsChanges(prefab, config, enemy)) return false;
        string guid = AssetDatabase.AssetPathToGUID(path);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (Ensure(root, config, enemy) && PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                throw new InvalidOperationException("Clash prefab save refused: " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        if (guid != AssetDatabase.AssetPathToGUID(path)) throw new InvalidOperationException("Clash authoring changed prefab GUID: " + path);
        Validate(AssetDatabase.LoadAssetAtPath<GameObject>(path), enemy);
        return true;
    }

    public static void ValidateData(CharacterData data)
    {
        if (data == null || data.ResolveWorldPrefab() == null) throw new InvalidOperationException("Clash actor CharacterData/prefab is missing.");
        var prefab = data.ResolveWorldPrefab();
        if (prefab.GetComponent<CharacterInfo>()?.SourceData != data)
            throw new InvalidOperationException(data.name + ": clash actor health must reference its actual CharacterData.");
        Validate(prefab, data.isEnemy);
    }

    public static bool IsCompatibleEnemySkill(CombatLabClashController config, SkillSO skill)
    {
        if (config == null || config.labEnemyProfile == null || config.labEnemyProfile.attacks == null || skill == null)
            throw new InvalidOperationException("Explicit clash configuration/profile/skill required; no skill-name inference.");
        return config.labEnemyProfile.attacks.Any(attack => attack != null && attack.skill == skill &&
            (config.compatibleEnemyStates ?? Array.Empty<string>()).Contains(attack.stateName));
    }

    // Production session authoring must merge these exact state aliases into its controller config.
    public static string[] RequiredEnemyStateAliases(CombatLabClashController config, LitBrainsCombatProfile profile) =>
        (profile != null ? profile.attacks : null)?.Where(attack => attack != null && IsCompatibleEnemySkill(config, attack.skill))
            .Select(attack => attack.stateName.Substring(attack.stateName.LastIndexOf('.') + 1)).Distinct().OrderBy(state => state, StringComparer.Ordinal).ToArray()
        ?? Array.Empty<string>();

    private static void ValidateActor(GameObject actor, bool enemy)
    {
        if (actor == null || actor.GetComponent<Animator>() == null || actor.GetComponent<AnimationEvents>() == null ||
            (enemy ? actor.GetComponent<LitBrainsEnemy>() == null : actor.GetComponent<SquadCharacterController>() == null))
            throw new InvalidOperationException("Clash requires the explicit root Animator/event receiver and matching player/Brains actor.");
    }

    private static void ValidateVolume(GameObject actor, CombatLabClashActor volume)
    {
        if (!volume.enabled || !Finite(volume.radius) || volume.radius <= 0 || !Finite(volume.localCenter.x) || !Finite(volume.localCenter.y) || !Finite(volume.localCenter.z) ||
            volume.volumeAnchor != null && !volume.volumeAnchor.IsChildOf(actor.transform))
            throw new InvalidOperationException(actor.name + ": invalid authored clash geometry or external volume anchor; no automatic repair.");
    }

    private static void ValidateGeometry(CombatLabClashController config, bool enemy)
    {
        if (config == null) throw new InvalidOperationException("Missing clash geometry source; no default geometry inferred.");
        Vector3 center = enemy ? config.enemyCenter : config.playerCenter;
        float radius = enemy ? config.enemyRadius : config.playerRadius;
        if (!Finite(radius) || radius <= 0 || !Finite(center.x) || !Finite(center.y) || !Finite(center.z))
            throw new InvalidOperationException("Invalid clash geometry in the explicit scene configuration.");
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public sealed class ConfigurationScope : IDisposable
    {
        private Scene scene;
        public CombatLabClashController Config { get; }
        public ConfigurationScope()
        {
            scene = EditorSceneManager.OpenPreviewScene(IluviliraeSetup.ScenePath);
            try
            {
                var configs = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CombatLabClashController>(true)).ToArray();
                if (configs.Length != 1) throw new InvalidOperationException("The authored laboratory must contain exactly one clash configuration.");
                Config = configs[0];
            }
            catch { Dispose(); throw; }
        }
        public void Dispose() { if (scene.IsValid()) { EditorSceneManager.ClosePreviewScene(scene); scene = default; } }
    }
}

/// <summary>Explicit, bounded authoring migration. Never invoked on import or during Play Mode.</summary>
public static class ProductionCombatMigration
{
    public const string SessionPath = "Assets/Core/System/GameplaySessionRoot.prefab";
    public const string LabPath = "Assets/Characters/3_Enemy/Iluvilirae/Iluvilirae_Test.unity";
    public const string ReportDirectory = "Library/ProductionCombatMigration";
    private const string CatalogPath = "Assets/Resources/CombatSkillKits/Default.asset";
    public const string ControllerDirectory = "Assets/CombatRealTime/Migration/Controllers";
    private static readonly string[] Roster = { "Lucian", "Vesta", "Soren", "Aurell" };
    private static readonly string[] Services = {
        "RealTimeCombatManager", "RealTimeCombatInput", "CombatMobilityController", "SkillsManager",
        "CounterSkillCombatController", "CombatCinematicPlaybackService", "LightSkillCombatController",
        "CombatLockOnCameraController", "CombatImpactFeedbackController", "CombatHealthThresholdController",
        "CombatSkillCinematicController", "CombatSkillKitInput", "CombatLabClashController", "CombatWorldRuntime"
    };

    [Serializable] public sealed class Entry
    {
        public string category, path, guid, detail;
        public Entry(string category, string path, string detail)
        {
            this.category = category; this.path = path; this.detail = detail;
            guid = string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
        }
    }

    [Serializable] public sealed class Report
    {
        public bool applied;
        public bool succeeded;
        public bool authoringPlanEvaluated;
        public string scope = "full-inventory-and-readiness";
        // Authoring success is not runtime/online readiness.
        public bool ReadyForProduction;
        public CombatReadinessEvidence.Readiness readiness;
        public ProductionCombatAudit.Cost auditCost;
        public ProductionCombatAudit.Cost postApplyAuditCost;
        public List<string> enemySubjects = new List<string>();
        public List<string> zoneSubjects = new List<string>();
        public List<string> inventorySubjects = new List<string>();
        public List<string> saveSubjects = new List<string>();
        public List<string> networkSubjects = new List<string>();
        public List<Entry> blockers = new List<Entry>();
        public List<string> errors = new List<string>();
        public List<string> changedAssets = new List<string>();
        public List<Entry> entries = new List<Entry>();
    }

    private sealed class Pass
    {
        public readonly bool apply;
        public readonly ProductionCombatAudit.Session audit;
        public readonly Report report = new Report();
        public Pass(bool apply, ProductionCombatAudit.Session audit = null, bool authoringOnly = false)
        {
            this.apply = apply; report.applied = apply;
            this.audit = authoringOnly ? null : audit ?? new ProductionCombatAudit.Session();
        }
        public bool Change(string path, string detail)
        {
            report.entries.Add(new Entry("change", path, detail));
            if (!report.changedAssets.Contains(path)) report.changedAssets.Add(path);
            return apply;
        }
        public void Note(string category, string path, string detail) => report.entries.Add(new Entry(category, path, detail));
    }

    [MenuItem("Lit/Combat/Production Migration/Dry Run")]
    public static void DryRun() => Run(false);

    [MenuItem("Lit/Combat/Production Migration/Apply")]
    public static void Apply()
    {
        if (EditorUtility.DisplayDialog("Production combat migration",
            "Run preflight, then save only the reported roster, lab profiles, lab scene and session prefab? " +
            "Enemy patterns and zone assets will not be replaced. Use an isolated checkout first.", "Apply", "Cancel")) Run(true);
    }

    // -executeMethod ProductionCombatMigration.RunBatch is read-only unless this exact opt-in is supplied.
    public static void RunBatch()
    {
        bool apply = Environment.GetCommandLineArgs().Contains("-applyProductionCombatMigration");
        try { Run(apply); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    public static Report Run(bool apply)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Stop Play Mode and wait for compilation before migration.");
        var audit = new ProductionCombatAudit.Session();
        audit.CaptureSnapshot();
        var preflight = Execute(false, audit);
        if (!audit.VerifyUnchanged())
        {
            preflight.errors.Add("Source/asset/import state changed during read-only preflight; evidence invalidated. Finish fresh sync/import, then retry with a new session. Do not apply or copy back this run.");
            preflight.succeeded = false;
            preflight.ReadyForProduction = false;
            if (preflight.readiness != null) preflight.readiness.ReadyForProduction = false;
        }
        WriteReport(preflight, "dry-run.json");
        if (preflight.errors.Count != 0)
            throw new InvalidOperationException("Migration blocked; see " + ReportDirectory + "/dry-run.json\n" + string.Join("\n", preflight.errors));
        // Reuse the verified preflight graph/read cache; post-write evidence gets a fresh session below.
        var result = apply ? Execute(true, audit) : preflight;
        WriteReport(result, apply ? "apply.json" : "dry-run.json");
        Debug.Log("[ProductionCombatMigration] " + (apply ? "Applied" : "Proposed") + " staged authoring asset writes: " +
            result.changedAssets.Count + "; ReadyForProduction=" + result.ReadyForProduction + "; blockers=" + result.blockers.Count +
            ". Report: " + ReportDirectory + (apply ? "/apply.json" : "/dry-run.json"));
        if (result.errors.Count != 0) throw new InvalidOperationException(string.Join("\n", result.errors));
        return result;
    }

    // Deliberately does not produce inventory/readiness evidence. Full Run/AuditReadiness remain unchanged.
    public static Report RunAuthoringInstall(bool apply)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Authoring installation requires stable imported scripts outside Play Mode.");
        var plan = Execute(false, authoringOnly: true);
        if (plan.errors.Count != 0) throw new InvalidOperationException(string.Join("\n", plan.errors));
        var result = apply ? Execute(true, authoringOnly: true) : plan;
        WriteReport(result, apply ? "implementation-common-apply.json" : "implementation-common-plan.json");
        if (result.errors.Count != 0) throw new InvalidOperationException(string.Join("\n", result.errors));
        return result;
    }

    private static Report Execute(bool apply, ProductionCombatAudit.Session audit = null, bool authoringOnly = false)
    {
        var pass = new Pass(apply, audit, authoringOnly);
        if (authoringOnly) pass.report.scope = "approved-authoring-only; inventory/readiness not evaluated";
        Scene lab = SceneManager.GetSceneByPath(LabPath);
        bool opened = !lab.IsValid() || !lab.isLoaded;
        try
        {
            if (!opened && lab.isDirty) throw new InvalidOperationException("Save or close the dirty lab scene first.");
            if (opened) lab = EditorSceneManager.OpenScene(LabPath, OpenSceneMode.Additive);
            var arenas = lab.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LucianBrainsCombatArena>(true)).ToArray();
            if (arenas.Length != 1) throw new InvalidOperationException("Expected exactly one LucianBrainsCombatArena.");
            var arena = arenas[0];
            if (!authoringOnly) Inventory(pass);
            ValidateSources(arena);
            MigrateSession(pass, arena);
            MigrateRoster(pass, arena);
            MigrateEnemyProfiles(pass, arena);
            MigrateClashFixtures(pass, arena);
            int changes = pass.report.entries.Count;
            ConfigureWorld(pass, arena.gameObject, arena.arenaCamera, arena, LabPath);
            if (apply && pass.report.entries.Count != changes)
            {
                EditorSceneManager.MarkSceneDirty(lab);
                if (!EditorSceneManager.SaveScene(lab)) throw new IOException("Could not save " + LabPath);
            }
            pass.report.succeeded = true;
            pass.report.authoringPlanEvaluated = true;
        }
        catch (Exception exception)
        {
            pass.report.errors.Add(exception.ToString());
            pass.report.succeeded = false;
        }
        finally
        {
            if (opened && lab.IsValid() && lab.isLoaded) EditorSceneManager.CloseScene(lab, true);
        }
        pass.report.changedAssets.Sort(StringComparer.Ordinal);
        if (authoringOnly) return pass.report;
        var evidenceAudit = apply && pass.report.changedAssets.Count > 0 ? new ProductionCombatAudit.Session() : pass.audit;
        ProductionCombatEvidenceImport.Evaluate(pass.report, evidenceAudit);
        pass.report.auditCost = pass.audit.Costs();
        if (evidenceAudit != pass.audit) pass.report.postApplyAuditCost = evidenceAudit.Costs();
        return pass.report;
    }

    // Inventory/readiness only: does not open the lab or plan/apply any authoring changes.
    [MenuItem("Lit/Combat/Production Migration/Audit Readiness")]
    public static void AuditReadiness()
    {
        WriteReport(BuildReadinessReport(), "readiness.json");
    }

    public static Report BuildReadinessReport()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Audit readiness outside Play Mode/compilation.");
        var pass = new Pass(false);
        try { Inventory(pass); pass.report.succeeded = true; }
        catch (Exception exception) { pass.report.errors.Add(exception.ToString()); }
        ProductionCombatEvidenceImport.Evaluate(pass.report, pass.audit);
        return pass.report;
    }

    private static void ValidateSources(LucianBrainsCombatArena arena)
    {
        foreach (string name in Services) RequireType(name);
        var world = RequireType("CombatWorldRuntime");
        if (world.GetField("freeAttacks")?.FieldType != typeof(FreeAttackProfile) || world.GetField("gameplayCamera")?.FieldType != typeof(Camera))
            throw new InvalidOperationException("Parent API required: public FreeAttackProfile freeAttacks and public Camera gameplayCamera.");
        if (typeof(LitBrainsAttackProfile).GetField("skill")?.FieldType != typeof(SkillSO))
            throw new InvalidOperationException("Parent API required: LitBrainsAttackProfile.public SkillSO skill.");
        Require(arena.arenaCamera, "Lab arenaCamera");
        if (arena.freeAttacks == null) throw new InvalidOperationException("Lab freeAttacks missing.");
        if (arena.basicSkills == null || arena.basicSkills.Length != 3 || arena.basicSkills.Any(skill => skill == null))
            throw new InvalidOperationException("Expected the three exact lab ground BasicSkills.");
        var catalog = Load<CombatSkillKitCatalog>(CatalogPath);
        foreach (string name in Roster)
        {
            var kit = (CombatSkillKit)Enum.Parse(typeof(CombatSkillKit), name);
            var data = Load<CharacterData>(DataPath(name));
            if (data.isEnemy || data.UniqueId != AssetDatabase.AssetPathToGUID(DataPath(name)))
                throw new InvalidOperationException("Refusing identity repair or enemy roster data: " + DataPath(name));
            if (catalog.GetCharacter(kit) != data || catalog.GetSkills(kit)?.Length != 4 || catalog.GetSkills(kit).Any(skill => skill == null))
                throw new InvalidOperationException("Catalog identity/four authored slots missing for " + name);
            var source = PlayerSource(arena, name);
            Require(source.GetComponent<Animator>()?.runtimeAnimatorController, name + " lab controller");
            Require(source.GetComponent<CharacterInfo>()?.SourceData, name + " lab CharacterInfo");
            Require(data.worldPrefab, name + " production prefab");
            foreach (var skill in catalog.GetSkills(kit))
                if (!SkillTargetingContract.Validate(skill, out string reason)) throw new InvalidOperationException(reason);
        }
        if (arena.enemyAttackSkills == null || arena.enemyAttackSkills.Length != 3 || arena.enemyAttackSkills.Any(skill => skill == null))
            throw new InvalidOperationException("Expected three authored lab enemyAttackSkills.");
    }

    private static void MigrateSession(Pass pass, LucianBrainsCombatArena arena)
    {
        EditPrefab(pass, SessionPath, root =>
        {
            var battle = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "BattleManager").ToArray();
            if (battle.Length != 1) throw new InvalidOperationException("Expected one BattleManager in session prefab.");
            var cameraMode = root.GetComponentInChildren<LitGameplayCameraModeController>(true);
            var camera = cameraMode != null ? cameraMode.GetComponent<Camera>() : null;
            Require(camera, "Production gameplay camera");
            ConfigureWorld(pass, battle[0].gameObject, camera, arena, SessionPath);
        });
    }

    private static void ConfigureWorld(Pass pass, GameObject host, Camera camera, LucianBrainsCombatArena arena, string path)
    {
        foreach (string service in Services) Ensure(pass, host, service, path);
        var world = Get(host, "CombatWorldRuntime");
        if (world != null)
        {
            Copy(pass, world, arena, "freeAttacks", path);
            Reference(pass, world, "gameplayCamera", camera, path);
        }
        else pass.Note("binding", path, "New CombatWorldRuntime receives arena.freeAttacks and local gameplayCamera on apply.");
        var manager = Get(host, "RealTimeCombatManager");
        var input = Get(host, "RealTimeCombatInput");
        var mobility = Get(host, "CombatMobilityController");
        foreach (string service in new[] { "CounterSkillCombatController", "LightSkillCombatController", "CombatSkillCinematicController" })
        {
            var component = Get(host, service);
            if (component == null) continue;
            Reference(pass, component, "combatManager", manager, path);
            Reference(pass, component, "cinematicPlayback", Get(host, "CombatCinematicPlaybackService"), path);
            if (service != "CounterSkillCombatController") Reference(pass, component, "combatInput", input, path);
        }
        if (manager != null)
        {
            Reference(pass, manager, "combatInput", input, path);
            Reference(pass, manager, "playerMobility", mobility, path);
            Reference(pass, manager, "combatSkillCinematicController", Get(host, "CombatSkillCinematicController"), path);
            Reference(pass, manager, "combatHealthThresholdController", Get(host, "CombatHealthThresholdController"), path);
        }
        if (mobility != null) Reference(pass, mobility, "combatInput", input, path);
        var counter = Get(host, "CounterSkillCombatController");
        var labCounter = arena.GetComponent<CounterSkillCombatController>();
        Require(labCounter, "Lab counter controller");
        if (counter != null)
            foreach (string field in new[] { "defaultCounterSkill", "availableSkills" }) Copy(pass, counter, labCounter, field, path);
        var clash = Get(host, "CombatLabClashController");
        var labClash = arena.GetComponent<CombatLabClashController>();
        Require(labClash, "Lab clash controller");
        if (clash != null)
            foreach (string field in new[] { "enableClashes", "compatiblePlayerSkills", "recoilDistance",
                "recoilSeconds", "recoverySeconds", "hitVfx", "contactAudio", "diagnostics", "playerClips", "enemyClips",
                "labEnemyProfile", "authoringVersion", "playerCenter", "enemyCenter", "playerRadius", "enemyRadius" })
                Copy(pass, clash, labClash, field, path);
        if (clash != null)
        {
            var data = Load<CharacterData>("Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset");
            var conversion = ProductionBrainsEnemyConversion.Analyze(data);
            var candidate = ProductionBrainsEnemyConversion.CreateInspectionCandidate(conversion);
            try
            {
                string[] aliases = (labClash.compatibleEnemyStates ?? Array.Empty<string>())
                    .Concat(CombatClashAuthoring.RequiredEnemyStateAliases(labClash, candidate)).Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var serialized = new SerializedObject(clash);
                var states = serialized.FindProperty("compatibleEnemyStates");
                bool same = states.arraySize == aliases.Length;
                for (int i = 0; same && i < aliases.Length; i++) same &= states.GetArrayElementAtIndex(i).stringValue == aliases[i];
                if (!same && pass.Change(path, "Clash: reviewed laboratory and production state aliases"))
                {
                    states.arraySize = aliases.Length;
                    for (int i = 0; i < aliases.Length; i++) states.GetArrayElementAtIndex(i).stringValue = aliases[i];
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            finally { Object.DestroyImmediate(candidate); }
        }
        // Lab scene object references must never leak into the persistent prefab.
        var lockCamera = Get(host, "CombatLockOnCameraController");
        if (lockCamera != null) Reference(pass, lockCamera, "controlledCamera", camera, path);
    }

    private static void MigrateClashFixtures(Pass pass, LucianBrainsCombatArena arena)
    {
        var config = arena.GetComponent<CombatLabClashController>();
        foreach (var prefab in new[] { arena.lucianPrefab, arena.vestaPrefab, arena.sorenPrefab, arena.aurellPrefab, arena.enemyPrefab }.Distinct())
        {
            Require(prefab, "Authored clash fixture prefab");
            bool enemy = prefab == arena.enemyPrefab;
            if (!CombatClashAuthoring.NeedsChanges(prefab, config, enemy)) continue;
            string path = AssetDatabase.GetAssetPath(prefab);
            EditPrefab(pass, path, root =>
            {
                if (pass.Change(path, "Add explicit laboratory clash fixture volume")) CombatClashAuthoring.Ensure(root, config, enemy);
            });
        }
    }

    private static void MigrateRoster(Pass pass, LucianBrainsCombatArena arena)
    {
        var catalog = Load<CombatSkillKitCatalog>(CatalogPath);
        foreach (string name in Roster)
        {
            string path = DataPath(name);
            var target = Load<CharacterData>(path);
            string id = target.UniqueId, characterId = target.characterId, guid = AssetDatabase.AssetPathToGUID(path);
            var sourcePrefab = PlayerSource(arena, name);
            var source = sourcePrefab.GetComponent<CharacterInfo>().SourceData;
            var controller = ConfigureController(pass, arena, sourcePrefab, name);
            int count = pass.report.entries.Count;
            References(pass, target, "groundBasicSkills", arena.basicSkills.Cast<Object>().ToArray(), path);
            Copy(pass, target, source, "airBasicSkills", path);
            Copy(pass, target, source, "chargedSkill", path);
            var kit = (CombatSkillKit)Enum.Parse(typeof(CombatSkillKit), name);
            References(pass, target, "combatSkills", catalog.GetSkills(kit).Cast<Object>().ToArray(), path);
            if (pass.apply && pass.report.entries.Skip(count).Any(entry => entry.category == "change")) AssetDatabase.SaveAssetIfDirty(target);
            if (target.UniqueId != id || target.characterId != characterId || AssetDatabase.AssetPathToGUID(path) != guid)
                throw new InvalidOperationException("Identity changed unexpectedly: " + path);
            string prefabPath = AssetDatabase.GetAssetPath(target.worldPrefab);
            EditPrefab(pass, prefabPath, root =>
            {
                var animator = root.GetComponent<Animator>();
                Require(animator, name + " root Animator");
                if (controller != null) Reference(pass, animator, "m_Controller", controller, prefabPath);
                else pass.Change(prefabPath, "Animator.m_Controller -> " + ControllerDirectory + "/" + name + ".overrideController");
                foreach (string component in new[] { "CharacterInfo", "SquadCharacterController", "LitOpsiveLocomotionBridge", "PlayerScriptedDodgeController", "PlayerScriptedJumpController", "PlayerStateMotionController", "AnimationEvents" })
                    Require(Get(root, component), prefabPath + " required root " + component);
                foreach (string component in new[] { "PlayerActionPresentationController", "CombatTimeDomain", "NetworkCharacterInput", "PlayerProtectionEffects", "PlayerGuardState" })
                    Ensure(pass, root, component, prefabPath);
                var clashConfig = arena.GetComponent<CombatLabClashController>();
                if (CombatClashAuthoring.NeedsChanges(root, clashConfig, false) && pass.Change(prefabPath, "Add authored player clash volume"))
                    CombatClashAuthoring.Ensure(root, clashConfig, false);
                // Solo Lucian executes borrowed kits on his own actor, not hidden support actors.
                string[] characterModules = name == "Lucian" ? new[] { "PlayerRegenerationEffect", "PlayerControlSkills", "PlayerJusticeStrike" } :
                    name == "Aurell" ? new[] { "PlayerJusticeStrike" } :
                    name == "Soren" ? new[] { "PlayerControlSkills" } : name == "Vesta" ? new[] { "PlayerRegenerationEffect" } : Array.Empty<string>();
                foreach (string component in characterModules) Ensure(pass, root, component, prefabPath);
                if (arena.alignPlayerCombatTorso) Ensure(pass, root, "LucianCombatTorsoFacing", prefabPath);
                Reference(pass, Get(root, "CharacterInfo"), "characterData", target, prefabPath);
                Reference(pass, Get(root, "SquadCharacterController"), "characterData", target, prefabPath);
                var presentation = Get(root, "PlayerActionPresentationController");
                if (presentation != null) Reference(pass, presentation, "animator", animator, prefabPath);
            });
            pass.Note("source", path, "Identity/worldPrefab/stats/inventory preserved; combat source=" + AssetDatabase.GetAssetPath(source) +
                "; controller=" + AssetDatabase.GetAssetPath(sourcePrefab.GetComponent<Animator>().runtimeAnimatorController));
        }
    }

    private static RuntimeAnimatorController ConfigureController(Pass pass, LucianBrainsCombatArena arena, GameObject source, string name)
    {
        string path = ControllerDirectory + "/" + name + ".overrideController";
        var baseController = source.GetComponent<Animator>().runtimeAnimatorController;
        // A later run may read one of our own controllers from a shared roster prefab.
        while (baseController is AnimatorOverrideController generated &&
            AssetDatabase.GetAssetPath(baseController).StartsWith(ControllerDirectory + "/", StringComparison.Ordinal))
            baseController = generated.runtimeAnimatorController;
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (existing == null && AssetDatabase.LoadMainAssetAtPath(path) != null)
            throw new InvalidOperationException("Refusing to replace a non-override asset: " + path);
        var working = new AnimatorOverrideController(baseController);
        try
        {
            var mappings = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            working.GetOverrides(mappings);
            var clash = arena.GetComponent<CombatLabClashController>();
            if (clash != null)
                foreach (var replacement in clash.playerClips ?? Array.Empty<CombatLabClashController.ClipReplacement>())
                {
                    Require(replacement.source, "Lab controller source clip");
                    Require(replacement.replacement, "Lab controller replacement clip");
                    int index = mappings.FindIndex(pair => pair.Key == replacement.source);
                    if (index < 0) throw new InvalidOperationException("Missing exact lab override slot in " + name + ": " + replacement.source.name);
                    mappings[index] = new KeyValuePair<AnimationClip, AnimationClip>(replacement.source, replacement.replacement);
                }
            working.ApplyOverrides(mappings);
            bool equal = existing != null && existing.runtimeAnimatorController == baseController;
            if (equal)
            {
                var current = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                existing.GetOverrides(current);
                equal = current.Count == mappings.Count && current.All(pair => mappings.Any(next => next.Key == pair.Key && next.Value == pair.Value));
            }
            if (!equal && pass.Change(path, "Exact lab controller + CombatLabClashController.playerClips; no base controller/state edits"))
            {
                if (!AssetDatabase.IsValidFolder(ControllerDirectory)) AssetDatabase.CreateFolder("Assets/CombatRealTime/Migration", "Controllers");
                if (existing == null)
                {
                    existing = new AnimatorOverrideController(baseController) { name = name + "_ProductionCombat" };
                    AssetDatabase.CreateAsset(existing, path);
                }
                else existing.runtimeAnimatorController = baseController;
                existing.ApplyOverrides(mappings);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
            }
            pass.Note("controller-source", path, "base=" + AssetDatabase.GetAssetPath(baseController));
            return existing;
        }
        finally { Object.DestroyImmediate(working); }
    }

    private static void MigrateEnemyProfiles(Pass pass, LucianBrainsCombatArena arena)
    {
        var brain = arena.enemyPrefab != null ? arena.enemyPrefab.GetComponent<LitBrainsEnemy>() : null;
        Require(brain, "Lab LitBrainsEnemy");
        var profiles = new HashSet<LitBrainsCombatProfile>();
        if (brain.combatProfile != null) profiles.Add(brain.combatProfile);
        var clash = arena.GetComponent<CombatLabClashController>();
        if (clash != null && clash.labEnemyProfile != null) profiles.Add(clash.labEnemyProfile);
        if (profiles.Count == 0) throw new InvalidOperationException("No authored lab enemy profiles.");
        var expected = new Dictionary<string, SkillSO> {
            { "Attack", arena.enemyAttackSkills[0] }, { "Attack_Sweep", arena.enemyAttackSkills[1] }, { "Attack_Followup", arena.enemyAttackSkills[2] }
        };
        string[] expectedSkillNames = { "Juggernaut_Strike", "Juggernaut_Sweep", "Juggernaut_Followup" };
        for (int i = 0; i < expectedSkillNames.Length; i++)
            if (arena.enemyAttackSkills[i].SkillName != expectedSkillNames[i])
                throw new InvalidOperationException("Lab enemy skill order changed; refusing positional inference.");
        foreach (var profile in profiles)
        {
            string path = AssetDatabase.GetAssetPath(profile);
            var serialized = new SerializedObject(profile);
            var attacks = Property(serialized, "attacks");
            if (attacks.arraySize != 3) throw new InvalidOperationException("Unexpected authored attack count: " + path);
            var states = new HashSet<string>();
            bool dirty = false;
            for (int i = 0; i < attacks.arraySize; i++)
            {
                var attack = attacks.GetArrayElementAtIndex(i);
                string state = attack.FindPropertyRelative("stateName").stringValue;
                if (!states.Add(state) || !expected.TryGetValue(state, out var skill))
                    throw new InvalidOperationException("Unmapped/duplicate enemy state " + state + " in " + path);
                var field = attack.FindPropertyRelative("skill");
                if (field == null) throw new InvalidOperationException("Missing parent LitBrainsAttackProfile.skill API.");
                if (field.objectReferenceValue == skill) continue;
                if (field.objectReferenceValue != null)
                    throw new InvalidOperationException("Refusing to replace an existing authored enemy skill link: " + path + " / " + state);
                if (pass.Change(path, "attacks[" + i + "].skill: " + state + " -> " + AssetDatabase.GetAssetPath(skill)))
                { field.objectReferenceValue = skill; dirty = true; }
            }
            if (dirty) { serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(profile); }
            pass.Note("preserved", path, "Only skill references change; identity, clips, phase markers, reach, cadence, resistance and advances remain authored.");
        }
    }

    private static void Inventory(Pass pass)
    {
        var enemyAssets = new HashSet<string>();
        int unreachableCharacters = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterData", new[] { "Assets" }).OrderBy(value => value))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!pass.audit.Graph.Reachable.Contains(path)) { unreachableCharacters++; continue; }
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data == null) continue;
            pass.report.saveSubjects.Add("character:" + guid + ":" + data.UniqueId + ":" + data.characterId);
            string prefabPath = AssetDatabase.GetAssetPath(data.worldPrefab);
            string detail = "id=" + data.characterId + "; uniqueId=" + data.UniqueId + "; prefab=" + prefabPath;
            if (!data.isEnemy)
            {
                pass.audit.AuditPlayerSkillMetadata(pass.report, data);
                pass.Note("player-inventory", path, detail + "; combatSkills=" + (data.combatSkills?.Count ?? 0) +
                    (Roster.Contains(data.name) ? "; supported roster" : "; not a roster migration target"));
                continue;
            }
            enemyAssets.Add(path);
            pass.report.enemySubjects.Add("enemy:" + guid);
            if (!string.IsNullOrEmpty(prefabPath)) enemyAssets.Add(prefabPath);
            var prefab = data.worldPrefab;
            var brain = prefab != null ? prefab.GetComponent<LitBrainsEnemy>() : null;
            var legacy = prefab != null ? prefab.GetComponent<EnemyController>() : null;
            string blocker = prefab == null ? "missing worldPrefab" : brain == null ?
                (legacy != null ? "EnemyController legacy state/EnemyCombatProfileSO: no authored LitBrainsCombatProfile or Brains phase-event mapping; retain existing pattern" :
                    "no root LitBrainsEnemy or EnemyController; bespoke enemy requires explicit adapter") :
                brain.combatProfile == null ? "LitBrainsEnemy has no authored combatProfile" :
                "Brains profile=" + AssetDatabase.GetAssetPath(brain.combatProfile) + "; validate attack skill bindings/phase events before enabling";
            pass.Note("enemy-inventory", path, detail + "; legacyProfile=" + AssetDatabase.GetAssetPath(data.enemyCombatProfile) +
                "; thresholds=" + data.enableCombatHealthThresholds + "; " + blocker);
            if (brain == null || brain.combatProfile == null)
            {
                if (brain == null && legacy != null && ProductionEncounterMigration.TryValidateAuthoredEncounter(data, out string encounterReason))
                {
                    pass.Note("supported-specialized-encounter", path, encounterReason);
                    continue;
                }
                string contract = data.characterId == "mad_scientist_nina" ?
                    "GhostController/IGhostInteractionHandler, dormant-introduction-active and knowledge gates, delayed death presentation require an encounter adapter." :
                    data.characterId == "dead_weight_etienne" ?
                    "DeadWeightBoss/BossEncounterBehaviour engagement/authority, pre-engagement damage filter and owned emergency-brake suspension/cooldown must survive." :
                    data.characterId == "false_choir_ardent" ?
                    "FalseChoirBoss requires three tuned echo relays, party arena gate, exposed-only damage, reveal/relay cooldowns, decoys and authority." :
                    data.characterId == "broken_anchor_belmont" ?
                    "BrokenAnchorBoss torch/projectile/release sequence and resolution are bespoke; enemyCombatProfile is null." :
                    "Retain existing encounter/death/threshold and authored pattern contracts; no generic Juggernaut replacement is permitted.";
                pass.report.blockers.Add(new Entry("unsupported-enemy", path, blocker + "; " + contract));
            }
            else
            {
                var serialized = new SerializedObject(brain.combatProfile);
                var attacks = serialized.FindProperty("attacks");
                var states = new HashSet<string>(StringComparer.Ordinal);
                bool mapped = attacks != null && attacks.arraySize > 0;
                for (int i = 0; attacks != null && i < attacks.arraySize; i++)
                {
                    var attack = attacks.GetArrayElementAtIndex(i);
                    string state = attack.FindPropertyRelative("stateName")?.stringValue;
                    mapped &= !string.IsNullOrWhiteSpace(state) && states.Add(state) &&
                        attack.FindPropertyRelative("clip")?.objectReferenceValue != null &&
                        attack.FindPropertyRelative("skill")?.objectReferenceValue != null;
                }
                if (!mapped) pass.report.blockers.Add(new Entry("incomplete-enemy-mapping", path,
                    "Every authored Brains state needs a unique stateName, clip and explicit SkillSO; no fallback mapping inferred."));
            }
        }
        if (unreachableCharacters > 0) pass.Note("warning:unreachable-character-definitions", "Assets", unreachableCharacters + " non-gameplay CharacterData definitions omitted from enemy/save gates; not loaded or converted.");
        foreach (string path in pass.audit.Graph.Reachable.Where(path => path.EndsWith(".unity", StringComparison.Ordinal)).OrderBy(value => value, StringComparer.Ordinal))
        {
            string[] references = pass.audit.Graph.Direct(path).Where(enemyAssets.Contains).ToArray();
            if (references.Length > 0) pass.Note("zone-enemy-dependencies", path, string.Join("; ", references));
        }
        pass.audit.Collect(pass.report);
    }

    private static void EditPrefab(Pass pass, string path, Action<GameObject> edit)
    {
        Load<GameObject>(path);
        string guid = AssetDatabase.AssetPathToGUID(path);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int changes = pass.report.entries.Count;
            edit(root);
            bool changed = pass.report.entries.Skip(changes).Any(entry => entry.category == "change");
            if (pass.apply && changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success) throw new IOException("Could not save " + path);
            }
            if (AssetDatabase.AssetPathToGUID(path) != guid) throw new InvalidOperationException("Prefab GUID changed: " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void Ensure(Pass pass, GameObject host, string name, string path)
    {
        Type type = RequireType(name);
        var components = host.GetComponents(type);
        if (components.Length > 1) throw new InvalidOperationException("Duplicate " + name + " on " + host.name);
        if (components.Length == 0)
        {
            if (pass.Change(path, host.name + ": add " + name)) host.AddComponent(type);
        }
        else if (components[0] is Behaviour behaviour && !behaviour.enabled)
        {
            if (pass.Change(path, host.name + ": enable " + name)) behaviour.enabled = true;
        }
    }

    private static void Reference(Pass pass, Object target, string field, Object value, string path)
    {
        var serialized = new SerializedObject(target);
        var property = Property(serialized, field);
        if (property.objectReferenceValue == value) return;
        if (!pass.Change(path, target.GetType().Name + "." + field + " -> " + (value != null ? value.name : "null"))) return;
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void References(Pass pass, Object target, string field, Object[] values, string path)
    {
        var serialized = new SerializedObject(target);
        var property = Property(serialized, field);
        if (property.arraySize == values.Length && Enumerable.Range(0, values.Length).All(i => property.GetArrayElementAtIndex(i).objectReferenceValue == values[i])) return;
        if (!pass.Change(path, field + " -> " + string.Join(", ", values.Select(AssetDatabase.GetAssetPath)))) return;
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Copy(Pass pass, Object target, Object source, string field, string path)
    {
        var from = new SerializedObject(source);
        var to = new SerializedObject(target);
        var sourceProperty = Property(from, field);
        if (SerializedProperty.DataEquals(sourceProperty, Property(to, field))) return;
        if (!pass.Change(path, target.GetType().Name + "." + field + " <- " + source.name)) return;
        to.CopyFromSerializedProperty(sourceProperty);
        to.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SerializedProperty Property(SerializedObject target, string field) => target.FindProperty(field) ??
        throw new InvalidOperationException(target.targetObject.GetType().Name + "." + field + " serialized API missing.");
    private static Type RequireType(string name)
    {
        var types = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().Where(type => type.Name == name && !type.IsAbstract).ToArray();
        if (types.Length != 1) throw new InvalidOperationException("Expected one runtime component type: " + name);
        return types[0];
    }
    private static Component Get(GameObject host, string name) => host.GetComponent(RequireType(name));
    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ??
        throw new InvalidOperationException("Missing " + typeof(T).Name + ": " + path);
    private static void Require(Object value, string label)
    {
        if (value == null) throw new InvalidOperationException("Missing " + label);
    }
    private static string DataPath(string name) => "Assets/Characters/1_Squad/" + name + "/" + name + ".asset";
    private static GameObject PlayerSource(LucianBrainsCombatArena arena, string name)
    {
        var prefab = name == "Lucian" ? arena.lucianPrefab : name == "Vesta" ? arena.vestaPrefab : name == "Soren" ? arena.sorenPrefab : arena.aurellPrefab;
        Require(prefab, name + " lab prefab");
        return prefab;
    }
    private static void WriteReport(Report report, string name)
    {
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(Path.Combine(ReportDirectory, name), JsonUtility.ToJson(report, true));
    }
}
