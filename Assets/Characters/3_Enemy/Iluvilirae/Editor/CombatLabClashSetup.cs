using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CombatLabClashSetup
{
    private const string Folder = IluviliraeSetup.Folder + "/Clash";
    [MenuItem("Lit/Combat Lab/Configure Attack Clash")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring clash clips.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(IluviliraeSetup.Folder, "Clash");
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            var clash = arena.GetComponent<CombatLabClashController>() ?? arena.gameObject.AddComponent<CombatLabClashController>();
            if (clash.authoringVersion == 0)
            {
                clash.playerCenter = clash.enemyCenter = new Vector3(0, 1.1f, .8f);
                clash.authoringVersion = 1;
            }
            var oldMappings = clash.playerClips ?? Array.Empty<CombatLabClashController.ClipReplacement>();
            var playerMappings = new System.Collections.Generic.List<CombatLabClashController.ClipReplacement>();
            for (int i = 0; i < arena.basicSkills.Length; i++)
            {
                var skill = arena.basicSkills[i];
                if (skill.Context != BasicSkillContext.Grounded) continue;
                var source = oldMappings.FirstOrDefault(m => m.replacement == skill.AnimationClip).source ?? skill.AnimationClip;
                var copy = CopyClip(source, "PlayerBasic_" + i, ImpactTime(source));
                string skillPath = Folder + "/ClashBasic_" + i + ".asset";
                if (AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(skillPath) == null)
                    AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(skill), skillPath);
                skill = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(skillPath);
                skill.animationClip = copy;
                arena.basicSkills[i] = skill;
                EditorUtility.SetDirty(skill);
                playerMappings.Add(new CombatLabClashController.ClipReplacement { source = source, replacement = copy });
            }
            clash.playerClips = playerMappings.ToArray();
            clash.compatiblePlayerSkills = arena.basicSkills.Where(s => s.Context == BasicSkillContext.Grounded).ToArray();
            string profilePath = Folder + "/CombatLab_ClashEnemyProfile.asset";
            var original = arena.enemyPrefab.GetComponent<LitBrainsEnemy>().combatProfile;
            if (AssetDatabase.LoadAssetAtPath<LitBrainsCombatProfile>(profilePath) == null)
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original), profilePath);
            clash.labEnemyProfile = AssetDatabase.LoadAssetAtPath<LitBrainsCombatProfile>(profilePath);
            var enemyMappings = new System.Collections.Generic.List<CombatLabClashController.ClipReplacement>();
            foreach (var attack in clash.labEnemyProfile.attacks)
            {
                if (!clash.compatibleEnemyStates.Contains(attack.stateName)) continue;
                var source = original.attacks.First(a => a.stateName == attack.stateName).clip;
                attack.clip = CopyClip(source, "Enemy_" + attack.stateName, attack.impactSeconds);
                enemyMappings.Add(new CombatLabClashController.ClipReplacement { source = source, replacement = attack.clip });
            }
            clash.enemyClips = enemyMappings.ToArray();
            if (clash.hitVfx == null) clash.hitVfx = AssetDatabase.LoadAssetAtPath<GameObject>(IluviliraeSetup.Folder + "/CombatLab_CounterHitVfx.prefab");
            if (clash.contactAudio == null) clash.contactAudio = AssetDatabase.LoadAssetAtPath<AudioClipSO>("Assets/Audio/AudioClips/ActionBindings/SFX_Combat_GuardedImpact.asset");
            EditorUtility.SetDirty(clash.labEnemyProfile);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (previous.Length > 0 && previous.All(s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }
    private static float ImpactTime(AnimationClip clip)
    {
        var events = AnimationUtility.GetAnimationEvents(clip).Where(e => e.functionName.Contains("Impact") || e.functionName == "HitEnemy").ToArray();
        if (events.Length == 0) throw new InvalidOperationException("No authored impact on " + clip.name);
        return events.Min(e => e.time);
    }
    private static AnimationClip CopyClip(AnimationClip source, string name, float impact)
    {
        string path = Folder + "/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = UnityEngine.Object.Instantiate(source); clip.name = name; AssetDatabase.CreateAsset(clip, path); }
        var events = AnimationUtility.GetAnimationEvents(clip).Where(e => e.functionName != "OpenAttackClashWindow" && e.functionName != "CloseAttackClashWindow");
        AnimationUtility.SetAnimationEvents(clip, events.Concat(new[] {
            new AnimationEvent { functionName = "OpenAttackClashWindow", time = Mathf.Max(0, impact - .08f) },
            new AnimationEvent { functionName = "CloseAttackClashWindow", time = Mathf.Min(clip.length, impact + .06f) }
        }).OrderBy(e => e.time).ToArray());
        EditorUtility.SetDirty(clip);
        return clip;
    }
    public static void RunBatch()
    {
        try { Configure(); Configure(); Debug.Log("[CombatClash] Authoring PASS"); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
