using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static partial class FreeAttackPlayValidation
{
    private const string MatrixKey = "Lit.FreeAttack.Acceptance";
    private const string RemainingKey = "Lit.FreeAttack.AcceptanceRemaining";
    private const string ClashKey = "Lit.FreeAttack.AcceptanceClash";
    private static readonly List<string> results = new List<string>();
    private static readonly Vector2[] directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left,
        new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1), new Vector2(-1, 1) };
    private static int matrixCase, sampleHp, secondHp, clashCount, voidCase;
    private static float castAt, sampledSpeed, playerY, enemyY;
    private static Vector3 castOrigin;
    private static SkillSO matrixSkill;
    private static PlayerAttackContext matrixContext;
    private static LitBrainsEnemy second;
    private static GameObject platform, legacyObject;
    private static EnemyController legacy;
    private static EnemyTacticalResponseController defence;
    private static bool screenshotTaken, duplicateChecked;
    private static bool physicalFailure;

    public static void RunAcceptanceBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch editor only.");
        SessionState.SetBool(MatrixKey, true);
        RunBatch();
    }
    public static void RunRemainingAcceptanceBatch()
    {
        SessionState.SetBool(RemainingKey, true);
        RunAcceptanceBatch();
    }
    public static void RunClashAcceptanceBatch()
    {
        SessionState.SetBool(ClashKey, true);
        RunRemainingAcceptanceBatch();
    }
    private static void Pass(string message) { results.Add(message); Debug.Log("[FreeAttackAcceptance] PASS " + message); }
    private static void WriteMatrixReport(int code)
    {
        if (!SessionState.GetBool(MatrixKey, false)) return;
        SessionState.SetBool(MatrixKey, false);
        results.Add(code == 0 && !physicalFailure ? "AUTOMATED ACCEPTANCE PASS" : "AUTOMATED ACCEPTANCE FAILED stage=" + stage);
        results.Add("Not a manual assessment of feel. Network and production rollout excluded.");
        string report = SessionState.GetBool(ClashKey, false) ? "FreeAttackAcceptanceClashReport.txt" :
            SessionState.GetBool(RemainingKey, false) ? "FreeAttackAcceptanceRemainderReport.txt" : "FreeAttackAcceptanceReport.txt";
        if (SessionState.GetBool(ClashKey, false)) Time.captureDeltaTime = 0;
        SessionState.SetBool(ClashKey, false);
        SessionState.SetBool(RemainingKey, false);
        File.WriteAllLines(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs/" + report), results);
    }
    private static void Freeze(LitBrainsEnemy enemy)
    {
        Set(enemy, "hurtRemaining", 1000f); Set(enemy, "fluidCooldown", 1000f);
        Set(enemy, "protectionRemaining", 1000f); Set(enemy, "resistanceDamage", 0f);
        Set(enemy, "nextDetection", float.PositiveInfinity); Set(enemy, "currentHealth", 1000);
    }
    private static void MoveEnemy(LitBrainsEnemy enemy, Vector3 position)
    {
        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent.enabled && agent.isOnNavMesh) agent.Warp(position);
        else enemy.transform.position = position;
        enemy.GetComponent<Rigidbody>().position = position;
        Physics.SyncTransforms();
    }
    private static void TickMatrix(LucianBrainsCombatArena arena, RealTimeCombatManager manager,
        PlayerActionPresentationController action, LitOpsiveLocomotionBridge bridge, SkillsManager skills)
    {
        float elapsed = Time.time - at;
        var player = arena.Player;
        var enemy = arena.Enemy;
        if (stage == 132 && elapsed > 5)
        {
            physicalFailure = true;
            results.Add("FAIL real clash not observed; see diagnostic windows in editor log");
            Debug.LogWarning("[FreeAttackAcceptance] Clash not observed; continuing independent physical scenarios.");
            if (SessionState.GetBool(ClashKey, false)) { Finish(2); return; }
            action.CancelAction();
            arena.GetComponent<CombatLabClashController>().Cancel();
            enemy.SetCinematicSuspended(true); enemy.SetCinematicSuspended(false); Freeze(enemy);
            bridge.TeleportForSceneTransition(enemy.transform.position - Vector3.forward * 2, Quaternion.identity);
            Next(140); return;
        }
        if ((stage == 141 || stage == 142) && elapsed > 8)
            throw new InvalidOperationException("Projection/landing timeout: player=" + player.transform.position + " enemy=" + enemy.transform.position);
        if (stage == 112 && action.IsActionActive && matrixContext.ImpactConsumed && !duplicateChecked)
        {
            int hp = enemy.CurrentHealth;
            Check(manager.ResolveFreeAttackImpact(matrixSkill, out _) == 0 && enemy.CurrentHealth == hp,
                "Duplicate impact event applied a second hit");
            duplicateChecked = true;
        }
        if (stage == 100)
        {
            manager.EndCombat(); enemy.SetCinematicSuspended(true);
            MoveEnemy(enemy, new Vector3(100, 0, 100));
            if (wall != null) UnityEngine.Object.Destroy(wall);
            matrixCase = 0; Next(101);
        }
        else if (stage == 101)
        {
            int mode = matrixCase / 8;
            bridge.TeleportForSceneTransition(new Vector3(0, mode == 2 ? 2.5f : .02f, -10), Quaternion.identity);
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = mode == 0 ? Vector2.zero : directions[matrixCase % 8].normalized, rightTrigger = mode == 1 ? 1 : 0 });
            sampledSpeed = 0; screenshotTaken = false; Next(102);
        }
        else if (stage == 102 && elapsed > .22f)
        {
            int mode = matrixCase / 8;
            if (mode != 2 && !bridge.Grounded) return;
            Check(mode != 2 || !bridge.Grounded, "Air fixture never left the ground");
            sampledSpeed = Vector3.ProjectOnPlane(bridge.Velocity, Vector3.up).magnitude;
            if (mode == 1) Check(bridge.IsSprintHeld && sampledSpeed > .1f, "Running fixture has no sprint/movement: " + sampledSpeed);
            var intent = mode == 0 ? Vector3.ProjectOnPlane(Quaternion.Euler(0, matrixCase % 8 * 45, 0) * Vector3.forward, Vector3.up) :
                FreeAttackProfile.ResolveDirection(directions[matrixCase % 8], arena.arenaCamera.transform.forward, player.transform.forward);
            if (mode == 0)
            {
                bridge.TeleportForSceneTransition(player.transform.position, Quaternion.LookRotation(intent));
                bridge.SetActionFacingDirection(intent);
            }
            matrixSkill = mode == 2 ? player.CharacterData.airBasicSkills[0] : arena.basicSkills[0];
            skills.SetAnimationEventSkill(matrixSkill);
            Check(manager.TryUseSkill(matrixSkill), "Matrix attack rejected: " + matrixCase + " reason=" + manager.LastSkillTargetingFailure);
            matrixContext = action.AttackContext; castOrigin = player.transform.position; castAt = Time.time;
            Check(matrixContext != null && matrixContext.Target == null && !manager.HasLockedCombatTarget, "Empty directional action acquired a lock/target");
            Check(Vector3.Angle(matrixContext.Direction, intent) < 1, "Input intention changed: " + matrixCase);
            Next(103);
        }
        else if (stage == 103)
        {
            if (!screenshotTaken && Time.time - castAt > .2f && matrixCase % 8 == 0)
            {
                screenshotTaken = true;
                ScreenCapture.CaptureScreenshot(Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                    "Logs/FreeAttack_Mode" + matrixCase / 8 + ".png"));
            }
            if (action.IsActionActive) return;
            Check(action.LastEndReason == PlayerActionPresentationController.ActionEndReason.Completed,
                "Directional attack did not complete: case=" + matrixCase + " reason=" + action.LastEndReason);
            Check(!manager.IsCombatActive && !manager.HasLockedCombatTarget, "Empty attack engaged/locked: " + matrixCase);
            Check(action.AttackContext == null && !bridge.IsExternalLockActive, "Matrix action leaked a session/lock");
            Pass("direction " + matrixCase % 8 + " mode=" + matrixCase / 8 + " startSpeed=" + sampledSpeed.ToString("F2") + " completed=" + action.LastEndReason);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            if (++matrixCase < 24) Next(101); else Next(110);
        }
        else if (stage == 110)
        {
            bridge.TeleportForSceneTransition(new Vector3(0, .02f, -6), Quaternion.identity);
            enemy.SetCinematicSuspended(false); MoveEnemy(enemy, new Vector3(-.65f, 0, -4)); Freeze(enemy);
            second = UnityEngine.Object.Instantiate(arena.enemyPrefab, new Vector3(.8f, 0, -3.9f), Quaternion.identity).GetComponent<LitBrainsEnemy>();
            Freeze(second); Next(111);
        }
        else if (stage == 111 && elapsed > .25f && bridge.Grounded && second.NavigationReady && enemy.NavigationReady)
        {
            bridge.TeleportForSceneTransition(new Vector3(0, .02f, -6), Quaternion.identity); bridge.SetActionFacingDirection(Vector3.forward);
            sampleHp = enemy.CurrentHealth; secondHp = second.CurrentHealth;
            matrixSkill = arena.basicSkills[0]; skills.SetAnimationEventSkill(matrixSkill);
            Check(manager.TryUseSkill(matrixSkill) && action.AttackContext?.TargetRoot == enemy.transform, "Two-enemy acquisition unstable");
            matrixContext = action.AttackContext;
            duplicateChecked = false;
            MoveEnemy(second, new Vector3(.8f, 0, -4.3f)); Next(112);
        }
        else if (stage == 112 && !action.IsActionActive)
        {
            Check(sampleHp - enemy.CurrentHealth == Mathf.RoundToInt(matrixSkill.Damages) && second.CurrentHealth == secondHp,
                "Two-enemy contact: hp=" + enemy.CurrentHealth + "/" + second.CurrentHealth + " initial=" + sampleHp + "/" + secondHp +
                " caster=" + player.transform.position + " victims=" + enemy.transform.position + "/" + second.transform.position);
            Check(matrixContext.TargetRoot == enemy.transform && matrixContext.ImpactConsumed, "Context changed or contact not consumed");
            Pass("two live enemies: stable captured victim, one actual contact, no double damage");
            second.gameObject.SetActive(false);
            manager.EndCombat();
            bridge.TeleportForSceneTransition(new Vector3(0, .02f, -6), Quaternion.identity); bridge.SetActionFacingDirection(Vector3.forward);
            sampleHp = enemy.CurrentHealth; skills.SetAnimationEventSkill(matrixSkill);
            Check(manager.TryUseSkill(matrixSkill), "Moving-target attack rejected");
            matrixContext = action.AttackContext;
            MoveEnemy(enemy, new Vector3(5, 0, -4.3f)); Next(113);
        }
        else if (stage == 113 && !action.IsActionActive)
        {
            Check(enemy.CurrentHealth == sampleHp && !manager.IsCombatActive && matrixContext.TargetRoot == enemy.transform,
                "Moving target was followed, replaced, or damaged outside contact");
            Pass("target moves away during preparation: captured identity retained, contact missed, no aggro");
            enemy.SetCinematicSuspended(true); MoveEnemy(enemy, new Vector3(100, 0, 100)); Next(120);
        }
        else if (stage == 120)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab");
            legacyObject = UnityEngine.Object.Instantiate(prefab, new Vector3(0, 0, -4.3f), Quaternion.identity);
            legacy = legacyObject.GetComponent<EnemyController>(); legacy.enabled = false;
            Set(legacy, "requiresFlameInfluence", false);
            legacy.Health.SetHealth(1000, 1000);
            defence = legacyObject.GetComponent<EnemyTacticalResponseController>() ?? legacyObject.AddComponent<EnemyTacticalResponseController>();
            defence.enabled = false; defence.Profile.guardedDamageMultiplier = 0;
            var state = typeof(EnemyTacticalResponseController).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
            state.SetValue(defence, Enum.Parse(state.FieldType, "Guarding"));
            bridge.TeleportForSceneTransition(new Vector3(0, .02f, -6), Quaternion.identity); Next(121);
        }
        else if (stage == 121 && elapsed > .25f && bridge.Grounded)
        {
            bridge.TeleportForSceneTransition(new Vector3(0, .02f, -6), Quaternion.identity); bridge.SetActionFacingDirection(Vector3.forward);
            sampleHp = legacy.Health.CurrentHp;
            skills.SetAnimationEventSkill(matrixSkill);
            Check(manager.TryUseSkill(matrixSkill) && action.AttackContext?.TargetRoot == legacy.transform, "Guard fixture not acquired");
            Next(122);
        }
        else if (stage == 122 && !action.IsActionActive)
        {
            Check(legacy.Health.CurrentHp == sampleHp && manager.IsCombatActive && !manager.HasLockedCombatTarget, "Absorbed contact must engage without HP loss/lock");
            Pass("Legacy guard absorbs 100%: zero HP lost, encounter engaged without lock");
            manager.EndCombat();
            var state = typeof(EnemyTacticalResponseController).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
            state.SetValue(defence, Enum.Parse(state.FieldType, "Dodging"));
            bridge.TeleportForSceneTransition(new Vector3(0, .02f, -6), Quaternion.identity); bridge.SetActionFacingDirection(Vector3.forward);
            skills.SetAnimationEventSkill(matrixSkill); Check(manager.TryUseSkill(matrixSkill), "Dodge fixture attack refused"); Next(123);
        }
        else if (stage == 123 && !action.IsActionActive)
        {
            Check(legacy.Health.CurrentHp == sampleHp && !manager.IsCombatActive, "Dodged contact engaged or damaged");
            Pass("Legacy dodge: no HP loss, no contact engagement");
            UnityEngine.Object.Destroy(legacyObject); Next(130);
        }
        else if (stage == 130)
        {
            if (SessionState.GetBool(ClashKey, false)) Time.captureDeltaTime = 1f / 60;
            arena.GetComponent<CombatLabClashController>().diagnostics = true;
            enemy.SetCinematicSuspended(false); MoveEnemy(enemy, new Vector3(0, 0, -4));
            enemy.SetCinematicSuspended(true); enemy.SetCinematicSuspended(false);
            enemy.combatProfile.attacks = new[] { enemy.combatProfile.attacks[0] };
            enemy.combatProfile.initialAttackDelay = 1f; enemy.combatProfile.attackInterval = new Vector2(10, 10);
            Set(enemy, "hurtRemaining", 0f); Set(enemy, "fluidCooldown", 0f); Set(enemy, "nextDetection", float.PositiveInfinity);
            enemy.transform.rotation = Quaternion.identity;
            bridge.TeleportForSceneTransition(enemy.transform.position + Vector3.forward * 2.2f, Quaternion.Euler(0, 180, 0));
            enemy.NotifyPlayerContact(player.transform);
            sampleHp = enemy.CurrentHealth; secondHp = player.CurrentHp;
            clashCount = arena.GetComponent<CombatLabClashController>().ResolvedCount; Next(131);
        }
        else if (stage == 131 && enemy.AttackPhase == LitBrainsAttackPhase.Preparation &&
            (float)typeof(LitBrainsEnemy).GetField("attackElapsed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(enemy) >= .02f)
        {
            matrixSkill = arena.basicSkills[0]; skills.SetAnimationEventSkill(matrixSkill);
            Debug.Log("[FreeAttackAcceptance] Clash launch at enemy elapsed=" +
                typeof(LitBrainsEnemy).GetField("attackElapsed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(enemy));
            Vector3 facing = Vector3.ProjectOnPlane(enemy.transform.position - player.transform.position, Vector3.up).normalized;
            bridge.TeleportForSceneTransition(player.transform.position, Quaternion.LookRotation(facing));
            bridge.SetActionFacingDirection(facing);
            Check(manager.TryUseSkill(matrixSkill), "Clash attack refused"); Next(132);
            matrixContext = action.AttackContext;
            Check(matrixContext?.TargetRoot == enemy.transform, "Clash fixture is not facing the enemy");
        }
        else if (stage == 132 && arena.GetComponent<CombatLabClashController>().ResolvedCount > clashCount)
        { Next(133); }
        else if (stage == 133 && elapsed > .65f)
        {
            Check(enemy.CurrentHealth == sampleHp && player.CurrentHp == secondHp && !action.IsActionActive && !bridge.IsScriptedPlanarMotionActive,
                "Clash damaged actors or leaked motion");
            Pass("real unlocked clash: both impacts neutralized, recovery releases UCC motion");
            if (SessionState.GetBool(ClashKey, false)) { Finish(0); return; }
            enemy.SetCinematicSuspended(true); enemy.SetCinematicSuspended(false); Freeze(enemy);
            bridge.TeleportForSceneTransition(enemy.transform.position - Vector3.forward * 2, Quaternion.identity); Next(140);
        }
        else if (stage == 140 && elapsed > .3f && bridge.Grounded && enemy.NavigationReady)
        {
            bridge.TeleportForSceneTransition(enemy.transform.position - Vector3.forward * 2, Quaternion.identity); bridge.SetActionFacingDirection(Vector3.forward);
            matrixSkill = player.CharacterData.chargedSkill; skills.SetAnimationEventSkill(matrixSkill);
            sampleHp = enemy.CurrentHealth; playerY = player.transform.position.y; enemyY = enemy.transform.position.y;
            Check(manager.TryUseSkill(matrixSkill) && !manager.HasLockedCombatTarget, "Unlocked projection rejected"); Next(141);
        }
        else if (stage == 141 && !action.IsActionActive && enemy.IsChargedAirborne)
        {
            Check(player.transform.position.y > playerY + 1.5f && enemy.transform.position.y > enemyY + 1.5f &&
                sampleHp - enemy.CurrentHealth == Mathf.RoundToInt(matrixSkill.Damages), "Projection missing or duplicated");
            Pass("unlocked Ascension: one launch impact, both actors airborne, commands restored");
            matrixSkill = player.CharacterData.airBasicSkills[0]; skills.SetAnimationEventSkill(matrixSkill);
            Check(manager.TryUseSkill(matrixSkill), "Manual aerial Basic rejected after projection"); Next(142);
        }
        else if (stage == 142 && !action.IsActionActive && bridge.Grounded && enemy.NavigationReady)
        {
            Check(bridge.TargetCollisionBypassCount == 0 && !bridge.IsExternalLockActive, "Air recovery leaked a lock");
            Pass("manual aerial Basic after projection, safe landing and navigation reattachment");
            Freeze(enemy); manager.EndCombat(); enemy.SetCinematicSuspended(true); MoveEnemy(enemy, new Vector3(100, 0, 100));
            platform = GameObject.CreatePrimitive(PrimitiveType.Cube); platform.name = "AcceptanceVoidPlatform";
            platform.transform.position = new Vector3(2000, -.15f, 2000); platform.transform.localScale = new Vector3(20, .3f, 4);
            Physics.SyncTransforms();
            Check(!Physics.Raycast(new Vector3(2011, 1, 2000), Vector3.down, 2, ~0, QueryTriggerInteraction.Ignore), "Void fixture has hidden ground");
            voidCase = 0; Next(150);
        }
        else if (stage == 150)
        {
            if (wall != null) UnityEngine.Object.Destroy(wall);
            if (voidCase >= 2)
            {
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(2008, 1, 2000); wall.transform.localScale = new Vector3(.2f, 2, 4);
            }
            bridge.TeleportForSceneTransition(new Vector3(2006, .02f, 2000), Quaternion.Euler(0, 90, 0)); Next(151);
        }
        else if (stage == 151 && elapsed > .3f && bridge.Grounded)
        {
            bridge.SetActionFacingDirection(Vector3.right); castOrigin = player.transform.position;
            matrixSkill = AssetDatabase.LoadAssetAtPath<SkillSO>(voidCase % 2 == 0 ? TargetMobilitySkillSetup.EclairPath : TargetMobilitySkillSetup.BondPath);
            skills.SetAnimationEventSkill(matrixSkill); Check(manager.TryUseSkill(matrixSkill), "Safe partial void dash rejected"); Next(152);
        }
        else if (stage == 152 && !action.IsActionActive)
        {
            float travel = Vector3.Dot(player.transform.position - castOrigin, Vector3.right);
            Check(travel > .1f && travel < (voidCase >= 2 ? 2f : 4.05f) && bridge.Grounded && player.transform.position.y > -.2f &&
                bridge.TargetCollisionBypassCount == 0 && !bridge.IsExternalLockActive && !bridge.IsScriptedPlanarMotionActive,
                "Dash crossed the void or leaked control: " + travel + " pos=" + player.transform.position);
            Pass(matrixSkill.SkillName + (voidCase >= 2 ? " stops before wall" : " stops before real void") + ", travel=" + travel.ToString("F2") + "m, grounded and clean");
            if (++voidCase < 4) Next(150);
            else
            {
                if (!physicalFailure) Pass(SessionState.GetBool(RemainingKey, false) ? "remaining physical scenarios completed; spatial/directional evidence in FreeAttackAcceptancePlayFinal.log" :
                    "all 24 direction/context cases and spatial/contact/physical scenarios completed");
                Finish(physicalFailure ? 2 : 0);
            }
        }
    }
}
