using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static partial class BootstrapDistrictPlayProbe
{
    private static LadderController moonriseLadder;
    private static int moonriseSavedHealth, moonriseSavedTorch;
    private static float moonriseTorchStarted;
    private static Vector3 moonriseSavedPosition, moonriseLadderDestination;
    private static string moonriseSavedMarker, moonriseSavedFlame, moonriseSavedActor, moonriseActorEntity;

    private static void BeginMoonriseContinuity(SquadCharacterController actor)
    {
        moonriseLadder = Object.FindObjectsByType<LadderController>().Where(ladder => ladder.leafs1 != null && ladder.leafs2 != null)
            .OrderBy(ladder => Vector3.SqrMagnitude(ladder.leafs1.position - moonriseEnemyOrigin)).First();
        moonriseSavedMarker = moonriseEnemy.GetComponentInParent<SceneMarker>().MarkerId;
        moonriseSavedFlame = moonriseFlame.FlameId;
        moonriseSavedActor = actor.CharacterData.UniqueId;
        moonriseActorEntity = actor.GetEntityId().ToString();
        if (!moonriseCorridorFlame.IsEffectivelyLit)
        {
            moonriseWalkingFlame = moonriseCorridorFlame;
            moonriseTorchBefore = actor.FlameSecondsRemaining;
            BeginMoonriseWalkingApproach(actor, moonriseCorridorFlame.GetInteractionAnchor().position, 279);
            return;
        }
        BeginMoonriseWalkingApproach(actor, moonriseLadder.leafs1.position, 276);
    }

    private static void EquipMoonriseExplorationTorch()
    {
        moonriseTorchStarted = Time.realtimeSinceStartup;
        InputSystem.QueueStateEvent(moonrisePad, new GamepadState().WithButton(GamepadButton.West));
        Advance(267);
    }

    private static void TickMoonriseContinuity()
    {
        var flow = GameFlowService.Instance;
        if (report.stage == 271)
        {
            if (flow == null || flow.IsTransitioning || flow.HasGameplaySession) return;
            Check(LocalPlayerContext.LocalCharacterRoot == null, "Post-victory save recipe returned to the real menu and released the old actor.");
            Check(GameFlowService.StartOrLoadGame(), "Resume uses the active slot's saved destination, without a scene override.");
            Advance(272); return;
        }
        var actor = LocalPlayerContext.LocalCharacterRoot?.GetComponent<SquadCharacterController>();
        if (actor == null) return;
        if (report.errors.Count != 0) throw new InvalidOperationException("Runtime error during post-victory continuity: " + report.errors[0]);
        float elapsed = Time.realtimeSinceStartup - moonriseTorchStarted;
        switch (report.stage)
        {
            case 274:
                if (InputModeCoordinator.CurrentMode == InputMode.UserInterface)
                {
                    var panel = RealTimeCombatSceneUiController.Instance != null
                        ? typeof(RealTimeCombatSceneUiController).GetField("victoryPanel", Private).GetValue(RealTimeCombatSceneUiController.Instance) as CanvasGroup : null;
                    CheckVictoryUi(panel);
                    var submit = new GamepadState();
                    if (elapsed > .2f && (elapsed - .2f) % .7f < .12f) submit = submit.WithButton(GamepadButton.South);
                    InputSystem.QueueStateEvent(moonrisePad, submit);
                    return;
                }
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                if (!ControlReady(out _)) return;
                var manager = RealTimeCombatManager.Instance;
                Check(actor.GetComponent<LitUccInteractionBridge>().CanEvaluateLitInteractions &&
                    !actor.GetComponent<LitOpsiveLocomotionBridge>().IsInputSuppressedByUcc && !manager.IsCombatActive && !manager.HasLockedCombatTarget,
                    "Gamepad A closes victory UI and restores exploration, interactions, UCC input and camera target state.");
                RecordMoonriseRouteCheckpoint("victory_ui_closed");
                BeginMoonriseContinuity(actor); return;
            case 267:
                if (elapsed > .1f) InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                if (!actor.IsFlameEquipped || elapsed < 1) return;
                Check(true, "Fresh exploration X equips the real torch after victory; no stale attack or combat lock consumes it.");
                moonriseSavedTorch = actor.FlameSecondsRemaining;
                moonriseTorchStarted = Time.realtimeSinceStartup;
                Advance(268); return;
            case 268:
                if (elapsed < 2) return;
                Check(actor.IsFlameEquipped && actor.FlameSecondsRemaining < moonriseSavedTorch,
                    "Equipped torch consumes its actual reserve again after the combat.");
                if (!ControlReady(out _)) return;
                Check(moonriseLadder.CanBeDetectedBy(actor), "The authored ladder accepts the living grounded actor after combat.");
                Check(CharacterInteractionDetection.IsInActiveFlameInfluence(moonriseLadder),
                    "The carried torch satisfies the shared interaction light gate for this ladder.");
                WriteMoonriseLadderSelection(actor);
                Check(ReferenceEquals(RuntimeOutlineSelectionManager.ActiveInteractable, moonriseLadder),
                    "The actual player detector/outline selects the nearby ladder before the fresh A press; selected=" +
                    (RuntimeOutlineSelectionManager.ActiveInteractable as Component)?.name);
                moonriseLadderDestination = moonriseLadder.leafs2.TransformPoint(moonriseLadder.destinationLocalOffset);
                moonriseTorchStarted = Time.realtimeSinceStartup;
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState().WithButton(GamepadButton.South));
                Advance(269); return;
            case 269:
                if (elapsed > .12f) InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                if (elapsed > 15) throw new TimeoutException("A did not traverse the authored ladder after victory; position=" + actor.transform.position +
                    " expected=" + moonriseLadderDestination + " grounded=" + actor.IsGrounded + " mode=" + InputModeCoordinator.CurrentMode +
                    " detected=" + moonriseLadder.CanBeDetectedBy(actor));
                if (Vector3.ProjectOnPlane(actor.transform.position - moonriseLadderDestination, Vector3.up).magnitude > .35f ||
                    Mathf.Abs(actor.transform.position.y - moonriseLadderDestination.y) > .6f || !ControlReady(out _)) return;
                Check(!actor.GetComponent<LitOpsiveLocomotionBridge>().IsInputSuppressedByUcc &&
                    actor.GetComponent<LitUccInteractionBridge>().CanEvaluateLitInteractions,
                    "Fresh gamepad A selects the actual ladder, traverses to the other endpoint and releases motion/input locks.");
                RecordMoonriseRouteCheckpoint("ladder_up");
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState().WithButton(GamepadButton.West));
                moonriseTorchStarted = Time.realtimeSinceStartup;
                Advance(270); return;
            case 270:
                if (elapsed > .1f) InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                if (actor.IsFlameEquipped || elapsed < 1) return;
                Check(true, "A second fresh X puts away the torch after ladder traversal.");
                moonriseSavedHealth = actor.CurrentHp;
                moonriseSavedTorch = actor.FlameSecondsRemaining;
                moonriseSavedPosition = actor.transform.position;
                Check(WorldSaveAdapter.Instance != null && CharacterStateStore.Instance != null, "World and character persistence services remain available after victory and ladder traversal.");
                WorldSaveAdapter.Instance.SaveWorldSnapshot();
                CharacterStateStore.Instance.Save();
                Advance(273); return;
            case 273:
                var store = CharacterStateStore.Instance;
                if (store.IsSaving) return;
                Check(store.LastSaveSucceeded, "Real post-victory character/world save finished successfully.");
                Check(GameFlowService.OpenMainMenu(), "Normal menu return accepted after the post-victory save.");
                Advance(271); return;
            case 272:
                if (!ZoneReady("Assets/Scenes/Maison/ZoneManifest_District_1.asset") || !ControlReady(out _)) return;
                Check(actor.CharacterData.UniqueId == moonriseSavedActor && actor.GetEntityId().ToString() != moonriseActorEntity,
                    "Resume created a new actor with the same persistent identity.");
                Check(actor.CurrentHp == moonriseSavedHealth && actor.FlameSecondsRemaining == moonriseSavedTorch && !actor.IsFlameEquipped,
                    "Resume preserves actual post-combat health and remaining torch points/equip state.");
                Check(Vector3.Distance(actor.transform.position, moonriseSavedPosition) < .4f,
                    "Resume preserves the actual post-ladder position, instead of the new-game spawn.");
                var marker = Object.FindObjectsByType<SceneMarker>(FindObjectsInactive.Include).Single(candidate => candidate.MarkerId == moonriseSavedMarker);
                Check(marker.RuntimeInstance != null && marker.RuntimeInstance.GetComponent<CharacterInfo>().IsDead && !marker.RuntimeInstance.activeInHierarchy,
                    "The same defeated marker remains dead/inactive after disk save and menu reload.");
                Check(Object.FindObjectsByType<Flame>().Single(flame => flame.FlameId == moonriseSavedFlame).IsEffectivelyLit,
                    "The paid encounter Flame remains lit after reload.");
                Check(report.errors.Count == 0, "No runtime Error/Exception/Assert during the Moonrise encounter and continuity recipe.");
                if (MoonriseFullRouteRequested)
                {
                    Check(CycleProgressionService.Instance.IsCompleted(moonriseLucDefinition) && actor.CountItemById("jon_collar") == 1 &&
                        KnowledgeManager.Instance.HasKnowledge(moonriseLucDefinition.FindStep("luc_farewell").rewards[0].knowledge),
                        "Disk save/menu resume preserves Luc's completed named steps, reward and the unique collar in the same fresh route.");
                    Check(!UnityEngine.SceneManagement.SceneManager.GetSceneByName("District_1_Cycle_Luc").isLoaded,
                        "Resume skips the completed Luc cycle scene using its terminal named step.");
                    RecordMoonriseRouteCheckpoint("post_victory_save_resumed");
                    NextMoonriseLandingStage(285);
                    return;
                }
                Finish(true, "Actual Moonrise Flame, pursuit/impacts, gamepad counter, Knocked Out, fresh-X victory, death/disappearance, torch, ladder and disk save/menu resume verified. Continuous 15-20 minute traversal, manual feel and multiplayer remain unqualified.");
                return;
        }
    }

    private static void CheckVictoryUi(CanvasGroup panel)
    {
        if (panel == null || !panel.gameObject.activeInHierarchy || panel.alpha <= .001f)
            throw new InvalidOperationException("The UI focus after this victory does not belong to the visible production victory panel.");
    }

    private static void WriteMoonriseLadderSelection(SquadCharacterController actor)
    {
        Vector3 origin = actor.GetInteractionOriginWorldPosition();
        var collider = moonriseLadder.GetInteractionDetectionCollider();
        var anchor = moonriseLadder.GetInteractionAnchor();
        Vector3 point = CharacterInteractionDetection.GetInteractionRangePoint(collider, anchor, origin);
        Vector3 selection = CharacterInteractionSelection.SelectionPoint(collider, anchor, point);
        Vector3 flat = Vector3.ProjectOnPlane(selection - origin, Vector3.up).normalized;
        string diagnostic = "actor=" + actor.transform.position + " origin=" + origin + " forward=" + actor.transform.forward +
            " anchor=" + anchor.position + " range=" + Vector3.Distance(point, origin) + " max=" + moonriseLadder.GetInteractionMaxDistance(actor) +
            " dot=" + Vector3.Dot(flat, actor.transform.forward) + " visible=" +
            CharacterInteractionDetection.IsInteractionTargetVisibleFromCharacter(moonriseLadder, collider, anchor, origin, actor.transform) +
            " selected=" + (RuntimeOutlineSelectionManager.ActiveInteractable as Component)?.name;
        foreach (var hit in Physics.RaycastAll(origin, (anchor.position - origin).normalized,
                     Vector3.Distance(origin, anchor.position), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance))
            diagnostic += "\nrayHit=" + hit.collider.name + " scene=" + hit.collider.gameObject.scene.name + " point=" + hit.point;
        System.IO.File.WriteAllText(System.IO.Path.Combine(report.run, "ladder-selection.txt"), diagnostic);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(report.run, "Moonrise_LadderSelection.png"));
    }
}
