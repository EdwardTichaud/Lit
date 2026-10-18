using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class MovingLegsAnimatorSetup
{
    private const string MaskPath = "Assets/Characters/AnimationPresentation/MovingLegs/MovingLegs.mask";
    [MenuItem("Lit/Animation/Install Moving Legs Overlay")]
    public static void Install()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null) { mask = new AvatarMask { name = "MovingLegs" }; AssetDatabase.CreateAsset(mask, MaskPath); }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, true);
        EditorUtility.SetDirty(mask);
        var paths = new List<string> { "Assets/Characters/4_Animations/Player_Model.controller", "Assets/Characters/1_Squad/Soren/Soren_Model.controller" };
        const string labPath = "Assets/Characters/3_Enemy/Iluvilirae/Lucian_ImpactLab.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(labPath) != null) paths.Add(labPath);
        foreach (var path in paths)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) throw new InvalidOperationException("Missing controller " + path);
            // Idempotent: do not overwrite an already authored overlay.
            if (controller.layers.Any(layer => layer.name == PlayerMovingLegsOverlay.LayerName)) continue;
            var root = controller.layers[0].stateMachine;
            var freeSource = root.states.First(s => s.state.name == "Locomotion").state;
            var combatSource = root.states.First(s => s.state.name == "CombatLocomotion").state;
            foreach (string parameter in new[] { "MovingLegsX", "MovingLegsZ", "MovingLegsSpeed", "MovingLegsMagnitude", "MovingLegsTier" })
                if (!controller.parameters.Any(p => p.name == parameter)) controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
            var machine = new AnimatorStateMachine { name = PlayerMovingLegsOverlay.LayerName };
            AssetDatabase.AddObjectToAsset(machine, controller);
            var copies = new Dictionary<Motion, Motion>();
            var free = machine.AddState("Locomotion"); free.motion = CopyMotion(freeSource.motion, controller, copies); free.writeDefaultValues = false;
            var combat = machine.AddState("CombatLocomotion"); combat.motion = CopyMotion(combatSource.motion, controller, copies); combat.writeDefaultValues = false;
            machine.defaultState = free;
            Transition(free, combat, AnimatorConditionMode.If);
            Transition(combat, free, AnimatorConditionMode.IfNot);
            controller.AddLayer(new AnimatorControllerLayer { name = PlayerMovingLegsOverlay.LayerName, avatarMask = mask,
                stateMachine = machine, defaultWeight = 0, blendingMode = AnimatorLayerBlendingMode.Override });
            EditorUtility.SetDirty(controller);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[MovingLegs] Installed masked, event-free locomotion layers on player controllers.");
    }
    public static void RunBatch() { if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch required"); Install(); EditorApplication.Exit(0); }
    private static void Transition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
    {
        var transition = from.AddTransition(to); transition.hasExitTime = false; transition.duration = .06f;
        transition.AddCondition(mode, 0, "CombatStrafeActive");
    }
    private static string Parameter(string name) => name switch
    {
        "HorizontalMovement" => "MovingLegsX", "ForwardMovement" => "MovingLegsZ", "LitSpeed" => "MovingLegsSpeed",
        "LocomotionTier" => "MovingLegsTier", "CombatMoveMagnitude" => "MovingLegsMagnitude", _ => name
    };
    private static Motion CopyMotion(Motion motion, AnimatorController controller, Dictionary<Motion, Motion> copies)
    {
        if (motion == null) return null;
        // Reuse already event-free clips; only eventful clips need a clean copy.
        if (motion is AnimationClip originalClip && originalClip.events.Length == 0) return motion;
        if (copies.TryGetValue(motion, out var existing)) return existing;
        var copy = UnityEngine.Object.Instantiate(motion); copy.name = "MovingLegs_" + motion.name;
        copies.Add(motion, copy); AssetDatabase.AddObjectToAsset(copy, controller);
        if (copy is AnimationClip clip) clip.events = Array.Empty<AnimationEvent>();
        if (copy is BlendTree tree)
        {
            tree.blendParameter = Parameter(tree.blendParameter); tree.blendParameterY = Parameter(tree.blendParameterY);
            var children = tree.children;
            for (int i = 0; i < children.Length; i++) { children[i].motion = CopyMotion(children[i].motion, controller, copies); children[i].directBlendParameter = Parameter(children[i].directBlendParameter); }
            tree.children = children;
        }
        EditorUtility.SetDirty(copy); return copy;
    }
}
