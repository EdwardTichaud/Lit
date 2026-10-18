using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lit.Combat.Migration.Network;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit authoring only. NetworkBehaviours must exist before NGO spawns a prefab.</summary>
public static class CombatNetworkSetup
{
    public const string CatalogPath = "Assets/Resources/CombatNetwork/Default.asset";
    [Serializable] public sealed class Report
    {
        public bool applied, succeeded;
        public string runId = Guid.NewGuid().ToString("N");
        public string scope;
        public List<string> changedAssets = new List<string>();
        public List<string> savedAssets = new List<string>();
        public List<string> errors = new List<string>();
        public List<string> blockers = new List<string>();
        public List<string> unsupportedSkills = new List<string>();
        public List<string> deferredChecks = new List<string>();
    }

    [MenuItem("Lit/Combat/Production Migration/Network Authoring Dry Run")]
    public static void DryRun() => Run(false);

    [MenuItem("Lit/Combat/Production Migration/Apply Network Authoring")]
    public static void Apply()
    {
        if (EditorUtility.DisplayDialog("Combat network authoring",
            "Install actor network adapters and their skill catalog on existing roster/enemy prefabs? This does not validate multiplayer gameplay. Review the dry-run first.", "Apply", "Cancel")) Run(true);
    }

    public static Report Run(bool apply, bool commonAuthoringPlanned = false, bool approvedManifestOnly = false)
    {
        if (apply && commonAuthoringPlanned)
            throw new InvalidOperationException("Planned common authoring is only admissible in a preflight, never in Apply.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Author network components outside Play Mode and compilation.");
        var skills = CollectSkills();
        var prefabs = CollectPrefabs(approvedManifestOnly);
        var report = Inspect(skills, prefabs);
        report.scope = approvedManifestOnly ? "approved-encounter-manifest-only; full reachability not evaluated" : "full-gameplay-reachability";
        var cinematicSettings = ReadCinematicSettings(out string cinematicError);
        if (cinematicSettings == null)
        {
            if (commonAuthoringPlanned)
            {
                report.deferredChecks.Add(cinematicError + " Re-evaluate after the reviewed common authoring Apply; no runtime defaults accepted.");
                if (!report.changedAssets.Contains(CatalogPath)) report.changedAssets.Add(CatalogPath);
            }
            else report.blockers.Add(cinematicError);
        }
        report.applied = apply;
        var watched = report.changedAssets.SelectMany(path => new[] { path, path + ".meta" })
            .Concat(new[] { "Assets/Resources/CombatNetwork.meta" }).Distinct(StringComparer.Ordinal).ToArray();
        var before = watched.ToDictionary(path => path, path => File.Exists(path) ? CombatReadinessEvidence.HashFile(path) : null, StringComparer.Ordinal);
        try
        {
            if (apply && report.blockers.Count == 0)
            {
                var catalog = WriteCatalog(skills);
                foreach (var path in prefabs.Keys) WritePrefab(path, catalog, cinematicSettings);
            }
            report.succeeded = report.blockers.Count == 0;
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); report.succeeded = false; }
        foreach (var path in watched)
            if (File.Exists(path) && CombatReadinessEvidence.HashFile(path) != before[path]) report.savedAssets.Add(path);
        Directory.CreateDirectory(ProductionCombatMigration.ReportDirectory);
        File.WriteAllText(ProductionCombatMigration.ReportDirectory + (apply ? "/network-apply.json" : "/network-dry-run.json"), JsonUtility.ToJson(report, true));
        if (apply && !report.succeeded) throw new InvalidOperationException(string.Join("\n", report.blockers.Concat(report.errors)));
        return report;
    }

    public static void RunBatch()
    {
        try
        {
            bool apply = Array.IndexOf(Environment.GetCommandLineArgs(), "-applyNetworkCombatAuthoring") >= 0;
            var report = Run(apply, approvedManifestOnly: Environment.GetCommandLineArgs().Contains("-combatAuthoringManifestOnly"));
            Debug.Log("[CombatNetworkSetup] " + (apply ? "Applied" : "Proposed") + " authoring assets=" + report.changedAssets.Count +
                " blockers=" + report.blockers.Count + " unsupported skills=" + report.unsupportedSkills.Count + ". No multiplayer readiness inferred.");
            EditorApplication.Exit(report.blockers.Count == 0 ? 0 : 1);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static Report Inspect(List<SkillSO> skills, Dictionary<string, bool> prefabs)
    {
        var report = new Report();
        var cinematicSettings = ReadCinematicSettings(out _);
        var catalog = AssetDatabase.LoadAssetAtPath<CombatNetworkSkillCatalog>(CatalogPath);
        if (catalog != null && EditorUtility.IsDirty(catalog)) report.blockers.Add("Save the dirty network catalog before migration: " + CatalogPath);
        if (AssetDatabase.LoadMainAssetAtPath(CatalogPath) != null && catalog == null)
            report.blockers.Add("Catalog path contains another asset type: " + CatalogPath);
        if (skills.Count == 0) report.blockers.Add("No authored roster skills were found.");
        var entries = catalog != null ? new SerializedObject(catalog).FindProperty("entries") : null;
        if (catalog != null && !catalog.Rebuild(out string reason)) report.blockers.Add(reason);
        bool catalogChanged = catalog == null;
        var ids = new Dictionary<int, SkillSO>();
        if (catalog != null)
            foreach (var entry in catalog.Entries)
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.skill));
                if (!string.Equals(entry.CanonicalGuid, guid, StringComparison.OrdinalIgnoreCase))
                    report.blockers.Add("Catalog GUID no longer matches its skill asset: " + AssetDatabase.GetAssetPath(entry.skill));
                if (entry.Id > 0) ids[entry.Id] = entry.skill;
            }
        foreach (var skill in skills)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skill));
            if (!CombatSkillNetworkId.TryGet(guid, out int id) || ids.TryGetValue(id, out var previous) && previous != skill)
                report.blockers.Add("Invalid/colliding stable skill ID: " + AssetDatabase.GetAssetPath(skill));
            else ids[id] = skill;
            bool found = false;
            if (entries != null)
                for (int i = 0; i < entries.arraySize; i++)
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("skill").objectReferenceValue == skill) { found = true; break; }
            catalogChanged |= !found;
            if (CombatNetworkSkillCatalog.Classify(skill, out string unsupported) == CombatNetworkSkillFamily.Unsupported)
                report.unsupportedSkills.Add(AssetDatabase.GetAssetPath(skill) + ": " + unsupported);
        }
        if (catalogChanged) report.changedAssets.Add(CatalogPath);
        if (cinematicSettings != null)
        {
            foreach (var definition in CollectCinematicDefinitions(cinematicSettings))
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(definition));
                if (!CombatSkillNetworkId.TryGet(guid, out int id) || ids.ContainsKey(id))
                    report.blockers.Add("Invalid/colliding cinematic ID: " + AssetDatabase.GetAssetPath(definition));
                if (catalog == null || !catalog.Cinematics.Any(entry => entry.definition == definition && entry.assetGuid == guid))
                    if (!report.changedAssets.Contains(CatalogPath)) report.changedAssets.Add(CatalogPath);
            }
        }
        foreach (var pair in prefabs)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(pair.Key);
            if (root == null) { report.blockers.Add("Missing actor prefab: " + pair.Key); continue; }
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (EditorUtility.IsDirty(root) || stage != null && stage.assetPath == pair.Key && stage.scene.isDirty)
            { report.blockers.Add("Save or close the dirty actor prefab before migration: " + pair.Key); continue; }
            bool missingIdentity = root.GetComponent<NetworkObject>() == null;
            if (missingIdentity && root.GetComponentInChildren<NetworkObject>(true) != null)
            { report.blockers.Add("Nested network identity requires an explicit root migration: " + pair.Key); continue; }
            bool player = pair.Value;
            if (player)
            {
                string playerError = CombatPlayerNetworkAuthoring.Validate(root);
                if (playerError != null) report.blockers.Add(pair.Key + ": " + playerError);
            }
            var legacy = root.GetComponent<EnemyController>();
            var brain = root.GetComponent<LitBrainsEnemy>();
            if (player && (legacy != null || brain != null) || !player && legacy != null && brain != null)
            { report.blockers.Add("Conflicting player/enemy combat authorities: " + pair.Key); continue; }
            if (player && (root.GetComponent<SquadCharacterController>() == null || root.GetComponent<PlayerActionPresentationController>() == null))
            { report.blockers.Add("Migrate the player's common runtime before network authoring: " + pair.Key); continue; }
            if (!player && legacy == null && brain == null)
            { report.blockers.Add("Enemy has no authored combat actor; do not synthesize a brain: " + pair.Key); continue; }
            var animator = ResolveAnimator(root);
            if (!player)
            {
                string presentationError = CombatEnemyNetworkAuthoring.Validate(root, animator);
                if (presentationError != null) report.blockers.Add(pair.Key + ": " + presentationError);
            }
            var runtime = root.GetComponent<CombatNetworkActorRuntime>();
            if (missingIdentity || !player && root.GetComponent<CharacterInfo>() == null || runtime == null || catalog == null || runtime.Catalog != catalog || player &&
                (animator == null || animator.GetComponent<CombatNetworkAnimationEvents>() == null) ||
                player && CombatPlayerNetworkAuthoring.NeedsChanges(root) ||
                player && CinematicActorNeedsChanges(root, cinematicSettings) ||
                !player && CombatEnemyNetworkAuthoring.NeedsChanges(root, animator)) report.changedAssets.Add(pair.Key);
            if (player && animator == null) report.blockers.Add("Player has no authored Animator: " + pair.Key);
        }
        report.changedAssets.Sort(StringComparer.Ordinal);
        report.unsupportedSkills.Sort(StringComparer.Ordinal);
        return report;
    }

    private static Dictionary<string, bool> CollectPrefabs(bool approvedManifestOnly)
    {
        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        var kits = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        if (kits == null) throw new InvalidOperationException("Missing roster catalog.");
        foreach (CombatSkillKit kit in Enum.GetValues(typeof(CombatSkillKit)))
        {
            var character = kits.GetCharacter(kit);
            if (character == null || character.worldPrefab == null) throw new InvalidOperationException("Missing authored roster prefab: " + kit);
            result[AssetDatabase.GetAssetPath(character.worldPrefab)] = true;
        }
        var reachable = approvedManifestOnly ? null : new HashSet<string>(ProductionCombatAudit.GetGameplayReachablePaths(), StringComparer.Ordinal);
        var dataPaths = approvedManifestOnly ? ProductionEncounterMigration.AuthoredEnemyDataPaths.Concat(new[] { "Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset" }) :
            AssetDatabase.FindAssets("t:CharacterData").Select(AssetDatabase.GUIDToAssetPath);
        foreach (string dataPath in dataPaths)
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterData>(dataPath);
            if (character != null && character.isEnemy && character.worldPrefab != null)
            {
                string path = AssetDatabase.GetAssetPath(character.worldPrefab);
                if (!approvedManifestOnly && !reachable.Contains(dataPath) && !reachable.Contains(path)) continue;
                if (!result.ContainsKey(path)) result[path] = false;
            }
        }
        return result;
    }

    private static List<SkillSO> CollectSkills()
    {
        var result = new HashSet<SkillSO>();
        var kits = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        if (kits == null) throw new InvalidOperationException("Missing roster catalog.");
        foreach (CombatSkillKit kit in Enum.GetValues(typeof(CombatSkillKit)))
        {
            var data = kits.GetCharacter(kit);
            if (data == null) continue;
            if (data.groundBasicSkills != null) foreach (var skill in data.groundBasicSkills) if (skill != null) result.Add(skill);
            if (data.airBasicSkills != null) foreach (var skill in data.airBasicSkills) if (skill != null) result.Add(skill);
            if (data.combatSkills != null) foreach (var skill in data.combatSkills) if (skill != null) result.Add(skill);
            if (data.chargedSkill != null) result.Add(data.chargedSkill);
            var wheel = kits.GetSkills(kit);
            if (wheel != null) foreach (var skill in wheel) if (skill != null) result.Add(skill);
        }
        return result.OrderBy(skill => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skill)), StringComparer.Ordinal).ToList();
    }

    private static CombatNetworkSkillCatalog WriteCatalog(List<SkillSO> skills)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/CombatNetwork")) AssetDatabase.CreateFolder("Assets/Resources", "CombatNetwork");
        var catalog = AssetDatabase.LoadAssetAtPath<CombatNetworkSkillCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<CombatNetworkSkillCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        var serialized = new SerializedObject(catalog);
        var entries = serialized.FindProperty("entries");
        foreach (var skill in skills)
        {
            bool found = false;
            for (int i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("skill").objectReferenceValue == skill) { found = true; break; }
            if (found) continue;
            int index = entries.arraySize++;
            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("skill").objectReferenceValue = skill;
            entry.FindPropertyRelative("assetGuid").stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skill));
            entry.FindPropertyRelative("heightTolerance").floatValue = 1.8f;
            entry.FindPropertyRelative("contactArc").floatValue = Mathf.Clamp(skill.enemyImpact.arcDegrees, 1, 180);
            entry.FindPropertyRelative("obstructionMask").intValue = Physics.DefaultRaycastLayers;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var settings = ReadCinematicSettings(out string cinematicError);
        if (settings == null) throw new InvalidOperationException(cinematicError);
        var cinematics = serialized.FindProperty("cinematics");
        foreach (var definition in CollectCinematicDefinitions(settings))
        {
            int index = -1;
            for (int i = 0; i < cinematics.arraySize; i++)
                if (cinematics.GetArrayElementAtIndex(i).FindPropertyRelative("definition").objectReferenceValue == definition) { index = i; break; }
            if (index < 0) index = cinematics.arraySize++;
            var entry = cinematics.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("definition").objectReferenceValue = definition;
            entry.FindPropertyRelative("assetGuid").stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(definition));
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (!catalog.Rebuild(out string reason)) throw new InvalidOperationException(reason);
        AssetDatabase.SaveAssetIfDirty(catalog);
        return catalog;
    }

    private static Animator ResolveAnimator(GameObject root)
    {
        var actor = root.GetComponent<CharacterAnimationController>();
        return actor != null && actor.Animator != null ? actor.Animator : root.GetComponentInChildren<Animator>(true);
    }

    private static void WritePrefab(string path, CombatNetworkSkillCatalog catalog, CinematicSettings cinematicSettings)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            bool changed = false;
            if (root.GetComponent<NetworkObject>() == null) { root.AddComponent<NetworkObject>(); changed = true; }
            if (root.GetComponent<SquadCharacterController>() != null)
                changed |= CombatPlayerNetworkAuthoring.Ensure(root);
            if (root.GetComponent<SquadCharacterController>() == null && root.GetComponent<CharacterInfo>() == null)
            { root.AddComponent<CharacterInfo>(); changed = true; }
            var runtime = root.GetComponent<CombatNetworkActorRuntime>();
            if (runtime == null) { runtime = root.AddComponent<CombatNetworkActorRuntime>(); changed = true; }
            var serialized = new SerializedObject(runtime);
            var reference = serialized.FindProperty("catalog");
            if (reference.objectReferenceValue != catalog) { reference.objectReferenceValue = catalog; serialized.ApplyModifiedPropertiesWithoutUndo(); changed = true; }
            var animator = ResolveAnimator(root);
            if (root.GetComponent<SquadCharacterController>() != null)
                changed |= EnsureCinematicActor(root, runtime, cinematicSettings);
            if (root.GetComponent<SquadCharacterController>() == null)
                changed |= CombatEnemyNetworkAuthoring.Ensure(root, animator);
            if (root.GetComponent<SquadCharacterController>() != null && animator != null && animator.GetComponent<CombatNetworkAnimationEvents>() == null)
            { animator.gameObject.AddComponent<CombatNetworkAnimationEvents>(); changed = true; }
            if (changed && PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException("Could not save " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private sealed class CinematicSettings
    {
        public CounterSkillSO counter;
        public float clarity, dodge, preImpact, retry;
    }

    private static CinematicSettings ReadCinematicSettings(out string error)
    {
        error = "Install/save the common GameplaySessionRoot combat settings before network cinematic authoring.";
        var session = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionCombatMigration.SessionPath);
        if (session == null) return null;
        var managers = session.GetComponentsInChildren<RealTimeCombatManager>(true);
        var counters = session.GetComponentsInChildren<CounterSkillCombatController>(true);
        var reactions = session.GetComponentsInChildren<CombatHealthThresholdController>(true);
        if (managers.Length != 1 || counters.Length != 1 || reactions.Length != 1) return null;
        var manager = new SerializedObject(managers[0]);
        var reaction = new SerializedObject(reactions[0]);
        var clarity = manager.FindProperty("clarityForS");
        var dodge = reaction.FindProperty("dodgeWindowSeconds");
        var counter = reaction.FindProperty("counterWindowSeconds");
        var scale = reaction.FindProperty("reactionTimeScale");
        var retry = reaction.FindProperty("counterRetryDelaySeconds");
        var definition = counters[0].ConfiguredCounterSkill;
        if (clarity == null || dodge == null || counter == null || scale == null || retry == null || definition == null ||
            definition.CombatCinematicRigPrefab == null || definition.CombatCinematicRigPrefab.RuntimeTimeline == null)
        { error = "Session cinematic settings/counter baked rig are missing; no substitute is generated."; return null; }
        float[] values = { clarity.floatValue, dodge.floatValue, counter.floatValue, scale.floatValue, retry.floatValue };
        if (values.Any(value => float.IsNaN(value) || float.IsInfinity(value) || value <= 0) || counter.floatValue > dodge.floatValue)
        { error = "Session clarity/reaction timing is invalid; fix its author settings before network migration."; return null; }
        error = null;
        return new CinematicSettings { counter = definition, clarity = clarity.floatValue, dodge = dodge.floatValue,
            preImpact = counter.floatValue / Mathf.Min(1, scale.floatValue), retry = retry.floatValue };
    }

    private static bool CinematicActorNeedsChanges(GameObject root, CinematicSettings settings)
    {
        if (settings == null) return true;
        if (root.GetComponent<RealTimeCombatLoadout>() == null) return true;
        if (root.GetComponent<CombatHealthThresholdController>() == null) return true;
        var runtime = root.GetComponent<CombatNetworkActorRuntime>();
        if (runtime == null) return true;
        var serialized = new SerializedObject(runtime);
        var pool = serialized.FindProperty("actorCinematicPlayback").objectReferenceValue as CombatCinematicPlaybackService;
        if (pool == null || !pool.transform.IsChildOf(root.transform)) return true;
        var poolSettings = new SerializedObject(pool);
        var poolRoot = poolSettings.FindProperty("poolRoot").objectReferenceValue as Transform;
        return poolRoot == null || !poolRoot.IsChildOf(root.transform) ||
            serialized.FindProperty("networkCounterSkill").objectReferenceValue != settings.counter ||
            serialized.FindProperty("networkClarityForS").floatValue != settings.clarity ||
            serialized.FindProperty("networkDodgeWindowSeconds").floatValue != settings.dodge ||
            serialized.FindProperty("networkCounterPreImpactSeconds").floatValue != settings.preImpact ||
            serialized.FindProperty("networkCounterRetrySeconds").floatValue != settings.retry;
    }

    private static bool EnsureCinematicActor(GameObject root, CombatNetworkActorRuntime runtime, CinematicSettings settings)
    {
        if (settings == null) throw new InvalidOperationException("Missing source cinematic settings.");
        if (!CinematicActorNeedsChanges(root, settings)) return false;
        if (root.GetComponent<RealTimeCombatLoadout>() == null) root.AddComponent<RealTimeCombatLoadout>();
        if (root.GetComponent<CombatHealthThresholdController>() == null) root.AddComponent<CombatHealthThresholdController>();
        var serialized = new SerializedObject(runtime);
        var reference = serialized.FindProperty("actorCinematicPlayback");
        var playback = reference.objectReferenceValue as CombatCinematicPlaybackService;
        if (playback != null && !playback.transform.IsChildOf(root.transform))
            throw new InvalidOperationException("Actor cinematic pool belongs to another actor: " + root.name);
        if (playback == null)
        {
            var child = root.transform.Find("ActorCinematicPlayback");
            if (child == null) { child = new GameObject("ActorCinematicPlayback").transform; child.SetParent(root.transform, false); }
            playback = child.GetComponent<CombatCinematicPlaybackService>() ?? child.gameObject.AddComponent<CombatCinematicPlaybackService>();
        }
        var poolSettings = new SerializedObject(playback);
        var poolReference = poolSettings.FindProperty("poolRoot");
        var pool = poolReference.objectReferenceValue as Transform;
        if (pool != null && !pool.IsChildOf(root.transform)) throw new InvalidOperationException("Actor cinematic pool root is external: " + root.name);
        if (pool == null)
        {
            pool = playback.transform.Find("Pool");
            if (pool == null) { pool = new GameObject("Pool").transform; pool.SetParent(playback.transform, false); }
            poolReference.objectReferenceValue = pool;
            poolSettings.ApplyModifiedPropertiesWithoutUndo();
        }
        reference.objectReferenceValue = playback;
        serialized.FindProperty("networkCounterSkill").objectReferenceValue = settings.counter;
        serialized.FindProperty("networkClarityForS").floatValue = settings.clarity;
        serialized.FindProperty("networkDodgeWindowSeconds").floatValue = settings.dodge;
        serialized.FindProperty("networkCounterPreImpactSeconds").floatValue = settings.preImpact;
        serialized.FindProperty("networkCounterRetrySeconds").floatValue = settings.retry;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static List<UnityEngine.Object> CollectCinematicDefinitions(CinematicSettings settings)
    {
        var definitions = new HashSet<UnityEngine.Object> { settings.counter };
        var kits = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        if (kits == null) throw new InvalidOperationException("Missing roster kit source.");
        foreach (CombatSkillKit kit in Enum.GetValues(typeof(CombatSkillKit)))
        {
            var light = kits.GetCharacter(kit)?.playerSettings?.equipment?.equippedLightSkill;
            if (light != null) definitions.Add(light);
        }
        return definitions.OrderBy(definition => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(definition)), StringComparer.Ordinal).ToList();
    }
}
