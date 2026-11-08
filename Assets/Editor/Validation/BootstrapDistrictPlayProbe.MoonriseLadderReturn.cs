using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static partial class BootstrapDistrictPlayProbe
{
    private static bool MoonriseLadderResumeRequested => Environment.GetCommandLineArgs().Contains("-moonriseLadderResume") || MoonriseWestRouteRequested;
    private static Vector3 moonriseLandingWalkGoal;
    private static float moonriseLandingStageAt;
    private static int moonriseLandingTorch;

    private static void PrepareMoonriseLadderResume()
    {
        if (!report.moonriseCombat || !MoonriseLadderResumeRequested) return;
        // This is a frozen, successful probe's own snapshot, never a user slot.
        string source = Path.GetFullPath(Root + "/PlayRuns/41d77ac8ab9a4c6caa0ec61900bf6161");
        var previous = JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(source, "result.json")));
        if (!previous.passed || !previous.restored) throw new InvalidOperationException("The seed validation run did not finish safely.");
        string from = Path.Combine(source, "Saves"), to = Path.Combine(report.run, "Saves");
        foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target, false);
        }
        report.scope = "Focused real Bootstrap/menu resume of an isolated post-victory validation snapshot at the upper ladder landing. Real gamepad torch, walking away, reapproach and fresh-A descent. No actor positioning, combat replay, full route pacing or multiplayer claim.";
        if (MoonriseWestRouteRequested) report.scope += " Then real solo walking/A to the two authored levers, pivot passage and onward portal; no puzzle activation injection.";
        Check(true, "Copied only the owned successful run's save into a new isolated save root; original snapshot remains unchanged.");
    }

    private static void TickMoonriseLadderReturn()
    {
        if (!ZoneReady("Assets/Scenes/Maison/ZoneManifest_District_1.asset") || !ControlReady(out var actor)) return;
        if (report.errors.Count != 0) throw new InvalidOperationException(report.errors[0]);
        if (Time.realtimeSinceStartup - moonriseLandingStageAt > 25 && report.stage != 285)
            throw new TimeoutException("Ladder return stage=" + report.stage + " actor=" + actor.transform.position + " goal=" + moonriseLandingWalkGoal + " detected=" + moonriseLadder?.CanBeDetectedBy(actor));
        switch (report.stage)
        {
            case 285:
                moonriseLadder = Object.FindObjectsByType<LadderController>().OrderBy(l => Vector3.Distance(l.leafs2.position, actor.transform.position)).First();
                Check(Vector3.Distance(actor.transform.position, moonriseLadder.leafs2.position) < .7f, "Resume really starts on the saved upper landing, not an approach fixture.");
                var defeated = Object.FindObjectsByType<SceneMarker>(FindObjectsInactive.Include).Single(m => m.MarkerId == "GlobalObjectId_V1-2-6f707f864cc6ecd40a182bedabf7a0ee-900000000000000100-0");
                Check(defeated.RuntimeInstance != null && defeated.RuntimeInstance.GetComponent<CharacterInfo>().IsDead, "The saved production Juggernaut is still dead before the return traversal.");
                if (moonrisePad == null || !moonrisePad.added) moonrisePad = InputSystem.AddDevice<Gamepad>("Moonrise ladder return validation");
                moonriseLandingTorch = actor.FlameSecondsRemaining;
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState().WithButton(GamepadButton.West));
                NextMoonriseLandingStage(286); return;
            case 286:
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                if (!actor.IsFlameEquipped || Time.realtimeSinceStartup - moonriseLandingStageAt < 1) return;
                moonriseLandingWalkGoal = FindMoonriseLandingExit(actor);
                Check(true, "A clear physical floor exists outside the upper endpoint activation sphere: " + moonriseLandingWalkGoal);
                ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_UpperLanding.png"));
                NextMoonriseLandingStage(287); return;
            case 287:
                if (Vector3.ProjectOnPlane(actor.transform.position - moonriseLandingWalkGoal, Vector3.up).magnitude > .25f)
                { WalkMoonriseLanding(actor, moonriseLandingWalkGoal); return; }
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                Check(Vector3.Distance(actor.transform.position, moonriseLadder.leafs2.position) > moonriseLadder.activationRadius + .2f,
                    "Real stick movement leaves the upper landing interaction radius, without teleporting or clearing its exit block.");
                File.WriteAllText(Path.Combine(report.run, "ladder-upper-walk.txt"), "actor=" + actor.transform.position + " endpoint=" + moonriseLadder.leafs2.position);
                NextMoonriseLandingStage(288); return;
            case 288:
                WriteMoonriseLandingEligibility(actor);
                Vector3 endpoint = moonriseLadder.leafs2.position;
                if (Vector3.ProjectOnPlane(actor.transform.position - endpoint, Vector3.up).magnitude > .45f)
                { WalkMoonriseLanding(actor, endpoint); return; }
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                if (!moonriseLadder.CanBeDetectedBy(actor) || !ReferenceEquals(RuntimeOutlineSelectionManager.ActiveInteractable, moonriseLadder)) return;
                Check(true, "The real detector/outline reacquires the ladder from upstairs after walking away and back.");
                moonriseLadderDestination = moonriseLadder.leafs1.TransformPoint(moonriseLadder.destinationLocalOffset);
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState().WithButton(GamepadButton.South));
                NextMoonriseLandingStage(289); return;
            case 289:
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                if (Vector3.Distance(actor.transform.position, moonriseLadderDestination) > .7f) return;
                Check(actor.GetComponent<LitUccInteractionBridge>().CanEvaluateLitInteractions && !actor.GetComponent<LitOpsiveLocomotionBridge>().IsInputSuppressedByUcc,
                    "Fresh gamepad A descends to the lower endpoint and releases locomotion and interaction locks.");
                Check(actor.IsFlameEquipped && actor.FlameSecondsRemaining < moonriseLandingTorch, "The torch stays equipped and consumes fuel throughout the return route.");
                ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_LadderReturn.png"));
                RecordMoonriseRouteCheckpoint("ladder_down_after_resume");
                if (MoonriseWestRouteRequested || MoonriseFullRouteRequested) { BeginMoonriseWestRoute(actor); return; }
                Finish(true, "Post-victory isolated snapshot resumed, upper landing physically exited and reapproached, actual detector selected the ladder and fresh gamepad A descended. Full continuous route remains unqualified.");
                return;
        }
    }

    private static void NextMoonriseLandingStage(int stage)
    {
        moonriseLandingStageAt = Time.realtimeSinceStartup;
        Advance(stage);
    }

    private static void WriteMoonriseLandingEligibility(SquadCharacterController actor)
    {
        var bridge = actor.GetComponent<LitOpsiveLocomotionBridge>();
        var combat = RealTimeCombatManager.Instance;
        string diagnostic = "actor=" + actor.transform.position + " ladder=" + moonriseLadder.name +
            " active=" + moonriseLadder.isActiveAndEnabled + " canDetect=" + moonriseLadder.CanBeDetectedBy(actor) +
            " inputSuppressed=" + actor.IsMovementInputSuppressed + " flight=" + actor.IsFlightActive +
            " driving=" + bridge.IsDriving + " uccSuppressed=" + bridge.IsInputSuppressedByUcc + " grounded=" + bridge.Grounded +
            " jump=" + actor.GetComponent<PlayerScriptedJumpController>()?.IsActive +
            " combat=" + combat?.IsCombatActive + " action=" + combat?.IsPlayerActionActive + " cinematic=" + combat?.IsCinematicSequenceActive +
            " awaiting=" + typeof(LadderController).GetField("awaitingServerResponse", Private).GetValue(moonriseLadder) +
            " passages=" + ((System.Collections.IDictionary)typeof(LadderController).GetField("activePassages", Private).GetValue(moonriseLadder)).Count +
            " radius=" + moonriseLadder.activationRadius + " lower=" + moonriseLadder.leafs1.position + " upper=" + moonriseLadder.leafs2.position +
            " selected=" + (RuntimeOutlineSelectionManager.ActiveInteractable as Component)?.name;
        File.WriteAllText(Path.Combine(report.run, "ladder-return-eligibility.txt"), diagnostic);
    }

    private static Vector3 FindMoonriseLandingExit(SquadCharacterController actor)
    {
        Vector3 from = actor.transform.position;
        string diagnostics = "start=" + from + " upper=" + moonriseLadder.leafs2.position;
        for (int angle = 0; angle < 360; angle += 30)
        {
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            Vector3 candidate = from + direction * (moonriseLadder.activationRadius + 1f);
            var floor = Physics.RaycastAll(candidate + Vector3.up, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.normal.y > .8f && !h.transform.IsChildOf(actor.transform)).OrderBy(h => h.distance).FirstOrDefault();
            diagnostics += "\nangle=" + angle + " floor=" + floor.collider?.name + " at=" + floor.point;
            if (floor.collider == null || Mathf.Abs(floor.point.y - from.y) > .6f) continue;
            if (Physics.SphereCastAll(from + Vector3.up * .9f, .3f, direction, Vector3.Distance(from, candidate), ~0, QueryTriggerInteraction.Ignore)
                .Any(h => !h.transform.IsChildOf(actor.transform))) continue;
            File.WriteAllText(Path.Combine(report.run, "ladder-upper-floor.txt"), diagnostics);
            return floor.point;
        }
        File.WriteAllText(Path.Combine(report.run, "ladder-upper-floor.txt"), diagnostics);
        throw new InvalidOperationException("No clear walkable departure found around the authored upper ladder endpoint. See ladder-upper-floor.txt.");
    }

    private static void WalkMoonriseLanding(SquadCharacterController actor, Vector3 target)
    {
        var camera = Camera.main;
        if (camera == null) throw new InvalidOperationException("Missing gameplay camera during upper landing walk.");
        Vector3 direction = Vector3.ProjectOnPlane(target - actor.transform.position, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        InputSystem.QueueStateEvent(moonrisePad, new GamepadState { leftStick = new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward)) * .5f });
    }
}
