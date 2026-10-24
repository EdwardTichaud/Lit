using System.Reflection;
using Lit.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

public sealed class CycleTimelinePresentationTests
{
    [TestCase(TimelinePlaybackState.Stopped)]
    [TestCase(TimelinePlaybackState.Failed)]
    public void InterruptedDissolveRestoresMaterialsBlocksAndVisibility(TimelinePlaybackState result)
    {
        ExerciseDissolve(result);
    }
    [Test]
    public void CompletedDissolveStaysInvisibleUntilTheQuestCommitsOrDisablesIt()
    {
        ExerciseDissolve(TimelinePlaybackState.Completed);
    }
    private static void ExerciseDissolve(TimelinePlaybackState result)
    {
        var root = new GameObject("Timeline dissolve isolated test");
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.transform.SetParent(root.transform);
        var original = new Material(Shader.Find("HDRP/Unlit"));
        var renderer = visual.GetComponent<Renderer>(); renderer.sharedMaterial = original;
        var block = new MaterialPropertyBlock(); block.SetFloat("_TestPreservedValue", .37f); renderer.SetPropertyBlock(block, 0);
        var effect = root.AddComponent<TimelineDissolveTarget>();
        var director = root.AddComponent<PlayableDirector>();
        try
        {
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("visualRoot").objectReferenceValue = visual.transform;
            serialized.FindProperty("fallbackMaterial").objectReferenceValue = Resources.Load<Material>("EnemyDeathDissolve");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            effect.OnTimelinePlaybackStarted(director);
            Assert.That(renderer.sharedMaterial, Is.Not.SameAs(original));
            Assert.That(original.HasProperty("_DissolveAmount"), Is.False);
            effect.strength = .5f;
            typeof(TimelineDissolveTarget).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effect, null);
            renderer.GetPropertyBlock(block, 0);
            Assert.That(block.GetFloat("_DissolveAmount"), Is.EqualTo(.5f).Within(.001f));
            Assert.That(block.GetFloat("_TestPreservedValue"), Is.EqualTo(.37f).Within(.001f));
            effect.OnTimelinePlaybackFinished(director, result);
            if (result == TimelinePlaybackState.Completed)
            {
                Assert.That(renderer.enabled, Is.False);
                effect.strength = 0; // AnimationTrack stops and resets serialized curves.
                typeof(TimelineDissolveTarget).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effect, null);
                Assert.That(renderer.enabled, Is.False, "Must not flash back while another peer finishes.");
                typeof(TimelineDissolveTarget).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effect, null);
            }
            Assert.That(renderer.enabled, Is.True);
            Assert.That(renderer.sharedMaterial, Is.SameAs(original));
            renderer.GetPropertyBlock(block, 0);
            Assert.That(block.GetFloat("_TestPreservedValue"), Is.EqualTo(.37f).Within(.001f));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(original); }
    }

    [Test]
    public void SharedFailureRestoresAnEffectAlreadyCompletedLocally()
    {
        var root = new GameObject("Shared sequence result test"); root.SetActive(false);
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube); visual.transform.SetParent(root.transform);
        try
        {
            var cycle = root.AddComponent<CycleController>();
            var effect = root.AddComponent<TimelineDissolveTarget>();
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("visualRoot").objectReferenceValue = visual.transform;
            serialized.FindProperty("cycle").objectReferenceValue = cycle;
            serialized.FindProperty("sequenceId").stringValue = "test.release";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            typeof(TimelineDissolveTarget).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effect, null);
            effect.OnTimelinePlaybackStarted(null);
            effect.OnTimelinePlaybackFinished(null, TimelinePlaybackState.Completed);
            Assert.That(visual.GetComponent<Renderer>().enabled, Is.False);
            typeof(CycleController).GetMethod("NotifySequencePresentationFinished", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cycle, new object[] { "test.release", false });
            Assert.That(visual.GetComponent<Renderer>().enabled, Is.True);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
