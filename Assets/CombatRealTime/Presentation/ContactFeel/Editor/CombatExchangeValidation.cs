using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Thirty real seconds of normal input and live enemy decisions. No action or impact is forced.</summary>
[InitializeOnLoad]
public static class CombatExchangeValidation
{
    private const string Key = "Lit.CombatFeel.Exchange";
    private static readonly List<Sample> samples = new List<Sample>();
    private static readonly HashSet<int> attacks = new HashSet<int>();
    private static readonly HashSet<string> states = new HashSet<string>();
    private static readonly HashSet<string> playerSkills = new HashSet<string>();
    private static readonly HashSet<LitBrainsAttackPhase> capturedPhases = new HashSet<LitBrainsAttackPhase>();
    private static Gamepad pad;
    private static double loadedAt;
    private static double startedAt;
    private static float nextSample, nextCapture, gameStartedAt;
    private static Vector3 lastEnemyPosition;
    private static LitBrainsAttackPhase lastPhase;
    private static int playerStarts, playerHits, enemyHits, stage, captureIndex, lastPlayerHp;
    private static int quietStarts, quietPrematureStarts, lastSequence;
    private static float committedAt;
    private static RealTimeCombatManager manager;
    private static string Output => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library/CombatFeel/Exchange");

    [Serializable] private sealed class Sample
    {
        public float realtime, gameTime, scale, gap, enemySpeed, animationSpeed, cameraRatio;
        public Vector3 player, enemy;
        public Vector3 playerForward, actionDirection;
        public string phase, attack;
        public int sequence, playerHp, enemyHp;
    }
    [Serializable] private sealed class Report
    {
        public bool passed;
        public bool rendered;
        public string failure;
        public string scope = "Normal synthetic gamepad, live AI, runtime HP raised to 1000 only for observation; no damage immunity. Enemy contacts count observed HP decreases. Numbers hidden only in rendered captures. Not manual feel/network acceptance.";
        public int playerStarts, playerHits, enemyHits, enemyActions, uniqueEnemyAttacks;
        public int quietStarts, quietPrematureStarts;
        public Sample[] samples;
        public string[] playerSkillNames;
    }

