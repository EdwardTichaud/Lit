using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HealingSkillPlayValidation
{
    private const string Key = "Lit.HealingSkill.Validation";
    private static double started;
    static HealingSkillPlayValidation() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated project required.");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        EditorApplication.isPlaying = true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static SquadCharacterController Actor(Vector3 position, int hp, bool enemy = false)
    {
        var root = new GameObject("HealingValidationActor");
        root.transform.position = position;
        var controller = root.AddComponent<SquadCharacterController>();
        var data = ScriptableObject.CreateInstance<CharacterData>();
        data.isEnemy = enemy;
        typeof(SquadCharacterController).GetField("characterData", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, data);
        controller.SetHealth(hp, 200);
        return controller;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < 60, "Healing validation timeout");
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            var manager = arena.GetComponent<RealTimeCombatManager>();
            var revitalisation = Resources.Load<SkillSO>("CombatSupportSkills/Skill_Revitalisation");
            var emergency = Resources.Load<SkillSO>("CombatSupportSkills/Skill_SoinsUrgence");
            Require(revitalisation != null && emergency != null && revitalisation.cooldownSeconds == 120 &&
                emergency.cooldownSeconds == 30, "Skill assets invalid");
            manager.EndCombat();
            arena.Enemy.SetCinematicSuspended(true);
            var player = arena.Player;
            player.SetHealth(40, 100);
            var ally = Actor(player.transform.position + Vector3.right * 30, 20);
            var outside = Actor(player.transform.position + Vector3.right * 30.01f, 20);
            var dead = Actor(player.transform.position + Vector3.right * 5, 0);
            var enemy = Actor(player.transform.position + Vector3.right * 5, 20, true);
            var inactive = Actor(player.transform.position + Vector3.right * 5, 20);
            inactive.gameObject.SetActive(false);
            Require(manager.TryUseSkill(revitalisation), "Group heal failed without combat/lock");
            Require(player.CurrentHp == 60 && ally.CurrentHp == 60, "Percent of maximum or inclusive radius incorrect");
            Require(outside.CurrentHp == 20 && dead.CurrentHp == 0 && enemy.CurrentHp == 20 && inactive.CurrentHp == 20,
                "Invalid recipient healed");
            Require(manager.GetSkillCooldownRemaining(revitalisation) > 119 && !manager.TryUseSkill(revitalisation), "120s cooldown invalid");
            player.SetCurrentHp(40);
            Require(manager.TryUseSkill(emergency) && player.CurrentHp == 90 && ally.CurrentHp == 60, "Self heal affected another actor");
            Require(manager.GetSkillCooldownRemaining(emergency) > 29 && !manager.TryUseSkill(emergency), "30s cooldown invalid");
            var clone = UnityEngine.Object.Instantiate(emergency);
            player.SetCurrentHp(80);
            Require(manager.TryUseSkill(clone) && player.CurrentHp == 100, "Heal exceeded maximum or did not clamp");
            var full = UnityEngine.Object.Instantiate(emergency);
            Require(!manager.TryUseSkill(full) && manager.GetSkillCooldownRemaining(full) == 0, "Empty heal consumed cooldown");
            var bridge = player.GetComponent<LitOpsiveLocomotionBridge>();
            player.SetCurrentHp(40);
            Require(bridge.TryAcquireExternalLock(new object(), out var handle), "Lock acquisition failed");
            Require(!manager.TryUseSkill(full), "Healing bypassed cinematic lock");
            handle.Dispose();
            Require(manager.TryUseSkill(full), "Healing stayed locked after restitution");
            Require(SkillHealing.CalculateAmount(1, 3, 20) == 1 && SkillHealing.CalculateAmount(0, 100, 50) == 0,
                "Rounding/death guards invalid");
            manager.EndCombat();
            Require(manager.GetSkillCooldownRemaining(revitalisation) > 0, "Combat end cleared cooldown");
            Debug.Log("[HealingSkill] PLAY PASS | self 50%, group 20%, radius 30 inclusive, outside/dead/enemy/inactive excluded, HP cap, independent 30/120 cooldowns, no lock target, external lock protected");
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(1);
        }
    }
}
