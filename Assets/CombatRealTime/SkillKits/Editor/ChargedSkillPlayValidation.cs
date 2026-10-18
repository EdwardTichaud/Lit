using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class ChargedSkillPlayValidation
{
    private const string Key = "Lit.ChargedSkill.Validation";
    private const string PreparationArmorKey = "Lit.ChargedSkill.PreparationArmor";
    private static int stage, before, casts, chargeGeneration;
    private static int cycles;
    private static int orphanHold;
    private static bool fixtureFrozen;
    private static bool initialChargeDamageTested;
    private static float at, gameAt, playerStart, enemyStart, peakPlayer, peakEnemy;
    private static double started;
    private static Gamepad pad;
    static ChargedSkillPlayValidation() => EditorApplication.update += Tick;
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated editor only.");
        ChargedSkillSetup.Configure();
        RunRuntimeBatch();
    }
    public static void RunRuntimeBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated editor only.");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }
    public static void RunPreparationArmorBatch()
    {
        SessionState.SetBool(PreparationArmorKey, true);
        RunRuntimeBatch();
    }
    public static void RunFinalPoseBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated editor only.");
        ChargedSkillSetup.Configure();
        var skill = UnityEditor.AssetDatabase.LoadAssetAtPath<ChargedSkillSO>(ChargedSkillSetup.SkillPath);
        var basic = UnityEditor.AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(ChargedSkillSetup.Folder + "/Clash/ClashBasic_0.asset").AnimationClip;
        foreach (var binding in UnityEditor.AnimationUtility.GetCurveBindings(basic))
        {
            float expected = UnityEditor.AnimationUtility.GetEditorCurve(basic, binding).Evaluate(basic.length);
            var curve = UnityEditor.AnimationUtility.GetEditorCurve(skill.preparationClip, binding);
            Require(curve != null && Mathf.Abs(curve.Evaluate(0) - expected) < .00001f &&
                Mathf.Abs(curve.Evaluate(.5f) - expected) < .00001f && Mathf.Abs(curve.Evaluate(1) - expected) < .00001f,
                "Preparation differs from BasicSkill_1 final pose: " + binding.propertyName);
        }
        Require(UnityEditor.AnimationUtility.GetAnimationEvents(skill.preparationClip).Length == 0, "Preparation replays attack events.");
        Debug.Log("[ChargedPreparation] PASS all preparation curves hold BasicSkill_1 final pose; no attack events");
        RunPreparationArmorBatch();
    }
    private static void TickPreparationArmor(LucianBrainsCombatArena arena, RealTimeCombatManager manager,
        RealTimeCombatInput input, PlayerActionPresentationController action, LitOpsiveLocomotionBridge bridge, ChargedSkillSO skill)
    {
        var actor = arena.Player;
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        if (stage == 0)
        {
            typeof(SquadCharacterController).GetField("enableIdleSitting", flags).SetValue(actor, false);
            if (actor.IsInSittingCycle) { actor.TrySetSitting(false); return; }
            if (!arena.Enemy.NavigationReady || Time.frameCount < 20) return;
            typeof(LitBrainsEnemy).GetField("hurtRemaining", flags).SetValue(arena.Enemy, 1000f);
            typeof(LitBrainsEnemy).GetField("currentHealth", flags).SetValue(arena.Enemy, 1000);
            actor.SetHealth(200, 200);
            manager.BeginExternalEncounter(actor.transform, arena.Enemy);
            bridge.TeleportForSceneTransition(arena.Enemy.transform.position - Vector3.forward * 2, Quaternion.identity);
            // Longer unsaved fixture window leaves time to probe competing commands.
            skill.chargeSeconds = 6;
            pad = InputSystem.AddDevice<Gamepad>(); Next(1);
        }
        else if (stage == 1 && Time.unscaledTime - at > .3f && bridge.Grounded)
        { Send(true); Next(2); }
        else if (stage == 2 && action.IsBasicSkillAction && !initialChargeDamageTested)
        {
            int generation = action.ActionGeneration;
            int hp = actor.CurrentHp;
            Debug.Log("[ChargedPreparation] initial probe actor=" + action.IsChargingSkill + " input=" + input.IsChargingSkillFor(action) +
                " held=" + typeof(RealTimeCombatInput).GetField("basicAttackHeld", flags).GetValue(input) +
                " release=" + typeof(RealTimeCombatInput).GetField("basicAttackRequiresRelease", flags).GetValue(input) +
                " owner=" + typeof(RealTimeCombatInput).GetField("chargePresentation", flags).GetValue(input) +
                " basicGeneration=" + typeof(RealTimeCombatInput).GetField("basicChargeGeneration", flags).GetValue(input) + " active=" + generation);
            actor.ApplyDamage(7, "ChargedInitialBasicValidation");
            string hurt = (string)typeof(RealTimeCombatManager).GetField("playerHurtAnimatorState", flags).GetValue(manager);
            Require(actor.CurrentHp == hp - 7 && action.IsCurrentSession(generation) && action.IsChargingSkill &&
                action.IsBasicSkillAction && !action.TryReplaceWithDamageReaction(hurt, PlayerActionPresentationProfile.CreateDefault()) &&
                !action.CanCancelToMobility, "Damage interrupted the initial held-X Basic before preparation.");
            initialChargeDamageTested = true;
            Debug.Log("[ChargedPreparation] PASS initial held-X Basic takes real damage without cancelling charge or session");
        }
        else if (stage == 2 && action.IsPreparingChargedSkill)
        {
            Require(initialChargeDamageTested, "Initial Basic damage case was not exercised.");
            chargeGeneration = action.ActionGeneration;
            int hp = actor.CurrentHp;
            actor.ApplyDamage(7, "ChargedPreparationValidation");
            Require(actor.CurrentHp == hp - 7 && action.IsCurrentSession(chargeGeneration) && action.IsPreparingChargedSkill,
                "Real damage interrupted preparation or was incorrectly ignored.");
            string hurt = (string)typeof(RealTimeCombatManager).GetField("playerHurtAnimatorState", flags).GetValue(manager);
            Require(action.IsDamageReactionSuppressed && !action.TryReplaceWithDamageReaction(hurt, PlayerActionPresentationProfile.CreateDefault()),
                "Hurt replaced protected preparation.");
            Require(!action.CanCancelToMobility && !action.CancelActionForMobility() && !action.CanStartAction,
                "Mobility/action can replace preparation.");
            CounterSkillCombatController.Instance?.BeginGuard();
            CounterSkillCombatController.Instance?.EndGuard();
            Require(!arena.GetComponent<CombatSkillKitInput>().Open(CombatSkillKit.Lucian), "Wheel interrupted preparation.");
            Require(action.IsCurrentSession(chargeGeneration) && typeof(RealTimeCombatInput).GetField("heldChargedSkill", flags).GetValue(input) == skill,
                "Damage or rejected commands cleared held charge.");
            Debug.Log("[ChargedPreparation] PASS real damage applied; preparation, held X and camera session preserved; Hurt/mobility/guard/wheel rejected");
            Next(20);
        }
        else if (stage == 20 && Time.unscaledTime - at > .25f)
        {
            var animator = actor.GetComponent<Animator>();
            Require(action.IsCurrentSession(chargeGeneration) && action.IsPreparingChargedSkill &&
                (animator.GetCurrentAnimatorStateInfo(0).IsName(skill.preparationState) ||
                    animator.GetNextAnimatorStateInfo(0).IsName(skill.preparationState)), "Charge pose was visually replaced after damage.");
            Send(false); Next(3);
        }
        else if (stage == 3 && Time.unscaledTime - at > .3f)
        {
            Require(!action.IsPreparingChargedSkill && !action.IsActionActive && !bridge.IsExternalLockActive &&
                !LitCameraDirector.Instance.IsChargeFramingActive && !action.IsDamageReactionSuppressed,
                "Voluntary X release leaked preparation resources.");
            int token = action.BeginChargedPreparation(skill);
            Require(token != 0, "Priority preparation fixture refused.");
            int cinematic = action.BeginExternalPresentation(arena);
            Require(cinematic != 0 && !action.IsPreparingChargedSkill && !bridge.IsExternalLockActive &&
                !LitCameraDirector.Instance.IsChargeFramingActive, "Priority presentation could not cleanly replace preparation.");
            action.EndExternalPresentation(cinematic);
            token = action.BeginChargedPreparation(skill);
            Require(token != 0, "Death preparation fixture refused.");
            actor.SetHealth(1, 200); actor.ApplyDamage(7, "ChargedPreparationDeathValidation");
            typeof(RealTimeCombatManager).GetMethod("EvaluateCombatOutcome", flags).Invoke(manager, null);
            Require(actor.CurrentHp == 0 && action.IsDeathAnimationLocked && !action.IsPreparingChargedSkill &&
                !bridge.IsExternalLockActive && !LitCameraDirector.Instance.IsChargeFramingActive,
                "Death did not clean protected preparation.");
            Debug.Log("[ChargedPreparation] PLAY PASS | damage armor, no invulnerability | ordinary cancellations rejected | X release, priority presentation and death cleanup");
            InputSystem.RemoveDevice(pad);
            SessionState.SetBool(PreparationArmorKey, false); SessionState.SetBool(Key, false); EditorApplication.Exit(0);
        }
    }
    private static void Require(bool condition, string text) { if (!condition) throw new InvalidOperationException(text); }
    private static void Next(int value) { stage = value; at = Time.unscaledTime; gameAt = Time.time; Debug.Log("[ChargedSkillTest] stage=" + value + " game=" + Time.time); }
    private static void Send(bool west) => InputSystem.QueueStateEvent(pad, west ? new GamepadState().WithButton(GamepadButton.West) : new GamepadState());
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < (SessionState.GetBool(PreparationArmorKey, false) ? 60 : 240), "Timeout stage " + stage);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena?.Player == null || arena.Enemy == null) return;
            var player = arena.Player;
            var enemy = arena.Enemy;
            var bridge = player.GetComponent<LitOpsiveLocomotionBridge>();
            var action = player.GetComponent<PlayerActionPresentationController>();
            var manager = arena.GetComponent<RealTimeCombatManager>();
            var input = arena.GetComponent<RealTimeCombatInput>();
            var skill = player.CharacterData.chargedSkill;
            Require(skill != null, "Lab character charged skill missing");
            if (SessionState.GetBool(PreparationArmorKey, false))
            { TickPreparationArmor(arena, manager, input, action, bridge, skill); return; }
            if (stage == 0)
            {
                typeof(SquadCharacterController).GetField("enableIdleSitting", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, false);
                if (player.IsInSittingCycle) { player.TrySetSitting(false); return; }
                if (!enemy.NavigationReady) return;
                if (!fixtureFrozen)
                {
                    enemy.SetCinematicSuspended(true);
                    enemy.SetCinematicSuspended(false);
                    typeof(LitBrainsEnemy).GetField("hurtRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, 1000f);
                    fixtureFrozen = true;
                }
                // Let first-frame HDRP shader imports settle before measuring held input in real time.
                if (Time.frameCount < 20) return;
                if (!enemy.NavigationReady) return;
                if (action.IsActionActive) return;
                typeof(LitBrainsEnemy).GetField("hurtRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, 1000f);
                typeof(LitBrainsEnemy).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, 1000);
                enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped = true;
                player.SetHealth(1000, 1000);
                bridge.TeleportForSceneTransition(enemy.transform.position - Vector3.forward * 2, Quaternion.identity);
                playerStart = player.transform.position.y; enemyStart = enemy.transform.position.y;
                peakPlayer = playerStart; peakEnemy = enemyStart;
                pad = InputSystem.AddDevice<Gamepad>();
                manager.PlayerSkillStarted += (value, _) => { if (value == skill) casts++; };
                input.SetInputActive(true);
                Require(AnimationUtility.GetAnimationEvents(skill.AnimationClip).Length == 2, "Unexpected authoring events");
                // A longer fixture-only hold verifies returning to the passive pose
                // after a complete aerial Basic, without speeding up shared clips.
                skill.airborneHoldSeconds = Mathf.Max(3f, player.CharacterData.airBasicSkills[0].AnimationClip.length + 1.5f);
                Next(1); return;
            }
            float elapsed = Time.unscaledTime - at;
            float gameElapsed = Time.time - gameAt;
            if (stage == 18 && gameElapsed > .9f)
                Require(player.GetComponent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>().UseGravity,
                    "Late aerial Basic extended suspension past the charged deadline");
            if (!enemy.IsChargedAirborne)
                typeof(LitBrainsEnemy).GetField("hurtRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, 1000f);
            peakPlayer = Mathf.Max(peakPlayer, player.transform.position.y);
            peakEnemy = Mathf.Max(peakEnemy, enemy.transform.position.y);
            if (stage == 1 && elapsed > .5f)
            { Send(true); Next(2); }
            else if (stage == 2 && elapsed > .1f)
            { Require(action.IsBasicSkillAction, "X did not start immediate BasicSkill"); Send(false); Next(3); }
            else if (stage == 3 && Time.time - gameAt > 1.7f)
            {
                Require(casts == 0 && !action.IsActionActive, "Short press charged or stuck");
                Send(true); Next(4);
            }
            else if (stage == 4 && elapsed > 1.8f)
            {
                Require(casts == 0, "Charge fired before two real seconds");
                Require(action.IsActionActive && !action.IsBasicSkillAction && bridge.IsExternalLockActive,
                    "Preparation pose or movement lock missing before charged launch");
                Require(LitCameraDirector.Instance != null && LitCameraDirector.Instance.IsChargeFramingActive,
                    "Charge camera framing missing");
                Require(player.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName(skill.preparationState) ||
                    player.GetComponent<Animator>().GetNextAnimatorStateInfo(0).IsName(skill.preparationState), "Charge animation missing");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs/ChargedPreparation.png"));
                before = enemy.CurrentHealth; Next(5);
            }
            else if (stage == 5 && casts == 1)
            { chargeGeneration = action.ActionGeneration; Next(6); }
            else if (stage == 5 && elapsed > 4)
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                throw new InvalidOperationException("Held charge did not launch: held=" + typeof(RealTimeCombatInput).GetField("basicAttackHeld", flags).GetValue(input) +
                    " armed=" + typeof(RealTimeCombatInput).GetField("heldChargedSkill", flags).GetValue(input) +
                    " release=" + typeof(RealTimeCombatInput).GetField("basicAttackRequiresRelease", flags).GetValue(input) +
                    " grounded=" + bridge.Grounded + " action=" + action.IsActionActive + " end=" + action.LastEndReason +
                    " targetReady=" + enemy.CanReceiveChargedLaunch + " nav=" + enemy.NavigationReady + " agent=" + enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled +
                    " hp=" + enemy.CurrentHealth + " dead=" + enemy.IsDead + " cine=" + enemy.IsCinematicSuspended +
                    " body=" + enemy.GetComponent<Rigidbody>() + " capsule=" + enemy.GetComponent<CapsuleCollider>() +
                    " combat=" + manager.IsCombatActive + " locked=" + manager.HasLockedCombatTarget + " inputlock=" + bridge.IsExternalLockActive +
                    " state=" + player.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).fullPathHash);
            }
            else if (stage == 6 && gameElapsed > 1.2f)
            {
                Require(!LitCameraDirector.Instance.IsChargeFramingActive, "Charge camera survived launch");
                Require(enemy.CurrentHealth == before - 10, "Only the launch contact may deal damage: delta=" + (before-enemy.CurrentHealth) +
                    " player=" + player.transform.position + " enemy=" + enemy.transform.position + " peaks=" + peakPlayer + "/" + peakEnemy +
                    " active=" + action.IsActionActive + " end=" + action.LastEndReason);
                Require(peakPlayer > playerStart + 2 && peakEnemy > enemyStart + 2, "Physical ascent missing: player=" + peakPlayer + " enemy=" + peakEnemy);
                Require(peakPlayer < playerStart + 3.6f && peakEnemy < enemyStart + 3.6f, "Ascent overshot configured height");
                Require(!action.IsCurrentSession(chargeGeneration), "Ascent did not restore controls | time=" + Time.time + " scale=" +
                    player.GetComponent<CombatTimeDomain>().Scale + " generation=" + action.ActionGeneration + " end=" + action.LastEndReason);
                Require(enemy.IsChargedAirborne, "Enemy fell before follow-up window");
                var playerAnimator = player.GetComponent<Animator>();
                Require(playerAnimator.GetCurrentAnimatorStateInfo(0).IsName(skill.playerFloatState) ||
                    playerAnimator.GetNextAnimatorStateInfo(0).IsName(skill.playerFloatState), "Lucian passive float missing");
                Require(enemy.Animator.GetCurrentAnimatorStateInfo(0).IsName(skill.enemyFloatState) ||
                    enemy.Animator.GetNextAnimatorStateInfo(0).IsName(skill.enemyFloatState), "Enemy floating hit missing");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs/ChargedFloating.png"));
                Send(false); Next(7);
            }
            else if (stage == 7 && elapsed > .15f)
            { Send(true); Next(8); }
            else if (stage == 8 && elapsed > .1f)
            {
                Require(action.IsBasicSkillAction && !bridge.Grounded, "Manual aerial follow-up unavailable");
                var playerAnimator = player.GetComponent<Animator>();
                Require(playerAnimator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.AirBasicSkill_1") ||
                    playerAnimator.GetNextAnimatorStateInfo(0).IsName("Base Layer.AirBasicSkill_1"), "Float pose overrode aerial BasicSkill");
                Send(false); Next(15);
            }
            else if (stage == 15 && gameElapsed > player.CharacterData.airBasicSkills[0].AnimationClip.length + .3f)
            {
                var playerAnimator = player.GetComponent<Animator>();
                Require(!action.IsActionActive && (playerAnimator.GetCurrentAnimatorStateInfo(0).IsName(skill.playerFloatState) ||
                    playerAnimator.GetNextAnimatorStateInfo(0).IsName(skill.playerFloatState)), "Passive float did not resume after completed aerial Basic");
                skill.airborneHoldSeconds = 1.2f;
                Next(9);
            }
            else if (stage == 9 && gameElapsed > 3)
            {
                Require(!enemy.IsChargedAirborne && enemy.NavigationReady, "Enemy navigation not restored after landing | air=" + enemy.IsChargedAirborne +
                    " pos=" + enemy.transform.position + " body=" + enemy.GetComponent<Rigidbody>().position + " offset=" + enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().baseOffset);
                Require(bridge.Grounded && !bridge.IsExternalLockActive, "Player gravity or input lock leaked");
                Require(casts == 1, "Holding produced repeated charged casts");
                Require(!manager.TryUseSkill(skill), "Cooldown ignored");
                skill.cooldownSeconds = 0;
                // Remove only the validated cooldown for repeated fixture cycles.
                var cooldowns = (System.Collections.Generic.Dictionary<Transform, System.Collections.Generic.Dictionary<SkillSO, float>>)
                    typeof(RealTimeCombatManager).GetField("skillCooldowns", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
                cooldowns.Clear();
                Next(10);
            }
            else if (stage == 10 && bridge.Grounded && !action.IsActionActive && enemy.NavigationReady)
            {
                bridge.TeleportForSceneTransition(enemy.transform.position - Vector3.forward * 2, Quaternion.identity);
                before = enemy.CurrentHealth;
                Require(manager.TryUseSkill(skill), "Repeated launch refused at cycle " + cycles);
                chargeGeneration = action.ActionGeneration;
                Next(11);
            }
            else if (stage == 11 && gameElapsed > 3.4f && bridge.Grounded && enemy.NavigationReady)
            {
                Require(enemy.CurrentHealth == before - 10, "Repeated damage mismatch at cycle " + cycles);
                Require(!bridge.IsExternalLockActive && !enemy.IsChargedAirborne, "Repeated cycle leaked resource");
                int hp = enemy.CurrentHealth;
                action.HandleChargedSkillContact(0); action.HandleChargedSkillContact(1);
                Require(enemy.CurrentHealth == hp, "Stale contact applied after session end");
                if (++cycles < 10) Next(10);
                else Next(12);
            }
            else if (stage == 12 && elapsed > .1f)
            {
                Require(manager.TryUseSkill(skill), "Interruption fixture launch refused");
                Next(13);
            }
            else if (stage == 13 && gameElapsed > .5f)
            { action.CancelAction(); Next(14); }
            else if (stage == 14 && gameElapsed > 1.8f && bridge.Grounded && enemy.NavigationReady)
            {
                Require(!bridge.IsExternalLockActive && !enemy.IsChargedAirborne, "Mid-ascent cancellation leaked resource");
                bridge.TeleportForSceneTransition(enemy.transform.position - Vector3.forward * 2, Quaternion.identity);
                Require(manager.TryUseSkill(skill), "Orphan hold fixture refused");
                Next(16);
            }
            else if (stage == 16 && gameElapsed > 1.1f && !bridge.Grounded)
            {
                orphanHold = bridge.AcquireCombatAirborneHold(action, .2f);
                Require(orphanHold != 0, "Orphan hold fixture unavailable");
                Next(17);
            }
            else if (stage == 16 && gameElapsed > 3f)
                throw new InvalidOperationException("Repeated charged clip missed its launch contact after interruption");
            else if (stage == 17 && gameElapsed > .4f)
            {
                Require(!bridge.Grounded, "An expired hold released the independent ChargedSkill hold");
                bridge.ReleaseCombatAirborneHold(orphanHold);
                bridge.ReleaseCombatAirborneHold(orphanHold);
                Send(true);
                Next(18);
            }
            else if (stage == 18 && pad.buttonWest.isPressed && elapsed > .1f)
            { Send(false); }
            else if (stage == 18 && gameElapsed > 2f && bridge.Grounded && enemy.NavigationReady && !action.IsActionActive)
            {
                Require(!bridge.IsExternalLockActive && !enemy.IsChargedAirborne, "Orphan hold prevented descent");
                if (pad != null) InputSystem.RemoveDevice(pad);
                Debug.Log("[ChargedSkill] PLAY PASS | charge animation/camera, single contact10, floating, manual Basic override, landing, ten cycles, interruption and expired/double-release hold isolation.");
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception); SessionState.SetBool(Key, false); EditorApplication.Exit(1);
        }
    }
}
