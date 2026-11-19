using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DialoguePresentationTests
{
    private static DialoguePresentationProfile Profile => AssetDatabase.LoadAssetAtPath<DialoguePresentationProfile>(DialoguePresentationSetup.ProfilePath);

    [Test] public void InstalledContractIsComplete() => DialoguePresentationValidator.Validate();

    [Test] public void OldLinesReceiveAnExpressiveDefault()
    {
        Assert.That(Profile.defaultIntent, Is.EqualTo(DialogueIntent.Explain));
        int index = Profile.Choose(DialogueRole.Speaking, DialogueIntent.Automatic, false, null, -1, 17);
        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        Assert.That(Profile.gestures[index].intent, Is.EqualTo(DialogueIntent.Explain));
    }

    [Test] public void SeatedSpeechNeverSelectsAStandingOnlyGesture()
    {
        foreach (DialogueIntent intent in System.Enum.GetValues(typeof(DialogueIntent)))
        {
            int index = Profile.Choose(DialogueRole.Speaking, intent, true, null, -1, 45);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That(Profile.gestures[index].seated, Is.True);
        }
    }

    [Test] public void AValidExplicitGestureOverridesAutomaticChoice()
    {
        var g = Profile.gestures.First(e => e.validated && e.role == DialogueRole.Speaking && e.standing);
        Assert.That(Profile.gestures[Profile.Choose(DialogueRole.Speaking, DialogueIntent.Automatic, false, g.id, -1, 2)].id, Is.EqualTo(g.id));
    }

    [Test] public void VariationAvoidsImmediateRepetition()
    {
        int previous = -1;
        for (uint seed = 1; seed <= 20; seed++)
        {
            int next = Profile.Choose(DialogueRole.Speaking, DialogueIntent.Explain, false, null, previous, seed * 11213);
            Assert.That(next, Is.Not.EqualTo(previous));
            previous = next;
        }
    }

    [Test] public void AFormerLineCannotReleaseTheNewPresentation()
    {
        var actor = new GameObject("Dialogue test actor");
        var owner = new GameObject("Dialogue test owner");
        try
        {
            var controller = actor.AddComponent<CharacterDialoguePresentationController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("fallbackProfile").objectReferenceValue = Profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            int first = controller.Begin(owner, null, false, null, DialogueRole.Speaking);
            int second = controller.Begin(owner, null, false, null, DialogueRole.Speaking);
            Assert.That(first, Is.GreaterThan(0));
            Assert.That(second, Is.GreaterThan(first));
            controller.End(first);
            controller.FinishSpeaking(first);
            Assert.That(controller.IsEngaged, Is.True);
            Assert.That(controller.Generation, Is.EqualTo(second));
            controller.FinishSpeaking(second);
            Assert.That(Profile.gestures[controller.CurrentGesture].role, Is.EqualTo(DialogueRole.Waiting));
            controller.End(second);
            Assert.That(controller.IsEngaged, Is.False);
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(owner); }
    }

    [Test] public void MouthOpeningIsNeverAdmittedByDialogueExpressions()
    {
        Assert.That(FacialExpressionController.IsDialogueSafeShape("Jaw_Open"), Is.False);
        Assert.That(FacialExpressionController.IsDialogueSafeShape("Mouth_Open"), Is.False);
        Assert.That(FacialExpressionController.IsDialogueSafeShape("Brow_Raise_Inner_L"), Is.True);
    }


    [TestCase(false)]
    [TestCase(true)]
    public void UpperBodyDialoguePreservesBaseFeetAndPelvis(bool seated)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab");
        var source = prefab.GetComponent<CharacterAnimationController>().Animator;
        var gesture = Profile.gestures.First(g => g.validated && g.role == DialogueRole.Speaking && (seated ? g.seated : g.standing));
        int layer = System.Array.IndexOf(source.runtimeAnimatorController.animationClips, gesture.upperBody);
        Assert.That(layer, Is.GreaterThanOrEqualTo(0));
        using (var sampler = new ActorPoseSampler(source))
        {
            var baseline = sampler.Sample(gesture.upperBody, sampleCount: 60, controller: source.runtimeAnimatorController,
                configureController: p =>
                {
                    p.SetBool("IsSitting", seated);
                    p.SetFloat("LitSpeed", 0);
                    p.Play(seated ? "Base Layer.Sitting_Idle" : "Base Layer.Locomotion", 0, .2f);
                });
            var animated = sampler.Sample(gesture.upperBody, sampleCount: 60, controller: source.runtimeAnimatorController,
                configureController: p =>
                {
                    p.SetBool("IsSitting", seated);
                    p.SetFloat("LitSpeed", 0);
                    p.Play(seated ? "Base Layer.Sitting_Idle" : "Base Layer.Locomotion", 0, .2f);
                    int overlay = p.GetLayerIndex(CharacterDialoguePresentationController.Layers[1]);
                    p.SetLayerWeight(overlay, 1);
                    p.Play(CharacterDialoguePresentationController.Layers[1] + "." + gesture.id, overlay, .3f);
                });
            for (int i = 0; i < baseline.Length; i++)
            {
                Assert.That(Vector3.Distance(baseline[i].body, animated[i].body), Is.LessThan(.0001f), "Pelvis at sample " + i);
                Assert.That(Vector3.Distance(baseline[i].leftFoot, animated[i].leftFoot), Is.LessThan(.0001f), "Left foot at sample " + i);
                Assert.That(Vector3.Distance(baseline[i].rightFoot, animated[i].rightFoot), Is.LessThan(.0001f), "Right foot at sample " + i);
            }
        }
    }

    [Test] public void RoleWithoutCompatibleAnimationsReturnsUnavailable()
    {
        var empty = ScriptableObject.CreateInstance<DialoguePresentationProfile>();
        try { Assert.That(empty.Choose(DialogueRole.Speaking, DialogueIntent.Automatic, false, null, -1, 1), Is.EqualTo(-1)); }
        finally { Object.DestroyImmediate(empty); }
    }
}
