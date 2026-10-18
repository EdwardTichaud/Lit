using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lit.Combat.Migration.Network;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Bounded pre-spawn wiring of authored encounters, never a conversion to generic AI.</summary>
public static class ProductionEncounterMigration
{
    private static readonly string[] DataPaths = {
        "Assets/Narrative/NinaCycle/Data/Enemy_ScientifiqueFou.asset",
        "Assets/Scenes/Cycles/Cycle_Etienne/Data/CharacterData_DeadWeight.asset",
        "Assets/Scenes/Cycles/Cycle_Belmont/Data/CharacterData_BrokenAnchor.asset",
        "Assets/Scenes/Cycles/Cycle_Ardent/Data/CharacterData_FalseChoir.asset",
        "Assets/Scenes/Cycles/Cycle_Belmont/Data/CharacterData_VeilleurDOmbre.asset",
        "Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut_CharacterData.asset",
        "Assets/Characters/3_Enemy/Juggernaut/GiantJuggernaut.asset"
    };
    private static readonly string[] Ids = {
        "mad_scientist_nina", "dead_weight_etienne", "broken_anchor_belmont", "false_choir_ardent",
        "shadow_warden_belmont", "giant_juggernaut", "giantjuggernaut"
    };
    public static System.Collections.Generic.IReadOnlyList<string> AuthoredEnemyDataPaths => DataPaths;

    [Serializable] public sealed class Entry
    {
        public string dataPath, dataGuid, uniqueId, characterId, prefabPath, prefabGuid, adapter;
        public string profilePath, profileProvenance;
        public List<string> sceneConsumers = new List<string>();
        public List<string> errors = new List<string>();
    }
    [Serializable] public sealed class Report
    {
        public bool applied, succeeded;
        public bool ReadyForProduction; // Authoring cannot certify gameplay.
        public bool sceneConsumersEvaluated;
        public List<Entry> encounters = new List<Entry>();
        public List<string> changedAssets = new List<string>();
        public List<string> savedAssets = new List<string>();
        public List<string> plannedFiles = new List<string>();
        public List<string> errors = new List<string>();
    }

    // Used by the common inventory: an authored EnemyController adapter is supported
    // without being forcibly replaced by a LitBrainsEnemy or a Juggernaut profile.
    public static bool TryValidateAuthoredEncounter(CharacterData data, out string reason)
    {
        reason = "Not one of the bounded production encounters.";
        if (data == null) return false;
        int index = Array.IndexOf(DataPaths, AssetDatabase.GetAssetPath(data));
        if (index < 0) return false;
        var errors = Validate(data, index);
        reason = errors.Count == 0 ? "Authored EnemyController encounter adapter; mechanics and identity retained." : string.Join("; ", errors);
        return errors.Count == 0;
    }

    [MenuItem("Lit/Combat/Production Migration/Encounter Authoring Dry Run")]
    public static void DryRun() => Run(false);

