using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Short isolated batch validation; no authored asset is rewritten.</summary>
[InitializeOnLoad]
public static partial class FreeAttackPlayValidation
{
    private const string Key = "Lit.FreeAttack.Validation";
    private static int stage, enemyHealth, dashIndex;
    private static double started;
    private static float at;
    private static Vector3 dashStart, targetedOrigin;
    private static Gamepad pad;
    private static GameObject wall;
    private static int engagements;
    private static PlayerAttackContext firstContactAction;
    private static Action<ICombatTarget> engagementObserver;
    private const string FeelKey = Key + ".BasicFeel";
    private const string SkillFeelKey = Key + ".SkillFeel";
    private const string RuptureFeelKey = Key + ".RuptureFeel";
    private static float rupturePeak, ruptureNextSample;
    private static Vector3 ruptureOrigin;
    private static Vector3 ruptureLanding;
    private static bool ruptureAirborne, ruptureLanded;
    public static void RunRuptureFeelBatch()
    {
        SessionState.SetBool(RuptureFeelKey, true);
        RunSkillFeelBatch();
    }
    private static Vector3 skillFeelDirection;
    private static int skillFeelImpacts;
    private static float skillFeelTargetDrift;
    private static RealTimeCombatManager skillFeelManager;

    public static void RunSkillFeelBatch()
    {
        SessionState.SetBool(SkillFeelKey, true);
        RunBatch();
    }
    [Serializable] private sealed class FeelReport
    {
        public bool passed;
        public string failure, scope = "Real laboratory actors, X presses with explicit stick aim/contact, then an empty swing; not manual feel/network acceptance.";
        public int starts, impacts, inputToStartFrames, stage;
        public int[] damage = new int[3];
        public float[] minimumScale = { 1f, 1f, 1f };
        public string[] skills = new string[4];
        public Vector3[] playerStarts = new Vector3[4];
        public Vector3[] targetStarts = new Vector3[4];
    }
    private static FeelReport feel;
    private static RealTimeCombatManager feelManager;
    private static int feelPulses, feelInputFrame;
    private static bool feelReleased;
    private static float feelPulseAt, feelStartAt;

