using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Explicit, preflighted migration. Never runs on import or resets jump/dodge tuning.</summary>
public static class PlayerInPlaceMigration
{
    public const string Folder = "Assets/Characters/1_Squad/Lucian/Animation/PlayerInPlace";
    public const string LibraryPath = Folder + "/PlayerStateMotions.asset";
    public const string ManifestPath = Folder + "/Editor/MigrationManifest.json";
    public static AnimationClip ResolveReplacement(AnimationClip source)
    {
        if (File.Exists(ManifestPath))
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id);
            var entry = manifest.replacements.FirstOrDefault(r => r.sourceGuid == guid && r.sourceId == id);
            if (entry != null)
                return AssetDatabase.LoadAllAssetsAtPath(entry.targetPath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        }
        if (!PlayerInPlaceAudit.IsRootCandidate(source)) return source;
        throw new InvalidOperationException("Run Migrate Player Gameplay To InPlace before installing this state: " + source.name);
    }
    private static readonly string[] PlayerPrefabs = {
        PlayerInPlaceAudit.LucianPath,
        "Assets/Characters/1_Squad/Link/Player_Model_Link.prefab",
        "Assets/Characters/1_Squad/Luna/Player_Model_Luna.prefab",
        "Assets/Characters/1_Squad/Mia/Player_Model_Mia.prefab"
    };
    [Serializable] public sealed class Record
    {
        public string sourceGuid, sourcePath, targetPath, sourceEvents, targetEvents;
        public long sourceId;
        public string[] consumers;
        public float sourceDistance, sourceYaw, residualDistance, residualYaw;
    }
    [Serializable] public sealed class Fingerprint { public string path, hash; }
    [Serializable] public sealed class Manifest
    {
        public Record[] replacements;
        public Fingerprint[] protectedFiles;
        public string[] preexistingWarnings;
        public string jumpBefore, jumpAfter, dodgeBefore, dodgeAfter;
    }
    private sealed class Prepared
    {
        public AnimationClip source, target;
        public bool create;
        public PlayerInPlaceSampling.Samples sample;
        public Record record;
    }
    [Serializable] private sealed class PreflightManifest
    {
        public Record[] replacements;
        public Fingerprint[] inputs;
    }
    private const string PreflightPath = "Library/PlayerInPlacePreflight.json";

    public static void OrganizeLucianAnimations() => LucianAnimationFolderUtility.Organize();

    public static void Preflight() => RunMigration(false);

    [MenuItem("Lit/Animation/Migrate Player Gameplay To InPlace")]
    public static void Migrate() => RunMigration(true);

    private static void RunMigration(bool apply)
    {
        var temporaryClips = new List<AnimationClip>();
        try { MigrateCore(temporaryClips, apply); }
        finally
        {
            foreach (var clip in temporaryClips)
                if (clip != null && !AssetDatabase.Contains(clip)) Object.DestroyImmediate(clip);
        }
    }

    private static void MigrateCore(List<AnimationClip> temporaryClips, bool apply)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before migration.");
        var originalManifest = File.Exists(ManifestPath) ? JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)) : null;
        if (originalManifest != null) VerifyProtected(originalManifest);
        var warnings = new List<string>();
        var consumers = PlayerInPlaceAudit.Collect(warnings);
        if (originalManifest != null && warnings.Except(originalManifest.preexistingWarnings).Any())
            throw new InvalidOperationException("Resolve new missing references before migration: " + string.Join("; ", warnings.Except(originalManifest.preexistingWarnings)));
        var pending = new List<Prepared>();
        var sourcePaths = AssetDatabase.GetAllAssetPaths();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerInPlaceAudit.ControllerPath);
        var skills = PlayerSkills().ToArray();
        var sequences = PlayerInPlaceAudit.PlayerSequences().ToArray();
        // Capture override keys before changing the underlying controller's motions.
        var overrideSnapshots = PlayerInPlaceAudit.PlayerOverrides().ToDictionary(o => o, o => {
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            o.GetOverrides(pairs);
            return pairs;
        });
        foreach (string prefabPath in PlayerPrefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null || prefab.GetComponent<LitOpsiveLocomotionBridge>() == null ||
                prefab.GetComponent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>() == null)
                throw new InvalidOperationException("Player motion dependencies missing: " + prefabPath);
        }
        var protectedFiles = consumers.Keys.Where(c => !PlayerInPlaceAudit.IsRootCandidate(c))
            .Select(AssetDatabase.GetAssetPath).Where(File.Exists).Distinct().Select(p => new Fingerprint { path = p, hash = Hash(p) }).ToList();
        const string jumpScript = "Assets/Characters/6_UCC_Opsive/OpsiveIntegration/PlayerScriptedJumpController.cs";
        protectedFiles.Add(new Fingerprint { path = jumpScript, hash = Hash(jumpScript) });
        string jumpBefore = ProtectedJump();
        string dodgeBefore = ProtectedDodge();

        using (var sampler = new PlayerInPlaceSampling())
        {
            // Finish every conversion and verify every candidate before persisting anything.
            foreach (var pair in consumers)
            {
                var source = pair.Key;
                var sample = sampler.Sample(source);
                if (!PlayerInPlaceAudit.IsRootCandidate(source) && !HasPhysicalTrajectory(sample)) continue;
                if (IsProtectedJumpClip(source, controller)) continue; // The user explicitly protects the complete existing jump.
                var path = AssetDatabase.GetAssetPath(source);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id);
                string equivalentPath = PlayerInPlaceAudit.FindEquivalent(path, sourcePaths);
                var equivalents = string.IsNullOrEmpty(equivalentPath) ? Array.Empty<AnimationClip>() :
                    AssetDatabase.LoadAllAssetsAtPath(equivalentPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                if (equivalents.Length > 1) throw new InvalidOperationException("Ambiguous InPlace subclips: " + equivalentPath);
                AnimationClip target = equivalents.SingleOrDefault();
                bool create = target == null || AnimationUtility.GetAnimationEvents(source).Length > 0 ||
                              Mathf.Abs(target.length - source.length) > .001f;
                if (create)
                {
                    target = Object.Instantiate(source);
                    temporaryClips.Add(target);
                    target.name = source.name + "_Inplace";
                    PlayerInPlaceSampling.Neutralize(target, sample);
                }
                var residual = sampler.Sample(target);
                if (residual.MaxDisplacement > .01f || residual.MaxYaw > .1f)
                {
                    // Some vendor InPlace clips retain root curves. Fix a dedicated copy only.
                    if (!create) { target = Object.Instantiate(source); temporaryClips.Add(target); target.name = source.name + "_Inplace"; create = true; }
                    PlayerInPlaceSampling.Neutralize(target, sample);
                    residual = sampler.Sample(target);
                }
                if (residual.MaxDisplacement > .01f || residual.MaxYaw > .1f)
                {
                    if (create) Object.DestroyImmediate(target);
                    throw new InvalidOperationException("Root trajectory remains in converted clip: " + path + " (" + residual.MaxDisplacement + " m, " + residual.MaxYaw + " deg)");
                }
                string targetPath = create ? Folder + "/" + SafeName(source.name) + "_" + guid.Substring(0, 8) + "_Inplace.anim" : equivalentPath;
                if (create && AssetDatabase.LoadMainAssetAtPath(targetPath) != null)
                    throw new InvalidOperationException("Output already exists without a migrated reference: " + targetPath);
                pending.Add(new Prepared { source = source, target = target, create = create, sample = sample,
                    record = new Record { sourceGuid = guid, sourceId = id, sourcePath = path, targetPath = targetPath,
                        consumers = pair.Value.ToArray(), sourceDistance = sample.Distance, sourceYaw = sample.MaxYaw,
                        residualDistance = residual.MaxDisplacement, residualYaw = residual.MaxYaw,
                        sourceEvents = Events(source), targetEvents = Events(target) } });
            }
        }
        if (pending.Count == 0)
        {
            Validate();
            Debug.Log("[Player InPlace] Already migrated; no assets written.");
            return;
        }
        foreach (var item in pending)
            if (item.record.sourceEvents != item.record.targetEvents)
                throw new InvalidOperationException("Animation events differ: " + item.record.sourcePath);

        var library = AssetDatabase.LoadAssetAtPath<PlayerStateMotionLibrary>(LibraryPath) ?? ScriptableObject.CreateInstance<PlayerStateMotionLibrary>();
        var profiles = new List<PlayerStateMotionLibrary.Profile>(library.profiles);
        var byClip = pending.ToDictionary(p => p.source);
        foreach (var layer in controller.layers)
            PlayerInPlaceAudit.Visit(layer.stateMachine, layer.name, (state, statePath) => {
                if (!(state.motion is AnimationClip clip) || !byClip.TryGetValue(clip, out var item) || !NeedsTrajectory(statePath)) return;
                var skill = skills.FirstOrDefault(s => s.animatorState == statePath || s.animationClip == clip);
                if (skill != null && !NeedsSkillTrajectory(skill)) return;
                var sample = item.sample;
                if (sample.MaxDisplacement <= .005f && sample.MaxYaw <= .1f) return;
                // The previous player bridge applied this multiplier to authored actions.
                float multiplier = LegacyFloat("rootMotionSpeedMultiplier", 1.04f);
                bool visualFacing = skill != null && skill.presentation.facingMode == PlayerActionFacingMode.VisualOnly;
                profiles.Add(new PlayerStateMotionLibrary.Profile {
                    statePath = statePath, duration = sample.duration,
                    localX = PlayerInPlaceSampling.Curve(sample.positions.Select(p => p.x * multiplier).ToArray()),
                    localZ = PlayerInPlaceSampling.Curve(sample.positions.Select(p => p.z * multiplier).ToArray()),
                    yaw = PlayerInPlaceSampling.Curve(sample.yaw.Select(v => visualFacing ? 0f : v).ToArray()),
                    allowAirborne = LegacyAirborne(skill), initialUpwardSpeed = 0f
                });
            });

        // Backups include the user's working-tree content, never a checkout of HEAD.
        var backupPaths = new HashSet<string>(PlayerPrefabs) { PlayerInPlaceAudit.ControllerPath };
        foreach (var s in skills) backupPaths.Add(AssetDatabase.GetAssetPath(s));
        foreach (var s in sequences) backupPaths.Add(AssetDatabase.GetAssetPath(s));
        foreach (var s in overrideSnapshots.Keys) backupPaths.Add(AssetDatabase.GetAssetPath(s));
        backupPaths.Add(LibraryPath);
        backupPaths.Add(ManifestPath);
        foreach (var file in protectedFiles) backupPaths.Add(file.path);
        foreach (var item in pending) backupPaths.Add(item.record.sourcePath);
        var inputs = backupPaths.Where(File.Exists).SelectMany(p => File.Exists(p + ".meta") ? new[] { p, p + ".meta" } : new[] { p })
            .Distinct().OrderBy(p => p).Select(p => new Fingerprint { path = p, hash = Hash(p) }).ToArray();
        var preflight = new PreflightManifest { replacements = pending.Select(p => p.record).ToArray(), inputs = inputs };
        string preflightJson = JsonUtility.ToJson(preflight, true);
        if (!apply)
        {
            File.WriteAllText(PreflightPath, preflightJson);
            if (!AssetDatabase.Contains(library)) Object.DestroyImmediate(library);
            Debug.Log("[Player InPlace] Preflight complete; review " + PreflightPath + " before running migration.");
            return;
        }
        if (!File.Exists(PreflightPath) || File.ReadAllText(PreflightPath) != preflightJson)
            throw new InvalidOperationException("Run Audit Player InPlace and review the complete preflight manifest before applying. Inputs or replacements have changed.");
        string backup = "Library/PlayerInPlaceBackup/" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        foreach (string path in backupPaths.Where(File.Exists))
        {
            string copy = backup + "/" + path;
            Directory.CreateDirectory(Path.GetDirectoryName(copy));
            File.Copy(path, copy, false);
        }
        EnsureFolder(Folder);
        EnsureFolder(Folder + "/Editor");
        library.profiles = profiles.ToArray();
        foreach (var item in pending.Where(p => p.create)) AssetDatabase.CreateAsset(item.target, item.record.targetPath);
        if (!AssetDatabase.Contains(library)) AssetDatabase.CreateAsset(library, LibraryPath);
        else EditorUtility.SetDirty(library);
        foreach (var layer in controller.layers)
            PlayerInPlaceAudit.Visit(layer.stateMachine, layer.name, (state, _) => {
                var replacement = ReplaceMotion(state.motion, byClip);
                bool changed = state.motion != replacement;
                if (changed) state.motion = replacement;
                if (state.tag == "RealTimeCombatRootMotion") { state.tag = "RealTimeCombatInPlace"; changed = true; }
                if (changed) EditorUtility.SetDirty(state);
            });
        EditorUtility.SetDirty(controller);
        foreach (var skill in skills)
        {
            bool changed = false;
            if (skill.animationClip != null && byClip.TryGetValue(skill.animationClip, out var replacement))
            { skill.animationClip = replacement.target; changed = true; }
            var so = new SerializedObject(skill);
            var legacy = so.FindProperty("presentation.rootMotionMode");
            if (legacy != null)
            {
                skill.presentation.movementPolicy = legacy.intValue == 1 && NeedsSkillTrajectory(skill)
                    ? PlayerActionMovementPolicy.StateTrajectory
                    : legacy.intValue == 2 ? PlayerActionMovementPolicy.ExistingScripted : PlayerActionMovementPolicy.VisualOnly;
                changed = true;
            }
            if (changed) EditorUtility.SetDirty(skill);
        }
        foreach (var sequence in sequences)
        {
            bool changed = false;
            foreach (var step in sequence.steps.Where(step => step != null))
            {
                if (step.qtePresentationClip != null && byClip.TryGetValue(step.qtePresentationClip, out var qte))
                { step.qtePresentationClip = qte.target; changed = true; }
                if (step.successPlayerAnimationClip != null && byClip.TryGetValue(step.successPlayerAnimationClip, out var success))
                { step.successPlayerAnimationClip = success.target; changed = true; }
            }
            if (changed) EditorUtility.SetDirty(sequence);
        }
        foreach (var snapshot in overrideSnapshots)
        {
            var overrides = snapshot.Key;
            var pairs = snapshot.Value;
            bool changed = false;
            for (int i = 0; i < pairs.Count; i++)
            {
                var key = byClip.TryGetValue(pairs[i].Key, out var baseEntry) ? baseEntry.target : pairs[i].Key;
                var value = pairs[i].Value != null && byClip.TryGetValue(pairs[i].Value, out var entry) ? entry.target : pairs[i].Value;
                if (key != pairs[i].Key || value != pairs[i].Value) changed = true;
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(key, value);
            }
            if (changed) { overrides.ApplyOverrides(pairs); EditorUtility.SetDirty(overrides); }
        }
        foreach (string path in PlayerPrefabs) ConfigurePrefab(path, library);
        AssetDatabase.SaveAssets();
        var manifest = new Manifest { replacements = pending.Select(p => p.record).ToArray(), protectedFiles = protectedFiles.ToArray(),
            preexistingWarnings = warnings.ToArray(), jumpBefore = jumpBefore, jumpAfter = ProtectedJump(),
            dodgeBefore = dodgeBefore, dodgeAfter = ProtectedDodge() };
        if (originalManifest != null)
        {
            manifest.replacements = originalManifest.replacements.Concat(manifest.replacements).ToArray();
            manifest.protectedFiles = originalManifest.protectedFiles;
            manifest.jumpBefore = originalManifest.jumpBefore;
            manifest.dodgeBefore = originalManifest.dodgeBefore;
        }
        File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest, true));
        AssetDatabase.ImportAsset(ManifestPath);
        VerifyProtected(manifest);
        Debug.Log($"[Player InPlace] Migrated {pending.Count} clips, {profiles.Count} script trajectories. Jump/dodge unchanged. Backup: {backup}");
    }

    public static IEnumerable<SkillSO> PlayerSkills() => AssetDatabase.FindAssets("t:SkillSO", new[] { "Assets/CombatRealTime/Skills" })
        .Select(g => AssetDatabase.LoadAssetAtPath<SkillSO>(AssetDatabase.GUIDToAssetPath(g))).Where(s => s != null);
    private static bool LegacyAirborne(SkillSO skill) => skill != null &&
        new SerializedObject(skill).FindProperty("presentation.allowAirborneRootMotion")?.boolValue == true;
    private static bool NeedsSkillTrajectory(SkillSO skill)
    {
        var legacy = new SerializedObject(skill).FindProperty("presentation.rootMotionMode");
        return (legacy != null ? legacy.intValue == 1 : skill.presentation.movementPolicy == PlayerActionMovementPolicy.StateTrajectory) &&
               !(skill.targetLunge != null && skill.targetLunge.enabled);
    }
    public static bool NeedsTrajectory(string path)
    {
        string name = path.Substring(path.LastIndexOf('.') + 1);
        return path.StartsWith("Base Layer.", StringComparison.Ordinal) && !path.Contains("/") && !name.Contains("Dodge") && !name.Contains("Locomotion") &&
            !name.Contains("Idle") && !name.Contains("Jump") && !name.StartsWith("Walk") && !name.StartsWith("Run") &&
            !name.StartsWith("Turn_") && name != "Falling" && !name.StartsWith("Landing") && name != "Death";
    }
    private static Motion ReplaceMotion(Motion motion, Dictionary<AnimationClip, Prepared> replacements)
    {
        if (motion is AnimationClip clip && replacements.TryGetValue(clip, out var replacement)) return replacement.target;
        if (motion is BlendTree tree)
        {
            var children = tree.children;
            bool changed = false;
            for (int i = 0; i < children.Length; i++)
            {
                var next = ReplaceMotion(children[i].motion, replacements);
                if (next != children[i].motion) { children[i].motion = next; changed = true; }
            }
            if (changed) { tree.children = children; EditorUtility.SetDirty(tree); }
        }
        return motion;
    }
    private static void ConfigurePrefab(string path, PlayerStateMotionLibrary library)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var bridge = root.GetComponent<LitOpsiveLocomotionBridge>();
            if (bridge == null) throw new InvalidOperationException("UCC bridge absent: " + path);
            var actor = root.GetComponent<CombatActorAnimationRoot>();
            var animator = actor != null ? actor.Animator : root.GetComponent<Animator>();
            if (animator == null) throw new InvalidOperationException("Gameplay Animator absent: " + path);
            var motion = root.GetComponent<PlayerStateMotionController>() ?? root.AddComponent<PlayerStateMotionController>();
            motion.Library = library;
            foreach (var recovery in root.GetComponents<AnimationGroundRecovery>()) Object.DestroyImmediate(recovery);
            animator.applyRootMotion = false;
            var so = new SerializedObject(bridge);
            var legacyLocomotion = so.FindProperty("useRootMotionLocomotion");
            bool wasRoot = legacyLocomotion?.boolValue == true;
            if (legacyLocomotion != null && !wasRoot)
                so.FindProperty("adaptMovingGroundRelief").boolValue = false;
            if (wasRoot)
            {
                // Keep the same physical tuning and animator speed conversion when removing the old branch.
                var blend = so.FindProperty("groundedRootMotionSpeedToBlend");
                if (blend != null) so.FindProperty("groundedAnimationSpeedToBlend").floatValue = blend.floatValue;
                so.FindProperty("tuneGroundedUccPhysics").boolValue = false;
            }
            foreach (string field in new[] { "useRootMotionLocomotion", "preserveAnimatorRootMotion", "restoreRootMotionSettingsOnDisable" })
            { var p = so.FindProperty(field); if (p != null) p.boolValue = false; }
            so.ApplyModifiedPropertiesWithoutUndo();
            var ucc = root.GetComponent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>();
            ucc.UseRootMotionPosition = false;
            ucc.UseRootMotionRotation = false;
            if (!(ucc.Abilities ?? Array.Empty<Opsive.UltimateCharacterController.Character.Abilities.Ability>()).Any(a => a is LitUccStateMotionAbility))
                ucc.Abilities = (ucc.Abilities ?? Array.Empty<Opsive.UltimateCharacterController.Character.Abilities.Ability>()).Concat(new[] { new LitUccStateMotionAbility() }).ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static float LegacyFloat(string name, float fallback)
    {
        var b = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerInPlaceAudit.LucianPath).GetComponent<LitOpsiveLocomotionBridge>();
        return new SerializedObject(b).FindProperty(name)?.floatValue ?? fallback;
    }
    private static string ProtectedJump() => string.Join("\n", PlayerPrefabs.Select(p =>
        JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<PlayerScriptedJumpController>())));
    private static string ProtectedDodge()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Core/System/GameplaySessionRoot.prefab");
        return JsonUtility.ToJson(root.GetComponentInChildren<PlayerScriptedDodgeController>(true)) + "\n" +
               JsonUtility.ToJson(root.GetComponentInChildren<CombatMobilityController>(true));
    }
    public static string Hash(string path)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
    private static string Events(AnimationClip clip) => string.Join("\n", AnimationUtility.GetAnimationEvents(clip).Select(e =>
        $"{e.time:R}|{e.functionName}|{e.stringParameter}|{e.floatParameter:R}|{e.intParameter}|{AssetDatabase.GetAssetPath(e.objectReferenceParameter)}|{e.messageOptions}"));
    private static string SafeName(string name) => string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    private static void VerifyProtected(Manifest manifest)
    {
        foreach (var f in manifest.protectedFiles)
            if (Hash(f.path) != f.hash) throw new InvalidOperationException("Protected file changed: " + f.path);
        if (NormalizeReferences(manifest.jumpBefore) != NormalizeReferences(ProtectedJump()) || NormalizeReferences(manifest.dodgeBefore) != NormalizeReferences(ProtectedDodge()))
            throw new InvalidOperationException("Protected jump/dodge component changed");
    }
    private static string NormalizeReferences(string json) => System.Text.RegularExpressions.Regex.Replace(json, "\"instanceID\":-?[0-9]+", "\"instanceID\":0");
    public static bool HasPhysicalTrajectory(PlayerInPlaceSampling.Samples sample) => sample.MaxDisplacement > .05f || sample.MaxYaw > .5f;
    private static bool IsProtectedJumpClip(AnimationClip clip, AnimatorController controller)
    {
        bool found = false;
        foreach (var layer in controller.layers)
            PlayerInPlaceAudit.Visit(layer.stateMachine, layer.name, (state, _) => {
                if (state.motion == clip && new[] { "Jump_Start", "Jump_Loop", "Falling", "Jump_End", "Landing", "Landing_Hard" }.Contains(state.name)) found = true;
            });
        return found;
    }
    [MenuItem("Lit/Animation/Validate Player Gameplay InPlace")]
    public static void Validate()
    {
        var warnings = new List<string>();
        var clips = PlayerInPlaceAudit.Collect(warnings).Keys;
        var remaining = clips.Where(PlayerInPlaceAudit.IsRootCandidate).ToArray();
        if (remaining.Length != 0) throw new InvalidOperationException("Root gameplay clips: " + string.Join(", ", remaining.Select(c => c.name)));
        if (!File.Exists(ManifestPath)) throw new InvalidOperationException("Migration manifest missing");
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
        VerifyProtected(manifest);
        var issues = new List<string>();
        foreach (string warning in warnings.Except(manifest.preexistingWarnings)) issues.Add(warning);
        using (var sampler = new PlayerInPlaceSampling())
            foreach (var clip in clips)
                if (!IsProtectedJumpClip(clip, AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerInPlaceAudit.ControllerPath)) &&
                    HasPhysicalTrajectory(sampler.Sample(clip))) issues.Add("Physical root trajectory: " + AssetDatabase.GetAssetPath(clip));
        foreach (var record in manifest.replacements)
        {
            var source = AssetDatabase.LoadAllAssetsAtPath(record.sourcePath).OfType<AnimationClip>().FirstOrDefault(c => {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c, out string _, out long id); return id == record.sourceId;
            });
            var target = AssetDatabase.LoadAllAssetsAtPath(record.targetPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (source == null || target == null) { issues.Add("Missing migration clip: " + record.sourcePath); continue; }
            if (Events(source) != Events(target)) issues.Add("Changed animation events: " + record.targetPath);
            if (Mathf.Abs(source.length - target.length) > .001f) issues.Add("Changed clip duration: " + record.targetPath);
        }
        foreach (var path in PlayerPrefabs) ValidatePlayer(AssetDatabase.LoadAssetAtPath<GameObject>(path), issues);
        var library = AssetDatabase.LoadAssetAtPath<PlayerStateMotionLibrary>(LibraryPath);
        if (library == null) issues.Add("Missing state motion library");
        else
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerInPlaceAudit.ControllerPath);
            var states = new HashSet<string>();
            foreach (var layer in controller.layers) PlayerInPlaceAudit.Visit(layer.stateMachine, layer.name, (state, path) => {
                states.Add(path);
                if (state.tag == "RealTimeCombatRootMotion") issues.Add("Root gameplay tag: " + path);
            });
            foreach (var profile in library.profiles)
            {
                if (!states.Contains(profile.statePath)) issues.Add("Orphan trajectory: " + profile.statePath);
                if (!float.IsFinite(profile.duration) || profile.duration <= 0f || profile.localX == null || profile.localZ == null || profile.yaw == null)
                    issues.Add("Invalid trajectory: " + profile.statePath);
                if (!NeedsTrajectory(profile.statePath)) issues.Add("Trajectory conflicts with existing scripted motion: " + profile.statePath);
            }
            if (library.profiles.Select(p => p.statePath).Distinct().Count() != library.profiles.Length) issues.Add("Duplicate state trajectories");
            foreach (var skill in PlayerSkills().Where(s => s.presentation.movementPolicy == PlayerActionMovementPolicy.StateTrajectory))
                if (library.Find(Animator.StringToHash(skill.animatorState)) == null) issues.Add("Missing skill trajectory: " + skill.name);
        }
        if (issues.Count != 0) throw new InvalidOperationException(string.Join("\n", issues));
        Debug.Log("[Player InPlace] Contract valid.");
    }

    public static void ValidatePlayer(GameObject root, List<string> issues)
    {
        if (root == null) { issues.Add("Missing player prefab"); return; }
        var actor = root.GetComponent<CombatActorAnimationRoot>();
        var ucc = root.GetComponent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>();
        var motion = root.GetComponent<PlayerStateMotionController>();
        var animator = actor != null ? actor.Animator : root.GetComponent<Animator>();
        if (animator == null || animator.applyRootMotion) issues.Add(root.name + ": automatic root motion enabled or Animator missing");
        if (ucc == null || ucc.UseRootMotionPosition || ucc.UseRootMotionRotation) issues.Add(root.name + ": UCC Root policy enabled");
        if (motion == null || motion.Library == null || !motion.enabled) issues.Add(root.name + ": state motion controller missing");
        if (ucc != null && (ucc.Abilities == null || ucc.Abilities.Count(a => a is LitUccStateMotionAbility) != 1))
            issues.Add(root.name + ": expected one UCC state motion ability");
        if (ucc != null && ucc.Abilities != null && ucc.Abilities.Any(a => a != null &&
            (a.UseRootMotionPosition == Opsive.UltimateCharacterController.Character.Abilities.Ability.AbilityBoolOverride.True ||
             a.UseRootMotionRotation == Opsive.UltimateCharacterController.Character.Abilities.Ability.AbilityBoolOverride.True)))
            issues.Add(root.name + ": ability can reactivate gameplay Root motion");
    }

    public static void FinalizeSerializedData()
    {
        var library = AssetDatabase.LoadAssetAtPath<PlayerStateMotionLibrary>(LibraryPath);
        foreach (string path in PlayerPrefabs) ConfigurePrefab(path, library);
        foreach (var skill in PlayerSkills()) EditorUtility.SetDirty(skill);
        AssetDatabase.SaveAssets();
        Validate();
    }

    public static void RebuildDedicatedCopies()
    {
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
        VerifyProtected(manifest);
        var prepared = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        try
        {
            using (var sampler = new PlayerInPlaceSampling())
            foreach (var record in manifest.replacements.Where(r => r.targetPath.StartsWith(Folder + "/")))
            {
                var source = AssetDatabase.LoadAllAssetsAtPath(record.sourcePath).OfType<AnimationClip>().Single(c => {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c, out string _, out long id); return id == record.sourceId;
                });
                var target = AssetDatabase.LoadAssetAtPath<AnimationClip>(record.targetPath);
                var copy = Object.Instantiate(source);
                prepared.Add(new KeyValuePair<AnimationClip, AnimationClip>(target, copy));
                copy.name = target.name;
                PlayerInPlaceSampling.Neutralize(copy, sampler.Sample(source));
                var residual = sampler.Sample(copy);
                if (residual.MaxDisplacement > .01f || residual.MaxYaw > .1f || Events(source) != Events(copy))
                    throw new InvalidOperationException("Dedicated copy preflight failed: " + record.targetPath);
            }
            foreach (var pair in prepared)
            {
                EditorUtility.CopySerialized(pair.Value, pair.Key);
                EditorUtility.SetDirty(pair.Key);
            }
            AssetDatabase.SaveAssets();
            Validate();
        }
        finally { foreach (var pair in prepared) Object.DestroyImmediate(pair.Value); }
    }

    // One-shot completion for the three shared consumers: the removed Root gate previously
    // kept their relief adaptation inactive. This preserves their pre-migration behavior.
    public static void PreserveCompanionGroundRelief()
    {
        foreach (var path in PlayerPrefabs.Where(p => p != PlayerInPlaceAudit.LucianPath))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var so = new SerializedObject(root.GetComponent<LitOpsiveLocomotionBridge>());
                so.FindProperty("adaptMovingGroundRelief").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Validate();
    }

    public static void PrunePresentationOnlyProfiles()
    {
        // The flame presentation layer receives InPlace clips for the complete graph contract,
        // without introducing a new physical movement command for equipping a flame.
        var library = AssetDatabase.LoadAssetAtPath<PlayerStateMotionLibrary>(LibraryPath);
        library.profiles = library.profiles.Where(p => !p.statePath.StartsWith("Upper Body Flame.", StringComparison.Ordinal)).ToArray();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Validate();
    }
}
