using System;
using System.Reflection;
using Lit.Traversal;
using UnityEditor;
using UnityEngine;

public static partial class BootstrapDistrictPlayProbe
{
    private static string grappleReturnedRun;
    private static bool GrappleReturnComplete => report != null && grappleReturnedRun == report.run;
    [MenuItem("Lit/Traversal/Test Limbes grapple (isolated save)")]
    public static void RunGrappleValidation() => Begin(false, limbes: true, limbesCycles: true, limbesExpansion: true, grapple: true);
    [InitializeOnLoadMethod]
    private static void BindGrappleFocusedScope()
    {
        EditorApplication.update += () =>
        {
            if (report != null && report.grapple && report.stage < 105)
                report.scope = "Focused presentation regression: five real cycles and grapple. Branch walking covered by the prior full run; unlock, approach, dialogue and camera scan alignment are fixtures. No manual, cold-save or multiplayer qualification.";
        };
    }
    private static void TickGrapple()
    {
        foreach (var runner in UnityEngine.Object.FindObjectsByType<Lit.Story.StorySequenceRunner>())
            if (runner.IsPlaying) typeof(Lit.Story.StorySequenceRunner).GetField("advanceRequested", Private).SetValue(runner, true);
        var root = LocalPlayerContext.LocalCharacterRoot;
        var actor = root != null ? root.GetComponent<SquadCharacterController>() : null;
        if (actor == null) return;
        var travel = actor.GetComponent<GrappleTravel>();
        if (report.stage == 500)
        {
            if (!ControlReady(out _)) return;
            report.scope = "Real isolated Bootstrap/portal session and five existing narrative cycles, then grapple round trip using UCC, scanner and interaction dispatch. Bondisseur unlock, launch positioning, camera orientation during scan and dialogue advances are explicit fixtures. Not manual/controller feel, bespoke animation parity, cold-save or multiplayer qualification.";
            var anchor = GrappleAnchor.Find("limbes.statue.aller");
            Check(anchor != null, "Five cycles enabled the grapple route, not a premature boss shortcut.");
            Check(AbilityService.Instance.Unlock(AbilityService.Instance.Resolve("bondisseur"), false), "Explicit isolated-save Bondisseur fixture unlocked through host AbilityService API.");
            Check(AbilityService.Instance.HasEquipped(actor.gameObject, AbilityService.Instance.Resolve("observateur")) &&
                anchor.HasTraversalCapability(actor), "Lucian simultaneously owns Observateur and Bondisseur in solo.");
            Check(actor.TrySetUccExternalPositionAndRotation(anchor.launch.position + Vector3.back, Quaternion.identity, true), "UCC approach fixture on departure platform, never across the gap.");
            Advance(501); return;
        }
        if (report.stage == 501 || report.stage == 504)
        {
            if (!ControlReady(out _) || EditorApplication.timeSinceStartup - report.stageStarted < 1) return;
            var anchor = GrappleAnchor.Find(report.stage == 501 ? "limbes.statue.aller" : "limbes.statue.retour");
            var camera = Camera.main;
            var rotation = camera.transform.rotation;
            camera.transform.rotation = Quaternion.LookRotation(anchor.HookPosition - camera.transform.position, camera.transform.up);
            try
            {
                var scanner = AbilityService.Instance.GetComponent<GrappleController>();
                typeof(GrappleController).GetMethod("Scan", Private).Invoke(scanner, null);
                Check(anchor.Revealed, "Production Observateur scanner revealed " + anchor.anchorId);
                bool usable = anchor.CanUse(actor);
                Check(usable, "Authored range, floor, collision sweep and grounded eligibility valid: " + anchor.anchorId + " at " + actor.transform.position + "; refusal=" + anchor.LastUseFailure);
                typeof(LocalInputRouter).GetMethod("RaiseInteract", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { default(UnityEngine.InputSystem.InputAction.CallbackContext) });
                Check(actor.GetComponent<GrappleTravel>()?.IsTravelling == true, "Production prioritized A interaction acquired UCC traversal.");
            }
            finally { camera.transform.rotation = rotation; }
            Advance(report.stage == 501 ? 502 : 505); return;
        }
        if (report.stage == 502 || report.stage == 505)
        {
            if (travel != null && travel.IsTravelling) return;
            if (EditorApplication.timeSinceStartup - report.stageStarted < 2 || !actor.IsGrounded) return;
            var anchor = GrappleAnchor.Find(report.stage == 502 ? "limbes.statue.aller" : "limbes.statue.retour");
            Check(Vector3.Distance(actor.transform.position, anchor.landing.position) < .65f, "Actual grapple landed safely: " + anchor.anchorId + " actor=" + actor.transform.position);
            Check(!actor.GetComponent<LitOpsiveLocomotionBridge>().IsScriptedTraversalActive, "Grapple UCC traversal lock released after landing without releasing the boss introduction's locks.");
            Check(anchor.Prepared && anchor.Revealed, "Discovery/preparation stored in persistent world rules.");
            if (report.stage == 502) { CaptureLiveLimbes(); Advance(130); return; }
            var state = AbilityService.Instance.Capture();
            AbilityService.Instance.Restore(state);
            Check(AbilityService.Instance.HasEquipped(actor.gameObject, AbilityService.Instance.Resolve("observateur")) && anchor.HasTraversalCapability(actor), "Multiple equipment entries survive service capture/restore.");
            grappleReturnedRun = report.run;
            Advance(132); return;
        }
        if (report.stage == 503)
        {
            foreach (var runner in UnityEngine.Object.FindObjectsByType<Lit.Story.StorySequenceRunner>())
                if (runner.IsPlaying) typeof(Lit.Story.StorySequenceRunner).GetField("advanceRequested", Private).SetValue(runner, true);
            if (Lit.Story.StorySequenceRunner.IsAnySequencePlaying || !ControlReady(out _)) return;
            Advance(504);
        }
    }
}
