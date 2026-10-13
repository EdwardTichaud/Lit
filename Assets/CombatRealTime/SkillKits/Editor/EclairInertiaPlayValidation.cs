using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class EclairInertiaPlayValidation
{
    private const string Key = "Lit.EclairInertia.Validation";
    private static int stage, health;
    private static double started;
    private static float launched, hit, settle;
    private static Vector3 heading, stoppedPosition, enemyAtHit;
    private static bool sampledStop, sawStunRelease;
    private static Gamepad pad;
    static EclairInertiaPlayValidation() => EditorApplication.update += Tick;
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch Editor.");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>().character = CombatLabCharacter.Lucian;
        EditorApplication.isPlaying = true;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < 60, "Eclair inertia test timed out at stage " + stage);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            var actor = arena.Player;
            var enemy = arena.Enemy;
            var manager = arena.GetComponent<RealTimeCombatManager>();
            var bridge = actor.GetComponent<LitOpsiveLocomotionBridge>();
            var action = actor.GetComponent<PlayerActionPresentationController>();
            var skill = UnityEngine.Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default").GetSkill(CombatSkillKit.Lucian, 1);
            if (stage == 0)
            {
                typeof(SquadCharacterController).GetField("enableIdleSitting", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(actor, false);
                if (actor.IsInSittingCycle) { actor.TrySetSitting(false); return; }
                Require(skill.stunSeconds == 1 && skill.TargetLunge.holdControlsUntilAnimationEnd &&
                    !skill.Presentation.allowMobilityCancel && skill.Presentation.recoveryNormalizedTime == 1, "Eclair authoring invalid");
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.position = enemy.transform.position - Vector3.up * .15f;
                floor.transform.localScale = new Vector3(40, .3f, 40);
                enemy.SetCinematicSuspended(true);
                enemy.detectionTargetOverride = new GameObject("EclairValidationFarTarget").transform;
                enemy.detectionTargetOverride.position = enemy.transform.position + Vector3.one * 1000;
                enemy.SetCinematicSuspended(false);
                bridge.TeleportForSceneTransition(enemy.transform.position - Vector3.forward * 5 + Vector3.up * .05f, Quaternion.identity);
                actor.SetHealth(200, 200);
                pad = InputSystem.AddDevice<Gamepad>();
                health = enemy.CurrentHealth; settle = Time.time; stage = 1; return;
            }
            if (stage == 1)
            {
                if (Time.time - settle < .2f) return;
                heading = Vector3.ProjectOnPlane(enemy.transform.position - actor.transform.position, Vector3.up).normalized;
                UnityEngine.Object.FindAnyObjectByType<SkillsManager>().SetAnimationEventSkill(skill);
                Require(manager.TryUseSkill(skill), "Eclair rejected");
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right });
                launched = Time.time; stage = 2;
            }
            if (stage == 2)
            {
                if (hit == 0 && enemy.CurrentHealth < health)
                {
                    hit = Time.time; enemyAtHit = enemy.transform.position;
                    Require(enemy.IsCounterStunned && enemy.AttackPhase == LitBrainsAttackPhase.None, "Impact did not immobilize/cancel enemy attack");
                }
                Require(Time.time - launched < 4f || hit > 0, "Eclair never hit");
                if (hit == 0) return;
                float elapsed = Time.time - hit;
                if (elapsed < .9f)
                    Require(enemy.IsCounterStunned && Vector3.Distance(enemyAtHit, enemy.transform.position) < .15f, "Enemy moved or stun ended before 1s");
                if (elapsed > 1.15f) { Require(!enemy.IsCounterStunned, "Enemy stun lasted more than 1s"); sawStunRelease = true; }
                if (action.IsActionActive)
                {
                    Require(bridge.IsExternalLockActive && !action.CanCancelToMobility, "Player controls unlocked before clip end");
                    if (Time.time - launched > .65f)
                    {
                        Require(Vector3.Angle(actor.transform.forward, heading) < 5f, "Eclair turned back toward enemy before animation end");
                        if (!sampledStop) { stoppedPosition = actor.transform.position; sampledStop = true; }
                        else Require(Vector3.Distance(stoppedPosition, actor.transform.position) < .2f, "Held input moved player during Eclair recovery");
                    }
                    return;
                }
                Require(sampledStop && Time.time - launched >= skill.AnimationClip.length * .95f, "Eclair animation ended early");
                Require(!bridge.IsExternalLockActive && !bridge.IsScriptedPlanarMotionActive && bridge.TargetCollisionBypassCount == 0,
                    "Eclair completion leaked controls/collision bypass");
                settle = Time.time; stoppedPosition = actor.transform.position; stage = 3;
            }
            if (stage == 3)
            {
                if (Time.time - settle < .35f) return;
                Require(sawStunRelease && health - enemy.CurrentHealth == Mathf.RoundToInt(skill.Damages), "Stun or unique damage not validated");
                Require(Vector3.Distance(stoppedPosition, actor.transform.position) > .2f, "Held movement did not resume after Eclair");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                bridge.TeleportForSceneTransition(enemy.transform.position - Vector3.forward * 5 + Vector3.up * .05f, Quaternion.identity);
                settle = Time.time; stage = 4;
            }
            if (stage == 4)
            {
                if (Time.time - settle < .2f) return;
                Require(manager.TryUseSkill(skill), "Eclair replay rejected");
                action.CancelAction();
                Require(!bridge.IsExternalLockActive && !bridge.IsScriptedPlanarMotionActive && bridge.TargetCollisionBypassCount == 0,
                    "Interrupted Eclair leaked its lock");
                InputSystem.RemoveDevice(pad);
                Debug.Log("[EclairInertia] PLAY PASS | real impact, enemy immobilized 1s, travel heading retained, held input blocked until clip end, movement restored, interruption/collision cleanup");
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
        }
        catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
    }
}
