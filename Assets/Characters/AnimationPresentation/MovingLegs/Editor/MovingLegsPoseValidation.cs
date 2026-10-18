using System;
using UnityEditor;
using UnityEngine;

public static class MovingLegsPoseValidation
{
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch required");
        MovingLegsAnimatorSetup.Install();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab");
        var root = new GameObject(prefab.name);
        foreach (Transform child in prefab.transform)
        {
            var instance = UnityEngine.Object.Instantiate(child.gameObject, root.transform, false);
            instance.name = child.name; // Avatar paths must not gain Unity's (Clone) suffix.
        }
        var original = prefab.GetComponent<Animator>();
        var animator = root.AddComponent<Animator>();
        animator.avatar = original.avatar; animator.runtimeAnimatorController = original.runtimeAnimatorController;
        animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind(); animator.Update(0);
        int layer = animator.GetLayerIndex(PlayerMovingLegsOverlay.LayerName);
        if (layer < 0 || !animator.isHuman) throw new InvalidOperationException("Humanoid masked layer missing");
        var leg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        var chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
        if (leg == null || chest == null) throw new InvalidOperationException("Required humanoid bones missing");
        int guard = Animator.StringToHash("Base Layer.RealTimeCombat_RootMotion.Guard_Block");
        int locomotion = Animator.StringToHash("Moving Legs.CombatLocomotion");
        animator.SetFloat("MovingLegsX", 1); animator.SetFloat("MovingLegsZ", 0);
        animator.SetFloat("MovingLegsTier", 1.1f);
        animator.SetFloat("MovingLegsMagnitude", 1);
        animator.SetLayerWeight(layer, 0); animator.Play(guard, 0, .2f); animator.Update(0);
        var torso = chest.localRotation;
        animator.SetLayerWeight(layer, 1); animator.Play(guard, 0, .2f); animator.Play(locomotion, layer, .1f); animator.Update(0);
        var first = leg.localRotation;
        if (Quaternion.Angle(torso, chest.localRotation) > .1f) throw new InvalidOperationException("Leg mask changed guard torso");
        animator.Play(guard, 0, .2f); animator.Play(locomotion, layer, .45f); animator.Update(0);
        float legAngle = Quaternion.Angle(first, leg.localRotation);
        if (legAngle < 4 || Quaternion.Angle(torso, chest.localRotation) > .1f)
            throw new InvalidOperationException("Leg gait/torso separation failed: leg delta=" + legAngle);
        if (root.transform.position.sqrMagnitude > .001f) throw new InvalidOperationException("Layer introduced root motion");
        UnityEngine.Object.DestroyImmediate(root);
        Debug.Log("[MovingLegs] POSE PASS | real humanoid leg gait delta=" + legAngle + ", unchanged guard chest and root, independent lower-body mask");
        EditorApplication.Exit(0);
    }
}