    public static void RunBasicFeelBatch()
    {
        SessionState.SetBool(FeelKey, true);
        RunBatch();
    }
    static FreeAttackPlayValidation() => EditorApplication.update += Tick;

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch editor only.");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>().character = CombatLabCharacter.Lucian;
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }
    private static void Check(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    private static void Set(object owner, string field, object value)
    {
        for (var type = owner.GetType(); type != null; type = type.BaseType)
        {
            var found = type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (found == null) continue;
            found.SetValue(owner, value); return;
        }
        throw new MissingFieldException(field);
    }
    private static void Next(int value) { stage = value; at = Time.time; }
    private static SkillSO Dash => AssetDatabase.LoadAssetAtPath<SkillSO>(dashIndex == 1 ? TargetMobilitySkillSetup.BondPath : TargetMobilitySkillSetup.EclairPath);

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Check(EditorApplication.timeSinceStartup - started < (SessionState.GetBool(MatrixKey, false) ? 150 : 90), "Short free-attack validation timeout, stage=" + stage);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena?.Player == null || arena.Enemy == null) return;
            var manager = arena.GetComponent<RealTimeCombatManager>();
            var action = arena.Player.GetComponent<PlayerActionPresentationController>();
            var bridge = arena.Player.GetComponent<LitOpsiveLocomotionBridge>();
            var skills = UnityEngine.Object.FindAnyObjectByType<SkillsManager>();
            if (SessionState.GetBool(SkillFeelKey, false))
            {
                TickSkillFeel(arena, manager, action, bridge, skills);
                return;
            }
            if (SessionState.GetBool(FeelKey, false))
            {
                Check(EditorApplication.timeSinceStartup - started < 35, "Basic feel timeout at stage=" + stage);
                TickBasicFeel(arena, manager, action, bridge, skills);
                return;
            }
            if (stage >= 100) { TickMatrix(arena, manager, action, bridge, skills); return; }
            if (stage == 0)
            {
                Set(arena.Player, "enableIdleSitting", false);
                if (arena.Player.IsInSittingCycle) { arena.Player.TrySetSitting(false); return; }
                if (!arena.Enemy.NavigationReady) return;
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "FreeAttackValidationFloor";
                floor.transform.position = new Vector3(0, -.15f, 0); floor.transform.localScale = new Vector3(60, .3f, 80);
                arena.Enemy.SetCinematicSuspended(true); arena.Enemy.SetCinematicSuspended(false);
                Set(arena.Enemy, "hurtRemaining", 1000f); Set(arena.Enemy, "fluidCooldown", 1000f);
                Set(arena.Enemy, "nextDetection", float.PositiveInfinity);
                Set(arena.Enemy, "currentHealth", 1000);
                arena.Player.SetHealth(1000, 1000);
                manager.EndCombat();
                manager.BeginExternalEncounter(arena.Player.transform, arena.Enemy, startEngaged: false);
                Check(manager.AllowsFreeAttacks && !manager.IsCombatActive && !manager.HasLockedCombatTarget, "Lab must prepare without engagement or lock");
                engagementObserver = target =>
                {
                    engagements++;
                    Check(action.AttackContext == firstContactAction && action.IsCurrentSession(firstContactAction.Generation), "First contact replaced its action session");
                };
                manager.EncounterAggroStarted += engagementObserver;
                arena.GetComponent<CombatWorldRuntime>().freeAttacks.diagnostics = true;
                Set(action, "debugTransitions", true);
                bridge.TeleportForSceneTransition(arena.Enemy.transform.position - Vector3.forward * 1.8f + Vector3.up * .02f, Quaternion.identity);
                var targetingProbe = ScriptableObject.CreateInstance<SkillSO>();
                try
                {
                    targetingProbe.targetingMode = SkillTargetingMode.RequiredTarget;
                    Check(manager.TryResolveSkillTargeting(targetingProbe, 7, out var captured, out var refusal), "Required temporary target refused: " + refusal);
                    Check(captured.TargetRoot == arena.Enemy.transform && !manager.HasLockedCombatTarget, "Temporary acquisition modified lock");
                    targetingProbe.targetingMode = SkillTargetingMode.Unconfigured;
                    Check(!manager.TryResolveSkillTargeting(targetingProbe, 8, out _, out _), "Unconfigured contract accepted");
                    Debug.Log("[SkillTargeting] PASS required temporary acquisition without camera lock; unconfigured rejected");
                }
                finally { UnityEngine.Object.Destroy(targetingProbe); }
                pad = InputSystem.AddDevice<Gamepad>();
                if (SessionState.GetBool(RemainingKey, false))
                { manager.EncounterAggroStarted -= engagementObserver; Next(130); }
                else Next(1);
                return;
            }
            if (stage == 1 && Time.time - at > .3f && bridge.Grounded)
            {
                bridge.TeleportForSceneTransition(arena.Enemy.transform.position - Vector3.forward * 1.5f + Vector3.up * .02f, Quaternion.identity);
                bridge.SetActionFacingDirection(Vector3.forward);
                enemyHealth = arena.Enemy.CurrentHealth;
                skills.SetAnimationEventSkill(arena.basicSkills[0]);
                Check(manager.TryUseSkill(arena.basicSkills[0]), "Unlocked basic rejected");
                firstContactAction = action.AttackContext;
                Check(action.AttackContext != null && !manager.HasLockedCombatTarget, "Action acquired a manual lock");
                Next(2);
            }
            else if (stage == 2 && !action.IsActionActive)
            {
                Check(enemyHealth - arena.Enemy.CurrentHealth == Mathf.RoundToInt(arena.basicSkills[0].Damages), "Unlocked basic contact missing or duplicated: damage=" + (enemyHealth - arena.Enemy.CurrentHealth) + " expected=" + arena.basicSkills[0].Damages + " reason=" + action.LastEndReason + " positions=" + arena.Player.transform.position + "/" + arena.Enemy.transform.position);
                Check(manager.IsCombatActive && engagements == 1 && !manager.HasLockedCombatTarget, "First contact must engage exactly once without lock");
                Debug.Log("[FreeAttack] PASS unlocked basic, actual contact, single damage, camera lock unchanged");
                targetedOrigin = arena.Enemy.transform.position;
                bridge.TeleportForSceneTransition(targetedOrigin - Vector3.forward * 5 + Vector3.up * .02f, Quaternion.identity);
                Next(9);
            }
            else if (stage == 9 && Time.time - at > .25f && bridge.Grounded)
            {
                bridge.TeleportForSceneTransition(targetedOrigin - Vector3.forward * 5 + Vector3.up * .02f, Quaternion.identity);
                bridge.SetActionFacingDirection(Vector3.forward);
                enemyHealth = arena.Enemy.CurrentHealth;
                skills.SetAnimationEventSkill(Dash);
                Check(manager.TryUseSkill(Dash) && action.AttackContext?.TargetRoot == arena.Enemy.transform, "Unlocked targeted Eclair refused");
                var captured = action.AttackContext;
                Check(manager.TryAdvanceManualLock() && manager.TryReleaseManualLock(), "Lock/unlock test unavailable");
                Check(action.AttackContext == captured, "Lock change replaced captured action");
                Next(10);
            }
            else if (stage == 10 && !action.IsActionActive)
            {
                Check(enemyHealth - arena.Enemy.CurrentHealth == Mathf.RoundToInt(Dash.Damages), "Targeted Eclair missed or doubled contact");
                Check(Mathf.Abs(Vector3.Dot(arena.Player.transform.position - targetedOrigin, Vector3.forward) - 4) < .5f, "Targeted Eclair did not exit 4m behind target");
                Check(!manager.HasLockedCombatTarget && bridge.TargetCollisionBypassCount == 0, "Targeted Eclair leaked lock or collisions");
                Check(engagements == 1, "Repeated contact replayed encounter introduction");
                Debug.Log("[FreeAttack] PASS unlocked targeted Eclair, unique contact, exit +4m, collision cleanup");
                manager.EndCombat(); arena.Enemy.SetCinematicSuspended(true);
                arena.Enemy.transform.position = new Vector3(100, 0, 100);
                bridge.TeleportForSceneTransition(new Vector3(0, .02f, -10), Quaternion.identity);
                Next(3);
            }
            else if (stage == 3 && Time.time - at > .25f && bridge.Grounded)
            {
                Check(!manager.IsCombatActive && InputModeCoordinator.CurrentMode == InputMode.Exploration, "Fixture failed to leave engagement");
                Check(LocalPlayerInput.FindSharedActionMap("RealTimeCombat").FindAction("BasicAttack").enabled, "Exploration basic binding missing");
                Check(!LocalPlayerInput.FindSharedActionMap("RealTimeCombat").FindAction("Counter").enabled, "Exploration enabled combat-only defence");
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.West)); Next(4);
            }
            else if (stage == 4 && action.IsActionActive)
            {
                Check(action.IsBasicSkillAction && !manager.IsCombatActive, "Air swing started an encounter");
                InputSystem.QueueStateEvent(pad, new GamepadState()); Next(5);
            }
            else if (stage == 5 && !action.IsActionActive)
            {
                Check(!manager.IsCombatActive, "Miss engaged combat");
                Debug.Log("[FreeAttack] PASS exploration X, empty swing, no encounter or combat-only input"); Next(6);
            }
            else if (stage == 6 && Time.time - at > .2f && bridge.Grounded)
            {
                if (dashIndex == 2)
                {
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.position = arena.Player.transform.position + arena.Player.transform.forward * 2 + Vector3.up;
                    wall.transform.rotation = arena.Player.transform.rotation;
                    wall.transform.localScale = new Vector3(4, 2, .2f);
                }
                dashStart = arena.Player.transform.position;
                skills.SetAnimationEventSkill(Dash);
                Check(manager.TryUseSkill(Dash), "Free dash refused " + Dash.SkillName);
                Check(action.AttackContext != null && action.AttackContext.Target == null, "Free dash acquired a far enemy");
                Next(7);
            }
            else if (stage == 7 && !action.IsActionActive)
            {
                float travelled = Vector3.ProjectOnPlane(arena.Player.transform.position - dashStart, Vector3.up).magnitude;
                Check(dashIndex == 2 ? travelled > .1f && travelled < 2 : Mathf.Abs(travelled - 10) < .5f,
                    "Wrong free dash distance: " + travelled + " case=" + dashIndex);
                Check(bridge.TargetCollisionBypassCount == 0 && !bridge.IsScriptedPlanarMotionActive && !bridge.IsExternalLockActive,
                    "Dash leaked movement or collision resources");
                Check(!manager.IsCombatActive && !manager.HasLockedCombatTarget, "Empty dash engaged/locked an enemy");
                Debug.Log("[FreeAttack] PASS dash " + dashIndex + " distance=" + travelled);
                if (++dashIndex < 3) { Next(6); return; }
                if (wall != null) UnityEngine.Object.Destroy(wall);
                Next(8);
            }
            else if (stage == 8 && Time.time - at > .2f)
            {
                skills.SetAnimationEventSkill(Dash);
                Check(manager.TryUseSkill(Dash), "Interruption fixture refused dash");
                var expired = action.AttackContext;
                expired.TryConsume(expired.Generation);
                action.CancelAction();
                Check(!manager.TryEngageFromPlayerContact(arena.Player.transform, arena.Enemy, expired) && !manager.IsCombatActive,
                    "Expired contact reopened an encounter");
                Check(action.AttackContext == null && bridge.TargetCollisionBypassCount == 0 && !bridge.IsExternalLockActive && !bridge.IsScriptedPlanarMotionActive,
                    "Interrupted dash leaked its context or resources");
                Debug.Log("[FreeAttack] PLAY PASS | unlocked basic, exploration input, empty swing, Eclair/Bond 10m, obstacle, interrupted cleanup");
                if (SessionState.GetBool(MatrixKey, false))
                { manager.EncounterAggroStarted -= engagementObserver; Next(100); }
                else Finish(0);
            }
        }
        catch (Exception error)
        {
            if (feel != null) feel.failure = error.ToString();
            Debug.LogException(error); Finish(1);
        }
    }

    private static void TickSkillFeel(LucianBrainsCombatArena arena, RealTimeCombatManager manager,
        PlayerActionPresentationController action, LitOpsiveLocomotionBridge bridge, SkillsManager skills)
    {
        // A successful skill stun replaces hurtRemaining. Keep the stationary
        // fixture explicit instead of silently allowing AI to resume between cases.
        if (stage > 0) Set(arena.Enemy, "hurtRemaining", 1000f);
        if (stage == 2) skillFeelTargetDrift = Mathf.Max(skillFeelTargetDrift,
            Vector3.ProjectOnPlane(arena.Enemy.transform.position - targetedOrigin, Vector3.up).magnitude);
        if (stage == 0)
        {
            if (!arena.Enemy.NavigationReady || !bridge.Grounded) return;
            Set(arena.Player, "enableIdleSitting", false);
            if (arena.Player.IsInSittingCycle) { arena.Player.TrySetSitting(false); return; }
            Set(arena.Enemy, "hurtRemaining", 1000f);
            Set(arena.Enemy, "fluidCooldown", 1000f);
            Set(arena.Enemy, "nextDetection", float.PositiveInfinity);
            arena.Enemy.Health.SetHealth(arena.Enemy.Health.MaxHp, arena.Enemy.Health.MaxHp);
            manager.EndCombat();
            manager.BeginExternalEncounter(arena.Player.transform, arena.Enemy, startEngaged: false);
            Check(manager.UsesBurstContactDash, "Laboratory burst profile is not installed.");
            skillFeelManager = manager;
            manager.PlayerSkillImpactApplied += ObserveSkillFeelImpact;
            Set(action, "debugContactMotion", true);
            dashIndex = 0;
            Next(1);
            return;
        }
        if (stage == 1)
        {
            if (Time.time - at < .3f || !bridge.Grounded || action.IsActionActive) return;
            skillFeelDirection = Vector3.ProjectOnPlane(arena.Player.transform.forward, Vector3.up).normalized;
            targetedOrigin = arena.Enemy.transform.position;
            bool rupture = SessionState.GetBool(RuptureFeelKey, false);
            dashStart = targetedOrigin - skillFeelDirection * (rupture ? 2 : 5) + Vector3.up * .02f;
            bridge.TeleportForSceneTransition(dashStart, Quaternion.LookRotation(skillFeelDirection));
            bridge.SetActionFacingDirection(skillFeelDirection);
            Physics.SyncTransforms();
            enemyHealth = arena.Enemy.CurrentHealth;
            skillFeelImpacts = 0;
            skillFeelTargetDrift = 0;
            var selected = rupture ? AssetDatabase.LoadAssetAtPath<SkillSO>("Assets/CombatRealTime/Skills/Skill_4_JumpKick_Rupture.asset") : Dash;
            if (rupture) Check(manager.TryAdvanceManualLock(), "Rupture manual lock unavailable.");
            skills.SetAnimationEventSkill(selected);
            Check(manager.TryUseSkill(selected), "Authored contact skill refused: " + selected.name);
            if (rupture)
            {
                ruptureOrigin = arena.Player.transform.position;
                rupturePeak = ruptureOrigin.y;
                ruptureNextSample = Time.unscaledTime;
                Next(3);
                return;
            }
            Check(action.AttackContext?.TargetRoot == arena.Enemy.transform && !manager.HasLockedCombatTarget,
                "Temporary target was not captured without camera lock.");
            Next(2);
            return;
        }
        if (stage == 3)
        {
            rupturePeak = Mathf.Max(rupturePeak, arena.Player.transform.position.y);
            ruptureAirborne |= !bridge.Grounded && bridge.VerticalVelocity > 0 &&
                arena.Player.transform.position.y > ruptureOrigin.y + .1f;
            if (ruptureAirborne && bridge.Grounded && !ruptureLanded)
            {
                ruptureLanded = true;
                ruptureLanding = arena.Player.transform.position;
            }
            if (Time.unscaledTime >= ruptureNextSample)
            {
                ruptureNextSample = Time.unscaledTime + .15f;
                Debug.Log($"[RuptureFeel] t={Time.time - at:F3} scale={Time.timeScale:F2} pos={arena.Player.transform.position} velocity={bridge.Velocity} grounded={bridge.Grounded} active={action.IsActionActive}");
            }
            if (action.IsActionActive || bridge.IsExternalLockActive || !bridge.Grounded) return;
            float retreat = -Vector3.Dot(arena.Player.transform.position - ruptureOrigin, skillFeelDirection);
            float slide = Vector3.ProjectOnPlane(arena.Player.transform.position - ruptureLanding, Vector3.up).magnitude;
            Debug.Log($"[RuptureFeel] Result | retreat={retreat:F3} rise={rupturePeak - ruptureOrigin.y:F3} groundSlide={slide:F3} damage={enemyHealth - arena.Enemy.CurrentHealth} contacts={skillFeelImpacts}");
            Check(retreat > .5f && rupturePeak > ruptureOrigin.y + .1f, "Rupture did not produce a backward jump.");
            Check(skillFeelImpacts == 1 && enemyHealth - arena.Enemy.CurrentHealth == 15, "Rupture impact missing or duplicated.");
            Check(!bridge.IsInputSuppressedByUcc && Mathf.Approximately(Time.timeScale, 1), "Rupture retained control/time.");
            Check(ruptureLanded && slide < .5f, "Rupture still slides more than half a metre after landing.");
            Debug.Log("[RuptureFeel] PLAY PASS | locked melee launch, unique contact, backward impulse, grounded control restitution. Manual feel not validated.");
            Finish(0);
            return;
        }
        if (stage != 2 || action.IsActionActive) return;
        float distance = Vector3.Dot(arena.Player.transform.position - dashStart, skillFeelDirection);
        float beyond = Vector3.Dot(arena.Player.transform.position - targetedOrigin, skillFeelDirection);
        Debug.Log($"[SkillFeel] {Dash.name} | travel={distance:F3} beyond={beyond:F3} impacts={skillFeelImpacts} damage={enemyHealth - arena.Enemy.CurrentHealth} position={arena.Player.transform.position} target={arena.Enemy.transform.position} targetDrift={skillFeelTargetDrift:F3}");
        Check(skillFeelTargetDrift < .03f, "Stationary contact fixture moved during the action.");
        Check(skillFeelImpacts == 1 && enemyHealth - arena.Enemy.CurrentHealth == Mathf.RoundToInt(Dash.Damages),
            "Contact missing or duplicated: " + Dash.name);
        Check(dashIndex == 0 ? Mathf.Abs(beyond - 4) < .5f : beyond < 0 && distance > 2.5f,
            "Wrong authored contact destination: " + Dash.name);
        Check(bridge.TargetCollisionBypassCount == 0 && !bridge.IsScriptedPlanarMotionActive && !bridge.IsExternalLockActive,
            "Contact skill retained collision or movement resources.");
        Check(Mathf.Approximately(Time.timeScale, 1), "Contact skill retained time control.");
        if (++dashIndex < 2) { Next(1); return; }
        Debug.Log("[SkillFeel] PLAY PASS | Eclair traversal, Bond approach, unique contacts, no manual lock, resources restored. Manual feel/network not validated.");
        Finish(0);
    }
    private static void ObserveSkillFeelImpact(SkillSO skill, int damage)
    {
        if (SessionState.GetBool(RuptureFeelKey, false) || skill == Dash) skillFeelImpacts++;
    }

    private static void TickBasicFeel(LucianBrainsCombatArena arena, RealTimeCombatManager manager,
        PlayerActionPresentationController action, LitOpsiveLocomotionBridge bridge, SkillsManager skills)
    {
        if (stage == 0)
        {
            if (!arena.Enemy.NavigationReady || !bridge.Grounded) return;
            Check(arena.basicSkills.Length >= 3, "Three authored Basics required.");
            Set(arena.Player, "enableIdleSitting", false);
            if (arena.Player.IsInSittingCycle) { arena.Player.TrySetSitting(false); return; }
            // Isolate the player's existing motor and contacts, not enemy decision timing.
            Set(arena.Enemy, "hurtRemaining", 1000f);
            Set(arena.Enemy, "fluidCooldown", 1000f);
            Set(arena.Enemy, "nextDetection", float.PositiveInfinity);
            arena.Enemy.Health.SetHealth(arena.Enemy.Health.MaxHp, arena.Enemy.Health.MaxHp);
            manager.EndCombat();
            manager.BeginExternalEncounter(arena.Player.transform, arena.Enemy, startEngaged: false);
            bridge.TeleportForSceneTransition(arena.Enemy.transform.position - Vector3.forward * 1.5f + Vector3.up * .02f, Quaternion.identity);
            Physics.SyncTransforms();
            skills.ResetAllBasicSkillCombos();
            feel = new FeelReport();
            feelManager = manager;
            arena.GetComponent<CombatWorldRuntime>().freeAttacks.diagnostics = true;
            Set(action, "debugTransitions", true);
            manager.PlayerSkillStarted += ObserveFeelStart;
            manager.PlayerSkillImpactApplied += ObserveFeelImpact;
            enemyHealth = arena.Enemy.CurrentHealth;
            pad = InputSystem.AddDevice<Gamepad>();
            Next(1);
            return;
        }
        if (stage == 1)
        {
            if (Time.time - at < .3f || !bridge.Grounded) return;
            Check(!manager.HasLockedCombatTarget && manager.CanReceiveFreeAttackInput, "Unlocked attack input unavailable.");
            // UCC has now resolved the camera-relative yaw. Place the fixture
            // target in that direction instead of assuming world +Z is forward.
            Vector3 forward = Vector3.ProjectOnPlane(arena.Player.transform.forward, Vector3.up).normalized;
            bridge.TeleportForSceneTransition(arena.Enemy.transform.position - forward * 1.5f + Vector3.up * .02f,
                Quaternion.LookRotation(forward));
            bridge.SetActionFacingDirection(forward);
            Physics.SyncTransforms();
            PulseFeelInput();
            Next(2);
            return;
        }
        if (stage == 2)
        {
            if (feel.starts > 0 && feel.starts <= 3)
                feel.minimumScale[feel.starts - 1] = Mathf.Min(feel.minimumScale[feel.starts - 1], Time.timeScale);
            ReleaseFeelInput();
            if (feel.starts == feelPulses && feelPulses < 3 && feelReleased && Time.unscaledTime - feelStartAt > .1f)
                PulseFeelInput();
            if (feel.starts < 3 || action.IsActionActive) return;
            Check(feel.impacts == 3, "Expected one actual contact per Basic, received=" + feel.impacts);
            int expected = 0;
            for (int i = 0; i < 3; i++)
            {
                int amount = Mathf.RoundToInt(arena.basicSkills[i].Damages);
                expected += amount;
                Check(feel.damage[i] == amount, "Basic damage missing or duplicated: " + i);
                Check(feel.minimumScale[i] <= arena.basicSkills[i].ImpactFeedback.hitStopTimeScale + .01f,
                    "Confirmed Basic did not produce its authored hit stop: " + i);
            }
            Check(enemyHealth - arena.Enemy.CurrentHealth == expected, "Health/contact ledger mismatch.");
            Check(feel.inputToStartFrames <= 2, "First input was delayed by more than two simulation frames.");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            manager.EndCombat();
            manager.BeginExternalEncounter(arena.Player.transform, arena.Enemy, startEngaged: false);
            bridge.TeleportForSceneTransition(arena.Enemy.transform.position - Vector3.forward * 8 + Vector3.up * .02f,
                Quaternion.LookRotation(Vector3.back));
            skills.ResetAllBasicSkillCombos();
            Next(3);
            return;
        }
        if (stage == 3)
        {
            if (Time.time - at < .3f || !bridge.Grounded) return;
            enemyHealth = arena.Enemy.CurrentHealth;
            PulseFeelInput();
            Next(4);
            return;
        }
        ReleaseFeelInput();
        if (feel.starts < 4 || action.IsActionActive) return;
        Check(feel.starts == 4 && feel.impacts == 3 && arena.Enemy.CurrentHealth == enemyHealth,
            "Empty swing produced a contact or extra attack.");
        Check(!manager.IsCombatActive && !manager.HasLockedCombatTarget && Mathf.Approximately(Time.timeScale, 1),
            "Empty swing engaged/locked or retained time control.");
        Check(!bridge.IsScriptedPlanarMotionActive && !bridge.IsExternalLockActive && bridge.TargetCollisionBypassCount == 0,
            "Combo left movement/collision resources active.");
        Finish(0);
    }

    private static void PulseFeelInput()
    {
        feelInputFrame = Time.frameCount;
        feelPulseAt = Time.unscaledTime;
        feelReleased = false;
        feelPulses++;
        InputSystem.QueueStateEvent(pad, FeelInputState(true));
    }
    private static void ReleaseFeelInput()
    {
        if (Time.unscaledTime - feelPulseAt >= .04f) feelReleased = true;
        InputSystem.QueueStateEvent(pad, FeelInputState(!feelReleased));
    }
    private static GamepadState FeelInputState(bool press)
    {
        var state = new GamepadState();
        if (stage < 3 && feelManager != null)
        {
            var lab = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            Vector3 forward = Vector3.ProjectOnPlane(lab.arenaCamera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 aim = Vector3.ProjectOnPlane(lab.Enemy.transform.position - lab.Player.transform.position, Vector3.up).normalized;
            state.leftStick = new Vector2(Vector3.Dot(aim, right), Vector3.Dot(aim, forward));
        }
        return press ? state.WithButton(GamepadButton.West) : state;
    }
    private static void ObserveFeelStart(SkillSO skill, EnemyController unused)
    {
        if (!(skill is BasicSkillsSO)) return;
        feel.starts++;
        feelStartAt = Time.unscaledTime;
        if (feel.starts == 1) feel.inputToStartFrames = Time.frameCount - feelInputFrame;
        int index = feel.starts - 1;
        if (index < 4)
        {
            feel.skills[index] = skill.name;
            feel.playerStarts[index] = feelManager.PlayerRoot.position;
            feel.targetStarts[index] = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>().Enemy.transform.position;
        }
    }
    private static void ObserveFeelImpact(SkillSO skill, int damage)
    {
        if (!(skill is BasicSkillsSO)) return;
        feel.impacts++;
        if (feel.starts > 0 && feel.starts <= 3) feel.damage[feel.starts - 1] += damage;
    }
    private static void Finish(int code)
    {
        if (SessionState.GetBool(SkillFeelKey, false))
        {
            SessionState.SetBool(SkillFeelKey, false);
            SessionState.SetBool(RuptureFeelKey, false);
            if (skillFeelManager != null) skillFeelManager.PlayerSkillImpactApplied -= ObserveSkillFeelImpact;
        }
        if (SessionState.GetBool(FeelKey, false))
        {
            SessionState.SetBool(FeelKey, false);
            if (feelManager != null)
            {
                feelManager.PlayerSkillStarted -= ObserveFeelStart;
                feelManager.PlayerSkillImpactApplied -= ObserveFeelImpact;
            }
            feel = feel ?? new FeelReport();
            feel.passed = code == 0;
            feel.stage = stage;
            Directory.CreateDirectory("Library/CombatFeel");
            File.WriteAllText("Library/CombatFeel/laboratory-combo.json", JsonUtility.ToJson(feel, true));
            Debug.Log("[CombatFeel] Laboratory combo " + (feel.passed ? "PASS" : "FAIL"));
        }
        WriteMatrixReport(code);
        SessionState.SetBool(Key, false);
        if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        EditorApplication.Exit(code);
    }
}
