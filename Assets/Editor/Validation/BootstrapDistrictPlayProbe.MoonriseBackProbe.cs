using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static partial class BootstrapDistrictPlayProbe
{
    private static float moonriseBackStarted;
    private static Vector3 moonriseBackOrigin, moonriseBackFacing, moonriseBackRetreat;
    private static Vector3[] moonriseBackArc;
    private static int moonriseBackWaypoint;
    private static bool moonriseBehindSeen;

    private static void TickMoonriseBackProbe(SquadCharacterController actor)
    {
        if (moonriseBackStarted <= 0)
        {
            moonriseBackStarted = Time.realtimeSinceStartup;
            moonriseBackOrigin = moonriseEnemy.Root.position;
            moonriseBackFacing = Vector3.ProjectOnPlane(moonriseEnemy.Root.forward, Vector3.up).normalized;
            moonriseBehindSeen = false;
            moonriseBackWaypoint = 0;
            Vector3 radial = Vector3.ProjectOnPlane(actor.transform.position - moonriseBackOrigin, Vector3.up).normalized;
            moonriseBackRetreat = moonriseBackOrigin - moonriseBackFacing * 3.1f;
            moonriseBackArc = null;
            foreach (float sign in new[] { 1f, -1f })
            {
                var arc = new[] { 60f, 120f, 180f }.Select(a => moonriseBackOrigin + Quaternion.AngleAxis(sign * a, Vector3.up) * radial * 1.15f).ToArray();
                bool clear = true;
                for (int i = 0; i < arc.Length; i++)
                {
                    if (!NavMesh.SamplePosition(arc[i], out var floor, .4f, NavMesh.AllAreas)) { clear = false; break; }
                    arc[i] = floor.position;
                    if (Physics.OverlapCapsule(floor.position + Vector3.up * .4f, floor.position + Vector3.up * 1.5f, .25f, ~0, QueryTriggerInteraction.Ignore)
                        .Any(c => !c.transform.IsChildOf(actor.transform))) { clear = false; break; }
                }
                if (clear) { moonriseBackArc = arc; break; }
            }
            Check(moonriseBackArc != null && moonriseEnemy.IsCounterStunned,
                "A clear physical walking arc exists around the real knocked-out enemy, without changing his pose or stun.");
            var mode = Camera.main.GetComponent<LitGameplayCameraModeController>();
            var mask = typeof(LitGameplayCameraModeController).GetField("visibilityMask", Private).GetValue(mode);
            Check(!RealTimeCombatManager.Instance.HasLockedCombatTarget && mode.MaskActive &&
                (Transform)typeof(LitTacticalVisibilityMask).GetField("enemyTransform", Private).GetValue(mask) == moonriseEnemy.Root &&
                (Transform)typeof(LitGameplayCameraModeController).GetField("clipEnemy", Private).GetValue(mode) == moonriseEnemy.Root &&
                (bool)typeof(LitGameplayCameraModeController).GetField("hasClipEnemyBody", Private).GetValue(mode),
                "Without manual lock, the production tactical mask and near clip protect the live engaged enemy body after the counter.");
            report.scope = "Focused real Moonrise enemy acquisition/attacks and counter, then gamepad walking behind the victim during genuine Knocked Out and retreat/recovery pursuit. No actor/enemy positioning, rotation, damage or extended stun fixture. Not death/save/full-route or 15-20 minute pacing proof.";
        }
        float elapsed = Time.realtimeSinceStartup - moonriseBackStarted;
        if (elapsed > 12) throw new TimeoutException("Rear pursuit probe timed out; " + MoonriseDiagnostic(actor, RealTimeCombatManager.Instance));
        if (moonriseEnemy.IsFlameDormant || !RealTimeCombatManager.Instance.IsCombatActive)
            throw new InvalidOperationException("Rear pursuit test left the valid lit encounter; no target-memory conclusion can be drawn.");

        Vector3 separation = Vector3.ProjectOnPlane(actor.transform.position - moonriseEnemy.Root.position, Vector3.up);
        float rearDot = Vector3.Dot(Vector3.ProjectOnPlane(moonriseEnemy.Root.forward, Vector3.up).normalized, separation.normalized);
        bool initialVision = moonriseEnemy.RuntimeCharacterData.TryEvaluateVision(moonriseEnemy.Root, actor.transform, out _, out float angle, out string reason);
        File.AppendAllText(Path.Combine(report.run, "rear-pursuit.txt"),
            $"time={elapsed:F3} actor={actor.transform.position} enemy={moonriseEnemy.Root.position} waypoint={moonriseBackWaypoint} KO={moonriseEnemy.IsCounterStunned} rearDot={rearDot:F3} initialVision={initialVision} angle={angle:F1} reason={reason} engaged={moonriseEnemy.IsEngagedWith(actor.transform)}\n");
        if (!moonriseBehindSeen && rearDot < -.6f && !initialVision)
        {
            Check(moonriseEnemy.IsCounterStunned && moonriseEnemy.IsEngagedWith(actor.transform),
                "Actual stick movement reaches the enemy's rear outside acquisition vision while genuine KO preserves the same known target.");
            moonriseBehindSeen = true;
            ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_PlayerBehindEnemy.png"));
        }
        if (moonriseEnemy.IsCounterStunned)
        {
            if (moonriseEnemy.AttackPhase != LitBrainsAttackPhase.None || moonriseImpacts.Count != moonriseKnockoutImpacts)
                throw new InvalidOperationException("Enemy attacked or impacted during the genuine Knocked Out rear approach.");
        }
        if (!moonriseEnemy.IsEngagedWith(actor.transform))
            throw new InvalidOperationException("The known player target was lost during the rear approach or KO recovery.");
        if (!moonriseBehindSeen)
        {
            if (!moonriseEnemy.IsCounterStunned) throw new InvalidOperationException("The input driver did not reach the rear before the real stun expired; this is not evidence of a detection defect.");
            if (moonriseBackWaypoint < moonriseBackArc.Length - 1 &&
                Vector3.ProjectOnPlane(actor.transform.position - moonriseBackArc[moonriseBackWaypoint], Vector3.up).magnitude < .2f)
                moonriseBackWaypoint++;
            QueueMoonriseBackWalk(actor, moonriseBackArc[moonriseBackWaypoint]);
            return;
        }
        // Evaluate resumed pursuit before asking the player to retreat farther:
        // a pursuing enemy can keep closing the gap indefinitely.
        if (!moonriseEnemy.IsCounterStunned &&
            Vector3.ProjectOnPlane(moonriseEnemy.Root.position - moonriseBackOrigin, Vector3.up).magnitude >= .25f &&
            Vector3.Dot(Vector3.ProjectOnPlane(moonriseEnemy.Root.forward, Vector3.up).normalized, separation.normalized) > .5f)
        {
            InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
            Check(true, "No enemy attack or impact occurred during Knocked Out, and the same known player target was retained throughout the rear approach and recovery.");
            Check(true, "After recovery the actual NavMesh enemy turns and moves towards the player who passed behind him.");
            ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_RearPursuitRecovered.png"));
            Finish(true, "Real production counter/KO followed by stick-driven rear approach outside acquisition vision, known-target retention and natural post-stun turn/pursuit verified. No death/save/full-route or human pacing claim.");
            return;
        }
        if (separation.magnitude < 2.85f)
        {
            QueueMoonriseBackWalk(actor, moonriseBackRetreat);
            return;
        }
        InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
    }

    private static void QueueMoonriseBackWalk(SquadCharacterController actor, Vector3 goal)
    {
        var camera = Camera.main;
        if (camera == null) throw new InvalidOperationException("Missing gameplay camera during rear approach.");
        Vector3 direction = Vector3.ProjectOnPlane(goal - actor.transform.position, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        InputSystem.QueueStateEvent(moonrisePad, new GamepadState { leftStick = new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward)) * .95f });
    }
}
