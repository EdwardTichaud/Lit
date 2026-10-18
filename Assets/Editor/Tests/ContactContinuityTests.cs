#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ContactContinuityTests
{
    [TestCase(true, 0)]
    [TestCase(false, 1)]
    public void ExplorationToCombatPreservesAttackEnablementOnlyWhenRequested(bool preserve, int cancellations)
    {
        var host = new GameObject("Input continuity fixture");
        host.SetActive(false);
        var coordinator = host.AddComponent<InputModeCoordinator>();
        var asset = ScriptableObject.CreateInstance<InputActionAsset>();
        var type = typeof(InputModeCoordinator);
        var instance = type.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        var previous = instance.GetValue(null);
        System.Action<object, InputActionChange> observe = null;
        try
        {
            asset.AddActionMap("Player").AddAction("Move");
            asset.AddActionMap("Camera").AddAction("Look");
            var attack = asset.AddActionMap("RealTimeCombat").AddAction("BasicAttack", InputActionType.Button, "<Gamepad>/buttonWest");
            type.GetField("actions", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(coordinator, asset);
            instance.SetValue(null, coordinator);
            asset.FindActionMap("Player").Enable(); asset.FindActionMap("Camera").Enable(); attack.Enable();
            int cancelled = 0;
            observe = (owner, change) =>
            {
                if (change == InputActionChange.ActionMapDisabled && ReferenceEquals(owner, attack.actionMap) ||
                    change == InputActionChange.ActionDisabled && ReferenceEquals(owner, attack)) cancelled++;
            };
            InputSystem.onActionChange += observe;
            InputModeCoordinator.SetBaseMode(InputMode.Combat, preserve);
            Assert.That(cancelled, Is.EqualTo(cancellations));
            Assert.That(attack.enabled, Is.True);
        }
        finally
        {
            if (observe != null) InputSystem.onActionChange -= observe;
            asset.Disable();
            Object.DestroyImmediate(host); Object.DestroyImmediate(asset); instance.SetValue(null, previous);
        }
    }

    [TestCase(InputMode.Dialogue)] [TestCase(InputMode.Cinematic)] [TestCase(InputMode.UserInterface)]
    public void ContinuousCombatRequestCannotBypassHigherPriorityContext(InputMode mode)
    {
        var host = new GameObject("Protected input fixture"); host.SetActive(false);
        var coordinator = host.AddComponent<InputModeCoordinator>();
        var asset = ScriptableObject.CreateInstance<InputActionAsset>();
        var type = typeof(InputModeCoordinator);
        var instance = type.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        var previous = instance.GetValue(null);
        var owner = new object();
        try
        {
            var attack = asset.AddActionMap("RealTimeCombat").AddAction("BasicAttack");
            type.GetField("actions", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(coordinator, asset);
            instance.SetValue(null, coordinator);
            InputModeCoordinator.Enter(owner, mode);
            InputModeCoordinator.SetBaseMode(InputMode.Combat, true);
            Assert.That(InputModeCoordinator.CurrentMode, Is.EqualTo(mode));
            Assert.That(attack.enabled, Is.False);
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(asset); instance.SetValue(null, previous); }
    }

    [Test]
    public void EndedSessionCannotEngageOrRedirectItsTarget()
    {
        var host = new GameObject("Session fixture"); host.SetActive(false);
        var manager = host.AddComponent<RealTimeCombatManager>();
        var actor = new GameObject("Actor"); actor.SetActive(false);
        var presentation = actor.AddComponent<PlayerActionPresentationController>();
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            var context = new PlayerAttackContext(7, skill, Vector3.zero, Vector3.forward, null, false);
            Assert.That(context.TryConsume(7), Is.True);
            Assert.That(presentation.IsCurrentSession(7), Is.False);
            Assert.That(manager.TryEngageFromPlayerContact(actor.transform, null, context), Is.False);
            Assert.That(manager.IsCombatActive, Is.False);
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(actor); Object.DestroyImmediate(skill); }
    }
}
#endif
