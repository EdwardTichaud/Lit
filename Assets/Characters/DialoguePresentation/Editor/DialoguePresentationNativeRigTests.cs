using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Checks complete source poses and the only retained body mask on the production rig.</summary>
public class DialoguePresentationNativeRigTests
{
    private static DialoguePresentationProfile Profile => AssetDatabase.LoadAssetAtPath<DialoguePresentationProfile>(DialoguePresentationSetup.ProfilePath);
    [Test] public void DialogueCopiesKeepTheOriginalLegAndSpineCurves()
    {
        foreach (var gesture in Profile.gestures.Where(g => g.validated && g.role == DialogueRole.Speaking))
        foreach (var binding in AnimationUtility.GetCurveBindings(gesture.source).Where(b =>
            b.type == typeof(Animator) && (b.propertyName.Contains("Leg") || b.propertyName.Contains("Foot") || b.propertyName.Contains("Spine"))))
        {
            var original = AnimationUtility.GetEditorCurve(gesture.source, binding);
            var copy = AnimationUtility.GetEditorCurve(gesture.body, binding);
            Assert.That(copy, Is.Not.Null, gesture.id + " / " + binding.propertyName);
            Assert.That(copy.keys, Is.EqualTo(original.keys), gesture.id + " / " + binding.propertyName);
        }
    }
    [Test] public void TorchMaskDoesNotReplacePelvisFeetOrTheFreeArm()
    {
        var source = ActorAnimationAudit.Actor(false);
        var gesture = Profile.gestures.First(g => g.validated && g.role == DialogueRole.Speaking && g.standing);
        using (var sampler = new ActorPoseSampler(source))
        {
            ActorPoseSampler.Frame[] Sample(bool torch) => sampler.Sample(gesture.body, sampleCount:120, controller:source.runtimeAnimatorController,
                configureController:a => {
                    a.SetFloat("LitSpeed",0);
                    a.Play("Base Layer.Dialogue_" + gesture.id,0,.2f);
                    int layer=a.GetLayerIndex("Upper Body Flame");
                    a.SetLayerWeight(layer,torch ? 1 : 0);
                    if(torch)a.Play("Upper Body Flame.Flame_Locomotion",layer,.25f);
                });
            var before=Sample(false);var after=Sample(true);
            float handChange=0;
            for(int i=0;i<before.Length;i++)
            {
                foreach(var b in new[]{HumanBodyBones.Hips,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,
                    HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand})
                    Assert.That(Vector3.Distance(before[i].bonePositions[(int)b],after[i].bonePositions[(int)b]),Is.LessThan(.0001f),b+" sample "+i);
                handChange=Mathf.Max(handChange,Vector3.Distance(before[i].bonePositions[(int)HumanBodyBones.RightHand],after[i].bonePositions[(int)HumanBodyBones.RightHand]));
            }
            Assert.That(handChange,Is.GreaterThan(.005f),"Positive control: the torch layer must animate the holding arm.");
        }
    }
    [Test] public void LucianLocomotionUsesManuallyAuthoredStateSpeeds()
    {
        foreach (string path in SimpleLucianAnimationSetup.ControllerPaths)
        {
            var c=AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
            Assert.That(c.parameters.Any(p=>p.name=="LocomotionPlaybackRate"),Is.False,path);
            foreach(var state in LocomotionPresentationCalibration.States(c.layers[0].stateMachine))
                if(state.name=="Locomotion" || state.name=="CombatLocomotion")
                    Assert.That(state.speedParameterActive,Is.False,path+" / "+state.name);
        }
    }
    [Test] public void LucianPrefabsHaveNoMissingComponents()
    {
        foreach (string path in SimpleLucianAnimationSetup.PrefabPaths)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach(var node in prefab.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), Is.Zero, path+" / "+node.name);
        }
    }
    [Test] public void LocomotionProfileHasNoPoseCompensationData()
    {
        var data=AssetDatabase.LoadAssetAtPath<CharacterData>(SimpleLucianAnimationSetup.DataPath);
        var p=data.playerSettings.locomotion.presentationProfile;
        Assert.That(typeof(LocomotionPresentationProfile).GetField("footSupports"), Is.Null);
        Assert.That(typeof(LocomotionPresentationProfile.Cycle).GetField("footGoals"), Is.Null);
    }
}
