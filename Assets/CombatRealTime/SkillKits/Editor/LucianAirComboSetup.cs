using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class LucianAirComboSetup
{
    public const string Folder = GroundImpactSkillSetup.Folder + "/AirCombo";
    public const string FirstPath = "Assets/CombatRealTime/Skills/AirBasicSkill_1.asset";
    public static string AerialSourcePath(int index) =>
        "Assets/0 - UnityPackages/Fab/TwinBladesBundle/Twinblades_Expansion_V2/Animation/Inplace/Battle/Twinblades_Air_Attack_" +
        (index - 1) + "_Inplace.FBX";

    [MenuItem("Lit/Combat Lab/Configure Lucian Aerial Combo")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring.");
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(GroundImpactSkillSetup.DataPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GroundImpactSkillSetup.ControllerPath);
        var first = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(FirstPath);
        if (data == null || controller == null || first == null)
            throw new InvalidOperationException("Lucian laboratory aerial assets missing.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(GroundImpactSkillSetup.Folder, "AirCombo");
        var combo = new[] { first, Create(first, 2), Create(first, 3) };
        foreach (var skill in combo.Skip(1))
        {
            var machine = controller.layers[0].stateMachine;
            string name = skill.AnimatorState.Substring("Base Layer.".Length);
            var state = machine.states.FirstOrDefault(item => item.state.name == name).state;
            if (state == null) state = machine.AddState(name);
            if (state.motion != skill.AnimationClip)
            {
                state.motion = skill.AnimationClip;
                state.writeDefaultValues = false;
                EditorUtility.SetDirty(state);
                EditorUtility.SetDirty(controller);
            }
        }
        if (data.airBasicSkills == null || !data.airBasicSkills.SequenceEqual(combo))
        {
            data.airBasicSkills = combo.ToList();
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[AirCombo] Installed three manual aerial Basics on Lucian laboratory only.");
    }

    private static BasicSkillsSO Create(BasicSkillsSO first, int index)
    {
        string path = Folder + "/AirBasicSkill_" + index + ".asset";
        var skill = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(path);
        if (skill != null)
        {
            UseTwinbladesAirClip(skill, index);
            return skill;
        }
        var source = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(GroundImpactSkillSetup.Folder + "/CombatLab_BasicSkill_" + index + ".asset");
        if (source == null) throw new InvalidOperationException("Sword swing source missing: " + index);
        skill = UnityEngine.Object.Instantiate(first);
        skill.name = "AirBasicSkill_" + index;
        skill.skillName = "Attaque aerienne basique " + index;
        skill.description = "Coup aerien manuel " + index + " du combo de Lucian.";
        skill.animationClip = null;
        skill.animatorState = "Base Layer.AirBasicSkill_" + index;
        skill.targetingMode = SkillTargetingMode.FreeAttack;
        skill.damages = source.damages;
        skill.clarityGainOnHit = source.clarityGainOnHit;
        skill.maximumHitDistance = source.maximumHitDistance;
        float impact01 = AnimationUtility.GetAnimationEvents(source.AnimationClip)
            .Single(e => e.functionName == "ResolveSkillImpact" || e.functionName == "HitEnemy").time / source.AnimationClip.length;
        skill.presentation.chainNormalizedTime = impact01 + .04f;
        skill.presentation.chainTransitionNormalizedTime = impact01 + .1f;
        skill.presentation.recoveryNormalizedTime = .82f;
        skill.presentation.mobilityCancelNormalizedTime = impact01 + .04f;
        AssetDatabase.CreateAsset(skill, path);
        UseTwinbladesAirClip(skill, index);
        return skill;
    }

    private static void UseTwinbladesAirClip(BasicSkillsSO skill, int index)
    {
        string path = Folder + "/AirBasicSkill_" + index + "_InPlace.anim";
        string provenance = "LucianAirCombo.TwinbladesAir.v1:" + AerialSourcePath(index);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip != null && skill.AnimationClip == clip && AssetImporter.GetAtPath(path).userData == provenance) return;
        var source = ActorAnimationAudit.Source(AerialSourcePath(index));
        if (!source.isHumanMotion) throw new InvalidOperationException("Humanoid aerial source required: " + source.name);
        var replacement = UnityEngine.Object.Instantiate(source);
        replacement.name = "AirBasicSkill_" + index + "_InPlace";
        try
        {
            var old = skill.AnimationClip;
            float impact01 = old != null ? AnimationUtility.GetAnimationEvents(old)
                .Single(e => e.functionName == "ResolveSkillImpact" || e.functionName == "HitEnemy").time / old.length
                : skill.presentation.chainNormalizedTime - .04f;
            float impact = impact01 * replacement.length;
            AnimationUtility.SetAnimationEvents(replacement, new[] {
                new AnimationEvent { time = 0, functionName = "ShowSword" },
                new AnimationEvent { time = impact, functionName = "ResolveSkillImpact" },
                new AnimationEvent { time = impact, functionName = "InstantiateSkillVFXAtIndex", intParameter = 0 },
                new AnimationEvent { time = replacement.length - .04f, functionName = "HideSwordWhenComboEnds" }
            });
            var settings = AnimationUtility.GetAnimationClipSettings(replacement);
            settings.loopTime = false;
            settings.loopBlendOrientation = settings.loopBlendPositionXZ = settings.loopBlendPositionY = true;
            AnimationUtility.SetAnimationClipSettings(replacement, settings);
            // Replace the authored copy in place: Animator and saved references keep their GUID.
            if (clip == null)
            {
                AssetDatabase.CreateAsset(replacement, path);
                clip = replacement;
            }
            else
            {
                EditorUtility.CopySerialized(replacement, clip);
                EditorUtility.SetDirty(clip);
            }
            var importer = AssetImporter.GetAtPath(path);
            importer.userData = provenance;
            AssetDatabase.WriteImportSettingsIfDirty(path);
            if (skill.AnimationClip != clip) { skill.animationClip = clip; EditorUtility.SetDirty(skill); }
        }
        finally { if (!EditorUtility.IsPersistent(replacement)) UnityEngine.Object.DestroyImmediate(replacement); }
    }
}
