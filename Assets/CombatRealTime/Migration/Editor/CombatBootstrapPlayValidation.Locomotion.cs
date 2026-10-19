using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static partial class CombatBootstrapPlayValidation
{
    private static bool LocomotionProbe => Environment.GetCommandLineArgs().Contains("-combatMigrationLocomotion");
    private static int probeLocomotionStage, probeJumpCycles;
    private static float probeLocomotionStarted, probeSprintGameStarted, probeSprintTier, probeJumpHeight;
    private static bool probeSprintRouted, probeSprintApplied, probeJumpActive, probeJumpAirborne;
    private static Vector3 probeSprintOrigin, probeJumpOrigin;
    private static string probeLocomotionError;

    private static void ResetLocomotionProbe()
    {
        Application.logMessageReceived -= OnLocomotionProbeLog;
        probeLocomotionError = null;
        probeLocomotionStage = 0;
        probeJumpCycles = 0;
        probeSprintRouted = probeSprintApplied = probeJumpActive = probeJumpAirborne = false;
        probeSprintTier = probeJumpHeight = 0;
    }

    private static void OnLocomotionProbeLog(string message, string stack, LogType type)
    {
        if (probeLocomotionError == null && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
            probeLocomotionError = message;
    }

    private static void NextLocomotionProbe(int next)
    {
        probeLocomotionStage = next;
        probeLocomotionStarted = Time.realtimeSinceStartup;
    }

    private static bool ValidateLocomotionProbe(SquadCharacterController actor)
    {
        if (probeLocomotionStage == 4) return true;
        if (probeLocomotionError != null) throw new InvalidOperationException("Runtime error during locomotion input: " + probeLocomotionError);
        var bridge = actor.GetComponent<LitOpsiveLocomotionBridge>();
        var jump = actor.GetComponent<PlayerScriptedJumpController>();
        float elapsed = Time.realtimeSinceStartup - probeLocomotionStarted;
        if (probeLocomotionStage == 0)
        {
            Require(TryFindProbeStick(actor, out probeStick), "Actual UCC shape and ground permit a short sprint-input fixture");
            Application.logMessageReceived += OnLocomotionProbeLog;
            probeSprintOrigin = actor.transform.position;
            probeSprintGameStarted = Time.time;
            InputSystem.QueueStateEvent(probeGamepad, new GamepadState { leftStick = probeStick, rightTrigger = 1 });
            NextLocomotionProbe(1);
            return false;
        }
        if (probeLocomotionStage == 1)
        {
            InputSystem.QueueStateEvent(probeGamepad, new GamepadState { leftStick = probeStick, rightTrigger = 1 });
            probeSprintRouted |= LocalInputRouter.SprintPressed;
            probeSprintApplied |= bridge.IsSprintHeld;
            var animator = actor.GetComponent<Animator>();
            if (animator != null && animator.parameters.Any(parameter => parameter.name == "LocomotionTier" && parameter.type == AnimatorControllerParameterType.Float))
                probeSprintTier = Mathf.Max(probeSprintTier, animator.GetFloat("LocomotionTier"));
            if (elapsed < .35f) return false;
            InputSystem.QueueStateEvent(probeGamepad, new GamepadState());
            float travel = Vector3.ProjectOnPlane(actor.transform.position - probeSprintOrigin, Vector3.up).magnitude;
            float rate = travel / Mathf.Max(.001f, Time.time - probeSprintGameStarted);
            Require(probeSprintRouted && probeSprintApplied && probeSprintTier >= 3 && travel > .1f && rate > probeWalkRate * 1.15f,
                "RT reaches sprint input/motor/animation and increases physical speed: routed=" + probeSprintRouted +
                " applied=" + probeSprintApplied + " tier=" + probeSprintTier + " walkRate=" + probeWalkRate + " sprintRate=" + rate + " travel=" + travel);
            NextLocomotionProbe(2);
            return false;
        }
        if (probeLocomotionStage == 2)
        {
            InputSystem.QueueStateEvent(probeGamepad, new GamepadState());
            if (elapsed < .3f) return false;
            Require(!LocalInputRouter.SprintPressed && !bridge.IsSprintHeld && actor.IsGrounded && !bridge.IsInputSuppressedByUcc,
                "RT release restores grounded free controls without a held sprint or input lock");
            probeJumpOrigin = actor.transform.position;
            probeJumpActive = probeJumpAirborne = false;
            probeJumpHeight = 0;
            InputSystem.QueueStateEvent(probeGamepad, new GamepadState().WithButton(GamepadButton.South));
            NextLocomotionProbe(3);
            return false;
        }
        if (elapsed >= .12f) InputSystem.QueueStateEvent(probeGamepad, new GamepadState());
        probeJumpActive |= jump != null && jump.IsActive;
        probeJumpAirborne |= !actor.IsGrounded;
        probeJumpHeight = Mathf.Max(probeJumpHeight, actor.transform.position.y - probeJumpOrigin.y);
        if (elapsed > 5)
            throw new TimeoutException("South input did not complete a physical jump: active=" + probeJumpActive +
                " airborne=" + probeJumpAirborne + " height=" + probeJumpHeight + " grounded=" + actor.IsGrounded +
                " suppressed=" + bridge.IsInputSuppressedByUcc + " mode=" + InputModeCoordinator.CurrentMode +
                " focus=" + InputFocusStack.HasAnyFocus() + " action=" + actor.GetComponent<PlayerActionPresentationController>().IsActionActive +
                " locks=" + DescribeProbeLocks(bridge) + " interactable=" + RuntimeOutlineSelectionManager.ActiveInteractable);
        if (!probeJumpActive || !probeJumpAirborne || probeJumpHeight <= .15f || !actor.IsGrounded || jump.IsActive ||
            bridge.IsInputSuppressedByUcc || actor.GetComponent<PlayerActionPresentationController>().IsActionActive) return false;
        Require(!bridge.IsInputSuppressedByUcc && !actor.GetComponent<PlayerActionPresentationController>().IsActionActive,
            "South produces a real jump and landing with controls restored: cycle=" + (probeJumpCycles + 1) + " height=" + probeJumpHeight);
        if (++probeJumpCycles < 2) { NextLocomotionProbe(2); return false; }
        Application.logMessageReceived -= OnLocomotionProbeLog;
        NextLocomotionProbe(4);
        return true;
    }

    private static string DescribeProbeLocks(LitOpsiveLocomotionBridge bridge)
    {
        var locks = (System.Collections.IEnumerable)typeof(LitOpsiveLocomotionBridge)
            .GetField("externalLocks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bridge);
        return string.Join(",", locks.Cast<object>().Select(handle => handle.GetType()
            .GetProperty("Owner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(handle)?.ToString()));
    }
}
