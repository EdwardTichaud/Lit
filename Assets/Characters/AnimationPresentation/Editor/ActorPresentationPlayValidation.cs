using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Records obtained movement and actual skinned soles. No corrective movement or pose writes.</summary>
[InitializeOnLoad]
public static class ActorPresentationPlayValidation
{
    private const string Key = "Lit.ActorPresentationPlayValidation";
    private static LucianBrainsCombatArena arena;
    private static Gamepad pad;
    private static int stage;
    private static double started;
    private static Vector3 pausePosition;
    private static float pausePhase;
    private static bool previousOptionsEnabled;
    private static EnterPlayModeOptions previousOptions;
    private static Camera inspection;
    private static readonly Vector2[] Directions = {Vector2.up,new Vector2(1,1).normalized,Vector2.right,new Vector2(1,-1).normalized,
        Vector2.down,new Vector2(-1,-1).normalized,Vector2.left,new Vector2(-1,1).normalized};
    static ActorPresentationPlayValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated validation project.");
        stage = 0; arena = null; pad = null; inspection = null;
        LocomotionPresentationCalibration.PrepareLaboratoryCandidates();
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        SessionState.SetBool(Key,true); EditorApplication.isPlaying = true;
    }
    public static void RunWithoutDomainReload()
    {
        previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        previousOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        SessionState.SetBool(Key + ".NoReload",true); Run();
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (arena == null) { arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>(); started = EditorApplication.timeSinceStartup; }
            if (arena == null || arena.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            var bridge = arena.Player.GetComponent<LitOpsiveLocomotionBridge>();
            var animator = arena.Player.GetComponent<CharacterAnimationController>().Animator;
            double elapsed = EditorApplication.timeSinceStartup - started;
            if (elapsed > 12) throw new InvalidOperationException("Movement validation timed out at " + stage);
            if (stage == 0)
            {
                arena.Enemy.canAttack = false; arena.Enemy.enabled = false;
                arena.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped = true;
                arena.Player.ApplyFlameState(arena.Player.FlameSecondsRemaining,false);
                arena.Player.gameObject.AddComponent<ActorPresentationRecorder>().authority = "UCC";
                arena.Enemy.gameObject.AddComponent<ActorPresentationRecorder>().authority = "NavMeshAgent";
                inspection = new GameObject("Locomotion inspection",typeof(Camera)).GetComponent<Camera>();
                inspection.CopyFrom(arena.arenaCamera); inspection.enabled = false; inspection.targetTexture = null; inspection.fieldOfView = 38;
                ActorAnimationLabValidation.ConfigureInspectionCamera(inspection);
                Directory.CreateDirectory(ActorAnimationAudit.ReportFolder + "/locomotion-frames");
                pad = InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(pad,new GamepadState {leftStick=Directions[0]});
                Next(1); return;
            }
            if (stage <= 16)
            {
                bridge.SetSprintModifier(stage > 8);
                if (elapsed < .65) return;
                if (stage == 1)
                {
                    var monitor = arena.Player.GetComponentInChildren<LitPresentationAnimatorMonitor>();
                    if (monitor == null || !bridge.IsMeasuredLocomotionPresentationActive)
                        throw new InvalidOperationException("Measured locomotion has no exclusive parameter owner.");
                    float x = animator.GetFloat("HorizontalMovement"), y = animator.GetFloat("ForwardMovement");
                    if (monitor.SetHorizontalMovementParameter(.123f,1,0) || monitor.SetForwardMovementParameter(.456f,1,0) ||
                        animator.GetFloat("HorizontalMovement") != x || animator.GetFloat("ForwardMovement") != y)
                        throw new InvalidOperationException("UCC raw input overwrote the measured presentation direction.");
                }
                Capture(stage);
                if (animator.applyRootMotion || bridge.IsCinematicMotionSessionActive)
                    throw new InvalidOperationException("Unexpected Root/cinematic authority during locomotion.");
                stage++; started = EditorApplication.timeSinceStartup;
                if (stage <= 16) InputSystem.QueueStateEvent(pad,new GamepadState {leftStick=Directions[(stage-1)%8]});
                else
                {
                    arena.Player.ApplyFlameState(arena.Player.FlameSecondsRemaining,true);
                    InputSystem.QueueStateEvent(pad,new GamepadState {leftStick=Vector2.up});
                }
                return;
            }
            if (stage == 17 && elapsed > .7)
            {
                Capture(stage);
                InputSystem.QueueStateEvent(pad,new GamepadState()); Next(18); return;
            }
            if (stage == 18 && elapsed > .8)
            {
                Capture(stage);
                RealTimeCombatManager.Instance.EndCombat();
                InputSystem.QueueStateEvent(pad,new GamepadState {leftStick=Vector2.up});
                bridge.SetSprintModifier(false); Next(19); return;
            }
            if (stage == 19 && elapsed > .7)
            {
                Capture(stage);
                pausePosition = arena.Player.transform.position;
                pausePhase = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                arena.SetPaused(true); Next(20); return;
            }
            if (stage == 20 && elapsed > .25)
            {
                if (Vector3.Distance(pausePosition,arena.Player.transform.position) > .01f ||
                    Mathf.Abs(pausePhase - animator.GetCurrentAnimatorStateInfo(0).normalizedTime) > .02f)
                    throw new InvalidOperationException("Pause moved the actor or advanced its cycle.");
                arena.SetPaused(false); Next(21); return;
            }
            if (stage == 21 && elapsed > .6)
            {
                Capture(stage);
                InputSystem.QueueStateEvent(pad,new GamepadState()); bridge.SetSprintModifier(false);
                Next(22); return;
            }
            if (stage == 22 && elapsed > .6)
                Finish("PASS runtime locomotion path, eight directions, walk/run, torch, exploration, pause and held-input restoration. Sole traces require measured/visual acceptance.",0);
        }
        catch (Exception error) { Debug.LogException(error); Finish(error.ToString(),1); }
    }
    private static void Capture(int index)
    {
        var actor = arena.Player.transform;
        var focus = actor.position + Vector3.up * .85f;
        var preferred = actor.forward * 3.2f + actor.right * 1.8f;
        float bestDistance = 0; Vector3 bestDirection = Vector3.forward;
        // Inspection only: avoid producing a wall image when the movement trial reaches a boundary.
        for (int angle = 0; angle < 8; angle++)
        {
            var offset = Quaternion.AngleAxis(angle * 45,Vector3.up) * preferred + Vector3.up * .45f;
            float distance = offset.magnitude; var direction = offset.normalized;
            foreach (var hit in Physics.SphereCastAll(focus,.1f,direction,distance,LayerMask.GetMask("Default","Ground","Obstacle","CameraObstruction"),QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(actor)) distance = Mathf.Min(distance,Mathf.Max(.3f,hit.distance-.15f));
            if (distance > bestDistance) { bestDistance=distance; bestDirection=direction; }
            if (distance >= offset.magnitude-.01f) break;
        }
        inspection.fieldOfView = Mathf.Clamp(38 * 3.7f / Mathf.Max(.8f,bestDistance),38,100);
        inspection.transform.position = focus + bestDirection * bestDistance;
        inspection.transform.LookAt(focus);
        ActorAnimationLabValidation.RenderSnapshot(inspection,ActorAnimationAudit.ReportFolder + "/locomotion-frames/stage-" + index.ToString("D2") + ".png");
    }
    private static void Next(int value) { stage = value; started = EditorApplication.timeSinceStartup; }
    private static void Finish(string result,int code)
    {
        SessionState.SetBool(Key,false);
        if (pad != null) InputSystem.RemoveDevice(pad);
        foreach (var recorder in UnityEngine.Object.FindObjectsByType<ActorPresentationRecorder>(FindObjectsSortMode.None)) recorder.enabled = false;
        Directory.CreateDirectory(ActorAnimationAudit.ReportFolder);
        string suffix = SessionState.GetBool(Key + ".NoReload",false) ? "no-reload" : "reload";
        File.WriteAllText(ActorAnimationAudit.ReportFolder + "/play-" + suffix + "-result.txt",result);
        if (SessionState.GetBool(Key + ".NoReload",false))
        { EditorSettings.enterPlayModeOptionsEnabled = previousOptionsEnabled; EditorSettings.enterPlayModeOptions = previousOptions; }
        Debug.Log(result); EditorApplication.Exit(code);
    }
}
