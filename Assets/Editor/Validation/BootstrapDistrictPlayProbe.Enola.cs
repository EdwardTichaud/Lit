using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public static partial class BootstrapDistrictPlayProbe
{
    private static Gamepad enolaPad;
    private static string enolaSessionId, enolaSaveId;
    private static int enolaBeat;
    private static Vector3 enolaApproach;
    [MenuItem("Lit/Enola/Test knife game (isolated save)")]
    public static void RunEnolaValidation() => Begin(false, enola: true);
    [InitializeOnLoadMethod]
    private static void BindEnolaCleanup() => EditorApplication.playModeStateChanged += change =>
    {
        if (change == PlayModeStateChange.ExitingPlayMode && enolaPad != null && enolaPad.added)
        { InputSystem.RemoveDevice(enolaPad); enolaPad = null; }
    };

    private static void TickEnola()
    {
        var root = LocalPlayerContext.LocalCharacterRoot;
        var actor = root != null ? root.GetComponent<SquadCharacterController>() : null;
        var game = UnityEngine.Object.FindAnyObjectByType<EnolaKnifeGame>();
        var action = game != null ? game.GetComponent<WorldActionInteractable>() : null;
        var bridge = actor != null ? actor.GetComponent<LitOpsiveLocomotionBridge>() : null;
        bool settled = EditorApplication.timeSinceStartup - report.stageStarted > .35;
        switch (report.stage)
        {
            case 600:
                report.scope = "Isolated production Bootstrap/Maison/Enola, actual outline and virtual gamepad A/B; UCC approach fixtures. Fades, camera, timing, abort, reward, return and disk reload. Not manual input or multiplayer.";
                var slot = SaveSessionManager.Instance.Sessions.Single().saves.Single(); enolaSessionId = slot.sessionId; enolaSaveId = slot.saveId;
                enolaPad = InputSystem.AddDevice<Gamepad>();
                ApproachEnola(actor, EnolaEntrance); Advance(601); return;
            case 601:
                if (!ControlReady(out _) || !settled) return;
                PressEnolaWorld(EnolaEntrance); Advance(602); return;
            case 602:
                if (!ZoneReady(EnolaKnifeAuthoring.ManifestPath) || !ControlReady(out _)) return;
                Check(game != null && !KnowledgeManager.Instance.HasKnowledge(game.reward), "Authored entrance loaded Enola with reward locked.");
                ApproachEnola(actor, action); Advance(603); return;
            case 603:
                if (!ControlReady(out _) || !settled) return;
                enolaApproach = actor.transform.position;
                PressEnolaWorld(action);
                Check(game.IsRunning && DialoguePanelUI.Instance?.IsShowing == true, "Selected stool opens Enola dialogue.");
                Advance(604); return;
            case 604:
                if (!settled) return;
                PulseEnola(GamepadButton.South); Advance(605); return;
            case 605:
                if (!game.IsPlayingRhythm || !settled || Time.frameCount == (int)typeof(EnolaKnifeGame).GetField("stageFrame", Private).GetValue(game)) return;
                Check(Vector3.Distance(actor.transform.position, game.stoolPoint.position) < .12f && bridge.IsScriptedTraversalActive, "Actual UCC actor held at StoolPoint.");
                var state = actor.GetComponent<CharacterAnimationController>().Animator.GetCurrentAnimatorStateInfo(0);
                Check(state.shortNameHash == Animator.StringToHash(EnolaKnifeGame.Idle) && state.loop, "Authentic knife idle plays in a loop.");
                var shotCamera = Camera.main;
                float pitch = Mathf.DeltaAngle(0, shotCamera.transform.eulerAngles.x);
                Check(pitch >= 29.5f && pitch <= 68.5f && Mathf.Abs(shotCamera.fieldOfView - 60f) < .5f &&
                    Vector3.Distance(shotCamera.transform.position, game.CurrentCameraPosition) < .05f, "Main gameplay camera holds the dedicated head-height shot.");
                var animator = actor.GetComponent<CharacterAnimationController>().Animator;
                foreach (var bone in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
                {
                    Vector3 view = shotCamera.WorldToViewportPoint(animator.GetBoneTransform(bone).position);
                    Check(view.z > shotCamera.nearClipPlane && view.x > 0 && view.x < 1 && view.y > 0 && view.y < 1,
                        "Knife shot frames " + bone + " at " + view);
                }
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(report.run, "Enola_KnifeCamera.png"));
                Advance(616); return;
            case 616:
                if (!settled) return;
                PulseEnola(GamepadButton.East); Advance(606); return;
            case 606:
                if (game.IsRunning || !ControlReady(out _) || !settled) return;
                Check(Vector3.Distance(actor.transform.position, enolaApproach) < .3f && !bridge.IsInputSuppressedByUcc &&
                    !RuntimeOutlineSelectionManager.IsSuspended && !InputFocusStack.HasAnyFocus() && !KnowledgeManager.Instance.HasKnowledge(game.reward),
                    "B restores position, input and outline and grants nothing.");
                PressEnolaWorld(action); Advance(607); return;
            case 607:
                if (!settled) return;
                PulseEnola(GamepadButton.South); enolaBeat = -1; Advance(608); return;
            case 608:
                if (game.IsPlayingRhythm)
                {
                    var rhythm = (EnolaKnifeRhythm)typeof(EnolaKnifeGame).GetField("rhythm", Private).GetValue(game);
                    double now = Time.unscaledTimeAsDouble;
                    if (rhythm.InWindow(now) && rhythm.Beat(now) != enolaBeat)
                    {
                        enolaBeat = rhythm.Beat(now);
                        var bar = (RectTransform)typeof(EnolaKnifeGame).GetField("targetWindow", Private).GetValue(game);
                        Vector2 window = rhythm.Window(enolaBeat);
                        Check(Mathf.Abs(bar.anchorMin.x - (window.x - window.y * .5f)) < .001f &&
                            Mathf.Abs(bar.anchorMax.x - (window.x + window.y * .5f)) < .001f,
                            "Yellow HUD zone matches scoring bounds for pass " + (enolaBeat + 1));
                        PulseEnola(GamepadButton.South);
                    }
                    return;
                }
                if (!game.IsRunning || DialoguePanelUI.Instance?.IsShowing != true || game.Score == 0) return;
                Check(game.Score >= EnolaKnifeRhythm.PassingScore, "Virtual gamepad passed the eight-beat round: " + game.Score);
                Advance(609); return;
            case 609:
                if (!settled) return;
                PulseEnola(GamepadButton.South); Advance(610); return;
            case 610:
                if (game.IsRunning || !ControlReady(out _) || !CharacterStateStore.Instance.LastSaveSucceeded) return;
                Check(KnowledgeManager.Instance.HasKnowledge(game.reward) && !bridge.IsInputSuppressedByUcc, "Victory granted the reward and wrote the normal save.");
                ApproachEnola(actor, EnolaExit); Advance(611); return;
            case 611:
                if (!ControlReady(out _) || !settled) return;
                PressEnolaWorld(EnolaExit); Advance(612); return;
            case 612:
                if (!ZoneReady("Assets/Scenes/Maison/ZoneManifest_Maison.asset") || !ControlReady(out _)) return;
                Check(KnowledgeManager.Instance.HasKnowledgeId(EnolaKnifeGame.RewardId) && game == null, "Authored exit returned to Maison with the reward.");
                CharacterStateStore.Instance.Save();
                Check(CharacterStateStore.Instance.LastSaveSucceeded, "Saved optional reward in Maison.");
                Advance(613); return;
            case 613:
                if (!settled) return;
                Check(GameFlowService.OpenMainMenu(), "Returned to menu before disk reload."); Advance(614); return;
            case 614:
                if (GameFlowService.Instance.IsTransitioning || !SceneManager.GetSceneByName("MainMenu").isLoaded || GameFlowService.Instance.HasGameplaySession) return;
                SaveSessionManager.Instance.SetActiveSave(enolaSessionId, enolaSaveId);
                Check(GameFlowService.StartOrLoadGame(), "Reopened the isolated slot normally."); Advance(615); return;
            case 615:
                if (!ZoneReady("Assets/Scenes/Maison/ZoneManifest_Maison.asset") || !ControlReady(out _)) return;
                Check(KnowledgeManager.Instance.UnlockedKnowledge.Count(k => k.knowledgeId == EnolaKnifeGame.RewardId) == 1, "Disk reload restored exactly one reward outside Enola.");
                Check(GameFlowService.OpenMainMenu(), "Finish Enola test at menu."); Advance(5); return;
        }
    }
    private static WorldActionInteractable EnolaEntrance => GameObject.Find(EnolaKnifeAuthoring.EntranceName).GetComponent<WorldActionInteractable>();
    private static WorldActionInteractable EnolaExit => GameObject.Find(EnolaKnifeAuthoring.ExitName).GetComponent<WorldActionInteractable>();
    private static void ApproachEnola(SquadCharacterController actor, WorldActionInteractable target)
    {
        Vector3 center = target.transform.position;
        foreach (var direction in new[] { Vector3.back, Vector3.left, Vector3.right, Vector3.forward })
        {
            var sample = center + direction * 1.35f;
            if (!Physics.Raycast(sample + Vector3.up * .7f, Vector3.down, out var floor, 1.5f, ~0, QueryTriggerInteraction.Ignore) ||
                floor.normal.y < .85f || Mathf.Abs(floor.point.y - center.y) > .55f) continue;
            if (Physics.OverlapCapsule(floor.point + Vector3.up * .35f, floor.point + Vector3.up * 1.6f, .28f, ~0, QueryTriggerInteraction.Ignore)
                .Any(c => !c.transform.IsChildOf(actor.transform))) continue;
            Check(actor.TrySetUccExternalPositionAndRotation(floor.point + Vector3.up * .04f, Quaternion.LookRotation(-direction), true), "Approach fixture on real floor: " + target.name);
            Physics.SyncTransforms(); return;
        }
        throw new InvalidOperationException("No safe approach for " + target.name);
    }
    private static void PressEnolaWorld(WorldActionInteractable target)
    {
        Check(RuntimeOutlineSelectionManager.IsActiveInteractable(target) && RuntimeOutlineSelectionManager.SelectedTargets.Count > 0,
            "Live detector outlined " + target.name + "; current=" + (RuntimeOutlineSelectionManager.ActiveInteractable as Component)?.name);
        PulseEnola(GamepadButton.South);
    }
    private static void PulseEnola(GamepadButton button)
    {
        InputSystem.QueueStateEvent(enolaPad, new GamepadState().WithButton(button)); InputSystem.Update();
        InputSystem.QueueStateEvent(enolaPad, new GamepadState()); InputSystem.Update();
    }
}
