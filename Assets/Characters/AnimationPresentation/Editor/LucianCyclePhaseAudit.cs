using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

/// <summary>Independent native-controller goals versus exported phase mapping. Never changes an asset.</summary>
public static class LucianCyclePhaseAudit
{
    [Serializable] sealed class Row
    {
        public string clip;
        public float cycleOffset,plusError,minusError,zeroError,rotationError;
        public int samples;
    }
    [Serializable] sealed class Report {public List<Row> cycles=new List<Row>();}
    public static void Run()
    {
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        var source=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder+"/LucianCadence.overrideController");
        var transient=new List<UnityEngine.Object>();var report=new Report();
        try
        {
            var combat=LucianContactBlendAudit.CreateMuscleController(source,transient,"CombatLocomotion",true);
            var explore=LucianContactBlendAudit.CreateMuscleController(source,transient,"Locomotion",true);
            using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
            foreach(var cycle in profile.walk.Concat(profile.run).Concat(new[]{profile.explorationWalk,profile.explorationRun}))
            {
                bool exploration=cycle==profile.explorationWalk||cycle==profile.explorationRun;
                bool running=cycle==profile.explorationRun||profile.run.Contains(cycle);
                var thresholds=exploration?profile.explorationBlendThresholds:profile.blendThresholds;
                float tier=running?thresholds.y:thresholds.x;var direction=cycle.direction.normalized;
                AnimatorControllerPlayable active=default;var row=new Row {clip=cycle.clip.name,cycleOffset=cycle.cycleOffset};
                sampler.Sample(cycle.clip,sampleCount:120,controller:exploration?explore:combat,configureController:p=>
                {
                    active=p;p.SetFloat("LitSpeed",tier);p.SetFloat("LocomotionTier",tier);p.SetFloat("LocomotionPlaybackRate",1);
                    p.SetFloat("HorizontalMovement",direction.x);p.SetFloat("ForwardMovement",direction.y);p.SetFloat("CombatMoveMagnitude",1);
                    p.SetBool("CombatStrafeActive",!exploration);p.SetBool("IsMoving",true);p.SetLayerWeight(1,0);p.SetLayerWeight(2,0);
                    p.Play(exploration?"Base Layer.Locomotion":"Base Layer.CombatLocomotion",0,0);
                },modifyIKGoals:a=>
                {
                    var weights=active.GetCurrentAnimatorClipInfo(0);
                    if(weights.Where(c=>c.clip==cycle.clip).Sum(c=>c.weight)<.999f)throw new InvalidOperationException("Native point is not pure: "+cycle.clip.name);
                    float phase=active.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    var left=a.transform.InverseTransformPoint(a.GetIKPosition(AvatarIKGoal.LeftFoot));var right=a.transform.InverseTransformPoint(a.GetIKPosition(AvatarIKGoal.RightFoot));
                    float Error(float offset)
                    {
                        cycle.footGoals.Evaluate(phase+offset,out var l,out var r,out _,out _);
                        return Mathf.Max(Vector3.Distance(l,left),Vector3.Distance(r,right));
                    }
                    row.plusError=Mathf.Max(row.plusError,Error(cycle.cycleOffset));row.minusError=Mathf.Max(row.minusError,Error(-cycle.cycleOffset));row.zeroError=Mathf.Max(row.zeroError,Error(0));
                    cycle.footGoals.Evaluate(phase+cycle.cycleOffset,out _,out _,out var lq,out var rq);
                    var inverse=Quaternion.Inverse(a.transform.rotation);
                    row.rotationError=Mathf.Max(row.rotationError,Quaternion.Angle(lq,inverse*a.GetIKRotation(AvatarIKGoal.LeftFoot)),Quaternion.Angle(rq,inverse*a.GetIKRotation(AvatarIKGoal.RightFoot)));
                    row.samples++;
                });
                report.cycles.Add(row);
            }
            File.WriteAllText("Library/LucianCoherence/native-controller-phase.json",JsonUtility.ToJson(report,true));
        }
        finally{for(int i=transient.Count-1;i>=0;i--)if(transient[i]!=null)UnityEngine.Object.DestroyImmediate(transient[i]);}
    }
}
