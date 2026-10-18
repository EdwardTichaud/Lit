using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GroundImpactSkillSetup
{
    public const string SkillPath = "Assets/CombatRealTime/Skills/Skill_ImpactDeLumiere.asset";
    public const string Folder = "Assets/Characters/3_Enemy/Iluvilirae";
    public const string ControllerPath = Folder + "/Lucian_ImpactLab.controller";
    public const string PrefabPath = Folder + "/Lucian_ImpactLab.prefab";
    public const string DataPath = Folder + "/Lucian_ImpactLab.asset";
    [MenuItem("Lit/Combat Lab/Configure Impact de lumiere")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring.");
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            var rupture = AssetDatabase.LoadAssetAtPath<SkillSO>("Assets/CombatRealTime/Skills/Skill_4_JumpKick_Rupture.asset");
            var skill = AssetDatabase.LoadAssetAtPath<SkillSO>(SkillPath);
            if (skill == null) { skill = ScriptableObject.CreateInstance<SkillSO>(); AssetDatabase.CreateAsset(skill, SkillPath); }
            string clipPath = Folder + "/ImpactDeLumiere_InPlace.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) { clip = UnityEngine.Object.Instantiate(rupture.AnimationClip); clip.name = "ImpactDeLumiere_InPlace"; AssetDatabase.CreateAsset(clip, clipPath); }
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            var serializedClip = new SerializedObject(clip);
            foreach (var field in new[] { "m_LoopBlendOrientation", "m_LoopBlendPositionXZ", "m_LoopBlendPositionY" })
                serializedClip.FindProperty("m_AnimationClipSettings." + field).boolValue = true;
            serializedClip.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(clip);
            skill.skillName = "Impact de lumiere"; skill.animationClip = clip; skill.animatorState = "Base Layer.ImpactDeLumiere";
            skill.description = "Plonge vers le sol. Onde de 10 m de diametre, plus puissante depuis les airs. Peut interrompre Saut de rupture.";
            skill.damages = 20; skill.cooldownSeconds = 12; skill.icon = rupture.icon; skill.interruptionForce = 0;
            skill.groundImpact.enabled = true; skill.groundImpact.interruptibleSkill = rupture;
            skill.presentation.movementPolicy = PlayerActionMovementPolicy.VisualOnly;
            skill.presentation.lockMovementDuringAnimation = true;
            skill.presentation.recoveryNormalizedTime = 1; skill.presentation.allowMobilityCancel = false;
            skill.impactFeedback.enabled = false;
            SkillTargetingAuthoring.ConfigureIfMissing(skill);
            EditorUtility.SetDirty(skill);
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null)
            {
                var source = arena.lucianPrefab.GetComponent<Animator>().runtimeAnimatorController;
                if (source is AnimatorOverrideController overrides) source = overrides.runtimeAnimatorController;
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), ControllerPath);
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.FirstOrDefault(s => s.state.name == "ImpactDeLumiere").state;
            if (state == null) state = machine.AddState("ImpactDeLumiere");
            state.motion = clip; state.tag = "RealTimeCombatInPlace";
            EditorUtility.SetDirty(state); EditorUtility.SetDirty(controller);
            if (AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath) == null)
                AssetDatabase.CopyAsset("Assets/Characters/1_Squad/Lucian/Lucian.asset", DataPath);
            var character = AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath);
            if (!character.combatSkills.Contains(skill)) character.combatSkills.Add(skill);
            EditorUtility.SetDirty(character);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(arena.lucianPrefab), PrefabPath);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                root.GetComponent<Animator>().runtimeAnimatorController = controller;
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!(component is CharacterInfo) && !(component is SquadCharacterController)) continue;
                    var serialized = new SerializedObject(component);
                    serialized.FindProperty("characterData").objectReferenceValue = character;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            arena.lucianPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            character.worldPrefab = arena.lucianPrefab; EditorUtility.SetDirty(character);
            arena.character = CombatLabCharacter.Lucian;
            var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
            var data = new SerializedObject(catalog); data.FindProperty("lucian").GetArrayElementAtIndex(3).objectReferenceValue = skill;
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(arena); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        finally { if (previous.Length > 0 && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }
    public static void RunBatch()
    {
        try { Configure(); Debug.Log("[GroundImpact] AUTHORING PASS"); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
