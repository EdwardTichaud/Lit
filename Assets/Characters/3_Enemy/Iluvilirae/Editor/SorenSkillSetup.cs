using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SorenSkillSetup
{
    public const string Folder = "Assets/Characters/1_Squad/Soren";
    public const string SkillFolder = "Assets/Resources/CombatControlSkills";
    public const string DataPath = Folder + "/Soren.asset";
    public const string PrefabPath = Folder + "/Soren.prefab";
    [MenuItem("Lit/Combat Lab/Configure Soren Lucidity Skills")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring Soren.");
        EnsureFolder(Folder); EnsureFolder(SkillFolder);
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.lucianPrefab == null) throw new InvalidOperationException("Laboratory player missing.");
            var arrow = AssetDatabase.LoadAssetAtPath<SkillSO>("Assets/CombatRealTime/Skills/Skill_2_Fleche de lumière.asset");
            if (arrow == null) throw new InvalidOperationException("Existing light arrow missing.");
            var revelation = Skill("Revelation", "Révélation", SkillControlKind.Revelation, 20);
            revelation.control.range = 25; revelation.control.durationSeconds = 6; revelation.control.strength = .2f;
            revelation.description = "Marque la cible pendant 6 s : +20% de degats d'attaques joueurs. Portee 25 m.";
            var entrave = Skill("Entrave", "Entrave", SkillControlKind.Entrave, 25);
            entrave.control.range = 20; entrave.control.radius = 4; entrave.control.durationSeconds = 5; entrave.control.strength = .4f;
            entrave.description = "Zone fixe de 4 m sous la cible : navigation ennemie ralentie de 40% pendant 5 s. Portee 20 m.";
            var retreat = Skill("PasDeRecul", "Pas de recul", SkillControlKind.PasDeRecul, 8);
            retreat.control.distance = 5; retreat.control.durationSeconds = .25f;
            retreat.animatorState = "Base Layer.Soren_PasDeRecul";
            retreat.description = "Deplacement directionnel de 5 m en 0,25 s. Respecte obstacles et vide. Aucune invulnerabilite.";
            string clipPath = Folder + "/PasDeRecul_InPlace.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                var original = AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/1_Squad/Lucian/Animation/Twinblades_Dodge_F_Inplace.FBX")
                    .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
                clip = UnityEngine.Object.Instantiate(original); clip.name = "PasDeRecul_InPlace"; AssetDatabase.CreateAsset(clip, clipPath);
            }
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
            var clipSerialized = new SerializedObject(clip);
            foreach (var field in new[] { "m_LoopBlendOrientation", "m_LoopBlendPositionXZ", "m_LoopBlendPositionY" })
                clipSerialized.FindProperty("m_AnimationClipSettings." + field).boolValue = true;
            clipSerialized.ApplyModifiedPropertiesWithoutUndo();
            retreat.animationClip = clip;
            string controllerPath = Folder + "/Soren_Model.controller";
            var sourceController = arena.lucianPrefab.GetComponent<Animator>().runtimeAnimatorController;
            if (sourceController is AnimatorOverrideController overrides) sourceController = overrides.runtimeAnimatorController;
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) == null)
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceController), controllerPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.FirstOrDefault(s => s.state.name == "Soren_PasDeRecul").state;
            if (state == null) state = machine.AddState("Soren_PasDeRecul");
            state.motion = clip; state.tag = "RealTimeCombatInPlace"; state.speed = clip.length / retreat.control.durationSeconds;
            EditorUtility.SetDirty(state); EditorUtility.SetDirty(controller);
            var kit = new[] { arrow, revelation, entrave, retreat };
            foreach (var skill in new[] { revelation, entrave, retreat }) EditorUtility.SetDirty(skill);
            if (AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath) == null) AssetDatabase.CopyAsset("Assets/Characters/1_Squad/Lucian/Lucian.asset", DataPath);
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath);
            data.characterName = "Soren"; data.characterId = "Soren"; data.hp = 100; data.isEnemy = false;
            data.combatSkills = new List<SkillSO>(kit); data.skills.Clear(); data.voiceLines.Clear();
            data.enemyCombatProfile = null; data.enableCombatHealthThresholds = false; data.combatHealthThresholdStages.Clear();
            data.groundBasicSkills = arena.basicSkills.Where(s => s != null).ToList();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(arena.lucianPrefab), PrefabPath);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                root.name = "Soren"; root.GetComponent<Animator>().runtimeAnimatorController = controller;
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!(component is CharacterInfo) && !(component is SquadCharacterController)) continue;
                    var serialized = new SerializedObject(component);
                    serialized.FindProperty("characterData").objectReferenceValue = data; serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                if (root.GetComponent<PlayerControlSkills>() == null) root.AddComponent<PlayerControlSkills>();
                var presentation = root.GetComponent<PlayerActionPresentationController>();
                if (presentation == null) presentation = root.AddComponent<PlayerActionPresentationController>();
                presentation.ResolveReferences(root.GetComponent<Animator>(), root.GetComponent<LitOpsiveLocomotionBridge>());
                if (root.GetComponent<NetworkCharacterInput>() == null) root.AddComponent<NetworkCharacterInput>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            data.worldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); EditorUtility.SetDirty(data);
            arena.sorenPrefab = data.worldPrefab; arena.character = CombatLabCharacter.Soren;
            var arenaSerialized = new SerializedObject(arena); arenaSerialized.FindProperty("migrateAurellMode").boolValue = false; arenaSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(arena); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        finally { if (previous.Length > 0 && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }
    private static SkillSO Skill(string name, string label, SkillControlKind kind, float cooldown)
    {
        string path = SkillFolder + "/Skill_" + name + ".asset";
        var skill = AssetDatabase.LoadAssetAtPath<SkillSO>(path);
        if (skill == null) { skill = ScriptableObject.CreateInstance<SkillSO>(); AssetDatabase.CreateAsset(skill, path); }
        skill.skillName = label; skill.control.kind = kind; skill.cooldownSeconds = cooldown;
        skill.damages = 0; skill.clarityGainOnHit = 0; skill.impactFeedback.enabled = false;
        return skill;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
    public static void RunBatch()
    {
        try { Configure(); Configure(); Debug.Log("[Soren] AUTHORING PASS"); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
