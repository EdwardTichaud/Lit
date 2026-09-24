using System.Reflection;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

public sealed class CombatExplorationRecoveryTests
{
    [Test]
    public void SessionCleanupRunsOnceAndOldCompletionCannotEndReplacement()
    {
        var root = new GameObject("Action session fixture");
        root.SetActive(false);
        try
        {
            var presentation = root.AddComponent<PlayerActionPresentationController>();
            int cleaned = 0;
            int first = presentation.BeginExternalPresentation(root);
            Assert.That(presentation.RegisterActionCleanup(first, () => cleaned++), Is.True);
            int second = presentation.BeginExternalPresentation(root);
            Assert.That(cleaned, Is.EqualTo(1));
            presentation.EndExternalPresentation(first);
            Assert.That(presentation.IsCurrentSession(second), Is.True);
            Assert.That(presentation.RegisterActionCleanup(first, () => cleaned++), Is.False);
            presentation.EndExternalPresentation(second);
            presentation.EndExternalPresentation(second);
            Assert.That(presentation.IsActionActive, Is.False);
            Assert.That(cleaned, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void FailedReplacementPreservesCurrentSessionAndItsResources()
    {
        var root = new GameObject("Action preflight fixture");
        root.SetActive(false);
        try
        {
            var presentation = root.AddComponent<PlayerActionPresentationController>();
            int session = presentation.BeginExternalPresentation(root);
            bool cleaned = false;
            presentation.RegisterActionCleanup(session, () => cleaned = true);
            Assert.That(presentation.TryPlayCombatState("Missing", null, "Invalid"), Is.False);
            Assert.That(presentation.IsCurrentSession(session), Is.True);
            Assert.That(cleaned, Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void SessionTerminationDoesNotReleaseAnotherOwnersUccLock()
    {
        var root = new GameObject("Owned lock fixture");
        root.SetActive(false);
        try
        {
            var bridge = root.AddComponent<LitOpsiveLocomotionBridge>();
            var presentation = root.AddComponent<PlayerActionPresentationController>();
            var locks = GetField<System.Collections.IList>(bridge, "externalLocks");
            var constructor = typeof(LitOpsiveLocomotionBridge.ExternalLockHandle).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic)[0];
            var own = (LitOpsiveLocomotionBridge.ExternalLockHandle)constructor.Invoke(new object[] { bridge, presentation, false });
            var other = (LitOpsiveLocomotionBridge.ExternalLockHandle)constructor.Invoke(new object[] { bridge, root, true });
            locks.Add(own);
            locks.Add(other);
            SetField(bridge, "externalLockCount", 2);
            int session = presentation.BeginExternalPresentation(root);
            presentation.RegisterActionCleanup(session, own.Dispose);
            presentation.EndExternalPresentation(session);
            own.Dispose();
            Assert.That(own.IsReleased, Is.True);
            Assert.That(other.IsReleased, Is.False);
            Assert.That(bridge.IsExternalLockActive, Is.True);
            Assert.That(locks.Count, Is.EqualTo(1));
            // Scene invalidation must make a stale handle inert, including after a new lock arrives.
            typeof(LitOpsiveLocomotionBridge).GetMethod("InvalidateExternalLocks", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(bridge, null);
            var next = (LitOpsiveLocomotionBridge.ExternalLockHandle)constructor.Invoke(new object[] { bridge, root, true });
            locks.Add(next);
            SetField(bridge, "externalLockCount", 1);
            other.Dispose();
            Assert.That(locks.Count, Is.EqualTo(1));
            Assert.That(next.IsReleased, Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ReentrantOldCleanupCannotEndNewSession()
    {
        var root = new GameObject("Reentrant session fixture");
        root.SetActive(false);
        try
        {
            var presentation = root.AddComponent<PlayerActionPresentationController>();
            int first = presentation.BeginExternalPresentation(root);
            int next = 0;
            presentation.RegisterActionCleanup(first, () => next = presentation.BeginExternalPresentation(root));
            presentation.EndExternalPresentation(first);
            presentation.EndExternalPresentation(first);
            Assert.That(presentation.IsCurrentSession(next), Is.True);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void DefensiveHandoffRecognizesOnlyItsOwnSingleUccMotionLock()
    {
        var root = new GameObject("Defensive motion ownership fixture");
        root.SetActive(false);
        try
        {
            var bridge = root.AddComponent<LitOpsiveLocomotionBridge>();
            object owner = new object();
            SetField(bridge, "scriptedPlanarMotionOwner", owner);
            SetField(bridge, "scriptedPlanarMotionLockCount", 1);
            SetField(bridge, "externalLockCount", 1);
            Assert.That(bridge.HasOnlyScriptedMotionLock(owner), Is.True);
            Assert.That(bridge.HasOnlyScriptedMotionLock(new object()), Is.False);
            SetField(bridge, "externalLockCount", 2);
            Assert.That(bridge.HasOnlyScriptedMotionLock(owner), Is.False, "Never release a cinematic's additional lock.");
            SetField(bridge, "externalLockCount", 1);
            SetField(bridge, "scriptedTraversalLockCount", 1);
            Assert.That(bridge.HasOnlyScriptedMotionLock(owner), Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void BasicActionExitDoesNotReplaceGuardAnimationWithIdle()
    {
        var root = new GameObject("Guard ownership fixture");
        var controller = new AnimatorController();
        controller.AddLayer("Base Layer");
        controller.layers[0].stateMachine.AddState("Guard");
        controller.layers[0].stateMachine.AddState("Locomotion");
        try
        {
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            var presentation = root.AddComponent<PlayerActionPresentationController>();
            presentation.ResolveReferences(animator, null);
            animator.Play("Base Layer.Guard", 0, 0f);
            animator.Update(0f);
            SetField(presentation, "actionActive", true);
            SetField(presentation, "activeToken", 42);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("\\[PlayerAction\\] Recovery.*Animator state replaced"));
            typeof(PlayerActionPresentationController).GetMethod("FinishUnexpectedActionExit",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(presentation, new object[] { 42, true });
            animator.Update(.2f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Guard"), Is.True);
            Assert.That(presentation.IsActionActive, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(controller);
        }
    }

    [Test]
    public void HeldBasicAttackNeedsPhysicalReleaseAfterInterruptionEvenAcrossMapDisable()
    {
        var root = new GameObject("Held combo fixture");
        root.SetActive(false);
        var pad = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Gamepad>();
        var action = new UnityEngine.InputSystem.InputAction("BasicAttack", UnityEngine.InputSystem.InputActionType.Button,
            "<Gamepad>/buttonWest");
        try
        {
            action.Enable();
            var input = root.AddComponent<RealTimeCombatInput>();
            SetField(input, "basicAttackAction", action);
            SetField(input, "basicAttackHeld", true);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(pad,
                new UnityEngine.InputSystem.LowLevel.GamepadState().WithButton(UnityEngine.InputSystem.LowLevel.GamepadButton.West));
            UnityEngine.InputSystem.InputSystem.Update();
            input.CancelBufferedBasicSkills();
            action.Disable();
            var update = typeof(RealTimeCombatInput).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            update.Invoke(input, null);
            Assert.That(GetField<bool>(input, "basicAttackHeld"), Is.False);
            Assert.That(GetField<bool>(input, "basicAttackRequiresRelease"), Is.True);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.LowLevel.GamepadState());
            UnityEngine.InputSystem.InputSystem.Update();
            update.Invoke(input, null);
            Assert.That(GetField<bool>(input, "basicAttackRequiresRelease"), Is.False);
            Assert.That(GetField<bool>(input, "basicAttackHeld"), Is.False, "Release rearms; it must not restart attacks.");
        }
        finally
        {
            action.Dispose();
            UnityEngine.InputSystem.InputSystem.RemoveDevice(pad);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void DamageClearsPendingBasicWithoutInterruptingAnUnrelatedAction()
    {
        var root = new GameObject("Basic interruption fixture");
        root.SetActive(false);
        try
        {
            var presentation = root.AddComponent<PlayerActionPresentationController>();
            SetField(presentation, "actionActive", true);
            SetField(presentation, "activeActionIsBasic", false);
            SetField(presentation, "hasBufferedAction", true);
            SetField(presentation, "bufferedActionIsBasic", true);
            presentation.InterruptBasicSkillForDamage();
            Assert.That(GetField<bool>(presentation, "hasBufferedAction"), Is.False);
            Assert.That(presentation.IsActionActive, Is.True);
        }
        finally { Object.DestroyImmediate(root); }
    }

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
