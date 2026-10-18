using Unity.Netcode;
using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    [SerializeField] private SquadCharacterController supportTarget;
    public SquadCharacterController SupportTarget => supportTarget;

    public bool SetSupportTarget(SquadCharacterController target, bool allowSelf = false)
    {
        ResolvePlayerReferences();
        if (!(allowSelf && target == playerController && PlayerProtectionEffects.Alive(target)) &&
            !PlayerRegenerationEffect.IsValidRegenerationRecipient(playerController, target) &&
            !PlayerRegenerationEffect.IsValidAlly(playerController, target)) return false;
        supportTarget = target;
        return true;
    }

    public bool TryCycleSupportTarget(SkillSO skill)
    {
        ResolvePlayerReferences();
        bool intercession = skill != null && skill.IsProtectionSkill && skill.protection.kind == SkillProtectionKind.Intercession;
        if (skill == null || skill.healingTarget != SkillHealingTarget.AllyRegeneration && !intercession) return false;
        var allies = new System.Collections.Generic.List<SquadCharacterController>();
        foreach (var actor in FindObjectsByType<SquadCharacterController>(FindObjectsInactive.Exclude))
            if (PlayerRegenerationEffect.IsValidAlly(playerController, actor) &&
                Vector3.Distance(playerController.transform.position, actor.transform.position) <= (intercession ? skill.protection.radius : skill.healingRadius)) allies.Add(actor);
        if (intercession || PlayerRegenerationEffect.IsValidRegenerationRecipient(playerController, playerController)) allies.Add(playerController);
        allies.Sort((a, b) => a.GetEntityId().CompareTo(b.GetEntityId()));
        if (allies.Count == 0) { supportTarget = null; return false; }
        supportTarget = allies[(allies.IndexOf(supportTarget) + 1) % allies.Count];
        CombatDamageWorldFeedback.ShowMessage(supportTarget.transform, supportTarget.CharacterData.ResolveDisplayName(), new Color(.3f, 1f, .6f), 2.5f);
        return true;
    }

    private bool TryUseHealingSkill(SkillSO skill, PlayerAttackContext targeting = null)
    {
        ResolvePlayerReferences();
        if (IsCinematicSequenceActive || !SkillHealing.CanCast(playerController) || GetSkillCooldownRemaining(skill) > 0f) return false;
        bool passive = skill.healingTarget == SkillHealingTarget.AllyRegeneration;
        var recipient = targeting != null ? targeting.Ally : supportTarget;
        if (targeting == null && passive && PlayerRegenerationEffect.IsValidRegenerationRecipient(playerController, playerController)) recipient = playerController;
        bool drain = skill.healingTarget == SkillHealingTarget.EnemyLifeSteal;
        var enemyTarget = drain ? (targeting != null ? targeting.Target : LockedCombatTarget) : null;
        if (drain && (!PlayerRegenerationEffect.IsValidEnemy(playerController, enemyTarget) ||
            Vector3.Distance(playerController.transform.position, enemyTarget.Root.position) > skill.healingRadius)) return false;
        if (passive && (!PlayerRegenerationEffect.IsValidRegenerationRecipient(playerController, recipient) ||
            Vector3.Distance(playerController.transform.position, recipient.transform.position) > skill.healingRadius)) return false;
        var network = NetworkManager.Singleton;
        if (network != null && network.IsListening)
        {
            var relay = playerController.GetComponentInParent<NetworkCharacterInput>();
            if (relay == null || !relay.IsSpawned) return false;
            return relay.TryRequestHealingSkill(skill, passive ? recipient : null, enemyTarget);
        }
        if (passive || drain)
        {
            var effect = playerController.GetComponent<PlayerRegenerationEffect>();
            if (effect == null) effect = playerController.gameObject.AddComponent<PlayerRegenerationEffect>();
            bool assigned = drain ? effect.TryAssignLifeSteal(playerController, enemyTarget, skill) : effect.TryAssign(playerController, recipient, skill);
            if (!assigned) return false;
            ConfirmHealingSkill(skill, playerRoot, 0);
            return true;
        }
        if (SkillHealing.Apply(skill, playerController).Count == 0) return false;
        ConfirmHealingSkill(skill, playerRoot, skill.cooldownSeconds);
        return true;
    }

    public void ConfirmHealingSkill(SkillSO skill, Transform caster, float cooldown)
    {
        if (skill == null || caster == null) return;
        if (playerController != null && caster == playerController.transform && playerRoot != null) caster = playerRoot;
        if (!skillCooldowns.TryGetValue(caster, out var actorCooldowns))
            skillCooldowns[caster] = actorCooldowns = new System.Collections.Generic.Dictionary<SkillSO, float>();
        actorCooldowns[skill] = Time.time + Mathf.Max(0, cooldown);
        if (caster != playerRoot) return;
        PlayPlayerSkillStartSfx(skill, true);
        PlayerSkillStarted?.Invoke(skill, null);
    }
}
