using System.Reflection;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

public sealed class CombatExplorationRecoveryTests
{
    [TestCase("CombatIdle", false, "Locomotion")]
    [TestCase("Attack", true, "Locomotion")]
    [TestCase("Landing", false, "Landing")]
    [TestCase("Death", false, "Death")]
    public void CombatExitReturnsCombatAnimationsToLocomotionButPreservesOtherStates(
        string initial, bool actionActive, string expected)
    {
        var root = new GameObject("Combat animation exit fixture");
        var controller = new AnimatorController();
        controller.AddLayer("Base Layer");
        var machine = controller.layers[0].stateMachine;
        foreach (string name in new[] { "Locomotion", "CombatIdle", "Attack", "Landing", "Death" })
            machine.AddState(name);
        try
        {
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            var presentation = root.AddComponent<PlayerActionPresentationController>();
            presentation.ResolveReferences(animator, null);
            animator.Play("Base Layer." + initial, 0, 0f);
            animator.Update(0f);
            SetField(presentation, "actionActive", actionActive);
            SetField(presentation, "deathAnimationLocked", initial == "Death");
            presentation.ReturnToExplorationAfterCombat();
            animator.Update(1f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + expected), Is.True);
            Assert.That(presentation.IsActionActive, Is.False);
            Assert.That(presentation.IsDeathAnimationLocked, Is.EqualTo(initial == "Death"));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(controller);
        }
    }

    [Test]
    public void EndCombatCancelsAnInFlightPlayerAction()
    {
        GameObject root = new GameObject("Combat exploration recovery fixture");
        try
        {
            RealTimeCombatManager manager = root.AddComponent<RealTimeCombatManager>();
            PlayerActionPresentationController presentation = root.AddComponent<PlayerActionPresentationController>();
            SetField(manager, "playerActionPresentation", presentation);
            SetField(manager, "combatActive", true);
            SetField(presentation, "actionActive", true);
            SetField(presentation, "hasBufferedAction", true);

            manager.EndCombat();

            Assert.That(manager.IsPlayerActionActive, Is.False);
            Assert.That(GetField<bool>(presentation, "hasBufferedAction"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void SetField(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static T GetField<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
}
