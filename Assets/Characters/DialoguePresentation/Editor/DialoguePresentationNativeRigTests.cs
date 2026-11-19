using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class DialoguePresentationNativeRigTests
{
    private GameObject actor;
    private Animator animator;
    private AnimatorController controller;
    private AnimationClip upper, seated;
    private AvatarMask upperMask, freeMask;
    private DialoguePresentationProfile profile;

    [SetUp] public void Setup()
    {
        actor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("db19a32c9d8c08a4681098e8b4cd781f")));
        actor.hideFlags=HideFlags.HideAndDontSave;
        animator=actor.GetComponent<Animator>();
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion=false;
        animator.fireEvents=false;
        upper=AssetDatabase.LoadAssetAtPath<AnimationClip>(DialoguePresentationSetup.Folder + "/Speak_Explain_C_UpperBody.anim");
        seated=AssetDatabase.LoadAssetAtPath<AnimationClip>(DialoguePresentationSetup.Folder + "/Speak_Seated_UpperBody.anim");
        upperMask=AssetDatabase.LoadAssetAtPath<AvatarMask>(DialoguePresentationSetup.Folder + "/DialogueUpperBody.mask");
        freeMask=AssetDatabase.LoadAssetAtPath<AvatarMask>(DialoguePresentationSetup.Folder + "/DialogueFreeArm.mask");
        controller=new AnimatorController();
        var machine=new AnimatorStateMachine {name="Base Layer"};
        controller.AddLayer(new AnimatorControllerLayer{name="Base Layer",stateMachine=machine,defaultWeight=1});
        machine.AddState("Locomotion").motion=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Mixamo/Interaction/Lucian/Mixamo_Idle_Breathing.anim");
        machine.AddState("Sitting_Idle").motion=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Autres/Interaction/Lucian/Lucian_Sitting_Idle_ClosedMouth.anim");
        foreach(var name in CharacterDialoguePresentationController.Layers)
        {
            var graph=new AnimatorStateMachine {name=name};
            controller.AddLayer(new AnimatorControllerLayer{name=name,stateMachine=graph,avatarMask=name==CharacterDialoguePresentationController.Layers[2]?freeMask:upperMask,defaultWeight=0});
            graph.AddState("Speech").motion=upper;
            graph.AddState("SeatedSpeech").motion=seated;
            graph.AddState("Wait").motion=upper;
        }
        profile=ScriptableObject.CreateInstance<DialoguePresentationProfile>();
        profile.gestures.Add(new DialoguePresentationProfile.Gesture{id="Speech",upperBody=upper,role=DialogueRole.Speaking,intent=DialogueIntent.Explain,validated=true});
        profile.gestures.Add(new DialoguePresentationProfile.Gesture{id="Wait",upperBody=upper,role=DialogueRole.Waiting,intent=DialogueIntent.Neutral,validated=true});
        animator.runtimeAnimatorController=controller;
        animator.Rebind();
        animator.Play("Base Layer.Locomotion");
        animator.Update(0);
    }

    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(actor);
        foreach(var layer in controller.layers)
        {
            foreach(var state in layer.stateMachine.states) UnityEngine.Object.DestroyImmediate(state.state);
            UnityEngine.Object.DestroyImmediate(layer.stateMachine);
        }
        UnityEngine.Object.DestroyImmediate(controller);
        UnityEngine.Object.DestroyImmediate(profile);
    }

    private Vector3[] Pose()=>new[]{HumanBodyBones.Hips,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,
        HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,HumanBodyBones.LeftHand}
        .Select(b=>animator.GetBoneTransform(b).position).ToArray();

    private List<Vector3[]> Sample(bool sit,int layer,bool show)
    {
        animator.Rebind();
        animator.Play(sit?"Base Layer.Sitting_Idle":"Base Layer.Locomotion",0,.2f);
        for(int i=1;i<animator.layerCount;i++)animator.SetLayerWeight(i,0);
        if(show)
        {
            animator.Play(CharacterDialoguePresentationController.Layers[layer-1]+"."+(sit?"SeatedSpeech":"Speech"),layer,.2f);
            animator.SetLayerWeight(layer,1);
        }
        var poses=new List<Vector3[]>();
        for(int i=0;i<120;i++){animator.Update(1f/60);poses.Add(Pose());}
        return poses;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RealAvatarRetainsFeetAndPelvisUnderDialogue(bool sit)
    {
        var before=Sample(sit,2,false);
        var after=Sample(sit,2,true);
        float maximum=0;
        for(int i=0;i<before.Count;i++)for(int b=0;b<3;b++)maximum=Mathf.Max(maximum,Vector3.Distance(before[i][b],after[i][b]));
        Debug.Log("NATIVE_RIG_LOWER_DELTA "+sit+" "+maximum);
        Assert.That(maximum,Is.LessThan(.0001f));
    }

    [Test] public void TorchMaskPreservesTheEntireRightArm()
    {
        var before=Sample(false,3,false);
        var after=Sample(false,3,true);
        float maximum=0;
        for(int i=0;i<before.Count;i++)for(int b=3;b<6;b++)maximum=Mathf.Max(maximum,Vector3.Distance(before[i][b],after[i][b]));
        Debug.Log("NATIVE_RIG_TORCH_ARM_DELTA "+maximum);
        Assert.That(maximum,Is.LessThan(.0001f));
    }

    [Test] public void GestureActuallyMovesTheFreeHand()
    {
        var before=Sample(false,3,false);
        var after=Sample(false,3,true);
        float maximum=0;
        for(int i=0;i<before.Count;i++)maximum=Mathf.Max(maximum,Vector3.Distance(before[i][6],after[i][6]));
        Assert.That(maximum,Is.GreaterThan(.01f), "Positive control: dialogue must visibly affect the unmasked hand.");
    }

    [Test] public void ActualControllerFadesReturnsAndRejectsStaleCompletions()
    {
        var owner=new GameObject("Dialogue owner");
        try
        {
            var driver=actor.AddComponent<CharacterDialoguePresentationController>();
            var settings=new SerializedObject(driver);
            settings.FindProperty("fallbackProfile").objectReferenceValue=profile;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var physicalPosition=actor.transform.position;
            int first=driver.Begin(owner,null,false,null,DialogueRole.Speaking);
            Assert.That(first,Is.GreaterThan(0));
            for(int i=0;i<30;i++){driver.Refresh(1f/60);animator.Update(1f/60);}
            Assert.That(animator.GetLayerWeight(2),Is.GreaterThan(.95f));
            int second=driver.Begin(owner,null,false,null,DialogueRole.Speaking);
            driver.End(first);
            Assert.That(driver.IsEngaged,Is.True);
            driver.FinishSpeaking(second);
            driver.Refresh(1f/60);animator.Update(1f/60);
            Assert.That(profile.gestures[driver.CurrentGesture].role,Is.EqualTo(DialogueRole.Waiting));
            driver.End(second);
            for(int i=0;i<30;i++){driver.Refresh(1f/60);animator.Update(1f/60);}
            Assert.That(animator.GetLayerWeight(2),Is.Zero);
            Assert.That(Vector3.Distance(actor.transform.position,physicalPosition),Is.LessThan(.00001f));
            Assert.That(animator.applyRootMotion,Is.False);
        }
        finally{UnityEngine.Object.DestroyImmediate(owner);}
    }
}
