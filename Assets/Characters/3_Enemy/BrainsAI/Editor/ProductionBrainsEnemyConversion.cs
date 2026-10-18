using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Ultrabolt.BrainsAI;
using Object = UnityEngine.Object;

public static class ProductionBrainsEnemyConversion
{
    [Serializable]
    public sealed class AttackMapping
    {
        public SkillSO skill;
        public AnimationClip clip;
        public string skillGuid, clipGuid, statePath;
        public float reactionTime, lockTime, impactTime, endTime;
    }

    [Serializable]
    public sealed class Report
    {
        public CharacterData character;
        public EnemyCombatProfileSO sourceProfile;
        public AnimatorController sourceController;
        public string characterGuid, prefabGuid, prefabPath;
        public bool requiresFlameInfluence;
        public List<AttackMapping> attacks = new List<AttackMapping>();
        public List<string> blockers = new List<string>();
        public List<string> conversionBlockers = new List<string>();
        public List<ProductionBrainsScriptDiagnostics.Slot> scriptDiagnostics = new List<ProductionBrainsScriptDiagnostics.Slot>();
        public List<string> runtimeRequirements = new List<string>();
        public bool CanGenerate => blockers.Count == 0;
        public bool CanApply => CanGenerate && conversionBlockers.Count == 0;
    }

