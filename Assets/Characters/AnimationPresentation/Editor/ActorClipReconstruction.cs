using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Two-phase, source-based reconstruction. Never reads flattened target curves as a source.</summary>
public static class ActorClipReconstruction
{
    [Serializable] public sealed class Item
    {
        public string target, source, sourceHash, targetHash, eventHash, status;
        public long sourceFileId;
        public float maximumPoseError, maximumAngleError, physicalDistance, physicalYaw;
        public float verticalPoseOffset;
    }
    [Serializable] public sealed class Manifest { public string actorHash; public List<Item> items = new List<Item>(); }
    public const string ManifestPath = ActorAnimationAudit.ReportFolder + "/reconstruction.json";

    public static AnimationClip Candidate(AnimationClip source, AnimationClip target, Animator actor,float verticalPoseOffset = 0)
    {
        if (source == null || target == null || !source.isHumanMotion || !target.isHumanMotion)
            throw new InvalidOperationException("Resolved Humanoid source and target required.");
        if (Mathf.Abs(source.length - target.length) > 1f / Mathf.Max(1f, source.frameRate))
            throw new InvalidOperationException("Source/target duration mismatch: " + target.name);
        var candidate = UnityEngine.Object.Instantiate(source);
        try
        {
            candidate.name = target.name;
            var settings = AnimationUtility.GetAnimationClipSettings(candidate);
            settings.loopTime = AnimationUtility.GetAnimationClipSettings(target).loopTime;
            AnimationUtility.SetAnimationClipSettings(candidate, settings);
            AnimationUtility.SetAnimationEvents(candidate, AnimationUtility.GetAnimationEvents(target));
            var sourceBindings = new HashSet<EditorCurveBinding>(AnimationUtility.GetCurveBindings(source));
            foreach (var binding in AnimationUtility.GetCurveBindings(target))
                if (!sourceBindings.Contains(binding) && !binding.propertyName.StartsWith("RootT.") &&
                    !binding.propertyName.StartsWith("RootQ.") && !binding.propertyName.StartsWith("MotionT.") &&
                    !binding.propertyName.StartsWith("MotionQ."))
                    AnimationUtility.SetEditorCurve(candidate, binding, AnimationUtility.GetEditorCurve(target,binding));
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(target))
                AnimationUtility.SetObjectReferenceCurve(candidate, binding, AnimationUtility.GetObjectReferenceCurve(target,binding));
            // Inplace sources usually have no physical trajectory. Do not rewrite their body pose.
            using (var sampling = new PlayerInPlaceSampling(actor))
            {
                var motion = sampling.Sample(source);
                if (motion.MaxDisplacement > .001f || motion.MaxYaw > .05f)
                    PlayerInPlaceSampling.Neutralize(candidate, motion);
                ActorRigGrounding.ApplyPoseOffset(candidate,motion.humanScale,verticalPoseOffset);
            }
            return candidate;
        }
        catch { UnityEngine.Object.DestroyImmediate(candidate); throw; }
    }

    public static string Events(AnimationClip clip) => Hash128.Compute(string.Join("\n",
        ActorAnimationAudit.EventDetails(clip).Select(e => JsonUtility.ToJson(e)))).ToString();

    public static float PoseError(Animator actor, AnimationClip source, AnimationClip copy)
        => Compare(actor,source,copy).positionError;
    public struct PoseComparison { public float positionError, angleError; }
    public static PoseComparison Compare(Animator actor, AnimationClip source, AnimationClip copy,float verticalPoseOffset = 0)
    {
        using (var sampler = new ActorPoseSampler(actor))
        {
            var original = sampler.Sample(source, true);
            var repaired = sampler.Sample(copy, false,original.Length - 1);
            var comparison = new PoseComparison();
            for (int i = 0; i < original.Length; i++)
            {
                var a = original[i]; var b = repaired[i];
                // Compare in the source's co-moving frame; body bob/lean must survive.
                var inverse = Quaternion.Inverse(a.rotation);
                for (int bone = 0; bone < a.bonePositions.Length; bone++)
                {
                    if (a.bonePositions[bone] == Vector3.zero && b.bonePositions[bone] == Vector3.zero) continue;
                    comparison.positionError = Mathf.Max(comparison.positionError,
                        Vector3.Distance(inverse * (a.bonePositions[bone] - a.root) + Vector3.up * verticalPoseOffset, b.bonePositions[bone]));
                    comparison.angleError = Mathf.Max(comparison.angleError,
                        Quaternion.Angle(inverse * a.boneRotations[bone],b.boneRotations[bone]));
                }
            }
            return comparison;
        }
    }

    [MenuItem("Lit/Animation Presentation/Prevalidate Juggernaut Reconstruction")]
    public static void Prevalidate()
    {
        Directory.CreateDirectory(ActorAnimationAudit.ReportFolder);
        var manifest = new Manifest(); var actor = ActorAnimationAudit.Actor(true);
        manifest.actorHash = AssetDatabase.GetAssetDependencyHash(JuggernautV2Setup.PrefabPath).ToString();
        float verticalOffset = 0; // A failed/unstable reference support never justifies a guessed pose-height correction.
        foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { ActorAnimationAudit.EnemyFolder + "Animations" }))
        {
            var targetPath = AssetDatabase.GUIDToAssetPath(guid);
            var target = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);
            string sourcePath = ActorAnimationAudit.EnemySource(target.name.Replace("Juggernaut_v2_", ""));
            if (sourcePath == null) continue; // No provenance guessed for unrelated clips.
            var item = new Item { target = targetPath, source = sourcePath,
                sourceHash = AssetDatabase.GetAssetDependencyHash(sourcePath).ToString(),
                targetHash = AssetDatabase.GetAssetDependencyHash(targetPath).ToString(), eventHash = Events(target),verticalPoseOffset=verticalOffset };
            try
            {
                var source = ActorAnimationAudit.Source(sourcePath);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string _, out item.sourceFileId);
                var candidate = Candidate(source, target, actor,verticalOffset);
                try
                {
                    var comparison = Compare(actor,source,candidate,verticalOffset);
                    item.maximumPoseError = comparison.positionError; item.maximumAngleError = comparison.angleError;
                    using (var sampling = new PlayerInPlaceSampling(actor))
                    {
                        var motion = sampling.Sample(candidate);
                        item.physicalDistance = motion.MaxDisplacement; item.physicalYaw = motion.MaxYaw;
                    }
                    using (var sampler = new ActorPoseSampler(actor))
                        ActorAnimationAudit.WriteFrames(target.name + "_candidate", sampler.Sample(candidate));
                    item.status = item.maximumPoseError <= .005f && item.maximumAngleError <= .5f && item.physicalDistance <= .005f && item.physicalYaw <= .1f
                        && Events(candidate) == item.eventHash ? "Validated" : "Blocked: pose or physical trajectory differs";
                }
                finally { UnityEngine.Object.DestroyImmediate(candidate); }
            }
            catch (Exception e) { item.status = "Blocked: " + e.Message; }
            manifest.items.Add(item);
        }
        File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest, true));
        if (manifest.items.Count == 0 || manifest.items.Any(i => i.status != "Validated"))
            throw new InvalidOperationException("Reconstruction blocked. See " + ManifestPath);
        Debug.Log("[ActorClipReconstruction] Prevalidated " + manifest.items.Count + " original-source reconstructions.");
    }

    [MenuItem("Lit/Animation Presentation/Apply Validated Juggernaut Manifest")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
        if (manifest.items.Count == 0 || manifest.actorHash != AssetDatabase.GetAssetDependencyHash(JuggernautV2Setup.PrefabPath).ToString())
            throw new InvalidOperationException("Actor changed since prevalidation; regenerate the manifest.");
        var candidates = new List<AnimationClip>();
        try
        {
            // Build and validate every candidate before the first asset write.
            foreach (var item in manifest.items)
            {
                if (item.status != "Validated" || item.sourceHash != AssetDatabase.GetAssetDependencyHash(item.source).ToString() ||
                    item.targetHash != AssetDatabase.GetAssetDependencyHash(item.target).ToString())
                    throw new InvalidOperationException("Stale/unapproved manifest: " + item.target);
                var target = AssetDatabase.LoadAssetAtPath<AnimationClip>(item.target);
                var source = ActorAnimationAudit.Source(item.source, item.sourceFileId);
                var candidate = Candidate(source, target, ActorAnimationAudit.Actor(true),item.verticalPoseOffset);
                candidates.Add(candidate);
                var comparison = Compare(ActorAnimationAudit.Actor(true),source,candidate,item.verticalPoseOffset);
                if (Events(candidate) != item.eventHash || comparison.positionError > .005f || comparison.angleError > .5f)
                    throw new InvalidOperationException("Candidate changed since prevalidation: " + item.target);
            }
            for (int i = 0; i < candidates.Count; i++)
            {
                var target = AssetDatabase.LoadAssetAtPath<AnimationClip>(manifest.items[i].target);
                if (EditorJsonUtility.ToJson(target) == EditorJsonUtility.ToJson(candidates[i])) continue;
                EditorUtility.CopySerialized(candidates[i], target); EditorUtility.SetDirty(target);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(ActorAnimationAudit.ReportFolder + "/reconstruction-applied.json", JsonUtility.ToJson(manifest, true));
        }
        finally { foreach (var candidate in candidates) UnityEngine.Object.DestroyImmediate(candidate); }
    }
}
