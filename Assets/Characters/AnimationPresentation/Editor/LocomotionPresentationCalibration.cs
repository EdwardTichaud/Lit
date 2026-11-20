using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Measures candidates only. Support detection is reported for author review, not certified automatically.</summary>
public static class LocomotionPresentationCalibration
{
    [Serializable] public sealed class Measurement
    {
        public string actor, clip;
        public bool nativeFootIK;
        public float nativeSpeed, rootReferenceSpeed, leftPhase, rightPhase, estimatedStanceResidual;
        public float inplaceDuration, rootDuration;
        public string rootSource, status;
    }
    [Serializable] public sealed class Report { public List<Measurement> measurements = new List<Measurement>(); }
    public static Measurement Measure(Animator actor, AnimationClip clip, Vector2 direction,bool applyFootIK = false)
    {
        using (var sampler = new ActorPoseSampler(actor))
        {
            var frames = sampler.Sample(clip,applyFootIK:applyFootIK);
            var velocities = new List<float>();
            float leftMinimum = frames.Min(f => f.leftFoot.y), rightMinimum = frames.Min(f => f.rightFoot.y);
            Vector3 axis = new Vector3(direction.x,0,direction.y).normalized;
            float dt = clip.length / (frames.Length - 1);
            float leftPhase = 0, rightPhase = 0;
            float leftLowest = float.MaxValue, rightLowest = float.MaxValue;
            for (int i = 1; i < frames.Length - 1; i++)
            {
                var frame = frames[i];
                if (frame.leftFoot.y < leftLowest) { leftLowest = frame.leftFoot.y; leftPhase = frame.time / clip.length; }
                if (frame.rightFoot.y < rightLowest) { rightLowest = frame.rightFoot.y; rightPhase = frame.time / clip.length; }
                foreach (bool left in new[] { true, false })
                {
                    Vector3 foot = left ? frame.leftFoot : frame.rightFoot;
                    Vector3 before = left ? frames[i-1].leftFoot : frames[i-1].rightFoot;
                    float minimum = left ? leftMinimum : rightMinimum;
                    float speed = -Vector3.Dot((foot - before) / dt, axis);
                    if (foot.y <= minimum + .025f && speed > .1f) velocities.Add(speed);
                }
            }
            if (velocities.Count < 5) throw new InvalidOperationException("No reliable support samples: " + clip.name);
            velocities.Sort(); float nativeSpeed = velocities[velocities.Count / 2];
            var measurement = new Measurement { actor = actor.name, clip = AssetDatabase.GetAssetPath(clip), nativeSpeed = nativeSpeed, nativeFootIK=applyFootIK,
                inplaceDuration = clip.length,
                leftPhase = leftPhase, rightPhase = rightPhase,
                estimatedStanceResidual = velocities.Max(v => Mathf.Abs(v - nativeSpeed)) * dt,
                status = "Estimated support candidates; author contact validation required" };
            string originalName = clip.name.Replace("Juggernaut_v2_", "");
            string inplace = ActorAnimationAudit.EnemySource(originalName);
            if (inplace == null && originalName.EndsWith("_Inplace",StringComparison.OrdinalIgnoreCase))
                inplace = AssetDatabase.GetAssetPath(clip);
            if (inplace != null)
            {
                string rootName = Path.GetFileNameWithoutExtension(inplace).Replace("_Inplace","_Root");
                var paths = AssetDatabase.GetAllAssetPaths().Where(p => p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileNameWithoutExtension(p).Equals(rootName,StringComparison.OrdinalIgnoreCase)).ToArray();
                if (paths.Length == 1)
                {
                    var original = ActorAnimationAudit.Source(paths[0]);
                    measurement.rootDuration = original.length;
                    using (var sampling = new PlayerInPlaceSampling(actor))
                        measurement.rootReferenceSpeed = sampling.Sample(original).Distance / clip.length;
                    measurement.rootSource = paths[0];
                }
            }
            return measurement;
        }
    }
    [MenuItem("Lit/Animation Presentation/Measure Locomotion Candidates")]
    public static void MeasureCandidates()
    {
        var report = new Report();
        foreach (bool enemy in new[] { false, true })
        {
            var actor = ActorAnimationAudit.Actor(enemy);
            var clips = ActorAnimationAudit.Consumers(actor.runtimeAnimatorController).Keys;
            foreach (var clip in clips.Where(c => c.name.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0 || c.name.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (clip.name.Contains("Start") || clip.name.Contains("Stop")) continue;
                string[] directions = { "F", "FR", "R", "BR", "B", "BL", "L", "FL" };
                int index = Array.FindIndex(directions, d => clip.name.Contains("_" + d + "_") || clip.name.EndsWith("_" + d));
                float angle = Mathf.Max(0,index) * Mathf.PI / 4;
                try
                {
                    var controller = actor.runtimeAnimatorController as AnimatorController;
                    var ikOptions = controller.layers.SelectMany(l => States(l.stateMachine)).Where(s => UsesClip(s.motion,clip))
                        .Select(s => s.iKOnFeet).Distinct();
                    foreach (bool nativeIK in ikOptions)
                        report.measurements.Add(Measure(actor,clip,new Vector2(Mathf.Sin(angle),Mathf.Cos(angle)),nativeIK));
                }
                catch (Exception e) { Debug.LogWarning(e.Message); }
            }
        }
        Directory.CreateDirectory(ActorAnimationAudit.ReportFolder);
        File.WriteAllText(ActorAnimationAudit.ReportFolder + "/locomotion-candidates.json", JsonUtility.ToJson(report,true));
    }
    public static IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
    {
        foreach (var state in machine.states) yield return state.state;
        foreach (var child in machine.stateMachines) foreach (var state in States(child.stateMachine)) yield return state;
    }
    private static bool UsesClip(Motion motion, AnimationClip clip)
        => motion == clip || motion is BlendTree tree && tree.children.Any(child => UsesClip(child.motion,clip));
}
