using System;
using System.Linq;
using Lit.Timeline;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public sealed class BelmontReleaseSceneTests
{
    [Test]
    public void SavedReleaseHasEditableClipsBoundTargetsAndPersistentHandoff()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Cycles/Cycle_Belmont/District_1_Cycle_Belmont.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var cycle = roots.SelectMany(root => root.GetComponentsInChildren<CycleController>(true)).Single();
            var sequence = cycle.sequences.Single(binding => binding.id == "anchor_release");
            var director = sequence.director;
            var timeline = director.playableAsset as TimelineAsset;
            Assert.That(timeline.duration, Is.EqualTo(6).Within(.001));
            Assert.That(Lit.Editor.TimelineBindingsValidator.CollectBindingCoverageIssues(director, sequence.profile), Is.Empty);
            Assert.That(sequence.profile.Bindings.Count, Is.EqualTo(4));
            var targets = director.GetComponent<TimelineBindingTarget>();
            Assert.That(targets, Is.Not.Null);
            foreach (var binding in sequence.profile.Bindings)
            {
                if (binding.bindingId == "camera.cinemachine_brain") continue;
                var target = targets.Targets.Single(entry => entry.bindingId == binding.bindingId).target;
                Assert.That(target, Is.Not.Null);
                Assert.That(director.GetGenericBinding(binding.track), Is.SameAs(target), "Editor preview must also be bound.");
                var animation = (AnimationPlayableAsset)binding.track.GetClips().Single().asset;
                Assert.That(animation.clip, Is.Not.Null);
            }
            var cameraTrack = timeline.GetOutputTracks().OfType<CinemachineTrack>().Single();
            Assert.That(cameraTrack.GetClips().Count(), Is.EqualTo(2));
            foreach (var clip in cameraTrack.GetClips())
                Assert.That(((CinemachineShot)clip.asset).VirtualCamera.Resolve(director), Is.Not.Null);
            Assert.That(director.GetComponent<LitTimelineCinemachineBridge>(), Is.Not.Null);
            var boss = roots.SelectMany(root => root.GetComponentsInChildren<BrokenAnchorBoss>(true)).Single();
            var activation = cycle.activations.Single(binding => binding.target != null && boss.transform.IsChildOf(binding.target.transform));
            Assert.That(activation.useHideCondition, Is.True);
            Assert.That(activation.hideWhen.requirements.conditions.Any(condition => condition.stepId == "lower_flames_restored"), Is.True);
            Assert.That(cycle.disappearances.Any(binding => binding.target == activation.target), Is.False, "No second competing dissolve after the Timeline.");
            Assert.That(cycle.definition.steps.Single(step => step.terminal).id, Is.EqualTo("first_ancient_flame_lit"));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
