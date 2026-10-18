using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class GroundImpactPlayValidation
{
    private const string Key = "Lit.GroundImpact.Validation";
    private static int stage, before, generation;
    private static double started, at;
    private static Vector3 enemyPosition;
    private static LitBrainsEnemy nearby, outside;
    private static int nearbyBefore, outsideBefore;
    private static float ruptureStartY, rupturePeakY;
    static GroundImpactPlayValidation() => EditorApplication.update += Tick;
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated editor only.");
        GroundImpactSkillSetup.Configure();
        SessionState.SetBool(Key, true); EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        EditorApplication.isPlaying = true;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Next(int value) { stage = value; at = EditorApplication.timeSinceStartup; }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < 65, "Timeout stage " + stage);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena?.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            var player = arena.Player;
            var bridge = player.GetComponent<LitOpsiveLocomotionBridge>();
            var action = player.GetComponent<PlayerActionPresentationController>();
            var manager = arena.GetComponent<RealTimeCombatManager>();
            var skill = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default").GetSkill(CombatSkillKit.Lucian, 3);
            var rupture = skill.groundImpact.interruptibleSkill;
            if (stage == 0)
            {
                typeof(SquadCharacterController).GetField("enableIdleSitting", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, false);
                if (player.IsInSittingCycle) { player.TrySetSitting(false); return; }
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.position = arena.Enemy.transform.position - Vector3.up * .15f;
                floor.transform.localScale = new Vector3(60, .3f, 60);
                enemyPosition = arena.Enemy.transform.position;
                arena.Enemy.enabled = false;
                var navigation = arena.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                navigation.isStopped = true; navigation.ResetPath();
                nearby = UnityEngine.Object.Instantiate(arena.Enemy, enemyPosition + Vector3.right * 4, Quaternion.identity);
                outside = UnityEngine.Object.Instantiate(arena.Enemy, enemyPosition + Vector3.right * 6, Quaternion.identity);
                nearbyBefore = nearby.CurrentHealth; outsideBefore = outside.CurrentHealth;
                player.SetHealth(1000, 1000); skill.cooldownSeconds = 0;
                Require(skill.groundImpact.radius == 5 && skill.groundImpact.airborneDamageMultiplier == 2, "Wrong radius/aerial bonus");
                Require(AnimationUtility.GetAnimationEvents(skill.AnimationClip).Length == 0, "Old impact events survived");
                bridge.TeleportForSceneTransition(enemyPosition - Vector3.forward * 2, Quaternion.identity);
                Next(1); return;
            }
            double elapsed = EditorApplication.timeSinceStartup - at;
            if (stage == 1 && elapsed > .7)
            {
                before = arena.Enemy.CurrentHealth;
                Require(manager.TryUseSkill(skill), "Ground cast rejected"); Next(2);
            }
            else if (stage == 2 && elapsed > .6)
            {
                Require(before - arena.Enemy.CurrentHealth == 20, "Ground damage must be exactly 20 once");
                Require(nearbyBefore - nearby.CurrentHealth == 20, "Area failed to damage the second enemy");
                Require(outside.CurrentHealth == outsideBefore, "Enemy outside radius took damage");
                UnityEngine.Object.Destroy(nearby.gameObject); UnityEngine.Object.Destroy(outside.gameObject);
                Require(Vector3.ProjectOnPlane(arena.Enemy.transform.position - enemyPosition, Vector3.up).magnitude > .4f, "Enemy was not pushed");
                manager.ResolveGroundImpact(skill, false, player.transform.position);
                Require(before - arena.Enemy.CurrentHealth == 20, "Duplicate impact applied");
                Next(3);
            }
            else if (stage == 3 && !action.IsActionActive && elapsed > .2)
            {
                Require(!bridge.IsExternalLockActive, "Ground action leaked lock");
                bridge.TeleportForSceneTransition(arena.Enemy.transform.position - Vector3.forward * 2 + Vector3.up * 3, Quaternion.identity);
                Next(4);
            }
            else if (stage == 4 && elapsed > .15)
            {
                Require(!bridge.Grounded, "Aerial fixture grounded");
                before = arena.Enemy.CurrentHealth;
                Require(manager.TryUseSkill(skill), "Air cast rejected"); Next(5);
            }
            else if (stage == 5 && elapsed > .8)
            {
                Require(bridge.Grounded, "UCC did not land");
                Require(before - arena.Enemy.CurrentHealth == 40, "Air damage must be exactly 40 once");
                Next(6);
            }
            else if (stage == 6 && !action.IsActionActive && elapsed > .2)
            {
                bridge.TeleportForSceneTransition(arena.Enemy.transform.position - Vector3.forward * 2, Quaternion.identity);
                Next(7);
            }
            else if (stage == 7 && elapsed > .3)
            {
                before = arena.Enemy.CurrentHealth;
                ruptureStartY = rupturePeakY = player.transform.position.y;
                Require(manager.TryUseSkill(rupture), "Rupture start rejected");
                generation = action.ActionGeneration; Next(8);
            }
            else if (stage == 8 && elapsed > .1 && !bridge.Grounded && player.transform.position.y > ruptureStartY + .03f)
            {
                Require(action.IsCurrentSession(generation), "Rupture session missing");
                Require(manager.TryUseSkill(skill), "Rupture cannot chain to impact");
                Require(!action.IsCurrentSession(generation), "Rupture generation survived");
                action.CancelAction(); Next(9);
            }
            else if (stage == 8 && elapsed > 2)
            {
                var animator = player.GetComponent<Animator>();
                var clips = animator.GetCurrentAnimatorClipInfo(0);
                throw new InvalidOperationException("Rupture did not produce its airborne rebound | dealt=" +
                    (before - arena.Enemy.CurrentHealth) + " player=" + player.transform.position + " enemy=" + arena.Enemy.transform.position +
                    " forward=" + player.transform.forward + " vertical=" + bridge.VerticalVelocity +
                    " startY=" + ruptureStartY + " peakY=" + rupturePeakY +
                    " clip=" + (clips.Length > 0 ? clips[0].clip.name : "none") + " action=" + action.IsActionActive);
            }
            else if (stage == 9 && elapsed > .4)
            {
                Require(!bridge.IsExternalLockActive && !bridge.IsScriptedPlanarMotionActive, "Cancellation leaked resources");
                Debug.Log("[GroundImpact] PLAY PASS | ground20, air40, radius5, actual push, landing, unique impact, Rupture replace, cancellation locks");
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
            if (stage == 8) rupturePeakY = Mathf.Max(rupturePeakY, player.transform.position.y);
        }
        catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
    }
}
