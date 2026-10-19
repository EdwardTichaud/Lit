using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static partial class CombatBootstrapPlayValidation
{
    private static bool GamepadProbe
    {
        get
        {
            var arguments = Environment.GetCommandLineArgs();
            bool enabled = arguments.Contains("-combatMigrationGamepad");
            if (!enabled && arguments.Contains("-combatMigrationLocomotion"))
                throw new ArgumentException("The locomotion probe requires the virtual gamepad recipe.");
            if (!enabled && arguments.Contains("-combatMigrationCharge"))
                throw new ArgumentException("The charge probe requires the virtual gamepad recipe.");
            if (arguments.Contains("-combatMigrationDefenses") && !arguments.Contains("-combatMigrationEncounter"))
                throw new ArgumentException("The defense probe requires the actual production encounter recipe.");
            return enabled;
        }
    }
    private static Gamepad probeGamepad;
    private static RealTimeCombatManager probeInputManager;
    private static int probeInputStage, probeInputGeneration, probeBasicStarts;
    private static bool probeInputBasicReported;
    private static float probeInputStarted, probeRawPeak, probeWorldPeak, probeTravel, probeDirectionAlignment, probeMoveGameStarted, probeWalkRate;
    private static Vector2 probeStick;
    private static Vector3 probeMoveOrigin, probeExpectedDirection;

    private static void ResetGamepadProbeCycle()
    {
        if (probeInputManager != null) probeInputManager.PlayerSkillStarted -= OnProbeSkillStarted;
        probeInputManager = null;
        probeInputStage = probeBasicStarts = 0;
        probeInputBasicReported = false;
        probeRawPeak = probeWorldPeak = probeTravel = 0;
        probeDirectionAlignment = 1;
        ResetLocomotionProbe();
        ResetChargeProbe();
    }

    private static void CleanupGamepadProbe()
    {
        ResetGamepadProbeCycle();
        if (probeGamepad != null && probeGamepad.added) InputSystem.RemoveDevice(probeGamepad);
        probeGamepad = null;
    }

    private static void OnProbeSkillStarted(SkillSO skill, EnemyController target)
    {
        if (skill is BasicSkillsSO) probeBasicStarts++;
    }

    private static void NextInputProbe(int value)
    {
        probeInputStage = value;
        probeInputStarted = Time.realtimeSinceStartup;
    }

    private static bool ValidateGamepadProbe(RealTimeCombatManager manager, SquadCharacterController actor)
    {
        var bridge = actor.GetComponent<LitOpsiveLocomotionBridge>();
        var action = actor.GetComponent<PlayerActionPresentationController>();
        float elapsed = Time.realtimeSinceStartup - probeInputStarted;
        if (probeInputStage == 5) return true;
        if (probeInputStage == 0)
        {
            Require(TryFindProbeStick(actor, out probeStick), "Physical ground and capsule clearance permit a short camera-relative stick pulse");
            probeGamepad ??= InputSystem.AddDevice<Gamepad>();
            probeInputManager = manager;
            manager.PlayerSkillStarted += OnProbeSkillStarted;
            probeMoveOrigin = actor.transform.position;
            probeMoveGameStarted = Time.time;
            InputSystem.QueueStateEvent(probeGamepad, new GamepadState { leftStick = probeStick });
            NextInputProbe(1);
            return false;
        }
        if (probeInputStage == 1)
        {
            InputSystem.QueueStateEvent(probeGamepad, new GamepadState { leftStick = probeStick });
            probeRawPeak = Mathf.Max(probeRawPeak, LocalInputRouter.RawMoveValue.magnitude);
            probeWorldPeak = Mathf.Max(probeWorldPeak, bridge.CurrentWorldMoveInput.magnitude);
            if (bridge.CurrentWorldMoveInput.magnitude > .15f)
                probeDirectionAlignment = Mathf.Min(probeDirectionAlignment,
                    Vector3.Dot(new Vector3(bridge.CurrentWorldMoveInput.x, 0, bridge.CurrentWorldMoveInput.y).normalized, probeExpectedDirection));
            probeTravel = Mathf.Max(probeTravel, Vector3.ProjectOnPlane(actor.transform.position - probeMoveOrigin, Vector3.up).magnitude);
            if (elapsed < .35f) return false;
            InputSystem.QueueStateEvent(probeGamepad, new GamepadState());
            float travelAlignment = Vector3.Dot(Vector3.ProjectOnPlane(actor.transform.position - probeMoveOrigin, Vector3.up).normalized, probeExpectedDirection);
            probeWalkRate = probeTravel / Mathf.Max(.001f, Time.time - probeMoveGameStarted);
            Require(probeRawPeak > .15f && probeWorldPeak > .15f && probeTravel > .1f && probeDirectionAlignment > .95f && travelAlignment > .85f,
                "Virtual stick reaches input/router/UCC and physically moves Lucian: raw=" + probeRawPeak + " world=" + probeWorldPeak +
                " travel=" + probeTravel + " inputAlignment=" + probeDirectionAlignment + " travelAlignment=" + travelAlignment +
                " expected=" + probeExpectedDirection + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            NextInputProbe(2);
            return false;
        }
        if (probeInputStage == 2)
        {
            InputSystem.QueueStateEvent(probeGamepad, new GamepadState());
            if (elapsed < .25f) return false;
            Require(LocalInputRouter.RawMoveValue.sqrMagnitude < .01f && !bridge.IsInputSuppressedByUcc,
                "Stick release clears routed movement without suppressing UCC");
            probeInputGeneration = action.ActionGeneration;
            InputSystem.QueueStateEvent(probeGamepad, new GamepadState().WithButton(GamepadButton.West));
            NextInputProbe(3);
            return false;
        }
        if (probeInputStage == 3)
        {
            if (elapsed < .12f) return false;
            InputSystem.QueueStateEvent(probeGamepad, new GamepadState());
            Require(probeBasicStarts == 1 && action.IsBasicSkillAction && action.ActionGeneration != probeInputGeneration,
                "One fresh X input starts exactly one production Basic: starts=" + probeBasicStarts + " action=" + action.IsBasicSkillAction +
                " mode=" + InputModeCoordinator.CurrentMode + " context=" + GamepadInputContextStack.Current);
            probeInputGeneration = action.ActionGeneration;
            NextInputProbe(4);
            return false;
        }
        if (action.IsCurrentSession(probeInputGeneration))
        {
            if (elapsed > 8) throw new TimeoutException("Gamepad Basic retained its action: " + DescribeAction(action));
            return false;
        }
        if (!probeInputBasicReported)
        {
            Require(probeBasicStarts == 1 && !action.IsActionActive && !action.IsPreparingBasicTail,
                "Gamepad Basic releases its action with no duplicate callback, repeat or charged preparation");
            probeInputBasicReported = true;
        }
        if (LocomotionProbe && !ValidateLocomotionProbe(actor)) return false;
        if (ChargeProbe && !ValidateChargeProbe(manager, actor)) return false;
        manager.PlayerSkillStarted -= OnProbeSkillStarted;
        probeInputManager = null;
        NextInputProbe(5);
        return true;
    }

    private static bool TryFindProbeStick(SquadCharacterController actor, out Vector2 stick)
    {
        var camera = Camera.main;
        var motor = actor.GetComponent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>();
        var capsule = motor != null ? motor.Colliders.Take(motor.ColliderCount).OfType<CapsuleCollider>()
            .SingleOrDefault(candidate => candidate.enabled && !candidate.isTrigger) : null;
        if (camera == null || capsule == null || capsule.direction != 1 || motor.GroundedRaycastHit.collider == null)
        { stick = default; return false; }
        var scale = capsule.transform.lossyScale;
        float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float half = Mathf.Max(radius, capsule.height * Mathf.Abs(scale.y) * .5f);
        Vector3 center = capsule.transform.TransformPoint(capsule.center);
        Vector3 top = center + capsule.transform.up * (half - radius);
        Vector3 bottom = center - capsule.transform.up * (half - radius);
        float groundY = motor.GroundedRaycastHit.point.y;
        Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
        string refusals = "";
        foreach (var input in new[] { Vector2.up, Vector2.right, Vector2.left, Vector2.down,
            new Vector2(1, 1).normalized, new Vector2(-1, 1).normalized,
            new Vector2(1, -1).normalized, new Vector2(-1, -1).normalized })
        {
            Vector3 direction = (forward * input.y + right * input.x).normalized;
            Vector3 position = actor.transform.position;
            var blockers = Physics.CapsuleCastAll(top, bottom, Mathf.Max(.01f, radius - motor.ColliderSpacing),
                direction, 2, motor.ColliderLayerMask, QueryTriggerInteraction.Ignore)
                .Where(hit => !hit.transform.IsChildOf(actor.transform) && hit.normal.y < .7f).ToArray();
            if (blockers.Length > 0) { refusals += input + " blocked=" + string.Join(",", blockers.Select(hit => hit.collider.name)); continue; }
            bool floor = true;
            for (int index = 1; index <= 4; index++)
            {
                Vector3 expected = position + direction * (index * .5f);
                expected.y = groundY + .5f;
                floor &= Physics.RaycastAll(expected, Vector3.down, 1, motor.ColliderLayerMask, QueryTriggerInteraction.Ignore)
                    .Any(hit => !hit.transform.IsChildOf(actor.transform) && hit.normal.y > .7f && Mathf.Abs(hit.point.y - groundY) < .35f);
            }
            if (!floor) { refusals += input + " floor absent at actual ground=" + groundY + "; "; continue; }
            stick = input * .65f;
            probeExpectedDirection = direction;
            return true;
        }
        Debug.LogWarning("[CombatGamepadProbe] No safe movement fixture | root=" + actor.transform.position +
            " capsule=" + capsule.name + " radius=" + radius + " ground=" + motor.GroundedRaycastHit.point + " | " + refusals);
        stick = default;
        return false;
    }
}
