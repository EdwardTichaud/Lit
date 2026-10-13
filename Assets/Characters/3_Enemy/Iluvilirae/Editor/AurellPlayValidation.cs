using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class AurellPlayValidation
{
    private const string Key = "Lit.Aurell.Validation";
    private static double started;
    private static int stage;
    private static float stageAt;
    private static AurellValidationTarget victim;
    static AurellPlayValidation() { EditorApplication.update += Tick; }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated project required.");
        SessionState.SetBool(Key, true); EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath); EditorApplication.isPlaying = true;
    }
    public static void RunBatchWithoutDomainReload()
    {
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        RunBatch();
    }
    public static void RunBatchWithDomainReload()
    {
        EditorSettings.enterPlayModeOptionsEnabled = false;
        RunBatch();
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static SquadCharacterController Actor(string name, Vector3 position, bool justice = false)
    {
        var root = new GameObject(name); root.transform.position = position;
        var actor = root.AddComponent<SquadCharacterController>();
        root.GetComponent<Rigidbody>().isKinematic = true; root.GetComponent<Animator>().enabled = false;
        var data = ScriptableObject.CreateInstance<CharacterData>(); data.characterName = name;
        if (justice) data.combatSkills.Add(Resources.Load<SkillSO>("CombatSupportSkills/Skill_JusteRetour"));
        typeof(SquadCharacterController).GetField("characterData", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(actor, data);
        actor.SetHealth(100, 100); return actor;
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < 75, "Aurell validation timeout");
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            if (arena.character != CombatLabCharacter.Aurell) { arena.character = CombatLabCharacter.Aurell; arena.RestartEncounter(); return; }
            if (stage == 0)
            {
                var intercession = Resources.Load<SkillSO>("CombatSupportSkills/Skill_Intercession");
                var rempart = Resources.Load<SkillSO>("CombatSupportSkills/Skill_Rempart");
                var retour = Resources.Load<SkillSO>("CombatSupportSkills/Skill_JusteRetour");
                Require(intercession != null && rempart != null && retour != null, "Missing skills");
                Require(arena.Player.CharacterData.characterName == "Aurell" && arena.Player.CharacterData.combatSkills.Count == 4, "Wrong laboratory loadout");
                var owner = Actor("JusticeOwner", new Vector3(50, 0, 50), true);
                var ally = Actor("JusticeAlly", owner.transform.position + Vector3.right * 3);
                var effects = PlayerProtectionEffects.Get(owner);
                Require(SkillProtection.Activate(intercession, owner, ally), "Link failed");
                Require(ally.ApplyDamage(40, "test") == 10 && ally.CurrentHp == 90 && owner.CurrentHp == 85, "75% transfer / 50% reduction wrong");
                Require(Mathf.Abs(effects.Reserve - 15) < .01f, "Transferred damage counted as prevented");
                owner.SetHealth(100, 100); ally.SetHealth(100, 100); effects.ResetReserve();
                Require(SkillProtection.Activate(rempart, owner, null), "Aura failed");
                Require(ally.ApplyDamage(40, "test") == 7 && owner.CurrentHp == 93, "Multiplicative protection wrong");
                Require(Mathf.Abs(effects.Reserve - 26f) < .01f, "Protection counted twice or credited before final rounding");
                var guard = ally.gameObject.AddComponent<PlayerGuardState>(); guard.Set(true, .4f);
                owner.SetHealth(100, 100); ally.SetHealth(100, 100); effects.ResetReserve();
                Require(CombatProtectionDamage.ApplyAttack(ally, 11, "guard test", true) == 1 && owner.CurrentHp == 99, "Guard/protection rounding must occur at final shares only");
                guard.Set(false, .4f);
                effects.ResetReserve(); effects.AddPrevented(1000); Require(effects.Reserve == 50, "Reserve cap wrong");
                Require(effects.ConsumeReserve() == 50 && effects.ConsumeReserve() == 0, "Reserve double consumption");
                Require(SkillProtection.Activate(intercession, owner, owner), "Self link failed");
                owner.SetHealth(100, 100); Require(owner.ApplyDamage(40, "fall test") == 14, "Self reduction must be 65%");
                ally.transform.position = owner.transform.position + Vector3.right * 6.01f;
                ally.SetHealth(100, 100); Require(ally.ApplyDamage(10) == 10, "Aura radius not respected");
                var regen = ScriptableObject.CreateInstance<SkillSO>(); regen.healingTarget = SkillHealingTarget.AllyRegeneration; regen.healingRadius = 30;
                var passive = owner.gameObject.AddComponent<PlayerRegenerationEffect>();
                Require(passive.TryAssign(owner, ally, regen) && !effects.HasAura, "Regeneration did not replace Rempart");
                Require(SkillProtection.Activate(rempart, owner, null) && passive.Recipient == null, "Rempart did not replace regeneration");
                Require(SkillProtection.Activate(rempart, owner, null) && !effects.HasAura, "Aura toggle failed");
                Require(effects.BeginLink(intercession, ally), "Link refresh failed");
                ally.transform.position = owner.transform.position + Vector3.right * 30.01f;
                Require(!effects.HasLink && ally.ApplyDamage(10) == 10, "Broken link kept transferring damage");
                owner.gameObject.SetActive(false); Require(!effects.HasLink && effects.Reserve == 0 && !effects.HasAura, "Owner cleanup failed");
                UnityEngine.Object.Destroy(owner.gameObject); UnityEngine.Object.Destroy(ally.gameObject); UnityEngine.Object.Destroy(regen);
                arena.Enemy.SetCinematicSuspended(true);
                var combat = arena.GetComponent<RealTimeCombatManager>();
                combat.EndCombat();
                var targetObject = new GameObject("JusticeStrikeTarget");
                targetObject.transform.position = arena.Player.transform.position + arena.Player.transform.forward * 2;
                victim = targetObject.AddComponent<AurellValidationTarget>();
                combat.BeginExternalEncounter(arena.Player.transform, victim);
                PlayerProtectionEffects.Get(arena.Player).AddPrevented(50);
                Require(combat.TryUseSkill(retour), "Justice strike failed to start");
                Require(PlayerProtectionEffects.Get(arena.Player).Reserve == 0 && combat.GetSkillCooldownRemaining(retour) > 0, "Strike did not consume reserve / start cooldown");
                stage = 1; stageAt = Time.time;
                return;
            }
            if (stage == 1)
            {
                if (Time.time - stageAt < 1.3f) return;
                Require(victim.Damage == 50 && victim.Impacts == 1, "Authored strike event did not deliver one reserved impact: " + victim.Damage + "/" + victim.Impacts);
                var presentation = arena.Player.GetComponent<PlayerActionPresentationController>();
                Require(!presentation.IsActionActive && !arena.Player.GetComponent<LitOpsiveLocomotionBridge>().IsExternalLockActive, "Strike cleanup retained an action lock");
                presentation.HandleResolveSkillImpact();
                Require(victim.Impacts == 1, "Late event hit twice");
                PlayerProtectionEffects.Get(arena.Player).AddPrevented(20);
                arena.GetComponent<RealTimeCombatManager>().EndCombat();
                Require(PlayerProtectionEffects.Get(arena.Player).Reserve == 0, "Combat end retained reserve");
                var combat = arena.GetComponent<RealTimeCombatManager>();
                var intercession = Resources.Load<SkillSO>("CombatSupportSkills/Skill_Intercession");
                var rempart = Resources.Load<SkillSO>("CombatSupportSkills/Skill_Rempart");
                Require(combat.TryUseSkill(intercession) && !combat.TryUseSkill(intercession), "Intercession cooldown not enforced");
                Require(combat.TryUseSkill(rempart), "Rempart did not activate after strike");
                arena.Player.SetHealth(100, 100);
                var uccHealth = arena.Player.GetComponent<Opsive.UltimateCharacterController.Traits.CharacterHealth>();
                Require(uccHealth != null, "UCC health missing");
                uccHealth.Damage(40);
                Require(arena.Player.CurrentHp == 86, "External UCC hazard bypassed/doubled protection: " + arena.Player.CurrentHp);
                arena.Enemy.SetCinematicSuspended(false);
                var sommation = Resources.Load<SkillSO>("CombatSupportSkills/Skill_Sommation");
                Transform committed = arena.Enemy.CommittedAttackTarget;
                Require(combat.TryUseSkill(sommation) && arena.Enemy.GetComponent<EnemyTaunt>().Target == arena.Player, "Sommation failed");
                Require(arena.Enemy.CommittedAttackTarget == committed, "Sommation changed committed victim");
                arena.Enemy.SetCinematicSuspended(true);
                stage = 2; stageAt = Time.time;
            }
            else if (stage == 2)
            {
                if (Time.time - stageAt < 4.1f) return;
                var effects = PlayerProtectionEffects.Get(arena.Player);
                Require(!effects.HasLink && effects.HasAura, "Temporary link did not expire independently of aura");
                arena.GetComponent<RealTimeCombatManager>().BeginExternalEncounter(arena.Player.transform, victim);
                arena.Player.SetHealth(100, 100);
                Require(arena.Player.ApplyDamage(40) == 28, "Aura alone must reduce by 30%");
                arena.GetComponent<RealTimeCombatManager>().EndCombat();
                Require(effects.HasAura && effects.Reserve == 0, "Disengagement lost concentration or kept reserve");
                Debug.Log("[Aurell] PLAY PASS | transfer/self/aura, guard final rounding, reserve/cap, passive exclusivity, authored single impact, stale event, locks, UCC hazard, link range/expiry, cooldowns, taunt committed victim, disengagement | domainReload=" + (!EditorSettings.enterPlayModeOptionsEnabled || !EditorSettings.enterPlayModeOptions.HasFlag(EnterPlayModeOptions.DisableDomainReload)));
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
        }
        catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
    }
}

public sealed class AurellValidationTarget : MonoBehaviour, ICombatTarget
{
    public Transform Root => transform;
    public Transform LockPoint => transform;
    public Animator Animator => null;
    public bool IsDead => false;
    public bool IsAttackCommitted => false;
    public int ActionSequenceId => 0;
    public int Damage, Impacts;
    public int ReceiveDamage(int amount) { Damage += amount; Impacts++; return amount; }
    public void SetCinematicSuspended(bool value) { }
    public bool PlaceForCinematic(Vector3 position, Quaternion rotation) { transform.SetPositionAndRotation(position, rotation); return true; }
}
