using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LucianBorrowedArrowStunValidation
{
    private const string Key = "Lit.LucianBorrowedArrowStun.Validation";
    private static int stage, enemyHealth;
    private static double started;
    private static float at, hitAt;
    private static Vector3 hitPosition;
    private static bool sawProjectileRise, sawProjectileDescent;
    static LucianBorrowedArrowStunValidation() => EditorApplication.update += Tick;
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch Editor.");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        EditorApplication.isPlaying = true;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < 60, "Arrow stun test timeout");
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            var actor = arena.Player;
            var enemy = arena.Enemy;
            var manager = arena.GetComponent<RealTimeCombatManager>();
            var skill = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default").GetSkill(CombatSkillKit.Soren, 0);
            if (stage == 2 || stage == 3)
            {
                var cue = System.Linq.Enumerable.First(skill.VfxCues, entry => entry.delivery == SkillVfxDelivery.ProjectileFromPlayerHand);
                foreach (var candidate in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
                {
                    if (candidate.parent != null || candidate.name != cue.prefab.name + "(Clone)") continue;
                    if (candidate.forward.y > .05f) sawProjectileRise = true;
                    if (candidate.forward.y < -.05f) sawProjectileDescent = true;
                }
            }
            if (stage == 0)
            {
                typeof(SquadCharacterController).GetField("enableIdleSitting", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor, false);
                if (actor.IsInSittingCycle) { actor.TrySetSitting(false); return; }
                Require(actor.CharacterData.characterName == "Lucian" && skill.stunSeconds == 2, "Lucian/arrow profile incorrect");
                Require(skill.MinimumHitDistance == 0 && skill.MaximumHitDistance == 30 &&
                    skill.IsWithinHitRange(0) && skill.IsWithinHitRange(3) && skill.IsWithinHitRange(30) && !skill.IsWithinHitRange(30.01f),
                    "Arrow must have no minimum distance and retain its 30m maximum");
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.position = enemy.transform.position - Vector3.up * .15f;
                floor.transform.localScale = new Vector3(60, .3f, 60);
                enemy.SetCinematicSuspended(true);
                enemy.detectionTargetOverride = new GameObject("ArrowValidationFarTarget").transform;
                enemy.detectionTargetOverride.position = enemy.transform.position + Vector3.one * 1000;
                enemy.SetCinematicSuspended(false);
                actor.GetComponent<LitOpsiveLocomotionBridge>().TeleportForSceneTransition(enemy.transform.position - Vector3.forward * 3, Quaternion.identity);
                actor.SetHealth(200, 200);
                enemyHealth = enemy.CurrentHealth; at = Time.time; stage = 1; return;
            }
            if (stage == 1)
            {
                if (Time.time - at < .2f) return;
                UnityEngine.Object.FindAnyObjectByType<SkillsManager>().SetAnimationEventSkill(skill);
                Require(manager.TryUseSkill(skill), "Borrowed arrow rejected");
                Require(!enemy.IsCounterStunned, "Arrow stunned at launch instead of impact");
                at = Time.time; stage = 2;
            }
            if (stage == 2)
            {
                Require(Time.time - at < 5, "Arrow impact missing");
                if (enemy.CurrentHealth == enemyHealth)
                { Require(!enemy.IsCounterStunned, "Arrow stunned before impact"); return; }
                Require(enemy.IsCounterStunned && enemy.AttackPhase == LitBrainsAttackPhase.None, "Arrow impact did not immobilize enemy");
                hitAt = Time.time; hitPosition = enemy.transform.position; stage = 3;
            }
            if (stage == 3)
            {
                float elapsed = Time.time - hitAt;
                Require(enemyHealth - enemy.CurrentHealth == Mathf.RoundToInt(skill.Damages), "Arrow damaged twice");
                if (elapsed < 1.9f)
                    Require(enemy.IsCounterStunned && Vector3.Distance(hitPosition, enemy.transform.position) < .15f, "Enemy recovered or moved before two seconds");
                if (elapsed < 2.15f) return;
                Require(!enemy.IsCounterStunned, "Arrow stun lasted longer than two seconds");
                Require(sawProjectileRise && sawProjectileDescent, "Live arrow did not follow ascending/descending arc tangents");
                Debug.Log("[ArrowStun] PLAY PASS | live parabolic projectile rises then descends, 3m hit, no minimum range, max 30m, unique damage and two-second stun");
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
        }
        catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
    }
}
