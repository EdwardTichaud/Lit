using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class RuptureArmorPlayValidation
{
    private const string Key = "Lit.RuptureArmor.Validation";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int stage, generation, hits;
    private static double started;
    private static float launched, nextHit, settled;
    private static Gamepad pad;
    private static Vector3 castOrigin, recoveryOrigin;
    private static int enemyHealth;
    private static float enemyHitAt;
    private static Vector3 enemyHitPosition;
    private static bool stunExpired, rangeChecked;
    static RuptureArmorPlayValidation() => EditorApplication.update += Tick;
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
            Require(EditorApplication.timeSinceStartup - started < 60, "Rupture test timeout at stage " + stage);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            var actor = arena.Player;
            var manager = arena.GetComponent<RealTimeCombatManager>();
            var action = actor.GetComponent<PlayerActionPresentationController>();
            var bridge = actor.GetComponent<LitOpsiveLocomotionBridge>();
            var skill = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default").GetSkill(CombatSkillKit.Lucian, 2);
            string hurt = (string)typeof(RealTimeCombatManager).GetField("playerHurtAnimatorState", Private).GetValue(manager);
            if (stage == 0)
            {
                typeof(SquadCharacterController).GetField("enableIdleSitting", Private).SetValue(actor, false);
                if (actor.IsInSittingCycle) { actor.TrySetSitting(false); return; }
                Require(!skill.Presentation.allowDamageInterruption && !skill.Presentation.allowMobilityCancel &&
                    skill.Presentation.recoveryNormalizedTime == 1 && skill.Presentation.lockMovementDuringAnimation,
                    "Rupture must protect and lock movement for the entire clip");
                Require(skill.stunSeconds == 1, "Rupture stun must last one second");
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.position = arena.Enemy.transform.position - Vector3.up * .15f;
                floor.transform.localScale = new Vector3(60, .3f, 60);
                arena.Enemy.SetCinematicSuspended(true);
                arena.Enemy.detectionTargetOverride = new GameObject("RuptureValidationFarTarget").transform;
                arena.Enemy.detectionTargetOverride.position = arena.Enemy.transform.position + Vector3.one * 1000;
                arena.Enemy.SetCinematicSuspended(false);
                enemyHealth = arena.Enemy.CurrentHealth;
                bridge.TeleportForSceneTransition(arena.Enemy.transform.position - Vector3.forward * 2 + Vector3.up * .05f, Quaternion.identity);
                actor.SetHealth(200, 200);
                pad = InputSystem.AddDevice<Gamepad>();
                settled = Time.time; stage = 1; return;
            }
            if (stage == 1)
            {
                if (Time.time - settled < .2f) return;
                if (!rangeChecked)
                {
                    Require(skill.targetingMode == SkillTargetingMode.LockedTarget && skill.useLaunchRange &&
                        skill.IsWithinLaunchRange(3) && !skill.IsWithinLaunchRange(3.01f), "Rupture must require melee range (0-3m).");
                    bridge.TeleportForSceneTransition(arena.Enemy.transform.position - Vector3.forward * 6, Quaternion.identity);
                    manager.BeginExternalEncounter(actor.transform, arena.Enemy);
                    Require(!manager.TryUseSkill(skill) && !action.IsActionActive && manager.GetSkillCooldownRemaining(skill) == 0,
                        "Out-of-range Rupture started or consumed cooldown.");
                    bridge.TeleportForSceneTransition(arena.Enemy.transform.position - Vector3.forward * 2, Quaternion.identity);
                    Require(manager.TryReleaseManualLock(), "Lock test could not unlock.");
                    Require(!manager.TryUseSkill(skill) && !action.IsActionActive && manager.GetSkillCooldownRemaining(skill) == 0 &&
                        manager.LastSkillTargetingFailure.Contains("Verrouillez"), "Rupture without a manual lock started.");
                    Require(manager.TryAdvanceManualLock(), "Lock test could not lock again.");
                    rangeChecked = true; settled = Time.time; return;
                }
                bridge.TeleportForSceneTransition(actor.transform.position, Quaternion.identity);
                bridge.SetActionFacingDirection(Vector3.forward);
                UnityEngine.Object.FindAnyObjectByType<SkillsManager>().SetAnimationEventSkill(skill);
                Require(manager.TryUseSkill(skill), "Rupture rejected");
                castOrigin = actor.transform.position;
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right });
                generation = action.ActionGeneration;
                launched = Time.time; nextHit = launched + .15f; stage = 2;
            }
            if (stage >= 2 && stage <= 3)
            {
                if (enemyHitAt == 0 && arena.Enemy.CurrentHealth < enemyHealth)
                {
                    enemyHitAt = Time.time; enemyHitPosition = arena.Enemy.transform.position;
                    Require(arena.Enemy.IsCounterStunned && arena.Enemy.AttackPhase == LitBrainsAttackPhase.None,
                        "Confirmed Rupture impact did not stun the enemy");
                }
                if (enemyHitAt > 0)
                {
                    if (Time.time - enemyHitAt < .9f)
                        Require(arena.Enemy.IsCounterStunned && Vector3.Distance(enemyHitPosition, arena.Enemy.transform.position) < .15f,
                            "Enemy moved or recovered before one second");
                    if (Time.time - enemyHitAt > 1.15f)
                    {
                        Require(!arena.Enemy.IsCounterStunned, "Rupture stun exceeded one second");
                        stunExpired = true;
                    }
                }
                else Require(!arena.Enemy.IsCounterStunned, "Rupture stunned before impact");
            }
            if (stage == 2)
            {
                if (action.IsActionActive)
                {
                    Require(action.ActionGeneration == generation && action.IsDamageReactionSuppressed, "Damage replaced Rupture");
                    Require(!action.CancelActionForMobility(), "Mobility interrupted Rupture");
                    Require(bridge.IsInputSuppressedByUcc, "Movement unlocked during Rupture");
                    // A live enemy can turn/move the authored retreat direction;
                    // assert actual UCC input, not a fixed world-axis trajectory.
                    Require(actor.GetComponent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>().InputVector.sqrMagnitude < .001f,
                        "Held sideways input reached UCC during Rupture");
                    if (Time.time >= nextHit && hits < 3)
                    {
                        int before = actor.CurrentHp;
                        int applied = (int)typeof(RealTimeCombatManager).GetMethod("ApplyPlayerDamage", Private).Invoke(manager, new object[] { 7 });
                        Require(applied == 7 && actor.CurrentHp == before - 7, "Armor incorrectly blocks damage");
                        Require(action.ActionGeneration == generation && !action.TryReplaceWithDamageReaction(hurt, PlayerActionPresentationProfile.CreateDefault()), "Hurt interrupted Rupture");
                        hits++; nextHit = Time.time + .3f;
                    }
                    return;
                }
                Require(hits == 3 && actor.CurrentHp == 179, "Missing damage samples");
                Require(actor.transform.position.z < castOrigin.z - .5f, "Movement lock suppressed the authored recoil");
                Require(Time.time - launched >= skill.AnimationClip.length * .95f &&
                    action.LastEndReason == PlayerActionPresentationController.ActionEndReason.Completed, "Clip ended early");
                Require(!action.IsDamageReactionSuppressed && action.TryReplaceWithDamageReaction(hurt, PlayerActionPresentationProfile.CreateDefault()), "Armor leaked after completion");
                action.CancelAction(); settled = Time.time; recoveryOrigin = actor.transform.position; stage = 3;
                Debug.Log("[RuptureArmor] PASS melee/manual-lock refusals, unique impact/stun and backward recoil, full animation, movement lock released");
            }
            if (stage == 3)
            {
                if (Time.time - settled < .5f || bridge.IsExternalLockActive) return;
                Require(enemyHitAt > 0 && stunExpired && enemyHealth - arena.Enemy.CurrentHealth == Mathf.RoundToInt(skill.Damages),
                    "Rupture must produce a unique damaging impact and a one-second stun");
                Require(!bridge.IsInputSuppressedByUcc && Mathf.Abs(actor.transform.position.x - recoveryOrigin.x) > .2f,
                    "Held movement did not resume after Rupture");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                bridge.TeleportForSceneTransition(arena.Enemy.transform.position - Vector3.forward * 2 + Vector3.up * .05f, Quaternion.identity);
                settled = Time.time; stage = 4; return;
            }
            if (stage == 4)
            {
                if (Time.time - settled < .2f) return;
                bridge.TeleportForSceneTransition(actor.transform.position, Quaternion.identity);
                bridge.SetActionFacingDirection(Vector3.forward);
                Require(manager.TryUseSkill(skill), "Rupture replay rejected: " + manager.LastSkillTargetingFailure);
                castOrigin = actor.transform.position;
                enemyHealth = arena.Enemy.CurrentHealth;
                arena.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(arena.Enemy.transform.position + Vector3.right * 8);
                stage = 5; return;
            }
            if (stage == 5)
            {
                if (action.IsActionActive || bridge.IsExternalLockActive || !bridge.Grounded) return;
                Require(arena.Enemy.CurrentHealth == enemyHealth && actor.transform.position.z < castOrigin.z - .5f,
                    "A missed contact must still escape backward, without damage.");
                bridge.TeleportForSceneTransition(arena.Enemy.transform.position - Vector3.forward * 2 + Vector3.up * .05f, Quaternion.identity);
                settled = Time.time; stage = 6; return;
            }
            if (stage == 6)
            {
                if (Time.time - settled < .2f) return;
                bridge.TeleportForSceneTransition(actor.transform.position, Quaternion.identity);
                bridge.SetActionFacingDirection(Vector3.forward);
                Require(manager.TryUseSkill(skill), "Rupture death-test replay rejected");
                actor.SetHealth(1, 200);
                typeof(RealTimeCombatManager).GetMethod("ApplyPlayerDamage", Private).Invoke(manager, new object[] { 7 });
                typeof(RealTimeCombatManager).GetMethod("EvaluateCombatOutcome", Private).Invoke(manager, null);
                Require(actor.CurrentHp == 0 && action.IsDeathAnimationLocked && !action.IsDamageReactionSuppressed, "Armor blocked death or leaked on death");
                Require(!bridge.IsExternalLockActive, "Death leaked the movement lock");
                InputSystem.RemoveDevice(pad);
                Debug.Log("[RuptureArmor] PLAY PASS | manual lock and melee required | far/unlocked rejected without cost | unique impact/stun | backward UCC escape also on miss | movement restitution | death cleanup");
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
        }
        catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
    }
}
