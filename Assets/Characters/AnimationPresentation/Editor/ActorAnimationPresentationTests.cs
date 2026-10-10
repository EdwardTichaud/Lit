#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class ActorAnimationPresentationTests
{
    private static IEnumerable<string> RepairNames => AssetDatabase.FindAssets("t:AnimationClip",new[] {ActorAnimationAudit.EnemyFolder + "Animations"})
        .Select(AssetDatabase.GUIDToAssetPath).Select(p => AssetDatabase.LoadAssetAtPath<AnimationClip>(p).name.Replace("Juggernaut_v2_",""))
        .Where(n => ActorAnimationAudit.EnemySource(n) != null);
    [TestCaseSource(nameof(RepairNames))]
    public void ReconstructionRetainsSourcePoseAndEveryEvent(string name)
    {
        var source = ActorAnimationAudit.Source(ActorAnimationAudit.EnemySource(name));
        var target = AssetDatabase.LoadAssetAtPath<AnimationClip>(ActorAnimationAudit.EnemyFolder + "Animations/Juggernaut_v2_" + name + ".anim");
        var actor = ActorAnimationAudit.Actor(true);
        var candidate = ActorClipReconstruction.Candidate(source,target,actor);
        try
        {
            var fidelity = ActorClipReconstruction.Compare(actor,source,candidate);
            Assert.LessOrEqual(fidelity.positionError,.005f); Assert.LessOrEqual(fidelity.angleError,.5f);
            Assert.AreEqual(ActorClipReconstruction.Events(target),ActorClipReconstruction.Events(candidate));
            var repeated = ActorClipReconstruction.Candidate(source,candidate,actor);
            try { Assert.AreEqual(EditorJsonUtility.ToJson(candidate),EditorJsonUtility.ToJson(repeated),"A second reconstruction must not change the clip."); }
            finally { UnityEngine.Object.DestroyImmediate(repeated); }
            using (var sampling = new PlayerInPlaceSampling(actor))
            {
                var motion = sampling.Sample(candidate);
                Assert.LessOrEqual(motion.MaxDisplacement,.005f); Assert.LessOrEqual(motion.MaxYaw,.1f);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(candidate); }
    }
    [Test]
    public void NativeCadenceUsesObtainedVelocityAndDoesNotWritePhysics()
    {
        var profile = ScriptableObject.CreateInstance<LocomotionPresentationProfile>();
        var clip = new AnimationClip();
        try
        {
            profile.walk = new[] { new LocomotionPresentationProfile.Cycle {clip=clip,nativeSpeed=1.2f} };
            profile.run = new[] { new LocomotionPresentationProfile.Cycle {clip=clip,nativeSpeed=3.1f} };
            profile.Evaluate(1.2f,false,Vector2.up,out var blend,out var rate);
            Assert.AreEqual(1,rate,.0001f); Assert.AreEqual(profile.blendThresholds.x,blend,.0001f);
            profile.Evaluate(2.4f,false,Vector2.up,out blend,out rate);
            Assert.AreEqual(2,rate,.0001f);
            profile.Evaluate(0,true,Vector2.up,out blend,out rate);
            Assert.AreEqual(0,blend); Assert.AreEqual(1,rate);
        }
        finally { UnityEngine.Object.DestroyImmediate(profile); UnityEngine.Object.DestroyImmediate(clip); }
    }
    [Test]
    public void UnresolvedSourceIsNeverApproximated()
    {
        Assert.Throws<InvalidOperationException>(() => ActorAnimationAudit.Source("Assets/Missing_Source.fbx"));
    }
    [Test]
    public void RecoveryRetimingOnlyBeginsAfterGameplayRecoveryMarker()
    {
        var clip = new AnimationClip();
        try
        {
            clip.SetCurve("",typeof(Transform),"localPosition.x",AnimationCurve.Linear(0,0,2,0));
            var handoff = new VisualActionHandoff {sourceExitWindow=new Vector2(.4f,1)};
            Assert.AreEqual(6,handoff.RecoveryRate(clip,.2f),.001f);
            Assert.IsFalse(handoff.validated,"Unmeasured exits must not activate implicitly.");
        }
        finally { UnityEngine.Object.DestroyImmediate(clip); }
    }
    [Test]
    public void LocomotionMultiplierCannotAffectActionStates()
    {
        foreach (bool enemy in new[] {false,true})
        {
            var controller = ActorAnimationAudit.Actor(enemy).runtimeAnimatorController as AnimatorController;
            foreach (var state in controller.layers.SelectMany(l => LocomotionPresentationCalibration.States(l.stateMachine)))
                if (state.speedParameter == LocomotionPresentationProfile.PlaybackParameter && state.speedParameterActive)
                    Assert.That(state.name,Is.EqualTo("Locomotion").Or.EqualTo("CombatLocomotion"));
        }
    }
}
#endif
