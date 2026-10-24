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
    [Serializable] public sealed class BindingActor
    {
        public string actor, originalController, baseController, previewController, status;
        public int totalSlots, changedSlots;
    }
    [Serializable] public sealed class BindingReport
    {
        public string scope="Uninstalled actor-specific preview overrides. Base graphs, torch overrides, action clips and production prefabs are unchanged. Contacts/recovery acceptance pending.";
        public List<BindingActor> actors=new List<BindingActor>();
    }
    static AnimationClip SavePreviewClip(Input input)
    {
        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(input.entry.target);
        if(existing!=null)
        {
            if(!SameAuthoring(existing,input.candidate)){EditorUtility.CopySerialized(input.candidate,existing);EditorUtility.SetDirty(existing);}
            Object.DestroyImmediate(input.candidate);input.candidate=existing;
        }
        else AssetDatabase.CreateAsset(input.candidate,input.entry.target);
        AssetDatabase.SaveAssetIfDirty(input.candidate);AssetDatabase.ImportAsset(input.entry.target,ImportAssetOptions.ForceUpdate);
        input.candidate=AssetDatabase.LoadAssetAtPath<AnimationClip>(input.entry.target);Measure(input);
        return input.candidate;
    }
    [MenuItem("Lit/Animation Presentation/Facing Calibration/Prepare Actor Preview Bindings (Not Installed)")]
    public static void PreparePreviewBindings()
    {
        var first=Resolve();var directions=LoadDirectionalCandidates();var report=new BindingReport();
        Directory.CreateDirectory(Folder+"/Controllers");Directory.CreateDirectory(Folder+"/Additional");AssetDatabase.Refresh();
        foreach(var actorGroup in first.GroupBy(i=>i.entry.actor))
        {
            var actor=actorGroup.First().actor;var original=actor.runtimeAnimatorController;
            var view=new ControllerView(original);var row=new BindingActor {actor=actorGroup.Key,originalController=AssetDatabase.GetAssetPath(original),baseController=AssetDatabase.GetAssetPath(view.controller)};report.actors.Add(row);
            if(!actor.isHuman){row.previewController=row.originalController;row.status="Generic native controller preserved; no override generated";continue;}
            var calibrated=new Dictionary<AnimationClip,AnimationClip>();
            foreach(var input in actorGroup.Concat(directions.Where(i=>i.entry.actor==actorGroup.Key)))
            {
                var candidate=input.candidate!=null?input.candidate:ActorAnimationAudit.Source(input.entry.target);
                if(calibrated.TryGetValue(input.source,out var previous) && previous!=candidate)throw new InvalidOperationException("Ambiguous calibration mapping");
                calibrated[input.source]=candidate;
            }
            // Juggernaut's actual idle is a dedicated gameplay copy, not the supplier FBX used by the first-lot rig comparison.
            var state=view.controller.layers[0].stateMachine.states.Single(s=>s.state.name==(actorGroup.Key=="Lucian"?"CombatIdle":"Locomotion")).state;
            AnimationClip idle=state.motion as AnimationClip;
            if(idle==null && state.motion is BlendTree tree)
                idle=tree.children.Where(c=>c.motion is AnimationClip).OrderBy(c=>c.threshold).Select(c=>(AnimationClip)c.motion).Single();
            if(idle==null)throw new InvalidOperationException("Idle consumer is unresolved: "+actorGroup.Key);
            idle=view.Resolve(idle);
            if(!calibrated.ContainsKey(idle))
            {
                var path=AssetDatabase.GetAssetPath(idle);var entry=new Entry {actor=actorGroup.Key,cycle="CurrentIdle",source=path,sourceHash=Hash(path),metadataHash=Hash(path+".meta"),target=Folder+"/Additional/"+actorGroup.Key+"_CurrentIdle.anim",events=ActorAnimationAudit.EventDetails(idle)};
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(idle,out entry.sourceGuid,out entry.sourceFileId);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(actor.avatar,out entry.avatarGuid,out long _);
                var input=new Input {actor=actor,source=idle,entry=entry};
                try
                {
                    Measure(input);
                    if(entry.maximumCandidateYaw<=3)calibrated[idle]=idle;
                    else {entry.muscleOffset=CalibrateOffset(input);input.candidate=Candidate(input);Measure(input);calibrated[idle]=SavePreviewClip(input);}
                    SaveReport(new List<Input>{input},actorGroup.Key+"-current-idle");
                }
                finally {if(input.candidate!=null && !AssetDatabase.Contains(input.candidate))Object.DestroyImmediate(input.candidate);}
            }
            var preview=new AnimatorOverrideController(view.controller);preview.name=actorGroup.Key+"_FacingPreview";
            try
            {
                var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();preview.GetOverrides(pairs);
                for(int i=0;i<pairs.Count;i++)
                {
                    var key=pairs[i].Key;var effective=view.Resolve(key);var target=calibrated.TryGetValue(effective,out var replacement)?replacement:effective;
                    pairs[i]=new KeyValuePair<AnimationClip,AnimationClip>(key,target);
                    row.totalSlots++;if(target!=effective)row.changedSlots++;
                }
                preview.ApplyOverrides(pairs);
                var check=new ControllerView(preview);
                foreach(var pair in pairs)if(check.Resolve(pair.Key)!=pair.Value)throw new InvalidOperationException("Preview did not preserve an effective motion binding");
                row.previewController=Folder+"/Controllers/"+preview.name+".overrideController";
                var existing=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(row.previewController);
                if(existing==null)AssetDatabase.CreateAsset(preview,row.previewController);
                else
                {
                    var old=new List<KeyValuePair<AnimationClip,AnimationClip>>();existing.GetOverrides(old);
                    if(existing.runtimeAnimatorController!=view.controller || old.Count!=pairs.Count || old.Any(p=>!pairs.Any(n=>n.Key==p.Key&&n.Value==p.Value)))
                    {EditorUtility.CopySerialized(preview,existing);EditorUtility.SetDirty(existing);}
                    Object.DestroyImmediate(preview);preview=existing;
                }
                AssetDatabase.SaveAssetIfDirty(preview);row.status="Staged only. Disable LucianCombatTorsoFacing on the test instance before comparing; physical and recovery acceptance pending.";
            }
            finally {if(preview!=null && !AssetDatabase.Contains(preview))Object.DestroyImmediate(preview);}
        }
        File.WriteAllText(ReportFolder+"/preview-bindings.json",JsonUtility.ToJson(report,true));
    }
}
