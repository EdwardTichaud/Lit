using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class NetworkCharacterInput
{
    private readonly Dictionary<SkillSO, float> healingReadyAt = new Dictionary<SkillSO, float>();
    private bool healingRequestPending;
    private PlayerRegenerationEffect serverRegeneration;
    private CombatDamageWorldFeedback remoteRegenerationIndicator;

    public bool TryRequestHealingSkill(SkillSO skill, SquadCharacterController ally = null, ICombatTarget enemy = null)
    {
        var runtime = GetComponent<Lit.Combat.Migration.Network.CombatNetworkActorRuntime>();
        if (runtime != null)
        {
            Transform recipient = skill != null && skill.healingTarget == SkillHealingTarget.EnemyLifeSteal ? enemy?.Root : ally != null ? ally.transform : null;
            return runtime.TryRequestSkill(skill, recipient != null ? recipient.GetComponentInParent<NetworkObject>() : null, transform.forward);
        }
        if (!IsSpawned || !IsOwner || healingRequestPending || skill == null || !skill.IsHealingSkill) return false;
        if (Resources.Load<SkillSO>("CombatSupportSkills/" + skill.name) != skill) return false;
        NetworkObjectReference target = default;
        if (skill.healingTarget == SkillHealingTarget.AllyRegeneration)
        {
            var identity = ally != null ? ally.GetComponentInParent<NetworkObject>() : null;
            if (identity == null || !identity.IsSpawned) return false;
            target = identity;
        }
        else if (skill.healingTarget == SkillHealingTarget.EnemyLifeSteal)
        {
            var identity = enemy?.Root != null ? enemy.Root.GetComponentInParent<NetworkObject>() : null;
            if (identity == null || !identity.IsSpawned) return false;
            target = identity;
        }
        healingRequestPending = true;
        UseHealingSkillServerRpc(skill.name, target);
        return true;
    }

    [ServerRpc]
    private void UseHealingSkillServerRpc(string assetName, NetworkObjectReference ally)
    {
        var skill = string.IsNullOrEmpty(assetName) || assetName.Length > 128 || assetName.Contains("/") || assetName.Contains("\\")
            ? null : Resources.Load<SkillSO>("CombatSupportSkills/" + assetName);
        bool allowed = GetComponent<Lit.Combat.Migration.Network.CombatNetworkActorRuntime>() == null &&
            skill != null && skill.IsHealingSkill && controller != null && controller.CharacterData != null &&
            controller.CharacterData.combatSkills != null && controller.CharacterData.combatSkills.Contains(skill) &&
            SkillHealing.CanCast(controller) && (!healingReadyAt.TryGetValue(skill, out float ready) || Time.time >= ready);
        bool drain = allowed && skill.healingTarget == SkillHealingTarget.EnemyLifeSteal;
        bool passive = allowed && (skill.healingTarget == SkillHealingTarget.AllyRegeneration || drain);
        bool passiveAccepted = false;
        if (passive && ally.TryGet(out var allyObject))
        {
            var recipient = allyObject.GetComponentInChildren<SquadCharacterController>();
            if (serverRegeneration == null)
            {
                serverRegeneration = controller.GetComponent<PlayerRegenerationEffect>();
                if (serverRegeneration == null) serverRegeneration = controller.gameObject.AddComponent<PlayerRegenerationEffect>();
                serverRegeneration.TickApplied += OnRegenerationTick;
                serverRegeneration.LifeStealTickApplied += OnLifeStealTick;
                serverRegeneration.Ended += OnRegenerationEnded;
            }
            ICombatTarget enemyTarget = null;
            if (drain)
            {
                var legacy = allyObject.GetComponentInChildren<EnemyController>();
                enemyTarget = legacy != null ? new LegacyCombatTarget(legacy) : allyObject.GetComponentInChildren<LitBrainsEnemy>();
            }
            passiveAccepted = drain ? serverRegeneration.TryAssignLifeSteal(controller, enemyTarget, skill) : serverRegeneration.TryAssign(controller, recipient, skill);
            if (passiveAccepted) RegenerationStateClientRpc(ally, true, skill.regenerationHealthPerTick, skill.regenerationTickSeconds, drain);
        }
        var healed = allowed && !passive ? SkillHealing.Apply(skill, controller) : null;
        bool accepted = passiveAccepted || healed != null && healed.Count > 0;
        if (accepted)
        {
            healingReadyAt[skill] = Time.time + Mathf.Max(0, skill.cooldownSeconds);
            if (healed != null) foreach (var actor in healed)
            {
                var identity = actor.GetComponentInParent<NetworkObject>();
                if (identity != null && identity.IsSpawned)
                    HealingHealthClientRpc(identity, actor.CurrentHp, actor.MaxHp);
            }
        }
        HealingResultClientRpc(assetName, accepted, accepted ? skill.cooldownSeconds : 0f,
            new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } });
    }

    private void OnRegenerationTick(SquadCharacterController recipient, int amount)
    {
        var identity = recipient.GetComponentInParent<NetworkObject>();
        if (IsSpawned && IsServer && identity != null && identity.IsSpawned)
            HealingHealthClientRpc(identity, recipient.CurrentHp, recipient.MaxHp, amount);
    }

    private void OnRegenerationEnded()
    {
        if (IsSpawned && IsServer) RegenerationStateClientRpc(default, false, 0, 1);
    }

    private void OnLifeStealTick(ICombatTarget enemy, int damage, int healing)
    {
        if (!IsServer || !IsSpawned) return;
        var root = enemy is UnityEngine.Object obj && obj == null ? null : enemy.Root;
        var identity = root != null ? root.GetComponentInParent<NetworkObject>() : null;
        if (identity != null && identity.IsSpawned) LifeStealDamageFeedbackClientRpc(identity, damage);
        var owner = controller.GetComponentInParent<NetworkObject>();
        if (healing > 0 && owner != null && owner.IsSpawned)
            HealingHealthClientRpc(owner, controller.CurrentHp, controller.MaxHp, healing);
    }

    [ClientRpc]
    private void LifeStealDamageFeedbackClientRpc(NetworkObjectReference enemy, int damage)
    {
        if (!IsServer && enemy.TryGet(out var identity))
            CombatDamageWorldFeedback.Show(identity.transform, damage, new Color(.9f, .35f, .4f), 2.1f);
    }

    [ClientRpc]
    private void RegenerationStateClientRpc(NetworkObjectReference target, bool active, int amount, float interval, bool lifeSteal = false)
    {
        if (IsServer) return;
        if (remoteRegenerationIndicator != null)
        {
            remoteRegenerationIndicator.gameObject.SetActive(false);
            Destroy(remoteRegenerationIndicator.gameObject);
        }
        remoteRegenerationIndicator = null;
        if (active && target.TryGet(out var identity))
        {
            if (lifeSteal)
            {
                remoteRegenerationIndicator = PlayerRegenerationEffect.CreateLifeStealIndicator(identity.transform, amount, interval);
                return;
            }
            var actor = identity.GetComponentInChildren<SquadCharacterController>();
            if (actor != null && actor.CurrentHp > 0)
                remoteRegenerationIndicator = PlayerRegenerationEffect.CreateIndicator(actor, amount, interval);
        }
    }

    private void ClearRegenerationResources()
    {
        if (serverRegeneration != null)
        {
            serverRegeneration.Clear();
            serverRegeneration.TickApplied -= OnRegenerationTick;
            serverRegeneration.LifeStealTickApplied -= OnLifeStealTick;
            serverRegeneration.Ended -= OnRegenerationEnded;
            serverRegeneration = null;
        }
        if (remoteRegenerationIndicator != null)
        {
            remoteRegenerationIndicator.gameObject.SetActive(false);
            Destroy(remoteRegenerationIndicator.gameObject);
        }
        remoteRegenerationIndicator = null;
    }

    [ClientRpc]
    private void HealingHealthClientRpc(NetworkObjectReference identity, int current, int maximum, int regenerationAmount = 0)
    {
        if (IsServer || !identity.TryGet(out var actor)) return;
        var target = actor.GetComponentInChildren<SquadCharacterController>();
        if (target != null && target.CurrentHp > 0 && current > 0)
        {
            target.SetHealth(current, maximum);
            if (regenerationAmount > 0)
                CombatDamageWorldFeedback.ShowMessage(target.transform, "+" + regenerationAmount + " PV", new Color(.3f, 1f, .6f), 2.1f);
        }
    }

    [ClientRpc]
    private void HealingResultClientRpc(string assetName, bool accepted, float cooldown, ClientRpcParams rpcParams = default)
    {
        healingRequestPending = false;
        if (!accepted || controller == null) return;
        var skill = Resources.Load<SkillSO>("CombatSupportSkills/" + assetName);
        RealTimeCombatManager.Instance?.ConfirmHealingSkill(skill, controller.transform, cooldown);
    }
}
