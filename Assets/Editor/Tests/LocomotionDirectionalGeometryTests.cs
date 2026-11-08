using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

public sealed class LocomotionDirectionalGeometryTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void MixedCadenceMatchesTheControllersActualPhaseClock(bool exploration)
    {
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder+"/LucianCadence.overrideController");
        using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
        foreach(float gait in new[]{0f,.25f,.5f,.75f,1f})for(int i=0;i<16;i++)
        {
            var row=LucianGaitTimingAudit.Measure(sampler,profile,controller,gait,i*22.5f,exploration);
            Assert.That(row.stateLength,Is.EqualTo(row.weightedLength).Within(.00001f),"Native timing contract changed");
            Assert.That(row.currentCadence,Is.EqualTo(1).Within(.00001f),"gait="+gait+" angle="+i*22.5f);
        }
    }
    [TestCase(false)]
    [TestCase(true)]
    public void MeasuredNativeSpeedMatchesActualUnityDirectionalWeights(bool running)
    {
        var original=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        var profile=Object.Instantiate(original);profile.matchSimpleDirectionalBlend=true;
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder+"/LucianCadence.overrideController");
        try
        {
            using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
            for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI/8;var direction=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
                float tier=running?profile.blendThresholds.y:profile.blendThresholds.x;
                AnimatorControllerPlayable active=default;float expected=0;
                sampler.Sample(profile.run[0].clip,sampleCount:1,controller:controller,configureController:p=>
                {
                    active=p;p.SetFloat("LitSpeed",tier);p.SetFloat("LocomotionTier",tier);p.SetFloat("LocomotionPlaybackRate",1);
                    p.SetFloat("HorizontalMovement",direction.x);p.SetFloat("ForwardMovement",direction.y);p.SetFloat("CombatMoveMagnitude",1);
                    p.SetBool("CombatStrafeActive",true);p.SetBool("IsMoving",true);p.SetLayerWeight(1,0);p.SetLayerWeight(2,0);
                    p.Play("Base Layer.CombatLocomotion",0,0);
                },poseCorrection:a=>
                {
                    float sum=0,total=0;
                    foreach(var info in active.GetCurrentAnimatorClipInfo(0))
                    {
                        if(info.weight<.000001f)continue;
                        var cycle=profile.FindCycle(info.clip);
                        Assert.That(cycle,Is.Not.Null,"Unexpected clip at "+i+": "+info.clip.name);
                        sum+=cycle.nativeSpeed*info.weight;total+=info.weight;
                    }
                    Assert.That(total,Is.GreaterThan(.999f));expected=sum/total;
                });
                Assert.That(profile.NativeSpeed(running,direction),Is.EqualTo(expected).Within(.001f),"Unity weights at angle "+i*22.5f);
            }
        }
        finally{Object.DestroyImmediate(profile);}
    }
}
