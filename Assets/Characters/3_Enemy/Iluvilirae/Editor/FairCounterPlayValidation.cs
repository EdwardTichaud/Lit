using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Three counter attempts only, in an isolated batch editor; never rewrites authored assets.</summary>
[InitializeOnLoad]
public static class FairCounterPlayValidation
{
    private const string Key = "Lit.FairCounter.PlayValidation";
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static int stage, attackIndex, playerHealth;
    private static double started, pausedAt;
    private static float at, pausedPhase;
    private static bool frozen, pauseChecked, pauseSampled;
    private static Gamepad pad;
    private static TimeManager.TimeRequestHandle slow;
    static FairCounterPlayValidation() => EditorApplication.update += Tick;

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch editor only.");
        EnemyCounterTimingValidation.ValidateLab();
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Set(object owner, string name, object value)
    {
        for (var type = owner.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Flags);
            if (field == null) continue;
            field.SetValue(owner, value); return;
        }
        throw new MissingFieldException(owner.GetType().Name, name);
    }
    private static void Property(object owner, string name, object value)
    {
        for (var type = owner.GetType(); type != null; type = type.BaseType)
        {
            var setter = type.GetProperty(name, Flags | BindingFlags.Public | BindingFlags.DeclaredOnly)?.GetSetMethod(true);
            if (setter == null) continue;
            setter.Invoke(owner, new[] { value }); return;
        }
        throw new MissingMemberException(owner.GetType().Name, name);
    }
    private static void Next(int value) { stage = value; at = Time.time; }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.isPaused = false; EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < 90, "Short validation timed out at stage " + stage);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena?.Player == null || arena.Enemy == null) return;
            var enemy = arena.Enemy;
            var bridge = arena.Player.GetComponent<LitOpsiveLocomotionBridge>();
            var reactions = arena.GetComponent<CombatHealthThresholdController>();
            var counter = CounterSkillCombatController.Instance;
            if (stage == 0)
            {
                Set(arena.Player, "enableIdleSitting", false);
                if (arena.Player.IsInSittingCycle) { arena.Player.TrySetSitting(false); return; }
                if (!enemy.NavigationReady) return;
                if (!frozen)
                {
                    enemy.SetCinematicSuspended(true); enemy.SetCinematicSuspended(false);
                    Set(enemy, "hurtRemaining", 1000f); Set(enemy, "fluidCooldown", 1000f);
                    enemy.Animator.Play("Locomotion", 0, 0);
                    frozen = true;
                }
                if (Time.frameCount < 8 || !bridge.Grounded) return;
                pad = InputSystem.AddDevice<Gamepad>();
                arena.Player.SetHealth(1000, 1000);
                Set(enemy, "currentHealth", 1000);
                arena.GetComponent<RealTimeCombatInput>().SetInputActive(true);
                Next(1); return;
            }
            if (stage == 1 && Time.time - at > .15f)
            {
                var attack = enemy.combatProfile.attacks[attackIndex];
                Set(enemy, "counterStunActive", false); Set(enemy, "counterStunPending", false); Set(enemy, "hurtRemaining", 1000f);
                Set(enemy, "activeAttack", attack); Set(enemy, "fluidImpactUsed", false); Set(enemy, "attackElapsed", 0f);
                Property(enemy, "ActionSequenceId", enemy.ActionSequenceId + 1);
                Property(enemy, "CommittedAttackTarget", arena.Player.transform);
                Property(enemy, "AttackPhase", LitBrainsAttackPhase.Preparation);
                enemy.transform.rotation = Quaternion.identity;
                bridge.TeleportForSceneTransition(enemy.transform.position + Vector3.forward * 2, Quaternion.Euler(0, 180, 0));
                playerHealth = arena.Player.CurrentHp;
                if (attackIndex == 1) slow = TimeManager.EnsureInstance().AcquireGlobal(.4f, typeof(FairCounterPlayValidation));
                enemy.Animator.CrossFadeInFixedTime(attack.stateName, .05f, 0, 0);
                Debug.Log("[FairCounter] Playing " + attack.stateName + (attackIndex == 1 ? " at 0.4" : " at 1"));
                Next(2);
            }
            else if (stage == 2)
            {
                var attack = enemy.ActiveAttack;
                Require(attack != null, "Attack disappeared before counter");
                float phase = enemy.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime * attack.clip.length;
                if (attackIndex == 2 && !pauseChecked && reactions.IsExternalCounterWindowOpen)
                {
                    pausedPhase = phase; pausedAt = Time.realtimeSinceStartupAsDouble;
                    arena.SetPaused(true); Next(4); return;
                }
                if (phase < attack.impactSeconds - .1f || !reactions.IsExternalCounterWindowOpen) return;
                if (attackIndex == 1)
                    Require(!(bool)typeof(CombatHealthThresholdController).GetField("attackQteActive", Flags).GetValue(reactions),
                        "Slow test must exercise Y after B has expired");
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North));
                Next(3);
            }
            else if (stage == 3)
            {
                if (!counter.IsCinematicPlaying)
                { Require(Time.time - at < .3f, "Eligible Y did not start the cinematic counter"); return; }
                Require(arena.Player.CurrentHp == playerHealth, "Enemy impact beat the accepted counter");
                Require(!enemy.IsAttackCommitted, "Counter did not invalidate the attack");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                TimeManager.Instance?.Release(slow); slow = default;
                Next(5);
            }
            else if (stage == 4)
            {
                var attack = enemy.ActiveAttack;
                if (!pauseSampled)
                {
                    pausedPhase = enemy.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime * attack.clip.length;
                    pausedAt = Time.realtimeSinceStartupAsDouble; pauseSampled = true; return;
                }
                if (Time.realtimeSinceStartupAsDouble - pausedAt <= .2) return;
                Require(Mathf.Abs(enemy.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime * attack.clip.length - pausedPhase) < .002f,
                    "Paused animation advanced");
                Require(!reactions.IsExternalCounterWindowOpen, "Menu exposed combat input");
                arena.SetPaused(false); pauseChecked = true; Next(2);
            }
            else if (stage == 5 && !counter.IsCinematicPlaying)
            {
                Require(arena.Player.CurrentHp == playerHealth, "A late impact survived the counter");
                Require(!arena.GetComponent<RealTimeCombatManager>().IsCinematicSequenceActive, "Cinematic lock leaked");
                Debug.Log("[FairCounter] Counter PASS " + attackIndex);
                if (++attackIndex == 3) { Finish(0); return; }
                Next(1);
            }
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(Key, false);
        TimeManager.Instance?.Release(slow);
        if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        if (code == 0) Debug.Log("[FairCounter] PLAY PASS | three clips, normal/slow counters, B expiry independent, pause, no player damage or late impact.");
        EditorApplication.Exit(code);
    }
}
