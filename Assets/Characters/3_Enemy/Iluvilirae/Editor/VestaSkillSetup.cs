using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VestaSkillSetup
{
    public const string Folder = "Assets/Characters/1_Squad/Vesta";
    public const string DataPath = Folder + "/Vesta.asset";
    public const string PrefabPath = Folder + "/Vesta.prefab";
    public const string ControllerPath = Folder + "/Vesta_Model.controller";
    [MenuItem("Lit/Combat Lab/Configure Vesta Healing Skills")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring Vesta.");
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.lucianPrefab == null) throw new InvalidOperationException("Laboratory player missing.");
            var kit = new[] { "Revitalisation", "SoinsUrgence", "Regeneration", "SiphonVital" }
                .Select(name => Resources.Load<SkillSO>("CombatSupportSkills/Skill_" + name)).ToArray();
            if (kit.Any(skill => skill == null || !skill.IsHealingSkill)) throw new InvalidOperationException("Vesta healing kit incomplete.");
            foreach (var skill in kit) SkillTargetingAuthoring.ConfigureIfMissing(skill);
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath);
            if (data == null) throw new InvalidOperationException("Canonical Vesta CharacterData missing.");
            if (data.worldPrefab == null)
            {
                var portrait = data.portrait;
                string identity = data.UniqueId, characterId = data.characterId;
                EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/1_Squad/Lucian/Lucian.asset"), data);
                data.name = "Vesta"; data.portrait = portrait; data.characterId = characterId;
                var identityData = new SerializedObject(data);
                identityData.FindProperty("uniqueId").stringValue = identity; identityData.ApplyModifiedPropertiesWithoutUndo();
                data.hp = 100; data.playerSettings.equipment.equippedLightSkill = null;
                data.skills = new List<StatsSO>(); data.voiceLines.Clear();
                data.enemyCombatProfile = null; data.enableCombatHealthThresholds = false; data.combatHealthThresholdStages.Clear();
            }
            data.characterName = "Vesta"; data.isEnemy = false;
            data.combatSkills = new List<SkillSO>(kit);
            data.groundBasicSkills = arena.basicSkills.Where(skill => skill != null).ToList();
            var sourceController = arena.lucianPrefab.GetComponent<Animator>().runtimeAnimatorController;
            if (sourceController is AnimatorOverrideController overrides) sourceController = overrides.runtimeAnimatorController;
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null)
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceController), ControllerPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(arena.lucianPrefab), PrefabPath);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                root.name = "Vesta";
                root.GetComponent<Animator>().runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!(component is CharacterInfo) && !(component is SquadCharacterController)) continue;
                    var serialized = new SerializedObject(component);
                    serialized.FindProperty("characterData").objectReferenceValue = data;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                var presentation = root.GetComponent<PlayerActionPresentationController>() ?? root.AddComponent<PlayerActionPresentationController>();
                presentation.ResolveReferences(root.GetComponent<Animator>(), root.GetComponent<LitOpsiveLocomotionBridge>());
                if (root.GetComponent<PlayerRegenerationEffect>() == null) root.AddComponent<PlayerRegenerationEffect>();
                if (root.GetComponent<NetworkCharacterInput>() == null) root.AddComponent<NetworkCharacterInput>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            data.worldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); EditorUtility.SetDirty(data);
            var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
            var definition = new SerializedObject(catalog);
            definition.FindProperty("vestaCharacter").objectReferenceValue = data;
            var slots = definition.FindProperty("vesta"); slots.arraySize = 4;
            for (int i = 0; i < 4; i++) slots.GetArrayElementAtIndex(i).objectReferenceValue = kit[i];
            definition.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog);
            arena.vestaPrefab = data.worldPrefab; arena.character = CombatLabCharacter.Vesta;
            arena.spawnProtectionAlly = true;
            EditorUtility.SetDirty(arena); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        finally { if (previous.Length > 0 && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }
}
