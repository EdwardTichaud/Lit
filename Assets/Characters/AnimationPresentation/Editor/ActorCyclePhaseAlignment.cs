using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Aligns cyclic leg phase, never body height or orientation. Low confidence blocks the entire tree.</summary>
public static class ActorCyclePhaseAlignment
{
    [Serializable] public sealed class Item
    {
        public string actor, tree, clip, reference, status;
        public float offset, timeScale, correlation;
    }
    [Serializable] public sealed class Report { public List<Item> items = new List<Item>(); }
    private const int Samples = 120;
    public static Report Measure(Animator actor,BlendTree tree)
    {
        var reference = tree.children.Single(c => c.position == Vector2.up).motion as AnimationClip;
        if (reference == null) throw new InvalidOperationException("Forward cycle unresolved: " + tree.name);
        var result = new Report();
        using (var sampler = new ActorPoseSampler(actor))
        {
            var signal = Signal(sampler.Sample(reference,false,Samples));
            foreach (var child in tree.children.Where(c => c.position.sqrMagnitude > .001f))
            {
                if (!(child.motion is AnimationClip clip)) throw new InvalidOperationException("Unresolved directional cycle.");
                var candidate = Signal(sampler.Sample(clip,false,Samples));
                float best = float.NegativeInfinity; int shift = 0;
                for (int offset = 0; offset < Samples; offset++)
                {
                    float score = 0;
                    for (int i = 0; i < Samples; i++) score += signal[i] * candidate[(i + offset) % Samples];
                    score /= Samples;
                    if (score > best) { best = score; shift = offset; }
                }
                result.items.Add(new Item {actor=actor.name,tree=tree.name,clip=AssetDatabase.GetAssetPath(clip),
                    reference=AssetDatabase.GetAssetPath(reference),offset=(float)shift/Samples,timeScale=clip.length/reference.length,
                    correlation=best,status=best >= .75f ? "Phase candidate; ground acceptance separate" : "Blocked: unreliable correspondence; animation review required"});
            }
        }
        return result;
    }
    private static float[] Signal(ActorPoseSampler.Frame[] frames)
    {
        var values = frames.Take(Samples).Select(f => f.leftFoot.y-f.rightFoot.y).ToArray();
        float mean = values.Average(); float deviation = Mathf.Sqrt(values.Average(v => (v-mean)*(v-mean)));
        if (deviation < .001f) throw new InvalidOperationException("No distinct cyclic leg phase; contact annotations required.");
        return values.Select(v => (v-mean)/deviation).ToArray();
    }
    public static Report ApplyLaboratory(Animator actor,BlendTree tree)
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\','/').Contains("/Library/ActorAnimationSandbox/"))
            throw new InvalidOperationException("Phase candidates require isolated lab validation.");
        var report = Measure(actor,tree);
        if (report.items.Any(i => i.correlation < .75f)) return report;
        var children = tree.children;
        bool changed = false;
        foreach (var item in report.items)
        {
            int index = Array.FindIndex(children,c => c.position.sqrMagnitude > .001f && AssetDatabase.GetAssetPath(c.motion) == item.clip);
            changed |= children[index].cycleOffset != item.offset || children[index].timeScale != item.timeScale;
            children[index].cycleOffset = item.offset; children[index].timeScale = item.timeScale;
        }
        if (changed) { tree.children = children; EditorUtility.SetDirty(tree); }
        return report;
    }
    [MenuItem("Lit/Animation Presentation/Audit Directional Phases")]
    public static void Audit()
    {
        var report = new Report();
        foreach (bool enemy in new[] {false,true})
        {
            var actor = ActorAnimationAudit.Actor(enemy);
            var controller = (AnimatorController)actor.runtimeAnimatorController;
            var state = controller.layers.SelectMany(l => LocomotionPresentationCalibration.States(l.stateMachine))
                .Single(s => s.name == (enemy ? "Locomotion" : "CombatLocomotion"));
            foreach (var tier in ((BlendTree)state.motion).children.Where(c => c.motion is BlendTree))
                report.items.AddRange(Measure(actor,(BlendTree)tier.motion).items);
        }
        Directory.CreateDirectory(ActorAnimationAudit.ReportFolder);
        File.WriteAllText(ActorAnimationAudit.ReportFolder + "/directional-phases.json",JsonUtility.ToJson(report,true));
    }
}
