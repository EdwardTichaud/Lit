using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Runs the real Lucian prefab and UCC in an isolated batch Editor.</summary>
[InitializeOnLoad]
public static class LucianBrainsCombatSmoke
{
    private const string Key = "LucianBrainsCombatSmoke.Running";
    private static LucianBrainsCombatArena arena;
    private static Gamepad pad;
    private static int stage, before;
    private static double started;
    private static Vector3 initialPosition, enemyInitialPosition;
    private static Quaternion initialPlayerFoot, initialEnemyFoot;
    private static Quaternion stunFacing;
    private static bool knockoutSignalObserved;
    private static bool cameraShakeObserved;
    static LucianBrainsCombatSmoke() { EditorApplication.update += Tick; }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use a separate batch Editor for this smoke test.");
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    public static void RunDefenses()
    {
        Run();
        SessionState.SetBool(Key + ".Defenses", true);
    }

    public static void RunKnockoutSignal()
    {
        RunDefenses();
        SessionState.SetBool(Key + ".KnockoutSignal", true);
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (arena == null)
            {
                arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
                Require(arena != null, "Combat lab missing.");
                started = EditorApplication.timeSinceStartup;
            }
            double elapsed = EditorApplication.timeSinceStartup - started;
            if (elapsed > 20) throw new InvalidOperationException("Timed out at stage " + stage + " | timeScale=" + Time.timeScale + " | mode=" + InputModeCoordinator.CurrentMode + " | target=" + arena.Enemy.DetectionReason + " | enemy=" + arena.Enemy.transform.position + " | player=" + arena.Player.transform.position + " | attack=" + arena.Enemy.IsAttackCommitted + " | health=" + arena.Player.CurrentHp);
            switch (stage)
            {
                case 0:
                    if (SessionState.GetBool(Key + ".Defenses", false) && arena.Enemy != null)
                    {
                        // This focused path starts with pause checks, not a defeat/restart.
                        // Keep autonomous attacks off until the counter setup in stage 23.
                        arena.Enemy.enabled = false;
                        var defenseAgent = arena.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (defenseAgent.isOnNavMesh) defenseAgent.isStopped = true;
                    }
                    if (elapsed < 2) return;
                    Require(arena.Player != null && arena.Enemy != null, "Actors not initialized.");
                    Require(arena.Player.GetComponent<LitOpsiveLocomotionBridge>().IsDriving, "UCC not driving.");
                    Require(arena.Player.CurrentHp == arena.playerMaxHealth, "Health authority mismatch.");
                    Require(arena.Enemy.NavigationReady, "Juggernaut NavMesh missing.");
                    Require(LocalPlayerContext.LocalCharacterRoot == arena.Player.transform, "Local player context missing.");
                    pad = InputSystem.AddDevice<Gamepad>();
                    if (SessionState.GetBool(Key + ".KnockoutSignal", false))
                    {
                        var skill = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CounterSkillSO>(IluviliraeSetup.Folder + "/CombatLab_CounterSkill.asset"));
                        var timeline = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.TimelineAsset>(CounterKnockoutSignalSetup.TimelinePath));
                        // Test-only tail allows us to observe the signal before graph release.
                        timeline.fixedDuration = 4;
                        var skillData = new SerializedObject(skill);
                        skillData.FindProperty("timeline").objectReferenceValue = timeline;
                        skillData.ApplyModifiedPropertiesWithoutUndo();
                        typeof(CounterSkillCombatController).GetField("defaultCounterSkill", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(CounterSkillCombatController.Instance, skill);
                    }
                    if (SessionState.GetBool(Key + ".Defenses", false)) { Next(7); break; }
                    initialPosition = arena.Player.transform.position;
                    enemyInitialPosition = arena.Enemy.transform.position;
                    initialPlayerFoot = arena.Player.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftFoot).localRotation;
                    initialEnemyFoot = arena.Enemy.Animator.GetBoneTransform(HumanBodyBones.LeftFoot).localRotation;
                    InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1 });
                    Next(40); break;
                case 40:
                    if (elapsed < .2) return;
                    Require(LocalInputRouter.RightShoulderPressed, "RT did not enable sprint in combat.");
                    Require(UnityEngine.Object.FindAnyObjectByType<SkillWheel>().GetComponent<CanvasGroup>().alpha < .5f, "RT unexpectedly opened the skill wheel.");
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
                    Next(41); break;
                case 41:
                    if (elapsed < .2) return;
                    Require(!LocalInputRouter.RightShoulderPressed, "Releasing RT did not clear sprint.");
                    Require(LocalInputRouter.CameraZoomValue < -.5f, "RB did not route camera zoom-in.");
                    Require(UnityEngine.Object.FindAnyObjectByType<SkillWheel>().GetComponent<CanvasGroup>().alpha < .5f, "RB unexpectedly opened the skill wheel.");
                    InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right });
                    Next(42); break;
                case 42:
                    if (elapsed < .2) return;
                    Require(!LocalInputRouter.RightShoulderPressed, "Full left stick unexpectedly enabled sprint without RT.");
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadDown));
                    Next(43); break;
                case 43:
                    if (elapsed < .2) return;
                    Require(!RealTimeCombatManager.Instance.HasLockedCombatTarget, "D-pad down did not release the laboratory lock.");
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    Next(44); break;
                case 44:
                    if (elapsed < .2) return;
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadDown));
                    Next(45); break;
                case 45:
                    if (elapsed < .2) return;
                    Require(RealTimeCombatManager.Instance.HasLockedCombatTarget, "D-pad down did not restore the laboratory lock.");
                    initialPosition = arena.Player.transform.position;
                    InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right });
                    Next(1); break;
                case 1:
                    if (elapsed < 1) return;
                    Require(Vector3.Distance(initialPosition, arena.Player.transform.position) > 0.3f, "Gamepad does not move Lucian.");
                    var animator = arena.Player.GetComponent<Animator>();
                    Debug.Log("Locomotion player=" + animator.GetCurrentAnimatorStateInfo(0).shortNameHash + " mag=" + animator.GetFloat("CombatMoveMagnitude") + " enemySpeed=" + arena.Enemy.Animator.GetFloat("Speed"));
                    Require(animator.GetFloat("CombatMoveMagnitude") > .05f, "Lucian combat locomotion parameters missing.");
                    Require(Quaternion.Angle(initialPlayerFoot, animator.GetBoneTransform(HumanBodyBones.LeftFoot).localRotation) > .1f, "Lucian foot stayed static while moving.");
                    Require(Vector3.Distance(enemyInitialPosition, arena.Enemy.transform.position) > .1f, "Enemy did not pursue moving player.");
                    Require(Quaternion.Angle(initialEnemyFoot, arena.Enemy.Animator.GetBoneTransform(HumanBodyBones.LeftFoot).localRotation) > .1f, "Juggernaut foot stayed static while pursuing.");
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    arena.Enemy.enabled = false;
                    var agent = arena.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    agent.isStopped = true; agent.ResetPath();
                    arena.Enemy.GetComponent<Animator>().Play("Locomotion");
                    var bridge = arena.Player.GetComponent<LitOpsiveLocomotionBridge>();
                    bridge.TeleportForSceneTransition(arena.Enemy.transform.position + arena.Enemy.transform.forward * 2,
                        Quaternion.LookRotation(-arena.Enemy.transform.forward));
                    before = arena.Enemy.CurrentHealth;
                    Next(2); break;
                case 2:
                    if (elapsed < 0.4) return;
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.West));
                    Next(3); break;
                case 3:
                    if (elapsed < 1.7) return;
                    Require(arena.Enemy.CurrentHealth < before, "Authored player impact did not damage Juggernaut.");
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    arena.Player.GetComponent<PlayerActionPresentationController>().CancelAction();
                    arena.Player.GetComponent<LitOpsiveLocomotionBridge>().TeleportForSceneTransition(arena.Enemy.transform.position + arena.Enemy.transform.forward * 2, Quaternion.LookRotation(-arena.Enemy.transform.forward));
                    Physics.SyncTransforms();
                    before = arena.Player.CurrentHp;
                    arena.ResolveEnemyContact();
                    Require(arena.Player.CurrentHp == before - arena.enemyDamage, "Juggernaut contact did not use UCC health.");
                    Next(4); break;
                case 4:
                    if (elapsed < 1.5) return;
                    arena.Player.GetComponent<LitOpsiveLocomotionBridge>().TeleportForSceneTransition(
                        arena.Enemy.transform.position + arena.Enemy.transform.forward * 2,
                        Quaternion.LookRotation(-arena.Enemy.transform.forward));
                    before = arena.Player.CurrentHp;
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North));
                    Next(9); break;
                case 9:
                    if (elapsed < .2) return;
                    Require(CounterSkillCombatController.Instance.IsGuardHeld, "Y did not start guard.");
                    arena.ResolveEnemyContact();
                    Require(arena.Player.CurrentHp == before - CounterSkillCombatController.ModifyGuardDamage(arena.enemyDamage, false), "Guard damage differs from game.");
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    Next(10); break;
                case 10:
                    if (elapsed < .25) return;
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East));
                    initialPosition = arena.Player.transform.position;
                    Next(11); break;
                case 11:
                    if (elapsed < .12) return;
                    Require(arena.Player.GetComponent<PlayerScriptedDodgeController>().IsActive, "B did not start dodge.");
                    before = arena.Player.CurrentHp;
                    arena.ResolveEnemyContact();
                    Require(arena.Player.CurrentHp == before, "Dodge invulnerability did not avoid contact.");
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    Next(12); break;
                case 12:
                    if (elapsed < .8) return;
                    Require(Vector3.Distance(initialPosition, arena.Player.transform.position) > .2f, "Dodge did not move Lucian.");
                    InputSystem.QueueStateEvent(pad, new GamepadState { leftTrigger = 1 });
                    Next(13); break;
                case 13:
                    if (elapsed < .2) return;
                    var wheel = UnityEngine.Object.FindAnyObjectByType<SkillWheel>();
                    Require(wheel != null && wheel.GetComponent<CanvasGroup>().alpha > .5f, "LT did not open authored skill wheel.");
                    InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.up, leftTrigger = 1 });
                    Next(14); break;
                case 14:
                    if (elapsed < .15) return;
                    Require(UnityEngine.Object.FindAnyObjectByType<SkillWheel>().GetSkill(0) != null, "Lucian loadout not bound to wheel.");
                    InputSystem.QueueStateEvent(pad, new GamepadState { leftTrigger = 1 }.WithButton(GamepadButton.South));
                    Next(15); break;
                case 15:
                    if (elapsed < .2) return;
                    Require(RealTimeCombatManager.Instance.IsPlayerActionActive || RealTimeCombatManager.Instance.IsCinematicSequenceActive, "A did not confirm selected skill.");
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    arena.GetComponent<CombatSkillCinematicController>()?.AbortForActionTermination();
                    arena.Player.GetComponent<PlayerActionPresentationController>().CancelAction();
                    Next(16); break;
                case 16:
                    if (elapsed < .5) return;
                    // Enough earned clarity for the authored LightSkill; do not bypass its eligibility.
                    RealTimeCombatManager.Instance.RefundClarity(100);
                    arena.Player.GetComponent<LitOpsiveLocomotionBridge>().TeleportForSceneTransition(
                        arena.Enemy.transform.position + arena.Enemy.transform.forward * 8, Quaternion.LookRotation(-arena.Enemy.transform.forward));
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.LeftStick));
                    Next(17); break;
                case 17:
                    if (elapsed < .3) return;
                    Debug.Log("L3 diagnostics: " + arena.GetComponent<RealTimeCombatInput>().InputDiagnostics + " action=" + LocalPlayerInput.FindSharedActionMap("RealTimeCombat").FindAction("LightSkill").ReadValue<float>());
                    Require(arena.GetComponent<LightSkillCombatController>().IsCinematicPlaying, "L3 did not launch Lucian LightSkill.");
                    Require(RealTimeCombatManager.Instance.IsCinematicSequenceActive, "LightSkill does not own combat suspension.");
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    Next(18); break;
                case 18:
                    if (arena.GetComponent<LightSkillCombatController>().IsCinematicPlaying) return;
                    Require(!RealTimeCombatManager.Instance.IsCinematicSequenceActive, "LightSkill left cinematic ownership active.");
                    Require(InputModeCoordinator.CurrentMode == InputMode.Combat, "LightSkill did not restore combat inputs.");
                    arena.Enemy.TakeDamage(arena.Enemy.CurrentHealth);
                    arena.Enemy.enabled = true;
                    Next(5); break;
                case 5:
                    if (elapsed < 4.5) return;
                    Require(arena.Enemy.IsDead && !arena.Enemy.gameObject.activeSelf, "Enemy death did not finish/deactivate.");
                    arena.RestartEncounter();
                    Next(6); break;
                case 6:
                    if (elapsed < 1) return;
                    Require(arena.Player.CurrentHp == arena.playerMaxHealth && !arena.Enemy.IsDead && arena.Enemy.NavigationReady,
                        "Restart did not restore the encounter.");
                    arena.Player.GetComponent<LitOpsiveLocomotionBridge>().TeleportForSceneTransition(
                        arena.Enemy.transform.position + arena.Enemy.transform.forward * 2,
                        Quaternion.LookRotation(-arena.Enemy.transform.forward));
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
                    Next(19); break;
                case 19:
                    if (elapsed < .15) return;
                    Require(arena.Player.GetComponent<PlayerScriptedJumpController>().IsActive || !arena.Player.GetComponent<LitOpsiveLocomotionBridge>().Grounded, "A did not start jump.");
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    Next(20); break;
                case 20:
                    if (!arena.Player.GetComponent<LitOpsiveLocomotionBridge>().Grounded || arena.Player.GetComponent<PlayerScriptedJumpController>().IsActive) return;
                    arena.Player.GetComponent<LitOpsiveLocomotionBridge>().TeleportForSceneTransition(
                        arena.Enemy.transform.position + arena.Enemy.transform.forward * 2, Quaternion.LookRotation(-arena.Enemy.transform.forward));
                    Physics.SyncTransforms();
                    before = arena.Player.CurrentHp;
                    Next(8); break;
                case 8:
                    if (arena.Player.CurrentHp >= before) return;
                    arena.Player.ApplyDamage(arena.playerMaxHealth, "Smoke defeat");
                    Require(arena.EncounterOver, "Player defeat did not end combat.");
                    arena.RestartEncounter();
                    Next(7); break;
                case 7:
                    if (elapsed < 1) return;
                    Require(arena.Player.CurrentHp == arena.playerMaxHealth && !arena.EncounterOver, "Restart after defeat failed.");
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Start));
                    Next(21); break;
                case 21:
                    if (elapsed < .15) return;
                    Require(arena.IsPaused && Time.timeScale == 0 && InputModeCoordinator.CurrentMode == InputMode.UserInterface, "Start did not pause combat | paused=" + arena.IsPaused + " scale=" + Time.timeScale + " mode=" + InputModeCoordinator.CurrentMode + " playerMap=" + LocalPlayerInput.FindSharedActionMap("Player").enabled);
                    initialPosition = arena.Player.transform.position;
                    enemyInitialPosition = arena.Enemy.transform.position;
                    InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right });
                    Next(22); break;
                case 22:
                    if (elapsed < .3) return;
                    Require(Vector3.Distance(initialPosition, arena.Player.transform.position) < .05f && Vector3.Distance(enemyInitialPosition, arena.Enemy.transform.position) < .05f, "Actors moved during pause.");
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East));
                    Next(23); break;
                case 23:
                    if (elapsed < .15) return;
                    Require(!arena.IsPaused && InputModeCoordinator.CurrentMode == InputMode.Combat && Time.timeScale > 0, "B did not resume combat.");
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    arena.Enemy.enabled = false;
                    arena.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped = true;
                    Require(arena.Enemy.PlaceForCinematic(new Vector3(5, 0, 5), Quaternion.Euler(0, 123, 0)), "Counter setup cannot be placed away from the world origin.");
                    arena.Player.GetComponent<LitOpsiveLocomotionBridge>().TeleportForSceneTransition(
                        arena.Enemy.transform.position + arena.Enemy.transform.forward * 2, Quaternion.LookRotation(-arena.Enemy.transform.forward));
                    var reactionSettings = new SerializedObject(arena.GetComponent<CombatHealthThresholdController>());
                    reactionSettings.FindProperty("logEnemyReactions").boolValue = true;
                    reactionSettings.ApplyModifiedPropertiesWithoutUndo();
                    arena.Enemy.ReactionOpportunity += QueueCounter;
                    if (arena.Enemy.combatProfile != null)
                    {
                        // The profile owns committed attacks; let its decision path open the reaction window.
                        arena.Enemy.enabled = true;
                        arena.Enemy.canAttack = true;
                        typeof(LitBrainsEnemy).GetField("fluidCooldown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            .SetValue(arena.Enemy, 0f);
                        arena.Enemy.Animator.Play("Locomotion", 0, 0);
                    }
                    else arena.Enemy.Animator.Play("Attack_Sweep", 0, 0);
                    arena.Enemy.Animator.Update(0);
                    before = arena.Enemy.CurrentHealth;
                    Next(24); break;
                case 24:
                    // The authored animation event opens the real reaction window.
                    break;
                case 25:
                    if (elapsed < .1) return;
                    Require(CounterSkillCombatController.Instance.IsCinematicPlaying, "Y in reaction window did not start CounterSkill | " + ReactionDiagnostics());
                    Require(Mathf.Abs(Vector3.Distance(arena.Player.transform.position, arena.Enemy.transform.position) - 1.7f) < .15f,
                        "Counter actors did not retain canonical spacing after Timeline evaluation.");
                    InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right });
                    Next(26); break;
                case 26:
                    if (CounterSkillCombatController.Instance.IsCinematicPlaying)
                    {
                        if (SessionState.GetBool(Key + ".KnockoutSignal", false))
                        {
                            var rig = UnityEngine.Object.FindAnyObjectByType<CombatCinematicRig>();
                            var director = rig.GetComponent<UnityEngine.Playables.PlayableDirector>();
                            foreach (var binding in rig.CameraBindings)
                                if (binding.camera != null && binding.camera.State.PositionCorrection.sqrMagnitude > .000001f)
                                    cameraShakeObserved = true;
                            if (director.time > 1.95) Require(cameraShakeObserved, "CameraShake signals did not reach the Cinemachine camera pipeline.");
                            if (director.time < 3.45) Require(!arena.Enemy.IsCounterStunned, "Knockout started before its signal.");
                            if (director.time > 3.55)
                            {
                                Require(arena.Enemy.IsCounterStunned && StunVfxCount() == 1, "3.50s signal failed before graph release.");
                                knockoutSignalObserved = true;
                            }
                        }
                        return;
                    }
                    if (SessionState.GetBool(Key + ".KnockoutSignal", false)) Require(knockoutSignalObserved, "Signal never observed while Timeline owned actors.");
                    else Require(arena.Enemy.CurrentHealth < before, "CounterSkill impact did not damage Brains target.");
                    Require(arena.Enemy.IsCounterStunned && !arena.Enemy.IsAttackCommitted, "CounterSkill did not leave a stunned enemy.");
                    Require(!RealTimeCombatManager.Instance.IsCinematicSequenceActive && InputModeCoordinator.CurrentMode == InputMode.Combat && Time.timeScale > 0, "CounterSkill did not restore controls and time.");
                    initialPosition = arena.Player.transform.position;
                    enemyInitialPosition = arena.Enemy.transform.position;
                    stunFacing = arena.Enemy.transform.rotation;
                    Next(28); break;
                case 28:
                    if (elapsed < .12) return;
                    Require(Vector3.Distance(initialPosition, arena.Player.transform.position) > .03f, "Held movement failed to resume after CounterSkill.");
                    Require(arena.Enemy.Animator.GetCurrentAnimatorStateInfo(0).IsName("Knocked Out"), "Counter stun did not play Knocked Out.");
                    Require(StunVfxCount() == 1, "Counter stun did not instantiate exactly one VFX on the character.");
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    arena.Enemy.GetComponent<AnimationEvents>().KnockedOut();
                    Require(StunVfxCount() == 1, "Repeated knockout duplicated its effect.");
                    Require(arena.Enemy.ReceiveImpact(new CombatImpact(1, 100, CombatImpactOrigin.Basic)) == 1, "Stunned enemy rejected damage.");
                    Next(29); break;
                case 29:
                    if (elapsed < 1.65) return;
                    Require(arena.Enemy.IsCounterStunned && arena.Enemy.Animator.GetCurrentAnimatorStateInfo(0).IsName("Knocked Out"), "Stun ended early after another impact.");
                    Require(StunVfxCount() == 1, "An additional hit removed or duplicated the stun VFX.");
                    Require(Vector3.Distance(enemyInitialPosition, arena.Enemy.transform.position) < .03f && Quaternion.Angle(stunFacing, arena.Enemy.transform.rotation) < .5f,
                        "Stunned enemy moved or rotated.");
                    Next(30); break;
                case 30:
                    if (elapsed < .5) return;
                    Require(!arena.Enemy.IsCounterStunned && !arena.Enemy.Animator.GetCurrentAnimatorStateInfo(0).IsName("Knocked Out"), "Enemy did not recover after two seconds.");
                    Require(StunVfxCount() == 0, "Stun VFX survived recovery.");
                    arena.Enemy.ReceiveImpact(new CombatImpact(1, 100, CombatImpactOrigin.Counter));
                    arena.Enemy.GetComponent<AnimationEvents>().KnockedOut();
                    Require(StunVfxCount() == 1, "A second stun did not recreate its VFX.");
                    arena.Enemy.ReceiveImpact(new CombatImpact(arena.Enemy.CurrentHealth, 100, CombatImpactOrigin.Counter));
                    Require(arena.Enemy.IsDead && !arena.Enemy.IsCounterStunned, "A lethal counter must override stun.");
                    Require(StunVfxCount() == 0, "Stun VFX survived death.");
                    arena.enabled = false;
                    arena.enabled = true;
                    Next(27); break;
                case 27:
                    if (elapsed < 1) return;
                    Require(arena.Player != null && arena.Player.CurrentHp == arena.playerMaxHealth && !arena.Enemy.IsDead && arena.Enemy.NavigationReady, "Disable/enable failed to recreate clean encounter.");
                    Finish(SessionState.GetBool(Key + ".Defenses", false) ? "PASS gamepad pause/resume, CounterSkill authored reaction/impact/restoration, disable/enable" : "PASS gamepad bindings, Lucian/Juggernaut bone animation, authored impact, UCC health, guard, dodge, palette, LightSkill, CounterSkill, pause, automatic enemy contact, death, victory/defeat restart, disable/enable", 0); break;
            }
        }
        catch (Exception e) { Debug.LogException(e); Finish("FAIL " + e, 1); }
    }

    private static string ReactionDiagnostics()
    {
        var reaction = arena.GetComponent<CombatHealthThresholdController>();
        string result = "input=" + arena.GetComponent<RealTimeCombatInput>().InputDiagnostics + " attack=" + arena.Enemy.IsAttackCommitted + " id=" + arena.Enemy.ActionSequenceId;
        foreach (string field in new[] { "attackQteActive", "attackQteActionId", "reactionClock", "counterDeadline", "counterAwaitingRelease" })
            result += " " + field + "=" + typeof(CombatHealthThresholdController).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(reaction);
        return result;
    }

    private static int StunVfxCount()
    {
        Require(arena.Enemy.VFX_KnockedOut != null, "Stun VFX prefab reference missing.");
        int count = 0;
        foreach (var child in arena.Enemy.GetComponentsInChildren<Transform>())
            if (child.name == arena.Enemy.VFX_KnockedOut.name + "(Clone)") count++;
        return count;
    }

    private static void QueueCounter(AnimationEvent source)
    {
        Debug.Log("Reaction event diagnostics: " + ReactionDiagnostics());
        arena.Enemy.ReactionOpportunity -= QueueCounter;
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North));
        Next(25);
    }

    private static void Next(int next) { stage = next; started = EditorApplication.timeSinceStartup; }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Finish(string result, int code)
    {
        SessionState.SetBool(Key, false);
        SessionState.SetBool(Key + ".Defenses", false);
        SessionState.SetBool(Key + ".KnockoutSignal", false);
        File.WriteAllText("LucianBrainsCombatSmoke.result", result);
        Debug.Log(result);
        if (pad != null) InputSystem.RemoveDevice(pad);
        EditorApplication.Exit(code);
    }
}
