using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static partial class CombatBootstrapPlayValidation
{
    private static bool DefenseProbe => Environment.GetCommandLineArgs().Contains("-combatMigrationDefenses");
    private static int probeDefenseStage, probeDefenseCycles;
    private static float probeDefenseStarted, probeDefenseStepAt, probeDodgeTravel;
    private static bool probeDefenseRunning, probeDodgeSeen, probeGuardSeen;
    private static Vector3 probeDodgeOrigin;
    private static string probeDefenseError;

    private static void ResetDefenseProbe()
    {
        Application.logMessageReceived -= OnDefenseProbeLog;
        probeDefenseRunning = probeDodgeSeen = probeGuardSeen = false;
        probeDefenseStage = probeDefenseCycles = 0;
        probeDodgeTravel = 0;
        probeDefenseError = null;
    }

    private static void OnDefenseProbeLog(string message, string stack, LogType type)
    {
        if (probeDefenseError == null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            probeDefenseError = message;
    }

    private static void NextDefenseProbe(int next)
    {
        probeDefenseStage = next;
        probeDefenseStepAt = Time.realtimeSinceStartup;
    }

    private static bool DefenseSafePressPhase(PlayerActionPresentationController action, LitOpsiveLocomotionBridge bridge)
    {
        return (encounterEnemy.AttackPhase == LitBrainsAttackPhase.Recovery || encounterEnemy.AttackPhase == LitBrainsAttackPhase.None) &&
            action.CanCancelToMobility && !bridge.IsInputSuppressedByUcc && bridge.Grounded &&
            CombatHealthThresholdController.Instance?.IsExternalCounterWindowOpen != true;
    }

    private static bool ValidateDefenseProbe(RealTimeCombatManager manager, SquadCharacterController actor)
    {
        if (probeDefenseStage == 4) return true;
        var action = actor.GetComponent<PlayerActionPresentationController>();
        var bridge = actor.GetComponent<LitOpsiveLocomotionBridge>();
        var dodge = actor.GetComponent<PlayerScriptedDodgeController>();
        var guard = CounterSkillCombatController.Instance;
        if (!probeDefenseRunning)
        {
            Require(dodge != null && guard != null && manager.IsCombatActive, "Production engagement has its real dodge and guard controllers");
            probeGamepad ??= InputSystem.AddDevice<Gamepad>();
            probeDefenseStarted = probeDefenseStepAt = Time.realtimeSinceStartup;
            probeDefenseRunning = true;
            Application.logMessageReceived += OnDefenseProbeLog;
        }
        if (probeDefenseError != null) throw new InvalidOperationException("Runtime error during defense input: " + probeDefenseError);
        if (Time.realtimeSinceStartup - probeDefenseStarted > 45)
            throw new TimeoutException("Defense stage=" + probeDefenseStage + " cycle=" + probeDefenseCycles + " dodgeSeen=" + probeDodgeSeen +
                " travel=" + probeDodgeTravel + " guardSeen=" + probeGuardSeen + " guard=" + guard.IsGuardHeld +
                " action=" + action.IsActionActive + " grounded=" + actor.IsGrounded + " locks=" + DescribeProbeLocks(bridge) + " " + DescribeEncounter());
        RequireDefenseParticipants(manager, actor, guard);
        float elapsed = Time.realtimeSinceStartup - probeDefenseStepAt;
        if (probeDefenseStage == 0)
        {
            InputSystem.QueueStateEvent(probeGamepad, new GamepadState());
            if (elapsed < .3f || !DefenseSafePressPhase(action, bridge)) return false;
            probeDodgeOrigin = actor.transform.position;
            probeDodgeSeen = probeGuardSeen = false;
            probeDodgeTravel = 0;
            InputSystem.QueueStateEvent(probeGamepad, new GamepadState().WithButton(GamepadButton.East));
            NextDefenseProbe(1);
            return false;
        }
        if (probeDefenseStage == 1)
        {
            if (elapsed >= .12f) InputSystem.QueueStateEvent(probeGamepad, new GamepadState());
            probeDodgeSeen |= dodge.IsActive;
            probeDodgeTravel = Mathf.Max(probeDodgeTravel, Vector3.ProjectOnPlane(actor.transform.position - probeDodgeOrigin, Vector3.up).magnitude);
            if (elapsed > 5) throw new TimeoutException("B did not complete a real production dodge: seen=" + probeDodgeSeen +
                " travel=" + probeDodgeTravel + " active=" + dodge.IsActive + " locks=" + DescribeProbeLocks(bridge));
            if (!probeDodgeSeen || dodge.IsActive || probeDodgeTravel <= .1f || action.IsActionActive || bridge.IsInputSuppressedByUcc || !actor.IsGrounded) return false;
            Require(true, "Fresh B starts a real dodge, moves through UCC and restores control: cycle=" + (probeDefenseCycles + 1) + " travel=" + probeDodgeTravel);
            NextDefenseProbe(2);
            return false;
        }
        if (probeDefenseStage == 2)
        {
            InputSystem.QueueStateEvent(probeGamepad, new GamepadState());
            if (!DefenseSafePressPhase(action, bridge)) return false;
            InputSystem.QueueStateEvent(probeGamepad, new GamepadState().WithButton(GamepadButton.North));
            NextDefenseProbe(3);
            return false;
        }
        probeGuardSeen |= guard.IsGuardHeld;
        if (elapsed < .25f) return false;
        InputSystem.QueueStateEvent(probeGamepad, new GamepadState());
        if (elapsed > 5) throw new TimeoutException("Y guard did not start/release: seen=" + probeGuardSeen + " held=" + guard.IsGuardHeld + " locks=" + DescribeProbeLocks(bridge));
        if (!probeGuardSeen || guard.IsGuardHeld || action.IsActionActive || bridge.IsInputSuppressedByUcc || !actor.IsGrounded) return false;
        Require(true, "Fresh Y outside the counter window enters guard, then release clears only its controls: cycle=" + (probeDefenseCycles + 1));
        if (++probeDefenseCycles < 2) { NextDefenseProbe(0); return false; }
        Application.logMessageReceived -= OnDefenseProbeLog;
        probeDefenseStage = 4;
        return true;
    }

    private static void RequireDefenseParticipants(RealTimeCombatManager manager, SquadCharacterController actor, CounterSkillCombatController guard)
    {
        if (!manager.IsCombatActive || manager.PlayerRoot != actor.transform || actor.CurrentHp <= 0 || !encounterEnemy.IsEngagedWith(actor.transform) ||
            manager.HasLockedCombatTarget || guard.IsCinematicPlaying)
            throw new InvalidOperationException("Defense participants or ordinary-guard timing changed unexpectedly: " + DescribeEncounter());
    }
}
