using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class JuggernautV2Tests
{
    [Test]
    public void PrototypePreservesModelAndHasIndependentDataAndAnimator()
    {
        Assert.DoesNotThrow(JuggernautV2Setup.Validate);
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(JuggernautV2Setup.OriginalPrefab);
        var prototype = AssetDatabase.LoadAssetAtPath<GameObject>(JuggernautV2Setup.PrefabPath);
        var brain = prototype.GetComponent<JuggernautV2Brain>();
        Assert.That(brain, Is.InstanceOf<LitBrainsEnemy>());
        Assert.That(prototype.GetComponent<Animator>().runtimeAnimatorController,
            Is.Not.EqualTo(original.GetComponent<Animator>().runtimeAnimatorController));
        Assert.That(brain.characterData, Is.Not.EqualTo(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset")));
        Assert.That(brain.characterData.hp, Is.EqualTo(300));
        Assert.That(brain.detectionTargetOverride, Is.Null);
        Assert.That(prototype.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled, Is.False);
        var before = original.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var after = prototype.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Assert.That(before.Length, Is.EqualTo(after.Length));
        for (int i = 0; i < before.Length; i++) Assert.That(after[i].sharedMesh, Is.EqualTo(before[i].sharedMesh));
    }

    [Test]
    public void EachAttackIdHasOneTransitionAndNeverCallsLegacyAnimationEvents()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(JuggernautV2Setup.ControllerPath);
        var machine = controller.layers[0].stateMachine;
        var attacks = machine.anyStateTransitions.Where(t => t.destinationState.tag == "Attack").ToArray();
        Assert.That(attacks.Length, Is.EqualTo(3));
        for (int id = 1; id <= 3; id++)
        {
            Assert.That(attacks.Count(t => t.conditions.Any(c => c.parameter == "Attack ID" && c.mode == AnimatorConditionMode.Equals && c.threshold == id)), Is.EqualTo(1));
        }
        foreach (var clip in controller.animationClips)
        {
            var events = AnimationUtility.GetAnimationEvents(clip);
            if (clip.name.EndsWith("Strike") || clip.name.EndsWith("Sweep") || clip.name.EndsWith("Followup"))
            {
                Assert.That(events.Length, Is.EqualTo(4));
                Assert.That(events[0].functionName, Is.EqualTo("OpenBrainsReactionOpportunity"));
                Assert.That(events[1].functionName, Is.EqualTo("BeginBrainsStrike"));
                Assert.That(events[2].functionName, Is.EqualTo("ResolveBrainsAttackImpact"));
                Assert.That(events[3].functionName, Is.EqualTo("BeginBrainsRecovery"));
                Assert.That(events[0].time, Is.GreaterThan(0).And.LessThan(clip.length));
            }
            else Assert.That(events, Is.Empty);
        }
        Assert.That(machine.states.First(s => s.state.name == "Death").state.transitions, Is.Empty);
    }
}
