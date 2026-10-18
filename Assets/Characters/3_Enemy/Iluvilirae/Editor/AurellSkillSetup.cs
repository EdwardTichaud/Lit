using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AurellSkillSetup
{
    public const string Folder = "Assets/Characters/1_Squad/Aurell";
    public const string DataPath = Folder + "/Aurell.asset";
    public const string PrefabPath = Folder + "/Aurell.prefab";
    public const string SkillFolder = "Assets/Resources/CombatSupportSkills";
    [MenuItem("Lit/Combat Lab/Configure Aurell Justice Skills")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring Aurell.");
        EnsureFolder(Folder); EnsureFolder(SkillFolder);
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.lucianPrefab == null) throw new InvalidOperationException("Laboratory player prefab missing.");
            var baseSkill = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>("Assets/CombatRealTime/Skills/BasicSkill_3.asset");
            var source = baseSkill.animationClip;
            string clipPath = Folder + "/JusteRetour_InPlace.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) == null) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), clipPath);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            AnimationUtility.SetAnimationEvents(clip, AnimationUtility.GetAnimationEvents(source).Where(e =>
                e.functionName == "ShowSword" || e.functionName == "ResolveSkillImpact" || e.functionName == "HideSwordWhenComboEnds").ToArray());
            EditorUtility.SetDirty(clip);
            var intercession = Create("Intercession", "Intercession", SkillProtectionKind.Intercession, 25);
            intercession.protection.radius = 20; intercession.protection.durationSeconds = 4;
            intercession.protection.maximumLinkDistance = 30; intercession.protection.reduction = .5f;
            intercession.protection.transferFraction = .75f;
            intercession.description = "Transfere 75% des degats d'un allie pendant 4 s. Aurell reduit ses degats de 50%.";
            var rempart = Create("Rempart", "Rempart", SkillProtectionKind.Rempart, 0);
            rempart.protection.radius = 6; rempart.protection.reduction = .3f;
            rempart.description = "Aura passive de 6 m : 30% de reduction. Une concentration unique, reutiliser pour desactiver.";
            var sommation = Create("Sommation", "Sommation", SkillProtectionKind.Sommation, 30);
            sommation.protection.radius = 15; sommation.protection.durationSeconds = 6;
            sommation.description = "Attire les prochaines attaques des ennemis a 15 m pendant 6 s, sans interrompre un coup engage.";
            var retour = Create("JusteRetour", "Juste Retour", SkillProtectionKind.JusteRetour, 10);
            retour.protection.reserveMaxHealthFraction = .5f; retour.protection.animationSlot = source;
            retour.animationClip = clip; retour.animatorState = baseSkill.animatorState;
            retour.minimumHitDistance = 0; retour.maximumHitDistance = 3;
            retour.useLaunchRange = true; retour.minimumLaunchDistance = 0; retour.maximumLaunchDistance = 3;
            retour.presentation = PlayerActionPresentationProfile.CreateDefault();
            retour.presentation.movementPolicy = PlayerActionMovementPolicy.VisualOnly;
            retour.presentation.entryBlendSeconds = .08f; retour.presentation.exitBlendSeconds = .08f;
            retour.description = "Depense la reserve de degats evites. Aucun degat de base. Reserve maximale : 50% des PV max.";
            var kit = new[] { intercession, rempart, sommation, retour };
            foreach (var skill in kit) SkillTargetingAuthoring.ConfigureIfMissing(skill);
            foreach (var skill in kit) EditorUtility.SetDirty(skill);
            if (AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath) == null)
                AssetDatabase.CopyAsset("Assets/Characters/1_Squad/Lucian/Lucian.asset", DataPath);
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath);
            data.characterName = "Aurell"; data.characterId = "Aurell"; data.hp = 100; data.isEnemy = false;
            data.combatSkills = new List<SkillSO>(kit); data.skills = new List<StatsSO>(); data.voiceLines.Clear();
            data.enemyCombatProfile = null; data.enableCombatHealthThresholds = false; data.combatHealthThresholdStages.Clear();
            data.groundBasicSkills = arena.basicSkills.Where(s => s != null).ToList();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(arena.lucianPrefab), PrefabPath);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                root.name = "Aurell";
                foreach (var info in root.GetComponentsInChildren<CharacterInfo>(true))
                {
                    var serialized = new SerializedObject(info);
                    serialized.FindProperty("characterData").objectReferenceValue = data; serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var actor in root.GetComponentsInChildren<SquadCharacterController>(true))
                {
                    var serialized = new SerializedObject(actor);
                    serialized.FindProperty("characterData").objectReferenceValue = data; serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                if (root.GetComponent<PlayerProtectionEffects>() == null) root.AddComponent<PlayerProtectionEffects>();
                if (root.GetComponent<PlayerJusticeStrike>() == null) root.AddComponent<PlayerJusticeStrike>();
                if (root.GetComponent<PlayerGuardState>() == null) root.AddComponent<PlayerGuardState>();
                if (root.GetComponent<NetworkCharacterInput>() == null) root.AddComponent<NetworkCharacterInput>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            data.worldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); EditorUtility.SetDirty(data);
            arena.aurellPrefab = data.worldPrefab; arena.useAurell = true; arena.spawnProtectionAlly = true;
            EditorUtility.SetDirty(arena); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (previous.Length > 0 && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }
    private static SkillSO Create(string asset, string label, SkillProtectionKind kind, float cooldown)
    {
        string path = SkillFolder + "/Skill_" + asset + ".asset";
        var skill = AssetDatabase.LoadAssetAtPath<SkillSO>(path);
        if (skill == null) { skill = ScriptableObject.CreateInstance<SkillSO>(); AssetDatabase.CreateAsset(skill, path); }
        skill.skillName = label; skill.cooldownSeconds = cooldown; skill.damages = 0;
        skill.protection.kind = kind; skill.clarityGainOnHit = 0; skill.impactFeedback.enabled = false;
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
        try { Configure(); Configure(); Debug.Log("[Aurell] AUTHORING PASS"); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
