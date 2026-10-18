using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class VestaPlayValidation
{
    private const string Key = "Lit.Vesta.Validation";
    private static int stage, enemyHealth;
    private static double started;
    private static float gameAt;
    static VestaPlayValidation() => EditorApplication.update += Tick;
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated editor required.");
        VestaSkillSetup.Configure(); VestaSkillSetup.Configure();
        SessionState.SetBool(Key, true); EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        EditorApplication.isPlaying = true;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Next(int value) { stage = value; gameAt = Time.time; }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < 45, "Vesta test timed out at " + stage);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena?.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            var player = arena.Player; var ally = arena.ProtectionAlly;
            var manager = arena.GetComponent<RealTimeCombatManager>();
            var kit = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default").GetSkills(CombatSkillKit.Vesta);
            var effect = player.GetComponent<PlayerRegenerationEffect>();
            var presentation = player.GetComponent<PlayerActionPresentationController>();
            if (stage == 0)
            {
                Require(arena.character == CombatLabCharacter.Vesta && player.CharacterData.characterName == "Vesta", "Wrong player");
                Require(player.CharacterData.combatSkills.SequenceEqual(kit) && kit.Length == 4, "Vesta kit mixed or missing");
                Require(player.CharacterData.playerSettings.equipment.equippedLightSkill == null, "Inherited Lucian LightSkill");
                Require(player.GetComponent<LitOpsiveLocomotionBridge>().IsDriving &&
                    player.GetComponent<PlayerScriptedDodgeController>() != null && player.GetComponent<CombatTimeDomain>() != null &&
                    player.GetComponent<NetworkCharacterInput>() != null && effect != null, "Playable stack incomplete");
                Require(ally != null && ally != player, "Healing ally missing");
                typeof(SquadCharacterController).GetField("enableIdleSitting", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, false);
                if (player.IsInSittingCycle) { player.TrySetSitting(false); return; }
                arena.Enemy.enabled = false; arena.Enemy.Animator.speed = 0;
                var agent = arena.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>(); agent.isStopped = true; agent.ResetPath();
                manager.EndCombat(); presentation.CancelAction();
                player.SetHealth(20, 100); ally.SetHealth(10, 100);
                Require(manager.TryUseSkill(kit[0]), "Revitalisation failed");
                Require(player.CurrentHp == 40 && ally.CurrentHp == 30, "Group heal not applied to both actors");
                Require(!manager.TryUseSkill(kit[0]), "Group cooldown missing");
                Require(manager.TryUseSkill(kit[1]), "Emergency heal failed");
                Require(player.CurrentHp == 90 && ally.CurrentHp == 30, "Emergency heal affected wrong actor");
                Require(!manager.TryUseSkill(kit[1]), "Emergency cooldown missing");
                Require(manager.SetSupportTarget(ally) && manager.TryUseSkill(kit[2]), "Explicit regeneration failed");
                Require(effect.Recipient == ally && !presentation.IsActionActive, "Wrong passive target or animation lock");
                Next(1);
            }
            else if (stage == 1 && Time.time - gameAt > 1.2f)
            {
                Require(ally.CurrentHp == 31 && player.CurrentHp == 90, "Regeneration must heal ally by 1 | ally=" + ally.CurrentHp +
                    " caster=" + player.CurrentHp + " recipient=" + effect.Recipient + " scale=" + Time.timeScale);
                manager.BeginExternalEncounter(player.transform, arena.Enemy);
                enemyHealth = arena.Enemy.CurrentHealth;
                Require(manager.TryUseSkill(kit[3]), "Siphon failed");
                Require(effect.Recipient == null && effect.EnemyRecipient?.Root == arena.Enemy.transform, "Passives did not replace");
                Next(2);
            }
            else if (stage == 2 && Time.time - gameAt > 1.2f)
            {
                Require(player.CurrentHp == 91 && arena.Enemy.CurrentHealth == enemyHealth - 1, "Actual siphon transfer incorrect");
                Require(ally.CurrentHp == 31, "Previous regeneration remained active");
                effect.Clear();
                Require(effect.Recipient == null && effect.EnemyRecipient == null &&
                    !player.GetComponent<LitOpsiveLocomotionBridge>().IsExternalLockActive, "Passive cleanup leaked");
                Debug.Log("[Vesta] PLAY PASS | distinct actor, kit4, UCC stack, group/self heals, cooldowns, explicit ally regeneration, siphon replacement and cleanup");
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
        }
        catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
    }
}
