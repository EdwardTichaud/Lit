using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

public static partial class CombatBootstrapPlayValidation
{
    private static int encounterLifecycleStage, lifecycleImpactCount, lifecycleHealth;
    private static float lifecycleStarted, lifecycleMovingAt;
    private static bool lifecycleSawReturn, lifecycleControlsRestored, lifecycleFacingProven;

    private static T EnemyField<T>(string name) => (T)typeof(LitBrainsEnemy)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(encounterEnemy);

    private static void ResetEncounterLifecycle()
    {
        encounterLifecycleStage = 0;
        lifecycleSawReturn = lifecycleControlsRestored = lifecycleFacingProven = false;
        lifecycleMovingAt = -1;
    }

    private static void BeginEncounterEscape(SquadCharacterController player, int nextStage)
    {
        Require(player.TrySetUccExternalPositionAndRotation(encounterOrigin, encounterRotation, true),
            "Escape fixture moves only Lucian outside the authored pursuit radius through UCC");
        Require(!encounterEnemy.IsWithinPursuitBounds(player.transform), "Escape is outside the actual authored pursuit zone");
        lifecycleImpactCount = encounterImpacts.Count;
        lifecycleHealth = player.CurrentHp;
        lifecycleSawReturn = lifecycleControlsRestored = lifecycleFacingProven = false;
        lifecycleMovingAt = -1;
        encounterLifecycleStage = nextStage;
        lifecycleStarted = Time.realtimeSinceStartup;
    }

    private static bool EscapeControlsRestored(RealTimeCombatManager manager, SquadCharacterController player)
    {
        var world = manager.GetComponent<CombatWorldRuntime>();
        var locomotion = player.GetComponent<LitOpsiveLocomotionBridge>();
        var interaction = player.GetComponent<LitUccInteractionBridge>();
        return !manager.IsCombatActive && !manager.HasLockedCombatTarget && world != null && world.CountThreats(player) == 0 &&
            InputModeCoordinator.CurrentMode == InputMode.Exploration && !InputFocusStack.HasAnyFocus() &&
            locomotion != null && !locomotion.IsInputSuppressedByUcc &&
            interaction != null && interaction.CanEvaluateLitInteractions && player.IsGrounded;
    }

