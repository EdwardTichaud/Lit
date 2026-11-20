using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DialoguePresentationTests
{
    private static DialoguePresentationProfile Profile => AssetDatabase.LoadAssetAtPath<DialoguePresentationProfile>(DialoguePresentationSetup.ProfilePath);

    [Test] public void InstalledContractIsComplete() => DialoguePresentationValidator.Validate();

    [Test] public void MobileBodySuppressionIsPartOfTheReplicatedState()
    {
        var speaking = new DialoguePresentationState { active = true, bodySuppressed = false };
        var walking = speaking;
        walking.bodySuppressed = true;
        Assert.That(speaking.Equals(walking), Is.False);
        using (var writer = new Unity.Netcode.FastBufferWriter(256, Unity.Collections.Allocator.Temp))
        {
            writer.WriteNetworkSerializable(walking);
            using (var reader = new Unity.Netcode.FastBufferReader(writer, Unity.Collections.Allocator.Temp))
            {
                reader.ReadNetworkSerializable(out DialoguePresentationState received);
                Assert.That(received.Equals(walking), Is.True);
            }
        }
    }

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
            Assert.That(index, Is.EqualTo(-1), "Keep Sit Idle when a complete seated gesture is unavailable.");
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


    [Test] public void DialogueUsesCompleteBodyStatesInsteadOfLayers()
    {
        foreach (var g in Profile.gestures.Where(g => g.validated))
        {
            Assert.That(g.body, Is.Not.Null, g.id);
        }
    }

    [Test] public void RoleWithoutCompatibleAnimationsReturnsUnavailable()
    {
        var empty = ScriptableObject.CreateInstance<DialoguePresentationProfile>();
        try { Assert.That(empty.Choose(DialogueRole.Speaking, DialogueIntent.Automatic, false, null, -1, 1), Is.EqualTo(-1)); }
        finally { Object.DestroyImmediate(empty); }
    }
}
