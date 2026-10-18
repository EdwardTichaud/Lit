using System;
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
                arena.freeAttacks.diagnostics = true;
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
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }
    private static void Finish(int code)
    {
        WriteMatrixReport(code);
        SessionState.SetBool(Key, false);
        if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        EditorApplication.Exit(code);
    }
}
