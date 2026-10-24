using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

public static partial class ActorFacingCalibration
{
    sealed class ControllerView
    {
        public readonly AnimatorController controller;
        readonly List<Dictionary<AnimationClip,AnimationClip>> layers=new List<Dictionary<AnimationClip,AnimationClip>>();
        public ControllerView(RuntimeAnimatorController source)
        {
            var visited=new HashSet<RuntimeAnimatorController>();
            while(source is AnimatorOverrideController overrides)
            {
                if(!visited.Add(source))throw new InvalidOperationException("Cyclic override controller");
                var mappings=new List<KeyValuePair<AnimationClip,AnimationClip>>();overrides.GetOverrides(mappings);
                layers.Add(mappings.Where(p=>p.Key!=null && p.Value!=null).ToDictionary(p=>p.Key,p=>p.Value));
                source=overrides.runtimeAnimatorController;
            }
            controller=source as AnimatorController;
            if(controller==null)throw new InvalidOperationException("Unresolved base AnimatorController");
            layers.Reverse();
        }
        public AnimationClip Resolve(AnimationClip original)
        {
            var clip=original;
            foreach(var layer in layers)
                if(layer.TryGetValue(clip,out var replacement) || layer.TryGetValue(original,out replacement))clip=replacement;
            return clip;
        }
    }
    static Dictionary<AnimationClip,List<string>> EffectiveConsumers(RuntimeAnimatorController source)
    {
        var view=new ControllerView(source);var result=new Dictionary<AnimationClip,List<string>>();var stack=new HashSet<Motion>();
        void Motion(Motion motion,string path)
        {
            if(motion==null)return;
            if(motion is AnimationClip original)
            {
                var clip=view.Resolve(original);if(!result.TryGetValue(clip,out var consumers))result[clip]=consumers=new List<string>();consumers.Add(path);return;
            }
            if(motion is BlendTree tree)
            {
                if(!stack.Add(tree))throw new InvalidOperationException("Cyclic Blend Tree: "+path);
                foreach(var child in tree.children)Motion(child.motion,path+"/"+tree.name);stack.Remove(tree);
            }
        }
        void Machine(AnimatorStateMachine machine,string path)
        {
            foreach(var state in machine.states)Motion(state.state.motion,path+"/"+state.state.name);
            foreach(var child in machine.stateMachines)Machine(child.stateMachine,path+"/"+child.stateMachine.name);
        }
        foreach(var layer in view.controller.layers)Machine(layer.stateMachine,layer.name);
        return result;
    }
    [Serializable] public sealed class Phase
    {
        public string actor;
        public float walkToRunOffset, correlation;
        public string status;
    }
    [Serializable] public sealed class Transition
    {
        public string actor, from, to;
        public float outgoingPhase, maximumFootDifference, maximumHipDifference, maximumRootDisplacement, maximumHeadYaw;
    }
    [Serializable] public sealed class TransitionReport
    {
        public string scope = "Presentation crossfades, actual avatars, 60 Hz. Does not validate physical ground contact.";
        public List<Phase> phases = new List<Phase>();
        public List<Transition> transitions = new List<Transition>();
    }
    static Phase MeasurePhase(Input walk, Input run)
    {
        const int count=120;
        Func<Frame[],float[]> signal=frames=>Enumerable.Range(0,count).Select(i=>
        {
            float index=(float)i/count*(frames.Length-1);int a=Mathf.FloorToInt(index);int b=Mathf.Min(a+1,frames.Length-1);
            return Mathf.Lerp(frames[a].leftFoot.y-frames[a].rightFoot.y,frames[b].leftFoot.y-frames[b].rightFoot.y,index-a);
        }).ToArray();
        var a=signal(Sample(walk.actor,walk.source));var b=signal(Sample(run.actor,run.source));
        float meanA=a.Average(),meanB=b.Average();float devA=Mathf.Sqrt(a.Average(x=>(x-meanA)*(x-meanA))),devB=Mathf.Sqrt(b.Average(x=>(x-meanB)*(x-meanB)));
        var result=new Phase {actor=walk.entry.actor,status="Insufficient contact signal: preserve authored phase; review required"};
        if(devA<.001f || devB<.001f)return result;
        float best=float.NegativeInfinity;int shift=0;
        for(int offset=0;offset<count;offset++)
        {
            float score=0;for(int i=0;i<count;i++)score+=(a[i]-meanA)/devA*(b[(i+offset)%count]-meanB)/devB;
            score/=count;if(score>best){best=score;shift=offset;}
        }
        result.correlation=best;result.walkToRunOffset=(float)shift/count;
        result.status=best>=.75f?"Measured phase correspondence; rendered transition review required":"Unreliable phase correspondence: preserve authored phase; review required";
        return result;
    }
    static Frame[] SampleTransition(Animator source,AnimationClip outgoing,AnimationClip incoming,float phase,float incomingPhase,float outgoingRate=1,float incomingRate=1)
    {
        var root=Clone(source.transform,new Dictionary<Transform,Transform>()).gameObject;
        root.hideFlags=HideFlags.HideAndDontSave;root.SetActive(true);root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=source.transform.lossyScale;
        var animator=root.AddComponent<Animator>();animator.avatar=source.avatar;animator.applyRootMotion=false;animator.fireEvents=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
        var left=animator.isHuman?animator.GetBoneTransform(HumanBodyBones.LeftFoot):Named(root.transform,"foot_l");
        var right=animator.isHuman?animator.GetBoneTransform(HumanBodyBones.RightFoot):Named(root.transform,"foot_r");
        var hip=animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Hips):Named(root.transform,"pelvis");
        var head=animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Head):Named(root.transform,"head");
        var basis=Quaternion.Inverse(head.rotation)*Vector3.forward;var graph=PlayableGraph.Create("Facing crossfade measurement");
        try
        {
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var mixer=AnimationMixerPlayable.Create(graph,2);
            var clips=new[]{outgoing,incoming};
            for(int i=0;i<2;i++){var p=AnimationClipPlayable.Create(graph,clips[i]);p.SetApplyFootIK(false);p.SetApplyPlayableIK(false);p.SetSpeed(i==0?outgoingRate:incomingRate);graph.Connect(p,0,mixer,i);}
            AnimationPlayableOutput.Create(graph,"Pose",animator).SetSourcePlayable(mixer);
            graph.Play();mixer.SetInputWeight(0,1);graph.Evaluate(0);
            mixer.GetInput(0).SetTime(phase*outgoing.length);mixer.GetInput(1).SetTime(incomingPhase*incoming.length);graph.Evaluate(0);
            var frames=new Frame[13];
            for(int i=0;i<frames.Length;i++)
            {
                float blend=(float)i/(frames.Length-1);mixer.SetInputWeight(0,1-blend);mixer.SetInputWeight(1,blend);
                graph.Evaluate(i==0?0:1f/60);
                frames[i]=new Frame {leftFoot=left.position,rightFoot=right.position,hip=hip.position,root=root.transform.position,
                    yaw=Vector3.SignedAngle(Vector3.forward,Vector3.ProjectOnPlane(head.rotation*basis,Vector3.up),Vector3.up)};
            }
            return frames;
        }
        finally {graph.Destroy();Object.DestroyImmediate(root);}
    }
    [MenuItem("Lit/Animation Presentation/Facing Calibration/4 Audit First Lot Transitions")]
    public static void AuditTransitions()
    {
        var inputs=Resolve();var report=new TransitionReport();
        foreach(var input in inputs){input.candidate=AssetDatabase.LoadAssetAtPath<AnimationClip>(input.entry.target);if(input.candidate==null)throw new InvalidOperationException("Prepare clips first: "+input.entry.target);}
        foreach(var group in inputs.GroupBy(i=>i.entry.actor))
        {
            var cycles=group.ToArray();var phase=cycles.Length==3?MeasurePhase(cycles[1],cycles[2]):new Phase {actor=group.Key,status="No native Run; no Walk/Run transition"};report.phases.Add(phase);
            for(int from=0;from<cycles.Length;from++)for(int to=0;to<cycles.Length;to++)
            {
                if(from==to)continue;
                foreach(float start in new[]{0,.25f,.5f,.75f})
                {
                    float entry=from>0 && to>0?Mathf.Repeat(start+(phase.correlation>=.75f?(to==2?phase.walkToRunOffset:-phase.walkToRunOffset):0),1):0;
                    var before=SampleTransition(cycles[0].actor,cycles[from].source,cycles[to].source,start,entry);
                    var after=SampleTransition(cycles[0].actor,cycles[from].candidate,cycles[to].candidate,start,entry);
                    var row=new Transition {actor=group.Key,from=cycles[from].entry.cycle,to=cycles[to].entry.cycle,outgoingPhase=start};
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
        Directory.CreateDirectory(ReportFolder);File.WriteAllText(ReportFolder+"/transitions.json",JsonUtility.ToJson(report,true));
        if(report.transitions.Any(r=>r.maximumFootDifference>.001f || r.maximumHipDifference>.001f || r.maximumRootDisplacement>.001f))
            throw new InvalidOperationException("Crossfade changed a protected support/root pose. See transitions.json.");
    }

    [Serializable] public sealed class ConsumerEntry
    {
        public string actor, clip, guid, settings, status;
        public long fileId;
        public bool humanoid;
        public string[] consumers;
        public float meanHorizontalHeadYaw, maximumHorizontalHeadYaw;
    }
    [Serializable] public sealed class ConsumerReport
    {
        public string scope="Effective controller motions plus conservative Lucian skill/override catalog. Catalog entries are not proof of active use.";
        public List<ConsumerEntry> entries=new List<ConsumerEntry>();
        public List<string> unresolved=new List<string>();
    }
    [MenuItem("Lit/Animation Presentation/Facing Calibration/5 Audit Active Clip Consumers (Read Only)")]
    public static void AuditConsumers()
    {
        var report=new ConsumerReport();var all=Resolve();var actors=all.GroupBy(i=>i.entry.actor).Select(g=>g.First()).ToArray();
        foreach(var input in actors)
        {
            var references=EffectiveConsumers(input.actor.runtimeAnimatorController);
            if(input.entry.actor=="Lucian")
            {
                var extra=PlayerInPlaceAudit.Collect(report.unresolved);
                foreach(var pair in extra){if(!references.TryGetValue(pair.Key,out var consumers))references[pair.Key]=consumers=new List<string>();consumers.AddRange(pair.Value);}
            }
            if(input.entry.actor=="Juggernaut")
            {
                var enemy=input.actor.GetComponentInParent<LitBrainsEnemy>();
                if(enemy!=null && enemy.combatProfile!=null)foreach(var attack in enemy.combatProfile.attacks)
                {
                    if(attack.clip==null){report.unresolved.Add("Juggernaut attack: "+attack.stateName);continue;}
                    if(!references.TryGetValue(attack.clip,out var consumers))references[attack.clip]=consumers=new List<string>();
                    consumers.Add("Combat profile: "+attack.stateName);
                }
            }
            foreach(var pair in references.OrderBy(p=>AssetDatabase.GetAssetPath(p.Key)).ThenBy(p=>p.Key.name))
            {
                var row=new ConsumerEntry {actor=input.entry.actor,clip=AssetDatabase.GetAssetPath(pair.Key),humanoid=pair.Key.isHumanMotion,
                    consumers=pair.Value.Distinct().OrderBy(s=>s).ToArray(),settings=JsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(pair.Key)),
                    status="Contextual review required: movement, gaze and strike have separate intended directions"};
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pair.Key,out row.guid,out row.fileId);
                try
                {
                    var frames=Sample(input.actor,pair.Key);row.meanHorizontalHeadYaw=frames.Average(f=>f.yaw);row.maximumHorizontalHeadYaw=frames.Max(f=>Mathf.Abs(f.yaw));
                    if(all.Any(i=>i.entry.actor==input.entry.actor && i.source==pair.Key))row.status="First lot calibrated in dedicated copies; production reference unchanged";
                }
                catch(Exception e){report.unresolved.Add(row.actor+": "+row.clip+" "+e.Message);row.status="Unresolved sample; no automatic repair";}
                report.entries.Add(row);
            }
        }
        Directory.CreateDirectory(ReportFolder);File.WriteAllText(ReportFolder+"/consumers.json",JsonUtility.ToJson(report,true));
    }
}
