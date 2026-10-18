using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicit one-off authoring migration. Runtime never guesses a skill's mode.</summary>
public static class SkillTargetingAuthoring
{
    [Serializable] public sealed class Entry { public string path; public int mode; }
    [Serializable] public sealed class Report { public List<Entry> entries = new List<Entry>(); }

    public static HashSet<SkillSO> Collect(LucianBrainsCombatArena arena)
    {
        var skills = new HashSet<SkillSO>();
        void Add(SkillSO skill) { if (skill != null) skills.Add(skill); }
        foreach (var skill in arena.basicSkills ?? Array.Empty<BasicSkillsSO>()) Add(skill);
        foreach (var skill in arena.additionalCombatSkills ?? Array.Empty<SkillSO>()) Add(skill);
        var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        foreach (CombatSkillKit kit in Enum.GetValues(typeof(CombatSkillKit)))
        {
            foreach (var skill in catalog?.GetSkills(kit) ?? Array.Empty<SkillSO>()) Add(skill);
            var character = catalog?.GetCharacter(kit);
            if (character != null) { Add(character.chargedSkill); foreach (var skill in character.combatSkills) Add(skill); }
        }
        foreach (var guid in AssetDatabase.FindAssets("t:BasicSkillsSO", new[] { "Assets/CombatRealTime/Skills", IluviliraeSetup.Folder }))
            Add(AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(AssetDatabase.GUIDToAssetPath(guid)));
        foreach (var prefab in new[] { arena.lucianPrefab, arena.aurellPrefab, arena.vestaPrefab, arena.sorenPrefab })
        {
            var data = prefab != null ? prefab.GetComponent<CharacterInfo>()?.SourceData : null;
            if (data == null) continue;
            Add(data.chargedSkill);
            foreach (var skill in data.combatSkills) Add(skill);
        }
        return skills;
    }

    public static void ConfigureIfMissing(SkillSO skill)
    {
        if (skill == null || skill.targetingMode != SkillTargetingMode.Unconfigured) return;
        if (skill is BasicSkillsSO || skill is ChargedSkillSO) skill.targetingMode = SkillTargetingMode.FreeAttack;
        else if (skill.IsGroundImpactSkill) skill.targetingMode = SkillTargetingMode.CasterArea;
        else if (skill.IsHealingSkill) skill.targetingMode = skill.healingTarget switch {
            SkillHealingTarget.Self => SkillTargetingMode.Self,
            SkillHealingTarget.PlayersInRadius => SkillTargetingMode.AllyArea,
            SkillHealingTarget.AllyRegeneration => SkillTargetingMode.SelectedAlly,
            _ => SkillTargetingMode.RequiredTarget };
        else if (skill.IsProtectionSkill) skill.targetingMode = skill.protection.kind switch {
            SkillProtectionKind.Intercession => SkillTargetingMode.SelectedAlly,
            SkillProtectionKind.Rempart => SkillTargetingMode.AllyArea,
            SkillProtectionKind.Sommation => SkillTargetingMode.CasterArea,
            _ => SkillTargetingMode.RequiredTarget };
        else if (skill.IsControlSkill) skill.targetingMode = skill.control.kind == SkillControlKind.PasDeRecul ? SkillTargetingMode.FreeAttack : SkillTargetingMode.RequiredTarget;
        else if (skill.HasCombatCinematic) skill.targetingMode = SkillTargetingMode.RequiredTarget;
        else if (skill.TargetLunge.enabled && skill.TargetLunge.resolveImpactAtContact) skill.targetingMode = SkillTargetingMode.OptionalTarget;
        else if (AssetDatabase.GetAssetPath(skill) == "Assets/CombatRealTime/Skills/Skill_4_JumpKick_Rupture.asset") skill.targetingMode = SkillTargetingMode.LockedTarget;
        else skill.targetingMode = SkillTargetingMode.RequiredTarget;
        EditorUtility.SetDirty(skill);
    }

    public static void MigrateLoadedLab(LucianBrainsCombatArena arena)
    {
        foreach (var skill in Collect(arena)) ConfigureIfMissing(skill);
    }

    [MenuItem("Lit/Combat Lab/Validate Skill Targeting")]
    public static void Validate()
    {
        var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
        if (arena == null) throw new InvalidOperationException("Ouvrez Iluvilirae_Test pour valider ses skills.");
        var errors = Collect(arena).Where(skill => !SkillTargetingContract.Validate(skill, out _))
            .Select(skill => { SkillTargetingContract.Validate(skill, out var reason); return reason; }).ToArray();
        if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
        Debug.Log("[SkillTargeting] Contrats du laboratoire valides : " + Collect(arena).Count, arena);
    }

    [MenuItem("Lit/Combat Lab/Migrate Skill Targeting")]
    public static void Migrate()
    {
        var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
        if (arena == null) throw new InvalidOperationException("Ouvrez Iluvilirae_Test avant la migration.");
        MigrateLoadedLab(arena);
        AssetDatabase.SaveAssets();
        Validate();
        var report = new Report();
        foreach (var skill in Collect(arena).OrderBy(AssetDatabase.GetAssetPath))
            report.entries.Add(new Entry { path = AssetDatabase.GetAssetPath(skill), mode = (int)skill.targetingMode });
        Directory.CreateDirectory("Library/SkillTargetingMigration");
        File.WriteAllText("Library/SkillTargetingMigration/report.json", JsonUtility.ToJson(report, true));
    }

    public static void MigrateBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch editor only.");
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        Migrate();
    }
}
