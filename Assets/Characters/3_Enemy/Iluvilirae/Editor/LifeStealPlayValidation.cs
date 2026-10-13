using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LifeStealPlayValidation
{
    private const string Key = "Lit.LifeSteal.Validation";
    private sealed class Target : ICombatTarget
    {
        public Transform Root { get; set; }
        public Transform LockPoint => Root;
        public Animator Animator => null;
        public bool IsDead => Health <= 0;
        public bool IsAttackCommitted => false;
        public int ActionSequenceId => 0;
        public int Health = 10;
        public bool Blocked;
        public int ReceiveDamage(int damage) { int applied = Blocked ? 0 : Mathf.Min(Health, damage); Health -= applied; return applied; }
        public void SetCinematicSuspended(bool suspended) { }
        public bool PlaceForCinematic(Vector3 position, Quaternion rotation) => false;
    }
    private static double started;
    private static float stageTime;
    private static int stage, hp;
    private static Target target;
    private static SkillSO skill;
    private static PlayerRegenerationEffect effect;
    private static SquadCharacterController ally;
    static LifeStealPlayValidation() { EditorApplication.update += Tick; }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated project required.");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        EditorApplication.isPlaying = true;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Next(int value) { stage = value; stageTime = Time.time; }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < 90, "Life steal timeout stage=" + stage);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            var manager = arena.GetComponent<RealTimeCombatManager>();
            var player = arena.Player;
            if (stage == 0)
            {
                Time.captureDeltaTime = 1f / 60;
                arena.Enemy.SetCinematicSuspended(true);
                player.SetHealth(50, 100);
                target = new Target { Root = new GameObject("LifeStealValidationTarget").transform };
                target.Root.position = player.transform.position + Vector3.right * 10.01f;
                manager.BeginExternalEncounter(player.transform, target);
                arena.Enemy.SetCinematicSuspended(true);
                skill = Resources.Load<SkillSO>("CombatSupportSkills/Skill_SiphonVital");
                Require(skill != null && !manager.TryUseSkill(skill), "Out of range launch accepted");
                target.Root.position = player.transform.position + Vector3.right * 10;
                Require(manager.TryUseSkill(skill), "10m inclusive launch failed");
                effect = player.GetComponent<PlayerRegenerationEffect>();
                Require(effect != null && effect.EnemyRecipient == target && effect.Recipient == null, "Wrong passive target");
                Require(!manager.TryUseSkill(skill) && manager.GetSkillCooldownRemaining(skill) == 0, "Duplicate passive or cooldown");
                Require(player.CurrentHp == 50 && target.Health == 10, "Immediate tick before one second");
                target.Root.position = player.transform.position + Vector3.right * 5;
                Next(1);
            }
            if (stage == 1 && Time.time - stageTime > 1.15f)
            {
                Require(player.CurrentHp == 51 && target.Health == 9, $"1 PV/s transfer incorrect: caster={player.CurrentHp}, enemy={target.Health}, effectEnabled={effect.isActiveAndEnabled}, active={effect.EnemyRecipient != null}, range={Vector3.Distance(player.transform.position, target.Root.position)}, time={Time.time - stageTime}");
                target.Root.position = player.transform.position + Vector3.right * 15;
                Next(2);
            }
            if (stage == 2 && Time.time - stageTime > 1.5f)
            {
                Require(player.CurrentHp == 51 && target.Health == 9 && effect.EnemyRecipient == target, $"Out of range tick applied: caster={player.CurrentHp}, enemy={target.Health}, active={effect.EnemyRecipient != null}, range={Vector3.Distance(player.transform.position, target.Root.position)}");
                target.Root.position = player.transform.position;
                Next(3);
            }
            if (stage == 3 && Time.time - stageTime > .65f)
            {
                Require(player.CurrentHp == 51 && target.Health == 9, "Backlogged tick on return to range");
                Next(4);
            }
            if (stage == 4 && Time.time - stageTime > .5f)
            {
                Require(player.CurrentHp == 52 && target.Health == 8, "0m inclusive resume failed");
                target.Blocked = true;
                Next(5);
            }
            if (stage == 5 && Time.time - stageTime > 1.15f)
            {
                Require(player.CurrentHp == 52 && target.Health == 8, "Healing without actual stolen HP");
                target.Blocked = false; target.Health = 1; player.SetCurrentHp(99);
                Next(6);
            }
            if (stage == 6 && Time.time - stageTime > 1.15f)
            {
                Require(player.CurrentHp == 100 && target.IsDead && effect.EnemyRecipient == null, "Death/HP cap cleanup failed");
                var root = new GameObject("LifeStealValidationAlly");
                root.transform.position = player.transform.position + Vector3.right * 2;
                ally = root.AddComponent<SquadCharacterController>();
                root.GetComponent<Rigidbody>().isKinematic = true;
                var data = ScriptableObject.CreateInstance<CharacterData>();
                typeof(SquadCharacterController).GetField("characterData", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ally, data);
                ally.SetHealth(50, 100);
                var regeneration = Resources.Load<SkillSO>("CombatSupportSkills/Skill_Regeneration");
                Require(manager.SetSupportTarget(ally) && manager.TryUseSkill(regeneration), "Regeneration cast failed");
                target.Health = 10;
                manager.BeginExternalEncounter(player.transform, target);
                Require(manager.TryUseSkill(skill) && effect.Recipient == null && effect.EnemyRecipient == target,
                    "Two simultaneous passives survived switch");
                hp = target.Health;
                Next(7);
            }
            if (stage == 7 && Time.time - stageTime > 1.15f)
            {
                Require(ally.CurrentHp == 50 && target.Health == hp - 1 && player.CurrentHp == 100, "Full HP/passive replacement incorrect");
                effect.enabled = false;
                Require(effect.EnemyRecipient == null, "Disabled effect retained enemy");
                Debug.Log("[LifeSteal] PLAY PASS | 0/10 inclusive, >10 reject/suspend, 1 PV/s actual transfer, no backlog, blocked damage no heal, HP cap, death cleanup, shared passive slot, zero cooldown, disable");
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception); SessionState.SetBool(Key, false); EditorApplication.Exit(1);
        }
    }
}
