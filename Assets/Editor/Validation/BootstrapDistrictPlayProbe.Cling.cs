using System.Linq;
using Lit.Traversal;
using UnityEngine;

public static partial class BootstrapDistrictPlayProbe
{
    private static AbilitySaveData seuilEquipped;
    private static Pose seuilHoldPose;

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
            var abilities = AbilityService.Instance;
            seuilEquipped = abilities.Capture();
            var without = abilities.Capture();
            without.slots.RemoveAll(s => s.abilityId == "grimpeur");
            abilities.Restore(without);
            object[] args = { actor, null };
            Check((bool)typeof(ClingPoint).GetMethod("TryGetHoldPose", Private).Invoke(point, args),
                "Authored surface supports at least 4/5 of Lucian's body probes.");
            seuilHoldPose = (Pose)args[1];
            var approach = seuilHoldPose.position - seuilHoldPose.rotation * Vector3.forward * .50f;
            Check(actor.TrySetUccExternalPositionAndRotation(approach, Quaternion.identity, true),
                "Lucian placed within reach on the authored approach side.");
            Advance(651); return;
        }
        if (report.stage == 651)
        {
            if (UnityEditor.EditorApplication.timeSinceStartup - report.stageStarted < .10) return;
            Check(!bridge.IsScriptedTraversalActive, "No automatic grab without Grimpeur equipped.");
            AbilityService.Instance.Restore(seuilEquipped);
            Check(AbilityService.Instance.HasEquipped(actor.gameObject, AbilityService.Instance.Resolve("grimpeur")),
                "Grimpeur is equipped on Lucian after restoring the fixture.");
            Physics.SyncTransforms();
            object[] poseArgs = { actor, null };
            Check((bool)typeof(ClingPoint).GetMethod("TryGetHoldPose", Private).Invoke(point, poseArgs),
                "Resampled the moving beam after the unequipped check.");
            seuilHoldPose = (Pose)poseArgs[1];
            Check(actor.TrySetUccExternalPositionAndRotation(
                seuilHoldPose.position - seuilHoldPose.rotation * Vector3.forward * .30f,
                Quaternion.identity, true), "Same approach with Grimpeur; no interact input sent.");
            Physics.SyncTransforms();
            object[] args = { actor, true, null };
            bool eligible = (bool)typeof(ClingPoint).GetMethod("CanGrab", Private).Invoke(point, args);
            bool clear = (bool)typeof(ClingPoint).GetMethod("IsClear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { actor.transform.position, seuilHoldPose, actor, null });
            Check(eligible, $"Approach is eligible: actor={actor.transform.position}, target={seuilHoldPose.position}, clear={clear}, driving={bridge.IsDriving}, suppressed={bridge.IsInputSuppressedByUcc}, combat={bridge.IsCombatLockActive}");
            Advance(654); return;
        }
        if (report.stage == 654)
        {
            if (UnityEditor.EditorApplication.timeSinceStartup - report.stageStarted < .75) return;
            Check(bridge.IsScriptedTraversalActive, "Approaching with Grimpeur triggers the automatic grab.");
            var animation = actor.GetComponent<PlayerAnimationController>().Animator;
            Check(animation.GetCurrentAnimatorClipInfo(0).Any(clip => clip.clip.name == "Mixamo_ClimbingIdle"),
                "Lucian is holding with Mixamo_ClimbingIdle.");
            object[] poseArgs = { actor, null };
            Check((bool)typeof(ClingPoint).GetMethod("TryGetHoldPose", Private).Invoke(point, poseArgs) &&
                Vector3.Dot(actor.transform.forward, ((Pose)poseArgs[1]).rotation * Vector3.forward) > .98f,
                "Lucian faces the moving authored surface.");
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
            Check(true, "Animated hands/feet within support tolerance: " + contacts + "/4.");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(report.run, "Grimpeur.png"));
            Finish(true, "Automatic Grimpeur grab on the authored Seuil beam verified with and without equipment.");
        }
    }
}
