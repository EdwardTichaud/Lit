using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static partial class CombatBootstrapPlayValidation
{
    private static bool ChargeProbe => Environment.GetCommandLineArgs().Contains("-combatMigrationCharge");
    private static int probeChargeStage, probeChargeGeneration, probeChargeLaunches, probeChargeBasics, probeChargeSlowFrames;
    private static float probeChargeStarted, probeChargeStepAt, probeChargePressedAt, probeChargeLaunchedAt, probeChargeLastProgress;
    private static ChargedSkillSO probeChargeSkill;
    private static BasicSkillsSO probeChargeBasic;
    private static RealTimeCombatManager probeChargeManager;
    private static string probeChargeError;
    private static bool probeChargeFramingSeen;

    private static void ResetChargeProbe()
    {
        if (probeChargeManager != null) probeChargeManager.PlayerSkillStarted -= ObserveProbeCharge;
        Application.logMessageReceived -= ObserveProbeChargeError;
        probeChargeManager = null;
        probeChargeSkill = null;
        probeChargeBasic = null;
        probeChargeStage = probeChargeGeneration = probeChargeLaunches = probeChargeBasics = probeChargeSlowFrames = 0;
        probeChargeLastProgress = 0;
        probeChargeError = null;
        probeChargeFramingSeen = false;
    }

    private static void ObserveProbeCharge(SkillSO skill, EnemyController target)
    {
        if (skill is BasicSkillsSO) probeChargeBasics++;
        if (skill != probeChargeSkill) return;
        probeChargeLaunches++;
        probeChargeLaunchedAt = Time.realtimeSinceStartup;
    }

    private static void ObserveProbeChargeError(string message, string stack, LogType type)
    {
        if (probeChargeError == null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            probeChargeError = message;
    }

    private static void AdvanceChargeProbe(int next)
    {
        probeChargeStage = next;
        probeChargeStepAt = Time.realtimeSinceStartup;
    }

    private static void SendProbeCharge(bool held) => InputSystem.QueueStateEvent(probeGamepad,
        held ? new GamepadState().WithButton(GamepadButton.West) : new GamepadState());

    private static bool ValidateChargeProbe(RealTimeCombatManager manager, SquadCharacterController actor)
    {
        if (probeChargeStage == 7) return true;
        var action = actor.GetComponent<PlayerActionPresentationController>();
        var bridge = actor.GetComponent<LitOpsiveLocomotionBridge>();
        var animator = actor.GetComponent<Animator>();
        if (probeChargeManager == null)
        {
            probeChargeManager = manager;
            probeChargeSkill = actor.CharacterData.chargedSkill;
            Require(probeChargeSkill != null && probeChargeSkill.preparationMode == ChargedPreparationMode.BasicTail,
                "Actual production Lucian equips Basic-tail charge without a laboratory override");
            Require(animator != null && animator.parameters.Any(parameter => parameter.name == PlayerActionPresentationController.ChargeTailSpeedParameter),
                "Production Animator has the per-Basic charge playback parameter");
            probeChargeStarted = probeChargeStepAt = Time.realtimeSinceStartup;
            manager.PlayerSkillStarted += ObserveProbeCharge;
            Application.logMessageReceived += ObserveProbeChargeError;
        }
        if (probeChargeError != null) throw new InvalidOperationException("Runtime error during production charge: " + probeChargeError);
        if (Time.realtimeSinceStartup - probeChargeStarted > 40)
            throw new TimeoutException("Charge stage=" + probeChargeStage + " basics=" + probeChargeBasics + " launches=" + probeChargeLaunches +
                " slowedFrames=" + probeChargeSlowFrames + " cooldown=" + manager.GetSkillCooldownRemaining(probeChargeSkill) + " " + DescribeAction(action));
        if (manager.IsCombatActive || manager.HasLockedCombatTarget || manager.PlayerRoot != actor.transform || actor.CurrentHp <= 0)
            throw new InvalidOperationException("Targetless charge changed engagement, lock or controlled actor unexpectedly.");
        float elapsed = Time.realtimeSinceStartup - probeChargeStepAt;
        if (probeChargeStage == 0 || probeChargeStage == 3)
        {
            SendProbeCharge(false);
            if (elapsed < .35f || action.IsActionActive || !bridge.Grounded || bridge.IsInputSuppressedByUcc ||
                manager.GetSkillCooldownRemaining(probeChargeSkill) > 0) return false;
            probeChargePressedAt = Time.realtimeSinceStartup;
            probeChargeGeneration = probeChargeSlowFrames = 0;
            probeChargeFramingSeen = false;
            probeChargeLastProgress = 0;
            SendProbeCharge(true);
            AdvanceChargeProbe(probeChargeStage == 0 ? 1 : 4);
            return false;
        }
        if (probeChargeStage == 1 || probeChargeStage == 4)
        {
            if (probeChargeLaunches > 0)
            {
                Require(probeChargeStage == 4 && probeChargeLaunches == 1 && probeChargeBasics == 2 && probeChargeSlowFrames >= 2 && probeChargeFramingSeen &&
                    probeChargeLaunchedAt - probeChargePressedAt >= probeChargeSkill.chargeSeconds,
                    "Held X launches one ChargedSkill after the authored real-time threshold and slowed Basic tail");
                Require(manager.GetSkillCooldownRemaining(probeChargeSkill) > 0,
                    "Validated targetless ChargedSkill consumes its actual cooldown");
                AdvanceChargeProbe(5);
                return false;
            }
            if (action.IsBasicSkillAction)
            {
                if (probeChargeGeneration == 0)
                {
                    probeChargeGeneration = action.ActionGeneration;
                    probeChargeBasic = action.ActiveBasicForCharge;
                }
                if (!action.IsCurrentSession(probeChargeGeneration) || action.ActiveBasicForCharge != probeChargeBasic)
                    throw new InvalidOperationException("Charge replaced or restarted its actual initial Basic.");
                var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
                if (!state.IsName(probeChargeBasic.AnimatorState) || state.normalizedTime + .001f < probeChargeLastProgress)
                    throw new InvalidOperationException("Basic-tail charge changed clip or rewound its progress.");
                probeChargeLastProgress = state.normalizedTime;
                if (action.IsPreparingBasicTail && animator.GetFloat(PlayerActionPresentationController.ChargeTailSpeedParameter) <= .11f)
                {
                    probeChargeSlowFrames++;
                    probeChargeFramingSeen |= LitCameraDirector.Instance != null && LitCameraDirector.Instance.IsChargeFramingActive;
                }
                if (probeChargeStage == 1 && probeChargeSlowFrames >= 2 && probeChargeFramingSeen)
                {
                    Require(Time.realtimeSinceStartup - probeChargePressedAt < probeChargeSkill.chargeSeconds,
                        "Early-release fixture reached the actual slowed tail before the launch threshold");
                    SendProbeCharge(false);
                    AdvanceChargeProbe(2);
                }
            }
            return false;
        }
        if (probeChargeStage == 2)
        {
            SendProbeCharge(false);
            if (elapsed < .12f || action.IsActionActive) return false;
            Require(probeChargeLaunches == 0 && probeChargeBasics == 1 && ChargeProbeRestored(actor, action, bridge, animator),
                "Releasing X during the slowed tail finishes the Basic with speed, camera and controls restored; no ChargedSkill");
            AdvanceChargeProbe(3);
            return false;
        }
        if (probeChargeStage == 5)
        {
            // Keep the same press briefly after launch: it must not arm another action.
            if (elapsed < .4f) return false;
            SendProbeCharge(false);
            AdvanceChargeProbe(6);
            return false;
        }
        SendProbeCharge(false);
        if (elapsed < .15f || action.IsActionActive || !actor.IsGrounded) return false;
        Require(probeChargeLaunches == 1 && probeChargeBasics == 2 && ChargeProbeRestored(actor, action, bridge, animator),
            "Targetless charge completes once, leaves no Basic slowdown, framing, hold or UCC lock and does not engage combat");
        manager.PlayerSkillStarted -= ObserveProbeCharge;
        Application.logMessageReceived -= ObserveProbeChargeError;
        AdvanceChargeProbe(7);
        return true;
    }

    private static bool ChargeProbeRestored(SquadCharacterController actor, PlayerActionPresentationController action,
        LitOpsiveLocomotionBridge bridge, Animator animator) => actor.IsGrounded && !action.IsActionActive &&
        !action.IsPreparingBasicTail && !action.IsChargingSkill && !bridge.IsInputSuppressedByUcc && !bridge.IsExternalLockActive &&
        Mathf.Approximately(animator.GetFloat(PlayerActionPresentationController.ChargeTailSpeedParameter), 1) &&
        (LitCameraDirector.Instance == null || !LitCameraDirector.Instance.IsChargeFramingActive);
}
