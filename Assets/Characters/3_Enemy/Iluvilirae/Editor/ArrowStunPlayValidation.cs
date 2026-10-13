using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ArrowStunPlayValidation
{
    private const string Key = "Lit.ArrowStun.Validation";
    private static LucianBrainsCombatArena arena;
    private static SkillSO skill;
    private static int step, health;
    private static float stunStarted;
    private static double started;
    private static Transform otherActor;

    static ArrowStunPlayValidation() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch project required.");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        EditorApplication.isPlaying = true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < 90, "Arrow validation timeout, step=" + step);
            arena ??= UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            var manager = arena.GetComponent<RealTimeCombatManager>();
            var enemy = arena.Enemy;
            var rootField = typeof(RealTimeCombatManager).GetField("playerRoot", BindingFlags.NonPublic | BindingFlags.Instance);
            if (step == 0)
            {
                skill = AssetDatabase.LoadAssetAtPath<SkillSO>(AssetDatabase.GUIDToAssetPath("d8019012b40ae1b46af8bfcbec7cb88d"));
                Require(skill != null && skill.cooldownSeconds == 10 && skill.stunSeconds == 2, "Arrow settings invalid");
                otherActor = new GameObject("ArrowValidationOtherActor").transform;
                otherActor.position = enemy.transform.position + Vector3.one * 1000;
                enemy.detectionTargetOverride = otherActor;
                enemy.SetCinematicSuspended(true);
                Require(!enemy.TryApplySkillStun(2), "Cinematic priority lost");
                enemy.SetCinematicSuspended(false);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "ArrowValidationFloor";
                floor.transform.position = enemy.transform.position - Vector3.up * .1f;
                floor.transform.localScale = new Vector3(60, .2f, 60);
                Physics.SyncTransforms();
                var bridge = arena.Player.GetComponent<LitOpsiveLocomotionBridge>();
                Require(bridge.SetCinematicPositionAndRotation(enemy.transform.position - Vector3.forward * 15,
                    Quaternion.identity, true, false), "Player placement failed");
                health = enemy.CurrentHealth;
                step = 1;
                return;
            }
            if (step == 1)
            {
                Require(manager.TryUseSkill(skill), "Arrow launch rejected");
                Require(manager.GetSkillCooldownRemaining(skill) > 9.5f, "Cooldown did not start");
                Require(!manager.TryUseSkill(skill), "Repeated arrow launch accepted");
                Transform original = (Transform)rootField.GetValue(manager);
                rootField.SetValue(manager, otherActor);
                Require(manager.GetSkillCooldownRemaining(skill) == 0, "Cooldown shared between actors");
                rootField.SetValue(manager, original);
                step = 2;
            }
            if (step == 2 && enemy.CurrentHealth < health)
            {
                Require(enemy.IsCounterStunned, "Arrow damage did not stun immediately");
                Require(enemy.AttackPhase == LitBrainsAttackPhase.None, "Attack survived arrow stun");
                Require(!enemy.TryApplySkillStun(2), "Repeated stun extended the duration");
                stunStarted = Time.time;
                step = 3;
            }
            if (step == 3)
            {
                float elapsed = Time.time - stunStarted;
                if (elapsed < 1.8f) Require(enemy.IsCounterStunned, "Stun ended early");
                if (elapsed < 2.2f) return;
                Require(!enemy.IsCounterStunned, "Stun never released");
                Require(health - enemy.CurrentHealth == Mathf.RoundToInt(skill.Damages), "Duplicate arrow damage");
                // A committed action is explicitly interruptible, and late motion is cancelled.
                typeof(LitBrainsEnemy).GetField("<AttackPhase>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(enemy, LitBrainsAttackPhase.Strike);
                typeof(LitBrainsEnemy).GetField("advanceRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, 5f);
                Require(enemy.TryApplySkillStun(2), "Committed attack could not be stunned");
                Require(enemy.AttackPhase == LitBrainsAttackPhase.None &&
                    (float)typeof(LitBrainsEnemy).GetField("advanceRemaining", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(enemy) == 0,
                    "Interrupted attack retained motion");
                manager.EndCombat();
                Require(manager.GetSkillCooldownRemaining(skill) > 0, "End combat reset cooldown");
                manager.BeginExternalEncounter(arena.Player.transform, enemy);
                Require(!manager.TryUseSkill(skill), "Re-engagement reset cooldown");
                step = 4;
            }
            if (step == 4 && manager.GetSkillCooldownRemaining(skill) <= 0)
            {
                Require(manager.TryUseSkill(skill), "Arrow unavailable after cooldown expired");
                arena.Player.GetComponent<PlayerActionPresentationController>().CancelAction();
                enemy.ReceiveImpact(new CombatImpact(int.MaxValue, 0, CombatImpactOrigin.Basic));
                Require(enemy.IsDead && !enemy.TryApplySkillStun(2), "Death priority lost");
                Debug.Log("[ArrowStun] PLAY PASS | actual clip impact, 2s stun, 10s actor cooldown, repeat rejected, cinematic protected, action cancelled, combat end preserved");
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(1);
        }
    }
}
