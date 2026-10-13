using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CombatLabClashPlayValidation
{
    private const string Key = "Lit.ClashPlayValidation";
    private static LucianBrainsCombatArena arena;
    private static int stage, cycles, playerHp, enemyHp;
    private static double started;
    private static float resolvedAt;
    private static Vector3 playerStart, enemyStart;
    private static double nextTrace;
    static CombatLabClashPlayValidation() { EditorApplication.update += Tick; }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch project required.");
        var type = typeof(CombatLabClashPlayValidation).Assembly.GetType("MainMenuMissingPrefabRepair");
        if (type != null)
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            var method = type.GetMethod("RepairMissingPrefab", BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null) EditorApplication.delayCall -= (EditorApplication.CallbackFunction)Delegate.CreateDelegate(typeof(EditorApplication.CallbackFunction), method);
        }
        CombatLabClashSetup.Configure();
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        EditorApplication.isPlaying = true;
    }
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < 100, "Clash timeout stage=" + stage);
            arena ??= UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            var clash = arena.GetComponent<CombatLabClashController>();
            var presentation = arena.Player.GetComponent<PlayerActionPresentationController>();
            var bridge = arena.Player.GetComponent<LitOpsiveLocomotionBridge>();
            if (stage == 0)
            {
                Time.captureDeltaTime = 1f / 60;
                arena.Enemy.combatProfile.attacks = new[] { arena.Enemy.combatProfile.attacks[0] };
                arena.Enemy.combatProfile.initialAttackDelay = 1f;
                arena.Enemy.combatProfile.attackInterval = new Vector2(10, 10);
                bridge.SetExternalPositionAndRotation(arena.Enemy.transform.position + Vector3.forward * 2.2f,
                    Quaternion.Euler(0, 180, 0), true);
                clash.diagnostics = true;
                playerHp = arena.Player.CurrentHp; enemyHp = arena.Enemy.CurrentHealth;
                playerStart = arena.Player.transform.position; enemyStart = arena.Enemy.transform.position;
                stage = 1;
            }
            if (stage == 1 && arena.Enemy.AttackPhase == LitBrainsAttackPhase.Preparation)
            {
                float elapsed = (float)typeof(LitBrainsEnemy).GetField("attackElapsed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(arena.Enemy);
                if (elapsed < .02f) return;
                var skill = arena.basicSkills[0];
                UnityEngine.Object.FindAnyObjectByType<SkillsManager>().SetAnimationEventSkill(skill);
                Require(arena.GetComponent<RealTimeCombatManager>().TryUseSkill(skill), "Basic skill refused");
                stage = 2;
            }
            if (stage == 2 && clash.ResolvedCount > 0) { stage = 3; resolvedAt = Time.time; }
            if (stage == 2 && EditorApplication.timeSinceStartup > nextTrace)
            {
                nextTrace = EditorApplication.timeSinceStartup + 1;
                var pc = arena.Player.GetComponent<Animator>().GetCurrentAnimatorClipInfo(0);
                var ec = arena.Enemy.Animator.GetCurrentAnimatorClipInfo(0);
                Debug.Log($"[CombatClash] Trace player={(pc.Length > 0 ? pc[0].clip.name : "none")} enemy={(ec.Length > 0 ? ec[0].clip.name : "none")} windows={clash.playerVolume.Open}/{clash.enemyVolume.Open} grounded={bridge.Grounded}/{arena.Enemy.IsClashGrounded}");
            }
            if (stage == 3 && Time.time - resolvedAt > .6f)
            {
                Require(clash.ResolvedCount == 1, "Duplicate clash");
                Require(arena.Player.CurrentHp == playerHp && arena.Enemy.CurrentHealth == enemyHp, "Clash applied damage");
                Require(arena.Enemy.ResistanceDamage == 0, "Clash applied resistance damage");
                Require(!presentation.IsActionActive && !bridge.IsScriptedPlanarMotionActive, "Clash left player locks");
                Require(arena.Enemy.AttackPhase == LitBrainsAttackPhase.None, "Enemy attack remained active");
                Require(Vector3.Distance(playerStart, arena.Player.transform.position) > .1f, "Player recoil absent");
                Require(Vector3.Distance(enemyStart, arena.Enemy.transform.position) > .1f, "Enemy recoil absent");
                Require(bridge.TryAcquireExternalLock(new object(), out var externalLock), "External test lock refused");
                clash.Cancel(); clash.Cancel();
                Require(!externalLock.IsReleased, "Clash cleanup released another owner's lock");
                externalLock.Dispose();
                if (++cycles < 3) { arena.RestartEncounter(); stage = 0; return; }
                Debug.Log("[CombatClash] PLAY PASS | 3 real clashes | no HP/resistance loss | UCC/NavMesh recoil | locks released");
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1);
        }
    }
}
