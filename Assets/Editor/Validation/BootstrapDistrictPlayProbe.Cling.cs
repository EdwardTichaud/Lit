using System.Linq;
using Lit.Traversal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static partial class BootstrapDistrictPlayProbe
{
    private static void TickCling()
    {
        var actor = LocalPlayerContext.LocalCharacterRoot?.GetComponent<SquadCharacterController>();
        var point = ClingPoint.Find("limbes.seuil.poutre");
        var bridge = actor != null ? actor.GetComponent<LitOpsiveLocomotionBridge>() : null;
        if (actor == null || point == null || bridge == null) return;
        if (report.stage == 650)
        {
            if (!ControlReady(out _)) return;
            Check(AbilityService.Instance.IsUnlocked(AbilityService.Instance.Resolve("grimpeur")) ||
                AbilityService.Instance.Unlock(AbilityService.Instance.Resolve("grimpeur"), false), "Isolated test has Grimpeur.");
            object[] args = { actor, null };
            Check((bool)typeof(ClingPoint).GetMethod("TryGetHoldPose", Private).Invoke(point, args),
                "Authored surface supports at least 4/5 of Lucian's body probes.");
            Pose pose = (Pose)args[1];
            Check(actor.TrySetUccExternalPositionAndRotation(pose.position - pose.rotation * Vector3.forward * .1f,
                Quaternion.identity, true), "Test approached the grip from the wrong heading.");
            Advance(651); return;
        }
        if (report.stage == 651)
        {
            if (!bridge.IsScriptedTraversalActive || UnityEditor.EditorApplication.timeSinceStartup - report.stageStarted < 1.3) return;
            var animation = actor.GetComponent<PlayerAnimationController>().Animator;
            Check(animation.GetCurrentAnimatorClipInfo(0).Any(clip => clip.clip.name == "Mixamo_ClimbingIdle"),
                "Lucian is holding with Mixamo_ClimbingIdle.");
            Check(Vector3.Dot(actor.transform.forward, Vector3.right) > .99f, "Lucian faces the authored surface.");
            foreach (var bone in new[] { HumanBodyBones.Head, HumanBodyBones.Chest, HumanBodyBones.Hips,
                         HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
            {
                var t = animation.GetBoneTransform(bone);
                if (t != null) Check(true, bone + " relative to actor: " + actor.transform.InverseTransformPoint(t.position));
            }
            var surface = (Collider)typeof(ClingPoint).GetField("supportSurface", Private).GetValue(point);
            int contacts = new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot }
                .Count(bone =>
                {
                    var limb = animation.GetBoneTransform(bone);
                    return limb != null && Vector3.Distance(limb.position, surface.ClosestPoint(limb.position)) < .19f;
                });
            Check(contacts >= 3, "At least three of four animated hands/feet touch the support tolerance: " + contacts);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(report.run, "Grimpeur.png"));
            point.transform.position += Vector3.up * .14f;
            point.transform.Rotate(Vector3.up, 12f, Space.World);
            Physics.SyncTransforms();
            Advance(652); return;
        }
        if (report.stage == 652)
        {
            if (UnityEditor.EditorApplication.timeSinceStartup - report.stageStarted < .45) return;
            Check(bridge.IsScriptedTraversalActive && Vector3.Dot(actor.transform.forward, point.transform.right) > .99f,
                "Held character tracks the rotated and translated support.");
            enolaPad = InputSystem.AddDevice<Gamepad>();
            PulseEnola(GamepadButton.South);
            Advance(653); return;
        }
        if (report.stage == 653)
        {
            if (UnityEditor.EditorApplication.timeSinceStartup - report.stageStarted < .4) return;
            Check(!bridge.IsScriptedTraversalActive &&
                Vector3.Dot(actor.transform.position - point.GripPosition, -point.transform.right) > .65f,
                "Jump releases the grip and propels Lucian away from the wall.");
            Finish(true, "Actual Grimpeur hold, animation, moving surface and jump verified.");
        }
    }
}