    static CombatExchangeValidation() => EditorApplication.update += Tick;
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated editor only.");
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        SessionState.SetBool(Key, true);
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }
    private static void Check(bool valid, string reason) { if (!valid) throw new InvalidOperationException(reason); }
    private static void OnStart(SkillSO skill, EnemyController target) { playerStarts++; if (skill != null) playerSkills.Add(skill.name); }
    private static void OnHit(SkillSO skill, int damage) => playerHits++;

    private static void Capture(LucianBrainsCombatArena arena, string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var hidden = new List<Canvas>();
        try
        {
            foreach (var feedback in UnityEngine.Object.FindObjectsByType<CombatDamageWorldFeedback>())
            {
                var canvas = feedback.GetComponent<Canvas>();
                if (feedback.IsPersistent || canvas == null || !canvas.enabled) continue;
                hidden.Add(canvas); canvas.enabled = false;
            }
            CombatFeelPlayValidation.CaptureCamera(arena.arenaCamera, Path.Combine(Output, name + ".png"));
        }
        finally { foreach (var canvas in hidden) if (canvas != null) canvas.enabled = true; }
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (loadedAt == 0) loadedAt = EditorApplication.timeSinceStartup;
            Check(EditorApplication.timeSinceStartup - loadedAt < 100, "Exchange startup/observation timeout.");
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena?.Player == null || arena.Enemy == null) return;
            var player = arena.Player;
            var enemy = arena.Enemy;
            var bridge = player.GetComponent<LitOpsiveLocomotionBridge>();
            if (stage == 0)
            {
                typeof(SquadCharacterController).GetField("enableIdleSitting", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, false);
                if (player.IsInSittingCycle) { player.TrySetSitting(false); return; }
                if (!bridge.Grounded || !enemy.NavigationReady) return;
                player.SetHealth(1000, 1000); enemy.Health.SetHealth(1000, 1000);
                manager = arena.GetComponent<RealTimeCombatManager>();
                manager.PlayerSkillStarted += OnStart; manager.PlayerSkillImpactApplied += OnHit;
                lastPlayerHp = player.CurrentHp;
                pad = InputSystem.AddDevice<Gamepad>();
                Capture(arena, "00_start");
                startedAt = EditorApplication.timeSinceStartup; gameStartedAt = Time.time;
                nextSample = nextCapture = 0;
                lastEnemyPosition = enemy.transform.position; lastPhase = enemy.AttackPhase;
                stage = 1; return;
            }
            if (stage == 1)
            {
                // Enter Play Mode can finish its cold initialization after our first editor tick.
                // Start the recipe only after the normal simulation has actually advanced.
                if (Time.time - gameStartedAt < .5f) return;
                startedAt = EditorApplication.timeSinceStartup; gameStartedAt = Time.time;
                stage = 2; return;
            }
            float t = (float)(EditorApplication.timeSinceStartup - startedAt);
            var delta = Vector3.ProjectOnPlane(enemy.transform.position - player.transform.position, Vector3.up);
            float gap = delta.magnitude;
            if (player.CurrentHp < lastPlayerHp) enemyHits++;
            lastPlayerHp = player.CurrentHp;
            var forward = Vector3.ProjectOnPlane(arena.arenaCamera.transform.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var desired = t < 23 ? delta.normalized * (gap > 2.35f ? .8f : .35f) : Vector3.zero;
            if (t > 12 && t < 17 && gap <= 3.5f) desired = Vector3.Cross(Vector3.up, delta.normalized) * .35f;
            Vector2 stick = new Vector2(Vector3.Dot(desired, right), Vector3.Dot(desired, forward));
            var input = new GamepadState { leftStick = stick, rightTrigger = gap > 8 && t < 5 ? 1 : 0 };
            bool pressAttack = gap < 3.2f && (t < 12 || t > 17 && t < 23) && Mathf.Repeat(t, .6f) < .13f;
            bool pressDodge = t > 12 && t < 17 && Mathf.Repeat(t - 12, 2) < .12f;
            if (pressAttack)
                input = input.WithButton(GamepadButton.West);
            if (pressDodge)
                input = input.WithButton(GamepadButton.East);
            bool canCapture = !pressAttack && !pressDodge && !pad.buttonWest.isPressed && !pad.buttonEast.isPressed;
            InputSystem.QueueStateEvent(pad, input);
            if (enemy.AttackPhase != LitBrainsAttackPhase.None)
            {
                if (enemy.ActionSequenceId != lastSequence)
                {
                    lastSequence = enemy.ActionSequenceId; committedAt = Time.time;
                    if (t >= 25) quietStarts++;
                }
                attacks.Add(enemy.ActionSequenceId);
                if (enemy.ActiveAttack != null) states.Add(enemy.ActiveAttack.stateName);
            }
            if (enemy.AttackPhase != lastPhase)
            {
                if (t >= 25 && lastPhase == LitBrainsAttackPhase.Preparation && enemy.AttackPhase == LitBrainsAttackPhase.None &&
                    Time.time - committedAt < .35f) quietPrematureStarts++;
                Debug.Log($"[CombatExchange] t={t:F2} phase={enemy.AttackPhase} attack={enemy.ActiveAttack?.stateName} gap={gap:F2} travel={Vector3.Distance(lastEnemyPosition, enemy.transform.position):F3}");
                lastPhase = enemy.AttackPhase;
                if (canCapture && enemy.AttackPhase != LitBrainsAttackPhase.None && capturedPhases.Add(enemy.AttackPhase))
                    Capture(arena, "phase_" + enemy.AttackPhase);
            }
            lastEnemyPosition = enemy.transform.position;
            if (t >= nextSample)
            {
                var view = arena.arenaCamera.GetComponent<Opsive.UltimateCharacterController.Camera.CameraController>().ActiveViewType as LitTacticalUccViewType;
                samples.Add(new Sample { realtime = t, gameTime = Time.time, scale = Time.timeScale, gap = gap,
                    player = player.transform.position, enemy = enemy.transform.position, phase = enemy.AttackPhase.ToString(),
                    playerForward = player.transform.forward, actionDirection = manager.CurrentAttackContext?.Direction ?? Vector3.zero,
                    attack = enemy.ActiveAttack?.stateName, sequence = enemy.ActionSequenceId, enemySpeed = enemy.ActualSpeed,
                    animationSpeed = enemy.Animator.GetFloat("Speed"), cameraRatio = view != null ? view.ActionDistanceRatio : 1,
                    playerHp = player.CurrentHp, enemyHp = enemy.CurrentHealth });
                nextSample = t + .05f;
            }
            if (canCapture && t >= nextCapture)
            {
                Capture(arena, "exchange_" + captureIndex++.ToString("00")); nextCapture = t + 2.5f;
            }
            if (t < 30) return;
            Check(samples.Count >= 100 && Time.time - gameStartedAt >= 8, "Insufficient simulated frames after rendering warmup.");
            Check(attacks.Count >= 3, "Enemy did not sustain attacks during the 30-second exchange.");
            Check(playerStarts >= 3 && playerHits > 0, "Normal attack inputs did not produce confirmed player contacts.");
            Check(quietStarts > 0 && quietPrematureStarts == 0, "Enemy recovery-to-next-action produced a false preparation start.");
            Finish(null);
        }
        catch (Exception error) { Debug.LogException(error); Finish(error.Message); }
    }

    private static void Finish(string failure)
    {
        var report = new Report { passed = failure == null, rendered = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null,
            failure = failure, playerStarts = playerStarts, playerHits = playerHits,
            enemyHits = enemyHits, enemyActions = attacks.Count, uniqueEnemyAttacks = states.Count,
            quietStarts = quietStarts, quietPrematureStarts = quietPrematureStarts,
            playerSkillNames = new List<string>(playerSkills).ToArray(), samples = samples.ToArray() };
        File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
        if (manager != null) { manager.PlayerSkillStarted -= OnStart; manager.PlayerSkillImpactApplied -= OnHit; }
        if (pad != null) InputSystem.RemoveDevice(pad);
        SessionState.SetBool(Key, false);
        Debug.Log($"[CombatExchange] {(failure == null ? "PASS" : "FAIL")} starts={playerStarts} playerContacts={playerHits} enemyContacts={enemyHits} enemyActions={attacks.Count} types={states.Count}");
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
