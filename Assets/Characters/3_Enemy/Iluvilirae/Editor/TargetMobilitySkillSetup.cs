using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TargetMobilitySkillSetup
{
    public const string EclairPath = "Assets/CombatRealTime/Skills/Skill_1_Eclair.asset";
    public const string BondPath = "Assets/CombatRealTime/Skills/Skill_BondOffensif.asset";
    private const string Folder = "Assets/CombatRealTime/Skills/TargetMobility";
    private const string SourceClip = "Assets/Characters/1_Squad/Lucian/Animation/Skill_1_Eclair.anim";

    [MenuItem("Lit/Combat Lab/Configure Eclair And Bond Offensif")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring.");
        var eclair = AssetDatabase.LoadAssetAtPath<SkillSO>(EclairPath);
        if (eclair == null) throw new InvalidOperationException("Eclair is missing.");
        bool newBond = AssetDatabase.LoadAssetAtPath<SkillSO>(BondPath) == null;
        if (newBond) AssetDatabase.CopyAsset(EclairPath, BondPath);
        var bond = AssetDatabase.LoadAssetAtPath<SkillSO>(BondPath);
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/CombatRealTime/Skills", "TargetMobility");
        var original = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClip);
        string clipPath = Folder + "/Eclair_Contact.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) { clip = UnityEngine.Object.Instantiate(original); clip.name = "Eclair_Contact"; AssetDatabase.CreateAsset(clip, clipPath); }
        var events = AnimationUtility.GetAnimationEvents(clip).Where(e => e.functionName != "HitEnemy" && e.functionName != "ResolveSkillImpact" && e.functionName != "OpenSkillContactWindow");
        AnimationUtility.SetAnimationEvents(clip, events.Concat(new[] {
            new AnimationEvent { functionName = "OpenSkillContactWindow", time = .08f }
        }).OrderBy(e => e.time).ToArray());
        foreach (var skill in new[] { eclair, bond })
        {
            skill.animationClip = clip;
            skill.animatorState = "Base Layer.Skill_1_Eclair";
            skill.useLaunchRange = true;
            skill.minimumLaunchDistance = skill == bond ? 2 : 0;
            skill.maximumLaunchDistance = skill == bond ? 30 : 10;
            skill.presentation.movementPolicy = PlayerActionMovementPolicy.ExistingScripted;
            skill.targetLunge.enabled = true;
            skill.targetLunge.resolveImpactAtContact = true;
            skill.targetLunge.contactAnimationSlot = original;
            skill.targetLunge.passThroughTarget = skill == eclair;
            skill.targetLunge.passThroughDistance = 4;
            skill.targetLunge.reboundHorizontalImpulse = skill.targetLunge.reboundVerticalImpulse = 0;
            SkillTargetingAuthoring.ConfigureIfMissing(skill);
            EditorUtility.SetDirty(skill);
        }
        bond.skillName = "Bond offensif";
        if (newBond)
        {
            bond.stunSeconds = 0f;
            bond.targetLunge.holdControlsUntilAnimationEnd = false;
            bond.presentation.recoveryNormalizedTime = .88f;
            bond.presentation.allowMobilityCancel = true;
            bond.presentation.allowDodgeAfterRecovery = true;
        }
        eclair.stunSeconds = 1f;
        eclair.targetLunge.holdControlsUntilAnimationEnd = true;
        eclair.presentation.recoveryNormalizedTime = 1f;
        eclair.presentation.allowMobilityCancel = false;
        eclair.presentation.allowDodgeAfterRecovery = false;
        bond.name = "Skill_BondOffensif";
        EditorUtility.SetDirty(clip);
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            var extras = (arena.additionalCombatSkills ?? Array.Empty<SkillSO>()).ToList();
            extras.RemoveAll(skill => skill == null);
            if (!extras.Contains(bond)) extras.Add(bond);
            arena.additionalCombatSkills = extras.ToArray();
            EditorUtility.SetDirty(arena);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (previous.Length > 0 && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }
    public static void RunBatch()
    {
        try { Configure(); Configure(); Debug.Log("[TargetMobility] Authoring PASS"); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
