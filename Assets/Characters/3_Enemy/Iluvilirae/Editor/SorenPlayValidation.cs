using System;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SorenPlayValidation
{
    private const string Key = "Lit.Soren.Validation";
    private static double started;
    private static int stage;
    private static float at;
    private static Vector3 start;
    private static SquadCharacterController other;
    private static EnemyRevelation mark;
    private static SkillSO shortMark;
    private static int arrowHits, arrowDamage;
    private static float arrowImpactAt, originalAcceleration;
    private static bool immediateStun;
    static SorenPlayValidation() { EditorApplication.update += Tick; }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated project required.");
        EditorSettings.enterPlayModeOptionsEnabled = false;
        Start();
    }
    public static void RunBatchWithoutDomainReload()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated project required.");
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        Start();
    }
    private static void Start()
    {
        stage = 0; started = 0;
        arrowHits = arrowDamage = 0; arrowImpactAt = 0; immediateStun = false;
        SessionState.SetBool(Key, true); EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        // The authored lab now starts with Lucian. This unsaved fixture tests Soren.
        UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>().character = CombatLabCharacter.Soren;
        EditorApplication.isPlaying = true;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static SquadCharacterController Actor()
    {
        var root = new GameObject("ControlValidationCaster"); root.transform.position = new Vector3(50, 0, 50);
        var actor = root.AddComponent<SquadCharacterController>(); root.GetComponent<Rigidbody>().isKinematic = true;
        root.GetComponent<Animator>().enabled = false;
        var data = ScriptableObject.CreateInstance<CharacterData>();
        typeof(SquadCharacterController).GetField("characterData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor, data);
        actor.SetHealth(100, 100); return actor;
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < 80, "Soren validation timeout");
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.Player == null || arena.Enemy == null) return;
            var manager = arena.GetComponent<RealTimeCombatManager>();
            var effects = PlayerControlSkills.Get(arena.Player);
            var revelation = Resources.Load<SkillSO>("CombatControlSkills/Skill_Revelation");
            var entrave = Resources.Load<SkillSO>("CombatControlSkills/Skill_Entrave");
            var retreat = Resources.Load<SkillSO>("CombatControlSkills/Skill_PasDeRecul");
            if (stage == 0)
            {
                if (!arena.Enemy.NavigationReady) return;
                Require(arena.character == CombatLabCharacter.Soren && arena.Player.CharacterData.combatSkills.Count == 4, "Wrong character/loadout");
                var arrow = arena.Player.CharacterData.combatSkills[0];
                Require(arrow.damages == 10 && arrow.stunSeconds == 2 && arrow.cooldownSeconds == 10 && arrow.minimumHitDistance == 0 && arrow.maximumHitDistance == 30, "Arrow altered");
                arena.Enemy.SetCinematicSuspended(true);
                typeof(LitBrainsEnemy).GetField("currentHealth", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(arena.Enemy, 500);
                Require(manager.TryUseSkill(revelation) && !manager.TryUseSkill(revelation), "Revelation cooldown invalid");
                Require(arena.Enemy.ReceiveImpact(new CombatImpact(10, 0, CombatImpactOrigin.Basic)) == 12, "Basic not amplified / amplified twice");
                Require(arena.Enemy.ReceiveImpact(new CombatImpact(10, 0, CombatImpactOrigin.LightSkill)) == 12, "Light not amplified");
                Require(arena.Enemy.ReceiveImpact(new CombatImpact(10, 0, CombatImpactOrigin.Counter)) == 12, "Counter not amplified");
                Require(arena.Enemy.ReceiveImpact(new CombatImpact(10, 0, CombatImpactOrigin.Passive)) == 10, "Passive amplified");
                mark = arena.Enemy.GetComponent<EnemyRevelation>(); other = Actor();
                mark.Apply(other, 6, .2f); ulong newest = mark.Generation;
                effects.ClearCombatEffects(); Require(mark.IsActive && mark.Owner == other, "Other owner's mark cleared");
                mark.Apply(arena.Player, 6, .2f, newest - 1); Require(mark.Owner == other, "Stale generation replaced mark");
                Require(EnemyRevelation.ModifyDamage(arena.Enemy.transform, 10) == 12, "Marks stacked");
                PlayerControlSkills.Get(other).ClearCombatEffects();
                var original = arena.Enemy.transform.position;
                arena.Enemy.transform.position = arena.Player.transform.position + Vector3.right * 25.01f;
                Require(!effects.TryActivate(revelation, arena.Enemy), "Beyond 25 m accepted");
                arena.Enemy.transform.position = arena.Player.transform.position + Vector3.right * 25;
                Require(effects.TryActivate(revelation, arena.Enemy), "25 m rejected");
                arena.Enemy.transform.position = original;
                Require(manager.TryUseSkill(entrave) && !manager.TryUseSkill(entrave), "Entrave / cooldown failed");
                var zones = UnityEngine.Object.FindObjectsByType<EnemyEntraveZone>(FindObjectsInactive.Exclude);
                Require(zones.Length == 1, "Unexpected zone count");
                var zone = zones[0];
                Require(Mathf.Abs(EnemyEntraveZone.NavigationMultiplier(zone.transform.position + Vector3.right * 4) - .4f) < .001f, "Radius boundary wrong");
                Require(EnemyEntraveZone.NavigationMultiplier(zone.transform.position + Vector3.right * 4.01f) == 1, "Outside zone slowed");
                EnemyEntraveZone.Create(other, zone.transform.position, entrave);
                Require(Mathf.Abs(EnemyEntraveZone.NavigationMultiplier(zone.transform.position) - .4f) < .001f, "Zones stacked");
                PlayerControlSkills.Get(other).ClearCombatEffects();
                Require(zone.IsActive, "Other owner cleanup removed zone");
                arena.Enemy.transform.position = original + Vector3.up * 10;
                Require(!effects.TryActivate(entrave, arena.Enemy), "Groundless zone accepted");
                arena.Enemy.transform.position = original;
                float animatorSpeed = arena.Enemy.Animator.speed;
                arena.Enemy.SetCinematicSuspended(false);
                arena.Enemy.MoveTo(original + Vector3.forward, 3.6f);
                Require(Mathf.Abs(arena.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().speed - 1.44f) < .001f && arena.Enemy.Animator.speed == animatorSpeed, "Navigation slow changed animation / wrong physical speed");
                arena.Enemy.SetCinematicSuspended(true);
                shortMark = UnityEngine.Object.Instantiate(revelation); shortMark.control.durationSeconds = .12f;
                Require(effects.TryActivate(shortMark, arena.Enemy), "Short mark failed");
                var bridge = arena.Player.GetComponent<LitOpsiveLocomotionBridge>();
                var inputField = typeof(LitOpsiveLocomotionBridge).GetField("currentWorldMoveInput", BindingFlags.NonPublic | BindingFlags.Instance);
                for (int i = 0; i < 8; i++)
                {
                    var input = new Vector2(Mathf.Cos(i * Mathf.PI / 4), Mathf.Sin(i * Mathf.PI / 4));
                    inputField.SetValue(bridge, input);
                    Require(Vector3.Dot(effects.ResolveDirection(arena.Enemy.transform), new Vector3(input.x, 0, input.y)) > .999f, "Direction altered");
                }
                inputField.SetValue(bridge, Vector2.zero);
                start = arena.Player.transform.position; at = Time.time;
                var presentation = arena.Player.GetComponent<PlayerActionPresentationController>();
                typeof(PlayerActionPresentationController).GetField("debugContactMotion", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(presentation, true);
                Vector3 chosen = effects.ResolveDirection(arena.Enemy.transform);
                Require(manager.TryUseSkill(retreat), "Retreat failed | pos=" + start + " dir=" + chosen + " safe=" + presentation.GetSafePlanarTravel(chosen, 5) +
                    " cast=" + SkillHealing.CanCast(arena.Player) + " state=" + presentation.CanPlayState(retreat.AnimatorState) + " driving=" + bridge.IsDriving);
                Require(manager.GetSkillCooldownRemaining(retreat) > 0 && !manager.TryUseSkill(retreat), "Retreat cooldown invalid");
                stage = 1; return;
            }
            if (stage == 1)
            {
                if (Time.time - at < .65f) return;
                float distance = Vector3.ProjectOnPlane(arena.Player.transform.position - start, Vector3.up).magnitude;
                Require(Mathf.Abs(distance - 5) < .35f, "Retreat physical distance wrong: " + distance);
                Require(!effects.IsMoving && !arena.Player.GetComponent<PlayerActionPresentationController>().IsActionActive &&
                    !arena.Player.GetComponent<LitOpsiveLocomotionBridge>().IsExternalLockActive, "Movement retained locks");
                Require(!arena.GetComponent<CombatMobilityController>().IsDamageInvulnerable, "Retreat granted invulnerability");
                Require(!mark.IsActive && EnemyRevelation.ModifyDamage(arena.Enemy.transform, 10) == 10, "Mark did not expire");
                Require(EnemyEntraveZone.NavigationMultiplier(arena.Enemy.transform.position) < 1, "Zone moved or expired too soon");
                manager.EndCombat();
                Require(EnemyEntraveZone.NavigationMultiplier(arena.Enemy.transform.position) == 1, "Zone survived disengagement");
                var presentation = arena.Player.GetComponent<PlayerActionPresentationController>();
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = arena.Player.transform.position + Vector3.right * 1.2f + Vector3.up;
                wall.transform.localScale = new Vector3(.2f, 2, 3); Physics.SyncTransforms();
                Require(presentation.GetSafePlanarTravel(Vector3.right, 5) < 1, "Wall not clipping route");
                wall.SetActive(false); UnityEngine.Object.Destroy(wall); Physics.SyncTransforms();
                var floors = Physics.RaycastAll(arena.Player.transform.position + Vector3.up * .45f, Vector3.down, 1, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.normal.y >= .45f && h.collider.GetComponentInParent<SquadCharacterController>() == null && h.collider.GetComponentInParent<LitBrainsEnemy>() == null)
                    .Select(h => h.collider).Distinct().ToArray();
                Require(floors.Length > 0, "No test floor");
                try
                {
                    foreach (var floor in floors) floor.enabled = false; Physics.SyncTransforms();
                    Require(presentation.GetSafePlanarTravel(Vector3.left, 5) == 0 && !effects.TryMove(retreat, Vector3.left), "Void accepted");
                }
                finally { foreach (var floor in floors) if (floor != null) floor.enabled = true; Physics.SyncTransforms(); }
                var shortZone = UnityEngine.Object.Instantiate(entrave); shortZone.control.durationSeconds = .12f;
                EnemyEntraveZone.Create(other, arena.Enemy.transform.position, shortZone);
                Require(effects.TryMove(retreat, Vector3.left), "Interruption test movement failed");
                stage = 2; at = Time.time; return;
            }
            if (stage == 2)
            {
                if (Time.time - at < .16f) return;
                var presentation = arena.Player.GetComponent<PlayerActionPresentationController>();
                Require(presentation.TryReplaceWithDamageReaction(arena.hurtState, PlayerActionPresentationProfile.CreateDefault()), "Damage could not interrupt retreat");
                Require(!effects.IsMoving && !arena.Player.GetComponent<LitOpsiveLocomotionBridge>().HasOnlyScriptedMotionLock(effects), "Interrupted motion kept its slot");
                presentation.CancelAction();
                Require(!arena.Player.GetComponent<LitOpsiveLocomotionBridge>().IsExternalLockActive, "Damage recovery retained a lock");
                Require(EnemyEntraveZone.NavigationMultiplier(arena.Enemy.transform.position) == 1, "Zone failed natural expiration");
                manager.BeginExternalEncounter(arena.Player.transform, arena.Enemy);
                arena.Enemy.SetCinematicSuspended(false);
                var agent = arena.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                originalAcceleration = agent.acceleration; agent.acceleration = 0; agent.velocity = Vector3.zero;
                var arrow = arena.Player.CharacterData.combatSkills[0];
                manager.PlayerSkillImpactApplied += (skill, damage) =>
                {
                    if (skill != arrow) return;
                    arrowHits++; arrowDamage += damage; arrowImpactAt = Time.time;
                    immediateStun = arena.Enemy.IsCounterStunned;
                };
                Require(manager.TryUseSkill(arrow), "Existing arrow did not start on Soren");
                stage = 3; at = Time.time; return;
            }
            if (stage == 3)
            {
                Require(Time.time - at < 3, "Arrow authored event missing");
                if (arrowHits == 0 || Time.time - arrowImpactAt < .2f) return;
                Require(arrowHits == 1 && arrowDamage == 10 && immediateStun && arena.Enemy.IsCounterStunned, "Arrow impact/stun was not single and immediate");
                stage = 4; return;
            }
            if (stage == 4)
            {
                if (Time.time - arrowImpactAt < 2.2f) return;
                Require(arrowHits == 1 && !arena.Enemy.IsCounterStunned, "Arrow repeated or failed stun recovery");
                arena.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().acceleration = originalAcceleration;
                Debug.Log("[Soren] PLAY PASS | kit/arrow, marks origins/expiry/nonstack/ownership/generation, ranges, zones/ground/expiry/nonstack, navigation only, 8 directions, 5m UCC retreat/cooldown/no invulnerability, wall/void/interruption/locks, disengagement | domainReload=" + !EditorSettings.enterPlayModeOptionsEnabled);
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
        }
        catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
    }
}