    public static Report Run(bool apply, bool includeSceneConsumers = true)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Encounter authoring requires an idle editor outside Play Mode.");
        var report = new Report { applied = apply, sceneConsumersEvaluated = includeSceneConsumers };
        var catalog = AssetDatabase.LoadAssetAtPath<CombatNetworkSkillCatalog>(CombatNetworkSetup.CatalogPath);
        if (catalog == null) report.errors.Add("Install the common network skill catalog before encounter wiring.");
        var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        var profilePlans = PlanEncounterProfiles(report);
        var scenes = includeSceneConsumers ? AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
            .Select(AssetDatabase.GUIDToAssetPath).Concat(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path))
            .Where(path => AssetDatabase.LoadMainAssetAtPath(path) != null).Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
        var sceneDependencies = scenes.ToDictionary(path => path,
            path => new HashSet<string>(AssetDatabase.GetDependencies(path, true), StringComparer.Ordinal));
        for (int i = 0; i < DataPaths.Length; i++)
        {
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(DataPaths[i]);
            var entry = new Entry { dataPath = DataPaths[i], dataGuid = AssetDatabase.AssetPathToGUID(DataPaths[i]) };
            report.encounters.Add(entry);
            if (data == null) { entry.errors.Add("Required original CharacterData missing."); continue; }
            entry.uniqueId = data.UniqueId; entry.characterId = data.characterId;
            entry.prefabPath = AssetDatabase.GetAssetPath(data.worldPrefab);
            entry.prefabGuid = AssetDatabase.AssetPathToGUID(entry.prefabPath);
            profilePlans.TryGetValue(data, out var profilePlan);
            entry.profilePath = profilePlan != null ? profilePlan.path : AssetDatabase.GetAssetPath(data.enemyCombatProfile);
            entry.profileProvenance = profilePlan?.provenance;
            entry.errors.AddRange(Validate(data, i, profilePlan?.profile));
            entry.adapter = data.worldPrefab?.GetComponent<EnemyController>() != null ? "EnemyController/IEnemyEncounterActor" : "missing";
            foreach (var scene in scenes)
                if (sceneDependencies[scene].Contains(DataPaths[i]) || sceneDependencies[scene].Contains(entry.prefabPath))
                    entry.sceneConsumers.Add(scene);
            if (data.worldPrefab == null) continue;
            prefabs[entry.prefabPath] = data.worldPrefab;
        }
        foreach (var pair in prefabs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var root = pair.Value;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (EditorUtility.IsDirty(root) || stage != null && stage.assetPath == pair.Key && stage.scene.isDirty)
                report.errors.Add("Save or close dirty encounter prefab: " + pair.Key);
            if (root.GetComponentsInChildren<Transform>(true).Any(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0))
                report.errors.Add("Missing encounter script in prefab hierarchy: " + pair.Key);
            var animator = ResolveAnimator(root);
            string error = CombatEnemyNetworkAuthoring.Validate(root, animator);
            if (error != null) { report.errors.Add(pair.Key + ": " + error); continue; }
            var runtime = root.GetComponent<CombatNetworkActorRuntime>();
            bool wrongCatalog = runtime != null && new SerializedObject(runtime).FindProperty("catalog").objectReferenceValue != catalog;
            if (root.GetComponent<CombatTimeDomain>() == null || root.GetComponent<NetworkObject>() == null ||
                runtime == null || wrongCatalog || CombatEnemyNetworkAuthoring.NeedsChanges(root, animator))
                report.changedAssets.Add(pair.Key);
        }
        foreach (var entry in report.encounters)
            report.errors.AddRange(entry.errors.Select(error => entry.dataPath + ": " + error));
        if (apply && report.errors.Count == 0)
        {
            foreach (var pair in profilePlans)
            {
                var plan = pair.Value;
                try
                {
                    if (plan.create)
                    {
                        AssetDatabase.CreateAsset(plan.profile, plan.path);
                        report.savedAssets.Add(plan.path);
                        report.savedAssets.Add(plan.path + ".meta");
                    }
                    if (pair.Key.enemyCombatProfile != plan.profile)
                    {
                        pair.Key.enemyCombatProfile = plan.profile;
                        EditorUtility.SetDirty(pair.Key);
                        AssetDatabase.SaveAssetIfDirty(pair.Key);
                        report.savedAssets.Add(AssetDatabase.GetAssetPath(pair.Key));
                    }
                }
                catch (Exception exception) { report.errors.Add(plan.path + ": " + exception); break; }
            }
            if (report.errors.Count == 0)
            foreach (string path in report.changedAssets.Where(path => path.EndsWith(".prefab", StringComparison.Ordinal)))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<CombatTimeDomain>() == null) root.AddComponent<CombatTimeDomain>();
                    if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();
                    var runtime = root.GetComponent<CombatNetworkActorRuntime>();
                    if (runtime == null) runtime = root.AddComponent<CombatNetworkActorRuntime>();
                    var serialized = new SerializedObject(runtime);
                    serialized.FindProperty("catalog").objectReferenceValue = catalog;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    CombatEnemyNetworkAuthoring.Ensure(root, ResolveAnimator(root));
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException("Could not save " + path);
                    report.savedAssets.Add(path);
                }
                catch (Exception exception) { report.errors.Add(path + ": " + exception); break; }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        foreach (var plan in profilePlans.Values)
            if (!EditorUtility.IsPersistent(plan.profile)) UnityEngine.Object.DestroyImmediate(plan.profile);
        foreach (string path in report.changedAssets)
            foreach (string file in new[] { path, path + ".meta" })
                if (!report.plannedFiles.Contains(file)) report.plannedFiles.Add(file);
        report.succeeded = report.errors.Count == 0;
        Directory.CreateDirectory(ProductionCombatMigration.ReportDirectory);
        File.WriteAllText(ProductionCombatMigration.ReportDirectory + (apply ? "/encounter-apply.json" : "/encounter-dry-run.json"), JsonUtility.ToJson(report, true));
        return report;
    }

    private static Animator ResolveAnimator(GameObject root)
    {
        var actor = root.GetComponent<CharacterAnimationController>();
        return actor != null && actor.Animator != null ? actor.Animator : root.GetComponentInChildren<Animator>(true);
    }

    private static List<string> Validate(CharacterData data, int index, EnemyCombatProfileSO plannedProfile = null)
    {
        var errors = new List<string>();
        if (data.characterId != Ids[index] || string.IsNullOrEmpty(data.UniqueId) || !data.isEnemy)
            errors.Add("Original enemy identity does not match the bounded manifest.");
        var root = data.worldPrefab;
        if (root == null) { errors.Add("Missing authored worldPrefab."); return errors; }
        if (root.GetComponents<EnemyController>().Length != 1 || root.GetComponent<LitBrainsEnemy>() != null)
            errors.Add("Expected exactly one authored EnemyController and no concurrent Brains actor.");
        if (!EnemyController.HasRequiredComponents(root)) errors.Add(EnemyController.DescribeRequiredComponents(root));
        if (root.GetComponent<CharacterInfo>() == null) errors.Add("Missing CharacterInfo.");
        if (root.GetComponentInChildren<NetworkObject>(true) != null && root.GetComponent<NetworkObject>() == null)
            errors.Add("Nested-only NetworkObject cannot be repaired implicitly.");
        if (index == 0)
        {
            if (root.GetComponent<GhostController>() == null || data.enemyEncounterOptions?.startAsGhost != true || data.enemyDeathOptions?.enabled != true)
                errors.Add("ScientifiqueFou requires its authored Ghost introduction and last-word death contract.");
        }
        Type bossType = index == 1 ? typeof(DeadWeightBoss) : index == 2 ? typeof(BrokenAnchorBoss) : index == 3 ? typeof(FalseChoirBoss) : null;
        if (bossType != null)
        {
            var boss = root.GetComponent(bossType) as BossEncounterBehaviour;
            if (boss == null || boss.Definition == null) errors.Add("Missing authored " + bossType.Name + " or BossDefinition.");
            if (root.GetComponents<BossEncounterBehaviour>().Length != 1) errors.Add("Expected one specialized boss authority.");
            if (boss != null && boss.Enemy != root.GetComponent<EnemyController>()) errors.Add("Boss references a different enemy actor.");
        }
        // BrokenAnchor is intentionally a puzzle, not an ordinary attack-pattern enemy.
        var profile = plannedProfile != null ? plannedProfile : data.enemyCombatProfile;
        if (index != 2 && (profile == null || profile.patterns == null ||
            profile.patterns.Count == 0 || profile.patterns.Any(pattern => pattern == null || !pattern.IsConfigured)))
            errors.Add("Missing/incomplete original attack patterns; no generic replacement permitted.");
        if (index != 2 && profile?.patterns != null)
            foreach (var pattern in profile.patterns.Where(pattern => pattern?.skills != null))
                foreach (var skill in pattern.skills.Where(skill => skill != null))
                    if (data.combatSkills == null || !data.combatSkills.Contains(skill))
                        errors.Add("Pattern '" + pattern.name + "' requests unequipped skill " + AssetDatabase.GetAssetPath(skill));
        return errors;
    }

    private sealed class EncounterProfilePlan
    {
        public string path, provenance;
        public EnemyCombatProfileSO profile;
        public bool create;
    }

    private static Dictionary<CharacterData, EncounterProfilePlan> PlanEncounterProfiles(Report report)
    {
        var plans = new Dictionary<CharacterData, EncounterProfilePlan>();
        // Only these audited identities are approved for profile repair. Puzzle/ghost
        // encounters are not automatically converted or assigned unrelated attacks.
        foreach (int index in new[] { 1, 3, 4, 5, 6 })
        {
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(DataPaths[index]);
            if (data == null || data.worldPrefab == null) continue;
            if (index < 5 && IsEquippedProfile(data, data.enemyCombatProfile))
            {
                foreach (var skill in data.enemyCombatProfile.patterns.SelectMany(pattern => pattern.skills).Distinct())
                    ValidateEquippedSkill(data, skill, report.errors);
                continue;
            }
            string directory = index >= 5 ? "Assets/Characters/3_Enemy/GiantJuggernaut" :
                Path.GetDirectoryName(DataPaths[index]).Replace('\\', '/');
            string path = directory + "/" + Ids[index] + "_CombatProfile.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyCombatProfileSO>(path);
            if (EditorUtility.IsDirty(data) || existing != null && EditorUtility.IsDirty(existing))
                report.errors.Add("Save dirty encounter identity/profile before migration: " + DataPaths[index]);
            if (existing == null && AssetDatabase.LoadMainAssetAtPath(path) != null)
            { report.errors.Add("Dedicated profile path is occupied by another asset type: " + path); continue; }
            var source = data.enemyCombatProfile;
            if (source != null && EditorUtility.IsDirty(source))
                report.errors.Add("Save dirty authored source profile before migration: " + AssetDatabase.GetAssetPath(source));
            var prefabSource = data.worldPrefab.GetComponent<CharacterInfo>()?.SourceData?.enemyCombatProfile;
            var plan = new EncounterProfilePlan { path = path, profile = existing, create = existing == null };
            plan.provenance = "identity=" + DataPaths[index] + "; equipped SkillSO only; global tuning=" +
                (existing != null ? "existing dedicated profile (preserved)" : source != null ? AssetDatabase.GetAssetPath(source) : "identity EnemySettings and existing EnemyCombatProfileSO defaults") +
                "; exact single-skill pattern provenance=" + AssetDatabase.GetAssetPath(prefabSource);
            if (existing == null)
            {
                plan.profile = source != null ? UnityEngine.Object.Instantiate(source) : ScriptableObject.CreateInstance<EnemyCombatProfileSO>();
                plan.profile.name = Path.GetFileNameWithoutExtension(path);
                plan.profile.patterns = new List<EnemyCombatPattern>();
                var settings = data.enemySettings ?? new EnemySettings();
                if (source == null)
                {
                    plan.profile.trackingDegreesPerSecond = settings.LocomotionFacingSpeedDegreesPerSecond;
                    plan.profile.preferredCombatDistance = settings.LocomotionPositioning.preferredDistance;
                }
                foreach (var skill in data.combatSkills ?? new List<SkillSO>())
                {
                    if (skill == null) { report.errors.Add(DataPaths[index] + ": equipped skill is missing."); continue; }
                    ValidateEquippedSkill(data, skill, report.errors);
                    if (skill.AnimationClip == null) continue;
                    var sourcePatterns = source != null ? source.patterns : prefabSource?.patterns;
                    var authored = sourcePatterns?.FirstOrDefault(pattern => pattern?.skills?.Count == 1 && pattern.skills[0] == skill);
                    if (authored != null)
                    {
                        plan.profile.patterns.Add(new EnemyCombatPattern {
                            name = authored.name, skills = new List<SkillSO>(authored.skills), weight = authored.weight,
                            minimumStartDistance = authored.minimumStartDistance, cooldownSeconds = authored.cooldownSeconds,
                            recoverySeconds = authored.recoverySeconds, maximumConsecutiveUses = authored.maximumConsecutiveUses,
                            maximumStartAngle = authored.maximumStartAngle
                        });
                        plan.provenance += "; " + AssetDatabase.GetAssetPath(skill) + " uses exact existing pattern '" + authored.name + "' from " +
                            AssetDatabase.GetAssetPath(source != null ? source : prefabSource);
                    }
                    else
                    {
                        plan.profile.patterns.Add(new EnemyCombatPattern {
                            name = skill.AnimatorState, skills = new List<SkillSO> { skill }, weight = 1,
                            minimumStartDistance = skill.MinimumHitDistance,
                            cooldownSeconds = Mathf.Max(skill.cooldownSeconds, settings.ActorRetaliationDelaySeconds),
                            recoverySeconds = Mathf.Max(0, skill.AnimationClip.length * (1f - skill.Presentation.recoveryNormalizedTime)),
                            maximumConsecutiveUses = 1, maximumStartAngle = data.vision != null ? data.vision.fieldOfViewDegrees * .5f : 180f
                        });
                        plan.provenance += "; " + AssetDatabase.GetAssetPath(skill) +
                            " has no original matching pattern: range=SkillSO; cooldown=max(SkillSO.cooldownSeconds, EnemySettings.ActorRetaliationDelaySeconds);" +
                            " recovery=clip.length*(1-Presentation.recoveryNormalizedTime); angle=identity vision half-cone; weight=one equipped action";
                    }
                }
                // An aerial-only kit must never try to approach for an unequipped melee attack.
                plan.profile.preferMeleeApproach = plan.profile.patterns.Any(pattern => pattern.skills.Any(skill => !skill.EnemyActionMotion.IsAirborne));
                report.changedAssets.Add(path);
            }
            else
                foreach (var skill in data.combatSkills ?? new List<SkillSO>())
                    if (skill != null) ValidateEquippedSkill(data, skill, report.errors);
            if (data.enemyCombatProfile != plan.profile) report.changedAssets.Add(DataPaths[index]);
            plans.Add(data, plan);
        }
        return plans;
    }

    private static bool IsEquippedProfile(CharacterData data, EnemyCombatProfileSO profile) =>
        profile != null && profile.patterns != null && profile.patterns.Count > 0 &&
        data.combatSkills != null && profile.patterns.All(pattern => pattern != null && pattern.IsConfigured &&
            pattern.skills.All(skill => data.combatSkills.Contains(skill)));

    private static IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
    {
        foreach (var state in machine.states) yield return state.state;
        foreach (var child in machine.stateMachines)
            foreach (var state in States(child.stateMachine)) yield return state;
    }

    private static void ValidateEquippedSkill(CharacterData data, SkillSO skill, List<string> errors)
    {
        string subject = AssetDatabase.GetAssetPath(data) + ": " + AssetDatabase.GetAssetPath(skill);
        var animator = ResolveAnimator(data.worldPrefab);
        var controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
        if (controller == null || skill.AnimationClip == null || string.IsNullOrEmpty(skill.AnimatorState))
        { errors.Add(subject + ": original clip/state/controller unavailable."); return; }
        var states = controller.layers.SelectMany(layer => States(layer.stateMachine)).Where(state => state.name == skill.AnimatorState).ToArray();
        if (states.Length != 1 || states[0].motion != skill.AnimationClip)
            errors.Add(subject + ": shared prefab Animator state does not map uniquely to the equipped clip.");
        var events = AnimationUtility.GetAnimationEvents(skill.AnimationClip);
        if (events.Count(e => e.functionName == "EnemyAttack" && e.objectReferenceParameter == skill) != 1 ||
            events.Count(e => e.functionName == "EndEnemyAttack") != 1)
            errors.Add(subject + ": requires the original unique EnemyAttack(SkillSO) and EndEnemyAttack events.");
    }
}
