using System.Linq;
using Lit.Traversal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static partial class BootstrapDistrictPlayProbe
{
    private static AbilitySaveData seuilEquipped;
    private static Vector3 seuilSupportFacing;
    private static Collider seuilTopPlatform;
    private static Vector3 seuilTopFacing;
    private static int seuilCarrySnaps;
    private static void CountSeuilCarrySnap() => seuilCarrySnaps++;
    private static bool seuilTestAbilityWasActive;
    private static Gamepad abilityMenuPad;

    private static void TickAbilitiesMenu()
    {
        var pause = PausePanelController.Instance;
        var service = AbilityService.Instance;
        var panel = service.GetComponent<AbilitySelectionPanel>();
        var grimpeur = service.Resolve("grimpeur");
        var saut = service.Resolve("saut_de_lumiere");
        double elapsed = UnityEditor.EditorApplication.timeSinceStartup - report.stageStarted;
        switch (report.stage)
        {
            case 670:
                if (!ControlReady(out _)) return;
                if (!service.IsUnlocked(grimpeur)) service.Unlock(grimpeur, false);
                Check(service.IsEquipped(service.LocalPlayerId, grimpeur) && service.IsEquipped(service.LocalPlayerId, saut),
                    "Two abilities are equipped for the isolated solo test.");
                pause.OpenPanel();
                var entry = pause.pausePanel.GetComponentsInChildren<PauseCursorAction>(true).Single(a => a.name == "AbilitiesButton");
                Check(entry.IsAvailable && pause.cursorController.TrySetCurrentItem((RectTransform)entry.transform),
                    "Maison: ability entry receives cursor selection.");
                entry.OnCursorSubmit();
                Check(panel.IsOpen, "Maison: submitting the ability option opens the dedicated panel.");
                abilityMenuPad = InputSystem.AddDevice<Gamepad>();
                Advance(671); return;
            case 671:
                if (elapsed < .15) return;
                InputSystem.QueueStateEvent(abilityMenuPad, new GamepadState().WithButton(GamepadButton.South));
                Advance(672); return;
            case 672:
                if (elapsed < .15) return;
                InputSystem.QueueStateEvent(abilityMenuPad, new GamepadState());
                Check(!service.IsEquipped(service.LocalPlayerId, grimpeur) && service.IsEquipped(service.LocalPlayerId, saut),
                    "A removes the highlighted ability without removing the other solo ability.");
                Advance(673); return;
            case 673:
                if (elapsed < .15) return;
                InputSystem.QueueStateEvent(abilityMenuPad, new GamepadState().WithButton(GamepadButton.South));
                Advance(674); return;
            case 674:
                if (elapsed < .15) return;
                InputSystem.QueueStateEvent(abilityMenuPad, new GamepadState());
                Check(service.IsEquipped(service.LocalPlayerId, grimpeur), "A equips the same ability again.");
                Advance(675); return;
            case 675:
                if (elapsed < .15) return;
                InputSystem.QueueStateEvent(abilityMenuPad, new GamepadState().WithButton(GamepadButton.East));
                Advance(676); return;
            case 676:
                if (elapsed < .15) return;
                InputSystem.RemoveDevice(abilityMenuPad);
                abilityMenuPad = null;
                Check(!panel.IsOpen && pause.IsOpen, "B returns to Pause without resuming the game.");
                pause.ClosePanel();
                Advance(677); return;
            case 677:
                if (!ControlReady(out var actor)) return;
                UsePortal(actor); Advance(4); return;
        }
    }

    private static void TickCling()
    {
        var actor = LocalPlayerContext.LocalCharacterRoot?.GetComponent<SquadCharacterController>();
        var point = ClingPoint.Find("limbes.seuil.poutre");
        var bridge = actor != null ? actor.GetComponent<LitOpsiveLocomotionBridge>() : null;
        if (actor == null || point == null || bridge == null) return;
        if (report.stage == 650)
        {
            if (!ControlReady(out _)) return;
            var abilities = AbilityService.Instance;
            var grimpeur = abilities.Resolve("grimpeur");
            seuilTestAbilityWasActive = abilities.HasEditorTestAbility(grimpeur);
            abilities.SetEditorTestAbility(grimpeur, false);
            Check(abilities.IsUnlocked(grimpeur) || abilities.Unlock(grimpeur, false), "Isolated test has Grimpeur.");
            seuilEquipped = abilities.Capture();
            var without = abilities.Capture();
            without.slots.RemoveAll(s => s.abilityId == "grimpeur");
            abilities.Restore(without);
            Check(!abilities.HasEquipped(actor.gameObject, grimpeur), "The negative fixture keeps Grimpeur unlocked but unequipped.");
            object[] args = { actor, null };
            Check((bool)typeof(ClingPoint).GetMethod("TryGetHoldPose", Private).Invoke(point, args),
                "Authored surface supports at least 4/5 of Lucian's body probes.");
            var hold = (Pose)args[1];
            var approach = hold.position - hold.rotation * Vector3.forward * .50f;
            Check(actor.TrySetUccExternalPositionAndRotation(approach, Quaternion.identity, true),
                "Lucian placed within reach on the authored approach side.");
            Advance(651); return;
        }
        if (report.stage == 651)
        {
            if (UnityEditor.EditorApplication.timeSinceStartup - report.stageStarted < .10) return;
            Check(!bridge.IsScriptedTraversalActive, "No automatic grab without Grimpeur equipped.");
            AbilityService.Instance.Restore(seuilEquipped);
            if (seuilTestAbilityWasActive)
                AbilityService.Instance.SetEditorTestAbility(AbilityService.Instance.Resolve("grimpeur"), true);
            Check(AbilityService.Instance.HasEquipped(actor.gameObject, AbilityService.Instance.Resolve("grimpeur")),
                "Grimpeur is equipped on Lucian after restoring the fixture.");
            Physics.SyncTransforms();
            object[] poseArgs = { actor, null };
            Check((bool)typeof(ClingPoint).GetMethod("TryGetHoldPose", Private).Invoke(point, poseArgs),
                "Resampled the moving beam after the unequipped check.");
            var hold = (Pose)poseArgs[1];
            var surface = (Collider)typeof(ClingPoint).GetField("supportSurface", Private).GetValue(point);
            object[] capsule = { actor, null, null, null };
            bool hasCapsule = (bool)typeof(ClingPoint).GetMethod("GetBody",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, capsule);
            Check(hasCapsule, "Contact is tested with Lucian's actual locomotion capsule.");
            var offset = (Vector3)capsule[1];
            var radius = (float)capsule[2];
            Vector3 opposite = hold.rotation * Vector3.forward;
            var isNear = typeof(ClingPoint).GetMethod("IsNear", Private);
            var canGrab = typeof(ClingPoint).GetMethod("CanGrab", Private);
            bool found = false;
            foreach (float angle in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f })
            {
                Vector3 outward = Quaternion.AngleAxis(angle, Vector3.up) * opposite;
                Vector3 start = hold.position + Vector3.up * offset.y + outward * 7f;
                if (!surface.Raycast(new Ray(start, -outward), out var hit, 7f) || Mathf.Abs(hit.normal.y) > .15f) continue;
                Vector3 normal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                Vector3 feet = hit.point + normal * (radius + .02f);
                feet.y = hold.position.y;
                if ((bool)isNear.Invoke(point, new object[] { feet })) continue;
                Check(actor.TrySetUccExternalPositionAndRotation(feet, Quaternion.identity, true),
                    "Placed Lucian's capsule in contact with a different flank, outside the old grab radius.");
                Physics.SyncTransforms();
                object[] args = { actor, true, null };
                if (!(bool)canGrab.Invoke(point, args)) continue;
                seuilSupportFacing = surface.transform.InverseTransformDirection(((Pose)args[2]).rotation * Vector3.forward);
                found = true;
                break;
            }
            Check(found, "The actual beam accepts a contact outside the old radius without interact input.");
            Advance(654); return;
        }
        if (report.stage == 654)
        {
            if (UnityEditor.EditorApplication.timeSinceStartup - report.stageStarted < .75) return;
            Check(bridge.IsScriptedTraversalActive, "Approaching with Grimpeur triggers the automatic grab.");
            var animation = actor.GetComponent<PlayerAnimationController>().Animator;
            Check(animation.GetCurrentAnimatorClipInfo(0).Any(clip => clip.clip.name == "Mixamo_ClimbingIdle"),
                "Lucian is holding with Mixamo_ClimbingIdle.");
            var surface = (Collider)typeof(ClingPoint).GetField("supportSurface", Private).GetValue(point);
            Check(Vector3.Dot(actor.transform.forward,
                Vector3.ProjectOnPlane(surface.transform.TransformDirection(seuilSupportFacing), Vector3.up).normalized) > .98f,
                "Lucian keeps facing the selected flank as the beam moves.");
            foreach (var bone in new[] { HumanBodyBones.Head, HumanBodyBones.Chest, HumanBodyBones.Hips,
                         HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
            {
                var t = animation.GetBoneTransform(bone);
                if (t != null) Check(true, bone + " relative to actor: " + actor.transform.InverseTransformPoint(t.position));
            }
            int contacts = new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot }
                .Count(bone =>
                {
                    var limb = animation.GetBoneTransform(bone);
                    if (limb == null) return false;
                    Vector3 facing = actor.transform.forward;
                    var ray = new Ray(limb.position - facing * .30f, facing);
                    return surface.Raycast(ray, out var hit, .49f) && Mathf.Abs(hit.distance - .30f) < .19f;
                });
            Check(contacts == 4, "All four animated hands/feet stay within support tolerance: " + contacts + "/4.");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(report.run, "Grimpeur.png"));
            var traversalType = typeof(ClingPoint).Assembly.GetType("Lit.Traversal.ClingTraversal");
            var traversal = actor.GetComponent(traversalType);
            traversalType.GetMethod("Release", Private).Invoke(traversal, null);
            Advance(655); return;
        }
        if (report.stage == 655)
        {
            if (bridge.IsScriptedTraversalActive) return;
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Isolated moving top grip fixture";
            var feet = actor.transform.position + Vector3.right * 6f;
            platform.transform.SetPositionAndRotation(feet - Vector3.up * .2f, Quaternion.identity);
            platform.transform.localScale = new Vector3(.48f, .4f, 4f);
            seuilTopPlatform = platform.GetComponent<Collider>();
            platform.SetActive(false);
            var grip = platform.AddComponent<ClingPoint>();
            typeof(ClingPoint).GetField("pointId", Private).SetValue(grip, "validation.top.grip");
            typeof(ClingPoint).GetField("supportSurface", Private).SetValue(grip, seuilTopPlatform);
            typeof(ClingPoint).GetField("grabAlongSurface", Private).SetValue(grip, true);
            platform.SetActive(true);
            Physics.SyncTransforms();
            Check(actor.TrySetUccExternalPositionAndRotation(feet + Vector3.up * .035f, Quaternion.LookRotation(Vector3.right), true),
                "Lucian is placed standing on the marked narrow top, facing across it.");
            Advance(656); return;
        }
        if (report.stage == 656)
        {
            if (UnityEditor.EditorApplication.timeSinceStartup - report.stageStarted < 1.15) return;
            Check(bridge.IsScriptedTraversalActive && Mathf.Abs(Vector3.Dot(actor.transform.forward, Vector3.forward)) > .8f,
                "The top grip starts with no input and turns Lucian along the narrow platform.");
            seuilTopFacing = seuilTopPlatform.transform.InverseTransformDirection(actor.transform.forward);
            seuilCarrySnaps = 0;
            Opsive.Shared.Events.EventHandler.RegisterEvent(actor.gameObject, "OnCameraImmediatePosition", CountSeuilCarrySnap);
            seuilTopPlatform.transform.Rotate(Vector3.up, 33f, Space.World);
            seuilTopPlatform.transform.position += Vector3.right * .22f;
            Advance(657); return;
        }
        if (report.stage == 657)
        {
            if (UnityEditor.EditorApplication.timeSinceStartup - report.stageStarted < .40) return;
            Check(bridge.IsScriptedTraversalActive && Vector3.Dot(actor.transform.forward,
                seuilTopPlatform.transform.TransformDirection(seuilTopFacing)) > .98f,
                "The top grip follows the platform's rotation and translation.");
            Opsive.Shared.Events.EventHandler.UnregisterEvent(actor.gameObject, "OnCameraImmediatePosition", CountSeuilCarrySnap);
            Check(seuilCarrySnaps == 0, "Continuous carriage did not snap the gameplay camera (" + seuilCarrySnaps + ").");
            var animation = actor.GetComponent<PlayerAnimationController>().Animator;
            int contacts = new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot }
                .Count(bone => {
                    var limb = animation.GetBoneTransform(bone);
                    if (limb == null) return false;
                    float distance = Vector3.Distance(limb.position, seuilTopPlatform.ClosestPoint(limb.position));
                    Check(true, bone + ": distance from top = " + distance.ToString("F3") +
                        "; relative to actor = " + actor.transform.InverseTransformPoint(limb.position));
                    return distance < .17f;
                });
            Check(contacts == 4, "Four animated contacts on the top grip after platform movement: " + contacts + "/4.");
            Finish(true, "Verified four-limb grips on the moving Seuil flank and the moving top fixture.");
        }
    }
}
