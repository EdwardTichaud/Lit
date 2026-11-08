using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lit.Fractures;
using Lit.Fractures.Editor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static partial class BootstrapDistrictPlayProbe
{
    private static Gamepad fracturePad;
    private static string fractureActorId, fractureCameraId;
    private static int fractureIndex, fracturePhase;
    private static float fracturePause;
    private static readonly List<(Vector2 point, FractureStation.Kind? kind, int index)> fractureSteps = new();

    private static void TickFracture()
    {
        var lab = FractureGraybox.District;
        if ((InputModeCoordinator.CurrentMode == InputMode.Dialogue || InputFocusStack.HasAnyFocus()) && fracturePad != null)
        {
            var state = new GamepadState();
            if (Time.realtimeSinceStartup % .65f < .12f) state = state.WithButton(GamepadButton.South);
            InputSystem.QueueStateEvent(fracturePad, state); InputSystem.Update();
            return; // Confirm authored dialogue/knowledge windows, never force-clear their focus lock.
        }
        if (!ControlReady(out var actor) || lab == null) return;
        var entrance = lab.GetComponentInChildren<FractureLightEntrance>();
        if (report.stage == 400)
        {
            if (!ZoneReady(FractureDistrictAuthoring.ManifestPath)) return;
            Check(lab.Player == actor && UnityEngine.Object.FindObjectsByType<SquadCharacterController>().Length == 1, "Embedded labyrinth reuses the actual campaign Lucian without spawning another avatar.");
            Check(lab.previewCamera == null && lab.lucianPrefab == null && lab.GetComponentsInChildren<Camera>(true).Length == 0, "No fixture camera/input owner in the district scene.");
            fractureActorId = actor.GetEntityId().ToString(); fractureCameraId = Camera.main.GetEntityId().ToString();
            if (actor.IsFlameEquipped) actor.ToggleFlame();
            Check(actor.GetComponent<LitOpsiveLocomotionBridge>().TeleportForSceneTransition(
                FractureDistrictAuthoring.EntrancePosition + new Vector3(1.7f, .08f, 0), Quaternion.Euler(0, 270, 0)),
                "Bounded setup fixture places the existing UCC actor on the authored belvedere floor, not inside the labyrinth.");
            fracturePad = InputSystem.AddDevice<Gamepad>(); fractureIndex = fracturePhase = 0; fracturePause = Time.realtimeSinceStartup;
            Advance(401); return;
        }
        if (report.stage == 401)
        {
            if (Time.realtimeSinceStartup - fracturePause < .5f) { if (fracturePause == 0) fracturePause = Time.realtimeSinceStartup; return; }
            Check(!entrance.IsIlluminated && entrance.concealedWall.activeSelf && !entrance.revealedFrame.activeSelf, "Unlit entrance is concealed by a solid wall, with no entry marker.");
            ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Fracture_Entrance_Dark.png"));
            actor.ToggleFlame(); Check(actor.IsFlameEquipped, "Actual character torch equipped through its normal gameplay method.");
            Advance(402); return;
        }
        if (report.stage == 402)
        {
            if (!entrance.IsIlluminated) return;
            Check(!entrance.concealedWall.activeSelf && entrance.revealedFrame.activeSelf, "Carried torch reveals and opens the physical threshold using its attached Light range.");
            ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Fracture_Entrance_Lit.png"));
            BuildFractureSteps(); fracturePause = 0; Advance(403); return;
        }
        if (report.stage == 403)
        {
            if (fractureIndex < fractureSteps.Count) { TickFractureStep(lab, actor); return; }
            Check(lab.ExplorationCompleted && lab.ArchivePuzzle.FoundCount == 3 && lab.ArchivePuzzle.DoorOpen, "Physical continuous route solved the two archive/gallery puzzles.");
            ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Fracture_Gallery_Complete.png"));
            Check(actor.GetEntityId().ToString() == fractureActorId && Camera.main.GetEntityId().ToString() == fractureCameraId,
                "No teleport, zone change, camera replacement or player respawn during the actual maze route.");
            Check(UnityEngine.SceneManagement.SceneManager.GetSceneByName("District_1_Core").isLoaded, "District stays loaded while exploring and returning.");
            fractureSteps.Clear(); fractureIndex = fracturePhase = 0;
            foreach (var p in new[] { new Vector2(28,4),new Vector2(24,4),new Vector2(24,0),new Vector2(16,0),new Vector2(8,0),new Vector2(0,0),new Vector2(0,-14),new Vector2(2,-19),new Vector2(2,-27),new Vector2(0,-29),new Vector2(0,-32) }) fractureSteps.Add((p,null,0));
            Advance(404); return;
        }
        if (report.stage == 404)
        {
            if (fractureIndex < fractureSteps.Count) { TickFractureStep(lab, actor); return; }
            Check(Vector3.Distance(actor.transform.position, FractureDistrictAuthoring.EntrancePosition) < 3.2f, "Walked back across the same threshold onto the original district floor.");
            var rules = UnityEngine.Object.FindAnyObjectByType<WorldRulesStateManager>();
            Check(rules != null && rules.TryGetInt(FractureGraybox.DistrictStateKey, out var packed) && packed == lab.CaptureDistrictState(), "Puzzle state is in the campaign's existing world-variable snapshot.");
            WorldSaveAdapter.Instance.SaveWorldSnapshot(); CharacterStateStore.Instance.Save();
            Advance(405); return;
        }
        if (report.stage == 405)
        {
            if (CharacterStateStore.Instance.IsSaving) return;
            Check(CharacterStateStore.Instance.LastSaveSucceeded, "Actual isolated campaign world/character save finished.");
            Check(report.errors.Count == 0, "No runtime errors observed in this campaign route.");
            CleanupFracture();
            Check(GameFlowService.OpenMainMenu(), "Return to normal menu after the embedded route.");
            Advance(5);
        }
    }
    private static void BuildFractureSteps()
    {
        fractureSteps.Clear();
        void Walk(float x,float z) => fractureSteps.Add((new Vector2(x,z),null,0));
        void Use(float x,float z,FractureStation.Kind kind,int index=0) => fractureSteps.Add((new Vector2(x,z),kind,index));
        Walk(0,-27); Use(0,-24.8f,FractureStation.Kind.Entry);
        foreach(var p in new[]{new Vector2(2,-25),new Vector2(2,-19),new Vector2(0,-14),new Vector2(0,0),new Vector2(-8,0),new Vector2(-16,0),new Vector2(-24,0),new Vector2(-24,-5),new Vector2(-30,-5)}) Walk(p.x,p.y);
        Use(-30,-4.2f,FractureStation.Kind.Bookshelf,2); Walk(-30,-5); Walk(-22,-5); Use(-22,-4.2f,FractureStation.Kind.Bookshelf,0);
        Walk(-22,-5); Walk(-24,-5); Walk(-24,0); Walk(-24,3.1f); Use(-26,3.1f,FractureStation.Kind.Bookshelf,1);
        foreach(var p in new[]{new Vector2(-24,3.1f),new Vector2(-24,0),new Vector2(-16,0),new Vector2(-8,0),new Vector2(0,0),new Vector2(8,0),new Vector2(16,0)}) Walk(p.x,p.y);
        Use(21,-.8f,FractureStation.Kind.GalleryDial,0); Use(24,-.8f,FractureStation.Kind.GalleryDial,1); Use(24,-.8f,FractureStation.Kind.GalleryDial,1);
        Use(30,-.8f,FractureStation.Kind.GalleryValidate); Walk(24,0); Walk(24,4); Walk(28,4); Use(28,5.2f,FractureStation.Kind.Exit);
    }
    private static void TickFractureStep(FractureGraybox lab, SquadCharacterController actor)
    {
        var step = fractureSteps[fractureIndex];
        Vector3 target = lab.transform.TransformPoint(new Vector3(step.point.x,0,step.point.y));
        Vector3 delta = Vector3.ProjectOnPlane(target - actor.transform.position,Vector3.up);
        if (fracturePhase == 0 && delta.magnitude > .28f) { FractureMove(actor,delta.normalized); return; }
        FractureMove(actor,Vector3.zero);
        if (!step.kind.HasValue) { fractureIndex++; fracturePhase=0; return; }
        var station = lab.GetComponentsInChildren<FractureStation>().First(s=>s.kind==step.kind.Value && (s.kind==FractureStation.Kind.Bookshelf?s.clueIndex==step.index:s.kind!=FractureStation.Kind.GalleryDial||s.dialIndex==step.index));
        var facing = Vector3.ProjectOnPlane(station.transform.position-actor.transform.position,Vector3.up).normalized;
        // Declared orientation-only fixture: keep the real UCC motor facing
        // the console for the A test. Never changes position or gameplay rules.
        if (fracturePhase == 0)
        {
            actor.GetComponent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>()
                .SetRotation(Quaternion.LookRotation(facing,Vector3.up),snapAnimator:false);
            fracturePause=Time.realtimeSinceStartup+.25f; fracturePhase=1; return;
        }
        if (fracturePhase==1)
            actor.GetComponent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>()
                .SetRotation(Quaternion.LookRotation(facing,Vector3.up),snapAnimator:false);
        if (fracturePhase==1 && Time.realtimeSinceStartup>=fracturePause)
        {
            actor.RefreshLocalInteractionDetectionForExternalLocomotion();
            if (!RuntimeOutlineSelectionManager.IsActiveInteractable(station) && Time.realtimeSinceStartup < fracturePause + 3) return;
            string detail = station.label + " actor=" + actor.transform.position + " local=" + lab.transform.InverseTransformPoint(actor.transform.position) +
                " station=" + station.transform.position + " inReach=" + station.IsInReach(actor) + " lit=" + CharacterInteractionDetection.IsInActiveFlameInfluence(station) +
                " visible=" + CharacterInteractionDetection.IsInteractionTargetVisibleFromCharacter(station,station.GetInteractionDetectionCollider(),station.transform,actor.GetInteractionOriginWorldPosition(),actor.transform) +
                " selected=" + RuntimeOutlineSelectionManager.ActiveInteractable + " torch=" + actor.FlameSecondsRemaining +
                " focus=" + InputFocusStack.HasAnyFocus() + " combat=" + (RealTimeCombatManager.Instance != null && RealTimeCombatManager.Instance.IsCombatActive) +
                " bridge=" + actor.GetComponent<LitUccInteractionBridge>()?.GetInteractionBlockReason() + " collider=" + station.GetInteractionDetectionCollider().enabled;
            var args = new object[] {station,actor.GetInteractionOriginWorldPosition(),actor.transform.forward,false,0f};
            detail += " candidate=" + typeof(SquadCharacterController).GetMethod("TryEvaluateInteractionCandidate",Private).Invoke(actor,args) +
                " distance=" + Mathf.Sqrt((float)args[4]) + " origin=" + actor.GetInteractionOriginWorldPosition() +
                " resolved=" + CharacterInteractionDetection.ResolveTarget(station.GetInteractionDetectionCollider()) +
                " radius=" + typeof(SquadCharacterController).GetMethod("ResolveInteractionDetectionRadius",Private).Invoke(actor,null) +
                " hp=" + actor.CurrentHp + " eligible=" + station.CanBeDetectedBy(actor) + " forward=" + actor.transform.forward;
            File.AppendAllText(Path.Combine(report.run,"fracture-stations.txt"),detail+"\n");
            Check(RuntimeOutlineSelectionManager.IsActiveInteractable(station),"Normal selection reached: "+detail);
            FractureMove(actor,facing*.8f,true);
            fracturePause=Time.realtimeSinceStartup+.1f;fracturePhase=2;return;
        }
        if (fracturePhase==2 && Time.realtimeSinceStartup>=fracturePause)
        { FractureMove(actor,Vector3.zero);fracturePause=Time.realtimeSinceStartup+.25f;fracturePhase=3;return; }
        if (fracturePhase==3 && Time.realtimeSinceStartup>=fracturePause)
        {
            if(station.kind==FractureStation.Kind.Bookshelf) Check(lab.ArchivePuzzle.HasClue(station.clueIndex),"Clue recorded from physical shelf.");
            if(station.kind==FractureStation.Kind.GalleryValidate) Check(lab.ArchivePuzzle.DoorOpen,"Combination opens the campaign door.");
            fractureIndex++;fracturePhase=0;
        }
    }
    private static void FractureMove(SquadCharacterController actor,Vector3 world,bool interact=false)
    {
        Vector2 stick=new Vector2(world.x,world.z);
        if(LitGameplayCameraModeController.TryGetTacticalMovement(actor.transform,out var mode))
        {
            var right=mode.ResolveScreenMoveInput(Vector2.right);var forward=mode.ResolveScreenMoveInput(Vector2.up);
            stick=new Vector2(Vector2.Dot(stick,right),Vector2.Dot(stick,forward));
        }
        var state=new GamepadState{leftStick=stick};
        if(interact)state=state.WithButton(GamepadButton.South);
        InputSystem.QueueStateEvent(fracturePad,state);InputSystem.Update();
    }
    private static void CleanupFracture()
    {
        if(fracturePad!=null){InputSystem.QueueStateEvent(fracturePad,new GamepadState());InputSystem.Update();InputSystem.RemoveDevice(fracturePad);fracturePad=null;}
        fracturePause=0;
    }
}