    private static bool ValidateEncounterLifecycle(RealTimeCombatManager manager, SquadCharacterController player)
    {
        if (Time.realtimeSinceStartup - lifecycleStarted > 30)
            throw new TimeoutException("Encounter lifecycle stage=" + encounterLifecycleStage + " return=" +
                EnemyField<bool>("productionReturning") + " controls=" + EscapeControlsRestored(manager, player) + " " +
                DescribeEncounter() + " " + DescribeEncounterMotion());
        if (manager.HasLockedCombatTarget) throw new InvalidOperationException("Lifecycle created an unintended camera lock.");
        if (encounterLifecycleStage == 2)
        {
            if (!manager.IsCombatActive || !encounterEnemy.IsEngagedWith(player.transform) ||
                encounterImpacts.Count <= lifecycleImpactCount || player.CurrentHp >= lifecycleHealth) return false;
            Require(!EnemyField<bool>("productionReturning"), "Proximity during return resumes the actual enemy engagement");
            Require(true, "Reengagement produces a new unique attack and real damage without a player hit or camera lock");
            BeginEncounterEscape(player, 3);
            return false;
        }

        bool returning = EnemyField<bool>("productionReturning");
        lifecycleSawReturn |= returning;
        if (!lifecycleControlsRestored && EscapeControlsRestored(manager, player))
        {
            lifecycleControlsRestored = true;
            Require(true, "Last threat removed: exploration mode, no camera lock, UCC input and interaction evaluation restored");
        }
        Vector3 homeDelta = Vector3.ProjectOnPlane(EnemyField<Vector3>("productionHome") - encounterEnemy.Root.position, Vector3.up);
        if (returning && encounterEnemy.ActualSpeed > .1f && homeDelta.magnitude > .5f)
        {
            if (lifecycleMovingAt < 0) lifecycleMovingAt = Time.time;
            if (Time.time - lifecycleMovingAt > .25f)
            {
                float facing = Vector3.Dot(encounterEnemy.Root.forward, homeDelta.normalized);
                if (facing <= .7f) throw new InvalidOperationException("Returning enemy faces away from its home; dot=" + facing);
                if (!lifecycleFacingProven)
                {
                    Require(true, "Returning enemy faces its home while moving; dot=" + facing);
                    lifecycleFacingProven = true;
                }
                if (encounterLifecycleStage == 1 && lifecycleControlsRestored)
                {
                    Require(TryFindEncounterPosition(encounterEnemy, out var near,
                        new[] { 4f, 3f, 5f }, new[] { 180f, 135f, -135f, 90f, -90f, 0f }),
                        "A nearby clear return-reengagement fixture exists in the current navigation world");
                    Require(player.TrySetUccExternalPositionAndRotation(near, encounterRotation, true),
                        "Lucian approaches the returning enemy through UCC without forcing its perception");
                    Physics.SyncTransforms();
                    Require(encounterEnemy.RuntimeCharacterData.vision.TryEvaluate(encounterEnemy.Root, player.transform,
                        encounterEnemy.RuntimeCharacterData.enemyCombatProfile.returnReengageDistance, 360,
                        out float distance, out _, out string reason),
                        "Return fixture satisfies actual authored proximity perception: distance=" + distance + " reason=" + reason);
                    lifecycleHealth = player.CurrentHp;
                    encounterLifecycleStage = 2;
                    lifecycleStarted = Time.realtimeSinceStartup;
                }
            }
        }
        if (encounterLifecycleStage == 3 && lifecycleSawReturn && lifecycleControlsRestored &&
            !returning && homeDelta.magnitude < .3f && encounterEnemy.AttackPhase == LitBrainsAttackPhase.None)
        {
            Require(encounterEnemy.NavigationReady && !encounterEnemy.IsDead, "Living enemy returned to its original home with validated navigation");
            Require(Quaternion.Angle(encounterEnemy.Root.rotation, EnemyField<Quaternion>("productionHomeRotation")) < 2,
                "Completed return restores the authored home orientation");
            Require(player.CurrentHp == lifecycleHealth && encounterImpacts.Count == lifecycleImpactCount,
                "No late impact damages the escaped player or restarts an attack after disengagement");
            CleanupEncounterProbe();
            encounterDone = true;
            return true;
        }
        return false;
    }

    private static string DescribeEncounterMotion()
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        object[] arguments = { encounterEnemy.Root.position, default(RaycastHit) };
        bool grounded = (bool)typeof(LitBrainsEnemy).GetMethod("TryProductionGround", fields).Invoke(encounterEnemy, arguments);
        var ground = (RaycastHit)arguments[1];
        var agent = encounterEnemy.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        bool local = NavMesh.SamplePosition(encounterEnemy.Root.position, out var localHit, .15f, filter);
        bool nearby = NavMesh.SamplePosition(encounterEnemy.Root.position, out var nearbyHit, 1.5f, filter);
        return "motion=" + EnemyField<bool>("productionMotionActive") + " airborne=" + EnemyField<bool>("productionAirborne") +
            " endRequested=" + EnemyField<bool>("productionEndRequested") + " abort=" + EnemyField<bool>("productionAbort") +
            " verticalSpeed=" + EnemyField<float>("productionVerticalSpeed") + " airTime=" + EnemyField<float>("productionAirTime") +
            " ground=" + (grounded ? ground.collider.name + " scene=" + ground.collider.gameObject.scene.name +
                " point=" + ground.point + " normal=" + ground.normal : "absent") +
            " navLocal=" + (local ? localHit.position.ToString() : "absent") +
            " navNearby=" + (nearby ? nearbyHit.position.ToString() : "absent") + " baseOffset=" + agent.baseOffset;
    }
}
