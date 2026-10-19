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
    private const string MotionKey = "Lit.ChargedSkill.Motion";
    private const string MotionVisualKey = "Lit.ChargedSkill.MotionVisual";
    private static bool capturedMotionLaunch, capturedMotionBasic, capturedMotionDescent;
    private static int motionVisualFinish;
    public static void RunMotionVisualBatch()
    {
        SessionState.SetBool(MotionVisualKey, true);
        RunMotionBatch();
    }
    private static void CaptureMotion(string name, LucianBrainsCombatArena arena)
    {
        const string directory = "Library/CombatFeel/ChargedVisual";
        System.IO.Directory.CreateDirectory(directory);
        var camera = arena.arenaCamera;
        CombatFeelPlayValidation.CaptureCamera(camera, System.IO.Path.GetFullPath(directory + "/" + name + ".png"));
        foreach (var rig in new[] { arena.Player.GetComponentInChildren<Animator>(), arena.Enemy.Animator })
            foreach (var bone in new[] { HumanBodyBones.Head, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
            {
                var joint = rig.GetBoneTransform(bone);
                Require(joint != null, "Aerial framing rig bone missing.");
                var viewport = camera.WorldToViewportPoint(joint.position);
                Require(viewport.z > camera.nearClipPlane && viewport.x > .02f && viewport.x < .98f && viewport.y > .02f && viewport.y < .98f,
                    "Aerial framing clipped " + rig.name + "/" + bone + " at " + name + " viewport=" + viewport);
            }
        Debug.Log($"[ChargedVisual] {name} frame={Time.frameCount} player={arena.Player.transform.position} enemy={arena.Enemy.transform.position} camera={camera.transform.position} cameraForward={camera.transform.forward} fov={camera.fieldOfView}");
    }
    private const string MotionSafetyKey = "Lit.ChargedSkill.MotionSafety";
    private static GameObject motionCeiling;
    private static float ceilingPlayerLimit, ceilingEnemyLimit;
    public static void RunMotionSafetyBatch()
    {
        SessionState.SetBool(MotionSafetyKey, true);
        RunMotionBatch();
    }
    private static int motionContacts;
    private const string AirComboKey = "Lit.ChargedSkill.AirCombo";
    private static bool airStateSeen;
    private static SkillSO motionAerialSkill;
    private static int aerialContacts;
    public static void RunAirComboBatch()
    {
        SessionState.SetBool(AirComboKey, true);
        RunMotionBatch();
    }
    private static ChargedSkillSO motionSkill;
    public static void RunMotionBatch()
    {
        SessionState.SetBool(MotionKey, true);
        RunRuntimeBatch();
    }

    private static void TickMotion(LucianBrainsCombatArena arena, RealTimeCombatManager manager,
        PlayerActionPresentationController action, LitOpsiveLocomotionBridge bridge, ChargedSkillSO skill)
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        Require(EditorApplication.timeSinceStartup - started < 45, "Short charged motion timeout: " + stage);
        var actor = arena.Player;
        var enemy = arena.Enemy;
        if (!enemy.IsChargedAirborne) typeof(LitBrainsEnemy).GetField("hurtRemaining", fields).SetValue(enemy, 1000f);
        if (stage == 0)
        {
            typeof(SquadCharacterController).GetField("enableIdleSitting", fields).SetValue(actor, false);
            if (actor.IsInSittingCycle) { actor.TrySetSitting(false); return; }
            if (!enemy.NavigationReady || !bridge.Grounded) return;
            enemy.Health.SetHealth(enemy.Health.MaxHp, enemy.Health.MaxHp);
            typeof(LitBrainsEnemy).GetField("nextDetection", fields).SetValue(enemy, float.PositiveInfinity);
            manager.BeginExternalEncounter(actor.transform, enemy, startEngaged: false);
            motionSkill = skill;
            manager.PlayerSkillImpactApplied += ObserveMotionContact;
            Next(1); return;
        }
        peakPlayer = Mathf.Max(peakPlayer, actor.transform.position.y);
        peakEnemy = Mathf.Max(peakEnemy, enemy.transform.position.y);
        if (SessionState.GetBool(MotionVisualKey, false))
        {
            if (stage == 2 && enemy.IsChargedAirborne && !capturedMotionLaunch && actor.transform.position.y > playerStart + .2f)
            { capturedMotionLaunch = true; CaptureMotion("02_launch", arena); }
            if (stage == 3 && !capturedMotionBasic && Time.time - gameAt > .25f)
            { capturedMotionBasic = true; CaptureMotion("04_aerial_basic", arena); }
            if (stage == 3 && !capturedMotionDescent && Time.time - gameAt > skill.airborneHoldSeconds + .05f && !bridge.Grounded)
            { capturedMotionDescent = true; CaptureMotion("05_descent", arena); }
        }
        if (stage == 1)
        {
            if (Time.time - gameAt < .3f || !bridge.Grounded) return;
            var direction = Vector3.ProjectOnPlane(actor.transform.forward, Vector3.up).normalized;
            bridge.TeleportForSceneTransition(enemy.transform.position - direction * 1.8f + Vector3.up * .02f, Quaternion.LookRotation(direction));
            bridge.SetActionFacingDirection(direction);
            Physics.SyncTransforms();
            playerStart = peakPlayer = actor.transform.position.y;
            enemyStart = peakEnemy = enemy.transform.position.y;
            before = enemy.CurrentHealth;
            if (SessionState.GetBool(MotionVisualKey, false)) CaptureMotion("01_start", arena);
            motionContacts = 0;
            if (SessionState.GetBool(MotionSafetyKey, false) && cycles == 0)
            {
                float playerTop = actor.transform.position.y;
                foreach (var collider in bridge.ScriptedMotionColliders) playerTop = Mathf.Max(playerTop, collider.bounds.max.y);
                float enemyTop = enemy.GetComponent<CapsuleCollider>().bounds.max.y;
                float bottom = Mathf.Max(playerTop, enemyTop) + .9f;
                motionCeiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
                motionCeiling.name = "Charged motion validation ceiling";
                motionCeiling.transform.position = (actor.transform.position + enemy.transform.position) * .5f;
                motionCeiling.transform.position = new Vector3(motionCeiling.transform.position.x, bottom + .15f, motionCeiling.transform.position.z);
                motionCeiling.transform.localScale = new Vector3(8, .3f, 8);
                ceilingPlayerLimit = bottom - playerTop + .15f;
                ceilingEnemyLimit = bottom - enemyTop + .15f;
                Physics.SyncTransforms();
            }
            UnityEngine.Object.FindAnyObjectByType<SkillsManager>().SetAnimationEventSkill(skill);
            Require(manager.TryUseSkill(skill), "Charged motion launch refused.");
            Next(2); return;
        }
        if (SessionState.GetBool(MotionSafetyKey, false))
        {
            if (stage == 2)
            {
                if (cycles == 1 && enemy.IsChargedAirborne && Time.time - gameAt >= .47f)
                {
                    chargeGeneration = action.ActionGeneration;
                    action.CancelAction();
                    Require(!action.IsCurrentSession(chargeGeneration), "Interrupted ascent generation remained active.");
                    Next(4);
                }
                else if (cycles == 0 && action.PassiveChargedFloatSkill == skill && !action.IsActionActive)
                {
                    Require(peakPlayer - playerStart <= ceilingPlayerLimit && peakEnemy - enemyStart <= ceilingEnemyLimit,
                        "Charged ascent crossed the low ceiling.");
                    Next(4);
                }
                return;
            }
            if (stage != 4 || !bridge.Grounded || enemy.IsChargedAirborne || !enemy.NavigationReady || action.IsActionActive) return;
            Require(motionContacts == 1 && !bridge.IsExternalLockActive && !bridge.IsInputSuppressedByUcc && Mathf.Approximately(Time.timeScale, 1),
                "Charged safety case retained a lock or repeated its launch contact.");
            Debug.Log($"[ChargedSafety] PASS case={cycles} playerRise={peakPlayer - playerStart:F3} enemyRise={peakEnemy - enemyStart:F3} bothGrounded=True navigationReady=True");
            if (motionCeiling != null) UnityEngine.Object.Destroy(motionCeiling);
            if (++cycles < 2)
            {
                var cooldowns = (System.Collections.Generic.Dictionary<Transform, System.Collections.Generic.Dictionary<SkillSO, float>>)
                    typeof(RealTimeCombatManager).GetField("skillCooldowns", fields).GetValue(manager);
                if (cooldowns.TryGetValue(actor.transform, out var actorCooldowns)) actorCooldowns.Remove(skill);
                Next(1); return;
            }
            manager.PlayerSkillImpactApplied -= ObserveMotionContact;
            Debug.Log("[ChargedSafety] PLAY PASS | low ceiling, natural landing, ascent interruption, unique contacts, input/navigation restored");
            SessionState.SetBool(MotionSafetyKey, false); SessionState.SetBool(MotionKey, false); SessionState.SetBool(Key, false);
            EditorApplication.Exit(0);
            return;
        }
        if (stage == 2 && action.PassiveChargedFloatSkill == skill && !action.IsActionActive)
        {
            if (SessionState.GetBool(MotionVisualKey, false)) CaptureMotion("03_apex", arena);
            Require(motionContacts == 1 && before - enemy.CurrentHealth == 10, "Launch contact missing or duplicated.");
            Require(peakPlayer - playerStart > 2.5f && peakEnemy - enemyStart > 2.5f, "Actors did not reach their launch height.");
            Require(Mathf.Abs(actor.transform.position.y - enemy.transform.position.y) < .6f, "Actors separated vertically at the apex.");
            Require(!bridge.IsExternalLockActive && !bridge.IsInputSuppressedByUcc, "Float retained the charged action controls.");
            var aerial = actor.CharacterData.airBasicSkills[SessionState.GetBool(AirComboKey, false) ? cycles + 1 : 0];
            airStateSeen = false;
            motionAerialSkill = aerial;
            aerialContacts = 0;
            UnityEngine.Object.FindAnyObjectByType<SkillsManager>().SetAnimationEventSkill(aerial);
            Require(manager.TryUseSkill(aerial) && action.IsBasicSkillAction, "Aerial Basic could not interrupt the passive float.");
            Debug.Log($"[ChargedMotion] Apex | playerRise={peakPlayer - playerStart:F3} enemyRise={peakEnemy - enemyStart:F3} contacts={motionContacts} aerialBasic=True");
            Next(3); return;
        }
        if (stage == 3 && SessionState.GetBool(AirComboKey, false))
        {
            var animator = actor.GetComponentInChildren<Animator>();
            string expected = "Base Layer.AirBasicSkill_" + (cycles + 2);
            airStateSeen |= animator.GetCurrentAnimatorStateInfo(0).IsName(expected) ||
                animator.GetNextAnimatorStateInfo(0).IsName(expected);
        }
        if (stage != 3 || !bridge.Grounded || enemy.IsChargedAirborne || !enemy.NavigationReady || action.IsActionActive) return;
        if (SessionState.GetBool(AirComboKey, false))
        {
            Require(airStateSeen, "New aerial Animator state was not played: " + (cycles + 2));
            Require(aerialContacts == 1, "Aerial impact missing or repeated: " + (cycles + 2) + " contacts=" + aerialContacts);
            Require(!bridge.IsExternalLockActive && !bridge.IsInputSuppressedByUcc && motionContacts == 1,
                "Aerial action retained input resources or duplicated launch damage.");
            Debug.Log("[AirCombo] PLAY PASS | aerial=" + (cycles + 2) + " | Animator entered; landing and input restored");
            if (++cycles < 2)
            {
                var cooldowns = (System.Collections.Generic.Dictionary<Transform, System.Collections.Generic.Dictionary<SkillSO, float>>)
                    typeof(RealTimeCombatManager).GetField("skillCooldowns", fields).GetValue(manager);
                if (cooldowns.TryGetValue(actor.transform, out var actorCooldowns)) actorCooldowns.Remove(skill);
                Next(1); return;
            }
            SessionState.SetBool(AirComboKey, false);
        }
        if (SessionState.GetBool(MotionVisualKey, false))
        {
            if (motionVisualFinish == 0) { CaptureMotion("06_landing", arena); motionVisualFinish = Time.frameCount + 3; return; }
            if (Time.frameCount < motionVisualFinish) return;
        }
        Require(!bridge.IsExternalLockActive && !bridge.IsScriptedPlanarMotionActive && Mathf.Approximately(Time.timeScale, 1), "Charged landing retained movement/time resources.");
        Require(motionContacts == 1, "Charged contact repeated after aerial follow-up.");
        manager.PlayerSkillImpactApplied -= ObserveMotionContact;
        Debug.Log($"[ChargedMotion] PLAY PASS | impulse arc, paired apex, manual aerial Basic, both grounded, navigation/input restored | landingAfterApex={Time.time - gameAt:F3}");
        SessionState.SetBool(MotionKey, false); SessionState.SetBool(Key, false);
        SessionState.SetBool(MotionVisualKey, false);
        EditorApplication.Exit(0);
    }
    private static void ObserveMotionContact(SkillSO skill, int damage)
    {
        if (skill == motionSkill) motionContacts++;
        if (skill == motionAerialSkill) aerialContacts++;
    }
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
            if (SessionState.GetBool(MotionKey, false))
            { TickMotion(arena, manager, action, bridge, skill); return; }
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
