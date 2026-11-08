using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static partial class BootstrapDistrictPlayProbe
{
    private static bool MoonriseWestRouteRequested => Environment.GetCommandLineArgs().Contains("-moonriseWestRoute");
    private static TwoLeverPuzzle moonriseWestPuzzle;
    private static Lever moonriseWestLever;
    private static Vector3 moonriseWestGoal;
    private static float moonriseWestPressAt, moonriseWestFirstAt;
    private static PortalController moonriseWestPortal;

    private static void BeginMoonriseWestRoute(SquadCharacterController actor)
    {
        moonriseWestPuzzle = Object.FindObjectsByType<TwoLeverPuzzle>().Single(p => p.gameObject.scene.name == "District_1_Enigme_Levers");
        Check(Object.FindObjectsByType<SquadCharacterController>().Count(a => a.isActiveAndEnabled) == 1,
            "This route really has one active squad actor; no companion is placed on a lever.");
        moonriseWestLever = moonriseWestPuzzle.leverA;
        moonriseWestGoal = FindMoonriseWestApproach(actor, moonriseWestLever.transform.position);
        moonriseWestPressAt = -1;
        File.WriteAllText(Path.Combine(report.run, "west-route.txt"),
            "A=" + moonriseWestPuzzle.leverA.transform.position + " mode=" + moonriseWestPuzzle.leverA.activationMode + " duration=" + moonriseWestPuzzle.leverA.activeDuration +
            "\nB=" + moonriseWestPuzzle.leverB.transform.position + " mode=" + moonriseWestPuzzle.leverB.activationMode + " duration=" + moonriseWestPuzzle.leverB.activeDuration);
        Advance(291);
    }

    private static void TickMoonriseWestRoute()
    {
        if (report.errors.Count != 0) throw new InvalidOperationException(report.errors[0]);
        if (report.stage == 295)
        {
            if (!ZoneReady("Assets/Scenes/District_2/Courts/ZoneManifest_District_2_Courts.asset") || !ControlReady(out var arrived)) return;
            Check(true, "The actual onward portal loads Stillhaven with navigation and player control ready.");
            Check(Vector3.Distance(arrived.transform.position, new Vector3(1199, .1f, 0)) < 1.5f,
                "Relocating the Moonrise entrance preserves the first authored Stillhaven arrival point in world space.");
            RecordMoonriseRouteCheckpoint("stillhaven_arrived");
            if (MoonriseFullRouteRequested)
            {
                Finish(true, "One fresh Moonrise route completed: Luc/Jon, current production combat/counter/KO/death, victory/exploration, torch/ladder, disk save/menu resume retaining Luc and the dead enemy, landing walk/return, shared levers and Stillhaven. See timing CSV; human 15-20 minute discovery pacing, feel and multiplayer remain unqualified.");
                return;
            }
            Finish(true, "Post-victory save resumed, ladder descended, both levers reached and activated with real solo gamepad input, pivot opened and onward portal used. Full combat replay and measured 15-20 minute pacing remain unqualified.");
            return;
        }
        if (!ZoneReady("Assets/Scenes/Maison/ZoneManifest_District_1.asset") || !ControlReady(out var actor)) return;
        if (report.stage == 291 || report.stage == 292)
        {
            File.AppendAllText(Path.Combine(report.run, "west-route.txt"), Time.frameCount % 60 == 0 ?
                $"\nframe={Time.frameCount} stage={report.stage} actor={actor.transform.position} A={moonriseWestPuzzle.leverA.IsActive} B={moonriseWestPuzzle.leverB.IsActive}" : "");
            if (!moonriseWestLever.IsActive)
            {
                if (moonriseWestLever.UsesSharedInteractionSelection
                    ? ReferenceEquals(RuntimeOutlineSelectionManager.ActiveInteractable, moonriseWestLever)
                    : (bool)typeof(Lever).GetMethod("IsLocalCharacterInRange", Private).Invoke(moonriseWestLever, null))
                {
                    if (moonriseWestPressAt < 0)
                    {
                        if (moonriseWestLever.UsesSharedInteractionSelection)
                        {
                            var prompt = moonriseWestLever.GetComponent<WorldInteractionPrompt>();
                            Check(prompt != null, "A shared-prompt component is authored on the selected lever.");
                            if (!prompt.IsVisible) { InputSystem.QueueStateEvent(moonrisePad, new GamepadState()); return; }
                            Check(prompt.label == "Actionner", "The selected lever displays Actionner through the common world UI before gamepad A.");
                            ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_" + moonriseWestLever.name + "_Selected.png"));
                        }
                        moonriseWestPressAt = Time.realtimeSinceStartup;
                    }
                    if (Time.realtimeSinceStartup - moonriseWestPressAt > 4)
                        throw new InvalidOperationException("Fresh A did not activate " + moonriseWestLever.name + " from its actual interaction range.");
                    var input = new GamepadState();
                    if ((Time.realtimeSinceStartup - moonriseWestPressAt) % .6f < .12f) input = input.WithButton(GamepadButton.South);
                    InputSystem.QueueStateEvent(moonrisePad, input);
                }
                else WalkMoonriseWestPath(actor);
                return;
            }
            InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
            if (report.stage == 291)
            {
                moonriseWestFirstAt = Time.realtimeSinceStartup;
                Check(true, "Real gamepad A activates the first authored lever after walking from the ladder.");
                moonriseWestLever = moonriseWestPuzzle.leverB;
                moonriseWestGoal = FindMoonriseWestApproach(actor, moonriseWestLever.transform.position);
                moonriseWestPressAt = -1;
                Advance(292); return;
            }
            File.AppendAllText(Path.Combine(report.run, "west-route.txt"), "\nsecond activated after=" + (Time.realtimeSinceStartup - moonriseWestFirstAt) + " firstStillActive=" + moonriseWestPuzzle.leverA.IsActive);
            Check(moonriseWestPuzzle.leverA.IsActive && moonriseWestPuzzle.IsTriggered,
                "Both authored levers overlap in solo and trigger the actual puzzle; elapsed=" + (Time.realtimeSinceStartup - moonriseWestFirstAt));
            var firstPrompt = moonriseWestPuzzle.leverA.GetComponent<WorldInteractionPrompt>();
            if (firstPrompt != null) Check(!firstPrompt.IsVisible, "The first lever's prompt is hidden after walking away and selecting the second lever.");
            Check(moonriseWestPuzzle.pivotTarget != null, "The puzzle has its actual pivot binding.");
            RecordMoonriseRouteCheckpoint("west_puzzle_completed");
            moonriseWestFirstAt = Time.realtimeSinceStartup;
            Advance(293); return;
        }
        if (report.stage == 293)
        {
            InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
            if (Time.realtimeSinceStartup - moonriseWestFirstAt < 4) return;
            moonriseWestPortal = Object.FindObjectsByType<PortalController>().Single(p => p.IsSceneTransition &&
                (ZoneManifest)typeof(PortalController).GetField("destinationZone", Private).GetValue(p) ==
                AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/District_2/Courts/ZoneManifest_District_2_Courts.asset"));
            moonriseWestGoal = FindMoonriseWestApproach(actor, moonriseWestPortal.GetInteractionAnchor().position);
            var arrivals = moonriseWestPortal.DestinationPoints;
            for (int i = 0; i < 4; i++)
                Check(Vector3.Distance(arrivals[i].position, new Vector3(1199 + (i % 2) * 2, .1f, (i / 2) * 2)) < .01f,
                    "Relocated entrance preserves authored world arrival " + i + ": " + arrivals[i].position);
            moonriseWestPressAt = -1;
            moonriseWestFirstAt = Time.realtimeSinceStartup;
            Advance(294); return;
        }
        if (report.stage == 294)
        {
            if (GameFlowService.Instance.IsTransitioning) { InputSystem.QueueStateEvent(moonrisePad, new GamepadState()); Advance(295); return; }
            if (Time.realtimeSinceStartup - moonriseWestFirstAt > 30)
            {
                File.WriteAllText(Path.Combine(report.run, "west-portal-physics.txt"), "actor=" + actor.transform.position + " goal=" + moonriseWestGoal);
                throw new TimeoutException("Physical west-portal approach did not complete; actor=" + actor.transform.position + " goal=" + moonriseWestGoal);
            }
            if (!ReferenceEquals(RuntimeOutlineSelectionManager.ActiveInteractable, moonriseWestPortal)) { WalkMoonriseWestPath(actor); return; }
            var portalPrompt = moonriseWestPortal.GetComponent<WorldInteractionPrompt>();
            Check(portalPrompt != null, "The Stillhaven entrance has an authored shared prompt.");
            if (!portalPrompt.IsVisible) { InputSystem.QueueStateEvent(moonrisePad, new GamepadState()); return; }
            Check(portalPrompt.label == "Rejoindre Stillhaven", "The real selected portal displays its destination through the common world UI before A.");
            Check(true, "Walking along the authored corridor after solving the puzzle selects the real onward portal, without an approach teleport.");
            ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_WestPortalSelected.png"));
            InputSystem.QueueStateEvent(moonrisePad, new GamepadState().WithButton(GamepadButton.South));
            Advance(295);
        }
    }

    private static Vector3 FindMoonriseWestApproach(SquadCharacterController actor, Vector3 anchor)
    {
        if (!NavMesh.SamplePosition(actor.transform.position, out var from, 1, NavMesh.AllAreas)) throw new InvalidOperationException("No floor for west route start.");
        float best = float.PositiveInfinity;
        Vector3 result = default;
        var path = new NavMeshPath();
        for (int angle = 0; angle < 360; angle += 30)
        {
            Vector3 candidate = anchor + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * .8f;
            candidate.y = actor.transform.position.y;
            if (!NavMesh.SamplePosition(candidate, out var hit, 1, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - candidate.y) > .7f ||
                !NavMesh.CalculatePath(from.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            float length = 0;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            if (length >= best) continue;
            best = length; result = hit.position;
        }
        Check(!float.IsPositiveInfinity(best), "A complete runtime path exists to west-route point " + anchor);
        File.AppendAllText(Path.Combine(report.run, "west-route.txt"), "\nroute from=" + from.position + " goal=" + result + " length=" + best);
        return result;
    }

    private static void WalkMoonriseWestPath(SquadCharacterController actor)
    {
        var path = new NavMeshPath();
        if (!NavMesh.SamplePosition(actor.transform.position, out var from, 1, NavMesh.AllAreas) ||
            !NavMesh.CalculatePath(from.position, moonriseWestGoal, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            throw new InvalidOperationException("West route became unreachable at " + actor.transform.position);
        Vector3 target = path.corners.Skip(1).FirstOrDefault(c => Vector3.ProjectOnPlane(c - actor.transform.position, Vector3.up).magnitude > .25f);
        if (target == default) target = moonriseWestGoal;
        WalkMoonriseLanding(actor, target);
    }

}
