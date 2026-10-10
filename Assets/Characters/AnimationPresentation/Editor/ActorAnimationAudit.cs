using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Read-only pose audit; all reports live in Library.</summary>
public static class ActorAnimationAudit
{
    public const string ReportFolder = "Library/ActorAnimationValidation";
    public const string Sources = "Assets/Characters/1_Squad/Lucian/Animation/";
    public const string EnemyFolder = "Assets/Characters/3_Enemy/Juggernaut_v2/";
    public const string PlayerPrefab = "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab";
    [Serializable] public sealed class Entry
    {
        public string actor, target, source, sourceGuid, targetGuid, avatarGuid, sourceHash, targetHash;
        public long sourceFileId;
        public float length, bodyYRange, lowestFoot, highestSupportCandidate;
        public float sourcePoseError;
        public float sourceAngleError;
        public string status;
        public string[] consumers, curves;
        public bool[] consumerNativeFootIK;
        public string settings, events;
        public EventData[] eventDetails;
    }
    [Serializable] public sealed class EventData
    {
        public float time, floatParameter;
        public int intParameter, messageOptions;
        public string function, stringParameter, objectGuid;
        public long objectFileId;
    }
    public static EventData[] EventDetails(AnimationClip clip) => AnimationUtility.GetAnimationEvents(clip).Select(e =>
    {
        var result = new EventData {time=e.time,function=e.functionName,floatParameter=e.floatParameter,intParameter=e.intParameter,
            stringParameter=e.stringParameter,messageOptions=(int)e.messageOptions};
        if (e.objectReferenceParameter != null)
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(e.objectReferenceParameter,out result.objectGuid,out result.objectFileId);
        return result;
    }).ToArray();
    [Serializable] public sealed class Manifest { public int version = 1; public List<Entry> entries = new List<Entry>(); public List<string> unresolved = new List<string>(); }
    public static Animator Actor(bool enemy)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(enemy ? EnemyFolder + "Juggernaut_v2.prefab" : PlayerPrefab);
        var contract = prefab.GetComponent<CharacterAnimationController>();
        return contract != null ? contract.Animator : prefab.GetComponent<Animator>();
    }
    public static AnimationClip Source(string path, long id = 0)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        if (id != 0) clips = clips.Where(c => { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c, out string _, out long fileId); return fileId == id; }).ToArray();
        if (clips.Length != 1) throw new InvalidOperationException("Unresolved or ambiguous clip: " + path + " (" + clips.Length + ")");
        return clips[0];
    }
    public static string EnemySource(string name)
    {
        if (name == "Strike") return Sources + "TwinSword_attack03_Inplace.FBX";
        if (name == "Followup") return Sources + "TwinSword_attack04_Inplace.FBX";
        if (name == "Sweep") return Sources + "TwinSword_Attack15_Inplace.FBX";
        if (name == "Idle") return Sources + "Twinblades_Idle_Inplace.FBX";
        if (name == "Hurt") return "Assets/0 - UnityPackages/Fab/TwinBladesBundle/TwinSwordAnimsetBase_V2/Animation/InPlace/TwinSword_Hit_L_Inplace.FBX";
        if (name == "Death") return "Assets/0 - UnityPackages/Fab/TwinBladesBundle/Twinblades_Expansion_V2/Animation/Inplace/Hit/Death_v2.anim";
        if (name.StartsWith("Walk") || name.StartsWith("Run"))
        {
            var parts = name.Split('_');
            return Sources + "Twinblades_Strafe_" + parts[0] + "_" + (parts.Length > 1 ? parts[1] : "F") + "_Inplace.FBX";
        }
        return null;
    }
    [MenuItem("Lit/Animation Presentation/Audit Lucian And Juggernaut")]
    public static void Audit()
        => AuditSnapshot("before");
    public static void AuditAfter()
        => AuditSnapshot("after");
    private static void AuditSnapshot(string snapshot)
    {
        Directory.CreateDirectory(ReportFolder);
        var manifest = new Manifest();
        var playerHistory = File.Exists(PlayerInPlaceMigration.ManifestPath)
            ? JsonUtility.FromJson<PlayerInPlaceMigration.Manifest>(File.ReadAllText(PlayerInPlaceMigration.ManifestPath)) : null;
        foreach (bool enemy in new[] { false, true })
        {
            var actor = Actor(enemy);
            var consumers = Consumers(actor.runtimeAnimatorController);
            if (!enemy)
            {
                var failures = new List<string>();
                foreach (var pair in PlayerInPlaceAudit.Collect(failures))
                {
                    if (!consumers.TryGetValue(pair.Key,out var references)) consumers[pair.Key] = references = new List<string>();
                    references.AddRange(pair.Value);
                }
                manifest.unresolved.AddRange(failures); // Audit reports missing bindings; it never guesses a repair.
            }
            string folder = enemy ? EnemyFolder + "Animations" : Sources + "PlayerInPlace";
            var paths = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                .Concat(consumers.Keys.Select(AssetDatabase.GetAssetPath)).Distinct().OrderBy(p => p).ToArray();
            using (var sampler = new ActorPoseSampler(Actor(enemy)))
            foreach (string path in paths)
            {
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                {
                if (clip == null || !clip.isHumanMotion) continue;
                ActorPoseSampler.Frame[] frames; string soleIssue = null;
                try { frames = sampler.Sample(clip,measureSoles:true); }
                catch (InvalidOperationException error) { soleIssue = error.Message; frames = sampler.Sample(clip); }
                string name = enemy ? clip.name.Replace("Juggernaut_v2_", "") : clip.name;
                string sourcePath = enemy ? EnemySource(name) : null;
                var historical = playerHistory?.replacements?.FirstOrDefault(r => r.targetPath == path);
                if (!enemy && historical != null) sourcePath = AssetDatabase.GUIDToAssetPath(historical.sourceGuid);
                var entry = new Entry { actor = enemy ? "Juggernaut_v2" : "Lucian", target = path,
                    source = sourcePath, targetGuid = AssetDatabase.AssetPathToGUID(path), targetHash = AssetDatabase.GetAssetDependencyHash(path).ToString(),
                    avatarGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Actor(enemy).avatar)), length = clip.length,
                    bodyYRange = frames.Max(f => f.body.y) - frames.Min(f => f.body.y),
                    lowestFoot = frames.Min(f => Mathf.Min(f.leftFoot.y, f.rightFoot.y)),
                    highestSupportCandidate = frames.Max(f => Mathf.Min(f.leftFoot.y, f.rightFoot.y)), status = soleIssue == null ? "Audited, not approved" : "Sole capture blocked: " + soleIssue,
                    consumers = consumers.TryGetValue(clip, out var references) ? references.ToArray() : Array.Empty<string>(),
                    consumerNativeFootIK = actor.runtimeAnimatorController is AnimatorController ac ? ac.layers.SelectMany(l => LocomotionPresentationCalibration.States(l.stateMachine))
                        .Where(s => s.motion != null && (s.motion == clip || s.motion is BlendTree bt && ContainsClip(bt,clip))).Select(s => s.iKOnFeet).ToArray() : Array.Empty<bool>(),
                    curves = AnimationUtility.GetCurveBindings(clip).Select(b => b.type.Name + ":" + b.path + ":" + b.propertyName).ToArray(),
                    settings = JsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(clip)),
                    events = ActorClipReconstruction.Events(clip),eventDetails=EventDetails(clip) };
                if (sourcePath != null)
                {
                    try
                    {
                    var source = Source(sourcePath, !enemy && historical != null ? historical.sourceId : 0);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out entry.sourceGuid, out entry.sourceFileId);
                    entry.sourceHash = AssetDatabase.GetAssetDependencyHash(sourcePath).ToString();
                    WriteFrames(entry.actor + "_" + name + "_source", sampler.Sample(source,true,measureSoles:true));
                    var comparison = ActorClipReconstruction.Compare(actor,source,clip);
                    entry.sourcePoseError = comparison.positionError;
                    entry.sourceAngleError = comparison.angleError;
                    }
                    catch (Exception e) { entry.status = "Source unresolved: " + e.Message; }
                }
                WriteFrames(entry.actor + "_" + name + "_" + snapshot, frames);
                if (entry.consumerNativeFootIK.Contains(true))
                    WriteFrames(entry.actor + "_" + name + "_" + snapshot + "_nativeIK",sampler.Sample(clip,measureSoles:true,applyFootIK:true));
                manifest.entries.Add(entry);
                }
            }
        }
        File.WriteAllText(ReportFolder + "/manifest-" + snapshot + ".json", JsonUtility.ToJson(manifest, true));
        Debug.Log("[ActorAnimationAudit] " + manifest.entries.Count + " clips audited.");
    }
    private static bool ContainsClip(BlendTree tree,AnimationClip clip) => tree.children.Any(c => c.motion == clip || c.motion is BlendTree nested && ContainsClip(nested,clip));
    public static Dictionary<AnimationClip, List<string>> Consumers(RuntimeAnimatorController controller)
    {
        var result = new Dictionary<AnimationClip, List<string>>();
        if (controller is AnimatorOverrideController overrides)
        {
            foreach (var clip in overrides.animationClips) if (clip != null) result[clip] = new List<string> { overrides.name };
            controller = overrides.runtimeAnimatorController;
        }
        if (controller is AnimatorController graph)
            foreach (var layer in graph.layers) Visit(layer.stateMachine, layer.name, result);
        return result;
    }
    private static void Visit(AnimatorStateMachine machine, string path, Dictionary<AnimationClip,List<string>> result)
    {
        foreach (var state in machine.states) VisitMotion(state.state.motion, path + "/" + state.state.name, result);
        foreach (var child in machine.stateMachines) Visit(child.stateMachine, path + "/" + child.stateMachine.name, result);
    }
    private static void VisitMotion(Motion motion, string path, Dictionary<AnimationClip,List<string>> result)
    {
        if (motion is AnimationClip clip)
        {
            if (!result.TryGetValue(clip, out var refs)) result[clip] = refs = new List<string>();
            refs.Add(path);
        }
        else if (motion is BlendTree tree) foreach (var child in tree.children) VisitMotion(child.motion, path + "/" + tree.name, result);
    }
    public static void WriteFrames(string name, ActorPoseSampler.Frame[] frames)
    {
        var lines = new List<string> { "time,root_x,root_y,root_z,body_x,body_y,body_z,left_x,left_y,left_z,right_x,right_y,right_z,lefttoe_x,lefttoe_y,lefttoe_z,righttoe_x,righttoe_y,righttoe_z,root_qx,root_qy,root_qz,root_qw,body_qx,body_qy,body_qz,body_qw,left_qx,left_qy,left_qz,left_qw,right_qx,right_qy,right_qz,right_qw,delta_x,delta_y,delta_z,leftsole_x,leftsole_y,leftsole_z,rightsole_x,rightsole_y,rightsole_z" };
        foreach (var f in frames)
        {
            float[] v = { f.time, f.root.x,f.root.y,f.root.z,f.body.x,f.body.y,f.body.z,
                f.leftFoot.x,f.leftFoot.y,f.leftFoot.z,f.rightFoot.x,f.rightFoot.y,f.rightFoot.z,
                f.leftToe.x,f.leftToe.y,f.leftToe.z,f.rightToe.x,f.rightToe.y,f.rightToe.z,
                f.rotation.x,f.rotation.y,f.rotation.z,f.rotation.w,f.bodyRotation.x,f.bodyRotation.y,f.bodyRotation.z,f.bodyRotation.w,
                f.leftRotation.x,f.leftRotation.y,f.leftRotation.z,f.leftRotation.w,f.rightRotation.x,f.rightRotation.y,f.rightRotation.z,f.rightRotation.w,
                f.delta.x,f.delta.y,f.delta.z,f.leftSole.x,f.leftSole.y,f.leftSole.z,f.rightSole.x,f.rightSole.y,f.rightSole.z };
            lines.Add(string.Join(",", v.Select(x => x.ToString("R", CultureInfo.InvariantCulture))));
        }
        File.WriteAllLines(ReportFolder + "/" + name.Replace('/', '_') + ".csv", lines);
    }
}
