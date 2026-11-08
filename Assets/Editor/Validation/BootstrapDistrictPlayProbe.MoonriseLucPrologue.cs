using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static partial class BootstrapDistrictPlayProbe
{
    private static bool MoonriseLucRouteRequested => Environment.GetCommandLineArgs().Contains("-moonriseLucRoute") ||
        Environment.GetCommandLineArgs().Contains("-moonriseFullRoute");
    private static CycleDefinition moonriseLucDefinition;
    private static ICharacterDetectedInteractable moonriseLucTarget;
    private static float moonriseLucPressAt, moonriseLucReadAt;
    private static float moonriseLucTargetAt, moonriseLucDiagnosticAt;
    private static bool moonriseLucGraveNonBlocking;
    private static int moonriseGraveCaptureMask;
    private static readonly float[] moonriseGraveCaptureDelays = { .3f, .8f, 1.3f };
    private static float moonriseLucToastResumeAt;

    private static void BeginMoonriseLucPrologue(SquadCharacterController actor)
    {
        moonriseLucDefinition = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Scenes/Cycles/Cycle_Luc/LucCycle.asset");
        Check(!CycleProgressionService.Instance.IsStepCompleted(moonriseLucDefinition, "jon_collar_collected") &&
            !CycleProgressionService.Instance.IsStepCompleted(moonriseLucDefinition, "jon_grave_read") && !actor.HasItemById("jon_collar"),
            "Fresh Moonrise has neither Jon clue nor collar before any prologue action.");
        if (Environment.GetCommandLineArgs().Contains("-moonriseLucRouteOnly"))
            report.scope = "Focused fresh Bootstrap/Maison/Moonrise session, real walking and paid Ancient Flame, Jon collar A pickup, actual nonblocking grave reading and Luc dialogue/completion. No injected knowledge, inventory, dialogue or actor pose in Moonrise. Does not replay combat/continuity or measure 15-20 minute pacing.";
        SetMoonriseLucTarget(actor, Object.FindObjectsByType<InteractableItem>().Single(i => i.representedItem != null && i.representedItem.itemId == "jon_collar"), 300);
    }

    private static void SetMoonriseLucTarget(SquadCharacterController actor, ICharacterDetectedInteractable target, int stage)
    {
        moonriseLucTarget = target;
        moonriseWestGoal = FindMoonriseWestApproach(actor, target.GetInteractionAnchor().position);
        moonriseLucPressAt = -1;
        moonriseLucTargetAt = Time.realtimeSinceStartup;
        moonriseLucDiagnosticAt = -1;
        Advance(stage);
    }

    private static void TickMoonriseLucPrologue()
    {
        var actor = LocalPlayerContext.LocalCharacterRoot?.GetComponent<SquadCharacterController>();
        if (actor == null) return;
        if (report.errors.Count != 0) throw new InvalidOperationException(report.errors[0]);
        var progress = CycleProgressionService.Instance;
        var inventory = Object.FindAnyObjectByType<InventoryPanelController>();
        switch (report.stage)
        {
            case 300:
                if (!actor.HasItemById("jon_collar") || !progress.IsStepCompleted(moonriseLucDefinition, "jon_collar_collected"))
                { DriveMoonriseLucInteraction(actor); return; }
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                Check(actor.CountItemById("jon_collar") == 1, "Walking/A collected one real Jon collar and unlocked its knowledge step through production inventory.");
                SetMoonriseLucTarget(actor, Object.FindObjectsByType<InteractableItem>().Single(i => i.representedItem != null && i.representedItem.itemId == "jon_grave_marker"), 301);
                return;
            case 301:
                bool epitaphVisible = DialoguePanelUI.Instance?.IsShowing == true && DialoguePanelUI.Instance.dialogueText != null &&
                    DialoguePanelUI.Instance.dialogueText.text.Contains("repose Jon");
                if (!MoonriseGravePanelOpen(inventory) && !epitaphVisible) { DriveMoonriseLucInteraction(actor); return; }
                moonriseLucGraveNonBlocking = epitaphVisible;
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                Check(true, "Walking/A on the actual Jon grave displays its authored epitaph through the production reading UI, without a direct panel call.");
                if (epitaphVisible) Check(InputModeCoordinator.CurrentMode == InputMode.Exploration && !actor.IsMovementInputSuppressed,
                    "The authored grave uses nonblocking reading and keeps exploration available while its text is visible.");
                ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_JonGraveRead.png"));
                moonriseLucReadAt = Time.realtimeSinceStartup;
                moonriseGraveCaptureMask = 0;
                moonriseLucToastResumeAt = -1;
                Advance(302); return;
            case 302:
                CaptureMoonriseGraveReading();
                if (moonriseLucGraveNonBlocking && DialoguePanelUI.Instance?.IsShowing == true)
                {
                    InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                    return;
                }
                if (MoonriseGravePanelOpen(inventory))
                {
                    var input = new GamepadState();
                    float since = Time.realtimeSinceStartup - moonriseLucReadAt;
                    if (since > 1.5f && (since - 1.5f) % .6f < .12f) input = input.WithButton(GamepadButton.East);
                    InputSystem.QueueStateEvent(moonrisePad, input);
                    return;
                }
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                if (!ControlReady(out _)) return;
                if (Environment.GetCommandLineArgs().Contains("-moonriseReadabilityProbe"))
                {
                    if (moonriseLucToastResumeAt < 0) moonriseLucToastResumeAt = Time.realtimeSinceStartup;
                    if (Time.realtimeSinceStartup - moonriseLucToastResumeAt < .3f) return;
                    var toast = InfoBoxUI.Instance;
                    Check(toast != null && toast.infoText != null && toast.infoText.text.Contains("Collier de Jon") &&
                        toast.infoBoxRoot.GetComponent<CanvasGroup>().alpha > .8f,
                        "The real pickup toast resumes after the epitaph, with its message and visible duration preserved.");
                    ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_ToastAfterGrave.png"));
                }
                Check(progress.IsStepCompleted(moonriseLucDefinition, "jon_grave_read"), "Actual grave reading unlocks its knowledge step and its reading presentation finishes with exploration available.");
                SetMoonriseLucTarget(actor, Object.FindObjectsByType<GhostController>().Single(g => g.name == "Ghost_Luc"), 303);
                return;
            case 303:
                if (DialoguePanelUI.Instance == null || !DialoguePanelUI.Instance.IsShowing) { DriveMoonriseLucInteraction(actor); return; }
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                Check(DialoguePanelUI.Instance.dialogueText != null && DialoguePanelUI.Instance.dialogueText.text.Contains("Mon Jon"),
                    "Fresh A on the revealed Luc starts his eligible authored dialogue in the real dialogue UI.");
                Check(progress.IsStepCompleted(moonriseLucDefinition, "luc_spoken"), "Luc interaction is recorded by the authoritative named-step progression.");
                ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_LucDialogue.png"));
                Advance(304); return;
            case 304:
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                if (!progress.IsCompleted(moonriseLucDefinition) || DialoguePanelUI.Instance?.IsShowing == true || !ControlReady(out _)) return;
                if (Environment.GetCommandLineArgs().Contains("-moonriseLucUnloadProbe") && !ObserveMoonriseLucUnload(actor)) return;
                var reward = AssetDatabase.LoadAssetAtPath<KnowledgeSO>("Assets/Gameplay/Knowledges/Knowledge_JonLocation.asset");
                Check(KnowledgeManager.Instance.HasKnowledge(reward) && actor.CountItemById("jon_collar") == 1,
                    "Luc's completed dialogue grants its authored JonLocation reward and keeps the collected collar exactly once.");
                Check(!actor.GetComponent<LitOpsiveLocomotionBridge>().IsInputSuppressedByUcc && actor.GetComponent<LitUccInteractionBridge>().CanEvaluateLitInteractions,
                    "Luc farewell releases controls and world interaction before leaving the room.");
                RecordMoonriseRouteCheckpoint("luc_prologue_completed");
                if (Environment.GetCommandLineArgs().Contains("-moonriseLucRouteOnly"))
                    Finish(true, "Fresh Moonrise paid Ancient Flame, real collar pickup, nonblocking grave reading, Luc dialogue, named steps/reward and exploration restored. Combat replay and full timed route remain unqualified.");
                else BeginMoonriseRoomDoorApproach(actor);
                return;
        }
    }

    private static bool MoonriseGravePanelOpen(InventoryPanelController inventory) => inventory != null &&
        (bool)typeof(InventoryPanelController).GetField("readablePanelOpen", Private).GetValue(inventory);

    private static float moonriseLucCompletedObservedAt = -1;
    private static bool ObserveMoonriseLucUnload(SquadCharacterController actor)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("District_1_Cycle_Luc");
        if (moonriseLucCompletedObservedAt < 0)
        {
            moonriseLucCompletedObservedAt = Time.realtimeSinceStartup;
            Check(scene.isLoaded, "Luc's scene remains loaded when the final dialogue completes, before its safety margin.");
        }
        float elapsed = Time.realtimeSinceStartup - moonriseLucCompletedObservedAt;
        string diagnostic = $"elapsed={elapsed} sceneLoaded={scene.isLoaded} skip={CycleProgressionService.ShouldSkipScene(scene.name)} flowTransition={GameFlowService.Instance?.IsTransitioning} activeScene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}";
        foreach (var controller in Object.FindObjectsByType<CycleController>(FindObjectsInactive.Include).Where(c => c.definition == moonriseLucDefinition))
            diagnostic += $"\ncontroller={controller.name} active={controller.isActiveAndEnabled} spawned={controller.IsSpawned} completed={typeof(CycleController).GetProperty("Completed", Private).GetValue(controller)} ready={typeof(CycleController).GetMethod("CompletionPresentationReady", Private).Invoke(controller, null)} marginAt={typeof(CycleController).GetField("completionMarginStartedAt", Private).GetValue(controller)} unloadStarted={typeof(CycleController).GetField("unloadStarted", Private).GetValue(controller)}";
        File.WriteAllText(Path.Combine(report.run, "luc-unload.txt"), diagnostic);
        if (scene.isLoaded)
        {
            if (elapsed > 20) throw new TimeoutException("Luc scene did not unload after its presentation and margin. " + diagnostic);
            return false;
        }
        Check(elapsed >= moonriseLucDefinition.completionUnloadDelay - .5f, "Luc's additive scene unloads only after its configured five-second presentation margin.");
        Check(ControlReady(out _) && actor.CountItemById("jon_collar") == 1, "Luc scene unload preserves the controlled player and the collected collar.");
        return true;
    }

    private static void CaptureMoonriseGraveReading()
    {
        float elapsed = Time.realtimeSinceStartup - moonriseLucReadAt;
        for (int i = 0; i < moonriseGraveCaptureDelays.Length; i++)
        {
            if ((moonriseGraveCaptureMask & (1 << i)) != 0 || elapsed < moonriseGraveCaptureDelays[i]) continue;
            moonriseGraveCaptureMask |= 1 << i;
            string suffix = Mathf.RoundToInt(moonriseGraveCaptureDelays[i] * 1000).ToString();
            ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_GraveRead_" + suffix + "ms.png"));
            string diagnostic = "elapsed=" + elapsed + " dialogueShowing=" + DialoguePanelUI.Instance?.IsShowing +
                " text=" + DialoguePanelUI.Instance?.dialogueText?.text;
            foreach (var text in Object.FindObjectsByType<TMPro.TMP_Text>().Where(t => t.isActiveAndEnabled &&
                         (t.text.Contains("repose Jon") || t.text.Contains("Objet récupéré") || t.text.Contains("Connaissance acquise"))))
            {
                var canvas = text.GetComponentInParent<Canvas>();
                diagnostic += "\nui=" + text.name + " text=" + text.text + " canvas=" + canvas?.name + " order=" + canvas?.sortingOrder +
                    " groups=" + string.Join(",", text.GetComponentsInParent<CanvasGroup>().Select(g => g.name + ":" + g.alpha));
            }
            File.WriteAllText(Path.Combine(report.run, "grave-ui-" + suffix + "ms.txt"), diagnostic);
            if (Environment.GetCommandLineArgs().Contains("-moonriseReadabilityProbe"))
                Check(DialoguePanelUI.Instance?.IsShowing == true && InfoBoxUI.Instance != null &&
                    InfoBoxUI.Instance.infoBoxRoot.GetComponent<CanvasGroup>().alpha < .01f,
                    "The reading text has priority over the suspended pickup toast at " + suffix + "ms.");
        }
    }

    private static void DriveMoonriseLucInteraction(SquadCharacterController actor)
    {
        if (Time.realtimeSinceStartup >= moonriseLucDiagnosticAt)
        {
            moonriseLucDiagnosticAt = Time.realtimeSinceStartup + 2;
            WriteMoonriseLucDiagnostic(actor);
        }
        if (Time.realtimeSinceStartup - moonriseLucTargetAt > 20)
            throw new TimeoutException("Prologue target not selected/used; see luc-target-" + report.stage + ".txt. actor=" + actor.transform.position);
        if (!ReferenceEquals(RuntimeOutlineSelectionManager.ActiveInteractable, moonriseLucTarget))
        {
            WalkMoonriseWestPath(actor);
            return;
        }
        if (moonriseLucPressAt < 0)
        {
            Check(true, "The real local detector selected prologue target " + (moonriseLucTarget as Component)?.name + " before A.");
            moonriseLucPressAt = Time.realtimeSinceStartup;
        }
        if (Time.realtimeSinceStartup - moonriseLucPressAt > 6) throw new TimeoutException("Fresh A did not complete the selected prologue interaction: " + (moonriseLucTarget as Component)?.name);
        var input = new GamepadState();
        if ((Time.realtimeSinceStartup - moonriseLucPressAt) % .6f < .12f) input = input.WithButton(GamepadButton.South);
        InputSystem.QueueStateEvent(moonrisePad, input);
    }

    private static void WriteMoonriseLucDiagnostic(SquadCharacterController actor)
    {
        var component = moonriseLucTarget as Component;
        string diagnostic = "actor=" + actor.transform.position + " forward=" + actor.transform.forward + " goal=" + moonriseWestGoal +
            " selected=" + (RuntimeOutlineSelectionManager.ActiveInteractable as Component)?.name +
            " collarCount=" + actor.CountItemById("jon_collar") + " collarStep=" + CycleProgressionService.Instance.IsStepCompleted(moonriseLucDefinition, "jon_collar_collected");
        if (component != null)
        {
            var collider = moonriseLucTarget.GetInteractionDetectionCollider();
            var anchor = moonriseLucTarget.GetInteractionAnchor();
            Vector3 origin = actor.GetInteractionOriginWorldPosition();
            Vector3 point = CharacterInteractionDetection.GetInteractionRangePoint(collider, anchor, origin);
            diagnostic += "\ntarget=" + component.name + " eligible=" + moonriseLucTarget.CanBeDetectedBy(actor) +
                " lit=" + CharacterInteractionDetection.IsInActiveFlameInfluence(moonriseLucTarget) +
                " collider=" + collider?.name + " bounds=" + collider?.bounds +
                " range=" + Vector3.Distance(origin, point) + " max=" + moonriseLucTarget.GetInteractionMaxDistance(actor) +
                " visible=" + CharacterInteractionDetection.IsInteractionTargetVisibleFromCharacter(moonriseLucTarget, collider, anchor, origin, actor.transform);
            Vector3 delta = anchor.position - origin;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                diagnostic += "\nray=" + hit.collider.gameObject.scene.name + "/" + hit.collider.name + " point=" + hit.point;
        }
        File.WriteAllText(Path.Combine(report.run, "luc-target-" + report.stage + ".txt"), diagnostic);
        ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_LucTarget_" + report.stage + ".png"));
    }
}
