using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TargetMobilityPlayValidation
{
    private const string Key = "Lit.TargetMobility.Play";
    private static LucianBrainsCombatArena arena;
    private static SkillSO currentSkill;
    private static int step, caseIndex, enemyHp;
    private static float stepStarted;
    private static double started;
    private static Vector3 enemyPosition = new Vector3(-12, 0, 0);
    private static readonly float[] Distances = { 2, 5, 10, 10.01f, 1.99f, 2, 10, 30, 30.01f };
    private static readonly bool[] Expected = { true, true, true, false, false, true, true, true, false };
    private static GameObject wall;
    private static bool interrupted;
    private static float nextReplayTrace;
    private static readonly System.Collections.Generic.List<Collider> disabledFloors = new System.Collections.Generic.List<Collider>();
    static TargetMobilityPlayValidation() { EditorApplication.update += Tick; }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch project required.");
        var type = typeof(TargetMobilityPlayValidation).Assembly.GetType("MainMenuMissingPrefabRepair");
        if (type != null)
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            var method = type.GetMethod("RepairMissingPrefab", BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null) EditorApplication.delayCall -= (EditorApplication.CallbackFunction)Delegate.CreateDelegate(typeof(EditorApplication.CallbackFunction), method);
        }
        TargetMobilitySkillSetup.Configure();
        SessionState.SetInt(Key + ".StartCase", 0);
        SessionState.SetBool(Key, true); EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        EditorApplication.isPlaying = true;
    }
    public static void RunReplayBatch() { RunBatch(); SessionState.SetInt(Key + ".StartCase", 12); }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) { started = EditorApplication.timeSinceStartup; caseIndex = SessionState.GetInt(Key + ".StartCase", 0); }
            Require(EditorApplication.timeSinceStartup - started < 150, "Timeout case=" + caseIndex + " step=" + step);
            arena ??= UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            var player = arena.Player;
            var bridge = player.GetComponent<LitOpsiveLocomotionBridge>();
            var presentation = player.GetComponent<PlayerActionPresentationController>();
            var manager = arena.GetComponent<RealTimeCombatManager>();
            if (step == 0)
            {
                Time.captureDeltaTime = 1f / 60;
                typeof(PlayerActionPresentationController).GetField("debugContactMotion", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(presentation, true);
                arena.Enemy.SetCinematicSuspended(true);
                arena.Enemy.transform.position = enemyPosition;
                typeof(LitBrainsEnemy).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(arena.Enemy, 1000);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "MobilityValidationFloor"; floor.transform.position = new Vector3(-12, -.1f, 0);
                floor.transform.localScale = new Vector3(12, .2f, 80);
                step = 1;
            }
            if (step == 1)
            {
                presentation.CancelAction();
                if (currentSkill != null) UnityEngine.Object.Destroy(currentSkill);
                bool bond = caseIndex >= 4 && caseIndex < 9;
                currentSkill = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillSO>(bond ? TargetMobilitySkillSetup.BondPath : TargetMobilitySkillSetup.EclairPath));
                currentSkill.impactFeedback.enabled = false;
                float distance = caseIndex < 9 ? Distances[caseIndex] : caseIndex == 10 ? 2 : 5;
                if (caseIndex == 9)
                {
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "MobilityValidationWall";
                    wall.transform.position = enemyPosition + new Vector3(0, 2, 2);
                    wall.transform.localScale = new Vector3(6, 4, .4f);
                }
                if (caseIndex == 10)
                {
                    wall = new GameObject("MobilityValidationBattleWall"); wall.transform.position = enemyPosition;
                    var sphere = wall.AddComponent<SphereCollider>(); sphere.radius = 3; sphere.isTrigger = true;
                    wall.AddComponent<BattleWallContainment>().Initialize(null, player.transform);
                }
                if (caseIndex == 11)
                {
                    foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                        if (collider.enabled && collider.bounds.size.x >= 10 && collider.bounds.size.z >= 10 && collider.bounds.max.y < .2f)
                        { disabledFloors.Add(collider); collider.enabled = false; }
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "MobilityValidationIsland";
                    wall.transform.position = enemyPosition + new Vector3(0, -.1f, -7.5f);
                    wall.transform.localScale = new Vector3(12, .2f, 19);
                }
                if (caseIndex == 13)
                {
                    var capsule = arena.Enemy.GetComponent<CapsuleCollider>();
                    capsule.height = 3; capsule.center = Vector3.up * 1.5f; capsule.radius = 1.2f;
                }
                interrupted = false;
                bridge.SetExternalPositionAndRotation(enemyPosition - Vector3.forward * distance + Vector3.up * .1f, Quaternion.identity, true);
                player.SetHealth(1000, 1000);
                stepStarted = Time.time; step = 2;
            }
            if (step == 2 && Time.time - stepStarted > .15f)
            {
                UnityEngine.Object.FindAnyObjectByType<SkillsManager>().SetAnimationEventSkill(currentSkill);
                enemyHp = arena.Enemy.CurrentHealth;
                bool launched = manager.TryUseSkill(currentSkill);
                if (caseIndex == 13) Debug.Log($"[TargetMobility] Large target launch={launched} | {bridge.DriveDiagnostic} | locked={bridge.IsExternalLockActive}");
                bool expected = caseIndex < 9 ? Expected[caseIndex] : true;
                Require(launched == expected, "Wrong launch result case " + caseIndex + " distance=" + (caseIndex < 9 ? Distances[caseIndex] : 5));
                if (!expected) { Require(!presentation.IsActionActive && bridge.TargetCollisionBypassCount == 0, "Rejected skill acquired resources"); Next(); return; }
                stepStarted = Time.time; step = 3;
            }
            if (step == 3 && Time.time - stepStarted > 1.8f)
            {
                Debug.Log($"[TargetMobility] Result {caseIndex} | position={player.transform.position} HP={arena.Enemy.CurrentHealth} end={presentation.LastEndReason}");
                Require(arena.Enemy.CurrentHealth == enemyHp - (caseIndex == 12 ? 0 : 10), "Missing/double damage case=" + caseIndex + " HP=" + arena.Enemy.CurrentHealth + " expected=" + (enemyHp - 10));
                float offset = Vector3.Dot(player.transform.position - enemyPosition, Vector3.forward);
                bool bond = caseIndex >= 4 && caseIndex < 9;
                Require(bond ? offset < -.6f && offset > -1.6f : caseIndex == 9 ? offset < 1.5f && offset > .6f :
                    caseIndex == 10 ? offset < 2.6f && offset > 1.8f : caseIndex == 11 ? offset < 1.85f && offset > 1 :
                    caseIndex == 12 ? interrupted && offset < -.6f : Mathf.Abs(offset - 4) < .35f,
                    "Wrong destination case=" + caseIndex + " offset=" + offset);
                Require(bridge.TargetCollisionBypassCount == 0 && !bridge.IsScriptedPlanarMotionActive && !presentation.IsActionActive,
                    "Resources survived case " + caseIndex);
                Require(Mathf.Abs(player.transform.position.y - enemyPosition.y) < .3f, "Unexpected vertical impulse");
                Debug.Log($"[TargetMobility] Case {caseIndex} PASS | offset={offset:F3} | damage={(caseIndex == 12 ? 0 : 10)} | collisions restored");
                Next();
            }
            if (step == 3 && caseIndex == 13 && Time.time - stepStarted >= nextReplayTrace && nextReplayTrace < .4f)
            {
                nextReplayTrace += .05f;
                var animator = player.GetComponent<Animator>(); var state = animator.GetCurrentAnimatorStateInfo(0);
                var next = animator.GetNextAnimatorStateInfo(0); var clips = animator.GetCurrentAnimatorClipInfo(0);
                Debug.Log($"[TargetMobility] Replay | time={Time.time - stepStarted:F3} current={state.shortNameHash}/{state.normalizedTime:F3} next={next.shortNameHash}/{next.normalizedTime:F3} transition={animator.IsInTransition(0)} active={presentation.IsActionActive} clip={(clips.Length > 0 ? clips[0].clip.name : "none")}");
            }
            if (step == 3 && caseIndex == 12 && !interrupted && Time.time - stepStarted > .08f && bridge.TargetCollisionBypassCount > 0)
            {
                presentation.CancelAction(); interrupted = true;
                Require(bridge.TargetCollisionBypassCount == 0 && !bridge.IsScriptedPlanarMotionActive, "Interrupted skill kept resources");
            }
        }
        catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
    }
    private static void Next()
    {
        if (wall != null) UnityEngine.Object.Destroy(wall);
        foreach (var floor in disabledFloors) if (floor != null) floor.enabled = true;
        disabledFloors.Clear();
        caseIndex++; step = 1;
        if (caseIndex <= 13) return;
        TestCollisionLeases();
        Debug.Log("[TargetMobility] PLAY PASS | launch boundaries, traversal, 30m approach, wall, BattleWall, cliff, interruption, large target, nested collision leases");
        SessionState.SetBool(Key, false); EditorApplication.Exit(0);
    }

    private static void TestCollisionLeases()
    {
        var bridge = arena.Player.GetComponent<LitOpsiveLocomotionBridge>();
        var body = bridge.ScriptedMotionColliders[0];
        var target = arena.Enemy.GetComponent<CapsuleCollider>();
        bool original = Physics.GetIgnoreCollision(body, target);
        Physics.IgnoreCollision(body, target, true);
        int layer = target.gameObject.layer;
        Require(bridge.TryAcquireExternalLock(new object(), out var external), "External lease refused");
        Require(bridge.TryAcquireTargetCollisionBypass(new object(), arena.Enemy.transform, out var a), "First collision lease refused");
        Require(bridge.TryAcquireTargetCollisionBypass(new object(), arena.Enemy.transform, out var b), "Second collision lease refused");
        a.Dispose(); a.Dispose();
        Require(!external.IsReleased && bridge.TargetCollisionBypassCount == 1, "Cleanup stole another lease");
        b.Dispose();
        Require(Physics.GetIgnoreCollision(body, target) && target.gameObject.layer == layer, "Original collision state lost");
        external.Dispose();
        Require(bridge.TryAcquireTargetCollisionBypass(new object(), arena.Enemy.transform, out var c), "Disable test lease refused");
        bridge.enabled = false;
        Require(c.IsReleased && bridge.TargetCollisionBypassCount == 0 && Physics.GetIgnoreCollision(body, target), "Disable did not restore collisions");
        Physics.IgnoreCollision(body, target, original);
        bridge.enabled = true;
    }
}