    public static Report[] AuditProductionRoster() => AssetDatabase.FindAssets("t:CharacterData", new[] { "Assets" })
        .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal)
        .Select(AssetDatabase.LoadAssetAtPath<CharacterData>).Where(data => data != null && data.isEnemy)
        .Select(data => Analyze(data)).ToArray();

    [MenuItem("Lit/Combat/Production Enemies/Audit All Enemy Characters")]
    public static void AuditAllEnemyCharacters()
    {
        foreach (var report in AuditProductionRoster()) Debug.Log(JsonUtility.ToJson(report, true), report.character);
    }

    [MenuItem("Lit/Combat/Production Enemies/Audit Selected Character")]
    public static void AuditSelected()
    {
        var data = Selection.activeObject as CharacterData;
        if (data == null) throw new InvalidOperationException("Select the production CharacterData, not a prototype prefab.");
        Debug.Log(JsonUtility.ToJson(Analyze(data), true), data);
    }

    [MenuItem("Lit/Combat/Production Enemies/Generate Reviewed Juggernaut Assets")]
    public static void GenerateJuggernaut() => GenerateReviewedAssets(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset"));

    [MenuItem("Lit/Combat/Production Enemies/Convert Production Juggernaut In Place")]
    public static void ConvertJuggernaut()
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset");
        var report = Analyze(data);
        if (!report.CanApply) throw new InvalidOperationException("No asset modified.\n" + string.Join("\n", report.blockers.Concat(report.conversionBlockers)));
        var request = GenerateReviewedAssets(data);
        Apply(request);
    }

    [MenuItem("Lit/Combat/Production Enemies/Apply Selected Reviewed Request")]
    public static void ApplySelected()
    {
        var request = Selection.activeObject as ProductionBrainsEnemyConversionRequest;
        if (request == null) throw new InvalidOperationException("Select an explicit reviewed conversion request.");
        Apply(request);
    }

    public static Report Analyze(CharacterData data, AnimatorController sourceController = null)
    {
        var report = new Report { character = data };
        if (data == null || !data.isEnemy)
        { report.blockers.Add("A production enemy CharacterData is required."); return report; }
        string dataPath = AssetDatabase.GetAssetPath(data);
        report.characterGuid = AssetDatabase.AssetPathToGUID(dataPath);
        report.prefabPath = AssetDatabase.GetAssetPath(data.ResolveWorldPrefab());
        report.prefabGuid = AssetDatabase.AssetPathToGUID(report.prefabPath);
        report.sourceProfile = data.enemyCombatProfile;
        if (string.IsNullOrEmpty(report.characterGuid) || data.UniqueId != report.characterGuid)
            report.blockers.Add("Character identity does not match its persistent asset GUID; identity repair is forbidden.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(report.prefabPath);
        if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Regular)
        { report.blockers.Add("A regular, explicitly authored production prefab is required (no variant unpacking)."); return report; }
        report.scriptDiagnostics = ProductionBrainsScriptDiagnostics.Inspect(prefab);
        foreach (var slot in report.scriptDiagnostics)
            report.blockers.Add("Unresolved prefab component slot: " + slot.gameObjectPath + " slot=" + slot.componentSlot +
                " scriptGUID=" + slot.scriptGuid + " GUIDToAssetPath=" + slot.guidToAssetPath +
                " MonoScript.GetClass=" + slot.className + ". " + slot.status);
        var health = prefab.GetComponent<CharacterInfo>();
        if (health == null || health.SourceData != data)
            report.blockers.Add("Root CharacterInfo must reference this exact CharacterData.");
        var animator = prefab.GetComponent<Animator>();
        if (animator == null)
        {
            int nestedAnimators = prefab.GetComponentsInChildren<Animator>(true).Length;
            report.blockers.Add("Root Animator contract is absent (nested Animators: " + nestedAnimators +
                "). An explicit nested Animator/event-receiver adapter is required; no controller or hierarchy is inferred.");
            return report;
        }
        if (data.enemyDeathOptions != null && data.enemyDeathOptions.enabled)
            report.blockers.Add("Authored last-words/death presentation needs its dedicated encounter adapter; generic death is not a replacement.");
        var body = prefab.GetComponent<Rigidbody>();
        var capsule = prefab.GetComponent<CapsuleCollider>();
        if (prefab.GetComponent<NavMeshAgent>() == null || body == null || !body.isKinematic || body.useGravity ||
            capsule == null || capsule.isTrigger || capsule.direction != 1)
            report.blockers.Add("The exact kinematic, scripted, Y-capsule/NavMesh production motor contract is not present.");
        if (data.enemySettings == null || data.enemySettings.PhysicsAnimationMovementMode != EnemyController.AnimationMovementMode.ScriptedOnly)
            report.blockers.Add("Source animation/root-motion motor is not the proven ScriptedOnly contract.");
        if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component is IBossEncounterBehaviour))
            report.blockers.Add("Boss encounter: migrate its typed controller, damage policy, puzzle and network lifecycle first. Never substitute Juggernaut AI.");
        var legacy = prefab.GetComponent<EnemyController>();
        var existingBrain = prefab.GetComponent<LitBrainsEnemy>();
        if (existingBrain != null && existingBrain.combatProfile != null)
        {
            string profilePath = AssetDatabase.GetAssetPath(existingBrain.combatProfile);
            string provenancePath = string.IsNullOrEmpty(profilePath) ? null :
                System.IO.Path.GetDirectoryName(profilePath).Replace('\\', '/') + "/ConversionRequest.asset";
            var provenance = provenancePath != null ? AssetDatabase.LoadAssetAtPath<ProductionBrainsEnemyConversionRequest>(provenancePath) : null;
            if (provenance != null && provenance.character == data && provenance.reviewedProfile == existingBrain.combatProfile &&
                provenance.reviewedController == animator.runtimeAnimatorController && provenance.sourceController != null &&
                (sourceController == null || sourceController == provenance.sourceController))
                sourceController = provenance.sourceController;
            else
                report.blockers.Add("Converted actor lacks matching persisted source/controller provenance; no source controller inferred.");
        }
        bool mappedProduction = existingBrain != null && sourceController != null &&
            existingBrain.combatProfile != null && existingBrain.combatProfile.executeProductionPatterns && existingBrain.combatProfile.productionSourceProfile == data.enemyCombatProfile;
        // Clash authoring is an upgrade of this proven conversion, not an admission prerequisite.
        // Apply validates the unchanged source contract before upgrading owned clips and the prefab.
        if (mappedProduction)
            report.runtimeRequirements.Add("Clash authoring is upgraded by Apply: missing volumes/windows do not block CanApply; source provenance, geometry and non-clash events must still validate before any upgrade.");
        var flamePolicy = prefab.GetComponent<ProductionBrainsFlamePolicy>();
        report.requiresFlameInfluence = legacy != null ? legacy.RequiresFlameInfluence : flamePolicy != null && flamePolicy.requiresFlameInfluence;
        if (mappedProduction && (flamePolicy == null || !flamePolicy.enabled ||
            prefab.GetComponent<ProductionBrainsDeathLifecycle>()?.enabled != true || prefab.GetComponent<EnemyDeathPresentation>()?.enabled != true))
            report.conversionBlockers.Add("Converted production actor requires enabled fixed-flame and retained death/dissolve lifecycle adapters.");
        if (legacy == null && !mappedProduction) report.blockers.Add("Expected one legacy EnemyController; this is not an automatic repair or a prototype conversion.");
        else if (legacy != null)
        {
            if (sourceController != null && animator.runtimeAnimatorController != sourceController)
                report.blockers.Add("Reviewed source controller is not the current authored controller; refusing stale or borrowed provenance.");
            var serialized = new SerializedObject(legacy);
            if (!legacy.enabled)
                report.blockers.Add("Disabled legacy actor cannot become an enabled Brains actor implicitly.");
            if (serialized.FindProperty("combatEnabled")?.boolValue != true || legacy.StartsAsGhost)
                report.blockers.Add("Encounter/ghost gate requires an explicit ILitBrainsEncounterPolicy adapter before conversion.");
            foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || component == legacy) continue;
                var iterator = new SerializedObject(component).GetIterator();
                while (iterator.Next(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == legacy)
                        report.blockers.Add(component.GetType().Name + "." + iterator.propertyPath + " still references EnemyController; migrate its API first.");
            }
        }
        if (prefab.GetComponent<Brain>() != null && !mappedProduction)
            report.blockers.Add("A brain already exists. Refusing to add a second brain or alter a prototype.");
        var controller = sourceController != null ? sourceController : animator.runtimeAnimatorController as AnimatorController;
        report.sourceController = controller;
        if (controller == null) report.blockers.Add("A direct AnimatorController is required; override/ambiguous controllers need an explicit mapping adapter.");
        var profile = report.sourceProfile;
        if (profile == null || profile.patterns == null || profile.patterns.Count == 0)
        { report.blockers.Add("Missing authored production pattern profile."); return report; }
        var skills = profile.patterns.Where(pattern => pattern != null && pattern.skills != null)
            .SelectMany(pattern => pattern.skills).Where(skill => skill != null).Distinct()
            .OrderBy(skill => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skill)), StringComparer.Ordinal).ToArray();
        foreach (var skill in skills)
        {
            var mapping = InspectAttack(skill, controller, report.blockers);
            if (mapping != null) report.attacks.Add(mapping);
        }
        if (profile.patterns.Any(pattern => pattern == null || !pattern.IsConfigured))
            report.blockers.Add("Incomplete authored pattern; no inferred attacks will be supplied.");
        report.runtimeRequirements.Add("Common impacts must use CollectAuthoredImpactVictims, HasAuthoredImpactContact and AuthoredAttackDamage; retain actor-specific guard/dodge/clash authority.");
        report.runtimeRequirements.Add("Common ledger must use IsThreatFor(actor), not only the current perception target.");
        report.runtimeRequirements.Add("Converted death requires the existing SceneMarker persistence provider/identity before server Despawn(false); the retained inactive root remains registered for saves.");
        report.runtimeRequirements.Add("NGO death must explicitly choose server Despawn versus retained persistent corpse. SetActive(false) is not a network despawn; verify remote cleanup, late join, encounter/threat removal and pause/presentation release on disable and despawn.");
        return report;
    }

    /// <summary>Unsaved inspection candidate, not a production-ready asset or permission to convert.</summary>
    public static LitBrainsCombatProfile CreateInspectionCandidate(Report report)
    {
        if (report == null || report.character == null) throw new ArgumentException("An analyzed production character is required.");
        var profile = ScriptableObject.CreateInstance<LitBrainsCombatProfile>();
        profile.name = report.character.name + "_Brains_InspectionOnly";
        profile.hideFlags = HideFlags.HideAndDontSave;
        profile.productionSourceProfile = report.sourceProfile;
        var settings = report.character.enemySettings;
        if (settings == null) { Object.DestroyImmediate(profile); throw new InvalidOperationException("Missing authored enemy presentation settings."); }
        profile.hurtState = settings.ActorHitAnimatorState;
        profile.hurtBlendSeconds = settings.ActorHitAnimationTransitionSeconds;
        profile.locomotionState = settings.ActorIdleAnimatorState;
        profile.deathState = settings.ActorDeathAnimatorState;
        profile.attacks = report.attacks.Select(mapping => new LitBrainsAttackProfile {
            skill = mapping.skill, clip = mapping.clip, stateName = mapping.statePath,
            reactionSeconds = mapping.reactionTime, strikeSeconds = mapping.lockTime,
            impactSeconds = mapping.impactTime, recoverySeconds = mapping.endTime,
            reach = mapping.skill.MaximumHitDistance, arcDegrees = mapping.skill.enemyImpact.arcDegrees,
            advanceDistance = mapping.skill.EnemyActionMotion.enableAdvance ? mapping.skill.EnemyActionMotion.advanceDistance : 0,
            advanceSeconds = mapping.skill.EnemyActionMotion.advanceDuration
        }).ToArray();
        return profile;
    }

    public static AttackMapping InspectAttack(SkillSO skill, AnimatorController controller, List<string> blockers)
    {
        if (skill == null || skill.AnimationClip == null || controller == null || controller.layers.Length != 1)
        { blockers.Add("Missing skill/clip or unsupported Animator layer mapping."); return null; }
        var states = new List<KeyValuePair<string, AnimatorState>>();
        CollectStates(controller.layers[0].stateMachine, controller.layers[0].name, states);
        var matches = states.Where(state => state.Key == skill.AnimatorState || state.Value.name == skill.AnimatorState).ToArray();
        if (matches.Length != 1 || matches[0].Value.motion != skill.AnimationClip ||
            matches[0].Value.speed != 1f || matches[0].Value.speedParameterActive || matches[0].Value.timeParameterActive)
        { blockers.Add(skill.name + ": exact, unit-speed SkillSO/state/clip mapping not proven (no name-based clip substitution)."); return null; }
        var events = AnimationUtility.GetAnimationEvents(skill.AnimationClip);
        string[] essential = { "OpenEnemyReactionOpportunity", "LockEnemyAttackDirection", "EnemyAttack", "EndEnemyAttack" };
        foreach (string name in essential)
            if (events.Count(entry => entry.functionName == name) != 1)
            { blockers.Add(skill.name + ": expected exactly one " + name + "."); return null; }
        var reaction = events.Single(entry => entry.functionName == essential[0]);
        var locked = events.Single(entry => entry.functionName == essential[1]);
        var impact = events.Single(entry => entry.functionName == essential[2]);
        var end = events.Single(entry => entry.functionName == essential[3]);
        if (impact.objectReferenceParameter != skill || reaction.time >= impact.time || locked.time > impact.time ||
            impact.time >= end.time || end.time > skill.AnimationClip.length || events.Any(entry => entry.time > end.time))
        { blockers.Add(skill.name + ": authored event identity/order/timing is invalid."); return null; }
        var motion = skill.EnemyActionMotion;
        if (motion == null) { blockers.Add(skill.name + ": missing authored action motion contract."); return null; }
        foreach (var pair in new[] { new[] { "BeginEnemyAdvance", "EndEnemyAdvance" }, new[] { "BeginEnemyAirborne", "RequestEnemyLanding" } })
        {
            bool required = pair[0] == "BeginEnemyAdvance" ? motion.enableAdvance : motion.IsAirborne;
            var starts = events.Where(entry => entry.functionName == pair[0]).ToArray();
            var stops = events.Where(entry => entry.functionName == pair[1]).ToArray();
            if (starts.Length != (required ? 1 : 0) || stops.Length != starts.Length ||
                starts.Length == 1 && starts[0].time > stops[0].time)
                blockers.Add(skill.name + ": motion event pair does not match its authored motion profile: " + pair[0]);
        }
        if (events.Count(entry => entry.functionName == "BeginEnemyRush") != (motion.HasHomingRush ? 1 : 0))
            blockers.Add(skill.name + ": homing rush event/profile mismatch.");
        var known = new HashSet<string>(essential.Concat(new[] { "BeginEnemyAdvance", "EndEnemyAdvance", "BeginEnemyAirborne", "RequestEnemyLanding", "BeginEnemyRush", "EndEnemyRush", "OpenAttackClashWindow", "CloseAttackClashWindow" }));
        foreach (var entry in events)
            if (!known.Contains(entry.functionName)) blockers.Add(skill.name + ": unmapped event " + entry.functionName + ".");
        return new AttackMapping {
            skill = skill, clip = skill.AnimationClip, statePath = matches[0].Key,
            skillGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skill)),
            clipGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skill.AnimationClip)),
            reactionTime = reaction.time, lockTime = locked.time, impactTime = impact.time, endTime = end.time
        };
    }

    private static void CollectStates(AnimatorStateMachine machine, string path, List<KeyValuePair<string, AnimatorState>> result)
    {
        foreach (var child in machine.states) result.Add(new KeyValuePair<string, AnimatorState>(path + "." + child.state.name, child.state));
        foreach (var child in machine.stateMachines) CollectStates(child.stateMachine, path + "." + child.stateMachine.name, result);
    }

    public static ProductionBrainsEnemyConversionRequest GenerateReviewedAssets(CharacterData data, string outputFolder = null, CombatLabClashController clashConfig = null)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Edit-time generation only.");
        if (clashConfig == null)
        {
            using (var source = new CombatClashAuthoring.ConfigurationScope()) return GenerateReviewedAssets(data, outputFolder, source.Config);
        }
        if (data == null || data.ResolveWorldPrefab() == null) throw new InvalidOperationException("Exact production CharacterData/prefab required.");
        string folder = outputFolder ?? System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(data.ResolveWorldPrefab())).Replace('\\', '/') + "/BrainsProduction";
        if (!folder.StartsWith("Assets/", StringComparison.Ordinal) || folder.Contains("..") || folder.Contains('\\') || !folder.EndsWith("/BrainsProduction", StringComparison.Ordinal))
            throw new InvalidOperationException("Output must be a new explicit Assets/.../BrainsProduction folder.");
        string requestPath = folder + "/ConversionRequest.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ProductionBrainsEnemyConversionRequest>(requestPath);
        var report = Analyze(data, existing != null ? existing.sourceController : null);
        if (!report.CanGenerate) throw new InvalidOperationException("No asset modified.\n" + string.Join("\n", report.blockers));
        if (existing != null)
        {
            if (existing.character != data) throw new InvalidOperationException("Destination belongs to another source character.");
            if (existing.requiresFlameInfluence != report.requiresFlameInfluence)
                throw new InvalidOperationException("Existing request changes the authored fixed-flame requirement.");
            EnsureGeneratedClashEvents(report, existing.reviewedProfile, existing.reviewedController, clashConfig);
            ValidateDestination(report, existing.reviewedProfile, existing.reviewedController, clashConfig);
            return existing;
        }
        if (AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("Unowned/partial output folder exists; refusing to overwrite any asset: " + folder);
        var sourceController = report.sourceController;
        var states = new List<KeyValuePair<string, AnimatorState>>();
        CollectStates(sourceController.layers[0].stateMachine, sourceController.layers[0].name, states);
        string prefix = sourceController.layers[0].name + ".";
        foreach (string state in new[] { data.enemySettings.ActorIdleAnimatorState, data.enemySettings.ActorHitAnimatorState,
            data.enemySettings.ActorDeathAnimatorState, "CombatLocomotion" })
            if (states.Count(entry => entry.Key == state || entry.Value.name == state) != 1)
                throw new InvalidOperationException("Missing production presentation state; no replacement inferred: " + state);
        var requiredPaths = new HashSet<string>(report.attacks.Select(mapping => mapping.statePath));
        foreach (string state in new[] { data.enemySettings.ActorIdleAnimatorState, data.enemySettings.ActorHitAnimatorState,
            data.enemySettings.ActorDeathAnimatorState, "CombatLocomotion" })
            requiredPaths.Add(states.Single(entry => entry.Key == state || entry.Value.name == state).Key);
        var guard = states.Where(entry => entry.Value.name == "Guard").ToArray();
        if (guard.Length > 1) throw new InvalidOperationException("Ambiguous authored Guard state.");
        if (guard.Length == 1) requiredPaths.Add(guard[0].Key);
        string idlePath = states.Single(entry => entry.Key == data.enemySettings.ActorIdleAnimatorState || entry.Value.name == data.enemySettings.ActorIdleAnimatorState).Key;
        var created = new List<string>();
        try
        {
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), "BrainsProduction");
            string controllerPath = folder + "/Production.controller";
            created.Add(controllerPath);
            var controller = ProductionBrainsAnimatorGraph.Create(sourceController,
                states.Where(entry => requiredPaths.Contains(entry.Key)), idlePath, controllerPath);
            states.Clear();
            CollectStates(controller.layers[0].stateMachine, controller.layers[0].name, states);
            var profile = CreateInspectionCandidate(report);
            profile.hideFlags = HideFlags.None;
            profile.name = data.name + "_ProductionBrains";
            profile.executeProductionPatterns = true;
            profile.idleState = states.Single(entry => entry.Key == data.enemySettings.ActorIdleAnimatorState || entry.Value.name == data.enemySettings.ActorIdleAnimatorState).Key;
            profile.locomotionState = prefix + "CombatLocomotion";
            profile.hurtState = states.Single(entry => entry.Key == data.enemySettings.ActorHitAnimatorState || entry.Value.name == data.enemySettings.ActorHitAnimatorState).Key;
            profile.deathState = states.Single(entry => entry.Key == data.enemySettings.ActorDeathAnimatorState || entry.Value.name == data.enemySettings.ActorDeathAnimatorState).Key;
            profile.knockoutState = profile.hurtState;
            profile.turnDegreesPerSecond = report.sourceProfile.trackingDegreesPerSecond;
            profile.approachStopDistance = report.sourceProfile.preferredCombatDistance;
            profile.resistance = profile.interruptionProtectionSeconds = 0;
            var hurtClip = states.Single(entry => entry.Key == profile.hurtState).Value.motion as AnimationClip;
            profile.hurtSeconds = hurtClip != null ? hurtClip.length * data.enemySettings.ActorHitRecoveryNormalizedTime : 0;
            for (int i = 0; i < report.attacks.Count; i++)
            {
                var mapping = report.attacks[i];
                var clip = Object.Instantiate(mapping.clip);
                clip.name = mapping.clip.name + "_ProductionBrains";
                AnimationUtility.SetAnimationEvents(clip, ReviewedEvents(mapping, clashConfig));
                string clipPath = folder + "/" + mapping.skillGuid + ".anim";
                AssetDatabase.CreateAsset(clip, clipPath);
                created.Add(clipPath);
                profile.attacks[i].clip = clip;
                var state = states.Single(entry => entry.Key == mapping.statePath).Value;
                state.motion = clip;
                state.tag = "Attack";
                EditorUtility.SetDirty(state);
            }
            var locomotion = states.Single(entry => entry.Key == profile.locomotionState).Value;
            var idle = states.Single(entry => entry.Key == profile.idleState).Value;
            locomotion.tag = idle.tag = "Moveable";
            EditorUtility.SetDirty(locomotion);
            EditorUtility.SetDirty(idle);
            foreach (var parameter in new[] { "Attack", "Hurt" }) EnsureParameter(controller, parameter, AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Attack ID", AnimatorControllerParameterType.Int);
            EnsureParameter(controller, "Dead", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Grounded", AnimatorControllerParameterType.Bool);
            string profilePath = folder + "/ProductionProfile.asset";
            AssetDatabase.CreateAsset(profile, profilePath);
            created.Add(profilePath);
            var request = ScriptableObject.CreateInstance<ProductionBrainsEnemyConversionRequest>();
            request.character = data;
            request.sourceController = sourceController;
            request.requiresFlameInfluence = report.requiresFlameInfluence;
            request.reviewedProfile = profile;
            request.reviewedController = controller;
            AssetDatabase.CreateAsset(request, requestPath);
            created.Add(requestPath);
            EditorUtility.SetDirty(controller);
            ValidateDestination(report, profile, controller, clashConfig);
            foreach (string path in created) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(path));
            return request;
        }
        catch
        {
            // Only paths created in this invocation are eligible for cleanup.
            foreach (string path in created.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(path);
            throw;
        }
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        var parameter = controller.parameters.FirstOrDefault(entry => entry.name == name);
        if (parameter != null && parameter.type != type) throw new InvalidOperationException("Conflicting source Animator parameter: " + name);
        if (parameter == null) controller.AddParameter(name, type);
    }

    public static string MapEvent(string source)
    {
        switch (source)
        {
            case "OpenEnemyReactionOpportunity": return "OpenBrainsReactionOpportunity";
            case "LockEnemyAttackDirection": return "BeginBrainsStrike";
            case "EnemyAttack": return "ResolveBrainsAttackImpact";
            case "EndEnemyAttack": return "BeginBrainsRecovery";
            case "BeginEnemyAdvance": return "ProductionBrainsBeginAdvance";
            case "EndEnemyAdvance": return "ProductionBrainsEndAdvance";
            case "BeginEnemyAirborne": return "ProductionBrainsBeginAirborne";
            case "RequestEnemyLanding": return "ProductionBrainsRequestLanding";
            case "BeginEnemyRush": return "ProductionBrainsBeginRush";
            case "EndEnemyRush": return "ProductionBrainsEndRush";
            case "OpenAttackClashWindow": return "OpenAttackClashWindow";
            case "CloseAttackClashWindow": return "CloseAttackClashWindow";
            default: throw new InvalidOperationException("No proven event mapping: " + source);
        }
    }

    public static void Apply(ProductionBrainsEnemyConversionRequest request, CombatLabClashController clashConfig = null)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Conversion is explicit edit-time work only.");
        if (clashConfig == null)
        {
            using (var source = new CombatClashAuthoring.ConfigurationScope()) Apply(request, source.Config);
            return;
        }
        if (request == null) throw new ArgumentNullException(nameof(request));
        var report = Analyze(request.character, request.sourceController);
        // There is intentionally no allow-unsafe switch. Evidence must become executable before conversion.
        if (!report.CanApply) throw new InvalidOperationException("No asset modified.\n" + string.Join("\n", report.blockers.Concat(report.conversionBlockers)));
        if (request.requiresFlameInfluence != report.requiresFlameInfluence)
            throw new InvalidOperationException("Reviewed fixed-flame requirement differs from the authored source.");
        EnsureGeneratedClashEvents(report, request.reviewedProfile, request.reviewedController, clashConfig);
        ValidateDestination(report, request.reviewedProfile, request.reviewedController, clashConfig);
        var currentPrefab = request.character.ResolveWorldPrefab();
        var currentBrain = currentPrefab.GetComponent<LitBrainsEnemy>();
        if (currentBrain != null)
        {
            if (currentBrain.combatProfile != request.reviewedProfile || currentPrefab.GetComponent<Animator>().runtimeAnimatorController != request.reviewedController)
                throw new InvalidOperationException("Existing Brains actor is not this exact reviewed conversion.");
            ValidateEventReceivers(currentPrefab.GetComponent<Animator>(), request.reviewedProfile);
            if (!currentBrain.ValidateRuntimeContract(out string reason)) throw new InvalidOperationException(reason);
            CombatClashAuthoring.EnsurePrefab(currentPrefab, clashConfig, true);
            CombatClashAuthoring.ValidateData(request.character);
            return;
        }
        var root = PrefabUtility.LoadPrefabContents(report.prefabPath);
        try
        {
            var legacy = root.GetComponent<EnemyController>();
            Transform authoredLockPoint = legacy.LockPoint;
            Object.DestroyImmediate(legacy);
            var fov = root.GetComponent<FOV>() ?? root.AddComponent<FOV>();
            fov.useExternalDetection = true;
            var brain = root.AddComponent<LitBrainsEnemy>();
            brain.lockPoint = authoredLockPoint;
            brain.fov = fov;
            brain.characterData = request.character;
            brain.combatProfile = request.reviewedProfile;
            brain.canAttack = true;
            brain.walkSpeed = request.character.enemySettings.LocomotionPositioning.walkSpeed;
            brain.runSpeed = request.character.enemySettings.LocomotionPositioning.runSpeed;
            brain.navMeshAttachmentTolerance = request.character.enemySettings.NavigationReattachTolerance;
            var flamePolicy = root.GetComponent<ProductionBrainsFlamePolicy>() ?? root.AddComponent<ProductionBrainsFlamePolicy>();
            flamePolicy.requiresFlameInfluence = request.requiresFlameInfluence;
            if (root.GetComponent<ProductionBrainsDeathLifecycle>() == null) root.AddComponent<ProductionBrainsDeathLifecycle>();
            fov.canInvestigate = false;
            fov.onDetectTarget ??= new UnityEngine.Events.UnityEvent();
            fov.onLostTarget ??= new UnityEngine.Events.UnityEvent();
            fov.onEnterNone ??= new UnityEngine.Events.UnityEvent();
            fov.onEnterAlerted ??= new UnityEngine.Events.UnityEvent();
            fov.onEnterInvestigate ??= new UnityEngine.Events.UnityEvent();
            if (root.GetComponent<CombatTimeDomain>() == null) root.AddComponent<CombatTimeDomain>();
            CombatClashAuthoring.Ensure(root, clashConfig, true);
            root.GetComponent<Animator>().runtimeAnimatorController = request.reviewedController;
            ValidateEventReceivers(root.GetComponent<Animator>(), request.reviewedProfile);
            root.GetComponent<NavMeshAgent>().enabled = false;
            if (!brain.ValidateRuntimeContract(out string reason)) throw new InvalidOperationException(reason);
            if (PrefabUtility.SaveAsPrefabAsset(root, report.prefabPath) == null) throw new InvalidOperationException("Prefab save refused.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        if (AssetDatabase.AssetPathToGUID(report.prefabPath) != report.prefabGuid ||
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(request.character)) != report.characterGuid)
            throw new InvalidOperationException("GUID invariant violated.");
        CombatClashAuthoring.ValidateData(request.character);
    }

    internal static void ValidateEventReceivers(Animator animator, LitBrainsCombatProfile profile)
    {
        if (animator == null || animator.GetComponent<LitBrainsEnemy>() == null || animator.GetComponent<LitBrainsAnimationEvents>() == null)
            throw new InvalidOperationException("The converted Animator requires its Brains actor and production motion receiver on the same object.");
        var receivers = animator.GetComponents<MonoBehaviour>();
        foreach (var attack in profile.attacks)
        foreach (var entry in AnimationUtility.GetAnimationEvents(attack.clip))
        {
            int count = receivers.Count(receiver => receiver != null && receiver.GetType().GetMethod(entry.functionName,
                new[] { typeof(AnimationEvent) }) != null);
            if (count != 1) throw new InvalidOperationException("Converted event requires exactly one receiver on the Animator: " + entry.functionName);
        }
    }

    public static void ValidateDestination(Report report, LitBrainsCombatProfile profile, AnimatorController controller,
        CombatLabClashController clashConfig = null, bool validateClash = true)
    {
        if (clashConfig == null)
        {
            using (var source = new CombatClashAuthoring.ConfigurationScope()) ValidateDestination(report, profile, controller, source.Config, validateClash);
            return;
        }
        if (profile == null || !profile.executeProductionPatterns || controller == null || controller.layers.Length != 1 || profile.productionSourceProfile != report.sourceProfile ||
            profile.attacks == null || profile.attacks.Length != report.attacks.Count)
            throw new InvalidOperationException("Explicit reviewed profile/controller with the exact source provenance and full attack set required.");
        ProductionBrainsAnimatorGraph.Validate(controller);
        var parameters = controller.parameters;
        if (!parameters.Any(parameter => parameter.name == "Attack" && parameter.type == AnimatorControllerParameterType.Trigger) ||
            !parameters.Any(parameter => parameter.name == "Attack ID" && parameter.type == AnimatorControllerParameterType.Int) ||
            !parameters.Any(parameter => parameter.name == "Dead" && parameter.type == AnimatorControllerParameterType.Bool))
            throw new InvalidOperationException("Reviewed controller lacks the explicit Brains attack/identity/death parameters.");
        var presentationStates = new List<KeyValuePair<string, AnimatorState>>();
        CollectStates(controller.layers[0].stateMachine, controller.layers[0].name, presentationStates);
        foreach (string required in new[] { profile.idleState, profile.locomotionState, profile.hurtState, profile.knockoutState, profile.deathState })
            if (string.IsNullOrWhiteSpace(required) || presentationStates.Count(entry => entry.Key == required || entry.Value.name == required) != 1)
                throw new InvalidOperationException("Reviewed controller lacks explicit presentation state: " + required);
        var sourceStates = new List<KeyValuePair<string, AnimatorState>>();
        if (report.sourceController == null) throw new InvalidOperationException("Missing source controller provenance.");
        CollectStates(report.sourceController.layers[0].stateMachine, report.sourceController.layers[0].name, sourceStates);
        var settings = report.character.enemySettings;
        var presentation = new[] {
            new KeyValuePair<string, string>(settings.ActorIdleAnimatorState, profile.idleState),
            new KeyValuePair<string, string>(settings.ActorHitAnimatorState, profile.hurtState),
            new KeyValuePair<string, string>(settings.ActorDeathAnimatorState, profile.deathState)
        };
        foreach (var entry in presentation)
        {
            var source = sourceStates.Where(state => state.Key == entry.Key || state.Value.name == entry.Key).ToArray();
            if (source.Length != 1 || source[0].Key != entry.Value ||
                presentationStates.Single(state => state.Key == entry.Value).Value.motion != source[0].Value.motion)
                throw new InvalidOperationException("Reviewed presentation changes the authored state/clip: " + entry.Key);
        }
        foreach (var mapping in report.attacks)
        {
            var matches = profile.attacks.Where(attack => attack != null && attack.skill == mapping.skill).ToArray();
            if (matches.Length != 1 || matches[0].clip == null || matches[0].stateName != mapping.statePath ||
                matches[0].reactionSeconds != mapping.reactionTime || matches[0].strikeSeconds != mapping.lockTime ||
                matches[0].impactSeconds != mapping.impactTime || matches[0].recoverySeconds != mapping.endTime)
                throw new InvalidOperationException("Reviewed attack differs from its exact production source: " + mapping.skill.name);
            var attack = matches[0];
            var expected = new LitBrainsAttackProfile {
                skill = mapping.skill, clip = attack.clip, stateName = mapping.statePath,
                reactionSeconds = mapping.reactionTime, strikeSeconds = mapping.lockTime,
                impactSeconds = mapping.impactTime, recoverySeconds = mapping.endTime,
                reach = mapping.skill.MaximumHitDistance, arcDegrees = mapping.skill.enemyImpact.arcDegrees,
                advanceDistance = mapping.skill.EnemyActionMotion.enableAdvance ? mapping.skill.EnemyActionMotion.advanceDistance : 0,
                advanceSeconds = mapping.skill.EnemyActionMotion.advanceDuration
            };
            if (JsonUtility.ToJson(attack) != JsonUtility.ToJson(expected))
                throw new InvalidOperationException("Reviewed attack changes authored geometry/motion or adds unreviewed overrides: " + mapping.skill.name);
            if (!SameAnimationCurves(mapping.clip, attack.clip))
                throw new InvalidOperationException("Reviewed clip changes authored animation curves: " + mapping.skill.name);
            var allEvents = AnimationUtility.GetAnimationEvents(attack.clip);
            var events = allEvents.Where(entry => !IsClashEvent(entry)).ToArray();
            var sourceEvents = AnimationUtility.GetAnimationEvents(mapping.clip).Where(entry => !IsClashEvent(entry)).ToArray();
            if (events.Length != sourceEvents.Length)
                throw new InvalidOperationException("Reviewed clip has unmapped/missing events: " + mapping.skill.name);
            for (int i = 0; i < sourceEvents.Length; i++)
                if (events[i].functionName != MapEvent(sourceEvents[i].functionName) || events[i].time != sourceEvents[i].time ||
                    events[i].objectReferenceParameter != sourceEvents[i].objectReferenceParameter || events[i].intParameter != sourceEvents[i].intParameter ||
                    events[i].floatParameter != sourceEvents[i].floatParameter || events[i].stringParameter != sourceEvents[i].stringParameter)
                    throw new InvalidOperationException("Reviewed event timing/payload is not exact: " + mapping.skill.name + "/" + sourceEvents[i].functionName);
            if (validateClash && !SameEvents(allEvents, ReviewedEvents(mapping, clashConfig)))
                throw new InvalidOperationException("Reviewed clip lacks the exact configured clash window or changes source events: " + mapping.skill.name);
            var states = new List<KeyValuePair<string, AnimatorState>>();
            CollectStates(controller.layers[0].stateMachine, controller.layers[0].name, states);
            var state = states.SingleOrDefault(entry => entry.Key == attack.stateName).Value;
            if (state == null || state.motion != attack.clip || state.tag != "Attack" || state.speed != 1 || state.speedParameterActive || state.timeParameterActive)
                throw new InvalidOperationException("Reviewed controller does not play the exact attack clip/state at unit speed.");
        }
    }

    private static bool IsClashEvent(AnimationEvent entry) => entry.functionName == "OpenAttackClashWindow" || entry.functionName == "CloseAttackClashWindow";

    private static AnimationEvent[] ReviewedEvents(AttackMapping mapping, CombatLabClashController config)
    {
        var events = AnimationUtility.GetAnimationEvents(mapping.clip).Where(entry => !IsClashEvent(entry)).ToList();
        foreach (var entry in events) entry.functionName = MapEvent(entry.functionName);
        if (CombatClashAuthoring.IsCompatibleEnemySkill(config, mapping.skill))
        {
            if (mapping.skill.EnemyActionMotion.IsAirborne) throw new InvalidOperationException("Airborne attack cannot author a ground clash window: " + mapping.skill.name);
            events.Add(new AnimationEvent { functionName = "OpenAttackClashWindow", time = Mathf.Max(0, mapping.impactTime - .08f) });
            events.Add(new AnimationEvent { functionName = "CloseAttackClashWindow", time = Mathf.Min(mapping.clip.length, mapping.impactTime + .06f) });
        }
        return events.OrderBy(entry => entry.time).ToArray();
    }

    private static bool SameEvents(AnimationEvent[] left, AnimationEvent[] right) => left.Length == right.Length &&
        left.Zip(right, (a, b) => a.functionName == b.functionName && a.time == b.time && a.intParameter == b.intParameter &&
            a.floatParameter == b.floatParameter && a.stringParameter == b.stringParameter &&
            a.objectReferenceParameter == b.objectReferenceParameter && a.messageOptions == b.messageOptions).All(equal => equal);

    private static void EnsureGeneratedClashEvents(Report report, LitBrainsCombatProfile profile, AnimatorController controller, CombatLabClashController config)
    {
        ValidateDestination(report, profile, controller, config, validateClash: false);
        foreach (var mapping in report.attacks)
        {
            var clip = profile.attacks.Single(attack => attack.skill == mapping.skill).clip;
            string expected = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(profile)).Replace('\\', '/') + "/" + mapping.skillGuid + ".anim";
            if (clip == mapping.clip || AssetDatabase.GetAssetPath(clip) != expected)
                throw new InvalidOperationException("Clash authoring requires the owned generated clip, never the source: " + mapping.skill.name);
            var events = ReviewedEvents(mapping, config);
            if (SameEvents(AnimationUtility.GetAnimationEvents(clip), events)) continue;
            AnimationUtility.SetAnimationEvents(clip, events);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
        }
    }

    private static bool SameAnimationCurves(AnimationClip source, AnimationClip destination)
    {
        if (source.length != destination.length || source.frameRate != destination.frameRate || source.humanMotion != destination.humanMotion) return false;
        var bindings = AnimationUtility.GetCurveBindings(source);
        if (bindings.Length != AnimationUtility.GetCurveBindings(destination).Length) return false;
        foreach (var binding in bindings)
        {
            var left = AnimationUtility.GetEditorCurve(source, binding);
            var right = AnimationUtility.GetEditorCurve(destination, binding);
            if (left == null || right == null || left.preWrapMode != right.preWrapMode || left.postWrapMode != right.postWrapMode ||
                !left.keys.SequenceEqual(right.keys)) return false;
        }
        var objects = AnimationUtility.GetObjectReferenceCurveBindings(source);
        return objects.Length == AnimationUtility.GetObjectReferenceCurveBindings(destination).Length && objects.All(binding =>
            AnimationUtility.GetObjectReferenceCurve(source, binding).SequenceEqual(AnimationUtility.GetObjectReferenceCurve(destination, binding) ?? Array.Empty<ObjectReferenceKeyframe>()));
    }
}
