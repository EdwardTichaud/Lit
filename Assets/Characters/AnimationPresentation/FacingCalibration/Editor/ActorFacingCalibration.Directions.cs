using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class ActorFacingCalibration
{
    static List<Input> LoadDirectionalCandidates()
    {
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(ReportFolder+"/directions.json"));
        var inputs=new List<Input>();
        foreach(var entry in manifest.entries)
        {
            if(Hash(entry.source)!=entry.sourceHash || Hash(entry.source+".meta")!=entry.metadataHash)throw new InvalidOperationException("Directional source changed since calibration: "+entry.source);
            var input=new Input {actor=ActorAnimationAudit.Actor(entry.actor=="Juggernaut"),source=ActorAnimationAudit.Source(entry.source,entry.sourceFileId),
                candidate=ActorAnimationAudit.Source(entry.target),entry=entry};
            if(input.candidate==null)throw new InvalidOperationException("Directional candidate missing: "+entry.target);
            inputs.Add(input);
        }
        return inputs;
    }
    [MenuItem("Lit/Animation Presentation/Facing Calibration/7 Capture Directional Candidates")]
    public static void CaptureDirections() => CaptureInputs(LoadDirectionalCandidates(),"DirectionalCaptures");

    [MenuItem("Lit/Animation Presentation/Facing Calibration/8 Audit Directional Crossfades")]
    public static void AuditDirectionTransitions()
    {
        var inputs=LoadDirectionalCandidates();var report=new TransitionReport();var phases=new ActorCyclePhaseAlignment.Report();
        foreach(var group in inputs.GroupBy(i=>i.entry.actor))
        {
            var actor=group.First().actor;
            var view=new ControllerView(actor.runtimeAnimatorController);var controller=view.controller;
            var state=controller.layers[0].stateMachine.states.Single(s=>s.state.name==(group.Key=="Lucian"?"CombatLocomotion":"Locomotion")).state;
            if(!(state.motion is BlendTree root))throw new InvalidOperationException("Directional state has no Blend Tree");
            foreach(var tier in root.children.Where(c=>c.motion is BlendTree))
            {
                var tree=(BlendTree)tier.motion;var resolvedTree=Object.Instantiate(tree);resolvedTree.name=tree.name;
                try
                {
                    var resolvedChildren=resolvedTree.children;
                    for(int i=0;i<resolvedChildren.Length;i++)if(resolvedChildren[i].motion is AnimationClip clip)resolvedChildren[i].motion=view.Resolve(clip);
                    resolvedTree.children=resolvedChildren;phases.items.AddRange(ActorCyclePhaseAlignment.Measure(actor,resolvedTree).items);
                }
                finally {Object.DestroyImmediate(resolvedTree);}
                var children=tree.children.Where(c=>c.position.sqrMagnitude>.001f).OrderBy(c=>Mathf.Atan2(c.position.x,c.position.y)).ToArray();
                foreach(var child in children)
                    if(child.mirror || !(child.motion is AnimationClip))throw new InvalidOperationException("Mirrored or nested directional child needs a separate contextual audit: "+tree.name);
                for(int direction=0;direction<children.Length;direction++)foreach(int step in new[]{-1,1})
                {
                    var from=children[direction];var to=children[(direction+step+children.Length)%children.Length];
                    var a=group.Single(i=>i.source==view.Resolve((AnimationClip)from.motion));var b=group.Single(i=>i.source==view.Resolve((AnimationClip)to.motion));
                    foreach(float phase in new[]{0,.25f,.5f,.75f})
                    {
                        var before=SampleTransition(actor,a.source,b.source,Mathf.Repeat(phase+from.cycleOffset,1),Mathf.Repeat(phase+to.cycleOffset,1),from.timeScale*tier.timeScale,to.timeScale*tier.timeScale);
                        var after=SampleTransition(actor,a.candidate,b.candidate,Mathf.Repeat(phase+from.cycleOffset,1),Mathf.Repeat(phase+to.cycleOffset,1),from.timeScale*tier.timeScale,to.timeScale*tier.timeScale);
                        var row=new Transition {actor=group.Key,from=a.entry.cycle,to=b.entry.cycle,outgoingPhase=phase};
                        for(int i=0;i<after.Length;i++)
                        {
                            row.maximumFootDifference=Mathf.Max(row.maximumFootDifference,Vector3.Distance(before[i].leftFoot,after[i].leftFoot),Vector3.Distance(before[i].rightFoot,after[i].rightFoot));
                            row.maximumHipDifference=Mathf.Max(row.maximumHipDifference,Vector3.Distance(before[i].hip,after[i].hip));
                            row.maximumRootDisplacement=Mathf.Max(row.maximumRootDisplacement,after[i].root.magnitude);
                            row.maximumHeadYaw=Mathf.Max(row.maximumHeadYaw,Mathf.Abs(after[i].yaw));
                        }
                        report.transitions.Add(row);
                    }
                }
            }
        }
        File.WriteAllText(ReportFolder+"/directional-transitions.json",JsonUtility.ToJson(report,true));
        File.WriteAllText(ReportFolder+"/directional-phases.json",JsonUtility.ToJson(phases,true));
        if(report.transitions.Any(r=>r.maximumFootDifference>.001f || r.maximumHipDifference>.001f || r.maximumRootDisplacement>.001f))
            throw new InvalidOperationException("Directional crossfade changed a protected support/root pose.");
    }
    static float CalibrateOffset(Input input)
    {
        float lower=float.NegativeInfinity,upper=float.PositiveInfinity;
        foreach(string muscle in Turns)
        {
            var curve=AnimationUtility.GetEditorCurve(input.source,EditorCurveBinding.FloatCurve("",typeof(Animator),muscle));
            if(curve==null || curve.length==0)throw new InvalidOperationException("Missing turn muscle: "+input.entry.source);
            lower=Mathf.Max(lower,-1-curve.keys.Min(k=>k.value));upper=Mathf.Min(upper,1-curve.keys.Max(k=>k.value));
        }
        if(lower>0 || upper<0)throw new InvalidOperationException("Source already outside muscle limits; contextual review needed: "+input.entry.source);
        float shift=0;
        for(int iteration=0;iteration<10;iteration++)
        {
            input.entry.muscleOffset=shift;var probe=Candidate(input);float error;
            try {error=Sample(input.actor,probe).Average(f=>f.yaw);}finally {Object.DestroyImmediate(probe);}
            if(Mathf.Abs(error)<.05f)return shift;
            float step=shift+.01f<=upper?.01f:-.01f;
            if(shift+step<lower)break;
            input.entry.muscleOffset=shift+step;probe=Candidate(input);float derivative;
            try {derivative=Mathf.DeltaAngle(error,Sample(input.actor,probe).Average(f=>f.yaw))/step;}finally {Object.DestroyImmediate(probe);}
            if(Mathf.Abs(derivative)<.1f)break;
            float next=Mathf.Clamp(shift-error/derivative,lower,upper);
            if(Mathf.Abs(next-shift)<1e-5f)break;
            shift=next;
        }
        throw new InvalidOperationException("No reliable head/neck calibration within source muscle limits: "+input.entry.source);
    }
    [MenuItem("Lit/Animation Presentation/Facing Calibration/6 Prepare Directional Candidates")]
    public static void PrepareDirections()
    {
        var firstLot=Resolve();var inputs=new List<Input>();var failures=new List<string>();
        foreach(bool enemy in new[]{false,true})
        {
            var actor=ActorAnimationAudit.Actor(enemy);string actorName=enemy?"Juggernaut":"Lucian";
            foreach(var pair in EffectiveConsumers(actor.runtimeAnimatorController))
            {
                // Reachable motion consumers determine scope, never a filename heuristic.
                if(!pair.Value.Any(p=>p.Contains(enemy?"Directional":"CombatLocomotion")))continue;
                if(firstLot.Any(i=>i.entry.actor==actorName && i.entry.cycle=="Idle" && i.source==pair.Key))continue;
                var path=AssetDatabase.GetAssetPath(pair.Key);
                if(!pair.Key.isHumanMotion)throw new InvalidOperationException("Unexpected non-Humanoid directional clip: "+path);
                var entry=new Entry {actor=actorName,cycle=Path.GetFileNameWithoutExtension(path),source=path,sourceHash=Hash(path),metadataHash=Hash(path+".meta"),
                    consumers=pair.Value.Distinct().ToArray(),events=ActorAnimationAudit.EventDetails(pair.Key)};
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pair.Key,out entry.sourceGuid,out entry.sourceFileId);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(actor.avatar,out entry.avatarGuid,out long _);
                entry.target=Folder+"/Directional/"+actorName+"_"+entry.cycle+"_"+entry.sourceGuid.Substring(0,8)+"_"+entry.sourceFileId+".anim";
                inputs.Add(new Input {actor=actor,source=pair.Key,entry=entry});
            }
        }
        try
        {
            foreach(var input in inputs)
            {
                try
                {
                    Measure(input); // Native pose, source fingerprint and reference before any correction.
                    if(input.entry.maximumCandidateYaw<=3)
                    {
                        input.entry.target=input.entry.source;input.entry.status="Already aligned: original reference preserved";continue;
                    }
                    var reused=firstLot.SingleOrDefault(i=>i.entry.actor==input.entry.actor && i.source==input.source);
                    if(reused!=null)
                    {
                        input.entry.target=reused.entry.target;input.entry.muscleOffset=reused.entry.muscleOffset;
                        input.candidate=AssetDatabase.LoadAssetAtPath<AnimationClip>(input.entry.target);
                        if(input.candidate==null)throw new InvalidOperationException("First-lot candidate missing: "+input.entry.target);
                    }
                    else
                    {
                        input.entry.muscleOffset=CalibrateOffset(input);input.candidate=Candidate(input);
                    }
                    // Preserve the authored small gaze sway; the first-lot stricter 3-degree gate remains unchanged.
                    Measure(input,5);
                    if(Mathf.Abs(input.entry.candidateYaw)>.1f)throw new InvalidOperationException("Mean gaze not calibrated");
                }
                catch(Exception e){input.entry.status="BLOCKED: "+e.Message;failures.Add(input.entry.actor+" "+e.Message);}
            }
            SaveReport(inputs,"directional-prevalidation");
            if(failures.Count>0)throw new InvalidOperationException("No directional asset written: "+string.Join("; ",failures));
            Directory.CreateDirectory(Folder+"/Directional");AssetDatabase.Refresh();
            foreach(var input in inputs)
            {
                if(input.candidate==null || AssetDatabase.Contains(input.candidate))continue;
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(input.entry.target);
                if(existing!=null)
                {
                    if(!SameAuthoring(existing,input.candidate)){EditorUtility.CopySerialized(input.candidate,existing);EditorUtility.SetDirty(existing);}
                    Object.DestroyImmediate(input.candidate);input.candidate=existing;
                }
                else AssetDatabase.CreateAsset(input.candidate,input.entry.target);
                AssetDatabase.SaveAssetIfDirty(input.candidate);
                AssetDatabase.ImportAsset(input.entry.target,ImportAssetOptions.ForceUpdate);
                input.candidate=AssetDatabase.LoadAssetAtPath<AnimationClip>(input.entry.target);Measure(input,5);
            }
            foreach(var input in inputs)
                if(Hash(input.entry.source)!=input.entry.sourceHash || Hash(input.entry.source+".meta")!=input.entry.metadataHash)throw new InvalidOperationException("Directional source changed");
            SaveReport(inputs,"directions");
        }
        finally {foreach(var input in inputs)if(input.candidate!=null && !AssetDatabase.Contains(input.candidate))Object.DestroyImmediate(input.candidate);}
    }
}
