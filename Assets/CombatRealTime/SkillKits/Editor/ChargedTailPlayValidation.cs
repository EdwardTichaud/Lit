using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class ChargedTailPlayValidation
{
    private const string Key = "Lit.ChargedTail.PlayValidation";
    private const string ArmorKey = "Lit.ChargedTail.ArmorValidation";
    private static bool armorProbed;
    public static void RunArmorBatch() { scenario = 3; SessionState.SetBool(ArmorKey, true); RunBatch(); }
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int stage, scenario, basicGeneration, launchCount, tailFrames;
    private static double started;
    private static float pressedAt, releasedAt, lastProgress;
    private static float finalPoseAt, originalChargeSeconds;
    private static float caseReadyAt;
    private static Gamepad pad;
    private static BasicSkillsSO basic;
    private static ChargedSkillSO charged;
    private static bool seenTail;
    static ChargedTailPlayValidation() => EditorApplication.update += Tick;
    public static void RunFinalPoseBatch() { scenario = 6; RunBatch(); }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated editor only.");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }
    private static void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private static void Send(bool held) => InputSystem.QueueStateEvent(pad,
        held ? new GamepadState().WithButton(GamepadButton.West) : new GamepadState());
    private static void Observe(SkillSO skill, EnemyController target) { if (skill == charged) launchCount++; }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.isPaused = false; EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Check(EditorApplication.timeSinceStartup - started < 75, "Short charge tail validation timeout, stage=" + stage + " case=" + scenario);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena?.Player == null || arena.Enemy == null) return;
            var actor = arena.Player;
            var bridge = actor.GetComponent<LitOpsiveLocomotionBridge>();
            var action = actor.GetComponent<PlayerActionPresentationController>();
            var animator = actor.GetComponentInChildren<Animator>();
            var manager = arena.GetComponent<RealTimeCombatManager>();
            var input = arena.GetComponent<RealTimeCombatInput>();
            var skills = UnityEngine.Object.FindAnyObjectByType<SkillsManager>();
            if (!arena.Enemy.IsChargedAirborne) typeof(LitBrainsEnemy).GetField("hurtRemaining", Private).SetValue(arena.Enemy, 1000f);
            if (stage == 0)
            {
                typeof(SquadCharacterController).GetField("enableIdleSitting", Private).SetValue(actor, false);
                if (actor.IsInSittingCycle) { actor.TrySetSitting(false); return; }
                if (!bridge.Grounded || !arena.Enemy.NavigationReady) return;
                arena.Enemy.Health.SetHealth(1000, 1000);
                typeof(LitBrainsEnemy).GetField("nextDetection", Private).SetValue(arena.Enemy, float.PositiveInfinity);
                manager.BeginExternalEncounter(actor.transform, arena.Enemy, startEngaged: false);
                input.SetInputActive(true);
                charged = actor.CharacterData.chargedSkill;
                originalChargeSeconds = charged.chargeSeconds;
                manager.PlayerSkillStarted += Observe;
                pad = InputSystem.AddDevice<Gamepad>();
                caseReadyAt = Time.time;
                stage = 1;
            }
            if (stage == 1)
            {
                if (Time.time - caseReadyAt < .3f || !bridge.Grounded || action.IsActionActive || arena.Enemy.IsChargedAirborne || !arena.Enemy.NavigationReady) return;
                basic = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(ChargedSkillSetup.Folder + "/Clash/ClashBasic_" + scenario % 3 + ".asset");
                skills.SetGroundBasicSkillOverride(new[] { basic });
                var direction = Vector3.ProjectOnPlane(actor.transform.forward, Vector3.up).normalized;
                bridge.TeleportForSceneTransition(arena.Enemy.transform.position - direction * 1.8f + Vector3.up * .02f, Quaternion.LookRotation(direction));
                Physics.SyncTransforms();
                var cooldowns = (System.Collections.Generic.Dictionary<Transform, System.Collections.Generic.Dictionary<SkillSO, float>>)
                    typeof(RealTimeCombatManager).GetField("skillCooldowns", Private).GetValue(manager);
                if (cooldowns.TryGetValue(actor.transform, out var actorCooldowns)) actorCooldowns.Remove(charged);
                charged.chargeSeconds = scenario == 6 ? 8f : originalChargeSeconds;
                seenTail = false; tailFrames = 0; launchCount = 0; basicGeneration = 0; lastProgress = 0; finalPoseAt = 0;
                caseReadyAt = Time.time; stage = 8; return;
            }
            if (stage == 8)
            {
                if (Time.time - caseReadyAt < .15f || !bridge.Grounded) return;
                pressedAt = Time.unscaledTime; Send(true); stage = 2;
                Debug.Log("[ChargedTail] Press case=" + scenario + " game=" + Time.time + " grounded=" + bridge.Grounded); return;
            }
            if (stage == 2)
            {
                if (!action.IsBasicSkillAction) return;
                basicGeneration = action.ActionGeneration;
                Debug.Log("[ChargedTail] Basic started case=" + scenario + " generation=" + basicGeneration);
                stage = 3;
            }
            if (stage == 3)
            {
                Check(Time.unscaledTime - pressedAt < charged.chargeSeconds + 3f || launchCount > 0,
                    "Charge did not progress: held=" + typeof(RealTimeCombatInput).GetField("basicAttackHeld", Private).GetValue(input) +
                    " armed=" + typeof(RealTimeCombatInput).GetField("heldChargedSkill", Private).GetValue(input) +
                    " session=" + action.ActionGeneration + " active=" + action.IsActionActive + " tail=" + action.IsPreparingBasicTail);
                if (action.IsCurrentSession(basicGeneration) && action.IsBasicSkillAction)
                {
                    var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
                    Check(state.IsName(basic.AnimatorState), "Charge switched away from the actual Basic clip.");
                    Check(state.normalizedTime + .001f >= lastProgress, "Charge restarted the Basic clip.");
                    lastProgress = state.normalizedTime;
                    if (action.IsPreparingBasicTail)
                    {
                        seenTail = true;
                        if (animator.GetFloat(PlayerActionPresentationController.ChargeTailSpeedParameter) <= .11f) tailFrames++;
                        if (SessionState.GetBool(ArmorKey, false) && !armorProbed && tailFrames >= 4)
                        {
                            int hp = actor.CurrentHp;
                            actor.ApplyDamage(7, "ChargedTailArmorValidation");
                            action.InterruptBasicSkillForDamage();
                            Check(actor.CurrentHp == hp - 7, "Charge incorrectly prevented health damage.");
                            Check(action.IsCurrentSession(basicGeneration) && action.IsChargingSkill, "Damage interrupted charging.");
                            Check(!action.TryPlaySkill(basic, Animator.StringToHash(basic.AnimatorState)) &&
                                !action.TryPlayCombatState(basic.AnimatorState, basic.Presentation, "Charge replacement probe") &&
                                !action.TryReplaceWithDamageReaction(basic.AnimatorState, basic.Presentation) &&
                                !action.CancelActionForMobility(), "An ordinary action interrupted charging.");
                            Check(!action.LastRequestWasBuffered && action.PendingBasicForCharge == null, "A replacement was buffered during charge.");
                            armorProbed = true;
                            Debug.Log("[ChargedTailArmor] PASS | health damage applied; charge generation preserved; animation/mobility replacements refused");
                        }
                    }
                    if (scenario < 3 && tailFrames >= 4)
                    {
                        Check(Time.unscaledTime - pressedAt < charged.chargeSeconds, "Release fixture reached launch threshold.");
                        Send(false); releasedAt = Time.unscaledTime; stage = 4;
                    }
                    if (scenario == 6 && state.normalizedTime >= 1f)
                    {
                        if (finalPoseAt == 0) finalPoseAt = Time.unscaledTime;
                        if (Time.unscaledTime - finalPoseAt > .05f)
                            Check(animator.GetFloat(PlayerActionPresentationController.ChargeTailSpeedParameter) == 0, "Final pose was not held.");
                        if (Time.unscaledTime - finalPoseAt > 1.2f)
                        { Send(false); releasedAt = Time.unscaledTime; stage = 4; }
                    }
                }
                if (scenario >= 3 && scenario < 6 && launchCount > 0)
                {
                    Check(seenTail && tailFrames > 0 && launchCount == 1, "Launch skipped slowed tail or repeated.");
                    Check(Time.unscaledTime - pressedAt >= charged.chargeSeconds, "Charged skill started early.");
                    Check(Mathf.Approximately(animator.GetFloat(PlayerActionPresentationController.ChargeTailSpeedParameter), 1), "Charged launch inherited Basic slowdown.");
                    Send(false); stage = 5;
                }
                return;
            }
            if (stage == 4)
            {
                if (Time.unscaledTime - releasedAt < .12f) return;
                Check(launchCount == 0, "Early release launched a ChargedSkill.");
                Check(Mathf.Approximately(animator.GetFloat(PlayerActionPresentationController.ChargeTailSpeedParameter), 1), "Release did not restore speed.");
                if (action.IsActionActive) return;
                Check(seenTail, "Release case never entered the Basic tail.");
                stage = 5;
            }
            if (stage != 5 || action.IsActionActive || !bridge.Grounded || arena.Enemy.IsChargedAirborne || !arena.Enemy.NavigationReady) return;
            Check(!bridge.IsExternalLockActive && !bridge.IsInputSuppressedByUcc && Mathf.Approximately(Time.timeScale, 1), "Tail retained movement/input/time resources.");
            Check(Mathf.Approximately(animator.GetFloat(PlayerActionPresentationController.ChargeTailSpeedParameter), 1), "Tail speed remained after completion.");
            var director = UnityEngine.Object.FindAnyObjectByType<LitCameraDirector>();
            Check(director == null || !director.IsChargeFramingActive, "Charge camera remained active after release.");
            Debug.Log("[ChargedTail] PLAY PASS | basic=" + (scenario % 3 + 1) + " release=" + (scenario < 3 || scenario == 6) + " launches=" + launchCount + " slowedFrames=" + tailFrames + " finalPose=" + (finalPoseAt != 0));
            if (SessionState.GetBool(ArmorKey, false))
            {
                Check(armorProbed && launchCount == 1, "Protected charge did not complete exactly once.");
                scenario = 6;
                SessionState.SetBool(ArmorKey, false);
            }
            if (++scenario < 7) { caseReadyAt = Time.time; stage = 1; return; }
            charged.chargeSeconds = originalChargeSeconds;
            manager.PlayerSkillStarted -= Observe;
            skills.SetGroundBasicSkillOverride(null);
            InputSystem.RemoveDevice(pad);
            SessionState.SetBool(Key, false); EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            if (charged != null) charged.chargeSeconds = originalChargeSeconds;
            Debug.LogException(error);
            SessionState.SetBool(Key, false); EditorApplication.Exit(1);
        }
    }
}
