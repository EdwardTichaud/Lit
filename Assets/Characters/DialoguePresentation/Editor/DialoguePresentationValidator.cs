using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class DialoguePresentationValidator : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => Validate();

    [MenuItem("Lit/Animation/Validate Lucian Dialogue Presentation")]
    public static void Validate()
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/1_Squad/Lucian/Lucian.asset");
        var profile = data != null ? data.playerSettings.dialoguePresentation : null;
        if (profile == null) throw new BuildFailedException("Lucian dialogue presentation profile is missing.");
        var ids = new HashSet<string>();
        foreach (var g in profile.gestures)
        {
            if (string.IsNullOrWhiteSpace(g.id) || !ids.Add(g.id) || g.source == null || g.upperBody == null)
                throw new BuildFailedException("Incomplete or duplicate Lucian dialogue gesture: " + g.id);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(g.source, out string _, out long id);
            if (id != g.sourceId) throw new BuildFailedException("Unresolved dialogue source subasset: " + g.id);
            if (!g.validated)
            {
                if (string.IsNullOrWhiteSpace(g.validation)) throw new BuildFailedException("Unavailable gesture needs a reason: " + g.id);
                continue;
            }
            foreach (var binding in AnimationUtility.GetCurveBindings(g.upperBody))
                if (DialoguePresentationSetup.IsLowerBody(binding.propertyName) || DialoguePresentationSetup.IsFace(binding.propertyName) ||
                    binding.type != typeof(Animator))
                    throw new BuildFailedException("Forbidden dialogue upper-body curve: " + g.id + " / " + binding.propertyName);
            if (AnimationUtility.GetAnimationEvents(g.upperBody).Length != 0)
                throw new BuildFailedException("Dialogue clip contains gameplay events: " + g.id);
        }
        foreach (bool seated in new[] { false, true })
        foreach (DialogueRole role in Enum.GetValues(typeof(DialogueRole)))
            if (profile.Choose(role, DialogueIntent.Automatic, seated, null, -1, 1) < 0)
                throw new BuildFailedException("Missing dialogue coverage: seated=" + seated + " / " + role);
        foreach (var expression in profile.expressions)
            if (expression.preset != null && expression.preset.blendShapes.Any(w => w != null && !FacialExpressionController.IsDialogueSafeShape(w.blendShapeName)))
                throw new BuildFailedException("Dialogue expression controls mouth opening: " + expression.intent);
        foreach (string path in new[]
        {
            "Assets/Characters/3_Enemy/Iluvilirae/Lucian_ImpactLab.controller",
            "Assets/Characters/AnimationPresentation/LucianCoherencePreview/Cadence01/LucianCadence.controller"
        })
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            foreach (string layerName in CharacterDialoguePresentationController.Layers)
            {
                var layer = controller.layers.SingleOrDefault(l => l.name == layerName);
                if (layer == null || layer.avatarMask == null || layer.defaultWeight != 0)
                    throw new BuildFailedException("Missing or active-by-default dialogue layer: " + path + " / " + layerName);
                foreach (var g in profile.gestures)
                {
                    var state = layer.stateMachine.states.Select(s => s.state).SingleOrDefault(s => s.name == g.id);
                    var expected = layerName == CharacterDialoguePresentationController.Layers[0] ? g.body : g.upperBody;
                    if (state == null || state.motion != expected || state.transitions.Length != 0)
                        throw new BuildFailedException("Invalid dialogue state: " + layerName + " / " + g.id);
                }
                bool upper = layerName != CharacterDialoguePresentationController.Layers[0];
                if (upper && (layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root) ||
                    layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg) ||
                    layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg)))
                    throw new BuildFailedException("Dialogue upper mask affects the lower body.");
                if (layerName == CharacterDialoguePresentationController.Layers[2] &&
                    (layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm) ||
                     layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body)))
                    throw new BuildFailedException("Torch dialogue mask affects the holding arm or torso.");
            }
        }
        foreach (string path in new[]
        {
            "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab",
            "Assets/Characters/3_Enemy/Iluvilirae/Lucian_ImpactLab.prefab",
            "Assets/Characters/AnimationPresentation/LucianCoherencePreview/Cadence01/LucianCadence.prefab"
        })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var actor = prefab.GetComponentInChildren<SquadCharacterController>(true);
            var animator = actor.GetComponent<CharacterAnimationController>().Animator;
            if (actor.GetComponent<CharacterDialoguePresentationController>() == null || animator.applyRootMotion)
                throw new BuildFailedException("Lucian dialogue prefab authority is invalid: " + path);
        }
        Debug.Log("Lucian dialogue presentation contract validated.");
    }
}
