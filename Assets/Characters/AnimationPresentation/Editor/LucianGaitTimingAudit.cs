using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;

/// <summary>Measures native cycle distance against the actual controller's phase clock. No asset writes.</summary>
public static class LucianGaitTimingAudit
{
    [Serializable] public sealed class Row
    {
        public bool exploration;
        public float gait,angle,stateLength,weightedLength,phaseRate,nativeDistance,observedNativeSpeed,currentCadence;
    }
    [Serializable] sealed class Report
    {
        public string scope="Actual-avatar controller, static parameters, playback rate one. Native distances from measured cycle references; observes Unity phase rate. Not physical support or visual acceptance.";
        public List<Row> cases=new List<Row>();
    }
    public static Row Measure(ActorPoseSampler sampler,LocomotionPresentationProfile profile,AnimatorOverrideController controller,float gait,float angle,bool exploration=false)
    {
        float radians=angle*Mathf.Deg2Rad;var direction=new Vector2(Mathf.Sin(radians),Mathf.Cos(radians));
        var thresholds=exploration?profile.explorationBlendThresholds:profile.blendThresholds;
        float tier=Mathf.Lerp(thresholds.x,thresholds.y,gait);
        AnimatorControllerPlayable active=default;float start=0;int frame=0;
        var row=new Row {gait=gait,angle=angle,exploration=exploration};
        sampler.Sample(profile.run[0].clip,sampleCount:1,controller:controller,configureController:p=>
        {
            active=p;p.SetFloat("LitSpeed",tier);p.SetFloat("LocomotionTier",tier);p.SetFloat("LocomotionPlaybackRate",1);
            p.SetFloat("HorizontalMovement",direction.x);p.SetFloat("ForwardMovement",direction.y);p.SetFloat("CombatMoveMagnitude",1);
            p.SetBool("CombatStrafeActive",!exploration);p.SetBool("IsMoving",true);p.SetLayerWeight(1,0);p.SetLayerWeight(2,0);
            p.Play(exploration?"Base Layer.Locomotion":"Base Layer.CombatLocomotion",0,0);
        },poseCorrection:a=>
        {
            var state=active.GetCurrentAnimatorStateInfo(0);
            if(frame++==0){start=state.normalizedTime;return;}
            row.stateLength=state.length;row.phaseRate=(state.normalizedTime-start)/profile.run[0].clip.length;
            float distance=0,length=0,weight=0;
            foreach(var info in active.GetCurrentAnimatorClipInfo(0))
            {
                if(info.weight<.000001f)continue;
                var cycle=profile.FindCycle(info.clip);
                if(cycle==null)throw new InvalidOperationException("Unexpected gait clip: "+info.clip.name);
                distance+=cycle.nativeSpeed*info.clip.length*info.weight;length+=info.clip.length*info.weight;weight+=info.weight;
            }
            if(weight<.999f)throw new InvalidOperationException("Incomplete gait weights");
            row.nativeDistance=distance/weight;row.weightedLength=length/weight;
            row.observedNativeSpeed=row.nativeDistance*row.phaseRate;
            row.currentCadence=profile.CadenceForPresentedBlend(row.observedNativeSpeed,direction,tier,exploration);
        });
        return row;
    }
    public static void Run()
    {
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder+"/LucianCadence.overrideController");
        var graph=(AnimatorController)controller.runtimeAnimatorController;
        var root=(BlendTree)LocomotionPresentationCalibration.States(graph.layers[0].stateMachine).Single(s=>s.name=="CombatLocomotion").motion;
        foreach(var child in root.children)
        {
            if(Mathf.Abs(child.timeScale-1)>.00001f)throw new InvalidOperationException("Parent time scale requires explicit duration calibration");
            if(child.motion is BlendTree tree&&tree.children.Any(c=>Mathf.Abs(c.timeScale-1)>.00001f))throw new InvalidOperationException("Directional time scale requires explicit duration calibration");
        }
        var report=new Report();
        using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
            foreach(bool exploration in new[]{false,true})foreach(float gait in new[]{0f,.25f,.5f,.75f,1f})for(int i=0;i<16;i++)report.cases.Add(Measure(sampler,profile,controller,gait,i*22.5f,exploration));
        Directory.CreateDirectory("Library/LucianCoherence");
        File.WriteAllText("Library/LucianCoherence/gait-timing.json",JsonUtility.ToJson(report,true));
    }
}
