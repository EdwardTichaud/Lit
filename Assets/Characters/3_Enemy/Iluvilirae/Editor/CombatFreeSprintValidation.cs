using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CombatFreeSprintValidation
{
    private const string Key = "Lit.CombatFreeSprintValidation";
    private const string Request = "Library/CombatFreeSprint.request";
    private static readonly Vector2[] Directions = { Vector2.up, new Vector2(1, 1).normalized, Vector2.right,
        new Vector2(1, -1).normalized, Vector2.down, new Vector2(-1, -1).normalized, Vector2.left, new Vector2(-1, 1).normalized };
    private static Gamepad pad;
    private static LucianBrainsCombatArena arena;
    private static int stage;
    private static double started;
    private static float simulationStarted;
    private static int simulationFrame;
    private static Vector3 startPosition, expectedDirection;
    private static Quaternion startRotation;
    private static bool previousDiagnostics;
    private static InputSettings.BackgroundBehavior previousBackground;
    private static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
    private static bool previousRunInBackground;
    private static int recoveryState;
    private static float previousTakeoffTime;
    private static LitOpsiveLocomotionBridge.ExternalLockHandle retainedLock;

    static CombatFreeSprintValidation()
    {
        EditorApplication.update += Tick;
        if (SessionState.GetBool(Key, false)) SuppressUnrelatedSceneRepair();
    }

    private static void SuppressUnrelatedSceneRepair()
    {
        var repair = typeof(CombatFreeSprintValidation).Assembly.GetType("MainMenuMissingPrefabRepair");
        if (repair == null) return;
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(repair.TypeHandle);
        var method = repair.GetMethod("RepairMissingPrefab", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (method != null)
            EditorApplication.delayCall -= (EditorApplication.CallbackFunction)Delegate.CreateDelegate(typeof(EditorApplication.CallbackFunction), method);
    }

    [MenuItem("Lit/Combat/Validate Free Sprint in Iluvilirae")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before validation.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save modified scenes before validation.");
        stage = 0;
        arena = null;
        pad = null;
        started = EditorApplication.timeSinceStartup;
        SessionState.SetString(Key + ".Scene", SceneManager.GetActiveScene().path);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".Finished", false);
        SuppressUnrelatedSceneRepair();
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("RunBatch requires an isolated batch Editor.");
        Run();
    }

    public static void RunSprintBatch()
    {
        SessionState.SetBool(Key + ".SprintOnly", true);
        RunBatch();
    }

    public static void RunMobilityRecoveryBatch()
    {
        SessionState.SetBool(Key + ".MobilityRecovery", true);
        RunBatch();
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!SessionState.GetBool(Key, false))
        {
            if (File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(Request);
                try { Run(); } catch (Exception e) { File.WriteAllText("Library/CombatFreeSprint.result", "BLOCKED " + e.Message); }
            }
            return;
        }
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(Key, false);
            string previous = SessionState.GetString(Key + ".Scene", "");
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
            return;
        }
        if (SessionState.GetBool(Key + ".Finished", false)) return;
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            if (arena == null) arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            Require(EditorApplication.timeSinceStartup - started < 60, "Timed out waiting for lab at stage " + stage);
            if (arena == null || arena.Player == null || arena.Enemy == null || (stage == 0 && !arena.Enemy.NavigationReady)) return;
            var bridge = arena.Player.GetComponent<LitOpsiveLocomotionBridge>();
            float elapsed = Time.unscaledTime - simulationStarted;
            if (stage == 0)
            {
                if (Time.frameCount < 12) return;
                var diagnostics = typeof(LitOpsiveLocomotionBridge).GetProperty("logCombatLockMotionDiagnostics", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                previousDiagnostics = (bool)diagnostics.GetValue(bridge);
                diagnostics.SetValue(bridge, true);
                arena.Enemy.SetCinematicSuspended(true);
                previousBackground = InputSystem.settings.backgroundBehavior;
                previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
                previousRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                var agent = arena.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
                pad = InputSystem.AddDevice<Gamepad>();
                startPosition = arena.Player.transform.position;
                startRotation = arena.Player.transform.rotation;
                if (SessionState.GetBool(Key + ".MobilityRecovery", false))
                {
                    SetPad(new GamepadState());
                    var jump = arena.Player.GetComponent<PlayerScriptedJumpController>();
                    previousTakeoffTime = jump.jumpStartTakeoffNormalizedTime;
                    jump.jumpStartTakeoffNormalizedTime = .95f;
                    recoveryState = arena.Player.GetComponent<CharacterAnimationController>().Animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
                    Require(bridge.Jump(Vector2.zero, false), "Initial jump refused");
                    NextStage(101);
                    return;
                }
                arena.Player.SetSprintModifier(true);
                Require((bool)typeof(LitOpsiveLocomotionBridge).GetField("sprintPressed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(bridge),
                    "Sprint forwarding did not retain intent");
                NextDirection(1, bridge);
                return;
            }
            if (stage >= 101)
            {
                ValidateMobilityRecovery(bridge, elapsed);
                return;
            }
            if (stage <= 8)
            {
                if (elapsed < .85) return;
                if (Time.frameCount - simulationFrame < 12) return;
                Require(bridge.IsCombatFreeSprint, "Sprint mode not active for direction " + stage +
                    " lock=" + bridge.IsCombatLockActive + " input=" + LocalInputRouter.MoveValue +
                    " sprint=" + LocalInputRouter.SprintPressed + " suppressed=" + bridge.IsInputSuppressedByUcc +
                    " action=" + arena.Player.GetComponent<PlayerActionPresentationController>().IsActionActive +
                    " mode=" + InputModeCoordinator.CurrentMode + " resolved=" + bridge.LockLocomotionMode + " paused=" + EditorApplication.isPaused +
                    " driving=" + bridge.IsDriving + " frame=" + Time.frameCount + " arenaEnabled=" + arena.isActiveAndEnabled +
                    " arenaPaused=" + arena.IsPaused + " combatCinematic=" + arena.GetComponent<RealTimeCombatManager>().IsCinematicSequenceActive +
                    " cinematic=" + bridge.IsCinematicMotionSessionActive + " traversal=" + bridge.IsScriptedTraversalActive +
                    " guard=" + CounterSkillCombatController.Instance.IsGuardHeld + " world=" + bridge.CurrentWorldMoveInput +
                    " bridgeSprint=" + typeof(LitOpsiveLocomotionBridge).GetField("sprintPressed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(bridge));
                Require(Vector3.Dot((arena.Player.transform.position - startPosition).normalized, expectedDirection) > .5f,
                    "Sprint movement did not follow camera direction " + stage);
                Require(Vector3.Dot(arena.Player.transform.forward, expectedDirection) > .5f, "Facing opposed sprint " + stage);
                Require(bridge.IsCombatLockActive, "Sprint cleared lock");
                if (SessionState.GetBool(Key + ".SprintOnly", false))
                {
                    var motor = arena.Player.GetComponent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>();
                    var speedChange = motor.GetAbility<Opsive.UltimateCharacterController.Character.Abilities.SpeedChange>();
                    Require(speedChange != null && speedChange.IsActive, "UCC sprint ability not active");
                    Require(bridge.PlanarVelocity.magnitude > .1f, "Sprint has no physical velocity");
                    Finish("PASS held trigger reaches bridge, UCC sprint active, camera-relative movement and facing; speed=" + bridge.PlanarVelocity.magnitude);
                    return;
                }
                if (stage < 8) { NextDirection(stage + 1, bridge); return; }
                SetPad(new GamepadState { leftStick = Vector2.right });
                NextStage(9); return;
            }
            if (stage == 9)
            {
                if (elapsed < .5 || Time.frameCount - simulationFrame < 12) return;
                Require(bridge.LockLocomotionMode == LitOpsiveLocomotionBridge.CombatLocomotionMode.TargetStrafe, "Release did not restore strafe");
                SetPad(new GamepadState { leftStick = new Vector2(1, 1).normalized, rightTrigger = 1 });
                NextStage(10); return;
            }
            if (stage == 10)
            {
                if (elapsed < .3 || Time.frameCount - simulationFrame < 12) return;
                expectedDirection = new Vector3(bridge.CurrentWorldMoveInput.x, 0, bridge.CurrentWorldMoveInput.y).normalized;
                Require(arena.GetComponent<CombatMobilityController>().TryDodgeImmediate(), "Directional dodge refused");
                Require(bridge.IsCombatDirectionalEvasionFacing, "Dodge did not acquire facing");
                Require(Vector3.Dot(arena.Player.transform.forward, expectedDirection) > .95f, "Diagonal dodge facing mismatch");
                NextStage(11); return;
            }
            if (elapsed < 1.5 || Time.frameCount - simulationFrame < 12) return;
            Require(!bridge.IsCombatDirectionalEvasionFacing && bridge.IsCombatFreeSprint, "Dodge did not hand off to held sprint");
            Finish("PASS eight camera-relative sprint directions, facing, lock retention, trigger release and diagonal dodge handoff");
        }
        catch (Exception e) { Finish("FAIL " + e); }
    }

    private static void ValidateMobilityRecovery(LitOpsiveLocomotionBridge bridge, float elapsed)
    {
        var jump = arena.Player.GetComponent<PlayerScriptedJumpController>();
        var animator = arena.Player.GetComponent<CharacterAnimationController>().Animator;
        var presentation = arena.Player.GetComponent<PlayerActionPresentationController>();
        var mobility = arena.GetComponent<CombatMobilityController>();
        if (stage == 101)
        {
            if (elapsed < .12f) return;
            animator.CrossFade(recoveryState, .05f, 0);
            NextStage(102);
        }
        else if (stage == 102)
        {
            if (elapsed < .2f) return;
            Require(!jump.IsActive, "Interrupted takeoff retained active jump");
            jump.jumpStartTakeoffNormalizedTime = previousTakeoffTime;
            Require(mobility.TryDodgeImmediate(), "Dodge after interrupted jump refused");
            Require(bridge.TryAcquireExternalLock(arena, out retainedLock), "External lock fixture refused");
            NextStage(103);
        }
        else if (stage == 103)
        {
            if (elapsed < .12f) return;
            animator.CrossFade(recoveryState, .05f, 0);
            NextStage(104);
        }
        else if (stage == 104)
        {
            if (elapsed < .2f) return;
            Require(!presentation.IsActionActive && !bridge.IsScriptedPlanarMotionActive &&
                !bridge.IsCombatDirectionalEvasionFacing, "Interrupted dodge retained action resources");
            Require(bridge.IsExternalLockActive, "Dodge cleanup released unrelated lock");
            retainedLock.Dispose();
            retainedLock = null;
            NextStage(105);
        }
        else if (stage == 105)
        {
            if (elapsed < 1.5f) return;
            Require(mobility.TryDodgeImmediate(), "Next dodge refused after interruption and cooldown");
            presentation.CancelAction();
            Require(!bridge.IsExternalLockActive, "Cancelled dodge leaked movement lock");
            Require(bridge.Jump(Vector2.zero, false), "Next jump refused after interrupted dodge");
            jump.enabled = false;
            Require(!jump.IsActive, "Disabled jump retained active flag");
            jump.enabled = true;
            Finish("PASS interrupted jump -> dodge; interrupted dodge -> dodge/jump; unrelated lock preserved; disabled jump reset");
        }
    }

    private static void NextDirection(int next, LitOpsiveLocomotionBridge bridge)
    {
        bridge.SetExternalPositionAndRotation(startPosition, startRotation, true);
        var world = arena.Player.GetWorldSpaceInput(Directions[next - 1]);
        expectedDirection = new Vector3(world.x, 0, world.y).normalized;
        SetPad(new GamepadState { leftStick = Directions[next - 1], rightTrigger = 1 });
        NextStage(next);
    }

    private static void NextStage(int next)
    {
        stage = next;
        started = EditorApplication.timeSinceStartup;
        simulationStarted = Time.unscaledTime;
        simulationFrame = Time.frameCount;
    }

    private static void SetPad(GamepadState state)
    {
        InputSystem.QueueStateEvent(pad, state);
        InputSystem.Update();
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Finish(string result)
    {
        retainedLock?.Dispose();
        retainedLock = null;
        if (SessionState.GetBool(Key + ".MobilityRecovery", false) && arena != null && arena.Player != null)
            arena.Player.GetComponent<PlayerScriptedJumpController>().jumpStartTakeoffNormalizedTime = previousTakeoffTime;
        SessionState.SetBool(Key + ".MobilityRecovery", false);
        SessionState.SetBool(Key + ".SprintOnly", false);
        File.WriteAllText("Library/CombatFreeSprint.result", result);
        Debug.Log("[CombatFreeSprintValidation] " + result);
        if (pad != null) InputSystem.RemoveDevice(pad);
        if (pad != null)
        {
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            Application.runInBackground = previousRunInBackground;
        }
        if (arena != null && arena.Player != null)
            typeof(LitOpsiveLocomotionBridge).GetProperty("logCombatLockMotionDiagnostics", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(arena.Player.GetComponent<LitOpsiveLocomotionBridge>(), previousDiagnostics);
        pad = null;
        SessionState.SetBool(Key + ".Finished", true);
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(result.StartsWith("PASS", StringComparison.Ordinal) ? 0 : 1);
            return;
        }
        EditorApplication.isPlaying = false;
    }
}
