using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Editor;
using UnityEditor.Animations;
using System.Linq;

public static class CombatSkillKitSetup
{
    [MenuItem("Lit/Input/Install Directional Skill Kits")]
    public static void Install()
    {
        const string folder = "Assets/Resources/CombatSkillKits";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Resources", "CombatSkillKits");
        var catalog = AssetDatabase.LoadAssetAtPath<CombatSkillKitCatalog>(folder + "/Default.asset");
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<CombatSkillKitCatalog>();
            AssetDatabase.CreateAsset(catalog, folder + "/Default.asset");
        }
        var serialized = new SerializedObject(catalog);
        serialized.FindProperty("lucianCharacter").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/1_Squad/Lucian/Lucian.asset");
        serialized.FindProperty("vestaCharacter").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/1_Squad/Vesta/Vesta.asset");
        serialized.FindProperty("sorenCharacter").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/1_Squad/Soren/Soren.asset");
        serialized.FindProperty("aurellCharacter").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/1_Squad/Aurell/Aurell.asset");
        Set(serialized, "lucian", "Assets/CombatRealTime/Skills/Skill_BondOffensif.asset", "Assets/CombatRealTime/Skills/Skill_1_Eclair.asset", "Assets/CombatRealTime/Skills/Skill_4_JumpKick_Rupture.asset", null);
        Set(serialized, "vesta", "Assets/Resources/CombatSupportSkills/Skill_Revitalisation.asset", "Assets/Resources/CombatSupportSkills/Skill_SoinsUrgence.asset", "Assets/Resources/CombatSupportSkills/Skill_Regeneration.asset", "Assets/Resources/CombatSupportSkills/Skill_SiphonVital.asset");
        Set(serialized, "soren", "Assets/CombatRealTime/Skills/Skill_2_Fleche de lumière.asset", "Assets/Resources/CombatControlSkills/Skill_Revelation.asset", "Assets/Resources/CombatControlSkills/Skill_Entrave.asset", "Assets/Resources/CombatControlSkills/Skill_PasDeRecul.asset");
        Set(serialized, "aurell", "Assets/Resources/CombatSupportSkills/Skill_Intercession.asset", "Assets/Resources/CombatSupportSkills/Skill_Rempart.asset", "Assets/Resources/CombatSupportSkills/Skill_Sommation.asset", "Assets/Resources/CombatSupportSkills/Skill_JusteRetour.asset");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        foreach (CombatSkillKit kit in Enum.GetValues(typeof(CombatSkillKit)))
            foreach (var skill in catalog.GetSkills(kit))
                if (skill != null && !skill.IsHealingSkill && !skill.IsControlSkill && !skill.IsProtectionSkill && skill.AnimationClip == null ||
                    skill != null && skill.IsProtectionSkill && skill.protection.kind == SkillProtectionKind.JusteRetour && skill.AnimationClip == null)
                    throw new InvalidOperationException("Missing authored animation for " + skill.SkillName);
        // Borrowed Soren skills execute on Lucian's real Animator, not a hidden
        // Soren actor. Add only the authored retreat state it currently lacks.
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Characters/4_Animations/Player_Model.controller");
        var retreat = catalog.GetSkill(CombatSkillKit.Soren, 3);
        if (controller == null || retreat == null || retreat.AnimationClip == null) throw new InvalidOperationException("Retreat animation/controller missing.");
        var machine = controller.layers[0].stateMachine;
        var state = machine.states.FirstOrDefault(entry => entry.state.name == "Soren_PasDeRecul").state;
        if (state == null) state = machine.AddState("Soren_PasDeRecul");
        state.motion = retreat.AnimationClip;
        state.writeDefaultValues = false;
        state.tag = "RealTimeCombatInPlace";
        state.speed = retreat.AnimationClip.length / Mathf.Max(.01f, retreat.control.durationSeconds);
        var idle = machine.states.FirstOrDefault(entry => entry.state.name == "Locomotion").state;
        if (idle != null && state.transitions.Length == 0)
        {
            var exit = state.AddTransition(idle);
            exit.hasExitTime = true;
            exit.exitTime = 1;
            exit.duration = .08f;
        }
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(catalog);
        var lucian = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/1_Squad/Lucian/Lucian.asset");
        if (lucian == null) throw new InvalidOperationException("Lucian CharacterData missing.");
        lucian.combatSkills.Clear();
        foreach (var skill in catalog.GetSkills(CombatSkillKit.Lucian)) if (skill != null) lucian.combatSkills.Add(skill);
        EditorUtility.SetDirty(lucian);
        AssetDatabase.SaveAssets();
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/PlayerInputs.inputactions");
        InputActionCodeGenerator.GenerateWrapperCode("Assets/PlayerInputs.cs", asset,
            new InputActionCodeGenerator.Options { className = "PlayerInputs", sourceAssetPath = "Assets/PlayerInputs.inputactions" });
        AssetDatabase.Refresh();
        Debug.Log("[SkillKits] Catalog installed: 3 Lucian + 4 Vesta + 4 Soren + 4 Aurell; wrapper regenerated.");
    }

    private static void Set(SerializedObject data, string field, params string[] paths)
    {
        var array = data.FindProperty(field);
        array.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            var skill = paths[i] == null ? null : AssetDatabase.LoadAssetAtPath<SkillSO>(paths[i]);
            if (paths[i] != null && skill == null) throw new InvalidOperationException("Missing skill: " + paths[i]);
            array.GetArrayElementAtIndex(i).objectReferenceValue = skill;
        }
    }
}
