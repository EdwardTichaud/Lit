using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RegenerationPlayValidation
{
    private const string Key = "Lit.Regeneration.Validation";
    private static double started;
    private static float stageTime;
    private static int stage, oldHealth;
    private static SquadCharacterController a, b;
    private static PlayerRegenerationEffect effect;
    private static SkillSO skill;
    static RegenerationPlayValidation() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated project required.");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        EditorApplication.isPlaying = true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static SquadCharacterController Actor(Vector3 position)
    {
        var root = new GameObject("RegenerationValidationAlly");
        root.transform.position = position;
        var actor = root.AddComponent<SquadCharacterController>();
        root.GetComponent<Rigidbody>().isKinematic = true;
        root.GetComponent<Animator>().enabled = false;
        var data = ScriptableObject.CreateInstance<CharacterData>();
        typeof(SquadCharacterController).GetField("characterData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor, data);
        actor.SetHealth(50, 100);
        return actor;
    }

    private static bool HasLabel(SquadCharacterController actor)
    {
        foreach (var label in actor.GetComponentsInChildren<CombatDamageWorldFeedback>()) if (label.IsPersistent) return true;
        return false;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < 90, "Regeneration timeout stage=" + stage);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            var manager = arena.GetComponent<RealTimeCombatManager>();
            if (stage == 0)
            {
                Time.captureDeltaTime = 1f / 60;
                arena.Enemy.SetCinematicSuspended(true);
                skill = Resources.Load<SkillSO>("CombatSupportSkills/Skill_Regeneration");
                Require(skill != null && skill.cooldownSeconds == 0, "Passive asset invalid");
                a = Actor(arena.Player.transform.position + Vector3.right * 4);
                b = Actor(arena.Player.transform.position + Vector3.right * 7);
                Require(!manager.SetSupportTarget(arena.Player), "Self accepted as ally");
                Require(!manager.TryUseSkill(skill), "Unselected ally was automatically targeted");
                Require(manager.SetSupportTarget(a) && manager.TryUseSkill(skill), "Explicit cast failed");
                Require(manager.HasLockedCombatTarget, "Support targeting changed enemy lock");
                effect = arena.Player.GetComponent<PlayerRegenerationEffect>();
                Require(effect != null && effect.Recipient == a && HasLabel(a), "Passive indicator missing");
                Require(a.CurrentHp == 50, "Passive healed before first second");
                Require(!manager.TryUseSkill(skill), "Spam duplicated/refreshed passive");
                stageTime = Time.time; stage = 1;
            }
            if (stage == 1 && Time.time - stageTime > 1.15f)
            {
                Require(a.CurrentHp == 51 && b.CurrentHp == 50, "1 PV/s tick incorrect");
                Require(manager.GetSkillCooldownRemaining(skill) == 0, "Passive has cooldown");
                oldHealth = a.CurrentHp;
                Require(manager.SetSupportTarget(b) && manager.TryUseSkill(skill) && effect.Recipient == b, "Transfer failed");
                Require(arena.Player.GetComponents<PlayerRegenerationEffect>().Length == 1, "Duplicate caster effect");
                manager.EndCombat();
                stageTime = Time.time; stage = 2;
            }
            if (stage == 2 && Time.time - stageTime > 2.15f)
            {
                Require(a.CurrentHp == oldHealth && b.CurrentHp == 52, "Previous ally kept healing or EndCombat removed passive");
                Require(!HasLabel(a) && HasLabel(b), "Transfer left orphan indicator");
                b.SetCurrentHp(100);
                stageTime = Time.time; stage = 3;
            }
            if (stage == 3 && Time.time - stageTime > 1.15f)
            {
                Require(b.CurrentHp == 100 && effect.Recipient == b, "Full HP ended effect or overflowed HP");
                b.SetCurrentHp(0);
                stageTime = Time.time; stage = 4;
            }
            if (stage == 4 && Time.time - stageTime > .15f)
            {
                Require(effect.Recipient == null && !HasLabel(b) && b.CurrentHp == 0, "Death retained passive or resurrected target");
                Require(manager.SetSupportTarget(a) && manager.TryUseSkill(skill), "Reapply after death failed");
                effect.enabled = false;
                stageTime = Time.time; stage = 5;
            }
            if (stage == 5 && Time.time - stageTime > .15f)
            {
                Require(effect.Recipient == null && !HasLabel(a), "Disable retained resources");
                Debug.Log("[Regeneration] PLAY PASS | explicit ally, 1 PV/s, zero cooldown, one effect, transfer, lock preserved, persists EndCombat, HP cap, target death, disable, indicators cleaned");
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(1);
        }
    }
}
