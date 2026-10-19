#if UNITY_INCLUDE_TESTS
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ContactContinuityTests
{
    [TestCase(false)] [TestCase(true)]
    public void DestroyedThreatCannotInterruptCombatExit(bool destroyWholeObject)
    {
        var host = new GameObject("Destroyed threat exit fixture"); host.SetActive(false);
        var enemy = new GameObject("Destroyed threat"); enemy.SetActive(false);
        var manager = host.AddComponent<RealTimeCombatManager>();
        var target = enemy.AddComponent<EncounterActorTestDouble>();
        ICombatTarget retained = target;
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            typeof(RealTimeCombatManager).GetField("externalTarget", fields).SetValue(manager, retained);
            typeof(RealTimeCombatManager).GetField("combatActive", fields).SetValue(manager, true);
            var threats = (Dictionary<Transform, ICombatTarget>)typeof(RealTimeCombatManager).GetField("worldThreats", fields).GetValue(manager);
            threats.Add(enemy.transform, retained);
            if (destroyWholeObject) Object.DestroyImmediate(enemy);
            else Object.DestroyImmediate(target);
            Assert.That(retained, Is.Not.Null, "The interface must retain the destroyed Unity wrapper.");
            Assert.That(SkillTargetingContract.Exists(retained), Is.False);
            Assert.That(SkillTargetingContract.Alive(retained), Is.False);
            int exits = 0;
            manager.CombatStateChanged += engaged => { Assert.That(engaged, Is.False); exits++; };
            Assert.DoesNotThrow(manager.EndCombat);
            Assert.That(exits, Is.EqualTo(1), "Exit must reach its notification, not abort during target cleanup.");
            Assert.That(manager.IsCombatActive, Is.False);
            Assert.That(manager.ActiveThreatCount, Is.Zero);
            Assert.That(manager.CombatTarget, Is.Null);
            Assert.That(manager.HasLockedCombatTarget, Is.False);
            Assert.DoesNotThrow(manager.EndCombat);
        }
        finally { Object.DestroyImmediate(host); if (enemy != null) Object.DestroyImmediate(enemy); }
    }

    [Test]
    public void DeadInactiveTargetStillReceivesSuspensionCleanup()
    {
        var host = new GameObject("Inactive target cleanup fixture"); host.SetActive(false);
        var enemy = new GameObject("Dead inactive threat"); enemy.SetActive(false);
        var manager = host.AddComponent<RealTimeCombatManager>();
        var target = enemy.AddComponent<EncounterActorTestDouble>();
        target.dead = true; target.suspended = true;
        try
        {
            typeof(RealTimeCombatManager).GetField("externalTarget", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, target);
            Assert.That(SkillTargetingContract.Exists(target), Is.True);
            Assert.That(SkillTargetingContract.Alive(target), Is.False);
            Assert.DoesNotThrow(manager.EndCombat);
            Assert.That(target.suspended, Is.False);
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(enemy); }
    }

    [TestCase(true, 0, InputMode.Exploration)]
    [TestCase(false, 1, InputMode.Exploration)]
    [TestCase(true, 0, InputMode.Combat)]
    [TestCase(false, 1, InputMode.Combat)]
    public void ExplorationToCombatPreservesAttackEnablementOnlyWhenRequested(bool preserve, int cancellations, InputMode initialMode)
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
            type.GetField("baseMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(coordinator, initialMode);
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
